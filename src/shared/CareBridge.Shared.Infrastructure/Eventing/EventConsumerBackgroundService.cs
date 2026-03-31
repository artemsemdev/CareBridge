using System.Text;
using System.Text.Json;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Contracts.Serialization;
using CareBridge.Shared.Infrastructure.Correlation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CareBridge.Shared.Infrastructure.Eventing;

// Security: RabbitMQ credentials loaded from configuration (env var or Key Vault in production).
// Audit: Event consumers process domain events that create audit trail entries.
// Messages carry correlation metadata for distributed tracing — not user tokens.
public class EventConsumerBackgroundService : BackgroundService
{
    private const string ExchangeName = "carebridge.events";
    private const int MaxRetries = 3;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventConsumerBackgroundService> _logger;
    private readonly string _queueName;
    private readonly string[] _routingKeys;
    private readonly Dictionary<string, Type> _eventTypeMap;
    private IConnection? _connection;
    private IChannel? _channel;

    public EventConsumerBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<EventConsumerBackgroundService> logger,
        EventConsumerOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _queueName = options.QueueName;
        _routingKeys = options.RoutingKeys;
        _eventTypeMap = options.EventTypeMap;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _serviceProvider.GetRequiredService<IConfiguration>()["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(_serviceProvider.GetRequiredService<IConfiguration>()["RabbitMQ:Port"] ?? "5672"),
            UserName = _serviceProvider.GetRequiredService<IConfiguration>()["RabbitMQ:User"] ?? "guest",
            Password = _serviceProvider.GetRequiredService<IConfiguration>()["RabbitMQ:Password"] ?? "guest"
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync(_queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        foreach (var routingKey in _routingKeys)
        {
            await _channel.QueueBindAsync(_queueName, ExchangeName, routingKey, cancellationToken: stoppingToken);
        }

        await _channel.BasicQosAsync(0, 10, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                await ProcessMessageAsync(ea, stoppingToken);
                await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue {Queue}", _queueName);
                var retryCount = GetRetryCount(ea.BasicProperties);
                if (retryCount < MaxRetries)
                {
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                }
                else
                {
                    _logger.LogWarning("Message exceeded max retries ({MaxRetries}), discarding. DeliveryTag: {DeliveryTag}",
                        MaxRetries, ea.DeliveryTag);
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
            }
        };

        await _channel.BasicConsumeAsync(_queueName, false, consumer, stoppingToken);

        _logger.LogInformation("Event consumer started. Queue: {Queue}, RoutingKeys: {RoutingKeys}",
            _queueName, string.Join(", ", _routingKeys));

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var routingKey = ea.RoutingKey;
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());
        var correlationId = ea.BasicProperties.CorrelationId ?? string.Empty;

        using var scope = _serviceProvider.CreateScope();
        var correlationAccessor = scope.ServiceProvider.GetRequiredService<CorrelationIdAccessor>();
        correlationAccessor.Set(correlationId);

        if (!_eventTypeMap.TryGetValue(routingKey, out var eventType))
        {
            _logger.LogWarning("No handler registered for routing key {RoutingKey}", routingKey);
            return;
        }

        var @event = JsonSerializer.Deserialize(body, eventType, JsonDefaults.Options);
        if (@event == null)
        {
            _logger.LogWarning("Failed to deserialize event for routing key {RoutingKey}", routingKey);
            return;
        }

        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handler = scope.ServiceProvider.GetService(handlerType);
        if (handler == null)
        {
            _logger.LogWarning("No IEventHandler<{EventType}> registered", eventType.Name);
            return;
        }

        var method = handlerType.GetMethod("HandleAsync")!;
        await (Task)method.Invoke(handler, [@event, ct])!;

        _logger.LogInformation("Processed event {EventType} with CorrelationId: {CorrelationId}",
            routingKey, correlationId);
    }

    private static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers != null && properties.Headers.TryGetValue("x-retry-count", out var value))
        {
            return Convert.ToInt32(value);
        }
        return 0;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}

public class EventConsumerOptions
{
    public string QueueName { get; set; } = string.Empty;
    public string[] RoutingKeys { get; set; } = [];
    public Dictionary<string, Type> EventTypeMap { get; set; } = new();
}

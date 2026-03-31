using System.Text;
using System.Text.Json;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Contracts.Serialization;
using CareBridge.Shared.Infrastructure.Correlation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace CareBridge.Shared.Infrastructure.Eventing;

// Security: RabbitMQ credentials loaded from configuration (env var or Key Vault in production).
// In production, TLS is required for AMQP connections. See security-and-compliance.md §Encryption.
// Audit: All published events are consumed by the Audit Service for immutable audit trail.
public class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private const string ExchangeName = "carebridge.events";

    private readonly ICorrelationIdAccessor _correlationAccessor;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly IConnection _connection;
    private readonly IChannel _channel;

    public RabbitMqEventPublisher(
        ICorrelationIdAccessor correlationAccessor,
        IConfiguration configuration,
        ILogger<RabbitMqEventPublisher> logger)
    {
        _correlationAccessor = correlationAccessor;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:User"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        _channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true).GetAwaiter().GetResult();
    }

    public async Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IntegrationEvent
    {
        var routingKey = typeof(T).Name;
        var json = JsonSerializer.Serialize(@event, JsonDefaults.Options);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = _correlationAccessor.CorrelationId,
            MessageId = @event.EventId.ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                ["event-type"] = typeof(T).FullName,
                ["correlation-id"] = _correlationAccessor.CorrelationId
            }
        };

        await _channel.BasicPublishAsync(ExchangeName, routingKey, false, properties, body, ct);

        _logger.LogInformation("Published event {EventType} with Id {EventId}, CorrelationId: {CorrelationId}",
            routingKey, @event.EventId, _correlationAccessor.CorrelationId);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }
}

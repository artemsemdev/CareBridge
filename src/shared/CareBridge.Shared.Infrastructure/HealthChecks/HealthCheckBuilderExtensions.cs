using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CareBridge.Shared.Infrastructure.HealthChecks;

public static class HealthCheckBuilderExtensions
{
    /// <summary>
    /// Adds a SQL Server health check tagged as "ready" so it participates in the /ready probe.
    /// </summary>
    public static IHealthChecksBuilder AddCareBridgeSqlServer(
        this IHealthChecksBuilder builder,
        string connectionString)
    {
        return builder.AddSqlServer(
            connectionString,
            name: "sqlserver",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready"]);
    }

    /// <summary>
    /// Adds a RabbitMQ health check tagged as "ready" so it participates in the /ready probe.
    /// </summary>
    public static IHealthChecksBuilder AddCareBridgeRabbitMQ(
        this IHealthChecksBuilder builder,
        string host,
        int port,
        string user,
        string password)
    {
        var connectionString = $"amqp://{user}:{password}@{host}:{port}/";
        return builder.AddRabbitMQ(
            new Uri(connectionString),
            name: "rabbitmq",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready"]);
    }
}

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CareBridge.Shared.Infrastructure.HealthChecks;

public class StartupHealthCheck : IHealthCheck
{
    private volatile bool _isReady;

    public bool IsReady
    {
        get => _isReady;
        set => _isReady = value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_isReady
            ? HealthCheckResult.Healthy("Application has started.")
            : HealthCheckResult.Unhealthy("Application is still starting."));
    }
}

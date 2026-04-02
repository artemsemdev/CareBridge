using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace CareBridge.Shared.Infrastructure.Resilience;

public static class HttpResilienceExtensions
{
    /// <summary>
    /// Adds standard resilience handler (retry + circuit breaker + timeout) to an HttpClient builder.
    /// Retry: 2 retries with exponential backoff (200ms base).
    /// Circuit breaker: opens after failures exceed threshold, half-open after 30s.
    /// Overall timeout: 30 seconds.
    /// </summary>
    public static IHttpClientBuilder AddCareBridgeResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            // Retry: 2 retries with exponential backoff
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;

            // Circuit breaker: open after 50% failure rate within sampling window (min 5 requests)
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

            // Timeout per attempt
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);

            // Total request timeout (across all retries)
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        });

        return builder;
    }
}

using CareBridge.Shared.Contracts.Serialization;
using CareBridge.Shared.Infrastructure.Correlation;
using CareBridge.Shared.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CareBridge.Shared.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddCareBridgeDefaults(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        });

        builder.Services.AddSingleton<CorrelationIdAccessor>();
        builder.Services.AddSingleton<ICorrelationIdAccessor>(sp => sp.GetRequiredService<CorrelationIdAccessor>());

        builder.Services.AddSingleton<StartupHealthCheck>();
        builder.Services.AddHealthChecks()
            .AddCheck<StartupHealthCheck>("startup", tags: ["startup"]);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonDefaults.Options.PropertyNamingPolicy;
            options.SerializerOptions.PropertyNameCaseInsensitive = JsonDefaults.Options.PropertyNameCaseInsensitive;
            foreach (var converter in JsonDefaults.Options.Converters)
            {
                options.SerializerOptions.Converters.Add(converter);
            }
        });

        return builder;
    }

    public static WebApplication UseCareBridgeDefaults(this WebApplication app)
    {
        app.UseMiddleware<Middleware.CorrelationIdMiddleware>();
        app.UseMiddleware<Middleware.ExceptionHandlerMiddleware>();

        var startupCheck = app.Services.GetRequiredService<StartupHealthCheck>();
        startupCheck.IsReady = true;

        return app;
    }

    public static IEndpointRouteBuilder MapCareBridgeHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/startup", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("startup")
        });

        endpoints.MapHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready") || check.Tags.Contains("startup")
        });

        endpoints.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            Predicate = _ => false // Liveness: just checks the process is running
        });

        return endpoints;
    }
}

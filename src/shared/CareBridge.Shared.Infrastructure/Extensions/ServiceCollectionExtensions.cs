using System.Reflection;
using CareBridge.Shared.Contracts.Serialization;
using CareBridge.Shared.Infrastructure.Correlation;
using CareBridge.Shared.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace CareBridge.Shared.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddCareBridgeDefaults(this WebApplicationBuilder builder)
    {
        var serviceName = builder.Configuration["ServiceName"]
            ?? Assembly.GetEntryAssembly()?.GetName().Name
            ?? "UnknownService";

        // Structured JSON logging with Serilog
        builder.Host.UseSerilog((context, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .WriteTo.Console(new RenderedCompactJsonFormatter());
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

        // OpenTelemetry distributed tracing
        if (builder.Configuration.GetValue("OpenTelemetry:Enabled", true))
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName))
                .WithTracing(tracing =>
                {
                    tracing
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddEntityFrameworkCoreInstrumentation();

                    if (builder.Configuration.GetValue("OpenTelemetry:ConsoleExporter", false))
                    {
                        tracing.AddConsoleExporter();
                    }
                });
        }

        return builder;
    }

    public static WebApplication UseCareBridgeDefaults(this WebApplication app)
    {
        app.UseMiddleware<Middleware.CorrelationIdMiddleware>();
        app.UseMiddleware<Middleware.ExceptionHandlerMiddleware>();

        // Serilog request logging at Debug level: method, path, status code, duration
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (ctx, elapsed, ex) =>
                ex != null ? LogEventLevel.Error
                : ctx.Response.StatusCode >= 500 ? LogEventLevel.Error
                : LogEventLevel.Debug;
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
                diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? "/");
            };
        });

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

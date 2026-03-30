using System.Net;
using System.Security.Claims;
using CareBridge.Gateway;
using CareBridge.Shared.Infrastructure.Correlation;
using CareBridge.Shared.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.AddCareBridgeDefaults();

var services = builder.Configuration.GetSection("Services");
var caseServiceUrl = services["CaseService"] ?? "http://localhost:5010";
var carePlanServiceUrl = services["CarePlanService"] ?? "http://localhost:5020";
var observationServiceUrl = services["ObservationService"] ?? "http://localhost:5030";
var careGapEngineUrl = services["CareGapEngine"] ?? "http://localhost:5040";
var taskServiceUrl = services["TaskService"] ?? "http://localhost:5050";
var appointmentServiceUrl = services["AppointmentService"] ?? "http://localhost:5060";

builder.Services.AddHttpClient("CaseService", client =>
{
    client.BaseAddress = new Uri(caseServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<CorrelationIdForwardingHandler>();

builder.Services.AddHttpClient("CarePlanService", client =>
{
    client.BaseAddress = new Uri(carePlanServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<CorrelationIdForwardingHandler>();

builder.Services.AddHttpClient("ObservationService", client =>
{
    client.BaseAddress = new Uri(observationServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<CorrelationIdForwardingHandler>();

builder.Services.AddHttpClient("CareGapEngine", client =>
{
    client.BaseAddress = new Uri(careGapEngineUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<CorrelationIdForwardingHandler>();

builder.Services.AddHttpClient("TaskService", client =>
{
    client.BaseAddress = new Uri(taskServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<CorrelationIdForwardingHandler>();

builder.Services.AddHttpClient("AppointmentService", client =>
{
    client.BaseAddress = new Uri(appointmentServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<CorrelationIdForwardingHandler>();

builder.Services.AddScoped<CorrelationIdForwardingHandler>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCareBridgeDefaults();
app.MapCareBridgeHealthChecks();
app.UseCors();

// Mock auth middleware — reads or accepts any Bearer token, injects a dev claims principal
var useMockAuth = builder.Configuration.GetValue<bool>("Auth:UseMockAuth", true);
if (useMockAuth)
{
    app.Use(async (ctx, next) =>
    {
        if (!ctx.User.Identity?.IsAuthenticated ?? true)
        {
            var claims = new[]
            {
                new Claim("sub", "dev-user"),
                new Claim("name", "Dev Coordinator"),
                new Claim(ClaimTypes.Role, "CareCoordinator")
            };
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "MockAuth"));
        }
        await next();
    });
}

// POST /api/cases → Case Service POST /api/v1/cases
app.MapPost("/api/cases", async (HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "CaseService", HttpMethod.Post, "/api/v1/cases"));

// GET /api/cases → Case Service GET /api/v1/cases
app.MapGet("/api/cases", async (HttpContext ctx, IHttpClientFactory factory) =>
{
    var qs = ctx.Request.QueryString.Value ?? string.Empty;
    return await ProxyAsync(ctx, factory, "CaseService", HttpMethod.Get, $"/api/v1/cases{qs}");
});

// GET /api/cases/{caseId} → Case Service GET /api/v1/cases/{caseId}
app.MapGet("/api/cases/{caseId:guid}", async (Guid caseId, HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "CaseService", HttpMethod.Get, $"/api/v1/cases/{caseId}"));

// GET /api/cases/{caseId}/care-plan → Care Plan Service GET /api/v1/care-plans?caseId={caseId}
app.MapGet("/api/cases/{caseId:guid}/care-plan", async (Guid caseId, HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "CarePlanService", HttpMethod.Get, $"/api/v1/care-plans?caseId={caseId}"));

// GET /api/cases/{caseId}/observations → Observation Service GET /api/v1/observations?caseId={caseId}
app.MapGet("/api/cases/{caseId:guid}/observations", async (Guid caseId, HttpContext ctx, IHttpClientFactory factory) =>
{
    var qs = ctx.Request.QueryString.Value ?? string.Empty;
    var separator = string.IsNullOrEmpty(qs) ? "?" : qs + "&";
    return await ProxyAsync(ctx, factory, "ObservationService", HttpMethod.Get,
        $"/api/v1/observations?caseId={caseId}{(string.IsNullOrEmpty(qs) ? "" : "&" + qs.TrimStart('?'))}");
});

// POST /api/cases/{caseId}/observations → Observation Service POST /api/v1/observations (forward Idempotency-Key)
app.MapPost("/api/cases/{caseId:guid}/observations", async (Guid caseId, HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "ObservationService", HttpMethod.Post, "/api/v1/observations",
        forwardHeaders: ["Idempotency-Key"]));

// GET /api/cases/{caseId}/alerts → Care-Gap Engine GET /api/v1/alerts?caseId={caseId}
app.MapGet("/api/cases/{caseId:guid}/alerts", async (Guid caseId, HttpContext ctx, IHttpClientFactory factory) =>
{
    var qs = ctx.Request.QueryString.Value ?? string.Empty;
    return await ProxyAsync(ctx, factory, "CareGapEngine", HttpMethod.Get,
        $"/api/v1/alerts?caseId={caseId}{(string.IsNullOrEmpty(qs) ? "" : "&" + qs.TrimStart('?'))}");
});

// GET /api/alerts → Care-Gap Engine GET /api/v1/alerts
app.MapGet("/api/alerts", async (HttpContext ctx, IHttpClientFactory factory) =>
{
    var qs = ctx.Request.QueryString.Value ?? string.Empty;
    return await ProxyAsync(ctx, factory, "CareGapEngine", HttpMethod.Get, $"/api/v1/alerts{qs}");
});

// GET /api/alerts/{alertId} → Care-Gap Engine GET /api/v1/alerts/{alertId}
app.MapGet("/api/alerts/{alertId:guid}", async (Guid alertId, HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "CareGapEngine", HttpMethod.Get, $"/api/v1/alerts/{alertId}"));

// PATCH /api/alerts/{alertId}/acknowledge → Care-Gap Engine PATCH /api/v1/alerts/{alertId}/acknowledge
app.MapMethods("/api/alerts/{alertId:guid}/acknowledge", ["PATCH"],
    async (Guid alertId, HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "CareGapEngine", HttpMethod.Patch, $"/api/v1/alerts/{alertId}/acknowledge"));

// PATCH /api/alerts/{alertId}/resolve → Care-Gap Engine PATCH /api/v1/alerts/{alertId}/resolve
app.MapMethods("/api/alerts/{alertId:guid}/resolve", ["PATCH"],
    async (Guid alertId, HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "CareGapEngine", HttpMethod.Patch, $"/api/v1/alerts/{alertId}/resolve"));

// --- Task Service endpoints ---

// GET /api/tasks → Task Service GET /api/v1/tasks
app.MapGet("/api/tasks", async (HttpContext ctx, IHttpClientFactory factory) =>
{
    var qs = ctx.Request.QueryString.Value ?? string.Empty;
    return await ProxyAsync(ctx, factory, "TaskService", HttpMethod.Get, $"/api/v1/tasks{qs}");
});

// GET /api/cases/{caseId}/tasks → Task Service GET /api/v1/tasks?caseId={caseId}
app.MapGet("/api/cases/{caseId:guid}/tasks", async (Guid caseId, HttpContext ctx, IHttpClientFactory factory) =>
{
    var qs = ctx.Request.QueryString.Value ?? string.Empty;
    return await ProxyAsync(ctx, factory, "TaskService", HttpMethod.Get,
        $"/api/v1/tasks?caseId={caseId}{(string.IsNullOrEmpty(qs) ? "" : "&" + qs.TrimStart('?'))}");
});

// POST /api/tasks → Task Service POST /api/v1/tasks
app.MapPost("/api/tasks", async (HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "TaskService", HttpMethod.Post, "/api/v1/tasks"));

// PATCH /api/tasks/{taskId} → Task Service PATCH /api/v1/tasks/{taskId}
app.MapMethods("/api/tasks/{taskId:guid}", ["PATCH"],
    async (Guid taskId, HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "TaskService", HttpMethod.Patch, $"/api/v1/tasks/{taskId}"));

// --- Appointment Service endpoints ---

// GET /api/cases/{caseId}/appointments → Appointment Service GET /api/v1/appointments?caseId={caseId}
app.MapGet("/api/cases/{caseId:guid}/appointments", async (Guid caseId, HttpContext ctx, IHttpClientFactory factory) =>
{
    var qs = ctx.Request.QueryString.Value ?? string.Empty;
    return await ProxyAsync(ctx, factory, "AppointmentService", HttpMethod.Get,
        $"/api/v1/appointments?caseId={caseId}{(string.IsNullOrEmpty(qs) ? "" : "&" + qs.TrimStart('?'))}");
});

// POST /api/appointments → Appointment Service POST /api/v1/appointments
app.MapPost("/api/appointments", async (HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "AppointmentService", HttpMethod.Post, "/api/v1/appointments"));

// PATCH /api/appointments/{id} → Appointment Service PATCH /api/v1/appointments/{id}
app.MapMethods("/api/appointments/{id:guid}", ["PATCH"],
    async (Guid id, HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "AppointmentService", HttpMethod.Patch, $"/api/v1/appointments/{id}"));

app.Run();

static async Task<IResult> ProxyAsync(
    HttpContext ctx,
    IHttpClientFactory factory,
    string clientName,
    HttpMethod method,
    string path,
    string[]? forwardHeaders = null)
{
    var client = factory.CreateClient(clientName);

    var requestMessage = new HttpRequestMessage(method, path);

    if (method != HttpMethod.Get && ctx.Request.ContentLength > 0)
    {
        ctx.Request.EnableBuffering();
        requestMessage.Content = new StreamContent(ctx.Request.Body);
        if (ctx.Request.ContentType != null)
            requestMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ctx.Request.ContentType);
    }

    if (forwardHeaders is not null)
    {
        foreach (var headerName in forwardHeaders)
        {
            if (ctx.Request.Headers.TryGetValue(headerName, out var headerValue))
                requestMessage.Headers.TryAddWithoutValidation(headerName, (IEnumerable<string?>)headerValue);
        }
    }

    HttpResponseMessage response;
    try
    {
        response = await client.SendAsync(requestMessage, ctx.RequestAborted);
    }
    catch (TaskCanceledException)
    {
        return Results.Problem("The upstream service did not respond in time.", statusCode: 504, title: "Gateway Timeout");
    }
    catch (HttpRequestException)
    {
        return Results.Problem($"Unable to reach the upstream service.", statusCode: 502, title: "Bad Gateway");
    }

    var body = await response.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", System.Text.Encoding.UTF8, (int)response.StatusCode);
}

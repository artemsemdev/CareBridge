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

app.Run();

static async Task<IResult> ProxyAsync(HttpContext ctx, IHttpClientFactory factory, string clientName, HttpMethod method, string path)
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

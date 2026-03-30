using System.Text;
using CareBridge.ObservationService.Data;
using CareBridge.ObservationService.Entities;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using CareBridge.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCareBridgeDefaults();

builder.Services.AddDbContext<ObservationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ObservationDb")));

builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

var app = builder.Build();

app.UseCareBridgeDefaults();
app.MapCareBridgeHealthChecks();

// POST /api/v1/observations — ingest an observation
app.MapPost("/api/v1/observations", async (HttpContext ctx, ObservationDbContext db, IEventPublisher publisher, ILogger<Program> logger) =>
{
    var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(idempotencyKey))
        return Results.Problem("Idempotency-Key header is required.", statusCode: 400, title: "Validation Error");

    CreateObservationRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<CreateObservationRequest>();
    }
    catch
    {
        return Results.Problem("Invalid JSON body.", statusCode: 400, title: "Validation Error");
    }

    if (request is null)
        return Results.Problem("Request body is required.", statusCode: 400, title: "Validation Error");

    if (request.CaseId == Guid.Empty)
        return Results.Problem("CaseId is required.", statusCode: 400, title: "Validation Error");

    if (!Enum.IsDefined(typeof(ObservationType), request.Type))
        return Results.Problem("Type must be a valid ObservationType.", statusCode: 400, title: "Validation Error");

    if (string.IsNullOrWhiteSpace(request.Unit))
        return Results.Problem("Unit is required.", statusCode: 400, title: "Validation Error");

    // Validate RecordedAt: must not be in the future (5-minute tolerance)
    if (request.RecordedAt > DateTimeOffset.UtcNow.AddMinutes(5))
        return Results.Problem("RecordedAt must not be in the future.", statusCode: 422, title: "Validation Error");

    // Validate unit and value range per type
    var unitError = ValidateUnit(request.Type, request.Unit);
    if (unitError is not null)
        return Results.Problem(unitError, statusCode: 422, title: "Validation Error");

    var rangeError = ValidateRange(request.Type, request.Value);
    if (rangeError is not null)
        return Results.Problem(rangeError, statusCode: 422, title: "Validation Error");

    // Dedup check
    var existing = await db.Observations.FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey);
    if (existing is not null)
    {
        logger.LogInformation("Duplicate observation for IdempotencyKey {Key}, returning existing {Id}", idempotencyKey, existing.Id);
        return Results.Ok(ToResponse(existing));
    }

    var now = DateTimeOffset.UtcNow;
    var entity = new Observation
    {
        Id = Guid.NewGuid(),
        CaseId = request.CaseId,
        Type = request.Type,
        Value = request.Value,
        Unit = request.Unit.Trim(),
        RecordedAt = request.RecordedAt,
        ReceivedAt = now,
        DeviceId = request.DeviceId,
        IdempotencyKey = idempotencyKey
    };

    db.Observations.Add(entity);
    await db.SaveChangesAsync();

    try
    {
        await publisher.PublishAsync(new ObservationReceived
        {
            ObservationId = entity.Id,
            CaseId = entity.CaseId,
            Type = entity.Type,
            Value = entity.Value,
            Unit = entity.Unit,
            RecordedAt = entity.RecordedAt
        });
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to publish ObservationReceived event for observation {ObservationId}.", entity.Id);
    }

    return Results.Created($"/api/v1/observations/{entity.Id}", ToResponse(entity));
});

// GET /api/v1/observations?caseId={caseId}&type={type}&from={from}&to={to}&limit={limit}&cursor={cursor}
app.MapGet("/api/v1/observations", async (
    ObservationDbContext db,
    Guid? caseId,
    string? type,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int? limit,
    string? cursor) =>
{
    if (caseId is null)
        return Results.Problem("caseId query parameter is required.", statusCode: 400, title: "Validation Error");

    var pageSize = Math.Min(limit ?? 10, 100);
    var query = db.Observations.AsQueryable().Where(o => o.CaseId == caseId.Value);

    if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<ObservationType>(type, true, out var parsedType))
        query = query.Where(o => o.Type == parsedType);

    if (from.HasValue)
        query = query.Where(o => o.RecordedAt >= from.Value);

    if (to.HasValue)
        query = query.Where(o => o.RecordedAt <= to.Value);

    if (!string.IsNullOrWhiteSpace(cursor))
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            if (DateTimeOffset.TryParse(decoded, out var cursorTime))
                query = query.Where(o => o.RecordedAt < cursorTime);
        }
        catch { /* ignore invalid cursor */ }
    }

    var items = await query.OrderByDescending(o => o.RecordedAt).Take(pageSize + 1).ToListAsync();
    var hasMore = items.Count > pageSize;
    if (hasMore) items.RemoveAt(pageSize);

    string? nextCursor = null;
    if (hasMore && items.Count > 0)
        nextCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(items[^1].RecordedAt.ToString("O")));

    return Results.Ok(new PaginatedResponse<ObservationResponse>(
        items.Select(ToResponse).ToList(),
        nextCursor,
        hasMore));
});

app.Run();

static string? ValidateUnit(ObservationType type, string unit) => type switch
{
    ObservationType.BloodPressure when !unit.Equals("mmHg", StringComparison.OrdinalIgnoreCase)
        => "BloodPressure requires unit 'mmHg'.",
    ObservationType.HeartRate when !unit.Equals("bpm", StringComparison.OrdinalIgnoreCase)
        => "HeartRate requires unit 'bpm'.",
    ObservationType.SpO2 when !unit.Equals("%", StringComparison.OrdinalIgnoreCase)
        => "SpO2 requires unit '%'.",
    ObservationType.Glucose when !unit.Equals("mg/dL", StringComparison.OrdinalIgnoreCase)
        => "Glucose requires unit 'mg/dL'.",
    ObservationType.Temperature when !unit.Equals("°C", StringComparison.OrdinalIgnoreCase) && !unit.Equals("C", StringComparison.OrdinalIgnoreCase)
        => "Temperature requires unit '°C'.",
    ObservationType.Weight when !unit.Equals("kg", StringComparison.OrdinalIgnoreCase)
        => "Weight requires unit 'kg'.",
    _ => null
};

static string? ValidateRange(ObservationType type, decimal value) => type switch
{
    ObservationType.BloodPressure when value < 40 || value > 300
        => $"BloodPressure value {value} is outside the allowed range of 40–300 mmHg.",
    ObservationType.HeartRate when value < 20 || value > 300
        => $"HeartRate value {value} is outside the allowed range of 20–300 bpm.",
    ObservationType.SpO2 when value < 0 || value > 100
        => $"SpO2 value {value} is outside the allowed range of 0–100%.",
    ObservationType.Glucose when value < 20 || value > 600
        => $"Glucose value {value} is outside the allowed range of 20–600 mg/dL.",
    ObservationType.Temperature when value < 30 || value > 45
        => $"Temperature value {value} is outside the allowed range of 30–45°C.",
    ObservationType.Weight when value < 20 || value > 350
        => $"Weight value {value} is outside the allowed range of 20–350 kg.",
    _ => null
};

static ObservationResponse ToResponse(Observation entity) => new(
    entity.Id,
    entity.CaseId,
    entity.Type.ToString(),
    entity.Value,
    entity.Unit,
    entity.RecordedAt,
    entity.ReceivedAt,
    entity.DeviceId,
    entity.IdempotencyKey);

record CreateObservationRequest(
    Guid CaseId,
    ObservationType Type,
    decimal Value,
    string Unit,
    DateTimeOffset RecordedAt,
    string? DeviceId);

record ObservationResponse(
    Guid Id,
    Guid CaseId,
    string Type,
    decimal Value,
    string Unit,
    DateTimeOffset RecordedAt,
    DateTimeOffset ReceivedAt,
    string? DeviceId,
    string IdempotencyKey);

record PaginatedResponse<T>(List<T> Items, string? NextCursor, bool HasMore);

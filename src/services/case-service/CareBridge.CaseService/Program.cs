using System.Text;
using CareBridge.CaseService.Data;
using CareBridge.CaseService.Entities;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using CareBridge.Shared.Infrastructure.Extensions;
using CareBridge.Shared.Infrastructure.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCareBridgeDefaults();

// Security: Connection string loaded from configuration (env var or Key Vault in production). Never hardcode credentials.
var caseDbConnectionString = builder.Configuration.GetConnectionString("CaseDb")!;
builder.Services.AddDbContext<CaseDbContext>(options =>
    options.UseSqlServer(caseDbConnectionString, sql => sql.EnableRetryOnFailure()));

builder.Services.AddHealthChecks()
    .AddCareBridgeSqlServer(caseDbConnectionString);

builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

var app = builder.Build();

app.UseCareBridgeDefaults();
app.MapCareBridgeHealthChecks();

// PHI: Creates a new case with patient demographics (PatientId, PatientName, DiagnosisCode).
// Authorization: In production, restricted to CareCoordinator role via BFF authorization policy.
// Audit: CaseCreated event triggers immutable audit record in Audit Service per HIPAA §164.312(b).
app.MapPost("/api/v1/cases", async (CreateCaseRequest request, CaseDbContext db, IEventPublisher publisher, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.PatientId))
        return Results.Problem("PatientId is required.", statusCode: 400, title: "Validation Error");
    if (request.PatientId.Length > 50)
        return Results.Problem("PatientId cannot exceed 50 characters.", statusCode: 400, title: "Validation Error");
    if (string.IsNullOrWhiteSpace(request.PatientName))
        return Results.Problem("PatientName is required.", statusCode: 400, title: "Validation Error");
    if (request.PatientName.Length > 200)
        return Results.Problem("PatientName cannot exceed 200 characters.", statusCode: 400, title: "Validation Error");
    if (request.DischargeDate > DateTimeOffset.UtcNow)
        return Results.Problem("DischargeDate must not be in the future.", statusCode: 400, title: "Validation Error");
    if (string.IsNullOrWhiteSpace(request.DiagnosisCode) || !System.Text.RegularExpressions.Regex.IsMatch(request.DiagnosisCode, @"^[A-Za-z0-9.]+$"))
        return Results.Problem("DiagnosisCode is required and must contain only letters, digits, and dots.", statusCode: 400, title: "Validation Error");
    if (request.DiagnosisCode.Length > 20)
        return Results.Problem("DiagnosisCode cannot exceed 20 characters.", statusCode: 400, title: "Validation Error");
    if (string.IsNullOrWhiteSpace(request.DiagnosisDescription))
        return Results.Problem("DiagnosisDescription is required.", statusCode: 400, title: "Validation Error");
    if (request.DiagnosisDescription.Length > 500)
        return Results.Problem("DiagnosisDescription cannot exceed 500 characters.", statusCode: 400, title: "Validation Error");

    var now = DateTimeOffset.UtcNow;
    var entity = new CaseEntity
    {
        Id = Guid.NewGuid(),
        PatientId = request.PatientId.Trim(),
        PatientName = request.PatientName.Trim(),
        DiagnosisCode = request.DiagnosisCode.Trim(),
        DiagnosisDescription = request.DiagnosisDescription.Trim(),
        DischargeDate = request.DischargeDate,
        Status = CaseStatus.Active,
        CreatedAt = now,
        UpdatedAt = now
    };

    db.Cases.Add(entity);
    await db.SaveChangesAsync();

    // Audit: CaseCreated event carries patient context across service boundary for audit trail.
    try
    {
        await publisher.PublishAsync(new CaseCreated
        {
            CaseId = entity.Id,
            PatientId = entity.PatientId,
            PatientName = entity.PatientName,
            DiagnosisCode = entity.DiagnosisCode,
            DiagnosisDescription = entity.DiagnosisDescription,
            DischargeDate = entity.DischargeDate,
            Status = entity.Status
        });
    }
    catch (Exception ex)
    {
        // Log Hygiene: Log caseId only — no patient name or diagnosis in warning message.
        logger.LogWarning(ex, "Failed to publish CaseCreated event for case {CaseId}. Case was still created.", entity.Id);
    }

    return Results.Created($"/api/v1/cases/{entity.Id}", new CaseResponse(entity));
});

// PHI: Returns list of cases including patient names and diagnosis info.
// Authorization: In production, all authenticated roles can view cases (read-only for non-coordinators).
app.MapGet("/api/v1/cases", async (CaseDbContext db, string? status, int? limit, string? cursor) =>
{
    var pageSize = Math.Min(limit ?? 20, 100);

    var query = db.Cases.AsQueryable();

    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CaseStatus>(status, true, out var parsedStatus))
        query = query.Where(c => c.Status == parsedStatus);

    if (!string.IsNullOrWhiteSpace(cursor))
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            if (Guid.TryParse(decoded, out var lastId))
            {
                var lastItem = await db.Cases.FindAsync(lastId);
                if (lastItem != null)
                    query = query.Where(c => c.CreatedAt < lastItem.CreatedAt ||
                                             (c.CreatedAt == lastItem.CreatedAt && c.Id.CompareTo(lastId) < 0));
            }
        }
        catch { /* ignore invalid cursor */ }
    }

    var items = await query.OrderByDescending(c => c.CreatedAt).Take(pageSize + 1).ToListAsync();
    var hasMore = items.Count > pageSize;
    if (hasMore) items.RemoveAt(pageSize);

    string? nextCursor = null;
    if (hasMore && items.Count > 0)
        nextCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(items[^1].Id.ToString()));

    return Results.Ok(new PaginatedResponse<CaseResponse>(
        items.Select(c => new CaseResponse(c)).ToList(),
        nextCursor,
        hasMore));
});

// PHI: Returns full case detail including patient demographics and clinical context.
// Authorization: In production, all authenticated roles can view individual case details.
app.MapGet("/api/v1/cases/{id:guid}", async (Guid id, CaseDbContext db) =>
{
    var entity = await db.Cases.FindAsync(id);
    return entity is null ? Results.NotFound() : Results.Ok(new CaseResponse(entity));
});

// Authorization: In production, restricted to CareCoordinator role — only coordinators can change case status.
// Audit: CaseUpdated event records the status transition for compliance tracing per HIPAA §164.312(b).
app.MapMethods("/api/v1/cases/{id:guid}/status", ["PATCH"], async (Guid id, UpdateStatusRequest request, CaseDbContext db, IEventPublisher publisher, ILogger<Program> logger) =>
{
    var entity = await db.Cases.FindAsync(id);
    if (entity is null) return Results.NotFound();

    entity.Status = request.Status;
    entity.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync();

    try
    {
        await publisher.PublishAsync(new CaseUpdated
        {
            CaseId = entity.Id,
            Status = entity.Status,
            UpdatedAt = entity.UpdatedAt
        });
    }
    catch (Exception ex)
    {
        // Log Hygiene: Log caseId only — no patient-identifiable data in warning message.
        logger.LogWarning(ex, "Failed to publish CaseUpdated event for case {CaseId}.", entity.Id);
    }

    return Results.Ok(new CaseResponse(entity));
});

app.Run();

record CreateCaseRequest(
    string PatientId,
    string PatientName,
    string DiagnosisCode,
    string DiagnosisDescription,
    DateTimeOffset DischargeDate);

record UpdateStatusRequest(CaseStatus Status);

// HIPAA Minimum Necessary: CaseResponse includes PatientId, PatientName, and DiagnosisCode
// because care coordinators need this context for case management workflows.
// Contact information (phone, email, address) is NOT included in this response.
record CaseResponse(
    Guid Id,
    string PatientId,
    string PatientName,
    string DiagnosisCode,
    string DiagnosisDescription,
    DateTimeOffset DischargeDate,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public CaseResponse(CaseEntity entity) : this(
        entity.Id,
        entity.PatientId,
        entity.PatientName,
        entity.DiagnosisCode,
        entity.DiagnosisDescription,
        entity.DischargeDate,
        entity.Status.ToString(),
        entity.CreatedAt,
        entity.UpdatedAt)
    {
    }
}

record PaginatedResponse<T>(List<T> Items, string? NextCursor, bool HasMore);

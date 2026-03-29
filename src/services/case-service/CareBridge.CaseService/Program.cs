using System.Text;
using CareBridge.CaseService.Data;
using CareBridge.CaseService.Entities;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using CareBridge.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCareBridgeDefaults();

builder.Services.AddDbContext<CaseDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CaseDb")));

builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

var app = builder.Build();

app.UseCareBridgeDefaults();
app.MapCareBridgeHealthChecks();

app.MapPost("/api/v1/cases", async (CreateCaseRequest request, CaseDbContext db, IEventPublisher publisher, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.PatientId))
        return Results.Problem("PatientId is required.", statusCode: 400, title: "Validation Error");
    if (string.IsNullOrWhiteSpace(request.PatientName))
        return Results.Problem("PatientName is required.", statusCode: 400, title: "Validation Error");
    if (request.DischargeDate > DateTimeOffset.UtcNow)
        return Results.Problem("DischargeDate must not be in the future.", statusCode: 400, title: "Validation Error");
    if (string.IsNullOrWhiteSpace(request.DiagnosisCode) || !System.Text.RegularExpressions.Regex.IsMatch(request.DiagnosisCode, @"^[A-Za-z0-9.]+$"))
        return Results.Problem("DiagnosisCode is required and must contain only letters, digits, and dots.", statusCode: 400, title: "Validation Error");
    if (string.IsNullOrWhiteSpace(request.DiagnosisDescription))
        return Results.Problem("DiagnosisDescription is required.", statusCode: 400, title: "Validation Error");

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
        logger.LogWarning(ex, "Failed to publish CaseCreated event for case {CaseId}. Case was still created.", entity.Id);
    }

    return Results.Created($"/api/v1/cases/{entity.Id}", new CaseResponse(entity));
});

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

app.MapGet("/api/v1/cases/{id:guid}", async (Guid id, CaseDbContext db) =>
{
    var entity = await db.Cases.FindAsync(id);
    return entity is null ? Results.NotFound() : Results.Ok(new CaseResponse(entity));
});

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

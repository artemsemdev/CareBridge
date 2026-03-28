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

app.MapPost("/api/v1/cases", async (CreateCaseRequest request, CaseDbContext db, IEventPublisher publisher) =>
{
    var entity = new CaseEntity
    {
        Id = Guid.NewGuid(),
        PatientId = request.PatientId,
        PatientName = request.PatientName,
        DiagnosisCode = request.DiagnosisCode,
        DiagnosisDescription = request.DiagnosisDescription,
        DischargeDate = request.DischargeDate,
        Status = CaseStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    db.Cases.Add(entity);
    await db.SaveChangesAsync();

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

    return Results.Created($"/api/v1/cases/{entity.Id}", new CaseResponse(entity));
});

app.MapGet("/api/v1/cases", async (CaseDbContext db) =>
{
    var cases = await db.Cases.OrderByDescending(c => c.CreatedAt).ToListAsync();
    return Results.Ok(cases.Select(c => new CaseResponse(c)));
});

app.MapGet("/api/v1/cases/{id:guid}", async (Guid id, CaseDbContext db) =>
{
    var entity = await db.Cases.FindAsync(id);
    return entity is null ? Results.NotFound() : Results.Ok(new CaseResponse(entity));
});

app.Run();

record CreateCaseRequest(
    Guid PatientId,
    string PatientName,
    string DiagnosisCode,
    string DiagnosisDescription,
    DateTimeOffset DischargeDate);

record CaseResponse(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string DiagnosisCode,
    string DiagnosisDescription,
    DateTimeOffset DischargeDate,
    string Status,
    DateTimeOffset CreatedAt)
{
    public CaseResponse(CaseEntity entity) : this(
        entity.Id,
        entity.PatientId,
        entity.PatientName,
        entity.DiagnosisCode,
        entity.DiagnosisDescription,
        entity.DischargeDate,
        entity.Status.ToString(),
        entity.CreatedAt)
    {
    }
}

using System.Text;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using CareBridge.Shared.Infrastructure.Extensions;
using CareBridge.TaskService.Data;
using CareBridge.TaskService.Entities;
using CareBridge.TaskService.Handlers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCareBridgeDefaults();

builder.Services.AddDbContext<TaskDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TaskDb")));

builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddScoped<AlertRaisedHandler>();

builder.Services.AddSingleton(new EventConsumerOptions
{
    QueueName = "task-service",
    RoutingKeys = [nameof(AlertRaised)],
    EventTypeMap = new Dictionary<string, Type>
    {
        [nameof(AlertRaised)] = typeof(AlertRaised)
    }
});
builder.Services.AddScoped<IEventHandler<AlertRaised>, AlertRaisedHandler>();
builder.Services.AddHostedService<EventConsumerBackgroundService>();

var app = builder.Build();

app.UseCareBridgeDefaults();
app.MapCareBridgeHealthChecks();

// POST /api/v1/tasks — create task manually
app.MapPost("/api/v1/tasks", async (CreateTaskRequest request, TaskDbContext db, IEventPublisher publisher, ILogger<Program> logger) =>
{
    var errors = new List<string>();
    if (request.CaseId == Guid.Empty) errors.Add("CaseId is required.");
    if (string.IsNullOrWhiteSpace(request.Title)) errors.Add("Title is required.");
    if (request.Title?.Length > 200) errors.Add("Title cannot exceed 200 characters.");
    if (string.IsNullOrWhiteSpace(request.Description)) errors.Add("Description is required.");
    if (request.Description?.Length > 2000) errors.Add("Description cannot exceed 2000 characters.");
    if (errors.Count > 0)
        return Results.Problem(string.Join(" ", errors), statusCode: 400, title: "Validation Error");

    var now = DateTimeOffset.UtcNow;
    var task = new CareTask
    {
        Id = Guid.NewGuid(),
        CaseId = request.CaseId,
        Title = request.Title!,
        Description = request.Description!,
        Priority = request.Priority ?? TaskPriority.Medium,
        AssignedTo = request.AssignedTo,
        Status = CareBridgeTaskStatus.Open,
        CreatedAt = now,
        UpdatedAt = now
    };

    db.CareTasks.Add(task);
    await db.SaveChangesAsync();

    try
    {
        await publisher.PublishAsync(new TaskCreated
        {
            TaskId = task.Id,
            CaseId = task.CaseId,
            AlertId = null,
            Title = task.Title,
            Priority = task.Priority,
            CreatedAt = task.CreatedAt
        });
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to publish TaskCreated event for task {TaskId}.", task.Id);
    }

    return Results.Created($"/api/v1/tasks/{task.Id}", ToResponse(task));
});

// GET /api/v1/tasks — list tasks with filters
app.MapGet("/api/v1/tasks", async (
    TaskDbContext db,
    Guid? caseId,
    string? status,
    string? priority,
    string? assignedTo,
    int? limit,
    string? cursor) =>
{
    var pageSize = Math.Min(limit ?? 20, 100);
    var query = db.CareTasks.AsQueryable();

    if (caseId.HasValue)
        query = query.Where(t => t.CaseId == caseId.Value);

    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CareBridgeTaskStatus>(status, true, out var parsedStatus))
        query = query.Where(t => t.Status == parsedStatus);

    if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<TaskPriority>(priority, true, out var parsedPriority))
        query = query.Where(t => t.Priority == parsedPriority);

    if (!string.IsNullOrWhiteSpace(assignedTo))
        query = query.Where(t => t.AssignedTo == assignedTo);

    if (!string.IsNullOrWhiteSpace(cursor))
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            if (Guid.TryParse(decoded, out var lastId))
            {
                var lastItem = await db.CareTasks.FindAsync(lastId);
                if (lastItem != null)
                    query = query.Where(t => t.CreatedAt < lastItem.CreatedAt ||
                                             (t.CreatedAt == lastItem.CreatedAt && t.Id.CompareTo(lastId) < 0));
            }
        }
        catch { /* ignore invalid cursor */ }
    }

    var items = await query.OrderByDescending(t => t.CreatedAt).Take(pageSize + 1).ToListAsync();
    var hasMore = items.Count > pageSize;
    if (hasMore) items.RemoveAt(pageSize);

    string? nextCursor = null;
    if (hasMore && items.Count > 0)
        nextCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(items[^1].Id.ToString()));

    return Results.Ok(new PaginatedResponse<TaskResponse>(
        items.Select(ToResponse).ToList(),
        nextCursor,
        hasMore));
});

// GET /api/v1/tasks/{taskId} — get task detail
app.MapGet("/api/v1/tasks/{taskId:guid}", async (Guid taskId, TaskDbContext db) =>
{
    var task = await db.CareTasks.FindAsync(taskId);
    return task is null ? Results.NotFound() : Results.Ok(ToResponse(task));
});

// PATCH /api/v1/tasks/{taskId} — update task (status, assignment, priority)
app.MapMethods("/api/v1/tasks/{taskId:guid}", ["PATCH"],
    async (Guid taskId, UpdateTaskRequest request, TaskDbContext db, IEventPublisher publisher, ILogger<Program> logger) =>
    {
        var task = await db.CareTasks.FindAsync(taskId);
        if (task is null) return Results.NotFound();

        if (task.Status == CareBridgeTaskStatus.Completed)
            return Results.Problem("Cannot modify a completed task.", statusCode: 409, title: "Conflict");

        if (request.Status.HasValue)
        {
            var transitionError = ValidateTransition(task.Status, request.Status.Value);
            if (transitionError is not null)
                return Results.Problem(transitionError, statusCode: 409, title: "Conflict");

            task.Status = request.Status.Value;

            if (request.Status.Value == CareBridgeTaskStatus.Completed)
            {
                task.CompletedAt = DateTimeOffset.UtcNow;
                task.CompletedBy = request.CompletedBy ?? "care-coordinator";
            }
        }

        if (request.AssignedTo is not null)
            task.AssignedTo = request.AssignedTo;

        if (request.Priority.HasValue)
            task.Priority = request.Priority.Value;

        task.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        if (request.Status == CareBridgeTaskStatus.Completed)
        {
            try
            {
                await publisher.PublishAsync(new TaskCompleted
                {
                    TaskId = task.Id,
                    CaseId = task.CaseId,
                    AlertId = task.AlertId,
                    CompletedBy = task.CompletedBy!,
                    CompletedAt = task.CompletedAt!.Value
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish TaskCompleted event for task {TaskId}.", task.Id);
            }
        }

        return Results.Ok(ToResponse(task));
    });

app.Run();

static string? ValidateTransition(CareBridgeTaskStatus current, CareBridgeTaskStatus next)
{
    var valid = current switch
    {
        CareBridgeTaskStatus.Open => next is CareBridgeTaskStatus.InProgress or CareBridgeTaskStatus.Deferred,
        CareBridgeTaskStatus.InProgress => next is CareBridgeTaskStatus.Completed or CareBridgeTaskStatus.Deferred or CareBridgeTaskStatus.Open,
        CareBridgeTaskStatus.Deferred => next is CareBridgeTaskStatus.Open,
        CareBridgeTaskStatus.Completed => false,
        _ => false
    };
    return valid ? null : $"Cannot transition from {current} to {next}.";
}

static TaskResponse ToResponse(CareTask task)
{
    var age = FormatAge(task.CreatedAt);
    return new TaskResponse(
        task.Id, task.CaseId, task.AlertId,
        task.Title, task.Description,
        task.AssignedTo,
        task.Status.ToString(), task.Priority.ToString(),
        task.CreatedAt, task.UpdatedAt,
        task.CompletedAt, task.CompletedBy, age);
}

static string FormatAge(DateTimeOffset createdAt)
{
    var elapsed = DateTimeOffset.UtcNow - createdAt;
    if (elapsed.TotalMinutes < 1) return "just now";
    if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes} minute{((int)elapsed.TotalMinutes != 1 ? "s" : "")} ago";
    if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours} hour{((int)elapsed.TotalHours != 1 ? "s" : "")} ago";
    return $"{(int)elapsed.TotalDays} day{((int)elapsed.TotalDays != 1 ? "s" : "")} ago";
}

record CreateTaskRequest(
    Guid CaseId,
    string? Title,
    string? Description,
    TaskPriority? Priority,
    string? AssignedTo);

record UpdateTaskRequest(
    CareBridgeTaskStatus? Status,
    string? AssignedTo,
    TaskPriority? Priority,
    string? CompletedBy);

record TaskResponse(
    Guid Id,
    Guid CaseId,
    Guid? AlertId,
    string Title,
    string Description,
    string? AssignedTo,
    string Status,
    string Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    string? CompletedBy,
    string Age);

record PaginatedResponse<T>(List<T> Items, string? NextCursor, bool HasMore);

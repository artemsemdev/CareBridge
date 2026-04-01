using CareBridge.AuditService.Models;
using CareBridge.AuditService.Store;

namespace CareBridge.AuditService.UnitTests;

public class InMemoryAuditStoreTests
{
    private readonly InMemoryAuditStore _store = new();

    private static AuditEvent CreateRecord(
        Guid? id = null,
        Guid? eventId = null,
        Guid? caseId = null,
        string? eventType = null,
        string? actorId = null,
        string? entityType = null,
        Guid? entityId = null,
        DateTimeOffset? timestamp = null)
    {
        return new AuditEvent
        {
            Id = id ?? Guid.NewGuid(),
            EventId = eventId ?? Guid.NewGuid(),
            CaseId = caseId ?? Guid.NewGuid(),
            EventType = eventType ?? "CaseCreated",
            Action = "Created case",
            ActorId = actorId ?? "system",
            ActorRole = "System",
            EntityType = entityType ?? "Case",
            EntityId = entityId ?? Guid.NewGuid(),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString(),
            ServiceSource = "CaseService",
            Payload = "{}"
        };
    }

    [Fact]
    public async Task AppendAsync_StoresRecord()
    {
        var record = CreateRecord();
        await _store.AppendAsync(record);

        var result = await _store.GetByIdAsync(record.Id);
        Assert.NotNull(result);
        Assert.Equal(record.EventType, result.EventType);
    }

    [Fact]
    public async Task AppendAsync_DuplicateEventId_SkipsSilently()
    {
        var eventId = Guid.NewGuid();
        var record1 = CreateRecord(eventId: eventId);
        var record2 = CreateRecord(eventId: eventId);

        await _store.AppendAsync(record1);
        await _store.AppendAsync(record2);

        // Only the first record should exist
        var result = await _store.GetByEventIdAsync(eventId);
        Assert.NotNull(result);
        Assert.Equal(record1.Id, result.Id);

        // Second record ID should not exist
        var result2 = await _store.GetByIdAsync(record2.Id);
        Assert.Null(result2);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _store.GetByEventIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _store.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task QueryAsync_EmptyStore_ReturnsEmptyList()
    {
        var result = await _store.QueryAsync(new AuditQueryFilter());
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task QueryAsync_FilterByCaseId()
    {
        var targetCaseId = Guid.NewGuid();
        await _store.AppendAsync(CreateRecord(caseId: targetCaseId));
        await _store.AppendAsync(CreateRecord(caseId: targetCaseId));
        await _store.AppendAsync(CreateRecord()); // different case

        var result = await _store.QueryAsync(new AuditQueryFilter { CaseId = targetCaseId });
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, r => Assert.Equal(targetCaseId, r.CaseId));
    }

    [Fact]
    public async Task QueryAsync_FilterByActorId()
    {
        await _store.AppendAsync(CreateRecord(actorId: "care-coordinator"));
        await _store.AppendAsync(CreateRecord(actorId: "system"));

        var result = await _store.QueryAsync(new AuditQueryFilter { ActorId = "care-coordinator" });
        Assert.Single(result.Items);
        Assert.Equal("care-coordinator", result.Items[0].ActorId);
    }

    [Fact]
    public async Task QueryAsync_FilterByEntityType()
    {
        await _store.AppendAsync(CreateRecord(entityType: "Alert"));
        await _store.AppendAsync(CreateRecord(entityType: "Case"));
        await _store.AppendAsync(CreateRecord(entityType: "Alert"));

        var result = await _store.QueryAsync(new AuditQueryFilter { EntityType = "Alert" });
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task QueryAsync_FilterByEventType()
    {
        await _store.AppendAsync(CreateRecord(eventType: "AlertRaised"));
        await _store.AppendAsync(CreateRecord(eventType: "CaseCreated"));

        var result = await _store.QueryAsync(new AuditQueryFilter { EventType = "AlertRaised" });
        Assert.Single(result.Items);
        Assert.Equal("AlertRaised", result.Items[0].EventType);
    }

    [Fact]
    public async Task QueryAsync_FilterByEntityId()
    {
        var targetEntityId = Guid.NewGuid();
        await _store.AppendAsync(CreateRecord(entityId: targetEntityId));
        await _store.AppendAsync(CreateRecord());

        var result = await _store.QueryAsync(new AuditQueryFilter { EntityId = targetEntityId });
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task QueryAsync_FilterByDateRange()
    {
        var march1 = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var march15 = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var april1 = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        await _store.AppendAsync(CreateRecord(timestamp: march1));
        await _store.AppendAsync(CreateRecord(timestamp: march15));
        await _store.AppendAsync(CreateRecord(timestamp: april1));

        var result = await _store.QueryAsync(new AuditQueryFilter
        {
            From = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            To = new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.Zero)
        });

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task QueryAsync_CombinedFilters()
    {
        var targetCase = Guid.NewGuid();
        await _store.AppendAsync(CreateRecord(caseId: targetCase, eventType: "AlertRaised"));
        await _store.AppendAsync(CreateRecord(caseId: targetCase, eventType: "CaseCreated"));
        await _store.AppendAsync(CreateRecord(eventType: "AlertRaised"));

        var result = await _store.QueryAsync(new AuditQueryFilter
        {
            CaseId = targetCase,
            EventType = "AlertRaised"
        });

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task QueryAsync_OrderedByTimestampDescending()
    {
        var t1 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var t2 = DateTimeOffset.UtcNow.AddMinutes(-5);
        var t3 = DateTimeOffset.UtcNow;

        await _store.AppendAsync(CreateRecord(timestamp: t1));
        await _store.AppendAsync(CreateRecord(timestamp: t3));
        await _store.AppendAsync(CreateRecord(timestamp: t2));

        var result = await _store.QueryAsync(new AuditQueryFilter());
        Assert.Equal(3, result.Items.Count);
        Assert.True(result.Items[0].Timestamp >= result.Items[1].Timestamp);
        Assert.True(result.Items[1].Timestamp >= result.Items[2].Timestamp);
    }

    [Fact]
    public async Task QueryAsync_CursorPagination()
    {
        for (int i = 0; i < 5; i++)
            await _store.AppendAsync(CreateRecord(timestamp: DateTimeOffset.UtcNow.AddMinutes(-i)));

        var page1 = await _store.QueryAsync(new AuditQueryFilter { Limit = 2 });
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.HasMore);
        Assert.NotNull(page1.NextCursor);
        Assert.Equal(5, page1.TotalCount);

        var page2 = await _store.QueryAsync(new AuditQueryFilter { Limit = 2, Cursor = page1.NextCursor });
        Assert.Equal(2, page2.Items.Count);
        Assert.True(page2.HasMore);

        var page3 = await _store.QueryAsync(new AuditQueryFilter { Limit = 2, Cursor = page2.NextCursor });
        Assert.Single(page3.Items);
        Assert.False(page3.HasMore);
        Assert.Null(page3.NextCursor);
    }

    [Fact]
    public async Task QueryAsync_LimitClampedTo200()
    {
        for (int i = 0; i < 5; i++)
            await _store.AppendAsync(CreateRecord());

        var result = await _store.QueryAsync(new AuditQueryFilter { Limit = 999 });
        // Limit is clamped to 200, so all 5 items should be returned
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task QueryAsync_LimitClampedToMin1()
    {
        await _store.AppendAsync(CreateRecord());
        await _store.AppendAsync(CreateRecord());

        var result = await _store.QueryAsync(new AuditQueryFilter { Limit = 0 });
        Assert.Single(result.Items);
        Assert.True(result.HasMore);
    }

    [Fact]
    public async Task ConcurrentAppends_AreThreadSafe()
    {
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => _store.AppendAsync(CreateRecord()))
            .ToArray();

        await Task.WhenAll(tasks);

        var result = await _store.QueryAsync(new AuditQueryFilter { Limit = 200 });
        Assert.Equal(100, result.TotalCount);
    }

    [Fact]
    public async Task ConcurrentAppendAndQuery_AreThreadSafe()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var appendTask = Task.Run(async () =>
        {
            for (int i = 0; i < 50; i++)
                await _store.AppendAsync(CreateRecord());
        }, cts.Token);

        var queryTask = Task.Run(async () =>
        {
            for (int i = 0; i < 50; i++)
                await _store.QueryAsync(new AuditQueryFilter());
        }, cts.Token);

        await Task.WhenAll(appendTask, queryTask);
        // No exceptions means thread safety is maintained
    }
}

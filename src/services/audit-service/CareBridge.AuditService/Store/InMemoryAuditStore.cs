using System.Collections.Concurrent;
using CareBridge.AuditService.Models;

namespace CareBridge.AuditService.Store;

// Audit: Thread-safe in-memory store for local development. Production uses Cosmos DB.
// Append-only — no update/delete methods. Immutability enforced at the interface level.
public class InMemoryAuditStore : IAuditStore
{
    private readonly ConcurrentDictionary<Guid, AuditEvent> _records = new();
    private readonly ConcurrentDictionary<Guid, Guid> _eventIdIndex = new();

    public Task AppendAsync(AuditEvent record)
    {
        if (!_eventIdIndex.TryAdd(record.EventId, record.Id))
            return Task.CompletedTask;

        _records[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<AuditEvent?> GetByEventIdAsync(Guid eventId)
    {
        if (_eventIdIndex.TryGetValue(eventId, out var recordId) && _records.TryGetValue(recordId, out var record))
            return Task.FromResult<AuditEvent?>(record);

        return Task.FromResult<AuditEvent?>(null);
    }

    public Task<AuditEvent?> GetByIdAsync(Guid id)
    {
        _records.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task<PaginatedResult<AuditEvent>> QueryAsync(AuditQueryFilter filter)
    {
        var query = _records.Values.AsEnumerable();

        if (filter.CaseId.HasValue)
            query = query.Where(r => r.CaseId == filter.CaseId.Value);

        if (!string.IsNullOrEmpty(filter.ActorId))
            query = query.Where(r => r.ActorId.Equals(filter.ActorId, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(filter.EntityType))
            query = query.Where(r => r.EntityType.Equals(filter.EntityType, StringComparison.OrdinalIgnoreCase));

        if (filter.EntityId.HasValue)
            query = query.Where(r => r.EntityId == filter.EntityId.Value);

        if (!string.IsNullOrEmpty(filter.EventType))
            query = query.Where(r => r.EventType.Equals(filter.EventType, StringComparison.OrdinalIgnoreCase));

        if (filter.From.HasValue)
            query = query.Where(r => r.Timestamp >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(r => r.Timestamp <= filter.To.Value);

        var allFiltered = query.OrderByDescending(r => r.Timestamp).ToList();
        var totalCount = allFiltered.Count;

        if (!string.IsNullOrEmpty(filter.Cursor))
        {
            var cursorBytes = Convert.FromBase64String(filter.Cursor);
            var cursorId = new Guid(cursorBytes);
            var idx = allFiltered.FindIndex(r => r.Id == cursorId);
            if (idx >= 0)
                allFiltered = allFiltered.Skip(idx + 1).ToList();
        }

        var limit = Math.Clamp(filter.Limit, 1, 200);
        var page = allFiltered.Take(limit + 1).ToList();
        var hasMore = page.Count > limit;
        if (hasMore) page.RemoveAt(page.Count - 1);

        string? nextCursor = null;
        if (hasMore && page.Count > 0)
            nextCursor = Convert.ToBase64String(page[^1].Id.ToByteArray());

        return Task.FromResult(new PaginatedResult<AuditEvent>
        {
            Items = page,
            NextCursor = nextCursor,
            HasMore = hasMore,
            TotalCount = totalCount
        });
    }
}

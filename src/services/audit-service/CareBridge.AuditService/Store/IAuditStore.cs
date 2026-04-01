using CareBridge.AuditService.Models;

namespace CareBridge.AuditService.Store;

// Audit: Append-only store. No Update or Delete methods exist by design (HIPAA §164.312(b)).
public interface IAuditStore
{
    Task AppendAsync(AuditEvent record);
    Task<AuditEvent?> GetByEventIdAsync(Guid eventId);
    Task<AuditEvent?> GetByIdAsync(Guid id);
    Task<PaginatedResult<AuditEvent>> QueryAsync(AuditQueryFilter filter);
}

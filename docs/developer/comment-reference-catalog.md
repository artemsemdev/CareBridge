# Comment Reference Catalog

**Document type:** Quick-reference templates
**Last updated:** 2026-03-31

Copy-paste comment templates organized by category. Each template includes the comment prefix, a fill-in pattern, and a real-world example from CareBridge. For the full rationale behind each category, see [Source Code Commenting Standards](source-code-commenting-standards.md).

---

## Prefixes at a Glance

| Prefix | Category | When to Use |
|---|---|---|
| `// PHI:` | PHI/PII boundary | Code that accesses, returns, or stores patient-identifiable data |
| `// HIPAA Minimum Necessary:` | Minimum necessary | DTOs, projections, and event payloads carrying patient data |
| `// Audit:` | Audit trail | Event publishing, state transitions, intentional audit gaps |
| `// Authorization:` | Access control | Endpoints, handlers, service-to-service calls |
| `// Retention:` | Data lifecycle | Store interfaces, soft-delete, archival logic |
| `// Security:` | Security decisions | Secrets, encryption, input validation, blast radius limits |
| `// Log Hygiene:` | Log scrubbing | Log statements near PHI, error handlers, middleware |

Using consistent prefixes makes it possible to `grep` the codebase for all comments of a given compliance category.

---

## 1. PHI / PII Data Boundary

### Template
```
// PHI: {what patient data is accessed/returned} — {who should have access or what constraint applies}
```

### Examples

**API endpoint returning patient data:**
```csharp
// PHI: Returns patient demographics (name, DOB, contact info, MRN) for case detail view.
// Restricted to CareCoordinator and Clinician roles via BFF authorization policy.
app.MapGet("/api/v1/cases/{id}", async (Guid id, CaseDbContext db) =>
{
    var caseEntity = await db.Cases.FindAsync(id);
    return caseEntity is null ? Results.NotFound() : Results.Ok(caseEntity.ToDetailDto());
});
```

**Database query selecting patient fields:**
```csharp
// PHI: Query includes patientName for coordinator task list. No clinical data selected.
var tasks = await db.Tasks
    .Where(t => t.AssignedTo == coordinatorId)
    .Select(t => new TaskListItem(t.Id, t.CaseId, t.PatientName, t.Title, t.Status))
    .ToListAsync();
```

**Frontend component displaying patient info:**
```typescript
// PHI: Renders patient name and contact phone. Component is only mounted
// inside role-gated routes (CareCoordinator, Clinician).
function PatientContactCard({ patient }: { patient: PatientDto }) {
```

---

## 2. HIPAA Minimum Necessary

### Template
```
// HIPAA Minimum Necessary: {which PHI fields are included} — {why each is needed}.
// {which PHI fields are excluded and why}.
```

### Examples

**Response DTO with limited fields:**
```csharp
// HIPAA Minimum Necessary: TaskSummaryDto includes patientName (needed for task list display)
// and caseId (needed for navigation). No clinical data, MRN, DOB, or contact info is included.
public record TaskSummaryDto(Guid TaskId, Guid CaseId, string PatientName, string Title, string Status);
```

**Event payload crossing service boundary:**
```csharp
// HIPAA Minimum Necessary: AlertRaisedEvent carries caseId, alertId, type, and severity.
// Patient demographics and observation values are NOT included in the event.
// Consuming services must query the originating service if they need additional context.
public record AlertRaisedEvent(Guid CaseId, Guid AlertId, string AlertType, string Severity);
```

**Dashboard aggregation endpoint:**
```csharp
// HIPAA Minimum Necessary: Dashboard metrics return counts and percentages only.
// No individual patient records, names, or identifiers are included in the response.
app.MapGet("/api/v1/dashboard/metrics", async (ReportingDbContext db) =>
{
    var metrics = await db.GetAggregateMetricsAsync();
    return Results.Ok(metrics);
});
```

---

## 3. Audit Trail

### Template
```
// Audit: {what action is being audited} — {regulatory reference if applicable}
```

### Examples

**Publishing a domain event that creates an audit record:**
```csharp
// Audit: CaseStatusChanged event creates an immutable audit record in Audit Service.
// Per HIPAA §164.312(b) — all case lifecycle transitions must be traceable.
await eventBus.PublishAsync(new CaseStatusChangedEvent(
    caseEntity.Id, previousStatus, caseEntity.Status, actorId));
```

**Intentional audit gap with justification:**
```csharp
// Audit: No audit event for GET /api/v1/observations (read-only, high-frequency polling).
// Read-access auditing is handled at the infrastructure level via request logging.
// See observability.md §Access Logging for the tracing configuration.
app.MapGet("/api/v1/observations", async (ObservationDbContext db) => { ... });
```

**Immutability enforcement:**
```csharp
// Audit: IAuditStore is append-only by design. No Update/Delete methods exist.
// Per HIPAA §164.312(b) and §164.530(j) — audit records are immutable and retained 6+ years.
public interface IAuditStore
{
    Task AppendAsync(AuditEvent record);
    Task<AuditEvent?> GetByEventIdAsync(Guid eventId);
    Task<PaginatedResult<AuditEvent>> QueryAsync(AuditQueryFilter filter);
}
```

---

## 4. Access Control Intent

### Template
```
// Authorization: {which roles/identities can access} — {read/write/admin level}.
// {any special conditions: assigned-to check, time-based, etc.}
```

### Examples

**Endpoint with role restriction:**
```csharp
// Authorization: CareCoordinator role only. Coordinator must be assigned to this case.
// Clinicians cannot modify case status — they have read-only access via GET endpoint.
app.MapPut("/api/v1/cases/{id}/status",
    async (Guid id, UpdateStatusRequest req, ClaimsPrincipal user, CaseDbContext db) =>
{
    // Verify the requesting coordinator is assigned to this case
    var assignedTo = user.FindFirst("sub")?.Value;
    ...
})
.RequireAuthorization("CaseWrite");
```

**System-initiated event handler:**
```csharp
// Authorization: System-initiated action — no user context required.
// Runs under service identity. Audit records actor as "system" with source event correlation.
public class AutoCreateTaskOnAlertHandler : IEventHandler<AlertRaisedEvent>
{
    ...
}
```

**BFF proxy with authorization policy:**
```csharp
// Authorization: PlatformAdmin role only. Audit trail contains sensitive operational data.
// Non-admin roles cannot query the audit API even through the BFF.
auditGroup.MapGet("/", async (HttpContext ctx, IHttpClientFactory factory) =>
    await ProxyAsync(ctx, factory, "AuditService", "/api/v1/audit"))
    .RequireAuthorization("AuditRead");
```

---

## 5. Data Retention and Lifecycle

### Template
```
// Retention: {what data and retention rule} — {regulatory reference}.
// {what operations are restricted and why}.
```

### Examples

**Store interface with no delete:**
```csharp
// Retention: Audit records must be retained for minimum 6 years per HIPAA §164.530(j).
// No Update or Delete methods on this interface. Immutability is enforced at the API layer
// (no PUT/PATCH/DELETE endpoints) and the storage layer (append-only store).
public interface IAuditStore { ... }
```

**Soft-delete pattern:**
```csharp
// Retention: Cases are soft-deleted by transitioning to Closed status.
// Hard-delete is not supported — closed cases remain queryable for audit trail integrity
// and regulatory retention (6+ years per HIPAA §164.530(j), state laws may require longer).
public void CloseCase(Case caseEntity)
{
    caseEntity.Status = CaseStatus.Closed;
    caseEntity.ClosedAt = DateTimeOffset.UtcNow;
}
```

---

## 6. Security Decisions

### Template
```
// Security: {what decision was made} — {why, referencing threat or standard}.
```

### Examples

**Secrets from environment:**
```csharp
// Security: Connection string loaded from configuration (env var or Key Vault in production).
// Never hardcode credentials. See CLAUDE.md §Credential Pattern and
// security-and-compliance.md §Secret Management.
var connectionString = builder.Configuration.GetConnectionString("CaseDb");
```

**PHI excluded from event payloads:**
```csharp
// Security: NotificationSent event carries notificationId and caseId only.
// Patient contact details (phone, email) are NOT included in the event payload
// to limit PHI exposure if the message bus is compromised.
public record NotificationSentEvent(Guid NotificationId, Guid CaseId, string Channel, string Status);
```

**Input validation for injection prevention:**
```csharp
// Security: Validate observation value range to prevent injection of extreme values
// that could trigger false clinical alerts. Acceptable range is domain-specific
// (e.g., heart rate 20-300 bpm). Values outside range are rejected with 400.
if (request.Value < rule.MinValue || request.Value > rule.MaxValue)
    return Results.Problem("Observation value outside acceptable range", statusCode: 400);
```

**Correlation ID trust boundary:**
```csharp
// Security: CorrelationId is for distributed tracing only — never use it for authorization.
// It can be spoofed by external callers. All authorization decisions use JWT claims
// validated by the BFF and propagated via trusted internal headers.
var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
    ?? Guid.NewGuid().ToString();
```

---

## 7. Log Scrubbing

### Template
```
// Log Hygiene: {what is logged} — {what is NOT logged and why}.
```

### Examples

**Logging near observation processing:**
```csharp
// Log Hygiene: Log observation ID, type, and case ID for tracing.
// Do NOT log observation value (vital sign reading) — PHI per logging hygiene policy.
logger.LogInformation(
    "Processed observation {ObservationId} type={ObservationType} case={CaseId}",
    obs.Id, obs.Type, obs.CaseId);
```

**Error handler that might leak data:**
```csharp
// Log Hygiene: Log exception type and message only. Do not log exception Data dictionary
// or inner exception details — they may contain PHI from deserialized request bodies.
logger.LogError(ex, "Failed to process request {CorrelationId}", correlationId);
```

**Middleware request logging:**
```csharp
// Log Hygiene: Log HTTP method, path, status code, and duration.
// Request and response bodies are NOT logged — they may contain patient data.
// See security-and-compliance.md §Logging Hygiene for the full policy.
logger.LogInformation(
    "{Method} {Path} responded {StatusCode} in {Duration}ms",
    context.Request.Method, context.Request.Path, context.Response.StatusCode, elapsed);
```

---

## Grep Commands for Compliance Review

Use these commands to audit comment coverage across the codebase:

```bash
# Find all PHI boundary markers
grep -rn "// PHI:" src/

# Find all minimum necessary justifications
grep -rn "// HIPAA Minimum Necessary:" src/

# Find all audit annotations
grep -rn "// Audit:" src/

# Find all authorization intent comments
grep -rn "// Authorization:" src/

# Find all retention annotations
grep -rn "// Retention:" src/

# Find all security decision comments
grep -rn "// Security:" src/

# Find all log hygiene annotations
grep -rn "// Log Hygiene:" src/

# Count comments by category
for prefix in "PHI:" "HIPAA Minimum Necessary:" "Audit:" "Authorization:" "Retention:" "Security:" "Log Hygiene:"; do
    echo "$prefix $(grep -rn "// $prefix" src/ | wc -l) occurrences"
done
```

---

## Related Documents

- [Source Code Commenting Standards](source-code-commenting-standards.md) — Full rationale and regulatory references
- [Security and Compliance](../architecture/security-and-compliance.md) — Security architecture and threat model
- [Coding Boundaries](coding-boundaries.md) — Service boundary rules

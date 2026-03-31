# Source Code Commenting Standards

**Document type:** Development guidelines
**Last updated:** 2026-03-31

This document defines what comments belong in CareBridge source code, why they exist, and which industry standards drive each category. Healthcare software operates under regulatory scrutiny that general-purpose applications do not face. The right comments make compliance auditable directly from the codebase.

---

## Guiding Principle

> Comment the **regulatory why**, not the **technical what**.

Code should be self-documenting for its logic. Comments in a healthcare codebase exist to answer questions that code alone cannot: _Why is this data excluded? What compliance rule drives this design choice? Who is allowed to see this and under what authority?_

---

## Comment Categories

### 1. PHI / PII Data Boundary Markers

**What:** Mark every location where Protected Health Information (PHI) or Personally Identifiable Information (PII) is accessed, transformed, returned, or stored.

**Why:** HIPAA Security Rule (45 CFR §164.312) requires covered entities to implement access controls and audit mechanisms for electronic PHI (ePHI). Marking PHI touchpoints in code enables:
- Targeted security reviews during code review
- Faster incident response scoping ("which endpoints touch PHI?")
- Evidence of security-aware design for compliance assessments

**Standard:** HIPAA Security Rule §164.312(a)(1) — Access Control

**When to apply:**
- API endpoints that return patient data (name, MRN, contact info, clinical observations)
- Database queries that read or write patient-identifiable records
- Event payloads that carry patient context
- DTOs and mappers that include or exclude PHI fields

**Example:**
```csharp
// PHI: Returns patient demographics (name, DOB, contact) — restrict to CareCoordinator, Clinician roles
app.MapGet("/api/v1/cases/{id}", async (Guid id, CaseDbContext db) => { ... });
```

```csharp
// PHI: Observation values (vital signs) included in response — do not log response body
return Results.Ok(new ObservationResponse { Value = obs.Value, Unit = obs.Unit });
```

---

### 2. HIPAA Minimum Necessary Justification

**What:** When an endpoint or query returns patient data, document why each PHI field is included and confirm that no unnecessary fields are exposed.

**Why:** The HIPAA Minimum Necessary Rule (45 CFR §164.502(b)) requires that access to PHI be limited to the minimum amount needed to accomplish the intended purpose. Commenting the justification creates a reviewable record of this determination.

**Standard:** HIPAA Privacy Rule §164.502(b) — Minimum Necessary

**When to apply:**
- Any API response DTO that includes PHI fields
- Any database projection that selects patient-identifiable columns
- Any event payload that carries patient context to other services

**Example:**
```csharp
// HIPAA Minimum Necessary: Only patientName and caseId returned here.
// patientName is required for the coordinator's task list display.
// No clinical data, contact info, or MRN included in this projection.
public record TaskSummaryDto(Guid TaskId, Guid CaseId, string PatientName, string Title, string Status);
```

```csharp
// HIPAA Minimum Necessary: Event carries caseId and alertType only.
// No patient demographics or observation values cross the service boundary.
public record AlertRaisedEvent(Guid CaseId, Guid AlertId, string AlertType, string Severity);
```

---

### 3. Audit Trail Annotations

**What:** Mark code that produces audit-relevant records or that deliberately omits audit logging, and reference the applicable regulation.

**Why:** HIPAA requires audit controls that record and examine activity in systems containing ePHI (45 CFR §164.312(b)). Comments tie the code directly to the regulatory requirement, making compliance traceable.

**Standard:** HIPAA Security Rule §164.312(b) — Audit Controls

**When to apply:**
- Event publishing code that produces audit-trail entries
- State transitions on entities (status changes, assignments)
- Any location where an audit-relevant action is intentionally NOT logged (document why)

**Example:**
```csharp
// Audit: CaseCreated event triggers immutable audit record in Audit Service
// Per HIPAA §164.312(b) — all case lifecycle transitions must be traceable
await eventBus.PublishAsync(new CaseCreatedEvent(case.Id, case.PatientId, case.Status));
```

```csharp
// Audit: No audit event for observation list queries (read-only, high volume).
// Read access auditing is deferred to infrastructure-level logging (see observability.md).
```

---

### 4. Access Control Intent

**What:** Document the intended authorization rule for endpoints, data access methods, and event consumers.

**Why:** HIPAA requires role-based access controls (45 CFR §164.312(a)(1)). Documenting intent in code ensures that:
- Reviewers can verify the implementation matches the intent
- Authorization gaps are detectable without reading the full RBAC matrix
- Future changes are evaluated against the original access control design

**Standard:** HIPAA Security Rule §164.312(a)(1) — Access Control; also aligned with NIST 800-66 §4.14

**When to apply:**
- Every public API endpoint
- Service-to-service calls where user context is propagated
- Event consumers that trigger actions under a specific identity

**Example:**
```csharp
// Authorization: CareCoordinator and Clinician roles only.
// Coordinators can update; Clinicians have read-only access.
app.MapPut("/api/v1/cases/{id}/status", async (Guid id, UpdateStatusRequest req) => { ... })
    .RequireAuthorization("CaseWrite");
```

```csharp
// Authorization: System-initiated — no user context.
// Task auto-creation from AlertRaised events runs under service identity.
// Audit records this as actor="system", source="AlertRaised".
public class CreateTaskOnAlertHandler : IEventHandler<AlertRaisedEvent> { ... }
```

---

### 5. Data Retention and Lifecycle

**What:** Document retention requirements, immutability constraints, and deletion restrictions on entities and stores.

**Why:** HIPAA requires that documentation and audit records be retained for a minimum of six years (45 CFR §164.530(j)). State and local laws may extend this. Marking retention rules in code prevents accidental introduction of hard-delete operations.

**Standard:** HIPAA §164.530(j) — Retention Requirements; state-specific retention laws may override (typically 6–10 years for medical records)

**When to apply:**
- Entity definitions or store interfaces where delete is intentionally absent
- Soft-delete implementations
- Archival or purge logic

**Example:**
```csharp
// Retention: Audit records are immutable and must be retained for 6+ years per HIPAA §164.530(j).
// This interface intentionally has no Update or Delete methods.
public interface IAuditStore
{
    Task AppendAsync(AuditEvent record);
    Task<AuditEvent?> GetByEventIdAsync(Guid eventId);
    Task<PaginatedResult<AuditEvent>> QueryAsync(AuditQueryFilter filter);
}
```

```csharp
// Retention: Cases use soft-delete (Status=Closed). No hard-delete endpoint exists.
// Closed cases remain queryable for compliance and audit trail integrity.
```

---

### 6. Security-Sensitive Design Decisions

**What:** Document security-related choices that are not obvious from the code alone — especially trade-offs, intentional omissions, and defense-in-depth layers.

**Why:** Security decisions degrade when their rationale is lost. Future developers may "optimize" away a security control if they don't understand why it exists. This category is informed by OWASP Secure Coding Practices and NIST 800-53 SC (System and Communications Protection) controls.

**Standard:** OWASP Secure Coding Practices; NIST 800-53 SC family; HIPAA §164.312(e)(1) — Transmission Security

**When to apply:**
- Input validation that prevents injection (especially if the validation seems "extra")
- Encryption decisions (why TLS is required for a specific connection)
- Secrets management patterns (why a value comes from environment variables)
- Event payloads that intentionally exclude sensitive data to limit blast radius
- Rate limiting or throttling logic

**Example:**
```csharp
// Security: Connection string loaded from environment variable, never hardcoded.
// See CLAUDE.md credential pattern and Key Vault integration (security-and-compliance.md §Secret Management).
var connectionString = builder.Configuration.GetConnectionString("CaseDb");
```

```csharp
// Security: Event payload excludes patient contact info to limit PHI exposure in transit.
// If the consuming service needs contact details, it must query the Case Service API
// under its own authorization context.
```

```csharp
// Security: Correlation ID is propagated but never trusted for authorization decisions.
// It exists for tracing only. Authorization uses claims from the BFF user context header.
```

---

### 7. Log Scrubbing Annotations

**What:** Mark locations where logging intentionally excludes sensitive data, or where developers must take care not to log certain values.

**Why:** HIPAA prohibits unauthorized disclosure of PHI, which includes accidental exposure in application logs. The security-and-compliance.md defines a logging hygiene policy; these comments enforce it at the code level.

**Standard:** HIPAA §164.502(a) — Uses and Disclosures; §164.312(a)(1) — Access Control (applies to log storage)

**When to apply:**
- Structured log statements near PHI-handling code
- Middleware or filters that process request/response bodies
- Error handlers that might serialize full exception context

**Example:**
```csharp
// Log Hygiene: Log observation ID and type only — never log the observation value (vital sign reading).
// Per logging hygiene policy (security-and-compliance.md §Logging Hygiene).
logger.LogInformation("Observation {ObservationId} of type {Type} processed for case {CaseId}",
    obs.Id, obs.Type, obs.CaseId);
```

```csharp
// Log Hygiene: Do not log request body — contains patient demographics (PHI).
// Only log the request path and correlation ID for tracing.
```

---

## Standards Reference

The commenting standards above are derived from the following regulatory frameworks and industry guidelines:

| Standard | Full Name | Relevant Sections |
|---|---|---|
| **HIPAA Security Rule** | 45 CFR Part 164, Subpart C | §164.312(a) Access Control, §164.312(b) Audit Controls, §164.312(e) Transmission Security |
| **HIPAA Privacy Rule** | 45 CFR Part 164, Subpart E | §164.502(a) Uses and Disclosures, §164.502(b) Minimum Necessary |
| **HIPAA Admin Requirements** | 45 CFR Part 164, Subpart D | §164.530(j) Retention Requirements |
| **NIST SP 800-66** | Implementing the HIPAA Security Rule | §4.14 Access Control, §4.22 Audit Controls |
| **NIST SP 800-53** | Security and Privacy Controls | AC (Access Control), AU (Audit), SC (System and Communications) |
| **OWASP** | Secure Coding Practices | Input validation, output encoding, authentication, access control |
| **HITRUST CSF** | Common Security Framework | Control categories 01 (Access), 06 (Audit), 09 (Communications) |

---

## What NOT to Comment

Not all comments add value. Avoid these patterns:

| Anti-Pattern | Why It's Harmful |
|---|---|
| Restating what the code does (`// increment counter`) | Adds noise, obscures meaningful comments |
| Commenting every method signature | Becomes stale fast, creates false confidence |
| TODO comments without tracking | Accumulates tech debt invisibly |
| Commented-out code | Use version control instead |
| Change logs in comments (`// Modified by X on Y`) | Git history is the authoritative record |
| Explaining basic language features | Assumes readers don't know C# / TypeScript |

**Rule of thumb:** If removing the comment would make a compliance auditor's job harder, the comment belongs. If removing it changes nothing, it doesn't.

---

## Applying These Standards

### New Code
Every PR that introduces PHI handling, access control, audit events, or security decisions should include the appropriate comment category.

### Existing Code
Comments should be added incrementally during feature work — not as a bulk "add comments everywhere" pass. When you touch a file, add any missing compliance comments for the code you're working with.

### Code Review Checklist

When reviewing a PR, verify:

- [ ] PHI touchpoints are marked with `// PHI:` comments
- [ ] New API endpoints document their authorization intent
- [ ] Event payloads document what PHI is included/excluded and why
- [ ] Audit-relevant state transitions reference the applicable rule
- [ ] Log statements near PHI-handling code confirm no sensitive data is logged
- [ ] Security decisions that aren't obvious from the code are explained

---

## Related Documents

- [Security and Compliance](../architecture/security-and-compliance.md) — Full security architecture
- [Coding Boundaries](coding-boundaries.md) — Service boundary rules
- [Comment Reference Catalog](comment-reference-catalog.md) — Copy-paste comment templates for each category

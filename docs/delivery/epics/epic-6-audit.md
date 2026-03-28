# Epic 6: Audit and Compliance

**Epic ID:** `epic:audit`
**Wave:** 5 (combined with Epic 5 in execution)
**Milestone:** v0.5 — Dashboard & Audit
**Estimated effort:** ~3–4 days
**Issues:** 3

---

## Why This Epic Exists

Immutable audit trails are a foundational requirement in healthcare systems. Every state change must be traceable: who did what, when, and to which entity. Even with synthetic data, CareBridge demonstrates this pattern across the entire system. For a portfolio project, the audit trail is a key differentiator that signals security-first thinking.

## Value Delivered

Every domain event is captured as an immutable audit record. The audit trail is queryable by case, actor, action type, entity, and date range. Platform administrators can inspect the full history from the browser. No audit record can be modified or deleted.

## Dependencies

- **Epics 2–4** — all services must be publishing domain events for the audit trail to be meaningful

## Out of Scope

- Field-level access control on audit records
- Audit log export or archival (retention policy)
- Tamper detection or cryptographic chain verification
- Compliance reporting dashboards
- Cosmos DB locally (use in-memory store)

## Exit Criteria

- [ ] Every domain event from all services creates an immutable audit record
- [ ] Audit records are not modifiable (no update or delete API)
- [ ] Audit trail queryable by case, actor, action type, entity, date range
- [ ] Audit section visible on case detail page
- [ ] Standalone audit log page with all filter dimensions works

---

## Delivery Slices

| # | Slice | What it delivers | Issues |
|---|-------|-----------------|--------|
| A1 | Event consumer + storage | Subscribe to all events, write immutable records | A6-01 |
| A2 | Query API | Multi-dimensional filter + pagination | A6-02 |
| A3 | Frontend | Case detail audit section + standalone audit page | A6-03 |

---

## Execution Order

```
A6-01 → A6-02 → A6-03
```

Strictly sequential — each step depends on the previous.

---

## Service Introduced

| Service | Port | Database | Events Published | Events Consumed |
|---------|------|----------|-----------------|-----------------|
| Audit Service | 5080 | In-memory store (Cosmos DB in cloud) | — | All domain events |

---

## Audit Record Schema

```
AuditEvent
├── Id: Guid (unique per record)
├── Timestamp: DateTimeOffset (event occurrence time)
├── CorrelationId: string (links related actions across services)
├── EventType: string (e.g., "CaseCreated", "AlertRaised")
├── Action: string (human-readable, e.g., "Created case", "Raised alert")
├── ActorId: string (user ID or "system")
├── ActorRole: string (e.g., "CareCoordinator", "System")
├── EntityType: string (e.g., "Case", "Alert", "Task")
├── EntityId: Guid (the specific entity affected)
├── CaseId: Guid (for easy case-level queries)
├── ServiceSource: string (e.g., "CaseService", "CareGapEngine")
├── Payload: string (full JSON snapshot of the event data)
```

### Event-to-Audit Mapping

| Event | Action | EntityType | Actor |
|-------|--------|-----------|-------|
| CaseCreated | Created case | Case | From correlation context or "system" |
| CaseUpdated | Updated case status | Case | From correlation context |
| CarePlanActivated | Activated care plan | CarePlan | system |
| MilestoneCompleted | Completed milestone | Milestone | From event (user or system) |
| ObservationReceived | Recorded observation | Observation | system (device) |
| AlertRaised | Raised alert | Alert | system |
| AlertAcknowledged | Acknowledged alert | Alert | From event (user) |
| AlertResolved | Resolved alert | Alert | From event (user) |
| TaskCreated | Created task | Task | system or user |
| TaskCompleted | Completed task | Task | From event (user) |
| AppointmentBooked | Booked appointment | Appointment | From event (user) |
| AppointmentCompleted | Completed appointment | Appointment | From event (user) |
| AppointmentMissed | Missed appointment | Appointment | system |
| NotificationSent | Sent notification | Notification | system |

---

## Issues

### A6-01: Implement Audit Service event consumer and storage

**Labels:** `epic:audit`, `type:feature`, `priority:critical`
**Branch:** `feature/A6-01-audit-event-consumer`

**Description:**
Audit Service consumes all domain events and writes immutable audit records. Uses an in-memory store locally (Cosmos DB in cloud).

**Scope:**

Service: `CareBridge.AuditService` (port 5080)

Event subscriptions (via RabbitMQ):
- All 14 domain event types (same list as Reporting Service)
- Higher retry count: 5 attempts before dead-lettering (audit events are critical)

Event handler: `AuditEventHandler : IEventHandler<IntegrationEvent>`
- Single handler that processes all event types (no type-specific handlers needed)
- For each event:
  1. Check if an audit record with this EventId already exists (idempotency)
  2. Extract fields: actor, entity type, entity ID, case ID, action text
  3. Serialize the full event as the Payload field
  4. Create AuditEvent record
  5. Save to store

Storage: `IAuditStore` interface:
- `Task AppendAsync(AuditEvent record)` — add a record (no update, no delete)
- `Task<AuditEvent?> GetByEventIdAsync(Guid eventId)` — for dedup check
- `Task<PaginatedResult<AuditEvent>> QueryAsync(AuditQueryFilter filter)` — for query API

In-memory implementation:
- `InMemoryAuditStore` using `ConcurrentDictionary<Guid, AuditEvent>`
- Thread-safe append and query
- Query supports filter + sort + pagination

**Immutability enforcement:**
- `IAuditStore` has no Update or Delete methods
- The service exposes no PUT, PATCH, or DELETE endpoints (enforced in A6-02)
- In the Cosmos DB implementation (future), use a read-only access policy for query endpoints

**Acceptance criteria:**
- [ ] Audit Service starts and subscribes to all 14 domain event types
- [ ] Each domain event creates exactly one audit record
- [ ] Audit records include: eventType, action, actor, entityType, entityId, caseId, correlationId, full event payload
- [ ] Duplicate events (same EventId) do not create duplicate audit records
- [ ] Failed processing retries up to 5 times before dead-lettering
- [ ] No update or delete operations exist on the store interface
- [ ] In-memory store is thread-safe

**Dependencies:** All event-producing services

---

### A6-02: Implement Audit Service query API

**Labels:** `epic:audit`, `type:feature`, `priority:high`
**Branch:** `feature/A6-02-audit-query-api`

**Description:**
REST API for querying the audit trail across multiple filter dimensions. Strictly read-only.

**Scope:**

Endpoints:

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/audit` | Query audit records with filters |
| GET | `/api/v1/audit/{recordId}` | Get single audit record with full payload |

Query parameters for list:
- `caseId` — filter by case (most common query)
- `actorId` — actions by a specific user
- `entityType` — filter by entity type (Case, Alert, Task, etc.)
- `entityId` — changes to a specific entity
- `eventType` — filter by event type (CaseCreated, AlertRaised, etc.)
- `from` — timestamp range start (ISO 8601)
- `to` — timestamp range end
- `limit` — page size (default 50, max 200)
- `cursor` — pagination cursor

Filters are combinable: `?caseId=abc&eventType=AlertRaised&from=2026-03-01` returns alert events for a specific case since March 1st.

Response DTOs:
```
AuditRecordResponse
├── Id, Timestamp, CorrelationId, EventType, Action
├── ActorId, ActorRole, EntityType, EntityId, CaseId, ServiceSource
├── Payload: object (deserialized JSON, not raw string)

AuditListResponse
├── Items: List<AuditRecordSummary> (without Payload for list view)
├── NextCursor, HasMore, TotalCount
```

**No mutation endpoints.** There are no POST, PUT, PATCH, or DELETE endpoints on this service. The only way to create audit records is through event consumption.

**Acceptance criteria:**
- [ ] All filter dimensions work individually and in combination
- [ ] Cursor-based pagination works correctly
- [ ] Detail endpoint returns full payload as JSON object
- [ ] List endpoint returns summary (without payload) for performance
- [ ] No PUT, PATCH, or DELETE endpoints exist (verified by test or inspection)
- [ ] TotalCount reflects total matching records
- [ ] Timestamp-range queries work correctly with ISO 8601 dates
- [ ] Empty result set returns empty list (not 404)

**Dependencies:** A6-01

---

### A6-03: Add BFF and React audit trail view

**Labels:** `epic:audit`, `type:feature`, `priority:high`
**Branch:** `feature/A6-03-react-audit`

**Description:**
Wire the audit endpoint through the BFF and build audit views in the React frontend.

**Scope:**

BFF endpoints:
- `GET /api/audit` → Audit Service `GET /api/v1/audit`
- `GET /api/audit/{recordId}` → Audit Service `GET /api/v1/audit/{recordId}`
- `GET /api/cases/{caseId}/audit` → Audit Service `GET /api/v1/audit?caseId={caseId}`
- Add Audit Service HTTP client configuration (base URL: `http://localhost:5080`)

React — Audit section on case detail page (replace placeholder):
- Title: "Recent Activity" with count
- Compact list of last 10 audit entries for this case
- Each row: timestamp, action, actor, entity type
- "View full audit trail" link → standalone page with caseId pre-filtered

React — Standalone audit log page (new route: `/audit`):
- Full-width table with columns: Timestamp, Event Type, Action, Actor, Entity Type, Entity, Case (link), Service
- Filter controls:
  - Case ID (text input or dropdown)
  - Actor (text input)
  - Event Type (dropdown: all event types)
  - Entity Type (dropdown: Case, Alert, Task, etc.)
  - Date range (from/to date pickers)
- Expandable row detail: click a row to expand and show the full event payload as formatted JSON
- Pagination at the bottom

Sidebar navigation:
- "Audit" link activates the audit page

**Acceptance criteria:**
- [ ] Audit section on case detail page shows recent activity for that case
- [ ] Standalone audit page (`/audit`) shows all audit records with working filters
- [ ] All filter controls work individually and combined
- [ ] Expandable row shows full event payload as formatted JSON
- [ ] Date range filter works correctly
- [ ] Pagination works
- [ ] Clicking a case link navigates to the case detail page
- [ ] "View full audit trail" link from case detail pre-fills the caseId filter

**Dependencies:** A6-02, C2-09

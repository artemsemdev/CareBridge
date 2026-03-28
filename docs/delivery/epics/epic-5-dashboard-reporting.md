# Epic 5: Dashboard, Timeline, and Reporting

**Epic ID:** `epic:dashboard-reporting`
**Wave:** 5
**Milestone:** v0.5 — Dashboard & Audit
**Estimated effort:** ~1.5 weeks
**Issues:** 6

---

## Why This Epic Exists

Operational visibility is a core requirement. The dashboard is what a care coordinator looks at all day. The timeline is how anyone understands a patient's journey. This epic proves the CQRS pattern: domain services write to their own databases, while the Reporting Service consumes all domain events and maintains fast, denormalized read models.

## Value Delivered

- The CQRS architecture is proven end-to-end
- The operational dashboard provides at-a-glance KPIs: active cases, open alerts by severity, overdue tasks, pending appointments
- The case timeline shows a unified chronological view of every event in a patient's post-discharge journey
- Dashboard queries hit pre-aggregated read models, not transactional databases

## Dependencies

- **Epics 2–4** — all domain events from Case, Care Plan, Observation, Alert, Task, Appointment, and Notification services feed the reporting pipeline

## Out of Scope

- SLA compliance metrics (percentage of milestones met on time)
- Exportable reports (CSV, PDF)
- Configurable dashboard widgets
- Historical trend charts
- Team workload distribution views (beyond simple counts)
- Cosmos DB for read models locally (use in-memory store)

## Exit Criteria

- [ ] Reporting Service consumes all domain event types and updates read models
- [ ] Dashboard API returns correct operational KPIs
- [ ] Dashboard page renders summary cards, alert queue, and recent cases
- [ ] Case timeline API returns chronological events for a case
- [ ] Timeline component on case detail page shows all events with type-appropriate icons
- [ ] Read model queries are fast (< 200ms locally)

---

## Delivery Slices

| # | Slice | What it delivers | Issues |
|---|-------|-----------------|--------|
| D1 | Event consumers | Reporting Service subscribes to all domain events | D5-01 |
| D2 | Dashboard model | Denormalized dashboard summary + API | D5-02 |
| D3 | Timeline model | Chronological case timeline + API | D5-03 |
| D4 | BFF integration | Dashboard aggregation endpoint | D5-04 |
| D5 | Dashboard UI | React operational dashboard page | D5-05 |
| D6 | Timeline UI | React case timeline component | D5-06 |

---

## Execution Order

```
D5-01 → D5-02 → D5-04 → D5-05
      → D5-03 ──────────→ D5-06
```

- **D5-01** is the foundation — all event consumers
- **D5-02** and **D5-03** can run in parallel (independent read models)
- **D5-04** depends on D5-02 (dashboard read model)
- **D5-05** depends on D5-04 (BFF endpoint)
- **D5-06** depends on D5-03 (timeline read model)

---

## Service Introduced

| Service | Port | Database | Events Published | Events Consumed |
|---------|------|----------|-----------------|-----------------|
| Reporting Service | 5090 | In-memory store (Cosmos DB in cloud) | — | All domain events |

---

## Read Model Design

### Dashboard Summary Model

A single document/object updated incrementally as events arrive:

```
DashboardSummary
├── ActiveCaseCount: int
├── AlertsByStatus: { Open: int, Acknowledged: int }
├── AlertsBySeverity: { Critical: int, High: int, Medium: int, Informational: int }
├── OverdueTaskCount: int
├── OpenTaskCount: int
├── PendingAppointmentCount: int
├── RecentCases: List<RecentCase> (last 10, with Id, PatientName, Status, CreatedAt)
├── TopAlerts: List<TopAlert> (top 10 open, highest severity first, with Id, CaseId, Title, Severity, CreatedAt)
├── LastUpdatedAt: DateTimeOffset
```

Event-to-update mapping:

| Event | Update |
|-------|--------|
| CaseCreated | ActiveCaseCount++, add to RecentCases |
| CaseUpdated (→Closed) | ActiveCaseCount-- |
| AlertRaised | AlertsByStatus.Open++, AlertsBySeverity[severity]++, maybe add to TopAlerts |
| AlertAcknowledged | AlertsByStatus.Open--, AlertsByStatus.Acknowledged++ |
| AlertResolved | AlertsByStatus.Acknowledged--, remove from TopAlerts |
| TaskCreated | OpenTaskCount++ |
| TaskCompleted | OpenTaskCount-- |
| AppointmentBooked | PendingAppointmentCount++ |
| AppointmentCompleted | PendingAppointmentCount-- |
| AppointmentMissed | PendingAppointmentCount-- |

### Case Timeline Model

One document/list per CaseId:

```
CaseTimeline
├── CaseId: Guid
├── Entries: List<TimelineEntry>

TimelineEntry
├── Id: Guid
├── Timestamp: DateTimeOffset
├── EventType: string (e.g., "CaseCreated", "AlertRaised")
├── Category: string (case, careplan, observation, alert, task, appointment, notification)
├── Title: string (human-readable, e.g., "Care plan activated")
├── Description: string (detail, e.g., "General Post-Discharge plan with 5 milestones")
├── Actor: string (system or user ID)
├── Severity: string? (for alerts only)
├── EntityId: Guid? (the specific entity referenced)
```

Event-to-timeline mapping:

| Event | Category | Title | Description |
|-------|----------|-------|-------------|
| CaseCreated | case | Case created | Post-discharge case for {diagnosis} |
| CaseUpdated | case | Case status changed | Status updated to {status} |
| CarePlanActivated | careplan | Care plan activated | {templateName} with {count} milestones |
| MilestoneCompleted | careplan | Milestone completed | {milestoneName} |
| ObservationReceived | observation | Observation recorded | {type}: {value} {unit} |
| AlertRaised | alert | Alert raised: {severity} | {title} — {description} |
| AlertAcknowledged | alert | Alert acknowledged | Acknowledged by {user} |
| AlertResolved | alert | Alert resolved | Resolved by {user} |
| TaskCreated | task | Task created | {title} (Priority: {priority}) |
| TaskCompleted | task | Task completed | Completed by {user} |
| AppointmentBooked | appointment | Appointment booked | {type} scheduled for {date} |
| AppointmentCompleted | appointment | Appointment completed | {type} completed |
| AppointmentMissed | appointment | Appointment missed | {type} was missed (no-show) |
| NotificationSent | notification | Notification sent | {channel}: {subject} |

---

## Issues

### D5-01: Implement Reporting Service event consumers

**Labels:** `epic:dashboard-reporting`, `type:feature`, `priority:critical`
**Branch:** `feature/D5-01-reporting-event-consumers`

**Description:**
The Reporting Service subscribes to all domain events and processes them into denormalized read models. This is the CQRS read-side infrastructure.

**Scope:**

Service: `CareBridge.ReportingService` (port 5090)

Event subscriptions (via RabbitMQ):
- CaseCreated, CaseUpdated
- CarePlanActivated, MilestoneCompleted
- ObservationReceived
- AlertRaised, AlertAcknowledged, AlertResolved
- TaskCreated, TaskCompleted
- AppointmentBooked, AppointmentCompleted, AppointmentMissed
- NotificationSent

Read model storage — in-memory for local dev:
- `IReadModelStore` interface with two implementations:
  - `InMemoryReadModelStore` — thread-safe in-memory dictionaries (ConcurrentDictionary)
  - Future: `CosmosDbReadModelStore` (not built now)
- Registered via DI with configuration switch

Event handlers:
- One handler class per read model: `DashboardEventHandler`, `TimelineEventHandler`
- Each handler receives events and updates the corresponding read model
- Idempotent: processing the same event twice produces the same state (use EventId for dedup)

**Acceptance criteria:**
- [ ] Reporting Service starts and subscribes to all domain event types
- [ ] Events update in-memory read model state
- [ ] EventId-based deduplication prevents double-counting
- [ ] Events arriving out of order are handled gracefully (timeline sorted by timestamp, not arrival order)
- [ ] Logging shows each event processed: "[Reporting] Processed {EventType} for case {CaseId}"
- [ ] In-memory store is registered via DI and swappable

**Dependencies:** All event-producing services (C2-03, C2-05, M3-02, M3-03, W4-02, W4-05, W4-06)

---

### D5-02: Implement dashboard summary read model and API

**Labels:** `epic:dashboard-reporting`, `type:feature`, `priority:critical`
**Branch:** `feature/D5-02-dashboard-read-model`

**Description:**
Build the denormalized dashboard summary providing operational KPIs and expose it via REST API.

**Scope:**

Read model as described in the "Dashboard Summary Model" section above.

API endpoint:
- `GET /api/v1/reports/dashboard` → returns `DashboardSummaryResponse`

Response DTO:
```
DashboardSummaryResponse
├── ActiveCaseCount: int
├── Alerts: { Open: int, Acknowledged: int, BySeverity: { Critical: int, High: int, Medium: int, Informational: int } }
├── Tasks: { Open: int, Overdue: int }
├── Appointments: { Pending: int }
├── RecentCases: List<{ Id, PatientName, Status, DischargeDate, CreatedAt }>
├── TopAlerts: List<{ Id, CaseId, Title, Severity, Age, CreatedAt }>
├── LastUpdatedAt: DateTimeOffset
```

Overdue task detection:
- The dashboard needs to know overdue tasks, but Task Service doesn't publish "overdue" events
- Option A: Reporting Service tracks task creation timestamps and computes overdue based on age + priority
- Option B: Simple approach — count tasks in Open status created more than 24 hours ago as "overdue"
- Use Option B for MVP

**Acceptance criteria:**
- [ ] Dashboard summary returns correct counts reflecting processed events
- [ ] Counts update as new events are processed
- [ ] RecentCases returns last 10 created cases
- [ ] TopAlerts returns top 10 open alerts by severity (Critical first) then age
- [ ] Response time < 200ms (from in-memory store)
- [ ] Empty state returns zero counts (not errors or nulls)

**Dependencies:** D5-01

---

### D5-03: Implement case timeline read model and API

**Labels:** `epic:dashboard-reporting`, `type:feature`, `priority:critical`
**Branch:** `feature/D5-03-timeline-read-model`

**Description:**
Build a chronological event timeline per case and expose it via REST API.

**Scope:**

Read model as described in the "Case Timeline Model" section above.

API endpoint:
- `GET /api/v1/reports/timeline/{caseId}` → returns paginated `TimelineResponse`
- Query parameters: `limit` (default 50), `cursor`, `category` (optional filter)

Response DTO:
```
TimelineResponse
├── CaseId: Guid
├── Entries: List<TimelineEntry>
├── NextCursor: string?
├── HasMore: bool
├── TotalCount: int
```

Timeline entries are always sorted by Timestamp descending (newest first) by default.

Human-readable descriptions:
- Each event type maps to a title and description template (see table in Read Model Design above)
- Descriptions include actual values, not placeholder text

**Acceptance criteria:**
- [ ] Timeline returns all events for a case in chronological order (newest first)
- [ ] Each entry has a human-readable title and description with actual data
- [ ] Events from all services appear: case, care plan, observations, alerts, tasks, appointments, notifications
- [ ] Category filter works (e.g., `?category=alert` shows only alert events)
- [ ] Pagination works for cases with many events
- [ ] TotalCount reflects all entries (not just the current page)
- [ ] Empty timeline for a new case returns empty list (not 404)

**Dependencies:** D5-01

---

### D5-04: Build BFF dashboard aggregation endpoint

**Labels:** `epic:dashboard-reporting`, `type:feature`, `priority:high`
**Branch:** `feature/D5-04-bff-dashboard`

**Description:**
BFF endpoint that calls the Reporting Service for dashboard data and formats it for the frontend.

**Scope:**

BFF endpoints:
- `GET /api/dashboard/summary` → Reporting Service `GET /api/v1/reports/dashboard`
- `GET /api/cases/{caseId}/timeline` → Reporting Service `GET /api/v1/reports/timeline/{caseId}`
- Add Reporting Service HTTP client configuration (base URL: `http://localhost:5090`)

The BFF proxies directly for MVP — no additional aggregation logic needed. The Reporting Service already provides the pre-shaped response.

**Acceptance criteria:**
- [ ] Dashboard endpoint returns summary data from Reporting Service
- [ ] Timeline endpoint returns timeline data for a specific case
- [ ] Correlation ID forwarded to Reporting Service
- [ ] 502 if Reporting Service is unavailable

**Dependencies:** D5-02, D5-03

---

### D5-05: Build React operational dashboard page

**Labels:** `epic:dashboard-reporting`, `type:feature`, `priority:high`
**Branch:** `feature/D5-05-react-dashboard`

**Description:**
The main dashboard page — the landing page for coordinators and operations managers. Shows operational KPIs, the alert queue, and recent cases.

**Scope:**

Replace the placeholder Dashboard page at route `/` (or `/dashboard`):

**Summary cards row:**
- Active Cases (count, blue)
- Open Critical Alerts (count, red if > 0)
- Overdue Tasks (count, amber if > 0)
- Pending Appointments (count, gray)
- Each card is clickable → navigates to the corresponding list page

**Alert queue section:**
- Title: "Alert Queue" with count badge
- Table of top open alerts: Severity badge, Title, Case (link), Age
- Sorted by: Critical first → High → Medium → Informational, then oldest first
- Click alert row → navigate to case detail page
- "View all alerts" link → `/alerts`

**Recent cases section:**
- Title: "Recent Cases"
- Compact list: Patient Name, Status badge, Discharge Date, Days Since Discharge
- Click row → navigate to case detail page
- "View all cases" link → `/cases`

**Refresh behavior:**
- Manual refresh button in header
- React Query with 30-second stale time (auto-refetch when window regains focus)
- Last updated timestamp shown

**Acceptance criteria:**
- [ ] Dashboard page loads and displays all four summary cards with correct counts
- [ ] Alert queue shows alerts sorted by severity and age
- [ ] Recent cases section shows last 10 cases
- [ ] Clicking any card navigates to the appropriate list page
- [ ] Clicking an alert or case navigates to the case detail page
- [ ] Loading skeleton shown while data fetches
- [ ] Manual refresh button works
- [ ] Last updated timestamp displayed

**Dependencies:** D5-04, C2-08

---

### D5-06: Build React case timeline component

**Labels:** `epic:dashboard-reporting`, `type:feature`, `priority:high`
**Branch:** `feature/D5-06-react-timeline`

**Description:**
Add a timeline view to the case detail page showing all events in chronological order. Replace the placeholder Timeline section.

**Scope:**

Timeline component on case detail page:

**Layout:**
- Vertical timeline with a line connecting entries
- Each entry: icon (left), timestamp + title (header), description (body), actor (footer)
- Newest events at the top by default

**Icons and colors by category:**
| Category | Icon | Color |
|----------|------|-------|
| case | Folder | Blue |
| careplan | ClipboardCheck | Green |
| observation | Activity/Heart | Cyan |
| alert | AlertTriangle | Red/Amber (by severity) |
| task | CheckSquare | Purple |
| appointment | Calendar | Teal |
| notification | Bell | Gray |

**Features:**
- Sort toggle: newest first / oldest first
- Category filter chips: All, Case, Care Plan, Observations, Alerts, Tasks, Appointments
- "Load more" pagination for long timelines
- Relative timestamps ("2 hours ago", "yesterday") with full date on hover
- Alert entries show severity badge inline

**Data fetching:**
- React Query hook calling `GET /api/cases/{caseId}/timeline`
- Separate from the main case detail fetch (loaded when timeline section is visible)

**Acceptance criteria:**
- [ ] Timeline appears on case detail page replacing the placeholder
- [ ] All event types rendered with appropriate icons and colors
- [ ] Entries in correct chronological order (newest first by default)
- [ ] Sort toggle switches between newest-first and oldest-first
- [ ] Category filter chips show/hide event types
- [ ] Relative timestamps with hover for absolute date
- [ ] "Load more" works for cases with many events
- [ ] Empty state for cases with no events beyond creation

**Dependencies:** D5-03, C2-09

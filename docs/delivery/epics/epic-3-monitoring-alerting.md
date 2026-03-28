# Epic 3: Remote Monitoring and Alerting

**Epic ID:** `epic:monitoring-alerting`
**Wave:** 3
**Milestone:** v0.3 — Monitoring Loop
**Estimated effort:** ~1.5 weeks
**Issues:** 7

---

## Why This Epic Exists

This is the clinical value loop. Observations come in from simulated devices, get evaluated against thresholds, and abnormal readings or missed milestones generate alerts. Without this, the system is just a static case tracker.

## Value Delivered

Automated clinical surveillance. Simulated vital signs flow into the system and are evaluated without manual review. The Care-Gap Engine detects problems — both from abnormal readings and from missed care plan milestones. Alerts surface in the UI with severity classification.

## Dependencies

- **Epic 1** (Foundation) — shared contracts, middleware, event infrastructure
- **Epic 2** (Case Pipeline) — cases and care plans must exist for observations to attach to

## Out of Scope

- Complex multi-reading correlation rules (e.g., "BP high AND HR low simultaneously")
- Configurable threshold management UI (thresholds are hardcoded for MVP)
- Observation trend analysis or charting
- Device registration or authentication
- FHIR Observation resource mapping

## Exit Criteria

- [ ] POST an observation via the API → validated, stored, event published
- [ ] Abnormal observation → Care-Gap Engine generates an alert with correct severity
- [ ] Normal observation → no alert generated
- [ ] Scheduled scan detects overdue milestones and generates alerts
- [ ] Alerts visible in the React UI on both case detail and standalone alert queue pages
- [ ] Observations visible on the case detail page with abnormal values highlighted

---

## Delivery Slices

| # | Slice | What it delivers | Issues |
|---|-------|-----------------|--------|
| M1 | Observation Service core | Domain model + database + API + validation + dedup | M3-01, M3-02 |
| M2 | Care-Gap Engine rules | Threshold evaluation + alert generation from observations | M3-03 |
| M3 | Milestone scanning | Scheduled detection of overdue milestones | M3-04 |
| M4 | Alert management | Alert query API + acknowledge/resolve lifecycle | M3-05 |
| M5 | Frontend integration | Observations + alerts in the React UI | M3-06, M3-07 |

---

## Execution Order

```
M3-01 → M3-02 → M3-03 → M3-04
                   │       │
                   └──→ M3-05 → M3-07
                          │
M3-06 (parallel with M3-05)
```

- **M3-01** and **M3-02** are sequential (model then API)
- **M3-03** consumes events from M3-02
- **M3-04** can start after M3-03 (reuses Alert entity)
- **M3-05** can start after M3-03 (alert storage exists)
- **M3-06** and **M3-07** are frontend work — M3-06 (observations) is independent of M3-07 (alerts)

---

## Services Introduced

| Service | Port | Database | Events Published | Events Consumed |
|---------|------|----------|-----------------|-----------------|
| Observation Service | 5030 | `carebridge-observation-db` | ObservationReceived | — |
| Care-Gap Engine | 5040 | `carebridge-caregap-db` | AlertRaised, AlertAcknowledged, AlertResolved | ObservationReceived |

---

## Alert Threshold Configuration (MVP)

These thresholds are hardcoded in the Care-Gap Engine for MVP. A future version could make them configurable via admin UI.

| Observation Type | Condition | Severity | Alert Description |
|-----------------|-----------|----------|-------------------|
| BloodPressure (systolic) | > 180 | Critical | Critical: Systolic BP {value} mmHg exceeds 180 |
| BloodPressure (systolic) | > 160 | High | High: Systolic BP {value} mmHg exceeds 160 |
| BloodPressure (systolic) | > 140 | Medium | Elevated: Systolic BP {value} mmHg exceeds 140 |
| HeartRate | > 120 or < 50 | High | Heart rate {value} bpm outside normal range (50-120) |
| SpO2 | < 90 | Critical | Critical: SpO2 {value}% below 90 |
| SpO2 | < 94 | High | Low: SpO2 {value}% below 94 |
| Glucose | > 300 or < 70 | High | Glucose {value} mg/dL outside safe range (70-300) |
| Temperature | > 38.5 | Medium | Elevated temperature: {value}°C |
| Weight | Change > 3kg in 3 days | Medium | Significant weight change detected |

**Note:** Weight change detection requires comparing against recent observations — defer to a later iteration if too complex. Start with absolute thresholds only.

---

## Issues

### M3-01: Implement Observation Service domain model and database

**Labels:** `epic:monitoring-alerting`, `type:feature`, `priority:critical`
**Branch:** `feature/M3-01-observation-domain`

**Description:**
Define the observation entity, types, validation ranges, and database schema for vital sign ingestion.

**Scope:**

Domain model in `CareBridge.ObservationService`:
```
Observation
├── Id: Guid
├── CaseId: Guid
├── Type: ObservationType (BloodPressure, HeartRate, SpO2, Glucose, Temperature, Weight)
├── Value: decimal
├── Unit: string
├── RecordedAt: DateTimeOffset (when the reading was taken)
├── ReceivedAt: DateTimeOffset (when the system received it)
├── DeviceId: string? (optional, simulated device identifier)
├── IdempotencyKey: string (for deduplication)
```

Validation ranges per observation type:

| Type | Unit | Valid Range | Description |
|------|------|-------------|-------------|
| BloodPressure | mmHg | 40–300 | Systolic blood pressure |
| HeartRate | bpm | 20–300 | Heart rate |
| SpO2 | % | 0–100 | Blood oxygen saturation |
| Glucose | mg/dL | 20–600 | Blood glucose |
| Temperature | °C | 30–45 | Body temperature |
| Weight | kg | 20–350 | Body weight |

EF Core setup:
- `ObservationDbContext` with `DbSet<Observation>`
- Index: `IX_Observations_CaseId_RecordedAt` (composite, for time-series queries)
- Unique index: `IX_Observations_IdempotencyKey` (for deduplication)
- Register in db-migrator

**Acceptance criteria:**
- [ ] Migration creates `carebridge-observation-db` with `Observations` table
- [ ] All six observation types defined with correct units
- [ ] Unique constraint on IdempotencyKey prevents duplicates at the database level
- [ ] Composite index on (CaseId, RecordedAt) supports efficient range queries
- [ ] ObservationType stored as string in the database

**Dependencies:** F1-05, F1-06

---

### M3-02: Implement Observation Service REST API with validation

**Labels:** `epic:monitoring-alerting`, `type:feature`, `priority:critical`
**Branch:** `feature/M3-02-observation-api`

**Description:**
Build the observation ingestion API with input validation, idempotency-key deduplication, and event publishing.

**Scope:**

Endpoints:

| Method | Path | Description | Request | Response |
|--------|------|-------------|---------|----------|
| POST | `/api/v1/observations` | Ingest observation | `CreateObservationRequest` + `Idempotency-Key` header | 201 (new) or 200 (duplicate) |
| GET | `/api/v1/observations?caseId={caseId}` | List observations for a case | Query: `caseId` (required), `type`, `from`, `to`, `limit`, `cursor` | 200 + paginated list |

Request DTO:
```
CreateObservationRequest
├── CaseId: Guid (required)
├── Type: ObservationType (required)
├── Value: decimal (required)
├── Unit: string (required)
├── RecordedAt: DateTimeOffset (required)
├── DeviceId: string? (optional)
```

Validation rules:
- CaseId: required, must be a valid GUID
- Type: required, must be a valid ObservationType
- Value: required, must be within type-specific range (see M3-01)
- Unit: required, must match expected unit for the type
- RecordedAt: required, must not be in the future (with 5-minute tolerance for clock skew)
- `Idempotency-Key` header: required on POST

Deduplication logic:
1. Read `Idempotency-Key` from request header
2. Check if an observation with this key already exists in the database
3. If exists: return 200 with the existing observation (no new record created)
4. If not: create new observation, return 201

Event publishing:
- After successful creation (not on dedup hit), publish `ObservationReceived` event
- Event includes: observationId, caseId, type, value, unit, recordedAt

**Acceptance criteria:**
- [ ] Valid observation with new IdempotencyKey returns 201 and persists to database
- [ ] Same IdempotencyKey on retry returns 200 with the original observation (no duplicate)
- [ ] Value outside valid range returns 422 with descriptive error (includes allowed range)
- [ ] Missing required fields return 400
- [ ] `ObservationReceived` event published only for new observations, not dedup hits
- [ ] List endpoint supports filtering by caseId, type, and date range
- [ ] Cursor pagination works correctly

**Dependencies:** M3-01, F1-02

---

### M3-03: Implement Care-Gap Engine — observation threshold rules

**Labels:** `epic:monitoring-alerting`, `type:feature`, `priority:critical`
**Branch:** `feature/M3-03-caregap-threshold-rules`

**Description:**
The Care-Gap Engine consumes `ObservationReceived` events and evaluates readings against configured thresholds. Abnormal values generate alerts.

**Scope:**

Domain model in `CareBridge.CareGapEngine`:
```
Alert
├── Id: Guid
├── CaseId: Guid
├── Type: AlertType (AbnormalReading, MissedMilestone)
├── Severity: Severity (Informational, Medium, High, Critical)
├── Title: string (short summary, e.g., "Critical BP Reading")
├── Description: string (detailed, e.g., "Systolic BP 195 mmHg exceeds critical threshold of 180")
├── SourceEventId: Guid? (ObservationId or MilestoneId that triggered this alert)
├── Status: AlertStatus (Open, Acknowledged, Resolved)
├── CreatedAt: DateTimeOffset
├── AcknowledgedAt: DateTimeOffset?
├── AcknowledgedBy: string?
├── ResolvedAt: DateTimeOffset?
├── ResolvedBy: string?
```

EF Core setup:
- `CareGapDbContext` with `DbSet<Alert>`
- Indexes: `IX_Alerts_CaseId`, `IX_Alerts_Status`, `IX_Alerts_Severity`
- Register in db-migrator

Threshold evaluation:
- Event handler: `IEventHandler<ObservationReceived>`
- Threshold rules loaded from configuration (hardcoded in `appsettings.json` for MVP)
- For each observation, evaluate against all matching rules
- If threshold exceeded: create Alert, save to database, publish `AlertRaised` event
- If multiple rules match (e.g., BP > 140 AND BP > 160), use the highest severity only
- Normal readings: no action (log at Debug level)

`AlertRaised` event includes: alertId, caseId, alertType, severity, title, description, sourceEventId, createdAt

**Acceptance criteria:**
- [ ] Abnormal observation triggers alert creation in `carebridge-caregap-db`
- [ ] Alert severity matches the threshold configuration (highest matching rule wins)
- [ ] Normal observations produce no alerts
- [ ] `AlertRaised` event published with full alert details
- [ ] Multiple abnormal observations for the same case create separate alerts
- [ ] Alert title and description are human-readable with actual values

**Dependencies:** M3-02, F1-02

---

### M3-04: Implement Care-Gap Engine — scheduled milestone scan

**Labels:** `epic:monitoring-alerting`, `type:feature`, `priority:high`
**Branch:** `feature/M3-04-caregap-milestone-scan`

**Description:**
A background job runs periodically to detect overdue milestones and generate alerts. This catches care gaps that aren't triggered by incoming observations.

**Scope:**

Background service in Care-Gap Engine:
- `MilestoneScanBackgroundService` : `BackgroundService`
- Runs every 15 minutes (interval configurable in appsettings)
- On each tick:
  1. Call Care Plan Service API: `GET /api/v1/care-plans?status=Active` (or maintain local state from events — simpler to call the API for MVP)
  2. For each active care plan, check each Pending milestone
  3. If milestone DueAt < now AND no existing alert for this milestone: create alert
  4. Alert type: `MissedMilestone`, severity: `High`
  5. Save alert, publish `AlertRaised` event

Deduplication:
- Before creating an alert, check if an alert already exists with `SourceEventId = milestoneId` and `Type = MissedMilestone`
- If exists: skip (avoid duplicate alerts on every scan)

HTTP client for Care Plan Service:
- Registered as a named HttpClient in DI
- Base URL: `http://localhost:5020` (configurable)

**Acceptance criteria:**
- [ ] Background service starts with the Care-Gap Engine host
- [ ] Overdue milestones generate alerts with type `MissedMilestone` and severity `High`
- [ ] Each overdue milestone generates exactly one alert (no duplicates on subsequent scans)
- [ ] Alert description includes milestone name, due date, and case reference
- [ ] Scan interval is configurable via appsettings
- [ ] Non-overdue milestones are not flagged
- [ ] Already-completed milestones are not flagged

**Dependencies:** M3-03, C2-06

**Technical notes:**
- For MVP, calling the Care Plan Service API is acceptable. A more decoupled approach (maintaining local state from CarePlanActivated events) can be refactored later.
- Log the scan results: "Scanned X active plans, found Y overdue milestones, created Z new alerts"

---

### M3-05: Implement Care-Gap Engine alert query API

**Labels:** `epic:monitoring-alerting`, `type:feature`, `priority:high`
**Branch:** `feature/M3-05-alert-query-api`

**Description:**
REST endpoints for querying and managing alerts. Supports filtering, acknowledgment, and resolution.

**Scope:**

Endpoints:

| Method | Path | Description | Response |
|--------|------|-------------|----------|
| GET | `/api/v1/alerts` | List alerts with filters | 200 + paginated list |
| GET | `/api/v1/alerts/{alertId}` | Get alert detail | 200 + AlertResponse or 404 |
| PATCH | `/api/v1/alerts/{alertId}/acknowledge` | Acknowledge alert | 200 + AlertResponse |
| PATCH | `/api/v1/alerts/{alertId}/resolve` | Resolve alert | 200 + AlertResponse |

Query parameters for list:
- `caseId` — filter by case
- `status` — filter by status (Open, Acknowledged, Resolved)
- `severity` — filter by severity (Informational, Medium, High, Critical)
- `type` — filter by alert type (AbnormalReading, MissedMilestone)
- `limit`, `cursor` — pagination

Response DTO:
```
AlertResponse
├── Id, CaseId, Type, Severity, Title, Description, SourceEventId
├── Status, CreatedAt, AcknowledgedAt, AcknowledgedBy, ResolvedAt, ResolvedBy
├── Age: string (e.g., "2 hours ago", calculated)
```

State transition rules:
- Open → Acknowledged: sets AcknowledgedAt and AcknowledgedBy (from auth context)
- Acknowledged → Resolved: sets ResolvedAt and ResolvedBy
- Open → Resolved: NOT allowed (must acknowledge first)
- Resolved → anything: NOT allowed (terminal state)
- Publish `AlertAcknowledged` event on acknowledge
- Publish `AlertResolved` event on resolve

**Acceptance criteria:**
- [ ] List endpoint supports filtering by case, status, severity, and type individually and combined
- [ ] Acknowledge sets timestamp and user, publishes event
- [ ] Resolve sets timestamp and user, publishes event
- [ ] Cannot resolve without acknowledging first (returns 409)
- [ ] Cannot modify a resolved alert (returns 409)
- [ ] Pagination works correctly
- [ ] 404 for nonexistent alert IDs

**Dependencies:** M3-03

---

### M3-06: Add BFF and React observation display on case detail

**Labels:** `epic:monitoring-alerting`, `type:feature`, `priority:high`
**Branch:** `feature/M3-06-react-observations`

**Description:**
Wire observation data through the BFF and display on the case detail page. Replace the placeholder observation section with real data.

**Scope:**

BFF:
- New endpoint: `GET /api/cases/{caseId}/observations` → proxies to Observation Service `GET /api/v1/observations?caseId={caseId}`
- Add Observation Service HTTP client configuration (base URL: `http://localhost:5030`)

React case detail page — Observations section:
- Table with columns: Type, Value + Unit, Recorded At, Status
- Sorted by RecordedAt descending (newest first)
- Abnormal values highlighted:
  - Use the same threshold ranges as the Care-Gap Engine
  - Values above critical threshold: red background/text
  - Values above high threshold: orange/amber
  - Normal values: default styling
- Show last 10 observations by default with "Load more" / pagination
- Empty state: "No observations recorded yet"
- Type displayed as human-readable label (e.g., "Blood Pressure" not "BloodPressure")

**Acceptance criteria:**
- [ ] Observations section on case detail page shows real observation data
- [ ] Sorted by most recent first
- [ ] Abnormal values visually highlighted with severity-appropriate colors
- [ ] Normal values displayed without special treatment
- [ ] Empty state when no observations exist
- [ ] "Load more" works for cases with many observations
- [ ] BFF proxies correctly to Observation Service

**Dependencies:** M3-02, C2-09

---

### M3-07: Add BFF and React alert display

**Labels:** `epic:monitoring-alerting`, `type:feature`, `priority:high`
**Branch:** `feature/M3-07-react-alerts`

**Description:**
Wire alert data through the BFF and create alert views in the frontend. Both the case detail alert section and a standalone alert queue page.

**Scope:**

BFF:
- New endpoints:
  - `GET /api/cases/{caseId}/alerts` → Care-Gap Engine `GET /api/v1/alerts?caseId={caseId}`
  - `GET /api/alerts` → Care-Gap Engine `GET /api/v1/alerts`
  - `PATCH /api/alerts/{alertId}/acknowledge` → Care-Gap Engine
  - `PATCH /api/alerts/{alertId}/resolve` → Care-Gap Engine
- Add Care-Gap Engine HTTP client configuration (base URL: `http://localhost:5040`)

React case detail page — Alerts section (replace placeholder):
- List of alerts for this case
- Each alert shows: severity badge (color-coded), title, description, age, status
- Acknowledge and Resolve buttons (contextual based on current status)
- Open alerts at the top, resolved at the bottom

React standalone alert queue page (new route: `/alerts`):
- All open alerts across all cases, sorted by severity (Critical first) then by age (oldest first)
- Filterable by: severity, alert type
- Table with columns: Severity, Title, Case (link), Age, Actions (Acknowledge/Resolve)
- Badge in sidebar navigation showing count of open alerts
- Clicking case link navigates to case detail

Alert severity badges:
- Critical: red
- High: orange
- Medium: yellow
- Informational: blue

**Acceptance criteria:**
- [ ] Alerts section on case detail page shows alerts for that case
- [ ] Alert queue page (`/alerts`) shows all open alerts across cases
- [ ] Acknowledge button works and updates the UI optimistically
- [ ] Resolve button works and updates the UI optimistically
- [ ] Critical alerts visually distinguished from lower severity via color badges
- [ ] Sidebar navigation shows open alert count badge
- [ ] Clicking a case link in alert queue navigates to the case detail page
- [ ] Filters on alert queue page work correctly

**Dependencies:** M3-05, C2-09

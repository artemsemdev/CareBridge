# Epic 2: Case Intake Pipeline

**Epic ID:** `epic:case-pipeline`
**Wave:** 2
**Milestone:** v0.2 — Case Pipeline
**Estimated effort:** ~1.5 weeks
**Issues:** 9

---

## Why This Epic Exists

The case is the central operational unit of CareBridge. Without case creation and care plan activation, nothing else in the system has context. This epic proves the first event-driven workflow and delivers the first vertical slice from API through message bus to UI.

## Value Delivered

A discharge event creates a case and automatically triggers a care plan with tracked milestones. The BFF proxies requests to backend services. A React dashboard displays cases and care plans. The entire event-driven architecture is proven end-to-end.

## Dependencies

- **Epic 1** (Foundation) — solution structure, shared contracts, middleware, Docker Compose, service skeleton

## Out of Scope

- Full FHIR discharge bundle parsing (use simplified JSON payload)
- Multiple diagnosis pathways (start with one template: "General Post-Discharge")
- Case closure and archival workflows
- Role-based access control (use mock auth)

## Exit Criteria

- [ ] POST a discharge payload via the BFF → case created in Case Service database
- [ ] CaseCreated event fires on RabbitMQ
- [ ] Care Plan Service consumes the event, instantiates a plan with 5 milestones
- [ ] CarePlanActivated event fires
- [ ] React case list page displays all cases
- [ ] React case detail page shows patient info, care plan, and milestone progress

---

## Delivery Slices

| # | Slice | What it delivers | Issues |
|---|-------|-----------------|--------|
| C1 | Case Service core | Domain model + database + REST API | C2-01, C2-02 |
| C2 | Event publishing | CaseCreated and CaseUpdated events on RabbitMQ | C2-03 |
| C3 | Care Plan Service core | Domain model + database + template engine | C2-04 |
| C4 | Care plan activation | Event-driven care plan creation from CaseCreated | C2-05 |
| C5 | Care Plan API | REST endpoints for care plan queries | C2-06 |
| C6 | BFF wiring | Gateway proxies to Case and Care Plan services | C2-07 |
| C7 | React frontend | Case list + case detail with care plan view | C2-08, C2-09 |

---

## Execution Order

```
C2-01 → C2-02 → C2-03 ──┐
                          ├──→ C2-05 → C2-06 ──→ C2-07 → C2-08 → C2-09
C2-04 ────────────────────┘
```

- **C2-01** and **C2-04** can start in parallel (independent domain models)
- **C2-03** depends on the Case Service API (C2-02)
- **C2-05** depends on both event publishing (C2-03) and Care Plan domain (C2-04)
- **C2-07** depends on both service APIs (C2-02, C2-06)
- **C2-08** and **C2-09** are sequential — shell first, then detail page

---

## Services Introduced

| Service | Port | Database | Events Published | Events Consumed |
|---------|------|----------|-----------------|-----------------|
| Case Service | 5010 | `carebridge-case-db` | CaseCreated, CaseUpdated | — |
| Care Plan Service | 5020 | `carebridge-careplan-db` | CarePlanActivated, MilestoneCompleted | CaseCreated |
| API Gateway / BFF | 5000 | None (stateless) | — | — |
| React Frontend | 3000 | — | — | — |

---

## Issues

### C2-01: Implement Case Service domain model and database

**Labels:** `epic:case-pipeline`, `type:feature`, `priority:critical`
**Branch:** `feature/C2-01-case-domain-model`

**Description:**
Define the Case entity, supporting value objects, EF Core DbContext, and database migrations. This transforms the skeleton service from Epic 1 into the real Case Service.

**Scope:**

Domain model in `CareBridge.CaseService`:
```
Case
├── Id: Guid
├── PatientId: string
├── PatientName: string
├── DischargeDate: DateTimeOffset
├── DiagnosisCode: string (ICD-10 code, e.g., "I50.9")
├── DiagnosisDescription: string (e.g., "Heart failure, unspecified")
├── Status: CaseStatus (Active, Monitoring, Completed, Closed)
├── CreatedAt: DateTimeOffset
├── UpdatedAt: DateTimeOffset
```

EF Core setup:
- `CaseDbContext` with `DbSet<Case> Cases`
- Fluent configuration: PatientId (max 50), PatientName (max 200), DiagnosisCode (max 20), DiagnosisDescription (max 500)
- Database indexes: `IX_Cases_PatientId`, `IX_Cases_Status`, `IX_Cases_CreatedAt`
- Initial migration: `InitialCreate`
- Connection string: `Server=localhost,1433;Database=carebridge-case-db;User Id=sa;Password={env};TrustServerCertificate=True`

Register `CaseDbContext` in the db-migrator tool.

**Acceptance criteria:**
- [ ] Migration runs via db-migrator and creates `carebridge-case-db` with `Cases` table
- [ ] Case entity can be persisted and retrieved via EF Core
- [ ] All three indexes are applied
- [ ] `CaseStatus` enum stored as string in the database

**Dependencies:** F1-05, F1-06

---

### C2-02: Implement Case Service REST API

**Labels:** `epic:case-pipeline`, `type:feature`, `priority:critical`
**Branch:** `feature/C2-02-case-api`

**Description:**
Build the HTTP API endpoints for case management using ASP.NET Core Minimal APIs.

**Scope:**

Endpoints:

| Method | Path | Description | Request Body | Response |
|--------|------|-------------|-------------|----------|
| POST | `/api/v1/cases` | Create case from discharge data | `CreateCaseRequest` | 201 + `CaseResponse` |
| GET | `/api/v1/cases` | List cases | Query: `status`, `limit`, `cursor` | 200 + paginated list |
| GET | `/api/v1/cases/{caseId}` | Get case detail | — | 200 + `CaseResponse` or 404 |
| PATCH | `/api/v1/cases/{caseId}/status` | Update case status | `UpdateStatusRequest` | 200 + `CaseResponse` |

Request DTOs:
```
CreateCaseRequest
├── PatientId: string (required)
├── PatientName: string (required)
├── DischargeDate: DateTimeOffset (required, must not be in future)
├── DiagnosisCode: string (required)
├── DiagnosisDescription: string (required)

UpdateStatusRequest
├── Status: CaseStatus (required)
```

Response DTO:
```
CaseResponse
├── Id, PatientId, PatientName, DischargeDate, DiagnosisCode, DiagnosisDescription, Status, CreatedAt, UpdatedAt

PaginatedResponse<T>
├── Items: List<T>
├── NextCursor: string?
├── HasMore: bool
```

Validation:
- PatientId and PatientName required and non-empty
- DischargeDate must not be in the future
- DiagnosisCode must match pattern (letters, digits, dots)
- RFC 7807 error response for validation failures

Cursor-based pagination:
- Default limit: 20, max: 100
- Cursor is a base64-encoded last-seen ID
- Sort by CreatedAt descending

**Acceptance criteria:**
- [ ] POST `/api/v1/cases` creates a case and returns 201 with Location header
- [ ] GET `/api/v1/cases` returns paginated cases with cursor navigation
- [ ] GET `/api/v1/cases/{caseId}` returns case detail or 404
- [ ] PATCH `/api/v1/cases/{caseId}/status` updates status and returns 200
- [ ] Invalid input returns 400 with RFC 7807 body
- [ ] All responses use `application/json` content type

**Dependencies:** C2-01

**Technical notes:**
- Keep Minimal API endpoints in a dedicated `CaseEndpoints` static class with `MapCaseEndpoints(this WebApplication app)` extension
- Extract business logic into a `CaseService` (application service, not the whole project) that the endpoints call

---

### C2-03: Implement Case Service event publishing

**Labels:** `epic:case-pipeline`, `type:feature`, `priority:critical`
**Branch:** `feature/C2-03-case-events`

**Description:**
After a case is created or updated, publish domain events to RabbitMQ using the shared event infrastructure.

**Scope:**
- After successful `CreateCase`: publish `CaseCreated` event with full case data
- After successful `UpdateStatus`: publish `CaseUpdated` event with case ID, new status, timestamp
- Use `IEventPublisher` from shared infrastructure (implemented in F1-06)
- Correlation ID from the HTTP request must be present in the event envelope
- Events published after the database transaction commits (not inside the transaction)

**Acceptance criteria:**
- [ ] Creating a case publishes a `CaseCreated` event to RabbitMQ
- [ ] Updating case status publishes a `CaseUpdated` event
- [ ] Events contain: caseId, patientId, patientName, diagnosisCode, diagnosisDescription, dischargeDate, status, timestamps
- [ ] Correlation ID is present in the event envelope
- [ ] Events visible in RabbitMQ management UI under the `carebridge.events` exchange
- [ ] If event publishing fails, the case is still created (publish is best-effort, not transactional)

**Dependencies:** C2-02, F1-02

**Technical notes:**
- The decision to publish after commit (not transactionally) is intentional for MVP. A proper outbox pattern is a v2.0 enhancement.
- Log a warning if event publishing fails — don't throw to the caller

---

### C2-04: Implement Care Plan Service domain model and database

**Labels:** `epic:case-pipeline`, `type:feature`, `priority:critical`
**Branch:** `feature/C2-04-careplan-domain-model`

**Description:**
Define care plan entities, milestone definitions, the template system, and seed the default care plan template.

**Scope:**

Domain model in `CareBridge.CarePlanService`:
```
CarePlan
├── Id: Guid
├── CaseId: Guid
├── TemplateName: string
├── Status: CarePlanStatus (Active, Completed)
├── ActivatedAt: DateTimeOffset
├── CompletedAt: DateTimeOffset?
├── Milestones: List<Milestone>

Milestone
├── Id: Guid
├── CarePlanId: Guid
├── Name: string
├── Description: string
├── DueWithinHours: int
├── DueAt: DateTimeOffset (calculated: CarePlan.ActivatedAt + DueWithinHours)
├── Status: MilestoneStatus (Pending, Completed, Missed, Skipped)
├── CompletedAt: DateTimeOffset?
```

Template system (static configuration, not a database table):
```
CarePlanTemplate
├── Name: string
├── MilestoneDefinitions: List<MilestoneDefinition>

MilestoneDefinition
├── Name: string
├── Description: string
├── DueWithinHours: int
```

**Default template — "General Post-Discharge":**

| Milestone | Due Within | Description |
|-----------|-----------|-------------|
| Initial Outreach | 48 hours | Contact patient within 48 hours of discharge |
| First Observation | 72 hours | Receive first vital sign reading from patient |
| Follow-Up Appointment | 168 hours (7 days) | Schedule and confirm follow-up appointment |
| Care Plan Review | 336 hours (14 days) | Mid-point review of care plan progress |
| 30-Day Completion | 720 hours (30 days) | Complete post-discharge monitoring period |

EF Core setup:
- `CarePlanDbContext` with `DbSet<CarePlan>` and `DbSet<Milestone>`
- CarePlan → Milestones: one-to-many relationship
- Index on CaseId (unique — one plan per case for MVP)
- Register in db-migrator

**Acceptance criteria:**
- [ ] Migration creates `carebridge-careplan-db` with `CarePlans` and `Milestones` tables
- [ ] "General Post-Discharge" template is defined in code configuration
- [ ] CarePlan and Milestones can be persisted and retrieved with navigation properties
- [ ] DueAt is calculated correctly from ActivatedAt + DueWithinHours
- [ ] Unique index on CaseId prevents duplicate plans per case

**Dependencies:** F1-05, F1-06

---

### C2-05: Implement care plan activation on CaseCreated event

**Labels:** `epic:case-pipeline`, `type:feature`, `priority:critical`
**Branch:** `feature/C2-05-careplan-activation`

**Description:**
Care Plan Service subscribes to `CaseCreated` events and automatically instantiates a care plan with milestones for the new case. This is the first event-driven workflow in the system.

**Scope:**
- Register an `IEventHandler<CaseCreated>` in Care Plan Service
- On receiving `CaseCreated`:
  1. Check if a care plan already exists for this CaseId (idempotency guard)
  2. Look up the care plan template based on DiagnosisCode (default to "General Post-Discharge")
  3. Create a `CarePlan` record with `ActivatedAt = DateTimeOffset.UtcNow`
  4. Create `Milestone` records from the template, calculating `DueAt` from `ActivatedAt`
  5. Save to database
  6. Publish `CarePlanActivated` event
- If a care plan already exists for this CaseId, log and skip (idempotent)

**Acceptance criteria:**
- [ ] Creating a case via Case Service API triggers care plan creation in Care Plan Service
- [ ] CarePlan has 5 milestones matching the "General Post-Discharge" template
- [ ] Milestones have correct DueAt values (ActivatedAt + DueWithinHours)
- [ ] `CarePlanActivated` event is published with plan ID, case ID, template name, milestone count
- [ ] Processing the same CaseCreated event twice does not create a second care plan
- [ ] Correlation ID from the original request flows through to the CarePlanActivated event

**Dependencies:** C2-03, C2-04

**Technical notes:**
- The event consumer runs as a `BackgroundService` in the Care Plan Service host (pattern from F1-06)
- To verify end-to-end: POST to Case Service → check RabbitMQ → check Care Plan database

---

### C2-06: Implement Care Plan Service REST API

**Labels:** `epic:case-pipeline`, `type:feature`, `priority:high`
**Branch:** `feature/C2-06-careplan-api`

**Description:**
HTTP endpoints for querying care plans and managing milestones.

**Scope:**

Endpoints:

| Method | Path | Description | Response |
|--------|------|-------------|----------|
| GET | `/api/v1/care-plans?caseId={caseId}` | Get care plan for a case | 200 + `CarePlanResponse` or 404 |
| GET | `/api/v1/care-plans/{planId}` | Get care plan with milestones | 200 + `CarePlanResponse` or 404 |
| PATCH | `/api/v1/care-plans/{planId}/milestones/{milestoneId}` | Update milestone status | 200 + `MilestoneResponse` |

Response DTOs:
```
CarePlanResponse
├── Id, CaseId, TemplateName, Status, ActivatedAt, CompletedAt
├── Milestones: List<MilestoneResponse>
├── Progress: object { Completed: int, Total: int, PercentComplete: int }

MilestoneResponse
├── Id, Name, Description, DueAt, Status, CompletedAt, IsOverdue (calculated: Pending && DueAt < now)
```

Milestone update:
- Allowed transitions: Pending → Completed, Pending → Skipped
- Setting status to Completed sets CompletedAt to now
- Publish `MilestoneCompleted` event when status changes to Completed
- Check if all milestones are completed — if so, set CarePlan status to Completed

**Acceptance criteria:**
- [ ] GET by caseId returns the care plan with all milestones including progress summary
- [ ] GET by planId returns the same with milestones
- [ ] PATCH updates milestone status and returns updated milestone
- [ ] `MilestoneCompleted` event published when milestone is marked completed
- [ ] Care plan auto-completes when all milestones are completed
- [ ] Invalid transitions return 409 Conflict
- [ ] 404 for nonexistent plans or milestones

**Dependencies:** C2-04, C2-05

---

### C2-07: Implement BFF proxy endpoints for cases and care plans

**Labels:** `epic:case-pipeline`, `type:feature`, `priority:high`
**Branch:** `feature/C2-07-bff-proxy`

**Description:**
API Gateway / BFF routes frontend requests to the appropriate backend services. This is the only service exposed to the frontend.

**Scope:**

Proxy endpoints:

| BFF Endpoint | Target | Service |
|-------------|--------|---------|
| `POST /api/cases` | `POST /api/v1/cases` | Case Service (5010) |
| `GET /api/cases` | `GET /api/v1/cases` | Case Service (5010) |
| `GET /api/cases/{caseId}` | `GET /api/v1/cases/{caseId}` | Case Service (5010) |
| `GET /api/cases/{caseId}/care-plan` | `GET /api/v1/care-plans?caseId={caseId}` | Care Plan Service (5020) |

HTTP client setup:
- Named `HttpClient` per downstream service registered in DI
- Base URLs configured in `appsettings.Development.json`:
  ```json
  {
    "Services": {
      "CaseService": "http://localhost:5010",
      "CarePlanService": "http://localhost:5020"
    }
  }
  ```
- Correlation ID forwarded on all downstream requests via delegating handler
- Timeout: 10 seconds per downstream call

Mock authentication:
- Dev middleware that accepts any Bearer token (or no token) and creates a claims principal with:
  - `sub`: `dev-user`
  - `name`: `Dev Coordinator`
  - `role`: `CareCoordinator`
- Controlled by configuration flag: `Auth:UseMockAuth = true`

Error handling:
- If a downstream service returns an error, proxy it to the frontend
- If a downstream service is unreachable, return 502 Bad Gateway with Problem Details

**Acceptance criteria:**
- [ ] All proxy endpoints forward requests and return responses correctly
- [ ] Correlation ID is forwarded to downstream services
- [ ] Mock auth middleware creates a valid claims principal
- [ ] Downstream timeout returns 504 Gateway Timeout
- [ ] Downstream connection failure returns 502 Bad Gateway with Problem Details body

**Dependencies:** C2-02, C2-06

**Technical notes:**
- Use `IHttpClientFactory` with named clients — don't create raw `HttpClient` instances
- The BFF strips `/api/` and adds `/api/v1/` when proxying to internal services — this separates external and internal API versions

---

### C2-08: Create React application shell and case list page

**Labels:** `epic:case-pipeline`, `type:feature`, `priority:high`
**Branch:** `feature/C2-08-react-case-list`

**Description:**
Initialize the React frontend with application shell, routing, and the first functional page: the case list.

**Scope:**

Project setup (in `src/web/`):
- React 19 + TypeScript + Vite (from F1-01 scaffold)
- Install and configure: `tailwindcss`, `react-router-dom`, `@tanstack/react-query` (data fetching)
- API client module: base URL configurable via environment variable (`VITE_API_URL=http://localhost:5000`)
- TypeScript types matching the BFF response DTOs

Application shell:
- Layout component with:
  - Left sidebar navigation: Dashboard (placeholder), Cases, Tasks (placeholder), Alerts (placeholder), Audit (placeholder)
  - Top header with app name "CareBridge" and logged-in user display (from mock auth)
  - Main content area with `<Outlet />`
- Routes:
  - `/` → Dashboard (placeholder "Coming Soon" page)
  - `/cases` → Case list page
  - `/cases/:caseId` → Case detail page (next issue)

Case list page:
- Fetch cases from `GET /api/cases` using React Query
- Table display with columns: Patient Name, Diagnosis, Status, Discharge Date, Created
- Status shown as colored badge (Active = blue, Monitoring = yellow, Completed = green, Closed = gray)
- Click row to navigate to `/cases/{caseId}`
- Loading skeleton while data fetches
- Empty state: "No cases found" with explanation
- Error state: retry button

**Acceptance criteria:**
- [ ] `npm run dev` starts the frontend on `http://localhost:3000`
- [ ] Application shell renders with sidebar navigation and header
- [ ] Case list page fetches and displays cases from the BFF
- [ ] Each case row shows patient name, status badge, discharge date, diagnosis
- [ ] Clicking a case row navigates to `/cases/{caseId}`
- [ ] Empty state shown when no cases exist
- [ ] Loading skeleton shown during fetch
- [ ] API error shows retry option

**Dependencies:** C2-07

**Technical notes:**
- Use React Query for server state management — it handles caching, refetching, and loading states
- Don't build a custom design system — use Tailwind utility classes directly
- Keep components in a flat structure for now: `src/web/src/pages/`, `src/web/src/components/`, `src/web/src/api/`

---

### C2-09: Create React case detail page with care plan view

**Labels:** `epic:case-pipeline`, `type:feature`, `priority:high`
**Branch:** `feature/C2-09-react-case-detail`

**Description:**
Build the case detail page — the primary screen a care coordinator uses to manage a specific patient case.

**Scope:**

Page at route `/cases/:caseId`:

**Patient info card:**
- Patient name, patient ID, discharge date, diagnosis code + description
- Status badge
- Days since discharge (calculated from discharge date)

**Care plan section:**
- Plan name and activation date
- Progress bar: X of Y milestones completed
- Milestone list as a table or checklist:
  - Name, description, due date, status
  - Visual indicators: Pending (gray), Completed (green check), Missed (red warning), Skipped (gray strikethrough)
  - Overdue milestones (Pending + DueAt < now) highlighted with warning color
  - Relative time display ("due in 2 days", "3 days overdue")

**Placeholder sections** (empty cards with "Coming in next update" text):
- Recent Observations
- Alerts
- Tasks
- Appointments
- Timeline

**Navigation:**
- Back button/breadcrumb to case list
- Case title in page header

**Data fetching:**
- Parallel fetch: case detail + care plan (two React Query hooks)
- Both must resolve before rendering content

**Acceptance criteria:**
- [ ] Page loads case data and care plan from BFF
- [ ] Patient info card displays all fields correctly
- [ ] Care plan progress bar shows correct completion percentage
- [ ] Milestones displayed with correct status indicators
- [ ] Overdue milestones (pending + past due) highlighted in warning color
- [ ] Placeholder sections visible for future features
- [ ] Back navigation returns to case list
- [ ] 404 page shown for nonexistent case IDs
- [ ] Loading states for both data fetches

**Dependencies:** C2-08

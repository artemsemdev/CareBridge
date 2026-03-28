# Epic 4: Coordinator Workflows

**Epic ID:** `epic:coordinator-workflows`
**Wave:** 4
**Milestone:** v0.4 — Operational Workflows
**Estimated effort:** ~1.5 weeks
**Issues:** 8

---

## Why This Epic Exists

Alerts without actionable tasks are noise. This epic turns detection into operational response: tasks get created automatically from alerts, coordinators manage tasks and appointments, and completing work can advance the care plan. Notifications are logged (not delivered via real channels for MVP).

## Value Delivered

The full operational loop closes. Detection leads to action, and action is tracked to completion. The system goes from "we detected a problem" to "here's who's working on it and here's the proof it was resolved." Care plan milestones advance as tasks and appointments are completed.

## Dependencies

- **Epic 2** (Case Pipeline) — tasks and appointments relate to cases and care plans
- **Epic 3** (Monitoring & Alerting) — alerts trigger automatic task creation

## Out of Scope

- Real email/SMS delivery (log-based notifications only)
- Team-based task assignment and load balancing
- Appointment integration with external calendar systems
- Task templates or automation rules beyond alert→task
- Notification preferences per user
- Reminder scheduling (24-hour appointment reminders)

## Exit Criteria

- [ ] AlertRaised event automatically creates a task with appropriate priority
- [ ] Task lifecycle works: Open → InProgress → Completed/Deferred
- [ ] Completing a relevant task marks the corresponding care plan milestone as completed
- [ ] Appointments can be created, tracked, and completed
- [ ] Completing an appointment satisfies the follow-up appointment milestone
- [ ] Notifications generated for key events and stored/logged
- [ ] Task and appointment management UI works in the React frontend

---

## Delivery Slices

| # | Slice | What it delivers | Issues |
|---|-------|-----------------|--------|
| W1 | Task Service core | Domain model + database + API + events | W4-01, W4-02 |
| W2 | Alert→Task automation | Automatic task creation from AlertRaised events | W4-03 |
| W3 | Task→Milestone link | Task completion advances care plan milestones | W4-04 |
| W4 | Appointment Service | Full appointment lifecycle | W4-05 |
| W5 | Notification Service | Event-driven log-based notifications | W4-06 |
| W6 | Frontend integration | Task management + appointment UI | W4-07, W4-08 |

---

## Execution Order

```
W4-01 → W4-02 → W4-03
              → W4-04
              → W4-07
W4-05 → W4-08
W4-06 (independent, can run anytime after Epic 3)
```

- **W4-01 → W4-02** are sequential (model then API)
- **W4-03**, **W4-04**, and **W4-07** all depend on W4-02 but are independent of each other
- **W4-05** and **W4-08** are independent of the Task Service chain
- **W4-06** only depends on event infrastructure — can run in parallel with anything

---

## Services Introduced

| Service | Port | Database | Events Published | Events Consumed |
|---------|------|----------|-----------------|-----------------|
| Task Service | 5050 | `carebridge-task-db` | TaskCreated, TaskCompleted | AlertRaised |
| Appointment Service | 5060 | `carebridge-appointment-db` | AppointmentBooked, AppointmentCompleted, AppointmentMissed | — |
| Notification Service | 5070 | `carebridge-notification-db` | NotificationSent | AlertRaised, AppointmentBooked, AppointmentMissed, MilestoneCompleted |

---

## Issues

### W4-01: Implement Task Service domain model and database

**Labels:** `epic:coordinator-workflows`, `type:feature`, `priority:critical`
**Branch:** `feature/W4-01-task-domain`

**Description:**
Define the task entity, assignment model, and database schema.

**Scope:**

Domain model in `CareBridge.TaskService`:
```
CareTask (named CareTask to avoid collision with System.Threading.Tasks.Task)
├── Id: Guid
├── CaseId: Guid
├── AlertId: Guid? (null if manually created)
├── Title: string (max 200)
├── Description: string (max 2000)
├── AssignedTo: string? (user ID, null = unassigned)
├── Status: TaskStatus (Open, InProgress, Completed, Deferred)
├── Priority: TaskPriority (Low, Medium, High, Urgent)
├── CreatedAt: DateTimeOffset
├── UpdatedAt: DateTimeOffset
├── CompletedAt: DateTimeOffset?
├── CompletedBy: string?
```

EF Core setup:
- `TaskDbContext` with `DbSet<CareTask>`
- Indexes: `IX_Tasks_CaseId`, `IX_Tasks_Status`, `IX_Tasks_AssignedTo`, `IX_Tasks_AlertId`
- Unique index on AlertId WHERE AlertId IS NOT NULL (one task per alert)
- Register in db-migrator

**Acceptance criteria:**
- [ ] Migration creates `carebridge-task-db` with `CareTasks` table
- [ ] All status and priority values stored as strings
- [ ] Indexes in place for case, status, assignee, and alert queries
- [ ] Unique filtered index on AlertId prevents duplicate tasks per alert

**Dependencies:** F1-05, F1-06

---

### W4-02: Implement Task Service REST API

**Labels:** `epic:coordinator-workflows`, `type:feature`, `priority:critical`
**Branch:** `feature/W4-02-task-api`

**Description:**
CRUD endpoints for task management with event publishing.

**Scope:**

Endpoints:

| Method | Path | Description | Response |
|--------|------|-------------|----------|
| POST | `/api/v1/tasks` | Create task manually | 201 + TaskResponse |
| GET | `/api/v1/tasks` | List tasks with filters | 200 + paginated list |
| GET | `/api/v1/tasks/{taskId}` | Get task detail | 200 + TaskResponse or 404 |
| PATCH | `/api/v1/tasks/{taskId}` | Update task (status, assignment) | 200 + TaskResponse |

Query parameters for list:
- `caseId` — filter by case
- `status` — filter by status
- `priority` — filter by priority
- `assignedTo` — filter by assignee
- `limit`, `cursor` — pagination

Request DTOs:
```
CreateTaskRequest
├── CaseId: Guid (required)
├── Title: string (required)
├── Description: string (required)
├── Priority: TaskPriority (default: Medium)
├── AssignedTo: string? (optional)

UpdateTaskRequest
├── Status: TaskStatus? (optional)
├── AssignedTo: string? (optional)
├── Priority: TaskPriority? (optional)
```

State transition rules:
- Open → InProgress, Open → Deferred
- InProgress → Completed, InProgress → Deferred, InProgress → Open (reopen)
- Deferred → Open (reactivate)
- Completed → NOT allowed (terminal state)
- Setting status to Completed sets CompletedAt and CompletedBy (from auth context)

Events:
- `TaskCreated` published on POST (includes taskId, caseId, alertId, title, priority)
- `TaskCompleted` published when status changes to Completed (includes taskId, caseId, completedBy)

**Acceptance criteria:**
- [ ] POST creates a task and returns 201
- [ ] GET list supports filtering by case, status, priority, and assignee (individually and combined)
- [ ] PATCH updates status and/or assignment
- [ ] Invalid state transitions return 409 Conflict
- [ ] Cannot modify a completed task (returns 409)
- [ ] `TaskCreated` event published on creation
- [ ] `TaskCompleted` event published on completion
- [ ] Pagination with cursor works correctly

**Dependencies:** W4-01

---

### W4-03: Implement automatic task creation from alerts

**Labels:** `epic:coordinator-workflows`, `type:feature`, `priority:critical`
**Branch:** `feature/W4-03-alert-to-task`

**Description:**
Task Service consumes `AlertRaised` events and automatically creates a task for the care coordinator. This closes the detection → action loop.

**Scope:**

Event handler: `IEventHandler<AlertRaised>` in Task Service

Task generation logic:
1. Check if a task already exists with this AlertId (idempotency via unique index)
2. If exists: log and skip
3. Map alert to task:

| Alert Severity | Task Priority |
|---------------|---------------|
| Critical | Urgent |
| High | High |
| Medium | Medium |
| Informational | Low |

4. Title generation:
   - AbnormalReading: "Review {severity} {observationType} alert for case {caseId}"
   - MissedMilestone: "Follow up on missed milestone: {milestoneName} for case {caseId}"
5. Description: Include the full alert description from the event
6. AssignedTo: null (unassigned — coordinator picks up from queue)
7. Save task, publish `TaskCreated` event

**Acceptance criteria:**
- [ ] `AlertRaised` event creates a task in the Task Service database
- [ ] Task priority correctly maps from alert severity
- [ ] Task title is descriptive and includes alert context
- [ ] Task includes reference to originating AlertId and CaseId
- [ ] Duplicate `AlertRaised` events do not create duplicate tasks (idempotent)
- [ ] `TaskCreated` event published for the new task

**Dependencies:** W4-02, M3-03

---

### W4-04: Implement task completion updates care plan milestone

**Labels:** `epic:coordinator-workflows`, `type:feature`, `priority:high`
**Branch:** `feature/W4-04-task-milestone-link`

**Description:**
When a task is completed that was generated from a milestone-related alert, the Care Plan Service should mark the corresponding milestone as completed.

**Scope:**

Care Plan Service adds a new event handler: `IEventHandler<TaskCompleted>`

Logic:
1. Receive `TaskCompleted` event
2. Look up the task's AlertId (included in the event or looked up from Task Service — prefer including it in the event)
3. If AlertId is null (manual task): skip
4. Look up the alert source: if the alert was of type `MissedMilestone`, the `SourceEventId` is the MilestoneId
5. Look up the milestone by ID in the local care plan database
6. If milestone status is still Pending or Missed: mark as Completed, set CompletedAt
7. Publish `MilestoneCompleted` event
8. Check if all milestones are now completed → if so, complete the care plan

**Enriching TaskCompleted event:**
- Add `AlertId: Guid?` to the `TaskCompleted` event contract (update shared contracts)
- Task Service populates this from the CareTask entity when publishing

**Acceptance criteria:**
- [ ] Completing a task that originated from a MissedMilestone alert marks the milestone as completed
- [ ] `MilestoneCompleted` event published
- [ ] Non-milestone tasks (AbnormalReading alerts or manual tasks) do not affect milestones
- [ ] Care plan auto-completes if this was the last pending milestone
- [ ] If milestone was already completed (by manual action), the event handler is a no-op
- [ ] Milestone completion is visible in the Care Plan API response

**Dependencies:** W4-02, C2-06

**Technical notes:**
- This creates a cross-service workflow: Task Service → event → Care Plan Service. The coupling is through the event, not a direct API call.
- Consider also handling tasks created from "Initial Outreach" or "First Observation" alerts — the matching logic can use the alert description or a structured tag. For MVP, matching on MissedMilestone type + SourceEventId is sufficient.

---

### W4-05: Implement Appointment Service domain model, database, and API

**Labels:** `epic:coordinator-workflows`, `type:feature`, `priority:high`
**Branch:** `feature/W4-05-appointment-service`

**Description:**
Full appointment management: domain model, database, REST API, and event publishing. Also includes milestone satisfaction when appointments are completed.

**Scope:**

Domain model in `CareBridge.AppointmentService`:
```
Appointment
├── Id: Guid
├── CaseId: Guid
├── Type: AppointmentType (FollowUp, LabWork, Specialist)
├── ScheduledAt: DateTimeOffset
├── Status: AppointmentStatus (Proposed, Booked, Completed, Canceled, NoShow)
├── Notes: string? (max 1000)
├── CreatedAt: DateTimeOffset
├── UpdatedAt: DateTimeOffset
├── CompletedAt: DateTimeOffset?
```

EF Core setup:
- `AppointmentDbContext` with `DbSet<Appointment>`
- Indexes: `IX_Appointments_CaseId`, `IX_Appointments_Status`, `IX_Appointments_ScheduledAt`
- Register in db-migrator

Endpoints:

| Method | Path | Description | Response |
|--------|------|-------------|----------|
| POST | `/api/v1/appointments` | Create appointment | 201 + AppointmentResponse |
| GET | `/api/v1/appointments` | List with filters | 200 + paginated list |
| GET | `/api/v1/appointments/{id}` | Get detail | 200 or 404 |
| PATCH | `/api/v1/appointments/{id}` | Update status/notes | 200 + AppointmentResponse |

Query parameters: `caseId`, `status`, `from`, `to`, `limit`, `cursor`

State transitions:
- Proposed → Booked, Proposed → Canceled
- Booked → Completed, Booked → Canceled, Booked → NoShow
- Completed, Canceled, NoShow: terminal states

Events:
- `AppointmentBooked` on transition to Booked (appointmentId, caseId, type, scheduledAt)
- `AppointmentCompleted` on transition to Completed (appointmentId, caseId, completedAt)
- `AppointmentMissed` on transition to NoShow (appointmentId, caseId, scheduledAt)

**Milestone satisfaction:**
- Care Plan Service adds event handler for `AppointmentCompleted`
- On receiving: check if the case's care plan has a "Follow-Up Appointment" milestone in Pending status
- If so: mark it as Completed, publish `MilestoneCompleted`

**Acceptance criteria:**
- [ ] CRUD operations work for appointments
- [ ] All status transitions enforce valid paths (invalid transitions return 409)
- [ ] `AppointmentBooked` event published on booking
- [ ] `AppointmentCompleted` event published on completion
- [ ] `AppointmentMissed` event published on no-show
- [ ] Completing an appointment satisfies the "Follow-Up Appointment" care plan milestone
- [ ] Pagination and filtering work correctly

**Dependencies:** F1-05, F1-06, C2-06

---

### W4-06: Implement Notification Service — event consumer with log-based delivery

**Labels:** `epic:coordinator-workflows`, `type:feature`, `priority:medium`
**Branch:** `feature/W4-06-notification-service`

**Description:**
Notification Service consumes domain events and generates notification records. Delivery is simulated via structured log output for MVP.

**Scope:**

Domain model in `CareBridge.NotificationService`:
```
Notification
├── Id: Guid
├── CaseId: Guid
├── Channel: NotificationChannel (Email, InApp, SMS)
├── Recipient: string (email address, user ID, or phone number)
├── Subject: string
├── Body: string
├── Status: NotificationStatus (Sent, Failed)
├── TriggeredBy: string (event type that caused this notification)
├── CreatedAt: DateTimeOffset
```

Event subscriptions and templates:

| Event | Channel | Recipient | Subject Template |
|-------|---------|-----------|-----------------|
| AlertRaised (Critical/High) | Email, InApp | Care coordinator | "Alert: {alert.Title} — Case {caseId}" |
| AlertRaised (Medium) | InApp | Care coordinator | "Notice: {alert.Title} — Case {caseId}" |
| AppointmentBooked | InApp | Care coordinator | "Appointment scheduled — Case {caseId}" |
| AppointmentMissed | Email, InApp | Care coordinator | "Missed appointment — Case {caseId}" |
| MilestoneCompleted | InApp | Care coordinator | "Milestone completed: {milestoneName}" |

Template rendering:
- Simple string interpolation using event properties
- Body includes: event details, case reference, timestamp

"Delivery":
- Log the notification at Information level with full content:
  ```
  [Notification] Channel=Email, To=coordinator@carebridge.local, Subject="Alert: Critical BP Reading — Case abc-123", Body="..."
  ```
- Set status to `Sent` (simulated success)

Storage:
- `NotificationDbContext` (can use in-memory database or SQLite for simplicity, or full SQL Server to match pattern)
- Or use the existing SQL Server pattern for consistency

Query API:
- `GET /api/v1/notifications?caseId={caseId}` — notification history for a case
- Cursor pagination, sorted by CreatedAt descending

**Acceptance criteria:**
- [ ] AlertRaised (Critical/High) creates Email + InApp notifications
- [ ] AlertRaised (Medium) creates InApp notification only
- [ ] AppointmentBooked creates InApp notification
- [ ] AppointmentMissed creates Email + InApp notifications
- [ ] MilestoneCompleted creates InApp notification
- [ ] All notifications logged with full content at Information level
- [ ] Notification records persisted and queryable via API
- [ ] Templates render correctly with event data (no placeholder text in output)
- [ ] `NotificationSent` event published for each notification (consumed by Reporting/Audit later)

**Dependencies:** F1-06, M3-03

---

### W4-07: Add BFF and React task management UI

**Labels:** `epic:coordinator-workflows`, `type:feature`, `priority:high`
**Branch:** `feature/W4-07-react-tasks`

**Description:**
Wire task endpoints through the BFF and build the task management interface in React.

**Scope:**

BFF endpoints:
- `GET /api/tasks` → Task Service `GET /api/v1/tasks`
- `GET /api/cases/{caseId}/tasks` → Task Service `GET /api/v1/tasks?caseId={caseId}`
- `POST /api/tasks` → Task Service `POST /api/v1/tasks`
- `PATCH /api/tasks/{taskId}` → Task Service `PATCH /api/v1/tasks/{taskId}`
- Add Task Service HTTP client configuration (base URL: `http://localhost:5050`)

React — Task list page (new route: `/tasks`):
- Table: Title, Case (link), Priority, Status, Assigned To, Age
- Filters: status dropdown, priority dropdown
- Priority badges: Urgent (red), High (orange), Medium (yellow), Low (gray)
- Status badges: Open (blue), InProgress (amber), Completed (green), Deferred (gray)
- Click row to expand or navigate to detail
- Status transition buttons: "Start" (Open→InProgress), "Complete" (InProgress→Completed), "Defer" (→Deferred)
- Create task button → modal or inline form

React — Task section on case detail page:
- Replace placeholder with actual task list for this case
- Show task count in section header
- Same task display as the main list but filtered by case

React — Create task form:
- Fields: Title, Description, Priority (dropdown), Assigned To (text input)
- CaseId pre-filled when creating from case detail page
- Submit calls POST `/api/tasks`

Sidebar navigation:
- Tasks item shows badge with count of Open tasks (fetch count on app load)

**Acceptance criteria:**
- [ ] Task list page (`/tasks`) shows all tasks with working filters
- [ ] Status transition buttons work and update UI optimistically
- [ ] Tasks section on case detail page shows tasks for that case
- [ ] Create task form creates a new task
- [ ] Priority and status badges use correct colors
- [ ] Sidebar badge shows open task count
- [ ] Clicking case link navigates to case detail

**Dependencies:** W4-02, C2-09

---

### W4-08: Add BFF and React appointment management UI

**Labels:** `epic:coordinator-workflows`, `type:feature`, `priority:high`
**Branch:** `feature/W4-08-react-appointments`

**Description:**
Wire appointment endpoints through the BFF and build appointment views in the React frontend.

**Scope:**

BFF endpoints:
- `GET /api/cases/{caseId}/appointments` → Appointment Service `GET /api/v1/appointments?caseId={caseId}`
- `POST /api/appointments` → Appointment Service `POST /api/v1/appointments`
- `PATCH /api/appointments/{id}` → Appointment Service `PATCH /api/v1/appointments/{id}`
- Add Appointment Service HTTP client configuration (base URL: `http://localhost:5060`)

React — Appointment section on case detail page (replace placeholder):
- List of appointments for this case
- Each row: Type, Scheduled Date/Time, Status, Notes (truncated)
- Status badges: Proposed (gray), Booked (blue), Completed (green), Canceled (gray strikethrough), NoShow (red)
- Action buttons: "Confirm" (Proposed→Booked), "Complete" (Booked→Completed), "No Show" (Booked→NoShow), "Cancel" (→Canceled)
- Past-due appointments in Booked status highlighted with warning

React — Create appointment form:
- Fields: Type (dropdown: Follow-Up, Lab Work, Specialist), Date/Time picker, Notes (optional)
- CaseId pre-filled
- "Create as Proposed" and "Create as Booked" buttons
- Validation: scheduled date must be in the future

**Acceptance criteria:**
- [ ] Appointment section on case detail page shows appointments for that case
- [ ] Create appointment form works with date/time picker
- [ ] All status transitions work from the UI via action buttons
- [ ] Invalid transitions are not offered as button options
- [ ] Past-due booked appointments visually flagged (amber/warning background)
- [ ] Type displayed as human-readable label
- [ ] BFF proxies correctly to Appointment Service

**Dependencies:** W4-05, C2-09

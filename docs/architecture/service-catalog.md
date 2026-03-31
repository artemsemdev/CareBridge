# Service Catalog

**Document type:** Service reference
**Status:** Living document
**Last updated:** 2026-03-27

---

## Purpose of This Document

This catalog is the authoritative reference for every deployable service in the CareBridge platform. It defines what each service does, what data it owns, how it communicates, and why it exists as an independent unit of deployment. The intent is to give any engineer enough context to reason about service boundaries, failure blast radius, and operational behavior without reading source code.

For higher-level context on how these services compose into a running system, see [Solution Architecture](./solution-architecture.md). For data ownership boundaries, storage technology choices, and event schema conventions, see [Data Architecture](./data-architecture.md).

---

## Service Inventory at a Glance

| # | Service | Runtime | Primary Store | Owns Business Data | Event Producer | Event Consumer |
|---|---------|---------|---------------|-------------------|----------------|----------------|
| 1 | API Gateway / BFF | ASP.NET Core | None (stateless) | No | No | No |
| 2 | Case Service | ASP.NET Core | Azure SQL | Yes | Yes | Yes |
| 3 | Care Plan Service | ASP.NET Core | Azure SQL | Yes | Yes | Yes |
| 4 | Observation Ingestion Service | ASP.NET Core | Azure SQL | Yes | Yes | No |
| 5 | Care-Gap Engine | ASP.NET Core | Azure SQL (config only) | Rules config | Yes | Yes |
| 6 | Task Service | ASP.NET Core | Azure SQL | Yes | Yes | Yes |
| 7 | Appointment Service | ASP.NET Core | Azure SQL | Yes | Yes | Yes |
| 8 | Notification Service | ASP.NET Core | Azure SQL (logs) | Delivery logs | No | Yes |
| 9 | Audit Service | ASP.NET Core | Cosmos DB | Yes | No | Yes |
| 10 | Reporting / Read Model Service | ASP.NET Core | In-memory (Cosmos DB in cloud) | Derived views | No | Yes |

All services run as containers on AKS. All inter-service communication uses Azure Service Bus for asynchronous messaging and direct HTTP only through the API Gateway for synchronous reads.

---

## 1. API Gateway / BFF (Backend for Frontend)

### Purpose

Edge entry point for the React UI. Aggregates downstream service responses into shapes the frontend needs, propagates authentication context, and shields internal services from direct internet exposure.

### Responsibilities

- Terminate TLS and validate JWTs issued by Microsoft Entra ID.
- Propagate authenticated user identity (claims, roles) to downstream services via internal headers.
- Aggregate multiple downstream calls into single responses for dashboard views (BFF pattern).
- Route requests to the correct internal service.
- Apply rate limiting and request throttling at the edge.
- Return consistent error envelopes to the frontend.

### Owned Data

None. This service is stateless and owns no business data. Session affinity is not required.

### Public APIs

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/dashboard/summary` | Aggregated dashboard view (calls Reporting Service) |
| GET | `/api/cases/{caseId}` | Proxied to Case Service |
| GET | `/api/cases/{caseId}/care-plan` | Proxied to Care Plan Service |
| GET | `/api/cases/{caseId}/observations` | Proxied to Observation Ingestion Service |
| GET | `/api/cases/{caseId}/tasks` | Proxied to Task Service |
| POST | `/api/tasks` | Proxied to Task Service |
| GET | `/api/cases/{caseId}/appointments` | Proxied to Appointment Service |
| POST | `/api/appointments` | Proxied to Appointment Service |
| GET | `/api/audit/{caseId}` | Proxied to Audit Service |
| GET | `/api/notifications/history` | Proxied to Notification Service |

All endpoints require a valid Bearer token. The gateway validates the token before forwarding.

### Events Published

None.

### Events Consumed

None.

### Dependencies

| Dependency | Type | Purpose |
|------------|------|---------|
| Microsoft Entra ID | External | JWT validation, JWKS endpoint |
| Case Service | Internal HTTP | Case queries |
| Care Plan Service | Internal HTTP | Care plan queries |
| Observation Ingestion Service | Internal HTTP | Observation queries |
| Task Service | Internal HTTP | Task CRUD |
| Appointment Service | Internal HTTP | Appointment CRUD |
| Reporting / Read Model Service | Internal HTTP | Dashboard aggregation |
| Audit Service | Internal HTTP | Audit log queries |
| Notification Service | Internal HTTP | Notification history |

### Security Considerations

- The gateway is the only service with a public ingress. All other services accept traffic only from the internal AKS virtual network.
- JWT signature validation uses the Entra ID JWKS endpoint with key caching and rotation awareness.
- Role-based access control (RBAC) claims in the JWT determine which downstream calls are permitted.
- All forwarded requests include an `X-Correlation-Id` header for distributed tracing.
- Rate limiting prevents credential-stuffing and abuse of aggregation endpoints.

### Scaling Considerations

- Stateless: scales horizontally without constraint.
- CPU-bound during JWT validation and response aggregation. Profile under load to size pod requests.
- Consider response caching (short TTL) for dashboard aggregation to reduce fanout.
- HPA trigger: CPU utilization and request latency p95.

### Operational Concerns

- Health check endpoint at `/healthz` (liveness) and `/readyz` (readiness, verifies downstream reachability).
- Circuit breakers on every downstream call. A single failing service must not cascade to the entire UI.
- Structured JSON logging with correlation IDs for every request.
- Timeout budgets: each downstream call gets a fraction of the total request timeout, not the full budget.

### Failure Modes

| Failure | Impact | Mitigation |
|---------|--------|------------|
| Entra ID JWKS endpoint unreachable | New tokens cannot be validated | Cache JWKS keys with reasonable TTL; existing cached keys continue to work |
| Downstream service unavailable | Partial dashboard failure | Circuit breaker opens; gateway returns partial response with degradation indicator |
| Pod crash | Dropped in-flight requests | Multiple replicas behind Kubernetes Service; client retry with idempotency |

### Why It Exists as a Separate Service

The BFF pattern isolates frontend-specific aggregation logic from domain services. Without it, either the frontend makes N parallel calls (poor mobile performance, CORS complexity) or domain services take on presentation concerns. A dedicated gateway also centralizes auth validation, rate limiting, and observability at the edge.

---

## 2. Case Service

### Purpose

Owns the post-discharge case lifecycle. A case is the central coordination entity -- it represents a single patient's post-discharge care episode and is the anchor point that other services reference.

### Responsibilities

- Receive discharge bundles (FHIR-aligned) and create new cases.
- Maintain case state machine: `active` -> `escalated` -> `closed` (with valid transitions).
- Store patient demographics, discharge summary reference, assigned care team, and case metadata.
- Expose case queries by ID, patient, status, and care team assignment.
- Enforce that case state transitions are valid and auditable.

### Owned Data

| Entity | Storage | Description |
|--------|---------|-------------|
| Case | Azure SQL | Case ID, patient reference, status, created date, closed date, discharge summary reference |
| CaseAssignment | Azure SQL | Care team member assignments to cases |
| CaseStatusHistory | Azure SQL | Append-only log of status transitions |

### Public APIs

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/cases` | Create a case from a discharge bundle |
| GET | `/cases/{caseId}` | Retrieve case details |
| GET | `/cases?status={status}&assignedTo={userId}` | Query cases by status and/or assignment |
| PATCH | `/cases/{caseId}/status` | Transition case status |
| PUT | `/cases/{caseId}/assignments` | Update care team assignments |

### Events Published

| Event | Trigger | Key Payload Fields |
|-------|---------|-------------------|
| `CaseCreated` | New case persisted | caseId, patientId, dischargeSummaryRef, carePlanTemplateId |
| `CaseStatusChanged` | Status transition committed | caseId, previousStatus, newStatus, changedBy, reason |

### Events Consumed

| Event | Source | Action |
|-------|--------|--------|
| `DischargeReceived` | External integration / simulated | Initiates case creation workflow |

### Dependencies

| Dependency | Type | Purpose |
|------------|------|---------|
| Azure SQL | Infrastructure | Persistent storage |
| Azure Service Bus | Infrastructure | Event publishing |
| Microsoft Entra ID | External | Token validation (via internal header from gateway) |

### Security Considerations

- Case data contains patient references. Access is restricted by care team assignment -- a user can only query cases they are assigned to.
- All writes record the authenticated user ID for traceability.
- Database-level encryption at rest (Azure SQL TDE). No PHI in log output.

### Scaling Considerations

- Write volume correlates with discharge rate (predictable, not bursty in real usage).
- Read-heavy for dashboard queries. The Reporting Service offloads most read traffic via materialized views.
- Azure SQL elastic pool or DTU scaling based on connection count and query latency.
- HPA trigger: CPU and request queue depth.

### Operational Concerns

- Outbox pattern for reliable event publishing: case write and outbox insert in the same database transaction. A background publisher polls the outbox and sends to Service Bus.
- Idempotent case creation keyed on discharge bundle ID to prevent duplicates from retry storms.
- Database migrations managed via EF Core migrations, applied during deployment with rollback scripts.

### Failure Modes

| Failure | Impact | Mitigation |
|---------|--------|------------|
| Azure SQL unavailable | Cannot create or query cases | Retry with backoff; circuit breaker in gateway returns 503 |
| Service Bus unavailable | Events not published | Outbox retains unpublished events; publisher retries on recovery |
| Duplicate DischargeReceived | Potential duplicate case | Idempotency key on discharge bundle ID |

### Why It Exists as a Separate Service

The case is the central aggregate in the domain. Keeping it in its own service means case lifecycle logic (state machine, assignment rules, discharge intake) is not entangled with care plan logic, observation processing, or task management. Every other service references a `caseId` but never modifies case state directly -- they communicate through events.

---

## 3. Care Plan Service

### Purpose

Owns care plan templates and per-case milestone instances. When a case is created, this service activates the appropriate care plan template and tracks milestone completion throughout the care episode.

### Responsibilities

- Maintain a library of care plan templates (per pathway: cardiac, surgical, respiratory, etc.).
- On `CaseCreated`, instantiate the correct template as a set of concrete milestones with target dates.
- Track milestone states: `pending` -> `completed` | `overdue`.
- Evaluate milestone target dates on a schedule and flag overdue milestones.
- Expose care plan and milestone data for the case detail view.

### Owned Data

| Entity | Storage | Description |
|--------|---------|-------------|
| CarePlanTemplate | Azure SQL | Template ID, pathway, ordered milestones with relative due offsets |
| CarePlanInstance | Azure SQL | Instance ID, caseId, templateId, activation date |
| Milestone | Azure SQL | Milestone ID, plan instance, type, target date, status, completion date |

### Public APIs

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/care-plans/templates` | List available templates |
| GET | `/care-plans/{caseId}` | Retrieve the active care plan for a case |
| GET | `/care-plans/{caseId}/milestones` | List milestones with status |
| PATCH | `/care-plans/{caseId}/milestones/{milestoneId}` | Mark milestone as completed |

### Events Published

| Event | Trigger | Key Payload Fields |
|-------|---------|-------------------|
| `MilestoneCompleted` | Milestone marked complete | caseId, milestoneId, milestoneType, completedAt |
| `MilestoneOverdue` | Scheduled check finds past-due milestone | caseId, milestoneId, milestoneType, targetDate, daysOverdue |

### Events Consumed

| Event | Source | Action |
|-------|--------|--------|
| `CaseCreated` | Case Service | Instantiate care plan from template |

### Dependencies

| Dependency | Type | Purpose |
|------------|------|---------|
| Azure SQL | Infrastructure | Template and instance storage |
| Azure Service Bus | Infrastructure | Event publishing and consumption |

### Security Considerations

- Care plan data is scoped to a case. Access control follows the same care-team-based restriction as the Case Service.
- Template management (create/edit templates) is restricted to administrative roles.

### Scaling Considerations

- The overdue-check job runs on a timer (e.g., every 15 minutes). At scale, partition checks by case cohort to avoid a single large query.
- Read-heavy for the care plan detail view. Indexed queries on caseId.
- HPA trigger: CPU and message backlog on the `CaseCreated` subscription.

### Operational Concerns

- The overdue evaluator is a background hosted service within the same deployment. It acquires a distributed lock (Azure Blob lease) to prevent duplicate evaluation across replicas.
- Template changes do not retroactively modify active plan instances. A new template version creates a new template record.

### Failure Modes

| Failure | Impact | Mitigation |
|---------|--------|------------|
| CaseCreated event missed | No care plan activated for a case | Service Bus dead-letter monitoring; manual reconciliation endpoint |
| Overdue evaluator crashes | Late detection of overdue milestones | Lease-based failover to another replica; alert on evaluation gap |
| Azure SQL unavailable | Cannot read or update milestones | Retry with backoff; gateway circuit breaker |

### Why It Exists as a Separate Service

Care plan logic (templates, milestone scheduling, overdue evaluation) is a distinct subdomain from case lifecycle management. Templates evolve independently. The overdue evaluation loop has different scaling and scheduling characteristics than the synchronous CRUD of the Case Service. Separating them prevents a milestone evaluation storm from affecting case creation throughput.

---

## 4. Observation Ingestion Service

### Purpose

Receives, validates, deduplicates, and stores simulated remote patient monitoring (RPM) readings. This service is the single entry point for all device-originated clinical observations.

### Responsibilities

- Accept observation payloads (blood pressure, heart rate, SpO2, temperature, glucose, weight).
- Validate observations against expected schemas and physiological plausibility ranges.
- Deduplicate based on device ID + timestamp + observation type (idempotent processing).
- Persist validated observations.
- Publish events for downstream consumption (Care-Gap Engine, Reporting Service).
- Route invalid or unparseable messages to a dead-letter queue for investigation.

### Owned Data

| Entity | Storage | Description |
|--------|---------|-------------|
| Observation | Azure SQL | Observation ID, caseId, type, value, unit, device reference, recorded timestamp, ingested timestamp |
| ObservationDedup | Azure SQL | Deduplication keys with TTL for idempotency window |

### Public APIs

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/observations` | Submit one or more observation readings |
| GET | `/observations?caseId={caseId}&type={type}&from={from}&to={to}` | Query observations by case, type, and time range |
| GET | `/observations/{observationId}` | Retrieve a single observation |

### Events Published

| Event | Trigger | Key Payload Fields |
|-------|---------|-------------------|
| `ObservationReceived` | Valid observation persisted | caseId, observationId, type, value, unit, recordedAt |

### Events Consumed

None. This service is a pure ingress point. It does not react to domain events.

### Dependencies

| Dependency | Type | Purpose |
|------------|------|---------|
| Azure SQL | Infrastructure | Observation storage and dedup tracking |
| Azure Service Bus | Infrastructure | Event publishing, dead-letter queue |

### Security Considerations

- In production, device data ingestion would use device-scoped credentials or a shared access signature. In this portfolio project, ingestion is simulated and authenticated via the standard Entra ID flow.
- Observation data is clinical in nature. Access is restricted to care team members assigned to the relevant case.
- Input validation is strict: reject payloads with missing fields, out-of-range values, or malformed timestamps. Never persist unvalidated data.

### Scaling Considerations

- Observation ingestion is the highest-throughput write path in the system. Design for horizontal scaling.
- Partition writes by caseId to avoid hot partitions in the database.
- Batch inserts where possible to reduce database round trips.
- HPA trigger: message backlog depth and CPU.

### Operational Concerns

- Dead-letter queue for invalid messages. Alert when dead-letter depth exceeds threshold.
- Deduplication window is configurable (default: 5 minutes). Tunable per observation type if needed.
- Metrics: ingestion rate, validation failure rate, dedup hit rate, end-to-end latency (device timestamp to persisted).

### Failure Modes

| Failure | Impact | Mitigation |
|---------|--------|------------|
| Azure SQL unavailable | Observations cannot be persisted | Retry with backoff; observations are buffered in Service Bus subscription |
| Malformed observation payload | Individual reading rejected | Dead-letter with diagnostic metadata; does not affect valid messages in the same batch |
| Dedup store inconsistency | Possible duplicate observation | Duplicates are low-harm (downstream consumers should also be idempotent); periodic reconciliation job |

### Why It Exists as a Separate Service

Observation ingestion has fundamentally different traffic patterns (high throughput, bursty, write-heavy) compared to case or care plan management (lower throughput, read-heavy). Isolating it lets the team scale the ingestion pipeline independently, apply backpressure without affecting the rest of the platform, and evolve validation rules without redeploying domain services.

---

## 5. Care-Gap Engine

### Purpose

Stateless rules engine that evaluates clinical and operational rules against incoming data to detect care gaps and raise alerts. This is the analytical brain of the platform -- it turns raw signals into actionable notifications.

### Responsibilities

- Consume `ObservationReceived`, `MilestoneOverdue`, and scheduled timer triggers.
- Evaluate configurable rules per care pathway (e.g., "BP systolic > 180 for cardiac pathway -> raise critical alert").
- Publish `AlertRaised` events when rules fire.
- Support rule versioning so that changes are auditable and rollback-safe.
- Remain stateless in processing -- all state comes from the event payload and rule configuration.

### Owned Data

| Entity | Storage | Description |
|--------|---------|-------------|
| RuleDefinition | Azure SQL | Rule ID, pathway, trigger type, condition expression, severity, active flag, version |
| RuleEvaluationLog | Azure SQL | Evaluation ID, rule ID, caseId, input snapshot, result, evaluated at |

This service does not own patient or case data. It references `caseId` and observation values from event payloads.

### Public APIs

This service has no UI-facing API. Rule management is performed through an administrative interface (or directly via configuration).

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/admin/rules` | List rule definitions (admin only) |
| POST | `/admin/rules` | Create or update a rule (admin only) |
| GET | `/admin/rules/{ruleId}/evaluation-log` | Query evaluation history (admin only) |

### Events Published

| Event | Trigger | Key Payload Fields |
|-------|---------|-------------------|
| `AlertRaised` | Rule condition evaluates to true | caseId, ruleId, severity (info/warning/critical), triggerType, triggerPayloadSnapshot, message |

### Events Consumed

| Event | Source | Action |
|-------|--------|--------|
| `ObservationReceived` | Observation Ingestion Service | Evaluate observation-triggered rules |
| `MilestoneOverdue` | Care Plan Service | Evaluate milestone-triggered rules |
| Timer trigger | Internal scheduler | Evaluate time-based rules (e.g., "no observation in 48 hours") |

### Dependencies

| Dependency | Type | Purpose |
|------------|------|---------|
| Azure SQL | Infrastructure | Rule configuration and evaluation logs |
| Azure Service Bus | Infrastructure | Event consumption and publishing |

### Security Considerations

- Rule management is restricted to administrative roles. Misconfigured rules can cause alert storms or suppress genuine care gaps.
- Evaluation logs contain observation values and rule conditions. Access is restricted to clinical and administrative roles.

### Scaling Considerations

- Processing is stateless and CPU-bound (rule evaluation). Scales horizontally by adding consumers to the Service Bus subscription.
- Competing consumers pattern: multiple replicas each process a partition of incoming events.
- Avoid thundering herd on timer triggers: stagger scheduled evaluations or use leader election.
- HPA trigger: Service Bus message backlog and CPU.

### Operational Concerns

- Alert suppression / cooldown: after raising an alert for a given case + rule combination, suppress duplicate alerts for a configurable window (e.g., 1 hour) to prevent alert fatigue.
- Rule changes take effect on the next evaluation cycle. No in-flight evaluation is affected.
- Metrics: rules evaluated per second, alerts raised per hour, rule evaluation latency, suppression rate.

### Failure Modes

| Failure | Impact | Mitigation |
|---------|--------|------------|
| Rule configuration corrupted | Rules evaluate incorrectly or not at all | Rule versioning with rollback; canary deployment of rule changes |
| Service Bus subscription backlog | Delayed alert detection | Autoscale consumers; alert on backlog depth |
| False positive storm | Alert fatigue, wasted clinician attention | Cooldown windows; rule severity tuning; dashboards showing alert rate trends |

### Why It Exists as a Separate Service

Rules evaluation is a cross-cutting concern that spans observations, milestones, and time-based triggers. Embedding it in the Observation Service would couple clinical rules to the ingestion pipeline. Embedding it in the Care Plan Service would ignore observation-based rules. A dedicated engine allows rules to be authored, versioned, and deployed independently. It also isolates a CPU-intensive evaluation loop from the I/O-heavy ingestion and CRUD services.

---

## 6. Task Service

### Purpose

Manages operational tasks that represent concrete work items for care team members. Tasks are the primary mechanism by which alerts and care gaps translate into human action.

### Responsibilities

- Create tasks from `AlertRaised` events or through manual creation by care coordinators.
- Manage task lifecycle: `open` -> `in-progress` -> `completed` | `canceled`.
- Support assignment to specific users, priority levels, due dates, and free-text comments.
- Expose task queries by case, assignee, status, and priority.
- Publish events on task creation and completion for downstream consumption.

### Owned Data

| Entity | Storage | Description |
|--------|---------|-------------|
| Task | Azure SQL | Task ID, caseId, title, description, status, priority, assignee, due date, created at, completed at |
| TaskComment | Azure SQL | Comment ID, task ID, author, body, created at |

### Public APIs

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/tasks` | Create a task manually |
| GET | `/tasks?caseId={caseId}&assignee={userId}&status={status}` | Query tasks with filters |
| GET | `/tasks/{taskId}` | Retrieve task details including comments |
| PATCH | `/tasks/{taskId}` | Update task (status, assignment, priority) |
| POST | `/tasks/{taskId}/comments` | Add a comment to a task |

### Events Published

| Event | Trigger | Key Payload Fields |
|-------|---------|-------------------|
| `TaskCreated` | Task persisted (auto or manual) | caseId, taskId, title, priority, assignee, sourceAlertId (nullable) |
| `TaskCompleted` | Task status set to completed | caseId, taskId, completedBy, completedAt |

### Events Consumed

| Event | Source | Action |
|-------|--------|--------|
| `AlertRaised` | Care-Gap Engine | Auto-create a task with alert details as description |

### Dependencies

| Dependency | Type | Purpose |
|------------|------|---------|
| Azure SQL | Infrastructure | Task and comment storage |
| Azure Service Bus | Infrastructure | Event publishing and consumption |

### Security Considerations

- Task visibility is scoped to the care team assigned to the case. A user cannot see tasks for cases they are not assigned to.
- Task comments may contain clinical notes. Same access restrictions apply.

### Scaling Considerations

- Moderate write volume: task creation spikes correlate with alert volume.
- Read-heavy for the task list views. Indexed queries on (caseId, status) and (assignee, status).
- HPA trigger: CPU and request rate.

### Operational Concerns

- Auto-created tasks from alerts include the `sourceAlertId` for traceability back to the originating rule evaluation.
- Overdue task detection: a background job flags tasks past their due date. This emits metrics but does not publish events (to avoid circular alert-task loops).

### Failure Modes

| Failure | Impact | Mitigation |
|---------|--------|------------|
| AlertRaised event missed | Alert does not produce a task | Dead-letter monitoring; manual task creation as fallback |
| Azure SQL unavailable | Cannot create or query tasks | Retry with backoff; gateway circuit breaker |
| Duplicate AlertRaised | Duplicate tasks | Idempotency key on alertId prevents duplicate auto-created tasks |

### Why It Exists as a Separate Service

Task management is an operational concern, not a clinical one. Tasks have their own lifecycle, assignment logic, and query patterns that are distinct from cases, care plans, or observations. A care coordinator's task inbox is a fundamentally different view from a clinician's care plan dashboard. Separating tasks also means the alert-to-task pipeline can evolve (e.g., adding SLA tracking, escalation rules) without touching clinical services.

---

## 7. Appointment Service

### Purpose

Manages follow-up appointment lifecycle for post-discharge patients. Appointments are a key care plan milestone -- a missed follow-up appointment is one of the strongest predictors of readmission.

### Responsibilities

- Create and manage appointments through their state machine: `proposed` -> `booked` -> `completed` | `canceled` | `no-show`.
- Track appointment type (PCP follow-up, specialist visit, lab work, etc.).
- Satisfy care plan milestones when appointments are completed.
- Detect missed appointments based on scheduled time and lack of completion.
- Publish events for downstream alerting and reporting.

### Owned Data

| Entity | Storage | Description |
|--------|---------|-------------|
| Appointment | Azure SQL | Appointment ID, caseId, type, provider, scheduled date/time, status, milestoneId (nullable) |
| AppointmentStatusHistory | Azure SQL | Append-only log of status transitions |

### Public APIs

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/appointments` | Create a proposed appointment |
| GET | `/appointments?caseId={caseId}&status={status}` | Query appointments with filters |
| GET | `/appointments/{appointmentId}` | Retrieve appointment details |
| PATCH | `/appointments/{appointmentId}/status` | Transition appointment status |

### Events Published

| Event | Trigger | Key Payload Fields |
|-------|---------|-------------------|
| `AppointmentBooked` | Status transitions to booked | caseId, appointmentId, type, scheduledAt, milestoneId |
| `AppointmentMissed` | Scheduled time passed without completion or explicit no-show recorded | caseId, appointmentId, type, scheduledAt, milestoneId |

### Events Consumed

| Event | Source | Action |
|-------|--------|--------|
| `CaseCreated` | Case Service | Optionally pre-create proposed appointments from discharge bundle data |
| `MilestoneCompleted` | Care Plan Service | Link milestone satisfaction (informational) |

### Dependencies

| Dependency | Type | Purpose |
|------------|------|---------|
| Azure SQL | Infrastructure | Appointment storage |
| Azure Service Bus | Infrastructure | Event publishing and consumption |

### Security Considerations

- Appointment data includes provider and scheduling information. Access is scoped to care team assignment.
- Appointment status changes (especially no-show and canceled) are recorded with the acting user for auditability.

### Scaling Considerations

- Lower write volume than observations but with time-sensitive missed-appointment detection.
- The missed-appointment detector runs on a schedule (e.g., every 10 minutes), checking for appointments past their scheduled time that remain in `booked` status.
- HPA trigger: CPU and request rate.

### Operational Concerns

- The missed-appointment detector uses a distributed lock (Azure Blob lease) to prevent duplicate `AppointmentMissed` events across replicas.
- Integration point: in a production system, this service would integrate with scheduling systems (e.g., Epic, Cerner). In this portfolio project, appointments are managed entirely within CareBridge.

### Failure Modes

| Failure | Impact | Mitigation |
|---------|--------|------------|
| Missed-appointment detector delayed | Late detection of no-shows | Alert on detector execution gap; reduce check interval for critical pathways |
| Azure SQL unavailable | Cannot create or query appointments | Retry with backoff; gateway circuit breaker |
| Duplicate CaseCreated processing | Duplicate proposed appointments | Idempotency key on caseId + appointment type |

### Why It Exists as a Separate Service

Appointment scheduling has a distinct state machine, its own time-based detection logic (missed appointments), and in production would integrate with external scheduling systems. Keeping it separate from the Care Plan Service avoids coupling milestone tracking with appointment state management. The Appointment Service satisfies milestones through events, not direct database writes.

---

## 8. Notification Service

### Purpose

Delivers template-based notifications across multiple channels. This is a pure infrastructure service -- it does not make clinical decisions. It takes notification requests from domain events and ensures delivery with retry and logging.

### Responsibilities

- Consume domain events that require user notification (`AlertRaised`, `TaskCreated`, `AppointmentBooked`, `AppointmentMissed`, `MilestoneOverdue`, etc.).
- Resolve notification templates based on event type and recipient preferences.
- Deliver through configured channels: email (Azure Communication Services), in-app (push to frontend via SignalR), simulated SMS.
- Retry failed deliveries with exponential backoff.
- Route permanently failed messages to a dead-letter queue.
- Log all delivery attempts (success, failure, retry) for compliance and debugging.

### Owned Data

| Entity | Storage | Description |
|--------|---------|-------------|
| NotificationTemplate | Azure SQL | Template ID, event type, channel, subject template, body template |
| NotificationLog | Azure SQL | Log ID, event type, recipient, channel, status (sent/failed/retrying), attempts, last attempt at, error detail |

This service does not own clinical or case data. It references `caseId` and user identifiers from event payloads.

### Public APIs

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/notifications/history?userId={userId}&from={from}&to={to}` | Query notification delivery history |
| GET | `/notifications/in-app?userId={userId}&read={false}` | Retrieve unread in-app notifications |
| PATCH | `/notifications/in-app/{notificationId}/read` | Mark an in-app notification as read |

### Events Published

None. This is a terminal consumer.

### Events Consumed

| Event | Source | Action |
|-------|--------|--------|
| `AlertRaised` | Care-Gap Engine | Notify assigned care team members |
| `TaskCreated` | Task Service | Notify task assignee |
| `TaskCompleted` | Task Service | Notify task creator (if different from completer) |
| `AppointmentBooked` | Appointment Service | Notify patient (simulated) and care coordinator |
| `AppointmentMissed` | Appointment Service | Notify care coordinator with urgency |
| `MilestoneOverdue` | Care Plan Service | Notify assigned care team members |

### Dependencies

| Dependency | Type | Purpose |
|------------|------|---------|
| Azure SQL | Infrastructure | Template and log storage |
| Azure Service Bus | Infrastructure | Event consumption, dead-letter queue |
| Azure Communication Services | External | Email delivery |
| SignalR (Azure SignalR Service) | External | Real-time in-app notification push |

### Security Considerations

- Notification content may contain patient-identifying information (names, appointment times). Delivery logs are subject to the same access restrictions as clinical data.
- Simulated SMS: no real phone numbers are used. The SMS channel writes to the notification log as if delivery occurred.
- Email templates must not include sensitive clinical data in subject lines (visible in previews).

### Scaling Considerations

- Notification volume is downstream of alert and event volume. Scales with the number of events that trigger notifications.
- Retry storms can amplify load. Exponential backoff with jitter prevents thundering herd on channel recovery.
- HPA trigger: Service Bus message backlog.

### Operational Concerns

- Dead-letter monitoring: alert when failed delivery count exceeds threshold within a time window.
- Channel health dashboard: delivery success rate per channel, average delivery latency, retry rate.
- Template changes are hot-reloaded from the database. No redeployment required.

### Failure Modes

| Failure | Impact | Mitigation |
|---------|--------|------------|
| Email provider unavailable | Email notifications delayed | Retry with exponential backoff; dead-letter after max attempts; in-app channel as fallback |
| SignalR hub unavailable | In-app notifications not pushed | Notifications persist in database; frontend polls on reconnect |
| Template missing for event type | Notification not sent | Log error and dead-letter the event; alert operations team |

### Why It Exists as a Separate Service

Notification delivery is a cross-cutting infrastructure concern that spans every domain service. Embedding it in each service would duplicate retry logic, template management, and channel integration. A dedicated service provides a single place to manage delivery reliability, add new channels, and enforce notification policies (e.g., quiet hours, consolidation).

---

## 9. Audit Service

### Purpose

Maintains an append-only, immutable record of all significant events across the platform. This is the system of record for "what happened, when, and by whom" -- essential for compliance, debugging, and incident investigation.

### Responsibilities

- Consume events from all services across the platform.
- Persist each event as an immutable audit record in Cosmos DB.
- Support queries by case, user, event type, and time range.
- Never modify or delete records. Append-only by design.
- No business logic. This service records; it does not act.

### Owned Data

| Entity | Storage | Description |
|--------|---------|-------------|
| AuditEvent | Cosmos DB | Event ID, event type, caseId, userId, timestamp, source service, payload snapshot |

Partition key: `caseId` (enables efficient per-case timeline queries).
TTL: none (events are retained indefinitely within this portfolio project; production would define a retention policy).

### Public APIs

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/audit?caseId={caseId}&from={from}&to={to}` | Query audit trail for a case |
| GET | `/audit?userId={userId}&from={from}&to={to}` | Query audit trail for a user's actions |
| GET | `/audit?eventType={eventType}&from={from}&to={to}` | Query by event type |

All query endpoints support pagination via continuation tokens.

### Events Published

None.

### Events Consumed

| Event | Source | Action |
|-------|--------|--------|
| `CaseCreated` | Case Service | Record |
| `CaseStatusChanged` | Case Service | Record |
| `MilestoneCompleted` | Care Plan Service | Record |
| `MilestoneOverdue` | Care Plan Service | Record |
| `ObservationReceived` | Observation Ingestion Service | Record |
| `AlertRaised` | Care-Gap Engine | Record |
| `TaskCreated` | Task Service | Record |
| `TaskCompleted` | Task Service | Record |
| `AppointmentBooked` | Appointment Service | Record |
| `AppointmentMissed` | Appointment Service | Record |
| All future event types | Any service | Record (forward-compatible consumer) |

The Audit Service subscribes to all topics/events. It is a forward-compatible consumer: new event types are stored without code changes as long as they conform to the base event envelope schema.

### Dependencies

| Dependency | Type | Purpose |
|------------|------|---------|
| Cosmos DB | Infrastructure | Immutable event storage |
| Azure Service Bus | Infrastructure | Event consumption (subscribes to all topics) |

### Security Considerations

- Audit records contain event payloads that may include patient references. Access is restricted to clinical staff (scoped to their cases) and administrative/compliance roles (broader access).
- The Cosmos DB account uses RBAC with a dedicated service principal. No shared keys.
- Immutability is enforced at the application level (no update/delete endpoints) and can be reinforced with Cosmos DB's immutable backup policies.

### Scaling Considerations

- Write-heavy consumer: every event in the system produces an audit record. Cosmos DB autoscale RU/s handles throughput spikes.
- Query volume is low (audit queries are infrequent compared to operational reads). Partition key on `caseId` optimizes the most common query pattern.
- Cosmos DB's global distribution is not needed for this portfolio project but would be relevant in production.

### Operational Concerns

- Monitor Cosmos DB consumed RU/s vs provisioned. Alert on throttling (429 responses).
- Event processing lag: track the delta between event timestamp and audit record creation. Alert if lag exceeds threshold.
- No data migration concerns -- schema is a flexible document. New event types add new documents, not new columns.

### Failure Modes

| Failure | Impact | Mitigation |
|---------|--------|------------|
| Cosmos DB unavailable | Audit records not written | Events remain in Service Bus subscription; consumed on recovery. No data loss. |
| Service Bus subscription backlog | Delayed audit records | Acceptable for audit (not real-time); alert on extended delay |
| Malformed event payload | Individual audit record missing | Dead-letter the event; log error. Does not affect other events. |

### Why It Exists as a Separate Service

Audit logging is a non-negotiable cross-cutting concern in healthcare-adjacent systems. Embedding audit writes in each service would couple every service to Cosmos DB, duplicate serialization logic, and risk audit gaps when new services are added. A dedicated consumer with a "record everything" mandate ensures completeness. Its storage technology (Cosmos DB, optimized for append-heavy document writes) differs from the relational stores used by domain services.

---

## 10. Reporting / Read Model Service

### Purpose

Builds and serves denormalized read models that power the CareBridge dashboard, case timelines, and operational metrics. This is the CQRS read side -- it consumes domain events to maintain materialized views optimized for query patterns the UI needs.

### Responsibilities

- Consume domain events to build and update materialized views (in-memory for local dev, Cosmos DB in cloud).
- Serve dashboard queries: active case count, open/acknowledged alert counts by severity, overdue tasks, pending appointments, recent cases, top alerts.
- Serve case timeline: a chronological view of all events for a single case with category filtering and pagination.
- Accept that data is eventually consistent with the write side (seconds, not minutes).
- EventId-based deduplication prevents double-counting from at-least-once delivery.

### Owned Data

| View | Storage | Description |
|------|---------|-------------|
| DashboardSummary | In-memory (Cosmos DB in cloud) | Aggregated counts: active cases, alerts by status/severity, open/overdue tasks, pending appointments, recent cases, top alerts |
| CaseTimeline | In-memory (Cosmos DB in cloud) | Per-case chronological event stream for the detail view |

**MVP note:** ObservationTrend and OperationalMetrics views are deferred to post-MVP. The in-memory store uses `IReadModelStore` interface with `InMemoryReadModelStore` implementation, swappable via DI for a future `CosmosDbReadModelStore`.

Partition key strategy (cloud): `caseId` for case-scoped views; singleton key for dashboard summary.

### Public APIs

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/reports/dashboard` | Aggregated dashboard summary (active cases, alerts, tasks, appointments, recent cases, top alerts) |
| GET | `/api/v1/reports/timeline/{caseId}?limit=&cursor=&category=` | Chronological event timeline for a case with pagination and optional category filtering |

**Port:** 5090

**BFF routes:**
| BFF Endpoint | Proxies To |
|---|---|
| `GET /api/dashboard/summary` | `GET /api/v1/reports/dashboard` |
| `GET /api/cases/{caseId}/timeline` | `GET /api/v1/reports/timeline/{caseId}` |

### Events Published

None. This is a terminal consumer.

### Events Consumed

| Event | Source | Action |
|-------|--------|--------|
| `CaseCreated` | Case Service | ActiveCaseCount++, add to RecentCases; initialize case timeline |
| `CaseUpdated` | Case Service | ActiveCaseCount-- on Closed; update RecentCases status; append to timeline |
| `CarePlanActivated` | Care Plan Service | Append to case timeline |
| `MilestoneCompleted` | Care Plan Service | Append to case timeline |
| `ObservationReceived` | Observation Service | Append to case timeline |
| `AlertRaised` | Care-Gap Engine | AlertsByStatus.Open++, AlertsBySeverity[sev]++, add to TopAlerts; append to timeline |
| `AlertAcknowledged` | Care-Gap Engine | AlertsByStatus shift Open→Acknowledged; append to timeline |
| `AlertResolved` | Care-Gap Engine | AlertsByStatus.Acknowledged--, remove from TopAlerts; append to timeline |
| `TaskCreated` | Task Service | OpenTaskCount++; append to timeline |
| `TaskCompleted` | Task Service | OpenTaskCount--; append to timeline |
| `AppointmentBooked` | Appointment Service | PendingAppointmentCount++; append to timeline |
| `AppointmentCompleted` | Appointment Service | PendingAppointmentCount--; append to timeline |
| `AppointmentMissed` | Appointment Service | PendingAppointmentCount--; append to timeline |
| `NotificationSent` | Notification Service | Append to timeline |

### Dependencies

| Dependency | Type | Purpose |
|------------|------|---------|
| In-memory store (Cosmos DB in cloud) | Infrastructure | Materialized view storage |
| RabbitMQ (Service Bus in cloud) | Infrastructure | Event consumption |

### Security Considerations

- Read models contain aggregated and per-case data. The same care-team-scoped access rules apply: a user sees dashboard data filtered to their assigned cases.
- Aggregated operational metrics (across all cases) are restricted to supervisor and administrative roles.

### Scaling Considerations

- Read-heavy service. Cosmos DB point reads (by partition key) are sub-millisecond and scale linearly with provisioned throughput.
- Event consumption is the write path. Volume equals the sum of all domain events across the platform.
- Dashboard summary view is a single document updated on every event. Consider batching updates or using a short write-behind buffer to avoid excessive RU consumption from high-frequency observation events.
- HPA trigger: request rate (read side) and Service Bus backlog (write side).

### Operational Concerns

- Rebuild capability: if a materialized view becomes corrupted or a new view is added, the service must be able to replay events from the Audit Service or Service Bus archive to reconstruct state. Design event handlers to be idempotent.
- Consistency lag monitoring: track the delta between the latest event timestamp and the latest materialized view update. Alert on growing lag.
- View versioning: when the dashboard evolves, new view shapes may be needed. Old views are deprecated gracefully, not deleted.

### Failure Modes

| Failure | Impact | Mitigation |
|---------|--------|------------|
| Cosmos DB unavailable | Dashboard returns stale data or errors | Cache last-known-good response at the gateway with stale-while-revalidate semantics |
| Event processing lag | Dashboard shows stale counts | Acceptable within SLA (seconds); UI displays "last updated" timestamp; alert on extended lag |
| Corrupted materialized view | Incorrect dashboard data | Replay events to rebuild view; idempotent handlers ensure correctness |

### Why It Exists as a Separate Service

The CQRS pattern separates read and write models for a reason. Dashboard queries span data from every domain service -- cases, care plans, observations, tasks, appointments, and alerts. Without a dedicated read model, the API Gateway would need to query every service and aggregate in real time, which is slow, fragile, and creates coupling. A dedicated service also allows the team to optimize storage (Cosmos DB document shapes tuned for specific query patterns) independently of the write-side relational schemas.

---

## Cross-Service Communication Patterns

### Event Bus Topology

All asynchronous communication flows through Azure Service Bus using a topic-per-event-type model:

```
Service Bus Topics:
├── discharge-received       -> Case Service (subscription)
├── case-created             -> Care Plan Service, Appointment Service, Audit, Reporting
├── case-status-changed      -> Audit, Reporting
├── milestone-completed      -> Appointment Service, Audit, Reporting
├── milestone-overdue        -> Care-Gap Engine, Notification Service, Audit, Reporting
├── observation-received     -> Care-Gap Engine, Audit, Reporting
├── alert-raised             -> Task Service, Notification Service, Audit, Reporting
├── task-created             -> Notification Service, Audit, Reporting
├── task-completed           -> Notification Service, Audit, Reporting
├── appointment-booked       -> Notification Service, Audit, Reporting
└── appointment-missed       -> Notification Service, Audit, Reporting
```

Each subscribing service has its own subscription on the relevant topic, ensuring independent consumption and backpressure.

### Synchronous vs Asynchronous

| Pattern | Used For | Example |
|---------|----------|---------|
| Synchronous HTTP (via Gateway) | User-initiated reads and writes | GET /cases/{id}, POST /tasks |
| Asynchronous events (Service Bus) | Inter-service coordination | CaseCreated triggers care plan activation |

No service calls another service directly via HTTP. All inter-service data flow is event-driven. The only synchronous HTTP calls are from the API Gateway to individual services to serve user requests.

---

## Event Envelope Standard

All events conform to a common envelope schema, ensuring the Audit Service and Reporting Service can process any event type:

```json
{
  "eventId": "uuid",
  "eventType": "CaseCreated",
  "source": "case-service",
  "timestamp": "2026-03-27T14:30:00Z",
  "correlationId": "uuid",
  "caseId": "uuid",
  "userId": "uuid",
  "payload": {
    // Event-type-specific fields
  }
}
```

See [Data Architecture](./data-architecture.md) for the full event schema specification and versioning strategy.

---

## Service Dependency Matrix

This matrix shows runtime dependencies between services. An "E" indicates event-based (asynchronous) dependency. An "H" indicates HTTP (synchronous, via gateway only).

| Consumer \ Producer | Case | Care Plan | Observation | Care-Gap | Task | Appointment | Notification | Audit | Reporting |
|---------------------|------|-----------|-------------|----------|------|-------------|--------------|-------|-----------|
| **API Gateway** | H | H | H | -- | H | H | H | H | H |
| **Case Service** | -- | -- | -- | -- | -- | -- | -- | -- | -- |
| **Care Plan Service** | E | -- | -- | -- | -- | -- | -- | -- | -- |
| **Observation Ingestion** | -- | -- | -- | -- | -- | -- | -- | -- | -- |
| **Care-Gap Engine** | -- | E | E | -- | -- | -- | -- | -- | -- |
| **Task Service** | -- | -- | -- | E | -- | -- | -- | -- | -- |
| **Appointment Service** | E | E | -- | -- | -- | -- | -- | -- | -- |
| **Notification Service** | -- | E | -- | E | E | E | -- | -- | -- |
| **Audit Service** | E | E | E | E | E | E | -- | -- | -- |
| **Reporting Service** | E | E | E | E | E | E | -- | -- | -- |

---

## Deployment and Lifecycle

All services are deployed as containers on AKS. Each service has:

- Its own Helm chart with configurable replicas, resource requests/limits, and HPA rules.
- Independent CI/CD pipeline. A change to one service does not require redeployment of others.
- Health check endpoints (`/healthz`, `/readyz`) consumed by Kubernetes probes.
- Structured JSON logging shipped to Azure Monitor / Log Analytics.
- Distributed tracing via OpenTelemetry, correlated by `X-Correlation-Id`.

Service-to-service network traffic is restricted by Kubernetes NetworkPolicy. Only the API Gateway accepts ingress from outside the cluster.

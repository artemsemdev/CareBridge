# Delivery Plan

**Document type:** Delivery execution plan
**Status:** Draft
**Created:** 2026-03-28
**Scope:** Solo-developer iterative delivery of CareBridge MVP

---

## 1. Project Summary

**CareBridge** is a cloud-native post-discharge care coordination platform built as a portfolio-grade reference implementation on Azure and Kubernetes. It manages the 30-day window after hospital discharge: intake, care plan activation, remote monitoring, alerting, task management, scheduling, and audit trailing.

**Smallest Credible MVP:** A working end-to-end flow where a synthetic discharge creates a case, activates a care plan, accepts vital sign observations, detects abnormal readings, generates alerts, creates coordinator tasks, and displays everything in a React dashboard with a unified case timeline. This proves the core architecture (microservices, event-driven, CQRS) and the core clinical workflow in a single demonstrable system.

**What the MVP is NOT:**
- Not a production-certified healthcare system
- Not a multi-tenant SaaS platform
- Not a FHIR-first system (FHIR is a secondary projection, deferred from MVP)
- Not a fully scaled cloud deployment (local-first, cloud deployment is a later stage)

---

## 2. Delivery Stages

### Stage 0: Foundation (estimated: ~1 week) — COMPLETED 2026-03-28
Set up the solution structure, shared libraries, local development environment, and the first service skeleton. Nothing is shippable yet, but everything after this goes faster.

**Exit criteria:** `docker-compose up` starts local infrastructure. A skeleton .NET service builds, runs, and responds to a health check. Shared contracts compile. Database migration tooling works.

**Status:** All 6 issues (F1-01 through F1-06) implemented. Solution builds with 0 warnings/errors. 19 unit tests pass (16 contract serialization + 3 case service). Case Service skeleton wired with EF Core, RabbitMQ event publishing, health checks, correlation ID middleware, and structured logging.

### Stage 1: Case Intake Pipeline (estimated: ~1.5 weeks) — COMPLETED 2026-03-29
Discharge intake creates a case. Care Plan Service consumes the event and activates a plan with milestones. The BFF exposes this to a minimal React page. This is the first vertical slice: data flows from API to database to event bus to a second service and back to the UI.

**Exit criteria:** POST a synthetic discharge bundle via the BFF. Case appears in the database. CaseCreated event fires. Care Plan Service receives it, instantiates a plan with milestones. React page lists cases and shows case detail with care plan.

**Status:** All 9 issues (C2-01 through C2-09) implemented. Case Service updated with string PatientId, cursor-based pagination, PATCH status endpoint, and best-effort CaseUpdated event publishing. Care Plan Service implemented with EF Core domain model, "General Post-Discharge" template (5 milestones), idempotent CaseCreated handler, and REST API. API Gateway implemented with named HttpClients, CorrelationIdForwardingHandler, mock auth middleware, and 502/504 error handling. db-migrator extended for carebridge-careplan-db. React frontend built with Tailwind CSS, React Router, React Query, application shell, case list page, and case detail page with care plan view. .NET solution builds clean, 19 unit tests pass, frontend TypeScript compiles.

### Stage 2: Monitoring and Alerting Loop (estimated: ~1.5 weeks)
Observations flow in and get evaluated. Abnormal readings and missed milestones generate alerts. This closes the core detection loop.

**Exit criteria:** POST an observation. It is validated, stored, and published. Care-Gap Engine evaluates it and generates an alert for abnormal values. Scheduled scan detects missed milestones. Alerts are visible in the UI.

### Stage 3: Coordinator Workflows (estimated: ~1.5 weeks)
Alerts automatically create tasks. Coordinators manage tasks and schedule appointments. Completing a task or appointment can satisfy a care plan milestone. Notifications are logged (no real delivery channels yet).

**Exit criteria:** Alert generates a task automatically. Task lifecycle works (open, in-progress, completed). Appointments can be created and tracked. Completing relevant work updates milestone status. Notifications appear in the log.

### Stage 4: Dashboard, Timeline, and Reporting (estimated: ~1.5 weeks)
Reporting Service consumes all domain events and builds denormalized read models. The operational dashboard and case timeline come to life. This is where the CQRS pattern pays off.

**Exit criteria:** Operational dashboard shows alert counts, overdue tasks, workload distribution. Case timeline shows a chronological view of all events for a patient. Dashboard queries hit Cosmos DB read models (or in-memory equivalent locally), not the transactional databases.

### Stage 5: Audit and Compliance (estimated: ~1 week)
Audit Service captures all domain events into an immutable append-only log. Audit trail is queryable from the UI.

**Exit criteria:** Every state change across all services produces an audit event. Audit log is immutable and queryable by case, actor, date range, and action type. Audit view works in the React UI.

### Stage 6: Hardening and Observability (estimated: ~1 week)
Add structured logging, correlation IDs, health endpoints, and basic OpenTelemetry integration across all services. Error handling and resilience patterns.

**Exit criteria:** All services have /startup, /ready, /healthz endpoints. Correlation IDs propagate through HTTP and events. Structured JSON logging is consistent. Basic OTEL traces are emitted. Circuit breakers protect critical paths.

### Stage 7: Cloud Deployment (estimated: ~2 weeks)
Terraform provisions Azure resources. Dockerfiles and Helm charts package the services. GitHub Actions builds and deploys to AKS.

**Exit criteria:** Terraform provisions AKS, Azure SQL, Cosmos DB, Service Bus, ACR. GitHub Actions builds, tests, and pushes images. Helm deploys all services to a dev AKS cluster. The application runs in Azure with real managed services.

### Stage 8: Demo-Ready Release (estimated: ~1 week)
Synthetic data generator produces a realistic dataset. A demo walkthrough script exists. README and documentation are updated to reflect the built system.

**Exit criteria:** `dotnet run --project tools/synthetic-data-generator` seeds a compelling dataset. A documented demo path walks through the complete workflow. README reflects actual running state, not aspirational state.

---

## 3. Epics

Each epic has a dedicated execution-ready file in [`docs/delivery/epics/`](epics/) with full issue details, dependency graphs, technical specifications, and acceptance criteria.

| # | Epic | Issues | Wave | Execution File |
|---|------|--------|------|----------------|
| 1 | Foundation and Developer Experience | 6 | 1 | [epic-1-foundation.md](epics/epic-1-foundation.md) |
| 2 | Case Intake Pipeline | 9 | 2 | [epic-2-case-intake-pipeline.md](epics/epic-2-case-intake-pipeline.md) |
| 3 | Remote Monitoring and Alerting | 7 | 3 | [epic-3-monitoring-alerting.md](epics/epic-3-monitoring-alerting.md) |
| 4 | Coordinator Workflows | 8 | 4 | [epic-4-coordinator-workflows.md](epics/epic-4-coordinator-workflows.md) |
| 5 | Dashboard, Timeline, and Reporting | 6 | 5 | [epic-5-dashboard-reporting.md](epics/epic-5-dashboard-reporting.md) |
| 6 | Audit and Compliance | 3 | 5 | [epic-6-audit.md](epics/epic-6-audit.md) |
| 7 | Infrastructure and Cloud Deployment | 13 | 6–8 | [epic-7-infrastructure.md](epics/epic-7-infrastructure.md) |

**Total: 52 issues across 7 epics.**

### Epic Summaries

**Epic 1: Foundation** — Solution structure, shared contracts, shared middleware, Docker Compose, database migration tool, reference service skeleton. No dependencies. Start here.

**Epic 2: Case Intake Pipeline** — Case Service, Care Plan Service, event-driven care plan activation, BFF proxy, React case list and case detail pages. First vertical slice from API to UI.

**Epic 3: Remote Monitoring and Alerting** — Observation Service, Care-Gap Engine with threshold rules and milestone scanning, alert management API, observation and alert UI. Closes the clinical detection loop.

**Epic 4: Coordinator Workflows** — Task Service with auto-creation from alerts, task-to-milestone linking, Appointment Service, Notification Service (log-based), task and appointment UI. Closes the operational response loop.

**Epic 5: Dashboard, Timeline, and Reporting** — Reporting Service event consumers, dashboard summary read model, case timeline read model, React operational dashboard and timeline component. Proves the CQRS architecture.

**Epic 6: Audit and Compliance** — Audit Service event consumer, immutable append-only storage, multi-dimensional query API, audit trail UI. Healthcare compliance pattern.

**Epic 7: Infrastructure and Cloud Deployment** — Health endpoints, structured logging, OpenTelemetry, circuit breakers, Dockerfiles, Helm charts, Terraform (AKS, SQL, Cosmos DB, Service Bus), GitHub Actions CI/CD, synthetic data generator, demo walkthrough.

---

## 4. Iterative Delivery Slices

### Epic 1: Foundation and Developer Experience

| Slice | What it delivers |
|-------|-----------------|
| **F1: Solution structure** | .NET solution, project scaffolding, folder layout matching documented repo structure |
| **F2: Shared contracts** | Event envelope, common domain types, shared DTOs |
| **F3: Shared middleware** | Correlation ID propagation, error handling middleware, health check base |
| **F4: Local infrastructure** | Docker Compose with SQL Server, RabbitMQ, Azurite |
| **F5: Database migration tooling** | EF Core migration runner as a standalone tool |
| **F6: Service template** | A reference service skeleton with API, EF Core context, event publishing, health checks |

### Epic 2: Case Intake Pipeline

| Slice | What it delivers |
|-------|-----------------|
| **C1: Case Service core** | Domain model, database, REST API for case CRUD |
| **C2: Event publishing** | Case Service publishes CaseCreated, CaseUpdated events to RabbitMQ |
| **C3: Care Plan Service core** | Domain model, database, template engine, milestone definitions |
| **C4: Care plan activation** | Care Plan Service consumes CaseCreated, instantiates plan with milestones |
| **C5: BFF case endpoints** | Gateway proxies case and care plan queries |
| **C6: React case list** | Minimal React app with case list page |
| **C7: React case detail** | Case detail page showing patient info + care plan + milestones |

### Epic 3: Remote Monitoring and Alerting

| Slice | What it delivers |
|-------|-----------------|
| **M1: Observation Service core** | Domain model, database, REST API for observation ingestion |
| **M2: Observation validation** | Input validation, deduplication via idempotency key |
| **M3: Observation events** | ObservationReceived event published after successful ingestion |
| **M4: Care-Gap Engine — observation rules** | Consumes ObservationReceived, evaluates against thresholds, publishes AlertRaised |
| **M5: Care-Gap Engine — milestone scan** | Scheduled scan for missed milestones, publishes AlertRaised |
| **M6: Alert storage and query** | Care-Gap Engine stores alerts, exposes query API |
| **M7: BFF + React observation view** | Observation list on case detail page |
| **M8: BFF + React alert view** | Alert list on case detail page + standalone alert queue page |

### Epic 4: Coordinator Workflows

| Slice | What it delivers |
|-------|-----------------|
| **W1: Task Service core** | Domain model, database, REST API for task CRUD |
| **W2: Automatic task creation** | Task Service consumes AlertRaised, creates assigned task |
| **W3: Task lifecycle** | State transitions: open → in-progress → completed/deferred |
| **W4: Task-milestone link** | Completing a task can mark a care plan milestone as done |
| **W5: Appointment Service core** | Domain model, database, REST API for appointment CRUD |
| **W6: Appointment lifecycle** | States: proposed → booked → completed/canceled/no-show |
| **W7: Appointment-milestone link** | Completing an appointment satisfies relevant milestones |
| **W8: Notification Service core** | Event consumer, template rendering, log-based delivery |
| **W9: BFF + React task management** | Task list with filters, task detail, status transitions |
| **W10: BFF + React appointment view** | Appointment list, create appointment, status updates |

### Epic 5: Dashboard, Timeline, and Reporting

| Slice | What it delivers |
|-------|-----------------|
| **D1: Reporting Service event consumers** | Consume all domain events and project into read models |
| **D2: Dashboard read model** | Denormalized summary: open alerts, overdue tasks, active cases, workload |
| **D3: Case timeline read model** | Chronological event list per case |
| **D4: BFF dashboard endpoint** | Aggregated dashboard summary API |
| **D5: React operational dashboard** | Dashboard page with summary cards, alert queue, task overview |
| **D6: React case timeline** | Timeline component on case detail page |

### Epic 6: Audit and Compliance

| Slice | What it delivers |
|-------|-----------------|
| **A1: Audit Service event consumer** | Consume all domain events, write immutable audit records |
| **A2: Audit storage** | Cosmos DB (or in-memory locally) append-only store |
| **A3: Audit query API** | Query by case, actor, action type, date range |
| **A4: BFF + React audit view** | Audit trail page with filters |

### Epic 7: Infrastructure and Cloud Deployment

| Slice | What it delivers |
|-------|-----------------|
| **I1: Dockerfiles** | Multi-stage Dockerfiles for all services + frontend |
| **I2: Helm charts** | Helm chart per service with values files |
| **I3: Terraform — core resources** | AKS, ACR, networking, resource groups |
| **I4: Terraform — data services** | Azure SQL elastic pool, Cosmos DB, Service Bus |
| **I5: GitHub Actions — CI** | Build, test, lint, scan on PR |
| **I6: GitHub Actions — CD** | Deploy to dev AKS on merge to develop |
| **I7: Synthetic data generator** | Tool that seeds realistic demo data |
| **I8: Demo walkthrough** | Documented script showing the end-to-end workflow |

---

## 5. GitHub Issues

All issues are defined in the individual epic files under [`docs/delivery/epics/`](epics/). Each issue includes:
- Title and description
- Why it matters
- Detailed scope
- Acceptance criteria (checkbox format)
- Dependencies
- Labels and suggested branch name
- Technical notes where needed

### Issue Index

**Epic 1: Foundation** ([full details](epics/epic-1-foundation.md))
| ID | Title |
|----|-------|
| F1-01 | Create .NET solution structure and project scaffolding |
| F1-02 | Create shared event contracts and domain types |
| F1-03 | Create shared infrastructure middleware |
| F1-04 | Set up Docker Compose for local development |
| F1-05 | Create database migration runner tool |
| F1-06 | Create reference service skeleton with end-to-end patterns |

**Epic 2: Case Intake Pipeline** ([full details](epics/epic-2-case-intake-pipeline.md))
| ID | Title |
|----|-------|
| C2-01 | Implement Case Service domain model and database |
| C2-02 | Implement Case Service REST API |
| C2-03 | Implement Case Service event publishing |
| C2-04 | Implement Care Plan Service domain model and database |
| C2-05 | Implement care plan activation on CaseCreated event |
| C2-06 | Implement Care Plan Service REST API |
| C2-07 | Implement BFF proxy endpoints for cases and care plans |
| C2-08 | Create React application shell and case list page |
| C2-09 | Create React case detail page with care plan view |

**Epic 3: Remote Monitoring and Alerting** ([full details](epics/epic-3-monitoring-alerting.md))
| ID | Title |
|----|-------|
| M3-01 | Implement Observation Service domain model and database |
| M3-02 | Implement Observation Service REST API with validation |
| M3-03 | Implement Care-Gap Engine — observation threshold rules |
| M3-04 | Implement Care-Gap Engine — scheduled milestone scan |
| M3-05 | Implement Care-Gap Engine alert query API |
| M3-06 | Add BFF and React observation display on case detail |
| M3-07 | Add BFF and React alert display |

**Epic 4: Coordinator Workflows** ([full details](epics/epic-4-coordinator-workflows.md))
| ID | Title |
|----|-------|
| W4-01 | Implement Task Service domain model and database |
| W4-02 | Implement Task Service REST API |
| W4-03 | Implement automatic task creation from alerts |
| W4-04 | Implement task completion updates care plan milestone |
| W4-05 | Implement Appointment Service domain model, database, and API |
| W4-06 | Implement Notification Service — event consumer with log-based delivery |
| W4-07 | Add BFF and React task management UI |
| W4-08 | Add BFF and React appointment management UI |

**Epic 5: Dashboard, Timeline, and Reporting** ([full details](epics/epic-5-dashboard-reporting.md))
| ID | Title |
|----|-------|
| D5-01 | Implement Reporting Service event consumers |
| D5-02 | Implement dashboard summary read model and API |
| D5-03 | Implement case timeline read model and API |
| D5-04 | Build BFF dashboard aggregation endpoint |
| D5-05 | Build React operational dashboard page |
| D5-06 | Build React case timeline component |

**Epic 6: Audit and Compliance** ([full details](epics/epic-6-audit.md))
| ID | Title |
|----|-------|
| A6-01 | Implement Audit Service event consumer and storage |
| A6-02 | Implement Audit Service query API |
| A6-03 | Add BFF and React audit trail view |

**Epic 7: Infrastructure and Cloud Deployment** ([full details](epics/epic-7-infrastructure.md))
| ID | Title |
|----|-------|
| H-01 | Add health endpoints to all services |
| H-02 | Standardize structured JSON logging across all services |
| H-03 | Integrate OpenTelemetry distributed tracing |
| H-04 | Add circuit breakers for inter-service HTTP calls |
| H-05 | Error handling review and edge case hardening |
| I7-01 | Create multi-stage Dockerfiles for all services |
| I7-02 | Create Helm charts for all services |
| I7-03 | Create Terraform configuration for core Azure resources |
| I7-04 | Create Terraform configuration for data and messaging services |
| I7-05 | Set up GitHub Actions CI pipeline |
| I7-06 | Set up GitHub Actions CD pipeline |
| I7-07 | Build synthetic data generator tool |
| I7-08 | Create demo walkthrough documentation |

---

## 6. Recommended Execution Order

The order below optimizes for three goals: unblocking dependent work early, achieving visible end-to-end progress fast, and maintaining a deployable system at each increment.

### Wave 1: Foundation (issues can be parallelized) — COMPLETED
1. ~~**F1-01** — Solution structure~~ DONE
2. ~~**F1-04** — Docker Compose~~ DONE
3. ~~**F1-02** — Shared event contracts~~ DONE
4. ~~**F1-03** — Shared middleware~~ DONE
5. ~~**F1-05** — Database migration tool~~ DONE
6. ~~**F1-06** — Reference service skeleton~~ DONE

### Wave 2: First Vertical Slice — Case to UI — COMPLETED 2026-03-29
7. ~~**C2-01** — Case Service domain + database~~ DONE
8. ~~**C2-02** — Case Service REST API~~ DONE
9. ~~**C2-03** — Case Service event publishing~~ DONE
10. ~~**C2-04** — Care Plan Service domain + database~~ DONE
11. ~~**C2-05** — Care plan activation (event-driven)~~ DONE
12. ~~**C2-06** — Care Plan Service REST API~~ DONE
13. ~~**C2-07** — BFF proxy endpoints~~ DONE
14. ~~**C2-08** — React app shell + case list~~ DONE
15. ~~**C2-09** — React case detail + care plan view~~ DONE

**Milestone: First demo — discharge creates case, care plan activates, visible in browser. ✓ ACHIEVED**

### Wave 3: Clinical Value Loop
16. **M3-01** — Observation Service domain + database
17. **M3-02** — Observation Service API + validation
18. **M3-03** — Care-Gap Engine — threshold rules
19. **M3-04** — Care-Gap Engine — milestone scan
20. **M3-05** — Alert query API
21. **M3-06** — React observation display
22. **M3-07** — React alert display

**Milestone: Full detection loop — observations in, alerts out, visible in UI.**

### Wave 4: Operational Workflows
23. **W4-01** — Task Service domain + database
24. **W4-02** — Task Service REST API
25. **W4-03** — Automatic task creation from alerts
26. **W4-04** — Task completion → milestone update
27. **W4-05** — Appointment Service (full)
28. **W4-06** — Notification Service (log-based)
29. **W4-07** — React task management UI
30. **W4-08** — React appointment UI

**Milestone: Full operational loop — alert → task → resolution → milestone completion.**

### Wave 5: Dashboard, Timeline, and Audit
31. **D5-01** — Reporting Service event consumers
32. **D5-02** — Dashboard read model + API
33. **D5-03** — Timeline read model + API
34. **D5-04** — BFF dashboard endpoint
35. **D5-05** — React operational dashboard
36. **D5-06** — React case timeline
37. **A6-01** — Audit Service event consumer + storage
38. **A6-02** — Audit query API
39. **A6-03** — React audit trail view

**Milestone: Full CQRS proven. Dashboard, timeline, and audit all working.**

### Wave 6: Hardening
40. Health endpoints across all services
41. Structured JSON logging standardization
42. OpenTelemetry trace integration
43. Circuit breaker for inter-service HTTP calls
44. Error handling review and edge case coverage

**Milestone: Observability and resilience patterns demonstrated.**

### Wave 7: Cloud Deployment
45. **I7-01** — Dockerfiles
46. **I7-02** — Helm charts
47. **I7-03** — Terraform core resources
48. **I7-04** — Terraform data + messaging
49. **I7-05** — GitHub Actions CI
50. **I7-06** — GitHub Actions CD

**Milestone: System running on Azure AKS with automated pipeline.**

### Wave 8: Demo-Ready
51. **I7-07** — Synthetic data generator
52. **I7-08** — Demo walkthrough
53. README and documentation update to reflect built state

**Milestone: Portfolio-ready. Demonstrable. Documented.**

---

## 7. GitHub Project Setup

### Labels

**Type labels:**
| Label | Color | Description |
|-------|-------|-------------|
| `type:feature` | `#1D76DB` | New functionality |
| `type:infrastructure` | `#D4C5F9` | Infra, CI/CD, tooling |
| `type:bug` | `#D73A4A` | Something broken |
| `type:hardening` | `#FBCA04` | Resilience, observability, error handling |
| `type:documentation` | `#0075CA` | Documentation updates |

**Epic labels:**
| Label | Color |
|-------|-------|
| `epic:foundation` | `#C2E0C6` |
| `epic:case-pipeline` | `#BFD4F2` |
| `epic:monitoring-alerting` | `#F9D0C4` |
| `epic:coordinator-workflows` | `#FEF2C0` |
| `epic:dashboard-reporting` | `#E6CCB2` |
| `epic:audit` | `#D4C5F9` |
| `epic:cloud-deployment` | `#C5DEF5` |

**Priority labels:**
| Label | Color | Description |
|-------|-------|-------------|
| `priority:critical` | `#B60205` | Blocks everything else |
| `priority:high` | `#D93F0B` | Needed for current wave |
| `priority:medium` | `#FBCA04` | Planned for next wave |
| `priority:low` | `#0E8A16` | Backlog / nice-to-have |

**Status labels (optional, if not using project board):**
| Label |
|-------|
| `status:blocked` |
| `status:needs-design` |

### Milestones

| Milestone | Target | Description |
|-----------|--------|-------------|
| **v0.1 — Foundation** | Wave 1 complete ✓ | Solution structure, local dev, shared libs |
| **v0.2 — Case Pipeline** | Wave 2 complete ✓ | First vertical slice: discharge → case → care plan → UI |
| **v0.3 — Monitoring Loop** | Wave 3 complete | Observations → alerts → UI |
| **v0.4 — Operational Workflows** | Wave 4 complete | Tasks + appointments + notifications |
| **v0.5 — Dashboard & Audit** | Wave 5 complete | CQRS proven, full UI |
| **v0.6 — Hardened** | Wave 6 complete | Observability + resilience |
| **v0.7 — Cloud-Deployed** | Wave 7 complete | Running on Azure |
| **v1.0 — Demo-Ready** | Wave 8 complete | Portfolio-ready release |

### Project Board Columns

Use a single GitHub Project (board view) with these columns:

| Column | Purpose |
|--------|---------|
| **Backlog** | Issues not yet planned for the current wave |
| **Ready** | Issues with all dependencies met, ready to pick up |
| **In Progress** | Currently being worked on (1-2 max for solo dev) |
| **In Review** | PR open, needs self-review or CI passing |
| **Done** | Merged and verified |

### Workflow

1. At the start of each wave, move relevant issues from **Backlog** to **Ready**.
2. Pick one issue from **Ready**. Move to **In Progress**. Create a feature branch.
3. Work the issue. Push commits. Open a PR.
4. PR triggers CI. Self-review. Move to **In Review**.
5. Merge. Move to **Done**. Pick next issue.
6. Keep WIP to 1-2 issues max. Finish what you start before starting new work.
7. At wave end, verify the milestone exit criteria. Close the milestone.

### Branch Strategy

- `main` — stable, demo-ready (deploy to staging/prod)
- `develop` — integration branch (deploy to dev)
- `feature/{issue-id}-short-description` — per-issue branches off develop
- Merge strategy: squash merge to develop, regular merge to main

---

## 8. Definition of Done

An issue is **Done** when all of the following are true:

### Code
- [ ] Implementation matches the issue scope and acceptance criteria
- [ ] No known bugs introduced
- [ ] Code follows established patterns from the service template (F1-06)
- [ ] No hardcoded secrets or credentials
- [ ] No cross-service database access or shared domain models

### Testing
- [ ] Unit tests written for business logic
- [ ] Integration tests written for API endpoints (against real local database)
- [ ] All existing tests pass
- [ ] Manual verification against acceptance criteria

### Quality
- [ ] Code compiles without warnings
- [ ] No unresolved TODOs left in changed code (use GitHub issues for future work)
- [ ] Event contracts match the shared definitions in F1-02

### Delivery
- [ ] PR reviewed (self-review for solo dev: read your own diff before merging)
- [ ] CI pipeline passes (once I7-05 is in place)
- [ ] Merged to `develop`
- [ ] Issue moved to Done on the project board

### Documentation (when applicable)
- [ ] API changes reflected in service's endpoint documentation
- [ ] New events added to shared contracts
- [ ] Breaking changes documented

---

## 9. Risks, Gaps, and De-scoping Suggestions

### Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| **10 microservices is a lot for solo dev** | Delivery slows, fatigue, partial completion | Strict wave discipline. Ship each wave fully before starting the next. Consider merging closely-related services if velocity drops (e.g., Care-Gap Engine could be part of Observation Service). |
| **CQRS adds complexity without visible payoff early** | Reporting Service work may feel like overhead | Defer full CQRS until Wave 5. Use direct service queries for early UI. Switch to read models when the dashboard epic begins. |
| **Event-driven debugging is hard solo** | Events lost, handlers fail silently, hard to trace | Invest early in correlation ID propagation and structured logging (Wave 1). Use RabbitMQ management UI to inspect queues. |
| **Scope creep from documentation** | The docs describe more than MVP needs | This delivery plan defines the MVP. If it's not in the issues list, it's not in scope for v1.0. |
| **Azure costs during cloud deployment** | Unexpected charges from AKS, SQL, Cosmos | Use dev-tier SKUs in Terraform. Set budget alerts. Destroy resources when not actively demoing. |
| **No existing code patterns** | Each service starts from zero | The reference service skeleton (F1-06) is the most important issue. Get it right. |

### Gaps in Documentation

| Gap | Impact | Recommendation |
|-----|--------|---------------|
| **No defined care plan templates** | Can't implement C2-04 without knowing milestone definitions | Define one concrete template: "General Post-Discharge" with 5 milestones (initial outreach 48h, first observation 72h, follow-up appointment 7d, care plan review 14d, 30-day completion). Document this in a separate file or seed data. |
| **No alert threshold values specified** | Care-Gap Engine rules are undefined | Define explicit thresholds: systolic BP > 180 (critical), > 160 (high); HR > 120 or < 50 (high); SpO2 < 90 (critical), < 94 (high); glucose > 300 or < 70 (high); temp > 38.5 (medium). Document in caregap config. |
| **BFF aggregation logic is underspecified** | Dashboard endpoint unclear | Start simple: BFF proxies 1:1. Add aggregation only for the dashboard summary endpoint. |
| **No mock auth specification** | Unclear how to bypass Entra ID locally | Define a mock auth middleware that accepts a fixed bearer token and returns configurable claims/roles. Document the dev token format. |
| **FHIR mapping details not needed for MVP** | Documented but not buildable without more specification | FHIR integration is explicitly deferred. Add to a "v2.0" backlog. |

### De-scoping Suggestions

**Defer from MVP (v1.0):**
- FHIR R4 projection (ADR-003 calls this secondary — confirm it's post-MVP)
- Workload Identity for all services (prove with one service, document the pattern)
- Multi-environment Terraform (dev only for v1.0)
- Production-grade KEDA scaling (HPA is sufficient for demo)
- Real notification delivery channels (log-based is fine for portfolio)
- Field-level access control (role-based route access is sufficient)
- Full contract testing suite (unit + integration is sufficient initially)
- Staging and production environments (dev + local is enough for v1.0)
- Cosmos DB emulator locally (in-memory store is simpler and sufficient)

**Simplify for MVP:**
- One care plan template instead of multiple diagnosis pathways
- Simplified discharge payload instead of full FHIR bundle parsing
- Hardcoded alert thresholds instead of configurable rules engine
- Basic string templates for notifications instead of a full templating system
- In-memory read model store locally instead of Cosmos DB emulator
- Mock auth middleware instead of full Entra ID integration

**Consider merging if velocity is slow:**
- Care-Gap Engine into Observation Service (same bounded context, reduces a service)
- Notification Service into a background worker within the BFF (simplifies deployment)
- Audit Service and Reporting Service share similar patterns (both are event consumers writing to Cosmos DB) — could be one service with two storage modes

### What v2.0 Could Look Like
After v1.0 is shipped and demo-ready, the next phase could add:
- FHIR R4 projection with Azure Health Data Services
- Real Entra ID integration replacing mock auth
- Multi-environment deployment (dev, staging, prod)
- Full observability stack with Grafana dashboards
- Workload Identity across all services
- Configurable alert thresholds via admin UI
- Email/SMS notification delivery
- SLA compliance reporting

---

*This plan is designed to be executed, not admired. Start at Wave 1, issue F1-01. Ship it. Move to the next issue.*

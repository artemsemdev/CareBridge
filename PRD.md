# PRD.md

# CareBridge
## Product Requirements Document
### Cloud-Native Post-Discharge Care Coordination Platform

**Document Status:** Draft v1.0  
**Product Type:** Public portfolio / reference implementation  
**Industry:** Healthcare provider operations  
**Primary Goal:** Demonstrate strong architecture and delivery skills in Azure, Kubernetes, microservices, security, observability, and healthcare interoperability  
**Author:** Artem portfolio project  
**Language:** American English

---

## 1. Executive Summary

CareBridge is a cloud-native healthcare operations platform designed to support the first 30 days after a patient is discharged from a hospital. The platform ingests synthetic discharge data, activates a post-discharge care plan, receives simulated remote patient monitoring readings, identifies care gaps or abnormal observations, creates follow-up tasks for coordinators, schedules appointments, sends reminders, and maintains an end-to-end audit trail.

The project is intended as a serious GitHub showcase. It is not a toy CRUD system and not a fake “hospital dashboard.” It is a realistic operational workflow product designed to demonstrate:

- Azure-native cloud architecture
- Kubernetes-first microservices delivery
- event-driven workflows
- healthcare interoperability using FHIR concepts
- security, identity, and secret handling
- observability and operational maturity

CareBridge must be presented clearly as a **reference implementation using synthetic data only**. It is **not** a certified clinical platform, medical device, or production healthcare system.

---

## 2. Product Vision

Build a modern care coordination platform that helps provider organizations reduce post-discharge risk by ensuring timely outreach, follow-up, monitoring, and escalation.

The platform should enable a care team to:

- see newly discharged patients
- activate diagnosis-specific care plans
- review incoming home-device observations
- detect missed milestones and abnormal readings
- create and track operational tasks
- schedule and monitor follow-up appointments
- notify patients and staff
- review a full patient case timeline with a searchable audit trail

From a portfolio perspective, the product must also prove the ability to design and implement:

- domain-oriented microservices
- AKS-based deployment and operations
- Azure-native security and identity
- asynchronous messaging and decoupled workflows
- healthcare data interoperability
- production-style observability and CI/CD

---

## 3. Problem Statement

Hospitals and care organizations often struggle during the transition from discharge to follow-up care. Patients may miss appointments, delay medication-related actions, fail to submit home-monitoring readings, or show worsening symptoms without timely intervention. Operational teams need a system that can coordinate follow-up actions, surface risk, and make the state of each case visible and traceable.

Most portfolio demos in healthcare either stay too shallow or become unrealistically broad. CareBridge should focus on one strong operational workflow and do it well.

---

## 4. Product Positioning

CareBridge is positioned as a **cloud-native post-discharge care coordination platform** for providers.

It is intended to showcase realistic healthcare workflow orchestration rather than generic record management.

### Positioning statement

For healthcare organizations that need to coordinate the first 30 days after discharge, CareBridge provides a unified operational platform for case tracking, care-plan execution, remote observation intake, alerting, task orchestration, and follow-up scheduling.

Unlike a generic dashboard or simple patient portal, CareBridge is designed around event-driven workflows, auditability, and Azure-native cloud operations.

---

## 5. Goals

### 5.1 Primary Goals

1. Demonstrate strong Azure architecture depth.
2. Demonstrate real Kubernetes and AKS delivery maturity.
3. Demonstrate microservices with event-driven workflows, not just REST APIs.
4. Demonstrate healthcare interoperability using FHIR-aligned concepts.
5. Demonstrate security, auditability, and observability by design.
6. Create a portfolio-grade GitHub project that looks credible in interviews and technical reviews.

### 5.2 Secondary Goals

1. Make the product runnable locally in a developer mode.
2. Make the product deployable end-to-end to Azure.
3. Make the product easy to demo in 5 to 10 minutes.
4. Make the business value easy to explain to non-technical reviewers.

---

## 6. Non-Goals

The MVP will not include:

- real EHR integrations
- real PHI or live patient data
- claims, reimbursement, or billing workflows
- medical diagnosis or treatment recommendations
- formal medical device functionality
- production-grade multi-tenant SaaS billing
- full regulatory certification
- broad AI features as part of the core MVP

Optional AI capabilities may be added later, but they are not required for the initial showcase release.

---

## 7. Target Users

### 7.1 Care Coordinator
A non-physician operational user responsible for outreach, reminders, task completion, and follow-up coordination.

### 7.2 Clinician
A nurse or physician who needs a concise patient status view, recent observations, active alerts, and care-plan progress.

### 7.3 Operations Manager
A manager who needs visibility into open alerts, overdue tasks, workload, queue health, and case throughput.

### 7.4 Platform Administrator
A technical or semi-technical admin who manages templates, thresholds, routing rules, notification templates, and operational troubleshooting.

---

## 8. Core User Journey

The main demo flow for CareBridge is:

1. A patient is discharged from the hospital.
2. A synthetic discharge bundle enters the platform.
3. A post-discharge case is created.
4. A care plan is activated automatically.
5. The patient begins submitting home readings.
6. The platform detects either an abnormal reading or a missed milestone.
7. A task is created for a care coordinator.
8. A reminder or escalation is sent.
9. A follow-up appointment is scheduled.
10. The case timeline reflects every step end-to-end.

This flow gives a complete and believable operational narrative for demos, documentation, and interviews.

---

## 9. Scope

### 9.1 MVP Scope

The MVP will support:

- synthetic discharge intake
- patient case creation
- care plan activation from templates
- remote observation ingestion
- rules-based alerting
- care-gap detection
- outreach task creation and tracking
- follow-up appointment scheduling
- patient reminders and staff notifications
- coordinator dashboard
- patient case timeline
- operational audit logging
- Azure deployment to AKS
- observability, dashboards, and CI/CD

### 9.2 Future Scope

Possible later extensions:

- patient-facing portal
- clinician mobile view
- rules designer UI
- document upload and export workflows
- richer analytics
- AI-generated case summaries from synthetic data
- multi-tenant support

---

## 10. Product Principles

1. **Healthcare-realistic, not tutorial-generic**  
   Every workflow must feel plausible for provider operations.

2. **Cloud-native from day one**  
   Services should be designed for containerized deployment, fault tolerance, and observability.

3. **Operationally visible**  
   The state of the system must be understandable through logs, traces, metrics, dashboards, and audit trails.

4. **Security by default**  
   Secrets, access, and service-to-service identity must follow Azure-native best practices.

5. **Demo-friendly**  
   The application should be easy to explain and easy to run locally or in Azure.

6. **Disciplined scope**  
   The number of services and features should stay focused on one strong business workflow.

---

## 11. Functional Requirements

### FR-1. Discharge Intake and Case Creation
The system shall accept a synthetic discharge bundle and create a new post-discharge case.

#### Details
- Intake payload shall include patient data, discharge date, encounter context, primary conditions, and follow-up recommendations.
- The system shall validate required fields.
- The system shall create a case record tied to the patient and discharge event.
- The system shall instantiate a care plan from a configurable template.

#### Acceptance Criteria
- A new case appears in the coordinator dashboard within 10 seconds of intake.
- Invalid submissions return clear validation errors.
- The case contains owner, status, due dates, and timeline entries.

---

### FR-2. Care Plan Management
The system shall create and manage a diagnosis- or pathway-based post-discharge care plan.

#### Details
- Care plans may include milestones such as initial outreach, appointment scheduling, medication review, and daily observation checks.
- Coordinators shall be able to mark milestones as completed, skipped, or escalated.
- Clinicians shall be able to add notes or recommendations.

#### Acceptance Criteria
- Users can view open, completed, and overdue milestones per case.
- Overdue milestones are clearly flagged.
- Care-plan changes are recorded in the audit trail.

---

### FR-3. Remote Observation Ingestion
The system shall ingest simulated remote patient monitoring data.

#### Supported Observation Types
- blood pressure
- heart rate
- SpO2
- temperature
- blood glucose
- weight

#### Details
- Each reading shall include patient ID, timestamp, type, value, and source.
- The ingestion pipeline shall support retries and idempotent processing.
- Invalid messages shall be routed to a failed-message path.

#### Acceptance Criteria
- Duplicate events do not create duplicate observations.
- Valid readings appear in the patient case timeline.
- Invalid readings are visible in an operations troubleshooting view.

---

### FR-4. Care-Gap Detection and Alerting
The system shall detect missed care milestones and abnormal readings.

#### Example Rules
- no patient outreach completed within 48 hours after discharge
- no follow-up appointment scheduled within 7 days
- blood glucose above configured threshold
- SpO2 below configured threshold
- no remote readings received for a configured number of days

#### Details
- Rules shall be configurable by care pathway.
- Alerts shall support severity levels: informational, medium, high, critical.
- Alerts shall trigger tasks, reminders, or escalations based on policy.

#### Acceptance Criteria
- Abnormal observations create alerts within 30 seconds.
- Missed milestones create care-gap alerts automatically.
- Alerts support states such as open, acknowledged, resolved, and dismissed.

---

### FR-5. Task Orchestration
The system shall create and track operational tasks for care coordinators.

#### Details
- Tasks may be created manually or automatically.
- Tasks shall include owner, patient context, priority, due date, status, and comments.
- Tasks shall support assignment, reassignment, closure, and auditability.
- Task creation shall preserve correlation IDs back to the source event.

#### Acceptance Criteria
- Coordinators can filter tasks by owner, priority, due date, and status.
- Closing a task updates the case timeline.
- Overdue tasks appear in operational dashboards.

---

### FR-6. Appointment Coordination
The system shall support scheduling and tracking follow-up appointments.

#### Details
- Coordinators can create a follow-up appointment tied to a case.
- Appointment states shall include proposed, booked, completed, canceled, and no-show.
- Appointment reminders shall be triggered automatically when configured.

#### Acceptance Criteria
- A booked appointment satisfies the relevant care-plan milestone.
- Missed appointments can trigger new follow-up tasks.
- Appointment history is visible in the patient timeline.

---

### FR-7. Notifications
The system shall send reminders and operational notifications.

#### Notification Targets
- patient reminder
- coordinator reminder
- clinician escalation
- admin operational alert

#### Channels for MVP
- email
- in-app notification
- simulated SMS provider

#### Acceptance Criteria
- Notification attempts are logged.
- Failures are retryable.
- Templates are configurable by event type.

---

### FR-8. Dashboard and Case Timeline
The system shall provide a web dashboard for operational users.

#### Dashboard Views
- newly discharged cases
- high-risk cases
- active alerts
- overdue tasks
- today’s appointments
- queue and workload metrics

#### Case Detail View
- patient summary
- discharge summary
- recent observations
- care plan progress
- alerts
- tasks
- appointments
- communications
- timeline

#### Acceptance Criteria
- The dashboard loads within 2 seconds at normal demo load.
- The case view provides a coherent single-case operational picture.
- Timeline entries are ordered, filterable, and correlated.

---

### FR-9. Audit Logging
The system shall maintain a searchable audit trail for user and system actions.

#### Events to Audit
- sign-in
- case creation
- care-plan change
- observation processed
- alert raised
- task assigned
- notification sent
- appointment booked
- configuration change

#### Acceptance Criteria
- Every event includes timestamp, actor, action, entity type, entity ID, and correlation ID.
- Audit records can be queried by case and by user.
- Sensitive data is masked where appropriate.

---

### FR-10. Administration
The system shall include a lightweight administration experience.

#### Admin Capabilities
- manage care pathway templates
- configure alert thresholds
- manage notification templates
- manage assignment queues
- view failed messages and dead-letter items
- review service health indicators

---

## 12. Suggested Service Architecture

The product shall use a **microservices architecture** with event-driven workflows.

### Core Services

#### 1. API Gateway / BFF
Responsible for edge access for the web UI, auth context, aggregation, and request routing.

#### 2. Case Service
Owns post-discharge case lifecycle, patient operational summary, and status.

#### 3. Care Plan Service
Owns care-plan templates, milestone execution, and progression.

#### 4. Observation Ingestion Service
Receives simulated device readings, validates them, stores them, and publishes events.

#### 5. Care-Gap Engine
Evaluates rules and raises alerts based on milestones and observations.

#### 6. Task Service
Owns operational tasks, assignment, comments, due dates, and closure.

#### 7. Appointment Service
Owns scheduling state and appointment lifecycle.

#### 8. Notification Service
Owns templates, delivery attempts, retry logic, and provider adapters.

#### 9. Audit Service
Stores immutable operational audit events.

#### 10. Reporting / Read Model Service
Builds denormalized read models for fast dashboard and timeline views.

### Architectural Guidance
- Keep service boundaries clear and domain-oriented.
- Prefer asynchronous integration where appropriate.
- Keep the total service count disciplined.
- Avoid architecture inflation just to appear “complex.”

---

## 13. Recommended Azure Architecture

### Compute and Containers
- Azure Kubernetes Service (AKS)
- Azure Container Registry (ACR)

### Clinical / Interoperability Layer
- Azure Health Data Services FHIR service for FHIR-aligned clinical resource storage and exchange

### Messaging
- Azure Service Bus for queues, topics, retries, and dead-letter handling

### Data Stores
- Azure SQL Database for transactional service-owned data
- Azure Cosmos DB for denormalized read models or case timeline views
- Azure Cache for Redis for caching and short-lived shared state
- Azure Blob Storage for documents, exports, and synthetic artifacts

### Identity and Secrets
- Microsoft Entra ID for workforce identity
- AKS Workload Identity for pod-to-Azure authentication
- Azure Key Vault
- Secrets Store CSI Driver with Key Vault provider

### Observability
- OpenTelemetry
- Azure Monitor Application Insights
- Azure Monitor / Container Insights
- Managed Prometheus
- Azure Managed Grafana

### Delivery and Infrastructure
- GitHub Actions
- OIDC federation from GitHub to Azure
- Terraform or Bicep for infrastructure provisioning
- Helm for Kubernetes packaging and deployment

---

## 14. Recommended Technology Stack

### Backend
- .NET 10
- ASP.NET Core
- Minimal APIs or clean REST APIs per service
- background workers for asynchronous processing
- EF Core where relational persistence is needed

### Frontend
- React
- TypeScript
- Tailwind CSS
- a clean internal operations dashboard UI

### Local Development
- Docker Compose for shared dependencies
- service stubs or adapters for cloud-only integrations
- synthetic data generator and demo scenario runner

### Deployment
- one container per service
- Helm charts per service or domain grouping
- namespace separation by environment
- readiness, liveness, and startup probes
- autoscaling and rolling deployments

---

## 15. Security Requirements

The security story must be strong because this is a healthcare project.

### Required Security Posture
- no secrets in source control
- no checked-in environment files with sensitive values
- least-privilege access between services
- workload identity instead of static credentials
- TLS for all service communication paths where applicable
- role-based access for workforce users
- audit logging by default
- synthetic data only

### Identity and Access
- internal users authenticate through Microsoft Entra ID
- backend services authenticate to Azure resources through Workload Identity
- authorization must be role-based and explicit

### Data Handling
- synthetic data only
- no real PHI
- masking for sensitive-looking fields in logs and audit views
- no patient identifiers exposed unnecessarily outside designated views

---

## 16. Compliance and Positioning Constraints

The repository must clearly state:

- this is a portfolio / reference implementation
- this uses synthetic or de-identified sample data only
- this is not intended for real patient care decisions
- this is not a certified healthcare product
- this is healthcare-aware by design, not production-certified

This wording is important to keep the project credible and responsible.

---

## 17. Non-Functional Requirements

### Availability
The platform should be designed for high availability in Azure, though the public demo environment may use smaller-scale infrastructure.

### Performance
- patient case page p95 under 500 ms from read model
- dashboard p95 under 2 seconds
- alert generation within 30 seconds of abnormal reading ingestion

### Reliability
- message processing must be idempotent
- retries must be explicit and observable
- dead-letter flows must be inspectable
- services must tolerate temporary downstream failure

### Scalability
Target synthetic benchmark:
- 10,000 active cases
- 500,000 observations per day
- bursty intake after discharge waves

### Observability
Every service must emit:
- structured logs
- metrics
- traces
- health endpoints
- correlation IDs

### Maintainability
- clean service boundaries
- documented contracts
- automated tests
- CI validation on pull requests
- repeatable infrastructure provisioning

---

## 18. UX Requirements

### Dashboard UX
The dashboard should look like a modern internal operations product rather than a public marketing app.

### UX Priorities
- fast scanning of open work
- clear severity and priority indicators
- simple task and alert filtering
- compact but useful case summaries
- timeline readability
- minimal clicks to reach action items

### UX Non-Goals
- overly decorative design
- consumer-style branding
- complex chart overload

---

## 19. Reporting and Operational Views

The MVP should include at least the following operational views:

- open alerts by severity
- overdue tasks by owner or team
- appointments scheduled today
- cases without outreach within SLA
- failed or dead-letter messages
- system health summary
- recent audit activity

These views are important because they show that the platform is usable operationally, not just technically impressive under the hood.

---

## 20. Demo Scenarios

### Scenario A: Standard Follow-Up
1. Synthetic discharge bundle is ingested.
2. Case is created.
3. Care plan is activated.
4. Coordinator completes first outreach.
5. Follow-up appointment is booked.
6. Case appears on-track.

### Scenario B: Abnormal Reading
1. Patient submits high blood glucose.
2. Observation is ingested.
3. Alert is created.
4. Task is assigned.
5. Coordinator escalates to clinician.
6. Alert is acknowledged and tracked.

### Scenario C: Missed Milestone
1. No appointment exists by day 7 after discharge.
2. Care-gap engine raises an alert.
3. Reminder is sent.
4. Outreach task appears.
5. Coordinator closes the loop.

### Scenario D: Operational Traceability
1. Admin opens the dashboard.
2. Reviews open alerts, overdue tasks, and failed messages.
3. Opens one case and follows the full timeline.
4. Reviews the audit trail for that case.

---

## 21. Success Metrics

### Product Metrics
- percentage of cases with follow-up scheduled within target window
- percentage of alerts acknowledged within SLA
- percentage of overdue tasks
- notification success rate

### Engineering Metrics
- deployment success rate
- lead time to deploy
- failed-message visibility
- mean time to detect processing failures
- end-to-end request trace coverage

### Portfolio Metrics
- a reviewer can understand the product in under 3 minutes
- the system can be run locally and deployed to Azure
- the architecture feels enterprise-grade, not tutorial-grade

---

## 22. Risks

### Main Risks
- over-scoping the product
- adding too many services without business value
- weak backend with over-investment in UI polish
- unrealistic healthcare claims
- complex setup that becomes hard to run or explain

### Mitigation Strategy
- keep the workflow focused on post-discharge coordination
- keep service count disciplined
- use synthetic data only
- prioritize architecture, observability, and documentation
- design for demo simplicity without compromising technical credibility

---

## 23. Delivery Milestones

### Milestone 1: Foundations
- repository setup
- service templates
- local dev environment
- synthetic data model
- CI baseline

### Milestone 2: Core Workflow
- discharge intake
- case creation
- care-plan activation
- dashboard skeleton

### Milestone 3: Monitoring and Alerts
- observation ingestion
- care-gap rules
- alert lifecycle
- task creation

### Milestone 4: Coordination
- appointment scheduling
- notifications
- timeline aggregation
- admin functions

### Milestone 5: Cloud Hardening
- AKS deployment
- Service Bus integration
- Workload Identity
- Key Vault integration
- observability stack

### Milestone 6: Portfolio Polish
- scenario runner
- demo data generator
- architecture diagrams
- documentation and screenshots

---

## 24. Suggested Repository Structure

```text
carebridge/
  docs/
    prd.md
    architecture.md
    adr/
    diagrams/
    demo-scenarios/
  src/
    gateway/
    services/
      case-service/
      careplan-service/
      observation-service/
      caregap-engine/
      task-service/
      appointment-service/
      notification-service/
      audit-service/
      reporting-service/
    web/
    shared/
  tests/
    unit/
    integration/
    contract/
  infra/
    terraform/
    bicep/
  deploy/
    helm/
  tools/
    synthetic-data-generator/
    scenario-runner/
  .github/
    workflows/
```

---

## 25. What This Project Must Prove

If built well, CareBridge should prove the following:

- strong Azure cloud architecture knowledge
- strong AKS and Kubernetes runtime knowledge
- strong microservices and event-driven design skills
- strong security and secret-management discipline
- strong observability and operations thinking
- strong awareness of healthcare interoperability and auditability
- strong ability to build something that feels enterprise-grade and interview-ready

This should be the standard against which all implementation decisions are measured.

---

## 26. Final Product Statement

CareBridge is a cloud-native healthcare operations platform focused on the critical post-discharge window. It combines a realistic provider workflow with a modern Azure and Kubernetes architecture. As a GitHub showcase, it is intended to demonstrate deep practical knowledge in microservices, AKS, Azure services, observability, security, and healthcare-aligned systems design.


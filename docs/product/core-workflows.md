# Core Workflows

**Document type:** Workflow reference
**Last updated:** 2026-03-27

---

## Overview

This document describes the core operational workflows in CareBridge. Each workflow specifies the trigger, the services involved, the events exchanged, and what the user sees. Mermaid diagrams illustrate the sequence or flow of each workflow.

These workflows map directly to the functional requirements in the [PRD](prd.md) (Sections FR-1 through FR-9) and the domain concepts defined in the [Domain Overview](domain-overview.md).

---

## 1. Discharge Intake and Case Activation

### Description

When a patient is discharged from the hospital, an external system sends a synthetic FHIR-aligned discharge bundle to CareBridge. This triggers the creation of a new post-discharge case, automatic activation of a diagnosis-specific care plan, and population of the coordinator dashboard with a new actionable case.

This is the entry point for all downstream coordination workflows. Everything in CareBridge begins with a discharge event.

### Sequence Diagram

```mermaid
sequenceDiagram
    participant EHR as External EHR
    participant GW as API Gateway
    participant CS as Case Service
    participant SB as Service Bus
    participant CPS as Care Plan Service
    participant RS as Reporting Service
    participant AS as Audit Service

    EHR->>GW: POST /api/discharges (FHIR bundle)
    GW->>GW: Validate auth, rate limit
    GW->>CS: Forward discharge bundle
    CS->>CS: Validate required fields
    CS->>CS: Create case record (status: created)
    CS->>SB: Publish CaseCreated event
    CS-->>GW: 201 Created (case ID)
    GW-->>EHR: 201 Created

    SB->>CPS: CaseCreated event
    CPS->>CPS: Match diagnosis to pathway template
    CPS->>CPS: Instantiate care plan with milestones
    CPS->>SB: Publish CarePlanActivated event
    CPS->>SB: Publish MilestoneActivated (initial outreach, 48h)
    CPS->>SB: Publish MilestoneActivated (appointment, 7d)

    SB->>RS: CaseCreated event
    RS->>RS: Create dashboard entry
    SB->>RS: CarePlanActivated event
    RS->>RS: Update case read model

    SB->>AS: CaseCreated event
    AS->>AS: Write audit record
    SB->>AS: CarePlanActivated event
    AS->>AS: Write audit record
```

### Services Involved

| Service | Role |
|---|---|
| API Gateway | Authentication, validation, routing |
| Case Service | Creates and owns the case lifecycle |
| Care Plan Service | Instantiates care plan from template, activates milestones |
| Reporting Service | Builds dashboard and timeline read models |
| Audit Service | Records immutable audit events |

### Events Exchanged

| Event | Publisher | Subscribers |
|---|---|---|
| `CaseCreated` | Case Service | Care Plan Service, Reporting Service, Audit Service |
| `CarePlanActivated` | Care Plan Service | Reporting Service, Audit Service |
| `MilestoneActivated` | Care Plan Service | Reporting Service, Audit Service |

### What the User Sees

- **Sarah (Coordinator):** A new case appears on the coordinator dashboard within seconds. The case detail view shows the patient summary, discharge context, and an active care plan with pending milestones (initial outreach due in 48h, follow-up appointment due in 7d).
- **Maria (Ops Manager):** The operations dashboard intake count increments. The new case appears in the SLA tracking view.

---

## 2. Observation Ingestion and Alerting

### Description

After discharge, a patient submits remote monitoring readings (simulated by a device simulator in this reference implementation). Each observation is validated, stored, and evaluated against configured thresholds. If an abnormal reading is detected, the Care-Gap Engine raises an alert, which triggers task creation and notification to the assigned coordinator.

### Sequence Diagram

```mermaid
sequenceDiagram
    participant Dev as Device / Simulator
    participant GW as API Gateway
    participant OS as Observation Service
    participant SB as Service Bus
    participant CGE as Care-Gap Engine
    participant TS as Task Service
    participant NS as Notification Service
    participant RS as Reporting Service
    participant AS as Audit Service

    Dev->>GW: POST /api/observations (reading)
    GW->>OS: Forward observation
    OS->>OS: Validate payload (idempotency check)
    OS->>OS: Store observation
    OS->>SB: Publish ObservationReceived event
    OS-->>GW: 202 Accepted
    GW-->>Dev: 202 Accepted

    SB->>CGE: ObservationReceived event
    CGE->>CGE: Load rules for case pathway
    CGE->>CGE: Evaluate observation against thresholds

    alt Abnormal reading detected
        CGE->>SB: Publish AlertRaised event (severity: high)
        SB->>TS: AlertRaised event
        TS->>TS: Create task (assigned to case coordinator)
        TS->>SB: Publish TaskCreated event

        SB->>NS: AlertRaised event
        NS->>NS: Resolve notification template
        NS->>NS: Send alert to coordinator (in-app + email)
        NS->>SB: Publish NotificationSent event
    end

    SB->>RS: ObservationReceived event
    RS->>RS: Update case timeline and observation history

    SB->>AS: ObservationReceived event
    AS->>AS: Write audit record
    SB->>AS: AlertRaised event
    AS->>AS: Write audit record
```

### Services Involved

| Service | Role |
|---|---|
| API Gateway | Authentication, routing |
| Observation Service | Validates, deduplicates, stores readings, publishes events |
| Care-Gap Engine | Evaluates observations against threshold rules |
| Task Service | Creates tasks from alerts |
| Notification Service | Sends alerts to coordinators |
| Reporting Service | Updates case timeline and observation history |
| Audit Service | Records audit events |

### Events Exchanged

| Event | Publisher | Subscribers |
|---|---|---|
| `ObservationReceived` | Observation Service | Care-Gap Engine, Reporting Service, Audit Service |
| `AlertRaised` | Care-Gap Engine | Task Service, Notification Service, Reporting Service, Audit Service |
| `TaskCreated` | Task Service | Reporting Service, Audit Service |
| `NotificationSent` | Notification Service | Audit Service |

### What the User Sees

- **Sarah (Coordinator):** If the reading is abnormal, an alert appears in the dashboard alert panel. A new task appears in her task list with the alert context. She receives an in-app notification and an email.
- **Dr. Rivera (Physician):** If the alert is escalated, he sees it in his clinical review queue with the observation value, threshold exceeded, and trend context.
- **Alex (Admin):** Normal processing is invisible. If the observation fails validation, it appears in the dead-letter inspector.

---

## 3. Care-Gap Detection (Scheduled)

### Description

Not all care gaps are triggered by incoming data. Some are detected by the absence of expected activity. A scheduled process (CronJob on AKS) triggers the Care-Gap Engine to scan for cases that are missing expected actions within their SLA windows: no outreach in 48 hours, no appointment in 7 days, no observations received in a configured number of days.

### Flow Diagram

```mermaid
flowchart TD
    A[CronJob Trigger] --> B[Care-Gap Engine]
    B --> C{Scan active cases}

    C --> D[Check: Outreach within 48h?]
    C --> E[Check: Appointment within 7d?]
    C --> F[Check: Observations received in X days?]
    C --> G[Check: Other milestone deadlines?]

    D -->|No outreach| H[Publish AlertRaised\ntype: missed-outreach]
    E -->|No appointment| I[Publish AlertRaised\ntype: missed-appointment]
    F -->|No readings| J[Publish AlertRaised\ntype: no-observations]
    G -->|Milestone overdue| K[Publish AlertRaised\ntype: milestone-overdue]

    H --> L[Task Service]
    I --> L
    J --> L
    K --> L

    L --> M[Create task for coordinator]

    H --> N[Notification Service]
    I --> N
    J --> N
    K --> N

    N --> O[Send notification to coordinator]

    H --> P[Reporting Service]
    I --> P
    J --> P
    K --> P

    P --> Q[Update dashboard and timeline]

    H --> R[Audit Service]
    I --> R
    J --> R
    K --> R

    R --> S[Write audit records]
```

### Services Involved

| Service | Role |
|---|---|
| CronJob (AKS) | Triggers the scheduled scan on a configured interval |
| Care-Gap Engine | Queries case and milestone state, evaluates gap rules, publishes alerts |
| Task Service | Creates tasks from care-gap alerts |
| Notification Service | Notifies coordinators of detected gaps |
| Reporting Service | Updates dashboards with new alerts and care gap indicators |
| Audit Service | Records alert and task creation events |

### Events Exchanged

| Event | Publisher | Subscribers |
|---|---|---|
| `AlertRaised` | Care-Gap Engine | Task Service, Notification Service, Reporting Service, Audit Service |
| `TaskCreated` | Task Service | Reporting Service, Audit Service |
| `NotificationSent` | Notification Service | Audit Service |

### What the User Sees

- **Sarah (Coordinator):** New tasks appear in her queue flagged as care gaps. The alert panel shows missed-outreach or missed-appointment alerts with the specific SLA window that was exceeded.
- **Maria (Ops Manager):** SLA compliance metrics update. Cases that fell out of compliance are visible in the SLA report. Rising care-gap volumes indicate a potential staffing or process issue.

---

## 4. Task Lifecycle

### Description

Tasks are the fundamental unit of work for care coordinators. They can be created automatically (from alerts, care-gap detection, or missed appointments) or manually by a coordinator or clinician. Each task follows a defined lifecycle from creation through closure, with every state change recorded in the audit trail and reflected in the case timeline.

### Flow Diagram

```mermaid
flowchart TD
    subgraph Creation
        A1[Alert raised] --> B[Task created automatically]
        A2[Coordinator creates manually] --> B
        A3[Missed appointment] --> B
    end

    B --> C[Task status: open]
    C --> D{Assigned to coordinator}
    D --> E[Task status: in-progress]

    E --> F{Coordinator works task}
    F --> G[Add notes / comments]
    F --> H[Contact patient]
    F --> I[Schedule appointment]
    F --> J[Escalate to clinician]

    G --> K{Task complete?}
    H --> K
    I --> K
    J --> K

    K -->|Yes| L[Task status: completed]
    K -->|No| E
    K -->|Cannot complete| M[Task status: deferred]

    L --> N[Publish TaskCompleted event]
    N --> O[Case timeline updated]
    N --> P{Milestone satisfied?}
    P -->|Yes| Q[Milestone marked complete]
    P -->|No| R[No milestone change]

    M --> S[Publish TaskDeferred event]
    S --> T[New task may be created later]
```

### Services Involved

| Service | Role |
|---|---|
| Task Service | Owns task lifecycle: creation, assignment, status changes, closure |
| Case Service | Receives task completion events to update case status |
| Care Plan Service | Evaluates whether a completed task satisfies a milestone |
| Reporting Service | Updates case timeline and dashboard task counts |
| Audit Service | Records every task state transition |

### Events Exchanged

| Event | Publisher | Subscribers |
|---|---|---|
| `TaskCreated` | Task Service | Reporting Service, Audit Service |
| `TaskAssigned` | Task Service | Notification Service, Audit Service |
| `TaskCompleted` | Task Service | Care Plan Service, Reporting Service, Audit Service |
| `TaskDeferred` | Task Service | Reporting Service, Audit Service |

### What the User Sees

- **Sarah (Coordinator):** Tasks appear in her task list sorted by priority and due date. She opens a task, sees the patient context and alert details, performs the required action, adds notes, and marks the task as completed. Completed tasks move to a "done" view. If a task satisfies a care plan milestone, the milestone indicator updates on the case detail view.
- **Maria (Ops Manager):** The operations dashboard shows open task counts, overdue tasks, and completion rates by coordinator. She can identify coordinators with growing backlogs and rebalance assignments.

---

## 5. Appointment Scheduling and Follow-Up

### Description

Follow-up appointments are a critical milestone in post-discharge care. Coordinators schedule appointments for patients, which satisfy care plan milestones. The system sends reminders before the appointment. If an appointment is missed, the system detects this and creates a new follow-up task.

### Sequence Diagram

```mermaid
sequenceDiagram
    participant Sarah as Coordinator (Sarah)
    participant GW as API Gateway
    participant APS as Appointment Service
    participant SB as Service Bus
    participant CPS as Care Plan Service
    participant NS as Notification Service
    participant TS as Task Service
    participant RS as Reporting Service
    participant AS as Audit Service

    Sarah->>GW: POST /api/appointments (case, date, provider)
    GW->>APS: Create appointment
    APS->>APS: Validate and store appointment
    APS->>SB: Publish AppointmentBooked event
    APS-->>GW: 201 Created
    GW-->>Sarah: Appointment confirmed

    SB->>CPS: AppointmentBooked event
    CPS->>CPS: Check if milestone satisfied
    CPS->>SB: Publish MilestoneCompleted event

    SB->>NS: AppointmentBooked event
    NS->>NS: Schedule reminder (24h before)
    NS->>SB: Publish NotificationScheduled event

    SB->>RS: AppointmentBooked event
    RS->>RS: Update case timeline
    SB->>RS: MilestoneCompleted event
    RS->>RS: Update care plan progress

    Note over NS: 24 hours before appointment
    NS->>NS: Send reminder to patient (SMS + email)
    NS->>SB: Publish NotificationSent event

    Note over APS: Appointment time passes

    alt Appointment completed
        Sarah->>GW: PATCH /api/appointments/{id} (status: completed)
        GW->>APS: Update status
        APS->>SB: Publish AppointmentCompleted event
        SB->>RS: Update timeline
        SB->>AS: Write audit record
    else Appointment missed
        APS->>SB: Publish AppointmentMissed event
        SB->>TS: AppointmentMissed event
        TS->>TS: Create follow-up task
        TS->>SB: Publish TaskCreated event
        SB->>NS: AppointmentMissed event
        NS->>NS: Send missed-appointment notification to coordinator
    end

    SB->>AS: Write audit records for all events
```

### Services Involved

| Service | Role |
|---|---|
| Appointment Service | Creates and manages appointment lifecycle |
| Care Plan Service | Evaluates whether a booked appointment satisfies a milestone |
| Notification Service | Schedules and sends appointment reminders; notifies on missed appointments |
| Task Service | Creates follow-up tasks when appointments are missed |
| Reporting Service | Updates case timeline and milestone progress |
| Audit Service | Records all appointment events |

### Events Exchanged

| Event | Publisher | Subscribers |
|---|---|---|
| `AppointmentBooked` | Appointment Service | Care Plan Service, Notification Service, Reporting Service, Audit Service |
| `MilestoneCompleted` | Care Plan Service | Reporting Service, Audit Service |
| `NotificationScheduled` | Notification Service | Audit Service |
| `NotificationSent` | Notification Service | Audit Service |
| `AppointmentCompleted` | Appointment Service | Reporting Service, Audit Service |
| `AppointmentMissed` | Appointment Service | Task Service, Notification Service, Reporting Service, Audit Service |
| `TaskCreated` | Task Service | Reporting Service, Audit Service |

### What the User Sees

- **Sarah (Coordinator):** She creates an appointment from the case detail view. The care plan milestone for "follow-up appointment within 7 days" changes from pending to completed. If a patient misses an appointment, a new task appears in her queue.
- **Patient (simulated):** Receives a reminder notification 24 hours before the appointment via simulated SMS and email.
- **Maria (Ops Manager):** Appointment scheduling rates and no-show rates appear on the operations dashboard.

---

## 6. End-to-End Case Timeline

### Description

Every event that occurs for a case — from discharge intake through care plan completion — flows to the Reporting Service, which builds a chronological, unified case timeline. This timeline is the primary view for understanding the complete history of a patient's post-discharge coordination. It aggregates events from every service into a single, ordered, filterable view.

### Flow Diagram

```mermaid
flowchart TD
    subgraph Event Sources
        CS[Case Service] -->|CaseCreated| SB[Service Bus]
        CPS[Care Plan Service] -->|CarePlanActivated\nMilestoneCompleted| SB
        OS[Observation Service] -->|ObservationReceived| SB
        CGE[Care-Gap Engine] -->|AlertRaised| SB
        TS[Task Service] -->|TaskCreated\nTaskCompleted| SB
        APS[Appointment Service] -->|AppointmentBooked\nAppointmentCompleted\nAppointmentMissed| SB
        NS[Notification Service] -->|NotificationSent| SB
    end

    SB --> RS[Reporting Service]

    RS --> D1[Cosmos DB\nCase Read Model]
    RS --> D2[Cosmos DB\nTimeline Collection]

    subgraph Case Detail View
        D1 --> V1[Patient Summary]
        D1 --> V2[Care Plan Progress]
        D2 --> V3[Chronological Timeline]
        D1 --> V4[Observation History]
        D1 --> V5[Active Alerts]
        D1 --> V6[Open Tasks]
        D1 --> V7[Appointments]
    end
```

### Timeline Event Types

| Event Type | Source Service | Timeline Display |
|---|---|---|
| Case created | Case Service | Case intake with patient summary and discharge context |
| Care plan activated | Care Plan Service | Care plan name and initial milestones |
| Milestone activated | Care Plan Service | Milestone name and due date |
| Milestone completed | Care Plan Service | Milestone completion with who and how |
| Observation received | Observation Service | Reading type, value, and normal/abnormal indicator |
| Alert raised | Care-Gap Engine | Alert type, severity, and trigger details |
| Alert acknowledged | Care-Gap Engine | Who acknowledged and when |
| Alert resolved | Care-Gap Engine | Resolution notes |
| Task created | Task Service | Task type, assignee, and source alert |
| Task completed | Task Service | Completion notes and time to resolution |
| Appointment booked | Appointment Service | Date, provider, and appointment type |
| Appointment completed | Appointment Service | Completion confirmation |
| Appointment missed | Appointment Service | No-show flag and follow-up task reference |
| Notification sent | Notification Service | Channel, recipient, template, and delivery status |

### Services Involved

| Service | Role |
|---|---|
| Reporting Service | Consumes events from all services, builds denormalized read models in Cosmos DB |
| All other services | Publish domain events that feed the timeline |
| API Gateway | Serves timeline data to the frontend |

### What the User Sees

- **Sarah (Coordinator):** The case detail view shows a unified timeline with every event in chronological order. She can filter by event type (observations only, alerts only, etc.) to focus on specific aspects of the case. The timeline gives her complete context when working a task or preparing for a patient call.
- **Dr. Rivera (Physician):** The case timeline provides clinical context — observation trends, alert history, and care plan progress — in one view without switching between systems.
- **Maria (Ops Manager):** Can open any case and see the full operational history. Useful for investigating SLA misses, understanding escalation patterns, or reviewing a case before a team meeting.
- **Alex (Admin):** The timeline complements the audit log. While the audit log is the system-of-record for compliance, the timeline is the user-facing operational narrative.

---

## Workflow Integration Map

The following diagram shows how the six workflows connect to form the complete CareBridge operational flow.

```mermaid
flowchart LR
    A[1. Discharge Intake\nand Case Activation] --> B[2. Observation Ingestion\nand Alerting]
    A --> C[3. Care-Gap Detection\nScheduled]
    A --> D[4. Task Lifecycle]
    A --> E[5. Appointment Scheduling\nand Follow-Up]

    B --> D
    C --> D
    E --> D

    B --> F[6. End-to-End\nCase Timeline]
    C --> F
    D --> F
    E --> F
    A --> F

    style A fill:#e1f5fe
    style F fill:#e8f5e9
```

**Workflow 1** (Discharge Intake) is the entry point that activates all other workflows. **Workflows 2-5** represent the operational activities during the 30-day post-discharge window. **Workflow 6** (Case Timeline) is the aggregation layer that unifies all activity into a coherent operational record.

---

## Cross-References

- [Product Requirements Document](prd.md) — functional requirements FR-1 through FR-9
- [Domain Overview](domain-overview.md) — domain concepts referenced in these workflows
- [User Personas](user-personas.md) — who executes and benefits from each workflow
- [Solution Architecture](../architecture/solution-architecture.md) — service design and infrastructure supporting these workflows
- [Non-Functional Requirements](non-functional-requirements.md) — performance and reliability constraints on these workflows

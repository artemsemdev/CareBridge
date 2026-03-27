# API and Event Contracts

**Document type:** Contract conventions
**Status:** Living document
**Last updated:** 2026-03-27

This document defines the conventions for HTTP APIs and Service Bus events across CareBridge. For per-service API details, see [Service Catalog](service-catalog.md). For data flow, see [Data Architecture](data-architecture.md).

---

## API Style

All services expose RESTful HTTP APIs using ASP.NET Core Minimal APIs.

- **JSON** request and response bodies (`Content-Type: application/json`).
- **Standard HTTP methods:** GET (read), POST (create), PUT (full replace), PATCH (partial update), DELETE (soft delete).
- **Resource-oriented URLs.** No RPC-style verb endpoints.
- **Consistent pluralization.** Collections use plural nouns: `/cases`, `/tasks`, `/observations`.

---

## Versioning Approach

- **URL path versioning:** `/api/v1/cases`, `/api/v1/tasks`.
- Major version in the URL path. Minor/patch changes are backward-compatible within a major version.
- Breaking changes (field removal, type changes, semantic changes) require a new major version.
- For portfolio scope, only `v1` exists. The versioning structure is in place for extensibility.

---

## Error Response Conventions

All error responses use **RFC 7807 Problem Details** format:

```json
{
  "type": "https://carebridge.example.com/errors/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more fields failed validation.",
  "instance": "/api/v1/cases",
  "errors": [
    { "field": "dischargeDate", "message": "Discharge date must not be in the future." },
    { "field": "patientId", "message": "Patient ID is required." }
  ]
}
```

### Standard Error Codes

| Status | Meaning | When Used |
|---|---|---|
| 400 | Bad Request | Malformed JSON, missing required fields |
| 401 | Unauthorized | Missing or invalid JWT |
| 403 | Forbidden | Valid JWT but insufficient role/permissions |
| 404 | Not Found | Resource does not exist |
| 409 | Conflict | Duplicate resource (e.g., idempotency key collision) |
| 422 | Unprocessable Entity | Semantically invalid (e.g., blood pressure value of 999) |
| 500 | Internal Server Error | Unexpected failure |
| 503 | Service Unavailable | Downstream dependency failure |

---

## Idempotency Expectations

- **POST** endpoints accept an optional `Idempotency-Key` header. The service deduplicates on this key within a 24-hour window.
- **GET**, **PUT**, **DELETE** are naturally idempotent.
- Critical for observation ingestion (device retries) and task creation (event replay).

```
POST /api/v1/observations
Idempotency-Key: device-001-2026-03-27T08:15:00Z-bp
Content-Type: application/json

{ ... }
```

If the same `Idempotency-Key` is sent again within 24 hours, the service returns the original 201 response without creating a duplicate.

---

## Pagination and Filtering Conventions

### Cursor-Based Pagination

```
GET /api/v1/tasks?limit=20&cursor=eyJpZCI6MTAwfQ
```

**Response:**
```json
{
  "items": [ ... ],
  "nextCursor": "eyJpZCI6MTIwfQ",
  "hasMore": true
}
```

| Parameter | Default | Max | Description |
|---|---|---|---|
| `limit` | 20 | 100 | Items per page |
| `cursor` | (none) | - | Opaque cursor from previous response |

### Filtering

Filtering uses query parameters:

```
GET /api/v1/tasks?status=open&priority=high&assignedTo=sarah.chen@contoso.com
GET /api/v1/observations?caseId=case-5678&type=blood-pressure&from=2026-03-20&to=2026-03-27
GET /api/v1/cases?status=active&sort=createdAt:desc
```

| Convention | Format |
|---|---|
| Equality filter | `?status=active` |
| Date range | `?from=2026-03-20&to=2026-03-27` |
| Sorting | `?sort=field:asc` or `?sort=field:desc` |
| Multiple values | `?status=open,in-progress` (comma-separated) |

---

## Standard Request/Response Headers

### Request Headers

| Header | Required | Description |
|---|---|---|
| `Authorization` | Yes | `Bearer {JWT}` from Entra ID |
| `Content-Type` | Yes (POST/PUT/PATCH) | `application/json` |
| `X-Correlation-ID` | Recommended | Client-generated correlation ID. BFF generates one if absent. |
| `Idempotency-Key` | Optional (POST) | Deduplication key for create operations |

### Response Headers

| Header | Description |
|---|---|
| `X-Correlation-ID` | Echoed from request (or BFF-generated) |
| `X-Request-ID` | Unique ID for this specific request (server-generated) |
| `Content-Type` | `application/json` |

---

## Sample REST Endpoints

### Create Case from Discharge Bundle

**Request:**
```http
POST /api/v1/cases
Authorization: Bearer eyJ...
Content-Type: application/json
X-Correlation-ID: corr-001
Idempotency-Key: discharge-enc-12345

{
  "patient": {
    "firstName": "Jane",
    "lastName": "Doe",
    "mrn": "MRN-12345",
    "dateOfBirth": "1958-04-15",
    "phone": "+15551234567",
    "email": "jane.doe@example.com"
  },
  "discharge": {
    "encounterRef": "enc-12345",
    "dischargeDate": "2026-03-20T14:00:00Z",
    "facility": "General Hospital",
    "primaryConditions": ["I50.9", "E11.65"],
    "followUpRecommendations": ["Cardiology follow-up within 7 days", "Daily weight monitoring"]
  }
}
```

**Response (201 Created):**
```json
{
  "caseId": "case-5678",
  "status": "active",
  "patientId": "pat-1234",
  "dischargeDate": "2026-03-20T14:00:00Z",
  "assignedCoordinator": null,
  "createdAt": "2026-03-20T14:01:00Z",
  "carePlanId": null
}
```

### Get Case Detail

**Request:**
```http
GET /api/v1/cases/case-5678
Authorization: Bearer eyJ...
X-Correlation-ID: corr-002
```

**Response (200 OK):**
```json
{
  "caseId": "case-5678",
  "status": "active",
  "patient": {
    "patientId": "pat-1234",
    "firstName": "Jane",
    "lastName": "Doe",
    "mrn": "MRN-12345"
  },
  "dischargeDate": "2026-03-20T14:00:00Z",
  "assignedCoordinator": "sarah.chen@contoso.com",
  "primaryConditions": ["I50.9", "E11.65"],
  "createdAt": "2026-03-20T14:01:00Z",
  "updatedAt": "2026-03-21T09:00:00Z"
}
```

### Submit Observation

**Request:**
```http
POST /api/v1/observations
Authorization: Bearer eyJ...
Idempotency-Key: device-001-2026-03-27T08:15:00Z-bp

{
  "caseId": "case-5678",
  "patientId": "pat-1234",
  "type": "blood-pressure",
  "value": {
    "systolic": 158,
    "diastolic": 95
  },
  "unit": "mmHg",
  "measuredAt": "2026-03-27T08:15:00Z",
  "source": "device-simulator"
}
```

**Response (201 Created):**
```json
{
  "observationId": "obs-7890",
  "caseId": "case-5678",
  "type": "blood-pressure",
  "status": "accepted",
  "processedAt": "2026-03-27T08:15:01Z"
}
```

### List Tasks with Filtering

**Request:**
```http
GET /api/v1/tasks?status=open&priority=high&limit=10&sort=dueDate:asc
Authorization: Bearer eyJ...
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "taskId": "task-1234",
      "caseId": "case-5678",
      "title": "Follow up on high blood pressure alert",
      "priority": "high",
      "status": "open",
      "assignedTo": "sarah.chen@contoso.com",
      "dueDate": "2026-03-28T17:00:00Z",
      "createdAt": "2026-03-27T08:16:00Z",
      "correlationId": "corr-001"
    }
  ],
  "nextCursor": "eyJpZCI6MTIzNH0",
  "hasMore": false
}
```

---

## Event Naming Conventions

**Pattern:** `{Entity}{PastTenseVerb}` in PascalCase.

| Event | Publisher | Description |
|---|---|---|
| `CaseCreated` | Case Service | New case from discharge intake |
| `CaseStatusChanged` | Case Service | Case status transition |
| `CarePlanActivated` | Care Plan Service | Plan instantiated from template |
| `MilestoneCompleted` | Care Plan Service | Milestone marked as done |
| `MilestoneOverdue` | Care Plan Service | Milestone past due date |
| `ObservationReceived` | Observation Service | Valid observation stored |
| `AlertRaised` | Care-Gap Engine | New alert detected |
| `AlertAcknowledged` | Care-Gap Engine | Alert acknowledged by user |
| `AlertResolved` | Care-Gap Engine | Alert resolved/dismissed |
| `TaskCreated` | Task Service | New task assigned |
| `TaskCompleted` | Task Service | Task marked as done |
| `TaskReassigned` | Task Service | Task reassigned |
| `AppointmentBooked` | Appointment Service | Appointment confirmed |
| `AppointmentCompleted` | Appointment Service | Appointment attended |
| `AppointmentMissed` | Appointment Service | No-show detected |
| `NotificationSent` | Notification Service | Notification delivered |
| `NotificationFailed` | Notification Service | Delivery permanently failed |

---

## Message Envelope Convention

Every Service Bus message uses a standard envelope:

```json
{
  "messageId": "msg-uuid-1234",
  "eventType": "CaseCreated",
  "timestamp": "2026-03-20T14:01:00Z",
  "correlationId": "corr-001",
  "causationId": null,
  "source": "case-service",
  "version": "1.0",
  "data": {
    "caseId": "case-5678",
    "patientId": "pat-1234",
    "status": "active",
    "dischargeDate": "2026-03-20T14:00:00Z",
    "primaryConditions": ["I50.9", "E11.65"]
  }
}
```

### Service Bus Message Properties
In addition to the JSON body, key metadata is set as **Service Bus application properties** for routing and filtering:

| Property | Value | Purpose |
|---|---|---|
| `eventType` | `CaseCreated` | Subscription filter matching |
| `correlationId` | `corr-001` | Cross-service tracing |
| `source` | `case-service` | Origin identification |
| `version` | `1.0` | Schema version for compatibility |

---

## Correlation Metadata

| Field | Description | Set By |
|---|---|---|
| `messageId` | Unique ID for this specific message | Publisher (UUID) |
| `correlationId` | Business-level trace through the entire workflow | BFF (originated at intake) |
| `causationId` | The `messageId` of the event that caused this event | Publisher |
| `source` | Service that published this event | Publisher |

**Event genealogy example:**
1. `CaseCreated` (messageId: msg-001, correlationId: corr-001, causationId: null)
2. `CarePlanActivated` (messageId: msg-002, correlationId: corr-001, causationId: msg-001)
3. `MilestoneOverdue` (messageId: msg-003, correlationId: corr-001, causationId: msg-002)
4. `AlertRaised` (messageId: msg-004, correlationId: corr-001, causationId: msg-003)
5. `TaskCreated` (messageId: msg-005, correlationId: corr-001, causationId: msg-004)

This chain enables full traceability: "this task was created because this milestone went overdue because this care plan was activated because this case was created."

---

## Retry Semantics

| Setting | Value | Rationale |
|---|---|---|
| Max delivery count | 5 | Enough for transient failures, not infinite |
| Retry delay pattern | 1s, 5s, 30s, 2min, 10min (exponential) | Increasing backoff for persistent issues |
| After max retries | Dead-letter queue | Preserves message for investigation |
| Lock duration | 60 seconds | Enough for processing + DB write |
| Session lock renewal | Every 30 seconds (if sessions enabled) | Prevents lock expiry during long processing |

Consumers **must** be idempotent. Retries may deliver the same message multiple times.

---

## Dead-Letter Semantics

Every Service Bus subscription has a dead-letter queue (DLQ).

**When messages go to DLQ:**
- Processing fails after max delivery count (5 retries exhausted)
- Message TTL expires (7 days)
- Consumer explicitly dead-letters a poison message

**DLQ message retention:**
- DLQ messages retain all original properties plus:
  - `DeadLetterReason` — why the message was dead-lettered
  - `DeadLetterErrorDescription` — error details from the last processing attempt

**Operations:**
- **Monitoring:** Grafana dashboard panel shows DLQ count per subscription. Alert fires on DLQ > 0.
- **Inspection:** Azure Portal Service Bus Explorer or Azure CLI to peek/receive DLQ messages.
- **Replay:** Manual replay after root cause is fixed. Verify consumer idempotency before replaying.
- **No automatic DLQ replay** in MVP. Automatic replay risks reprocessing poison messages in a loop.

See [Failed Message Processing runbook](../runbooks/failed-message-processing.md).

---

## Schema Evolution Principles

1. **Additive changes are safe.** Adding a new optional field to an event or API response is backward-compatible.
2. **Never remove fields within a version.** Consumers may depend on any documented field.
3. **Never change field types within a version.** A `string` field must remain a `string`.
4. **Consumers must ignore unknown fields.** A consumer receiving a field it doesn't recognize should skip it, not fail.
5. **Breaking changes require a new version.** If a field must be removed, renamed, or retyped, create a new event version (e.g., `CaseCreated` v1 → v2) and dual-publish during migration.
6. **Contract tests validate compatibility.** CI pipeline runs schema validation to catch accidental breaking changes.

---

## Backward Compatibility Expectations

### APIs
- New optional query parameters: backward-compatible.
- New optional response fields: backward-compatible.
- Removing a field: **breaking** — requires new API version.
- Changing field type or semantics: **breaking**.

### Events
- New optional fields in `data`: backward-compatible.
- Removing a field from `data`: **breaking** — requires new event version.
- Changing envelope structure: **breaking** — affects all consumers.
- Producers should populate all documented fields. Consumers should tolerate missing optional fields with sensible defaults.

---

## Sample Event Payloads

### CaseCreated
```json
{
  "messageId": "msg-a1b2c3d4",
  "eventType": "CaseCreated",
  "timestamp": "2026-03-20T14:01:00Z",
  "correlationId": "corr-001",
  "causationId": null,
  "source": "case-service",
  "version": "1.0",
  "data": {
    "caseId": "case-5678",
    "patientId": "pat-1234",
    "patientName": "Jane Doe",
    "mrn": "MRN-12345",
    "status": "active",
    "dischargeDate": "2026-03-20T14:00:00Z",
    "facility": "General Hospital",
    "primaryConditions": ["I50.9", "E11.65"],
    "assignedCoordinator": null
  }
}
```

### ObservationReceived
```json
{
  "messageId": "msg-e5f6g7h8",
  "eventType": "ObservationReceived",
  "timestamp": "2026-03-27T08:15:01Z",
  "correlationId": "corr-002",
  "causationId": null,
  "source": "observation-service",
  "version": "1.0",
  "data": {
    "observationId": "obs-7890",
    "caseId": "case-5678",
    "patientId": "pat-1234",
    "type": "blood-pressure",
    "value": { "systolic": 158, "diastolic": 95 },
    "unit": "mmHg",
    "measuredAt": "2026-03-27T08:15:00Z",
    "source": "device-simulator"
  }
}
```

### AlertRaised
```json
{
  "messageId": "msg-i9j0k1l2",
  "eventType": "AlertRaised",
  "timestamp": "2026-03-27T08:15:05Z",
  "correlationId": "corr-002",
  "causationId": "msg-e5f6g7h8",
  "source": "caregap-engine",
  "version": "1.0",
  "data": {
    "alertId": "alert-3456",
    "caseId": "case-5678",
    "ruleId": "rule-bp-high",
    "severity": "high",
    "type": "abnormal-reading",
    "description": "Systolic blood pressure 158 mmHg exceeds threshold of 140 mmHg",
    "triggerObservationId": "obs-7890"
  }
}
```

### TaskCreated
```json
{
  "messageId": "msg-m3n4o5p6",
  "eventType": "TaskCreated",
  "timestamp": "2026-03-27T08:15:06Z",
  "correlationId": "corr-002",
  "causationId": "msg-i9j0k1l2",
  "source": "task-service",
  "version": "1.0",
  "data": {
    "taskId": "task-1234",
    "caseId": "case-5678",
    "title": "Follow up on high blood pressure alert",
    "priority": "high",
    "status": "open",
    "assignedTo": "sarah.chen@contoso.com",
    "dueDate": "2026-03-28T17:00:00Z",
    "sourceAlertId": "alert-3456"
  }
}
```

### NotificationSent
```json
{
  "messageId": "msg-q7r8s9t0",
  "eventType": "NotificationSent",
  "timestamp": "2026-03-27T08:15:08Z",
  "correlationId": "corr-002",
  "causationId": "msg-i9j0k1l2",
  "source": "notification-service",
  "version": "1.0",
  "data": {
    "notificationId": "notif-5678",
    "caseId": "case-5678",
    "channel": "email",
    "recipient": "sarah.chen@contoso.com",
    "templateId": "alert-high-priority",
    "status": "delivered"
  }
}
```

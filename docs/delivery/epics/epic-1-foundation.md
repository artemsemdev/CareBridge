# Epic 1: Foundation and Developer Experience

**Epic ID:** `epic:foundation`
**Wave:** 1
**Milestone:** v0.1 — Foundation
**Estimated effort:** ~1 week
**Issues:** 6

---

## Why This Epic Exists

Every service needs shared infrastructure: event contracts, middleware, database tooling, and a local development environment. Without this, each service reinvents the wheel and patterns drift immediately.

## Value Delivered

A productive local development loop. Any new service can be scaffolded in minutes using established patterns. `docker-compose up` brings up the full infrastructure. The service template demonstrates all integration patterns end-to-end.

## Dependencies

None. This is the starting point for the entire project.

## Out of Scope

- Workload Identity configuration (local dev uses connection strings)
- FHIR service integration
- Terraform / cloud provisioning
- CI/CD pipeline
- React frontend (scaffolded but not functional)

## Exit Criteria

- [ ] `docker-compose up -d` starts SQL Server, RabbitMQ, and Azurite without errors
- [ ] `dotnet build` succeeds at the solution level
- [ ] Shared contracts compile and events serialize/deserialize correctly
- [ ] `dotnet run --project tools/db-migrator` applies migrations
- [ ] Reference service skeleton runs, responds to health checks, publishes and consumes events

---

## Delivery Slices

| # | Slice | What it delivers | Issues |
|---|-------|-----------------|--------|
| F1 | Solution structure | .NET solution, project scaffolding, folder layout | F1-01 |
| F2 | Shared contracts | Event envelope, domain types, event definitions | F1-02 |
| F3 | Shared middleware | Correlation IDs, error handling, health checks | F1-03 |
| F4 | Local infrastructure | Docker Compose with SQL Server, RabbitMQ, Azurite | F1-04 |
| F5 | Database migration tooling | EF Core migration runner tool | F1-05 |
| F6 | Service template | Reference service with all patterns wired end-to-end | F1-06 |

---

## Execution Order

```
F1-01 ──┬──→ F1-02 ──┐
        ├──→ F1-03 ──┤
F1-04 ──┴──→ F1-05 ──┴──→ F1-06
```

- **F1-01** and **F1-04** have no dependencies — start with either or both.
- **F1-02** and **F1-03** depend on F1-01 (need the solution structure).
- **F1-05** depends on F1-01 and F1-04 (needs project structure and a running SQL Server).
- **F1-06** depends on everything else — this is the integration proof.

---

## Issues

### F1-01: Create .NET solution structure and project scaffolding

**Labels:** `epic:foundation`, `type:infrastructure`, `priority:critical`
**Branch:** `feature/F1-01-solution-structure`

**Description:**
Set up the root .NET solution file and create empty project folders matching the documented repository structure. This establishes the project layout that every subsequent task depends on.

**Scope:**
- Create `CareBridge.sln` solution file
- Create ASP.NET Core Web API projects:
  - `src/gateway/CareBridge.Gateway`
  - `src/services/case-service/CareBridge.CaseService`
  - `src/services/careplan-service/CareBridge.CarePlanService`
  - `src/services/observation-service/CareBridge.ObservationService`
  - `src/services/caregap-engine/CareBridge.CareGapEngine`
  - `src/services/task-service/CareBridge.TaskService`
  - `src/services/appointment-service/CareBridge.AppointmentService`
  - `src/services/notification-service/CareBridge.NotificationService`
  - `src/services/audit-service/CareBridge.AuditService`
  - `src/services/reporting-service/CareBridge.ReportingService`
- Create class library projects:
  - `src/shared/CareBridge.Shared.Contracts`
  - `src/shared/CareBridge.Shared.Infrastructure`
- Create console app:
  - `tools/db-migrator/CareBridge.DbMigrator`
- Create `src/web/` directory with React + Vite + TypeScript initialization (`npm create vite@latest`)
- Create empty directories: `tests/unit/`, `tests/integration/`, `tests/contract/`, `infra/terraform/`, `deploy/helm/`, `tools/synthetic-data-generator/`
- Add all .NET projects to the solution file
- Add `.editorconfig` with consistent formatting rules

**Acceptance criteria:**
- [ ] `dotnet build` succeeds at the solution level with zero errors and zero warnings
- [ ] All service project folders exist per the documented repo structure in README
- [ ] React app scaffolded — `cd src/web && npm install && npm run dev` starts the Vite dev server
- [ ] Solution file references all .NET projects
- [ ] `.editorconfig` enforces consistent code style

**Dependencies:** None

---

### F1-02: Create shared event contracts and domain types

**Labels:** `epic:foundation`, `type:feature`, `priority:critical`
**Branch:** `feature/F1-02-shared-contracts`

**Description:**
Define the shared event envelope format, common domain value types, and all domain event contracts. These are the integration backbone — every service publishes and consumes through these shared definitions.

**Scope:**

Event envelope base class in `CareBridge.Shared.Contracts`:
```
IntegrationEvent (abstract record)
├── EventId: Guid
├── OccurredAt: DateTimeOffset
├── CorrelationId: string
├── EventType: string (fully qualified type name)
├── Version: int (default 1)
```

Domain event records (all extend `IntegrationEvent`):
- `CaseCreated` — CaseId, PatientId, PatientName, DiagnosisCode, DiagnosisDescription, DischargeDate, Status
- `CaseUpdated` — CaseId, Status, UpdatedAt
- `CarePlanActivated` — CarePlanId, CaseId, TemplateName, MilestoneCount, ActivatedAt
- `MilestoneCompleted` — MilestoneId, CarePlanId, CaseId, MilestoneName, CompletedAt
- `ObservationReceived` — ObservationId, CaseId, Type, Value, Unit, RecordedAt
- `AlertRaised` — AlertId, CaseId, AlertType, Severity, Description, CreatedAt
- `AlertAcknowledged` — AlertId, CaseId, AcknowledgedBy, AcknowledgedAt
- `AlertResolved` — AlertId, CaseId, ResolvedBy, ResolvedAt
- `TaskCreated` — TaskId, CaseId, AlertId, Title, Priority, CreatedAt
- `TaskCompleted` — TaskId, CaseId, CompletedBy, CompletedAt
- `AppointmentBooked` — AppointmentId, CaseId, Type, ScheduledAt
- `AppointmentCompleted` — AppointmentId, CaseId, CompletedAt
- `AppointmentMissed` — AppointmentId, CaseId, ScheduledAt
- `NotificationSent` — NotificationId, CaseId, Channel, Recipient, Subject, SentAt

Common value types and enums:
- `CaseStatus` — Active, Monitoring, Completed, Closed
- `Severity` — Informational, Medium, High, Critical
- `TaskStatus` — Open, InProgress, Completed, Deferred
- `TaskPriority` — Low, Medium, High, Urgent
- `AppointmentStatus` — Proposed, Booked, Completed, Canceled, NoShow
- `ObservationType` — BloodPressure, HeartRate, SpO2, Glucose, Temperature, Weight
- `NotificationChannel` — Email, InApp, SMS
- `AlertType` — AbnormalReading, MissedMilestone

Serialization configuration:
- System.Text.Json with camelCase property naming
- Enum serialization as strings
- DateTimeOffset in ISO 8601 format
- JSON serializer options as a shared static configuration

**Acceptance criteria:**
- [ ] All 14 events from the API and Event Contracts doc are represented as record types
- [ ] Event envelope matches the documented schema (EventId, OccurredAt, CorrelationId, EventType, Version)
- [ ] All types compile and are referenced from `CareBridge.Shared.Contracts`
- [ ] JSON serialization round-trips correctly for every event type (unit test per event)
- [ ] Shared JSON serializer options are accessible from any service

**Dependencies:** F1-01

**Technical notes:**
- Use C# `record` types for events — immutability and value equality for free
- Keep this project dependency-free (no EF Core, no ASP.NET references)
- Events carry all data consumers need (event-carried state transfer pattern) — don't reference entities from other services

---

### F1-03: Create shared infrastructure middleware

**Labels:** `epic:foundation`, `type:feature`, `priority:critical`
**Branch:** `feature/F1-03-shared-middleware`

**Description:**
Build reusable middleware for cross-cutting concerns that every service needs. This goes into `CareBridge.Shared.Infrastructure` and provides extension methods for consistent service startup.

**Scope:**

Correlation ID middleware:
- Reads `X-Correlation-Id` from incoming HTTP request headers
- Generates a new GUID if header is missing
- Attaches correlation ID to the logging scope (available in all log entries)
- Sets `X-Correlation-Id` on outgoing HTTP responses
- Provides `ICorrelationIdAccessor` interface for passing to event publishing

Global exception handler:
- Catches unhandled exceptions
- Returns RFC 7807 Problem Details JSON (`application/problem+json`)
- Maps `ValidationException` → 400, `KeyNotFoundException` → 404, `InvalidOperationException` → 409, unhandled → 500
- Logs the exception with correlation ID
- Does not leak stack traces in non-Development environments

Health check registration:
- `/startup` — returns 200 when the app has finished initialization
- `/ready` — returns 200 when the service can accept traffic (includes dependency checks)
- `/healthz` — liveness probe, returns 200 if the process is running
- Extension method: `app.MapCareBridgeHealthChecks()` registers all three

Service registration extensions:
- `builder.AddCareBridgeDefaults()` — registers correlation ID middleware, exception handler, JSON serializer options, health checks, structured logging with Serilog or built-in JSON formatter
- Configures Serilog or Microsoft.Extensions.Logging with structured JSON output
- Sets `Logging:LogLevel:Default` to Information, `Microsoft.AspNetCore` to Warning

**Acceptance criteria:**
- [ ] Correlation ID is generated when missing and propagated through the request pipeline
- [ ] Incoming correlation ID is preserved when present
- [ ] Unhandled exceptions return RFC 7807 JSON with correct status codes
- [ ] Health endpoints return 200 when healthy
- [ ] `AddCareBridgeDefaults()` wires everything in a single call
- [ ] Structured JSON log output includes correlation ID, timestamp, level, message

**Dependencies:** F1-01

**Technical notes:**
- This project depends on ASP.NET Core abstractions but not on EF Core or any service-specific code
- Keep the middleware generic — no business logic

---

### F1-04: Set up Docker Compose for local development

**Labels:** `epic:foundation`, `type:infrastructure`, `priority:critical`
**Branch:** `feature/F1-04-docker-compose`

**Description:**
Create a Docker Compose file that starts all infrastructure dependencies for local development. This is the foundation of the local dev loop — without it, no service can run.

**Scope:**

`docker-compose.yml` at the repository root with these services:

| Container | Image | Ports | Purpose |
|-----------|-------|-------|---------|
| `carebridge-sqlserver` | `mcr.microsoft.com/mssql/server:2022-latest` | 1433:1433 | Transactional databases for all services |
| `carebridge-rabbitmq` | `rabbitmq:3-management` | 5672:5672, 15672:15672 | Message bus (Service Bus substitute) |
| `carebridge-azurite` | `mcr.microsoft.com/azure-storage/azurite` | 10000:10000, 10001:10001, 10002:10002 | Azure Storage emulator |

Configuration:
- SQL Server: `SA_PASSWORD` set via `.env` file, `ACCEPT_EULA=Y`, `MSSQL_PID=Developer`
- RabbitMQ: default guest/guest credentials, management plugin enabled
- Azurite: default configuration
- Named Docker volumes for data persistence across restarts (`carebridge-sqldata`, `carebridge-rabbitmqdata`)
- Custom bridge network `carebridge-network`

Supporting files:
- `.env.example` with all connection strings and default values:
  ```
  SQL_SA_PASSWORD=CareBridge_Dev_2026!
  SQL_CONNECTION_STRING=Server=localhost,1433;User Id=sa;Password=CareBridge_Dev_2026!;TrustServerCertificate=True
  RABBITMQ_HOST=localhost
  RABBITMQ_PORT=5672
  RABBITMQ_USER=guest
  RABBITMQ_PASSWORD=guest
  AZURITE_CONNECTION_STRING=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1
  ```
- `.env` in `.gitignore` (copy `.env.example` to `.env` for local use)

**Acceptance criteria:**
- [ ] `docker-compose up -d` starts all three containers without errors
- [ ] SQL Server accepts connections on `localhost:1433` with the configured SA password
- [ ] RabbitMQ management UI accessible at `http://localhost:15672` (guest/guest)
- [ ] Azurite responds to blob storage requests on `localhost:10000`
- [ ] Data persists across `docker-compose down` / `docker-compose up` cycles (named volumes)
- [ ] `.env.example` documents all required environment variables

**Dependencies:** None

**Technical notes:**
- Cosmos DB emulator is intentionally excluded — we'll use an in-memory store in Reporting and Audit services for local dev
- Don't add service containers to Docker Compose — services run natively with `dotnet run` for faster iteration

---

### F1-05: Create database migration runner tool

**Labels:** `epic:foundation`, `type:infrastructure`, `priority:high`
**Branch:** `feature/F1-05-db-migrator`

**Description:**
Build a standalone console application that discovers and runs EF Core migrations for all service databases. Each service owns its own database, but a single tool simplifies the initial setup.

**Scope:**

Console app at `tools/db-migrator/CareBridge.DbMigrator`:
- References all service projects that have EF Core DbContexts (initially just the reference skeleton, later Case, CarePlan, Observation, etc.)
- For each DbContext:
  1. Read connection string from configuration (appsettings or environment variable)
  2. Create the database if it doesn't exist (`EnsureCreated` or migration-based)
  3. Apply pending migrations
  4. Log which migrations were applied
- Connection strings follow the pattern: `Server=localhost,1433;Database={service}-db;User Id=sa;Password={from env};TrustServerCertificate=True`

Database naming convention:
| Service | Database Name |
|---------|--------------|
| Case Service | `carebridge-case-db` |
| Care Plan Service | `carebridge-careplan-db` |
| Observation Service | `carebridge-observation-db` |
| Care-Gap Engine | `carebridge-caregap-db` |
| Task Service | `carebridge-task-db` |
| Appointment Service | `carebridge-appointment-db` |
| Notification Service | `carebridge-notification-db` |

Configuration via `appsettings.json` with environment variable overrides.

**Acceptance criteria:**
- [ ] `dotnet run --project tools/db-migrator` applies all pending migrations (initially the skeleton service only)
- [ ] Each service gets its own named database on the SQL Server instance
- [ ] Running the tool twice is idempotent — no errors, no duplicate migrations
- [ ] Console output clearly shows: database name → migrations applied (or "up to date")
- [ ] Connection strings configurable via environment variables or appsettings

**Dependencies:** F1-01, F1-04

**Technical notes:**
- This tool will grow as new services add their DbContexts — design it so adding a new DbContext is a one-line change
- The tool references service projects but only uses their DbContext types, not their API layer

---

### F1-06: Create reference service skeleton with end-to-end patterns

**Labels:** `epic:foundation`, `type:feature`, `priority:critical`
**Branch:** `feature/F1-06-service-skeleton`

**Description:**
Build one fully wired service skeleton that demonstrates all the patterns every service will use. This is the most important issue in the foundation — every subsequent service copies from this template.

**Scope:**

Project: Use one of the existing service projects (e.g., Case Service) or create a temporary `src/services/skeleton-service/` to demonstrate patterns. The skeleton will be refactored into the real Case Service in Epic 2.

**1. Minimal API structure:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddCareBridgeDefaults(); // from shared middleware
builder.Services.AddDbContext<SkeletonDbContext>(...);
builder.Services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddHostedService<EventConsumerBackgroundService>();

var app = builder.Build();
app.MapCareBridgeHealthChecks();
app.MapPost("/api/v1/items", CreateItem);
app.MapGet("/api/v1/items", ListItems);
app.MapGet("/api/v1/items/{id}", GetItem);
app.Run();
```

**2. EF Core setup:**
- DbContext with one sample entity
- One migration
- `appsettings.Development.json` with local SQL Server connection string

**3. Event publishing:**
- `IEventPublisher` interface in shared infrastructure:
  ```csharp
  public interface IEventPublisher
  {
      Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IntegrationEvent;
  }
  ```
- `RabbitMqEventPublisher` implementation:
  - Connects to RabbitMQ using connection string from configuration
  - Publishes to a topic exchange named after the event type (e.g., `carebridge.events`)
  - Serializes event using shared JSON options
  - Includes correlation ID in message headers

**4. Event consuming:**
- `IEventHandler<T>` interface:
  ```csharp
  public interface IEventHandler<T> where T : IntegrationEvent
  {
      Task HandleAsync(T @event, CancellationToken ct = default);
  }
  ```
- `EventConsumerBackgroundService` — a hosted service that:
  - Subscribes to a RabbitMQ queue
  - Deserializes events using the shared envelope format
  - Routes to the correct `IEventHandler<T>` based on event type
  - Acknowledges on success, requeues on failure (with retry limit)
  - Extracts correlation ID from message headers and sets it in the scope

**5. Testing patterns:**
- One unit test: tests business logic with mocked `IEventPublisher` and mocked DbContext
- One integration test: starts the API with `WebApplicationFactory`, uses real SQL Server from Docker Compose, verifies POST → GET round-trip and event publication to RabbitMQ

**Acceptance criteria:**
- [ ] Service starts with `dotnet run` and responds to `GET /healthz` with 200
- [ ] `POST /api/v1/items` creates a record in SQL Server and returns 201
- [ ] `GET /api/v1/items` returns the created record
- [ ] Creating an item publishes an event to RabbitMQ (visible in management UI)
- [ ] Event consumer receives the event and processes it (log output confirms)
- [ ] Correlation ID flows from HTTP request → log entries → published event → consumed event log
- [ ] Unit test passes with `dotnet test`
- [ ] Integration test passes against Docker Compose infrastructure

**Dependencies:** F1-01, F1-02, F1-03, F1-04

**Technical notes:**
- This skeleton will be evolved into the real Case Service in Epic 2 — don't create a throwaway project
- The RabbitMQ abstractions (`IEventPublisher`, `IEventHandler<T>`) must be clean enough that swapping to Azure Service Bus later is a single implementation change
- Use the `RabbitMQ.Client` NuGet package (not MassTransit or NServiceBus — keep it lean)
- Keep the integration test isolated: each test creates its own database and cleans up after itself

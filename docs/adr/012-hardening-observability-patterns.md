# ADR-012: Hardening and Observability Patterns

**Status:** Accepted
**Date:** 2026-04-02
**Deciders:** Solution architect

## Context

CareBridge is a distributed microservices platform with 9 backend services, a BFF gateway, and a React frontend. After completing the core business logic (Epics 1-6), the system needed production-grade hardening: health checks for Kubernetes probes, structured logging for log aggregation, distributed tracing for cross-service debugging, resilience patterns for cascade failure protection, and systematic error handling review.

These concerns cut across all services and needed to be implemented consistently via the shared infrastructure layer (`CareBridge.Shared.Infrastructure`).

## Decision

### Health Checks (H-01)

All services expose three Kubernetes-compatible health endpoints via `MapCareBridgeHealthChecks()`:

- `/healthz` (liveness) -- always returns 200 if the process is running. No dependency checks.
- `/ready` (readiness) -- checks critical dependencies. SQL Server and RabbitMQ health checks are tagged "ready".
- `/startup` (startup) -- returns 503 during initialization, 200 after `UseCareBridgeDefaults()` completes.

Dependency checks per service:

| Service | SQL | RabbitMQ |
|---------|:---:|:--------:|
| Case Service | Yes | -- |
| Care Plan Service | Yes | Yes |
| Observation Service | Yes | -- |
| Care-Gap Engine | Yes | Yes |
| Task Service | Yes | Yes |
| Appointment Service | Yes | -- |
| Notification Service | Yes | Yes |
| Audit Service | -- | Yes |
| Reporting Service | -- | Yes |
| Gateway (BFF) | -- | -- |

NuGet packages: `AspNetCore.HealthChecks.SqlServer`, `AspNetCore.HealthChecks.Rabbitmq`.

### Structured JSON Logging (H-02)

All services output structured JSON logs via Serilog's `RenderedCompactJsonFormatter`. Every log entry includes:

- `@t` (timestamp), `@l` (level), `@m` (message)
- `Service` property (enriched from configuration or assembly name)
- `CorrelationId` (enriched via `CorrelationIdMiddleware` log scope)
- Request logging at Debug level via `UseSerilogRequestLogging()` (method, path, status code, duration)

Log level overrides: `Microsoft.AspNetCore: Warning`, `Microsoft.EntityFrameworkCore: Warning`.

### OpenTelemetry Distributed Tracing (H-03)

All services are instrumented with OpenTelemetry via `AddCareBridgeDefaults()`:

- ASP.NET Core instrumentation (inbound HTTP spans)
- HttpClient instrumentation (outbound HTTP spans with parent-child relationship)
- Entity Framework Core instrumentation (database query spans)
- W3C TraceContext propagation (default)
- Console exporter available for local debugging (`OpenTelemetry:ConsoleExporter = true`)
- Can be disabled entirely (`OpenTelemetry:Enabled = false`)

Service name is derived from `ServiceName` configuration key or assembly name.

### Circuit Breakers (H-04)

All inter-service HTTP calls use `Microsoft.Extensions.Http.Resilience` standard resilience handler:

- **Retry:** 2 retries with exponential backoff (200ms base)
- **Circuit breaker:** Opens after 50% failure rate within 30s sampling window (minimum 5 requests). Half-open after 30s.
- **Timeout:** 10 seconds per attempt, 30 seconds total across retries.

Applied to all named `HttpClient` registrations in:
- Gateway (8 clients: Case, CarePlan, Observation, CareGap, Task, Appointment, Reporting, Audit)
- Care-Gap Engine (1 client: CarePlan)
- Care Plan Service (1 client: CareGap Engine)

When the circuit is open, the Gateway returns `503 Service Unavailable` with ProblemDetails.

### Error Handling Hardening (H-05)

- **EF Core transient retry:** `EnableRetryOnFailure()` added to all 7 DbContext SQL Server configurations.
- **Input length validation:** All user-facing endpoints validate string length against database column limits before persistence.
- **DbUpdateException handling:** `ExceptionHandlerMiddleware` maps `DbUpdateException` (constraint violations) to `409 Conflict`.
- **Event handler resilience:** `EventConsumerBackgroundService` already wraps all handler calls in try-catch with NACK/retry and max-retry dead-lettering.
- **Idempotency constraints:** Database unique indexes on Observation.IdempotencyKey, CarePlan.CaseId, and Task.AlertId prevent race condition duplicates.

## Consequences

**Positive:**

- All services are Kubernetes-ready with startup, readiness, and liveness probes.
- Structured JSON logging enables log aggregation in any platform (ELK, Loki, Azure Monitor).
- OpenTelemetry provides vendor-neutral distributed tracing that can export to any backend.
- Circuit breakers prevent cascade failures when a downstream service is unhealthy.
- Consistent patterns via shared infrastructure -- new services get all hardening automatically.

**Negative:**

- OpenTelemetry adds a small performance overhead to each HTTP request and database query.
- Circuit breaker configuration uses defaults that may need tuning under production load.
- Structured JSON logs are less human-readable in console output compared to the previous formatted output.

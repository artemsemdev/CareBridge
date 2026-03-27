# Non-Functional Requirements

**Document type:** Quality attributes reference
**Last updated:** 2026-03-27

---

## Overview

This document defines the non-functional requirements (quality attributes) for CareBridge. These requirements constrain how the system behaves beyond its functional capabilities — covering performance, scalability, reliability, availability, security, observability, maintainability, portability, and data retention.

All targets are specified for the reference implementation using synthetic data. Production deployments in a real healthcare environment would require additional validation and potentially stricter thresholds.

---

## Performance Requirements

| Metric | Target | Notes |
|---|---|---|
| Case detail page load (p95) | < 500 ms | Served from Cosmos DB denormalized read model. Includes patient summary, care plan progress, recent observations, alerts, tasks, and appointments. |
| Dashboard load (p95) | < 2 s | Coordinator dashboard with active cases, open alerts, overdue tasks, and today's appointments. Served from pre-aggregated read models. |
| Alert generation latency | < 30 s from abnormal observation ingestion | Measured from the time an abnormal observation is accepted by the Observation Service to the time the Care-Gap Engine publishes the AlertRaised event. Includes Service Bus transit and rule evaluation. |
| Observation ingestion acknowledgment (p95) | < 200 ms | HTTP response time for accepting an observation. Processing is asynchronous — the 200 ms target covers validation and queue submission only. |
| Task list query (p95) | < 300 ms | Filtered task list for a single coordinator. Served from SQL read replica or indexed query. |
| Audit log query (p95) | < 1 s | Filtered by case ID or user ID within a 30-day window. |
| Timeline rendering (p95) | < 800 ms | Full chronological timeline for a single case, up to 500 events. |
| API Gateway routing overhead | < 20 ms added latency | Gateway should add minimal overhead to downstream service response times. |

### Measurement approach

- Performance targets are measured under synthetic benchmark load (see Scalability Targets).
- Percentile targets (p95) mean that 95% of requests complete within the stated threshold.
- Measurement uses distributed tracing via OpenTelemetry. Latency is captured at the API Gateway entry point and at each service boundary.
- Dashboard and page load targets include backend response time only. Frontend rendering time is additional.

---

## Scalability Targets

| Dimension | Target | Notes |
|---|---|---|
| Active cases | 10,000 | Cases in `created`, `active`, or `at-risk` status simultaneously. |
| Observations per day | 500,000 | Approximately 50 readings per day across 10,000 patients, with headroom for burst patterns. |
| Bursty intake patterns | Morning discharge waves | Discharge bundles arrive in concentrated windows (8:00-11:00 AM). The system must handle intake spikes without degrading dashboard performance. |
| Concurrent dashboard users | 50 | Care coordinators, clinicians, and managers accessing dashboards simultaneously. |
| Alerts generated per day | Up to 5,000 | Based on a 5-10% abnormal observation rate plus scheduled care-gap detection. |
| Tasks created per day | Up to 3,000 | From alerts, care gaps, and manual creation. |
| Notifications sent per day | Up to 10,000 | Reminders, escalations, and operational alerts across all channels. |

### Scaling mechanisms

| Mechanism | Where applied |
|---|---|
| **Horizontal Pod Autoscaler (HPA)** | API Gateway, Case Service, Task Service, Appointment Service, Reporting Service. Scales based on CPU and memory utilization. |
| **KEDA (Kubernetes Event-Driven Autoscaling)** | Observation Service, Care-Gap Engine, Notification Service. Scales based on Service Bus queue depth, enabling scale-to-zero during idle periods and rapid scale-out during bursts. |
| **Cosmos DB autoscale** | Read model collections scale throughput (RU/s) automatically based on demand. |
| **Azure SQL elastic pools** | Service databases can share resources and scale independently as needed. |
| **Service Bus partitioning** | Topics are partitioned to support high-throughput event publishing. |

---

## Reliability Requirements

| Requirement | Details |
|---|---|
| **Idempotent message processing** | Every service that consumes Service Bus messages must process them idempotently. Duplicate delivery (at-least-once semantics) must not create duplicate records, duplicate alerts, or duplicate notifications. Idempotency keys are derived from message ID or correlation ID. |
| **At-least-once delivery** | Azure Service Bus guarantees at-least-once delivery for all topic subscriptions. Combined with idempotent consumers, this ensures no messages are silently lost. |
| **Dead-letter queues** | Every Service Bus subscription has a dead-letter queue configured. Messages that fail processing after the configured retry count (default: 3 attempts with exponential backoff) are moved to the dead-letter queue for inspection and manual replay. |
| **Circuit breaker pattern** | Services that call downstream dependencies (databases, external APIs, other services) implement circuit breakers. When a downstream service is unavailable, the circuit opens and requests fail fast rather than cascading timeouts. Circuit state is monitored via health endpoints and metrics. |
| **Retry with exponential backoff** | Transient failures (HTTP 429, 503, network timeouts) are retried with exponential backoff and jitter. Maximum retry attempts and backoff intervals are configurable per service. |
| **No data loss for audit events** | Audit events are treated as critical. The Audit Service subscription has a higher retry count (5 attempts) and a longer lock duration. Audit dead-letter messages trigger an operational alert. |
| **Graceful shutdown** | Services handle SIGTERM by completing in-flight requests and messages before shutting down. Kubernetes termination grace period is set to 30 seconds. |
| **Health probes** | Every service exposes `/health/ready` (readiness), `/health/live` (liveness), and `/health/startup` (startup) endpoints. Kubernetes uses these to manage traffic routing and pod lifecycle. |

---

## Availability

| Dimension | Target | Notes |
|---|---|---|
| API endpoint availability | 99.5% during business hours | Measured as successful HTTP responses (non-5xx) divided by total requests. Business hours defined as 7:00 AM - 7:00 PM local time. |
| Dashboard availability | 99.5% during business hours | Dashboard read model availability. Degraded mode shows cached data with a staleness indicator if the Reporting Service is temporarily unavailable. |
| Message processing availability | 99.9% | Service Bus provides built-in high availability. Messages are persisted and retried. Processing may be delayed during service restarts but will not be lost. |
| Scheduled care-gap detection | 99% execution rate | CronJob executions should complete successfully at least 99% of the time. Failed executions are logged and retried on the next schedule. |

### Availability mechanisms

| Mechanism | Details |
|---|---|
| **AKS zone redundancy** | In production, the AKS cluster spans multiple availability zones. Pod anti-affinity rules distribute replicas across zones. |
| **Azure SQL zone redundancy** | Production databases use zone-redundant configuration for automatic failover. |
| **Cosmos DB multi-region** | For production, Cosmos DB can be configured with multi-region writes. For the reference implementation, single-region with zone redundancy is sufficient. |
| **Service Bus premium tier** | Premium tier provides zone redundancy and predictable performance. Standard tier is acceptable for the reference implementation. |
| **Graceful degradation** | The dashboard is designed to function with partial backend availability. If the Observation Service is down, the dashboard still shows case data, tasks, and appointments. Missing sections display a "temporarily unavailable" indicator rather than failing the entire page. |
| **Rolling deployments** | Helm deployments use rolling update strategy with `maxUnavailable: 0` to ensure zero-downtime deployments. Readiness probes gate traffic to new pods. |

---

## Security

Security is a first-class concern for CareBridge given its healthcare context. Detailed security requirements, threat model, and implementation patterns are documented in the dedicated security reference.

### Summary

| Area | Approach |
|---|---|
| **Authentication** | Microsoft Entra ID for workforce users. OAuth 2.0 / OIDC tokens validated at the API Gateway. |
| **Authorization** | Role-based access control (RBAC). Roles: coordinator, clinician, operations-manager, administrator. Enforced at the API Gateway and within each service. |
| **Service-to-service identity** | AKS Workload Identity. No static credentials or connection strings in pods. Services authenticate to Azure resources (SQL, Cosmos DB, Service Bus, Key Vault) using managed identity. |
| **Secrets management** | Azure Key Vault with Secrets Store CSI Driver. Secrets are mounted into pods as volumes, not environment variables. No secrets in source control. |
| **Data protection** | TLS for all service communication. Encryption at rest for all Azure data stores (platform-managed keys). Synthetic data only — no real PHI. |
| **Audit trail** | All user and system actions are logged to the Audit Service with actor, action, entity, timestamp, and correlation ID. Audit records are immutable. |
| **Log sanitization** | Sensitive-looking fields (patient identifiers, names) are masked in application logs. Full data is available only in designated views with appropriate authorization. |

**Full reference:** [Security and Compliance](../architecture/security-and-compliance.md)

---

## Observability

Every service in CareBridge emits structured telemetry. The observability stack enables operational monitoring, incident investigation, and performance analysis.

### Summary

| Signal | Implementation |
|---|---|
| **Structured logs** | JSON-formatted logs with correlation ID, service name, operation, and severity. Shipped to Azure Monitor / Log Analytics. |
| **Distributed traces** | OpenTelemetry SDK in every service. Traces propagate across HTTP calls and Service Bus messages. Exported to Application Insights. |
| **Metrics** | OpenTelemetry metrics for request latency, error rates, queue depths, active cases, and business KPIs. Exported to Managed Prometheus. |
| **Dashboards** | Azure Managed Grafana dashboards for infrastructure health, service performance, and business operations. |
| **Health endpoints** | `/health/ready`, `/health/live`, `/health/startup` on every service. Used by Kubernetes and by the system health view in the admin panel. |
| **Alerting** | Azure Monitor alert rules for error rate spikes, latency threshold breaches, dead-letter queue growth, and pod restart loops. |
| **Correlation** | Every request and message carries a correlation ID that threads through all services. This enables end-to-end tracing of a single discharge event through case creation, care plan activation, observation processing, alerting, and task completion. |

**Full reference:** [Observability](../architecture/observability.md)

---

## Maintainability

| Requirement | Details |
|---|---|
| **Clean service boundaries** | Each service owns its domain, its data store, and its API contract. Services communicate through well-defined events on Service Bus and through REST APIs via the API Gateway. No shared databases between services. |
| **Automated testing on every PR** | Unit tests, integration tests, and contract tests run in GitHub Actions on every pull request. PRs cannot be merged without passing checks. |
| **Documented API contracts** | Every service exposes an OpenAPI specification. Event schemas are documented with versioning. API and event contracts are the primary interface documentation. |
| **Documented event contracts** | All Service Bus events follow a standardized envelope format with event type, version, correlation ID, timestamp, and payload. Schema changes follow a backward-compatible evolution strategy. |
| **CI/CD with validation gates** | GitHub Actions pipelines include: lint, build, test, container image build, Helm chart validation, and deployment to staging. Production deployment requires manual approval. |
| **Infrastructure as code** | All Azure infrastructure is provisioned via Terraform or Bicep. No manual resource creation. Infrastructure changes go through the same PR review process as application code. |
| **Consistent service structure** | All backend services follow a common project structure, shared libraries for cross-cutting concerns (logging, tracing, health checks, Service Bus integration), and consistent configuration patterns. |
| **Feature flags** | New features can be gated behind feature flags for incremental rollout and safe testing in production-like environments. |

---

## Portability

| Requirement | Details |
|---|---|
| **Containerized services** | Every service is packaged as a Docker container with a standard Dockerfile. Containers run on any Kubernetes cluster, not just AKS. Local development uses Docker Compose. |
| **Kubernetes-native deployment** | Helm charts define all deployment manifests. Services use standard Kubernetes primitives (Deployments, Services, ConfigMaps, Secrets, CronJobs). No AKS-specific APIs in application code. |
| **OpenTelemetry for observability portability** | Telemetry is emitted via the OpenTelemetry SDK and OpenTelemetry Collector. The collector can export to any supported backend (Application Insights, Jaeger, Datadog, Grafana Cloud). Changing the observability backend does not require application code changes. |
| **FHIR alignment for clinical data portability** | Clinical data structures (patient, encounter, observation) align with HL7 FHIR R4 resource definitions. This enables interoperability with FHIR-compliant systems and positions the platform for real EHR integration in a production scenario. |
| **Database abstraction** | Data access uses Entity Framework Core with repository patterns. Switching from Azure SQL to another relational database requires configuration changes, not application rewrites. |
| **Configuration externalization** | All environment-specific configuration (connection strings, feature flags, thresholds) is externalized via Kubernetes ConfigMaps and Secrets. Application code reads configuration through the standard .NET configuration provider — no hardcoded values. |

---

## Data Retention

| Data Category | Retention Period | Storage | Notes |
|---|---|---|---|
| **Operational data (active)** | Active case lifetime + 90 days after case closure | Azure SQL (transactional), Cosmos DB (read models) | Includes cases, care plans, milestones, tasks, appointments, and notifications. Data remains queryable for 90 days after case closure for operational review and dispute resolution. |
| **Observation data** | Active case lifetime + 90 days | Azure SQL (Observation Service), Cosmos DB (timeline) | Individual patient readings. Retained alongside case data for trend analysis and timeline completeness. |
| **Audit data** | 365 days | Azure SQL (Audit Service) | Immutable audit records. Longer retention reflects compliance and traceability requirements. Audit data is append-only and never modified or deleted during the retention window. |
| **FHIR data** | Indefinite | Azure Health Data Services | FHIR resources stored in the Azure FHIR service are retained indefinitely as the clinical data layer. FHIR versioning preserves historical states. |
| **Dead-letter messages** | 14 days | Azure Service Bus | Failed messages are retained in dead-letter queues for 14 days. Admin can inspect and replay within this window. After 14 days, messages expire per Service Bus TTL. |
| **Application logs** | 30 days | Azure Monitor / Log Analytics | Structured logs from all services. 30-day retention balances operational debugging needs with storage costs. |
| **Metrics** | 90 days (detailed), 365 days (aggregated) | Managed Prometheus / Azure Monitor | High-resolution metrics for 90 days. Downsampled aggregates for long-term trend analysis. |
| **Distributed traces** | 30 days | Application Insights | Individual request traces. 30-day retention is sufficient for incident investigation. |

### Retention implementation

- Retention policies are enforced at the storage layer (Azure SQL retention jobs, Cosmos DB TTL, Log Analytics workspace settings).
- No manual deletion processes. Expiration is automated.
- Audit data deletion (after 365 days) is logged as an audit event itself for traceability.
- FHIR data retention follows Azure Health Data Services default behavior (indefinite with soft delete).

---

## Requirements Traceability

The following table maps non-functional requirements back to PRD sections and architecture documentation.

| NFR Area | PRD Section | Architecture Reference |
|---|---|---|
| Performance | Section 17 (Non-Functional Requirements) | [Solution Architecture](../architecture/solution-architecture.md) — read model design, caching strategy |
| Scalability | Section 17 (Scalability targets) | [Solution Architecture](../architecture/solution-architecture.md) — autoscaling, Service Bus partitioning |
| Reliability | Section 17 (Reliability) | [Solution Architecture](../architecture/solution-architecture.md) — messaging patterns, circuit breakers |
| Availability | Section 17 (Availability) | [Solution Architecture](../architecture/solution-architecture.md) — AKS zone redundancy, rolling deployments |
| Security | Section 15 (Security Requirements) | [Security and Compliance](../architecture/security-and-compliance.md) |
| Observability | Section 17 (Observability) | [Observability](../architecture/observability.md) |
| Maintainability | Section 17 (Maintainability) | [Solution Architecture](../architecture/solution-architecture.md) — service boundaries, CI/CD |
| UX Performance | Section 18 (UX Requirements) | [Solution Architecture](../architecture/solution-architecture.md) — BFF pattern, read models |

---

## Cross-References

- [Product Requirements Document](prd.md) — Sections 15, 17, and 18
- [Solution Architecture](../architecture/solution-architecture.md) — infrastructure and service design supporting these requirements
- [Security and Compliance](../architecture/security-and-compliance.md) — detailed security architecture
- [Observability](../architecture/observability.md) — detailed observability architecture
- [Core Workflows](core-workflows.md) — workflows constrained by these requirements
- [Domain Overview](domain-overview.md) — domain concepts and boundaries

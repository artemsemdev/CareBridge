# ADR-001: Microservices Architecture

**Status:** Accepted
**Date:** 2026-03-27
**Deciders:** Solution architect

## Context

CareBridge is a post-discharge care coordination platform that spans multiple clinical and operational domains: patient management, care plan execution, provider notifications, appointment scheduling, and clinical document exchange via FHIR. The system must demonstrate production-grade patterns for cloud-native healthcare software deployed on Azure Kubernetes Service (see [ADR-002](./002-azure-kubernetes-service.md)).

The architectural question is whether to build CareBridge as a monolith, a modular monolith, or a set of microservices. This decision has downstream effects on deployment topology, team scalability, data ownership, and the breadth of cloud-native patterns the platform can demonstrate.

The platform uses synthetic data exclusively (see [ADR-010](./010-synthetic-data-only.md)), so we do not face regulatory pressure to minimize service surface area. Instead, we optimize for demonstrating realistic distributed system patterns that map to how large healthcare organizations actually build and operate these systems.

## Decision

CareBridge will be built as a set of domain-oriented microservices with event-driven integration via Azure Service Bus (see [ADR-004](./004-azure-service-bus.md)).

Each microservice owns its domain, its data store, and its public contract. Services communicate asynchronously through domain events published to Service Bus topics. Synchronous calls are limited to the BFF-to-service path (see [ADR-009](./009-react-bff-frontend.md)) and are never used for inter-service coordination.

The service boundaries are:

- **API Gateway / BFF** -- edge access for the React dashboard, auth context propagation, request aggregation, routing.
- **Case Service** -- post-discharge case lifecycle, patient operational summary, status tracking.
- **Care Plan Service** -- care plan templates, milestone execution, progression tracking.
- **Observation Ingestion Service** -- simulated vital sign intake, validation, deduplication, event publishing.
- **Care-Gap Engine** -- rules-based evaluation, abnormal reading detection, missed milestone detection, alert generation.
- **Task Service** -- operational task management, assignment, comments, due dates, closure.
- **Appointment Service** -- follow-up appointment scheduling and lifecycle tracking.
- **Notification Service** -- template-based multi-channel notification delivery with retry logic.
- **Audit Service** -- immutable append-only audit event store in Cosmos DB (see [ADR-006](./006-read-models-cosmos-db.md)).
- **Reporting / Read Model Service** -- denormalized dashboard and timeline views in Cosmos DB, fed by domain events.

Each service is deployed as an independent Helm release within a shared AKS namespace (`carebridge-app`), with its own Azure SQL database (see [ADR-005](./005-azure-sql-for-transactional-data.md)) and its own Workload Identity binding (see [ADR-007](./007-workload-identity.md)). The shared namespace is a portfolio trade-off -- enterprise deployments would use per-team namespaces for tighter isolation.

## Consequences

**Positive:**

- Independent deployment and scaling per service. The Notification Service can scale horizontally during discharge surges without affecting the Care Plan Service.
- Clear domain boundaries enforce data ownership. No shared databases, no backdoor joins across domains.
- Demonstrates real-world patterns: distributed tracing with OpenTelemetry (see [ADR-008](./008-opentelemetry-azure-monitor.md)), eventual consistency, dead-letter queue handling, circuit breakers, health checks, and Helm-based GitOps deployment.
- Each service can evolve its schema independently, enabling safe rolling upgrades.

**Negative:**

- Operational complexity is substantially higher than a monolith. Local development requires running multiple containers, managing service discovery, and coordinating schema migrations.
- Distributed transactions are not available. Business processes that span services must use saga patterns or compensating actions, adding design complexity.
- Observability is a first-class concern, not an afterthought. Without distributed tracing and structured logging, debugging cross-service failures becomes impractical.
- The initial development velocity is slower. Scaffolding ten services with their infrastructure is more work than a single deployable unit.

## Alternatives Considered

**Monolith:** A single ASP.NET Core application with domain modules would be significantly faster to build and simpler to deploy. However, it would not demonstrate Kubernetes multi-service orchestration, inter-service messaging, distributed tracing, or per-service scaling -- all of which are core to the platform's purpose as a reference implementation.

**Modular monolith:** A single deployable with strict module boundaries and in-process messaging is a legitimate production pattern. It would demonstrate domain separation and clean architecture. However, it would not exercise AKS namespace isolation, Helm multi-release management, Workload Identity per pod, or Service Bus integration -- patterns that are essential to the CareBridge technical narrative.

**Serverless (Azure Functions):** A function-per-operation model would demonstrate event-driven patterns but would obscure container orchestration skills, make local development harder to reason about, and poorly model long-running care plan workflows that benefit from persistent service instances.

# ADR-006: Cosmos DB for Read Models and Audit Store

**Status:** Accepted
**Date:** 2026-03-27
**Deciders:** Solution architect

## Context

CareBridge's microservices architecture (see [ADR-001](./001-microservices-architecture.md)) separates transactional writes from read-optimized queries. The frontend dashboard (see [ADR-009](./009-react-bff-frontend.md)) needs to display cross-domain views: a patient's discharge summary alongside their active care plan, upcoming appointments, and notification history. These views span multiple services, each with its own Azure SQL database (see [ADR-005](./005-azure-sql-for-transactional-data.md)).

Building these views by querying multiple services synchronously at request time would be slow, fragile, and couple the frontend to the availability of every backend service. The platform needs denormalized read models that pre-aggregate data from multiple domains into query-optimized shapes.

Additionally, CareBridge requires an immutable audit trail of all clinically significant events: who did what, when, and what the state was at that point. This audit log must be append-only, queryable by patient and time range, and retained indefinitely. A relational database could serve this purpose, but the schema-flexible, append-heavy, partition-key-oriented nature of audit data aligns better with a document store.

## Decision

CareBridge will use Azure Cosmos DB (NoSQL API) for two distinct workloads:

### Denormalized Read Models

Event-driven projections consume domain events from Service Bus (see [ADR-004](./004-azure-service-bus.md)) and maintain denormalized documents in Cosmos DB. Key read model containers include:

- **PatientDashboard** -- partitioned by `patientId`, contains the patient's demographics, active care plan summary, next appointment, and recent notification status. The BFF queries this single document to render the patient overview, eliminating the need to fan out to four services.
- **ProviderWorklist** -- partitioned by `providerId`, contains the provider's assigned patients, pending tasks, and overdue items. This powers the provider's primary work screen.
- **DischargeAnalytics** -- partitioned by `dischargeMonth`, contains aggregated discharge metrics for the analytics dashboard.

Each read model is maintained by a dedicated projection handler that subscribes to the relevant Service Bus topics. Projections are idempotent -- replaying events produces the same document state.

### Immutable Audit Store

The Audit Service writes an immutable event record for every domain event it receives. Each audit document contains the event type, timestamp, actor identity, affected entity, and a snapshot of the relevant state at that point in time. The audit container is partitioned by `patientId` and uses a composite key of `patientId` + `timestamp` for efficient range queries.

Both workloads use the **Cosmos DB serverless tier** to minimize cost for a portfolio project with intermittent, low-volume traffic. Serverless billing is per-RU-consumed, with no idle cost.

Authentication uses Workload Identity (see [ADR-007](./007-workload-identity.md)) with the Cosmos DB Built-in Data Contributor role assigned to the relevant service identities.

## Consequences

**Positive:**

- Single-document reads by partition key are consistently fast (single-digit millisecond latency) regardless of data volume. The PatientDashboard document is retrieved in one read operation, compared to multiple SQL queries across multiple services.
- Schema flexibility accommodates evolving read model shapes without migrations. Adding a new field to the dashboard projection requires only a code change in the projection handler, not a database schema migration.
- Serverless tier aligns with the cost profile of a portfolio project. No provisioned throughput means no cost when the system is idle.
- The append-only audit store maps naturally to Cosmos DB's document model. Each event is a self-contained document with no relational joins needed for audit queries.
- Cosmos DB's time-to-live (TTL) can be configured per container. Read models can have TTL for stale data cleanup, while the audit container retains documents indefinitely.

**Negative:**

- Read models are eventually consistent with the transactional services. A care plan update in Azure SQL is visible in the Cosmos DB dashboard projection only after the domain event propagates through Service Bus and the projection handler processes it. Typical latency is under one second, but callers must design for this.
- Maintaining projection handlers adds development and testing overhead. Each read model requires a handler that correctly processes events, handles out-of-order delivery, and is idempotent on replay.
- Cosmos DB's query capabilities are limited compared to SQL. Complex ad-hoc queries, joins, and aggregations that would be straightforward in SQL require careful data modeling or are simply not feasible. Read models must be designed for known query patterns.
- Cross-partition queries (e.g., searching all patients by name across the PatientDashboard container) are expensive and slow. The read model design must ensure that primary access patterns align with partition keys.

## Alternatives Considered

**SQL views or a dedicated SQL read database:** Creating read models as SQL views or materialized tables in a separate Azure SQL database would leverage familiar tooling and powerful query capabilities. This approach works well and is used in many production systems. However, it adds another SQL database to manage (schema migrations, connection strings, Elastic Pool sizing), and the rigid schema makes evolving read model shapes more cumbersome than Cosmos DB's schema-flexible documents. For CareBridge, the document model better represents the pre-aggregated, query-specific nature of read models.

**Redis for read models:** Redis provides sub-millisecond reads and is commonly used as a caching layer. However, Redis is volatile by default -- data is lost on restart unless persistence is configured, and even with persistence, it is not a durable store. The audit log absolutely requires durability, ruling out Redis for that workload. For read models, Redis could work as a cache in front of Cosmos DB, but the added layer is not justified given Cosmos DB's already-low latency for partition-key reads.

**Azure Table Storage:** Table Storage provides a low-cost key-value store with partition-key-based access. It could serve read models. However, its query capabilities are more limited than Cosmos DB, it lacks serverless billing, and its SDK and developer experience are dated compared to the Cosmos DB SDK. The cost savings do not justify the ergonomic regression.

# ADR-005: Azure SQL for Transactional Service Databases

**Status:** Accepted
**Date:** 2026-03-27
**Deciders:** Solution architect

## Context

Each microservice in CareBridge (see [ADR-001](./001-microservices-architecture.md)) owns its data and requires a transactional store for operational state. The Patient Service manages demographics and discharge records. The Care Plan Service tracks plan instances, task assignments, and state transitions. The Scheduling Service maintains appointment slots and bookings. The Notification Service tracks delivery status and retry state.

All of these workloads are relational in nature: they involve structured schemas, referential integrity within a service boundary, transactional writes (often multi-row), and query patterns that benefit from SQL joins and indexes. The question is which managed relational database to use.

The platform is built on .NET 10 with ASP.NET Core and uses Entity Framework Core as the data access layer. EF Core's provider ecosystem and migration tooling vary in maturity across database engines. The choice of relational database should align with the .NET ecosystem while remaining a defensible production choice.

## Decision

CareBridge will use Azure SQL Database as the transactional data store for all microservices that require relational persistence.

The deployment model uses an **Elastic Pool** containing one database per microservice. This approach provides:

- **Per-service database isolation** consistent with the microservices data ownership principle. No cross-service joins, no shared schemas, no coupling through the database.
- **Elastic Pool resource sharing** to manage cost. In a portfolio project with synthetic workloads, individual databases would be underutilized. The Elastic Pool allows services to burst into shared eDTU/vCore capacity when needed (e.g., during synthetic data seeding) without provisioning dedicated resources for each database.
- **EF Core migrations per service** managed independently. Each service's CI/CD pipeline runs its own migrations against its own database, with no cross-service migration dependencies.
- **Azure SQL's built-in features**: automatic tuning, intelligent query processing, point-in-time restore, transparent data encryption at rest, and audit logging.

Authentication to Azure SQL uses Workload Identity (see [ADR-007](./007-workload-identity.md)) with Entra ID authentication. Each service's managed identity is granted the `db_datareader` and `db_datawriter` roles on its own database only. No service has access to another service's database.

Schema design within each service follows standard EF Core conventions: entity classes mapped to tables, value objects as owned types, and domain events dispatched through an outbox pattern backed by a transactional outbox table in the same database. The outbox ensures that domain events are published to Service Bus (see [ADR-004](./004-azure-service-bus.md)) reliably -- the event record is written in the same transaction as the domain state change.

## Consequences

**Positive:**

- EF Core's SQL Server provider is the most mature and feature-complete provider in the ecosystem. Features like temporal tables, JSON columns, hierarchical queries, and compiled models work reliably and are well-documented.
- Azure SQL Elastic Pool keeps costs predictable for a multi-database portfolio project. Databases that are idle consume minimal shared resources.
- Entra ID authentication via Workload Identity eliminates connection string secrets entirely. The managed identity authenticates directly to Azure SQL with no passwords to rotate.
- Point-in-time restore provides a safety net for synthetic data seeding errors without requiring manual backup management.
- The transactional outbox pattern, implemented within each Azure SQL database, guarantees exactly-once event publishing without distributed transactions.

**Negative:**

- Azure SQL licensing costs are higher than PostgreSQL Flexible Server for equivalent compute. The Elastic Pool mitigates this for development workloads, but production-scale deployments would need to evaluate cost carefully.
- Azure SQL is a Microsoft-proprietary database engine. While EF Core abstracts most differences, some SQL Server-specific features (temporal tables, specific JSON syntax) create coupling that would require migration effort if the platform moved to PostgreSQL.
- The Elastic Pool model means that a resource-intensive operation on one database (e.g., bulk synthetic data seeding in the Patient Service) can temporarily affect performance for other databases in the pool. This is acceptable for a reference implementation but would require monitoring in production.

## Alternatives Considered

**Azure Database for PostgreSQL Flexible Server:** PostgreSQL is a strong open-source relational database with excellent Azure managed support. EF Core's Npgsql provider is mature and actively maintained. PostgreSQL would be a defensible choice, and many healthcare organizations use it. However, EF Core's SQL Server provider has deeper feature coverage (compiled models, temporal table mapping, spatial types), and the .NET ecosystem's tooling, documentation, and community examples skew heavily toward SQL Server. For a reference implementation targeting .NET architects, Azure SQL better represents the ecosystem's conventions.

**Cosmos DB for everything:** Cosmos DB's document model (see [ADR-006](./006-read-models-cosmos-db.md)) could theoretically store all data. However, the operational data in CareBridge is fundamentally relational: care plans have tasks, tasks have assignments, patients have encounters, encounters have conditions. Modeling these relationships in a document store requires denormalization that complicates writes, makes multi-entity transactional updates difficult, and sacrifices the query flexibility that SQL provides. Cosmos DB is the right choice for read models and audit logs, not for transactional operational data.

**Azure Database for MySQL Flexible Server:** MySQL on Azure is a mature managed service, but EF Core's MySQL provider (Pomelo.EntityFrameworkCore.MySql) is community-maintained rather than backed by a major vendor. MySQL also lacks some features that Azure SQL provides natively, such as temporal tables and advanced JSON support. The provider maturity gap makes it a riskier choice for a reference implementation.

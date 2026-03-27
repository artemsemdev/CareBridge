# Coding Boundaries

**Document type:** Development guidelines
**Last updated:** 2026-03-27

These rules exist to keep service boundaries clean, prevent accidental coupling, and maintain independent deployability. Violating them creates hidden dependencies that break when services evolve independently.

---

## Service Boundary Rules

### 1. A service MUST NOT directly access another service's database
No cross-service SQL queries, no shared connection strings, no "just reading from their table." If you need data from another service, consume its events or call its API.

### 2. A service MUST NOT import code from another service's Domain or Infrastructure layers
Each service's domain entities, DbContext, and repositories are private to that service. Never reference `CaseService.Domain` from `TaskService`.

### 3. Cross-service data needs MUST be satisfied via events or API calls
If the Task Service needs the patient name for a task title, it gets it from the `AlertRaised` event payload (which includes case context), not by querying the Case Service database.

### 4. Shared code MUST live in src/shared/ and be limited to infrastructure concerns
Shared code is for cross-cutting infrastructure only — not business logic.

### 5. Each service MUST own its own EF Core DbContext and migrations
No shared DbContext. No shared migration history. Each service's database schema evolves independently.

---

## What Goes in src/shared/

**Allowed:**
- Event envelope base types (message ID, correlation ID, timestamp)
- Correlation ID middleware (HTTP header propagation)
- Health check base classes and registration helpers
- OpenTelemetry configuration helpers
- Common error response types (RFC 7807 Problem Details)
- Logging configuration helpers

**NOT allowed:**
- Business logic
- Domain entities
- Service-specific DTOs
- Anything with a dependency on a specific service's data model

---

## API Contract Rules

1. **Public API contracts** are defined in each service's `Contracts/` directory.
2. **API changes must be backward-compatible** within a version. Adding optional fields is fine. Removing or renaming fields is breaking.
3. **Breaking changes require a new API version** (`/api/v2/...`).
4. **Event schema changes must be additive.** New optional fields only. No removals within a version.
5. **Contract tests** validate that published schemas match consumer expectations. Run in CI on every PR.

---

## Testing Boundaries

| Test Type | Scope | Dependencies |
|---|---|---|
| **Unit tests** | Within service boundary only | Mock all external dependencies (DB, messaging, other services) |
| **Integration tests** | Service + its own database | Real database via test containers. Mock other services and messaging. |
| **Contract tests** | Schema validation | Validate event/API schemas without running services |
| **End-to-end tests** | Full workflow across services | All services running. Limited count — expensive and slow. |

---

## Common Boundary Violations to Avoid

| Violation | Why It's Bad | What to Do Instead |
|---|---|---|
| Calling another service's internal endpoint | Couples to implementation details that may change | Use only documented public API endpoints |
| Copying entity classes between services | Creates hidden schema coupling | Define separate DTOs per service. Map at the boundary. |
| Using the same database connection string for multiple services | Shared DB = shared schema evolution = deployment coupling | Each service gets its own database and credentials |
| Publishing internal domain events on the public event bus | Exposes implementation details to all consumers | Define explicit public event contracts. Internal events stay internal. |
| Importing shared business logic from src/shared/ | Creates a "distributed monolith" — all services coupled to shared logic | Duplicate simple logic rather than sharing it. Only share infrastructure. |
| Cross-service database migration dependencies | Service A can't deploy until Service B migrates | Each service's migrations are independent. Never depend on another service's schema. |

---

## Code Review Checklist for Boundary Compliance

When reviewing a PR, verify:

- [ ] No new project references to another service's Domain or Infrastructure
- [ ] No new database connection strings pointing to another service's database
- [ ] No direct HTTP calls to undocumented internal endpoints
- [ ] Shared code additions in `src/shared/` are infrastructure-only, not business logic
- [ ] New events are defined in the publishing service's `Contracts/` directory
- [ ] Event consumers handle unknown fields gracefully (don't fail on new optional fields)
- [ ] Database migrations are independent (no cross-service schema dependencies)
- [ ] New APIs follow the versioning and error response conventions in [API and Event Contracts](../architecture/api-and-event-contracts.md)

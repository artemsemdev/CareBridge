# ADR-009: React TypeScript SPA with ASP.NET Core BFF

**Status:** Accepted
**Date:** 2026-03-27
**Deciders:** Solution architect

## Context

CareBridge needs a web frontend for care coordinators and clinical staff to manage post-discharge workflows: reviewing patient dashboards, updating care plans, acknowledging notifications, and viewing FHIR-based clinical summaries. The frontend must be responsive, interactive, and capable of rendering complex clinical data views.

The architecture question has two dimensions: the frontend framework (React, Blazor, Angular, or a meta-framework like Next.js) and the API integration pattern (direct SPA-to-microservice calls, API gateway, or Backend-for-Frontend).

The backend is a set of microservices deployed on AKS (see [ADR-001](./001-microservices-architecture.md), [ADR-002](./002-azure-kubernetes-service.md)). The frontend should not need to know about individual service boundaries, authentication tokens for each service, or the internal topic structure. It needs a cohesive API surface.

## Decision

CareBridge will use a **React TypeScript single-page application** with an **ASP.NET Core Backend-for-Frontend (BFF)** service.

### Frontend: React + TypeScript

- **React 18+** with TypeScript for the SPA. Component library based on a healthcare-appropriate design system with accessibility compliance (WCAG 2.1 AA).
- **React Router** for client-side routing. Key views: patient list, patient dashboard, care plan editor, provider worklist, notification center, analytics dashboard.
- **TanStack Query (React Query)** for server state management -- caching, background refetching, optimistic updates. All data fetching goes through the BFF, never directly to backend microservices.
- **Vite** for build tooling. The production build outputs static assets that the BFF serves directly.

### BFF: ASP.NET Core

The BFF is a dedicated ASP.NET Core service deployed on AKS alongside the other microservices. It serves three roles:

1. **API aggregation:** The patient dashboard view requires data from the Patient Service, Care Plan Service, Scheduling Service, and Notification Service. The BFF orchestrates these calls (or reads from the Cosmos DB read model -- see [ADR-006](./006-read-models-cosmos-db.md)) and returns a single aggregated response. The frontend makes one call; the BFF handles fan-out.

2. **Authentication context:** The BFF manages the user's authentication session. It handles the OAuth 2.0 / OpenID Connect flow with Entra ID, stores tokens server-side (in an encrypted cookie or session store), and attaches the appropriate bearer token when calling downstream services. The SPA never handles or stores access tokens directly, eliminating an entire class of token-related frontend security concerns.

3. **Static asset serving:** The BFF serves the React SPA's production build (HTML, JS, CSS) from its own static file middleware. This eliminates the need for a separate CDN or static hosting configuration, simplifies deployment (one Helm release for frontend + BFF), and ensures that the SPA and its API are always version-consistent.

The BFF authenticates to downstream services using Workload Identity (see [ADR-007](./007-workload-identity.md)) for service-to-service calls, and forwards the user's identity context as a claim in the service call headers for authorization decisions.

### API Contract

The BFF exposes a RESTful API designed for the frontend's specific view models, not a generic CRUD API mirroring backend entities. Endpoints are organized by frontend concern:

- `GET /api/patients/{id}/dashboard` -- aggregated patient overview
- `GET /api/providers/{id}/worklist` -- provider task list
- `POST /api/careplans/{id}/tasks/{taskId}/complete` -- task state transition
- `GET /api/analytics/discharges` -- discharge metrics for dashboard charts

## Consequences

**Positive:**

- The BFF pattern provides a clean security boundary. Access tokens never reach the browser. CSRF protection is handled server-side. The attack surface for token theft is substantially reduced compared to SPA-managed tokens.
- API aggregation reduces frontend complexity and network round trips. The patient dashboard requires one HTTP call to the BFF instead of four parallel calls to different microservices.
- The BFF can implement response shaping, pagination, and caching logic that would be duplicated or absent in direct SPA-to-service calls.
- React with TypeScript is the dominant frontend stack in the market. Using it demonstrates relevant skills without introducing exotic tooling. The ecosystem of healthcare-oriented component libraries and accessibility tools is mature.
- Single Helm release for the frontend + BFF simplifies deployment and ensures version consistency between the SPA bundle and the API it calls.

**Negative:**

- The BFF is an additional service to build, deploy, and maintain. It has its own AKS deployment, health checks, scaling configuration, and observability instrumentation.
- The BFF can become a bottleneck if it performs synchronous fan-out to multiple services. Careful use of Cosmos DB read models for aggregated views (see [ADR-006](./006-read-models-cosmos-db.md)) mitigates this, but some write operations still require BFF-to-service calls.
- The BFF's view-model-oriented API is tightly coupled to the frontend's current needs. If a mobile application were added, it would likely need its own BFF or a shared API gateway, since mobile view models differ from web view models.
- Developers must work across both the React TypeScript codebase and the ASP.NET Core BFF. This requires familiarity with two ecosystems, though the boundary between them is well-defined.

## Alternatives Considered

**Blazor Server:** Blazor Server renders UI on the server and sends DOM diffs to the browser via SignalR. This keeps the entire stack in .NET, eliminating the React/TypeScript layer. However, Blazor Server couples the UI rendering to the server runtime -- every user interaction requires a server round trip via WebSocket. For a care coordination dashboard with interactive data tables, drag-and-drop care plan editing, and real-time notification badges, the latency and server resource consumption of Blazor Server are suboptimal. Blazor WebAssembly is an alternative but has a large initial download, limited library ecosystem compared to React, and is still maturing for complex LOB applications.

**Direct SPA-to-microservice calls:** The React SPA could call each backend microservice directly, using an API gateway (e.g., Azure API Management or NGINX ingress routes) for routing. This eliminates the BFF service but introduces several problems: the SPA must manage tokens for multiple downstream services, CORS must be configured for each service, the frontend must implement its own aggregation logic for cross-service views, and internal service boundaries are exposed to the client. The security and complexity costs outweigh the operational simplicity of removing the BFF.

**Next.js:** Next.js provides server-side rendering, API routes, and a React-based component model. Its API routes could serve as a BFF equivalent. However, Next.js requires a Node.js runtime, introducing a second server-side runtime alongside .NET. The team's backend expertise is in .NET, and the ASP.NET Core BFF provides equivalent functionality (API aggregation, auth handling, static serving) without adding Node.js to the operational stack. Next.js would be a strong choice for a team with full-stack JavaScript expertise.

# Epic 7: Infrastructure and Cloud Deployment

**Epic ID:** `epic:cloud-deployment`
**Wave:** 6–8 (Hardening → Cloud → Demo)
**Milestone:** v0.6 — Hardened, v0.7 — Cloud-Deployed, v1.0 — Demo-Ready
**Estimated effort:** ~3–4 weeks total
**Issues:** 13

---

## Why This Epic Exists

The portfolio story includes Azure cloud architecture, AKS, Terraform, CI/CD, and operational maturity. The system must run in the cloud — not just on localhost. This epic also includes hardening (observability, resilience), containerization, and the synthetic data generator that makes the demo compelling.

## Value Delivered

- All services containerized with production-optimized Docker images
- Helm charts enable repeatable Kubernetes deployments
- Terraform provisions the full Azure infrastructure
- GitHub Actions automates build, test, and deployment
- OpenTelemetry provides distributed tracing and structured logging
- Synthetic data generator creates a realistic demo dataset
- A documented walkthrough proves the system works end-to-end

## Dependencies

- **Epics 1–6** — all services must be built before containerization and deployment
- Hardening issues (H-01 through H-05) can start as soon as Wave 5 services stabilize
- Terraform (I7-03, I7-04) can start in parallel with other work

## Out of Scope

- Multi-environment promotion (staging, production) — dev only for v1.0
- Production-grade KEDA scaling (HPA is sufficient for demo)
- Workload Identity for all services (prove with one or two, document the pattern)
- FHIR service provisioning (deferred to v2.0)
- Full Grafana dashboard configuration (basic Azure Monitor is sufficient)
- Blue/green or canary deployment strategy (rolling update is sufficient)

## Exit Criteria

v0.6 (Hardened):
- [ ] All services have /startup, /ready, /healthz endpoints
- [ ] Correlation IDs propagate through HTTP and events end-to-end
- [ ] Structured JSON logging is consistent across all services
- [ ] OpenTelemetry traces are emitted
- [ ] Circuit breakers protect inter-service HTTP calls

v0.7 (Cloud-Deployed):
- [ ] Terraform provisions AKS, Azure SQL, Cosmos DB, Service Bus, ACR
- [ ] GitHub Actions builds, tests, and pushes images
- [ ] Helm deploys all services to dev AKS cluster
- [ ] Application runs on Azure with managed services

v1.0 (Demo-Ready):
- [ ] Synthetic data generator populates a compelling demo dataset
- [ ] Demo walkthrough is documented and completable in under 15 minutes
- [ ] README reflects actual built state

---

## Delivery Slices

### Wave 6: Hardening

| # | Slice | What it delivers | Issues |
|---|-------|-----------------|--------|
| H1 | Health endpoints | /startup, /ready, /healthz on all services | H-01 |
| H2 | Structured logging | Consistent JSON log format across all services | H-02 |
| H3 | OpenTelemetry | Distributed tracing integration | H-03 |
| H4 | Resilience | Circuit breakers for HTTP calls | H-04 |
| H5 | Error handling | Edge case review + hardening | H-05 |

### Wave 7: Cloud Deployment

| # | Slice | What it delivers | Issues |
|---|-------|-----------------|--------|
| I1 | Dockerfiles | Multi-stage Docker images for all services | I7-01 |
| I2 | Helm charts | Kubernetes deployment manifests | I7-02 |
| I3 | Terraform core | AKS, ACR, networking | I7-03 |
| I4 | Terraform data | SQL, Cosmos DB, Service Bus, Key Vault | I7-04 |
| I5 | CI pipeline | Build, test, scan on PR | I7-05 |
| I6 | CD pipeline | Deploy to AKS on merge | I7-06 |

### Wave 8: Demo-Ready

| # | Slice | What it delivers | Issues |
|---|-------|-----------------|--------|
| I7 | Data generator | Synthetic data seeding tool | I7-07 |
| I8 | Demo walkthrough | Documented end-to-end demo script | I7-08 |

---

## Execution Order

```
Wave 6 (can be parallelized):
H-01, H-02, H-03, H-04, H-05

Wave 7:
I7-01 → I7-02
I7-03 → I7-04
I7-05 (after code exists)
I7-06 (after I7-01, I7-02, I7-03, I7-04)

Wave 8:
I7-07 → I7-08
```

- All hardening issues are independent — work them in any order
- I7-03 and I7-04 (Terraform) can start early, even during Wave 5
- I7-05 (CI) only needs service code and tests
- I7-06 (CD) depends on everything: Dockerfiles, Helm, Terraform, CI

---

## Issues

### H-01: Add health endpoints to all services

**Labels:** `epic:cloud-deployment`, `type:hardening`, `priority:high`
**Branch:** `feature/H-01-health-endpoints`

**Description:**
Ensure every service has standardized health check endpoints for Kubernetes probes.

**Scope:**

Every service already has `MapCareBridgeHealthChecks()` from the shared middleware (F1-03). This issue ensures:

1. All 10 services + BFF call `MapCareBridgeHealthChecks()` in their startup
2. `/healthz` (liveness) — always returns 200 if the process is running
3. `/ready` (readiness) — checks critical dependencies:
   - Services with SQL: verify DB connection
   - Services consuming events: verify RabbitMQ connection
   - BFF: returns 200 (stateless, no dependency check needed)
   - Reporting/Audit: returns 200 (in-memory store always available)
4. `/startup` — returns 503 during initialization, 200 after startup completes

Health check registrations per service:

| Service | Readiness Checks |
|---------|-----------------|
| Case Service | SQL connection |
| Care Plan Service | SQL connection, RabbitMQ |
| Observation Service | SQL connection |
| Care-Gap Engine | SQL connection, RabbitMQ |
| Task Service | SQL connection, RabbitMQ |
| Appointment Service | SQL connection |
| Notification Service | RabbitMQ |
| Audit Service | RabbitMQ |
| Reporting Service | RabbitMQ |
| BFF / Gateway | None (stateless) |

**Acceptance criteria:**
- [ ] All services respond to `/healthz`, `/ready`, `/startup`
- [ ] Readiness probe fails if a critical dependency is unavailable
- [ ] Liveness probe always returns 200 if the process is running
- [ ] Startup probe returns 503 during initialization, 200 after
- [ ] Health endpoints do not require authentication

**Dependencies:** All services built (Epics 1-6)

---

### H-02: Standardize structured JSON logging across all services

**Labels:** `epic:cloud-deployment`, `type:hardening`, `priority:high`
**Branch:** `feature/H-02-structured-logging`

**Description:**
Ensure every service outputs structured JSON logs with consistent fields.

**Scope:**

Log format (every log entry):
```json
{
  "timestamp": "2026-03-28T14:30:00.000Z",
  "level": "Information",
  "message": "Case created",
  "correlationId": "abc-123",
  "service": "CaseService",
  "properties": { ... }
}
```

Across all services:
- Verify `AddCareBridgeDefaults()` is called and logging is configured
- Ensure correlation ID appears in every log entry
- Ensure service name appears in every log entry
- Set log levels: `Default: Information`, `Microsoft.AspNetCore: Warning`, `Microsoft.EntityFrameworkCore: Warning`
- Verify no PII appears in logs (patient names, etc.) — log patient IDs, not names
- Add request/response logging at Debug level (method, path, status code, duration)

**Acceptance criteria:**
- [ ] All services output structured JSON logs
- [ ] Every log entry includes correlationId, service name, and timestamp
- [ ] No PII in log output (verify by searching for patient name patterns)
- [ ] Request logging shows method, path, status code, and duration at Debug level
- [ ] Log levels are consistent across services

**Dependencies:** All services built

---

### H-03: Integrate OpenTelemetry distributed tracing

**Labels:** `epic:cloud-deployment`, `type:hardening`, `priority:medium`
**Branch:** `feature/H-03-opentelemetry`

**Description:**
Add OpenTelemetry SDK for distributed tracing across all services. For local dev, export to console. In cloud, this will export to Application Insights.

**Scope:**

NuGet packages (added to shared infrastructure or each service):
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Instrumentation.EntityFrameworkCore` (optional, for DB traces)
- `OpenTelemetry.Exporter.Console` (local dev)
- `Azure.Monitor.OpenTelemetry.Exporter` (cloud, added later)

Configuration in `AddCareBridgeDefaults()`:
- Add tracing for: ASP.NET Core, HttpClient, EF Core
- Service name from configuration or assembly name
- Console exporter for local development
- W3C TraceContext propagation (default)

Verify:
- HTTP calls from BFF to backend services carry trace context headers
- Events published to RabbitMQ include trace context (via message headers)
- Events consumed from RabbitMQ restore trace context

**Acceptance criteria:**
- [ ] Console output shows trace spans for HTTP requests
- [ ] BFF → backend service calls show parent-child span relationship
- [ ] Trace context propagates through RabbitMQ events (publish → consume)
- [ ] EF Core queries appear as spans (if enabled)
- [ ] Service name appears in each span
- [ ] OpenTelemetry can be disabled via configuration flag

**Dependencies:** All services built

---

### H-04: Add circuit breakers for inter-service HTTP calls

**Labels:** `epic:cloud-deployment`, `type:hardening`, `priority:medium`
**Branch:** `feature/H-04-circuit-breakers`

**Description:**
Protect inter-service HTTP calls with circuit breakers to prevent cascade failures.

**Scope:**

Use `Microsoft.Extensions.Http.Resilience` (built-in .NET resilience) or Polly:

For each named HttpClient in the BFF and Care-Gap Engine:
- Retry: 2 retries with exponential backoff (200ms, 400ms)
- Circuit breaker: open after 5 consecutive failures, half-open after 30 seconds
- Timeout: 10 seconds per request

When circuit is open:
- Return 503 with Problem Details: "Service temporarily unavailable"
- Log at Warning level with service name

Configuration:
- Retry count, circuit breaker thresholds, and timeout configurable via appsettings
- Default values suitable for local development

**Acceptance criteria:**
- [ ] HTTP calls to backend services use retry + circuit breaker policies
- [ ] After 5 consecutive failures, circuit opens and requests fail fast (no HTTP call made)
- [ ] Circuit half-opens after 30 seconds and allows a test request
- [ ] Open circuit returns 503 with descriptive error
- [ ] Resilience parameters configurable via appsettings
- [ ] Normal operation is unaffected (no performance overhead in happy path)

**Dependencies:** C2-07 (BFF exists)

---

### H-05: Error handling review and edge case hardening

**Labels:** `epic:cloud-deployment`, `type:hardening`, `priority:medium`
**Branch:** `feature/H-05-error-handling`

**Description:**
Review all services for error handling gaps and edge cases. Fix any issues found.

**Scope:**

Systematic review across all services:

1. **Event handler failures:**
   - Verify all event handlers have try-catch with logging
   - Failed handlers should nack the message (triggering retry), not crash the service
   - After max retries, message goes to dead-letter queue

2. **Database connection failures:**
   - Verify EF Core retries are enabled (`EnableRetryOnFailure`)
   - Temporary SQL errors should retry, not crash

3. **Validation edge cases:**
   - Empty strings that pass "required" checks
   - Unicode characters in patient names
   - Extremely long input strings
   - Negative values for observations
   - Future timestamps

4. **Concurrent operations:**
   - Two tasks created from the same alert (race condition on idempotency check)
   - Case status updated concurrently (optimistic concurrency via EF Core row version)

5. **Missing references:**
   - Observation submitted for a non-existent case ID
   - Task created for a non-existent case ID
   - Alert referencing a non-existent observation

**Acceptance criteria:**
- [ ] No unhandled exceptions in event handlers (all have try-catch with logging)
- [ ] EF Core retry on transient failure is enabled for all DbContexts
- [ ] Idempotency checks use database constraints (not just code checks) to handle race conditions
- [ ] Foreign key-like validations return appropriate errors (not 500)
- [ ] All edge cases identified have either a fix or a documented decision to accept the behavior

**Dependencies:** All services built

---

### I7-01: Create multi-stage Dockerfiles for all services

**Labels:** `epic:cloud-deployment`, `type:infrastructure`, `priority:high`
**Branch:** `feature/I7-01-dockerfiles`

**Description:**
Each .NET service and the React frontend gets a production-optimized multi-stage Dockerfile.

**Scope:**

.NET service Dockerfile template (one per service):
```dockerfile
# Stage 1: Restore
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY *.sln .
COPY src/shared/CareBridge.Shared.Contracts/*.csproj src/shared/CareBridge.Shared.Contracts/
COPY src/shared/CareBridge.Shared.Infrastructure/*.csproj src/shared/CareBridge.Shared.Infrastructure/
COPY src/services/{service}/*.csproj src/services/{service}/
RUN dotnet restore src/services/{service}/

# Stage 2: Build + Publish
FROM restore AS publish
COPY src/shared/ src/shared/
COPY src/services/{service}/ src/services/{service}/
RUN dotnet publish src/services/{service}/ -c Release -o /app --no-restore

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CareBridge.{Service}.dll"]
```

React frontend Dockerfile:
```dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY src/web/package*.json .
RUN npm ci
COPY src/web/ .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY deploy/nginx/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

Supporting files:
- `.dockerignore` at repo root (exclude `.git`, `node_modules`, `bin`, `obj`, docs, etc.)
- `deploy/nginx/nginx.conf` for frontend SPA routing
- `docker-compose.services.yml` for building and running all services as containers locally (separate from the infrastructure docker-compose)

**Acceptance criteria:**
- [ ] Every .NET service builds as a Docker image without errors
- [ ] Runtime images use the slim `aspnet:10.0` base (not the SDK image)
- [ ] React app builds and serves correctly from nginx
- [ ] Images build in under 3 minutes each (with layer caching)
- [ ] No source code or SDK tools in runtime images
- [ ] `.dockerignore` excludes non-essential files
- [ ] `docker-compose.services.yml` builds and runs all service containers

**Dependencies:** All service code complete (Epics 1-6)

---

### I7-02: Create Helm charts for all services

**Labels:** `epic:cloud-deployment`, `type:infrastructure`, `priority:high`
**Branch:** `feature/I7-02-helm-charts`

**Description:**
Helm chart per service with deployment, service, configmap, health probes, and resource limits.

**Scope:**

Chart structure: `deploy/helm/{service-name}/`
```
deploy/helm/case-service/
├── Chart.yaml
├── values.yaml
├── values.dev.yaml
├── templates/
│   ├── deployment.yaml
│   ├── service.yaml
│   ├── configmap.yaml
│   ├── serviceaccount.yaml
│   └── _helpers.tpl
```

Common chart patterns (use a shared library chart or copy):
- Deployment with:
  - Container image from ACR (image tag from values)
  - Startup probe → `/startup` (failureThreshold: 30, periodSeconds: 2)
  - Readiness probe → `/ready` (periodSeconds: 10)
  - Liveness probe → `/healthz` (periodSeconds: 15)
  - Resource requests: CPU 100m, Memory 128Mi
  - Resource limits: CPU 500m, Memory 512Mi
  - Environment variables from ConfigMap
  - Service account with Workload Identity annotations (placeholder)
- Service: ClusterIP on port 80 targeting container port 8080
- ConfigMap: connection strings, service URLs, feature flags

BFF-specific additions:
- Ingress resource with NGINX class
- TLS termination (placeholder for cert-manager)

Values files:
- `values.yaml` — defaults (image tag, replica count, resource limits)
- `values.dev.yaml` — dev overrides (single replica, dev connection strings)

**Acceptance criteria:**
- [ ] `helm lint deploy/helm/{service}` passes for all charts
- [ ] `helm template deploy/helm/{service}` renders valid Kubernetes manifests
- [ ] Every chart includes startup, readiness, and liveness probes
- [ ] Resource requests and limits defined for all deployments
- [ ] BFF chart includes ingress resource
- [ ] ConfigMap contains all required environment variables
- [ ] Values files support dev environment overrides

**Dependencies:** I7-01

---

### I7-03: Create Terraform configuration for core Azure resources

**Labels:** `epic:cloud-deployment`, `type:infrastructure`, `priority:high`
**Branch:** `feature/I7-03-terraform-core`

**Description:**
Terraform modules for foundational Azure resources: resource group, networking, AKS, and ACR.

**Scope:**

Directory: `infra/terraform/`

Modules:
- `main.tf` — provider configuration, backend
- `variables.tf` — input variables with dev defaults
- `outputs.tf` — AKS credentials, ACR login server, resource group name
- `resource-group.tf` — `carebridge-dev-rg`
- `networking.tf` — VNet `carebridge-dev-vnet` with subnets for AKS and services
- `aks.tf`:
  - AKS cluster `carebridge-dev-aks`
  - System node pool: 1 node, Standard_D2s_v3
  - User node pool: 1-3 nodes, Standard_D2s_v3, auto-scale
  - OIDC issuer enabled (for Workload Identity)
  - Managed identity
  - NGINX ingress controller add-on or manual Helm install
- `acr.tf`:
  - Azure Container Registry `carebridgedevacr`
  - Basic SKU (cheapest)
  - AKS role assignment: AcrPull on the ACR

Backend:
- Azure Storage account for Terraform state (created manually or via bootstrap script)
- State file per environment

Variables with dev defaults:
- `location = "eastus"` (or preferred region)
- `environment = "dev"`
- `aks_node_size = "Standard_D2s_v3"`
- `aks_min_nodes = 1`, `aks_max_nodes = 3`

**Acceptance criteria:**
- [ ] `terraform init` succeeds
- [ ] `terraform plan` shows expected resources without errors
- [ ] AKS cluster configuration includes OIDC issuer and managed identity
- [ ] ACR is created with AcrPull role assigned to AKS
- [ ] Network configuration places AKS in a dedicated subnet
- [ ] Variables have sensible dev-tier defaults (minimizing cost)

**Dependencies:** None (can start early)

---

### I7-04: Create Terraform configuration for data and messaging services

**Labels:** `epic:cloud-deployment`, `type:infrastructure`, `priority:high`
**Branch:** `feature/I7-04-terraform-data`

**Description:**
Terraform modules for Azure SQL, Cosmos DB, Service Bus, and Key Vault.

**Scope:**

Additional Terraform files in `infra/terraform/`:

`sql.tf`:
- Azure SQL Server `carebridge-dev-sql`
- Elastic pool (Basic or Standard S0 tier for cost)
- Per-service databases (7 databases matching service catalog)
- Entra ID admin enabled
- Firewall: allow Azure services + optional developer IP

`cosmos.tf`:
- Cosmos DB account `carebridge-dev-cosmos` (serverless tier)
- NoSQL API
- Database: `carebridge`
- Containers: `audit-events` (partition key: `/caseId`), `read-models` (partition key: `/caseId`)
- TTL: 365 days for audit, 90 days for read models

`servicebus.tf`:
- Service Bus namespace `carebridge-dev-sb` (Standard tier)
- Topics:
  - `patient-events` (CaseCreated, CaseUpdated)
  - `careplan-events` (CarePlanActivated, MilestoneCompleted)
  - `observation-events` (ObservationReceived)
  - `alert-events` (AlertRaised, AlertAcknowledged, AlertResolved)
  - `task-events` (TaskCreated, TaskCompleted)
  - `scheduling-events` (AppointmentBooked, AppointmentCompleted, AppointmentMissed)
  - `notification-events` (NotificationSent)
- Subscriptions per consuming service (matching service catalog)

`keyvault.tf`:
- Key Vault `carebridge-dev-kv`
- Access policies for AKS managed identity

`workload-identity.tf` (proof of concept for one service):
- Managed identity for Case Service
- Federated identity credential linking AKS OIDC to the managed identity
- RBAC role assignments: SQL access, Service Bus send/receive

**Acceptance criteria:**
- [ ] `terraform plan` succeeds for all data resources
- [ ] SQL databases match documented service catalog (7 databases)
- [ ] Cosmos DB containers have correct partition keys
- [ ] Service Bus topics and subscriptions match documented event architecture
- [ ] Key Vault created with AKS access
- [ ] At least one Workload Identity configured as proof of concept
- [ ] All resources use dev-tier SKUs (cost-optimized)

**Dependencies:** I7-03

---

### I7-05: Set up GitHub Actions CI pipeline

**Labels:** `epic:cloud-deployment`, `type:infrastructure`, `priority:high`
**Branch:** `feature/I7-05-ci-pipeline`

**Description:**
CI pipeline that runs on every pull request: build, test, lint, scan.

**Scope:**

Workflow file: `.github/workflows/ci.yml`

Trigger: `pull_request` to `develop` and `main`

Jobs:

**1. Build and Test (.NET):**
- Checkout
- Setup .NET 10
- Cache NuGet packages
- `dotnet restore`
- `dotnet build --no-restore`
- `dotnet test --no-build` (unit tests)
- Upload test results as artifact

**2. Integration Tests:**
- Start Docker Compose (SQL, RabbitMQ)
- Run db-migrator
- `dotnet test --filter Category=Integration`
- Upload test results

**3. Frontend:**
- Setup Node 20
- Cache npm packages
- `npm ci`
- `npm run lint`
- `npm run build`
- `npm run test` (if tests exist)

**4. Helm Lint:**
- Install Helm
- `helm lint deploy/helm/{service}` for each service

**5. Security Scan (optional):**
- Build Docker images
- Run Trivy scan on images
- Report findings

Status checks:
- All jobs must pass to merge

**Acceptance criteria:**
- [ ] CI pipeline triggers on PR creation and push to PR branch
- [ ] Build failure blocks merge (branch protection)
- [ ] Unit test results visible in PR
- [ ] Integration tests run against real Docker Compose infrastructure
- [ ] Frontend builds without errors
- [ ] Helm lint passes for all charts
- [ ] NuGet and npm caches improve second-run performance

**Dependencies:** Service code and tests exist

---

### I7-06: Set up GitHub Actions CD pipeline

**Labels:** `epic:cloud-deployment`, `type:infrastructure`, `priority:high`
**Branch:** `feature/I7-06-cd-pipeline`

**Description:**
CD pipeline that builds images, pushes to ACR, and deploys to AKS dev environment on merge to develop.

**Scope:**

Workflow file: `.github/workflows/cd.yml`

Trigger: `push` to `develop` branch

Prerequisites:
- OIDC federation from GitHub repo to Azure (configured manually in Azure portal)
- GitHub environment `dev` with Azure subscription ID, tenant ID, client ID as secrets

Jobs:

**1. Build and Push Images:**
- Login to Azure via OIDC (`azure/login@v2`)
- Login to ACR
- For each service: build Docker image, tag with git SHA and `latest`, push to ACR
- For frontend: build, tag, push

**2. Deploy to AKS:**
- Get AKS credentials
- For each service: `helm upgrade --install {service} deploy/helm/{service} -f deploy/helm/{service}/values.dev.yaml --set image.tag={git-sha}`

**3. Smoke Test:**
- Wait 60 seconds for pods to start
- Hit BFF `/healthz` endpoint
- Hit each service `/healthz` via port-forward or internal URL
- Report results

**4. Notify (optional):**
- Post deployment status to GitHub deployment environment

**Acceptance criteria:**
- [ ] Merge to `develop` triggers the CD pipeline
- [ ] Images tagged with git SHA and `latest`, pushed to ACR
- [ ] Helm deploys/upgrades all services on AKS
- [ ] Health check smoke test passes after deployment
- [ ] Pipeline uses OIDC federation (no stored credentials or secrets for Azure auth)
- [ ] Failed deployment doesn't leave the cluster in a broken state (Helm rollback on failure)

**Dependencies:** I7-01, I7-02, I7-03, I7-04

---

### I7-07: Build synthetic data generator tool

**Labels:** `epic:cloud-deployment`, `type:feature`, `priority:high`
**Branch:** `feature/I7-07-synthetic-data`

**Description:**
Console application that generates a realistic synthetic dataset by calling service APIs. The dataset showcases all features and creates a compelling demo.

**Scope:**

Console app: `tools/synthetic-data-generator/CareBridge.SyntheticDataGenerator`

Configuration:
- BFF base URL (default: `http://localhost:5000`)
- Random seed (default: 42, for deterministic output)
- Counts configurable via appsettings

Generated data:

**25 cases in various stages:**

| Category | Count | Description |
|----------|-------|-------------|
| Newly discharged | 5 | Created today, care plans active, no observations yet |
| Active monitoring | 8 | 3-10 days in, observations flowing, some normal, some abnormal |
| With alerts | 5 | Active cases with critical/high alerts and auto-generated tasks |
| Near completion | 4 | 20-28 days in, most milestones completed |
| Completed | 3 | All milestones done, cases closed |

**Per case (where applicable):**
- 5-20 observations (mix of types, mostly normal with occasional abnormal)
- Alerts generated by abnormal observations (via Care-Gap Engine, not directly inserted)
- Tasks auto-created from alerts + 2-3 manual tasks per active case
- 1-2 appointments per case (various statuses)
- Notifications generated from events

**Synthetic patient names** (realistic, diverse):
- Maria Santos, James Wilson, Priya Patel, etc. (deterministic from seed)

**Workflow:**
1. Create cases via `POST /api/cases` (one at a time, allowing events to propagate)
2. Wait briefly for care plan activation
3. Submit observations via `POST /api/v1/observations` (through BFF or directly)
4. Wait for alerts to generate
5. Acknowledge some alerts, resolve others
6. Create manual tasks, complete some
7. Create and manage appointments
8. Sleep between operations to allow event propagation

**Acceptance criteria:**
- [ ] `dotnet run --project tools/synthetic-data-generator` populates a demo-ready dataset
- [ ] 25 cases in various lifecycle stages
- [ ] Dashboard shows meaningful numbers (not all zeros, not all maxed)
- [ ] Alert queue has a mix of open Critical, High, and Medium alerts
- [ ] Some tasks are open, some in-progress, some completed
- [ ] Case timeline for active cases shows 10+ events
- [ ] Same seed produces identical results (deterministic)
- [ ] Generator completes in under 2 minutes

**Dependencies:** All service APIs (Epics 2-4), BFF

---

### I7-08: Create demo walkthrough documentation

**Labels:** `epic:cloud-deployment`, `type:documentation`, `priority:medium`
**Branch:** `feature/I7-08-demo-walkthrough`

**Description:**
A documented step-by-step script that walks through the end-to-end CareBridge workflow using the live system.

**Scope:**

Document: `docs/demo-walkthrough.md`

**Setup section:**
1. Start Docker Compose
2. Run migrations
3. Start all services (or use docker-compose.services.yml)
4. Run synthetic data generator
5. Open browser to `http://localhost:3000`

**Demo flow (10-15 minutes):**

1. **Dashboard overview** — Show summary cards, alert queue, recent cases
2. **Explore case list** — Show cases in various states
3. **Case detail (active case)** — Show patient info, care plan with milestones, observations, alerts, tasks, appointments
4. **Live discharge intake** — POST a new case via curl or the generator, watch it appear in the list, care plan auto-activates
5. **Submit observation** — POST an abnormal BP reading, watch alert appear
6. **Acknowledge and resolve alert** — Show the alert lifecycle
7. **Complete a task** — Show task queue, pick up a task, complete it, watch milestone update
8. **Schedule appointment** — Create, confirm, and complete an appointment
9. **View timeline** — Show the chronological event history for the case
10. **Audit trail** — Show the immutable audit log with filters

**Architecture talking points** (inline notes for the demo presenter):
- Event-driven workflow between services
- CQRS: dashboard reads from denormalized model, not transactional DB
- Idempotent processing: retry an observation POST with same key
- Health checks: hit `/healthz` on a service
- Correlation IDs: trace a request through logs

**Acceptance criteria:**
- [ ] Someone unfamiliar with the project can follow the walkthrough
- [ ] Each step references specific URLs, endpoints, or UI actions
- [ ] Expected results described at each step
- [ ] Architecture talking points provide context without being lectures
- [ ] Walkthrough completable in under 15 minutes
- [ ] Works with the synthetic data generator output

**Dependencies:** I7-07, all UI pages

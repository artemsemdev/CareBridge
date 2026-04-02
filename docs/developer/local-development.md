# Local Development Guide

**Document type:** Developer onboarding
**Last updated:** 2026-04-02

---

## Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| .NET SDK | 9.0+ | Build and run backend services |
| Node.js | 20+ | Build and run React frontend |
| Docker Desktop | Latest | Run infrastructure dependencies |
| Azure CLI | Latest | Cloud deployment (optional for local dev) |
| Helm CLI | 3.x | Kubernetes deployment (optional for local dev) |
| kubectl | Latest | Kubernetes management (optional for local dev) |

---

## Repository Structure

```
carebridge/
├── src/
│   ├── gateway/               # API Gateway / BFF
│   ├── services/
│   │   ├── case-service/
│   │   ├── careplan-service/
│   │   ├── observation-service/
│   │   ├── caregap-engine/
│   │   ├── task-service/
│   │   ├── appointment-service/
│   │   ├── notification-service/
│   │   ├── audit-service/     # Immutable audit trail (Epic 6)
│   │   └── reporting-service/
│   ├── web/
│   │   └── carebridge-ui/     # React frontend
│   └── shared/                # Shared contracts and middleware
├── tests/
├── infra/terraform/
├── deploy/helm/
├── tools/
│   ├── db-migrator/
│   └── synthetic-data-generator/ # Placeholder scaffold for Epic 7
└── docker-compose.yml
```

---

## Running Locally with Docker Compose

### Start Infrastructure Dependencies

```bash
docker-compose up -d
```

This starts:

| Service | Port | Purpose | Cloud Equivalent |
|---|---|---|---|
| SQL Server (Linux) | 1433 | Transactional databases | Azure SQL |
| RabbitMQ | 5672, 15672 (management UI) | Message broker | Azure Service Bus |
| Azurite | 10000-10002 | Blob/Queue/Table storage emulator | Azure Storage |

### First-Time Setup

```bash
# Apply database migrations for all services
dotnet run --project tools/db-migrator/CareBridge.DbMigrator
```

### Start All Services

```bash
# Run implemented backend services in separate terminals
dotnet run --project src/services/case-service/CareBridge.CaseService
dotnet run --project src/services/careplan-service/CareBridge.CarePlanService
dotnet run --project src/services/observation-service/CareBridge.ObservationService
dotnet run --project src/services/caregap-engine/CareBridge.CareGapEngine
dotnet run --project src/services/task-service/CareBridge.TaskService
dotnet run --project src/services/appointment-service/CareBridge.AppointmentService
dotnet run --project src/services/notification-service/CareBridge.NotificationService
dotnet run --project src/services/reporting-service/CareBridge.ReportingService
dotnet run --project src/services/audit-service/CareBridge.AuditService
dotnet run --project src/gateway/CareBridge.Gateway
```

At v0.6, `tools/synthetic-data-generator/` is a scaffold only and is not part of the runnable local stack yet.

### Service Ports

| Service | Port | URL |
|---|---|---|
| BFF / API Gateway | 5000 | http://localhost:5000 |
| Case Service | 5010 | http://localhost:5010 |
| Care Plan Service | 5020 | http://localhost:5020 |
| Observation Service | 5030 | http://localhost:5030 |
| Care-Gap Engine | 5040 | http://localhost:5040 |
| Task Service | 5050 | http://localhost:5050 |
| Appointment Service | 5060 | http://localhost:5060 |
| Notification Service | 5070 | http://localhost:5070 |
| Reporting Service | 5090 | http://localhost:5090 |
| Audit Service | 5080 | http://localhost:5080 |
| React Frontend | 5173 | http://localhost:5173 |

---

## Running Individual Services

For focused development on a single service:

```bash
cd src/services/case-service/CareBridge.CaseService
dotnet run
```

Each service reads `appsettings.Development.json` for local configuration:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=carebridge-case;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
  },
  "ServiceBus": {
    "ConnectionString": "amqp://guest:guest@localhost:5672"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

Services use the **adapter pattern** for cloud dependencies. In development mode, a RabbitMQ adapter replaces the Azure Service Bus SDK, and Azurite replaces Azure Blob Storage. The application code remains the same.

---

## Frontend Development

```bash
cd src/web/carebridge-ui
npm install
npm run dev
```

- **Vite dev server** runs with hot reload at http://localhost:5173 by default.
- The current frontend scaffold does not define a Vite API proxy. If local API forwarding is needed, add the proxy settings in `vite.config.ts`.
- Frontend validation is currently `npm run lint` and `npm run build`; there is no `npm test` script yet.

---

## Synthetic Data

Synthetic data tooling is planned after v0.6. The `tools/synthetic-data-generator/` directory exists as a scaffold, but there is no runnable generator or scenario runner in the repository yet.

For now, walk the system manually through the implemented APIs and UI:

- Create a case through the BFF or the React UI.
- Add observations to trigger care-gap evaluation and alert generation.
- Review the resulting tasks, appointments, dashboard updates, and case timeline.

---

## Running Tests

```bash
# Run the current .NET test projects in the solution
dotnet test CareBridge.sln

# Frontend validation
cd src/web/carebridge-ui
npm install
npm run lint
npm run build
```

---

## Common Issues

| Problem | Solution |
|---|---|
| SQL container not starting | Check Docker Desktop memory allocation (4GB+ recommended) |
| Port conflict on 1433 | Stop local SQL Server instance, or change docker-compose port mapping |
| RabbitMQ connection refused | Wait 10-15 seconds after `docker-compose up` for RabbitMQ to initialize |
| Migration fails | Ensure SQL container is healthy: `docker-compose ps` |
| Frontend can't reach API | Ensure the BFF is running on port 5000. Configure an API base URL in the app or add a Vite proxy in `vite.config.ts` if local forwarding is required. |
| Service Bus messages not processing | Check RabbitMQ management UI at http://localhost:15672 (guest/guest) |

---

## Cloud Dependencies in Local Mode

| Azure Service | Local Substitute | Limitations |
|---|---|---|
| Azure Service Bus | RabbitMQ | No dead-letter queue semantics, no sessions, no duplicate detection |
| Azure SQL | SQL Server Linux container | Feature-complete for development |
| Cosmos DB | In-memory store or local emulator | No partition key behavior, limited query support |
| Azure Blob Storage | Azurite | Feature-complete for development |
| Azure Key Vault | appsettings.Development.json | Secrets in plaintext locally (acceptable for dev with synthetic data) |
| Azure Health Data Services FHIR | Disabled in local mode | FHIR sync handler skips writes in development |
| Microsoft Entra ID | Mock auth provider | Fixed roles, no real token validation |
| Application Insights | Console logging | No distributed tracing visualization |

The adapter pattern means you can develop and test the full application workflow locally without any Azure subscription.

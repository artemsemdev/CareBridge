# Local Development Guide

**Document type:** Developer onboarding
**Last updated:** 2026-03-27

---

## Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| .NET SDK | 10.0+ | Build and run backend services |
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
│   │   ├── audit-service/
│   │   └── reporting-service/
│   ├── web/                   # React frontend
│   └── shared/                # Shared contracts and middleware
├── tests/
├── infra/terraform/
├── deploy/helm/
├── tools/
│   ├── synthetic-data-generator/
│   └── scenario-runner/
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
| Redis | 6379 | Caching | Azure Cache for Redis |
| Azurite | 10000-10002 | Blob/Queue/Table storage emulator | Azure Storage |

### First-Time Setup

```bash
# Apply database migrations for all services
dotnet run --project tools/db-migrator

# Seed synthetic data
dotnet run --project tools/synthetic-data-generator
```

### Start All Services

```bash
# Option 1: Run all services via docker-compose
docker-compose --profile services up -d

# Option 2: Run individual services for development
dotnet run --project src/gateway
dotnet run --project src/services/case-service
# ... etc.
```

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
| Audit Service | 5080 | http://localhost:5080 |
| Reporting Service | 5090 | http://localhost:5090 |
| React Frontend | 3000 | http://localhost:3000 |

---

## Running Individual Services

For focused development on a single service:

```bash
cd src/services/case-service
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
cd src/web
npm install
npm run dev
```

- **Vite dev server** with hot reload at http://localhost:3000.
- API calls are proxied to the BFF at http://localhost:5000 via Vite proxy config.
- **Auth in dev mode:** Uses a mock auth provider that simulates JWT tokens with configurable roles. No Entra ID setup needed for local development.

---

## Synthetic Data

### Generate Data

```bash
# Generate a full dataset: patients, discharge bundles, observations
dotnet run --project tools/synthetic-data-generator

# Generate a specific scenario
dotnet run --project tools/synthetic-data-generator -- --scenario standard-followup
dotnet run --project tools/synthetic-data-generator -- --scenario abnormal-reading
dotnet run --project tools/synthetic-data-generator -- --scenario missed-milestone
```

### Run Demo Scenario

```bash
# Execute the full demo flow: discharge → case → care plan → observations → alerts → tasks
dotnet run --project tools/scenario-runner
```

The scenario runner submits data through the API and waits for each stage to complete, providing a full end-to-end walkthrough.

---

## Running Tests

```bash
# Unit tests (no dependencies required)
dotnet test tests/unit/

# Integration tests (requires docker-compose dependencies)
docker-compose up -d
dotnet test tests/integration/

# Contract tests (validates API and event schemas)
dotnet test tests/contract/

# Frontend tests
cd src/web && npm test
```

---

## Common Issues

| Problem | Solution |
|---|---|
| SQL container not starting | Check Docker Desktop memory allocation (4GB+ recommended) |
| Port conflict on 1433 | Stop local SQL Server instance, or change docker-compose port mapping |
| RabbitMQ connection refused | Wait 10-15 seconds after `docker-compose up` for RabbitMQ to initialize |
| Migration fails | Ensure SQL container is healthy: `docker-compose ps` |
| Frontend can't reach API | Ensure BFF is running on port 5000. Check Vite proxy config in `vite.config.ts`. |
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

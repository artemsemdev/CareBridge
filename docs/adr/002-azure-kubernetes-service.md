# ADR-002: Azure Kubernetes Service for Container Orchestration

**Status:** Accepted
**Date:** 2026-03-27
**Deciders:** Solution architect

## Context

CareBridge's microservices architecture (see [ADR-001](./001-microservices-architecture.md)) requires a container orchestration platform that can manage multiple services, handle scaling, support namespace isolation, and integrate with Azure-native identity and networking. Azure offers several compute options for containerized workloads: Azure Kubernetes Service (AKS), Azure Container Apps (ACA), Azure App Service, and Azure Functions.

The choice of compute platform is one of the most consequential infrastructure decisions for the project. It dictates how services are deployed, how networking and ingress are configured, how secrets and identities are managed, and how much operational depth the platform can demonstrate.

CareBridge is a reference implementation intended to demonstrate production-grade cloud-native patterns. The compute platform must balance operational realism against unnecessary complexity.

## Decision

CareBridge will use Azure Kubernetes Service (AKS) as the container orchestration platform for all microservices.

The AKS cluster will be configured with:

- **System and user node pools** separated by workload type, with spot instances available for non-critical workloads in development environments.
- **Namespace-per-service isolation** for each microservice, enforcing resource quotas and network policies at the namespace level.
- **Helm-based deployment** for all services, with a shared chart library for common patterns (health checks, resource limits, pod disruption budgets) and per-service value overrides.
- **Workload Identity** (see [ADR-007](./007-workload-identity.md)) for all pod-to-Azure-resource authentication, eliminating static credentials.
- **NGINX Ingress Controller** with TLS termination for external traffic, internal service mesh communication over cluster DNS.
- **Horizontal Pod Autoscaler (HPA)** for CPU/memory-based scaling, with KEDA for event-driven scaling on Service Bus queue depth for the Notification Service and Audit Service.
- **GitHub Actions** pipelines that build container images, push to Azure Container Registry, and deploy via Helm upgrade commands targeting the appropriate namespace.

## Consequences

**Positive:**

- Full Kubernetes operational depth: namespace isolation, RBAC, network policies, resource quotas, pod disruption budgets, liveness/readiness probes, and rolling deployments are all exercised.
- Helm chart management demonstrates real GitOps patterns. Chart versions, value overrides per environment, and rollback procedures are first-class concerns.
- KEDA integration with Service Bus (see [ADR-004](./004-azure-service-bus.md)) demonstrates event-driven autoscaling, a pattern directly relevant to healthcare workloads with variable discharge volumes.
- AKS integrates natively with Azure Monitor, Managed Prometheus, and Managed Grafana (see [ADR-008](./008-opentelemetry-azure-monitor.md)), providing a complete observability stack without third-party tooling.
- AKS Workload Identity is the recommended identity pattern for pod-to-Azure authentication, and it requires AKS -- this is not available on Container Apps in the same way.

**Negative:**

- AKS has a larger operational surface than managed alternatives. Cluster upgrades, node pool management, and networking configuration require ongoing attention.
- Local development requires a Kubernetes-like environment (Docker Compose or kind/minikube), adding friction for developers who are not familiar with Kubernetes.
- The AKS control plane has a base cost even when no workloads are running, which matters for a portfolio project. The free tier control plane mitigates this, but node pool costs remain.
- Debugging pod scheduling failures, resource contention, and networking issues requires Kubernetes-specific operational knowledge.

## Alternatives Considered

**Azure Container Apps (ACA):** ACA provides a simpler abstraction over Kubernetes with built-in Dapr integration, automatic scaling, and managed ingress. It would reduce operational burden significantly. However, ACA intentionally hides Kubernetes primitives -- there is no access to namespaces, network policies, custom Helm charts, or pod-level configuration. For a reference implementation whose purpose includes demonstrating Kubernetes competency, this abstraction defeats the goal. ACA is the right choice for teams that want to avoid Kubernetes, not for a project that wants to showcase it.

**Azure App Service:** App Service is a mature PaaS for web applications with excellent .NET integration. It supports containers via Web App for Containers. However, it does not support multi-container pods, namespace isolation, Helm-based deployment, or event-driven autoscaling via KEDA. It is fundamentally a single-app-per-instance model, which does not map well to microservices orchestration.

**Azure Functions:** Functions excel at event-driven, short-lived compute. The Notification Service and Audit Service could plausibly be implemented as Functions triggered by Service Bus messages. However, Functions poorly model the Patient Service and Care Plan Service, which are long-running API services with persistent state. Mixing compute models (Functions for some services, AKS for others) adds operational complexity without proportional benefit for a reference implementation.

# ADR-007: AKS Workload Identity for Pod-to-Azure Authentication

**Status:** Accepted
**Date:** 2026-03-27
**Deciders:** Solution architect

## Context

CareBridge's microservices run on AKS (see [ADR-002](./002-azure-kubernetes-service.md)) and need to authenticate to multiple Azure services: Azure SQL (see [ADR-005](./005-azure-sql-for-transactional-data.md)), Cosmos DB (see [ADR-006](./006-read-models-cosmos-db.md)), Azure Service Bus (see [ADR-004](./004-azure-service-bus.md)), Azure Health Data Services FHIR (see [ADR-003](./003-azure-health-data-services-fhir.md)), and Azure Key Vault. Each service requires credentials to access these resources.

The authentication model must satisfy several constraints:

- **No static secrets** in configuration, environment variables, or Key Vault references that require an initial bootstrap secret.
- **Least privilege** -- each service should have access to only the Azure resources it needs, with no shared service accounts.
- **Azure-native** -- the identity model should integrate with Entra ID role-based access control (RBAC) for consistent permission management.
- **Kubernetes-native** -- the identity binding should work through standard Kubernetes service account mechanisms, not custom sidecars or init containers.

This is a foundational cross-cutting decision. Every service in CareBridge depends on the authentication model, so it must be settled early and applied consistently.

## Decision

CareBridge will use **AKS Workload Identity** for all pod-to-Azure-resource authentication. Every microservice receives its own Kubernetes service account, federated with a dedicated Azure Managed Identity via OIDC trust.

The configuration follows this pattern for each service:

1. **Azure Managed Identity** created per service (e.g., `id-carebridge-patient-svc`, `id-carebridge-careplan-svc`). Each identity exists in the platform's resource group.
2. **Federated credential** configured on the managed identity, trusting the AKS cluster's OIDC issuer for the specific Kubernetes service account and namespace.
3. **Kubernetes ServiceAccount** annotated with `azure.workload.identity/client-id` pointing to the managed identity's client ID. The ServiceAccount is deployed via Helm in the service's namespace.
4. **RBAC role assignments** grant each managed identity only the permissions it needs:
   - Patient Service identity: `db_datareader` and `db_datawriter` on its Azure SQL database, `Azure Service Bus Data Sender` on `patient-events` topic.
   - Care Plan Service identity: `db_datareader` and `db_datawriter` on its Azure SQL database, `Azure Service Bus Data Sender` on `careplan-events`, `Azure Service Bus Data Receiver` on `patient-events` subscription.
   - FHIR Gateway Service identity: `FHIR Data Contributor` on the AHDS FHIR workspace, `Azure Service Bus Data Receiver` on multiple topic subscriptions.
   - Audit Service identity: `Cosmos DB Built-in Data Contributor` on the audit container, `Azure Service Bus Data Receiver` on all topic subscriptions.
5. **Pod label** `azure.workload.identity/use: "true"` triggers the mutating webhook to inject the token volume and environment variables into the pod at scheduling time.

The `DefaultAzureCredential` from the Azure Identity SDK is used in all service code. It automatically discovers the workload identity token without any explicit configuration, making the code environment-agnostic -- the same credential chain works in AKS (workload identity), local development (Azure CLI), and CI/CD (service principal via environment variables).

## Consequences

**Positive:**

- Zero static secrets in the entire platform. No connection strings with passwords, no client secrets, no certificates to rotate. The authentication token is a short-lived JWT projected into the pod by the Kubernetes control plane.
- Least privilege is enforced at the Azure RBAC level. The Notification Service cannot read from the Patient Service's database, the Patient Service cannot write to Cosmos DB. Permission boundaries are explicit and auditable.
- Managed identity lifecycle is managed through infrastructure-as-code (Bicep/Terraform), making permission changes reviewable in pull requests.
- `DefaultAzureCredential` abstracts the identity mechanism. Service code does not reference workload identity directly, so it works seamlessly across local development, CI, and production environments.
- Workload Identity is Microsoft's recommended replacement for pod-managed identity and is the long-term supported path for AKS-to-Azure authentication.

**Negative:**

- Each service requires its own managed identity, federated credential, and set of RBAC role assignments. With six services, this means six identities, six federated credentials, and approximately 20 role assignments. The infrastructure-as-code footprint is substantial.
- Debugging authentication failures requires understanding the OIDC federation chain: AKS OIDC issuer, federated credential configuration, service account annotation, pod label, and RBAC assignment. A misconfiguration at any point results in opaque 401/403 errors.
- Local development cannot use workload identity directly. Developers authenticate via `az login` and `DefaultAzureCredential` falls through to the Azure CLI credential. This works but means the local identity has broader permissions than the production workload identity, which is a slight deviation from production parity.
- The mutating admission webhook that injects identity tokens must be healthy for pods to schedule correctly. If the webhook is unavailable during cluster upgrades, new pod deployments will fail.

## Alternatives Considered

**AAD Pod-Managed Identity (v1):** Pod-managed identity was the predecessor to Workload Identity on AKS. It used a DaemonSet (NMI) to intercept IMDS calls and inject identity tokens. Microsoft has deprecated this approach in favor of Workload Identity, citing reliability issues with the NMI DaemonSet and the move toward OIDC-based federation. Adopting a deprecated identity model in a new reference implementation would be counterproductive.

**Static connection strings in Azure Key Vault:** Storing database connection strings and Service Bus keys in Key Vault is a common pattern. However, the pod still needs credentials to authenticate to Key Vault, creating a bootstrap problem. Key Vault references in AKS can use workload identity, making Key Vault useful for non-identity secrets (API keys for external services, configuration values), but redundant for Azure service authentication. CareBridge uses Key Vault only for secrets that cannot be replaced by RBAC (e.g., synthetic data generation API keys), not for Azure service credentials.

**Service principals with client secrets:** Creating an Entra ID app registration per service and injecting client secrets via Kubernetes secrets or Key Vault is a traditional approach. It works but introduces secret rotation as an ongoing operational concern. Client secrets expire (default 2 years, often configured shorter), and rotation requires coordinated secret update and pod restart. Workload Identity eliminates this class of operational toil entirely.

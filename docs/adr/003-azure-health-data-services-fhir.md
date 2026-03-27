# ADR-003: Azure Health Data Services FHIR for Clinical Interoperability

**Status:** Accepted
**Date:** 2026-03-27
**Deciders:** Solution architect

## Context

CareBridge coordinates post-discharge care, which requires exchanging clinical data in standardized formats. FHIR R4 is the dominant interoperability standard in US healthcare, mandated by CMS interoperability rules and adopted by all major EHR vendors. The platform needs a FHIR-compliant layer to expose patient data, care plans, and clinical documents in a format that external systems can consume.

The question is whether to use a managed FHIR service, self-host an open-source FHIR server, or build a custom FHIR mapping layer on top of the internal domain model. This decision affects operational burden, standards compliance, and integration surface area.

CareBridge operates exclusively with synthetic data (see [ADR-010](./010-synthetic-data-only.md)), so the FHIR layer does not need to meet HIPAA certification requirements. However, the architecture should mirror what a production healthcare system would use, since the platform serves as a reference implementation.

## Decision

CareBridge will use Azure Health Data Services (AHDS) FHIR as the managed interoperability layer for all FHIR R4 resource storage and retrieval.

The FHIR Gateway Service (see [ADR-001](./001-microservices-architecture.md)) translates between CareBridge's internal domain events and FHIR R4 resources, writing to and reading from the AHDS FHIR endpoint. Specifically:

- **Patient resources** are projected from the Patient Service domain model into FHIR Patient, Encounter, and Condition resources.
- **Care plan resources** are projected from the Care Plan Service into FHIR CarePlan and Task resources.
- **Clinical documents** are stored as FHIR DocumentReference resources with synthetic content.
- The FHIR Gateway Service subscribes to domain events via Service Bus (see [ADR-004](./004-azure-service-bus.md)) and maintains the FHIR projection asynchronously. This means the FHIR store is eventually consistent with the transactional services.
- Authentication to AHDS FHIR uses Workload Identity (see [ADR-007](./007-workload-identity.md)) with a managed identity that has the FHIR Data Contributor role.

The AHDS FHIR endpoint is also exposed through the BFF (see [ADR-009](./009-react-bff-frontend.md)) for the frontend to display FHIR-native views when needed, such as patient timeline and care plan summaries.

## Consequences

**Positive:**

- Full FHIR R4 compliance out of the box, including search parameters, resource validation, versioning, and history. No need to implement or maintain FHIR specification conformance manually.
- Native Azure integration: AHDS FHIR authenticates via Entra ID, logs to Azure Monitor, and integrates with Azure API Management if needed. This aligns with the platform's Azure-native posture.
- SMART on FHIR support is available for future extension, enabling third-party app authorization if the platform evolves to demonstrate that pattern.
- Managed service eliminates patching, scaling, and availability concerns for the FHIR layer.
- The async projection pattern (domain events to FHIR resources) demonstrates a real-world integration architecture where the FHIR store is a read-optimized projection, not the system of record.

**Negative:**

- AHDS FHIR has per-transaction and per-storage costs that accumulate even with synthetic data. The serverless pricing model helps, but cost monitoring is necessary.
- The FHIR store is eventually consistent with the transactional services. A care plan created in the Care Plan Service may not appear in the FHIR endpoint for seconds or longer, depending on Service Bus processing latency.
- AHDS FHIR has throughput limits (RU-based) that require capacity planning for bulk operations like synthetic data seeding. Bulk import must be batched appropriately.
- The FHIR Gateway Service becomes a critical integration point. If it fails or falls behind on event processing, the FHIR projection drifts from the transactional state. Dead-letter queue monitoring and replay mechanisms are required.

## Alternatives Considered

**HAPI FHIR on AKS:** HAPI FHIR is the leading open-source FHIR server, written in Java, with full R4 support. Running it on AKS would provide more control over configuration and eliminate per-transaction costs. However, it introduces a JVM workload into an otherwise .NET-native stack, requires managing a separate PostgreSQL or SQL Server database for HAPI's persistence, and adds significant operational overhead for upgrades, scaling, and monitoring. The operational burden is not justified when a managed alternative exists that integrates natively with the rest of the Azure stack.

**Custom FHIR mapping layer:** Building a lightweight API that translates internal domain models to FHIR JSON on the fly, without a dedicated FHIR store, would be simpler to deploy. However, it would not support FHIR search parameters, resource history, or bundle operations without reimplementing substantial parts of the FHIR specification. Partial FHIR compliance is worse than no FHIR support at all -- it creates a false impression of interoperability.

**No FHIR layer:** Omitting FHIR entirely and using only proprietary APIs would simplify the architecture. However, FHIR interoperability is a defining characteristic of modern healthcare platforms, and its omission would significantly reduce the reference value of CareBridge.

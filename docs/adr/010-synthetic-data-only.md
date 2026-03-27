# ADR-010: Synthetic Data Only -- No Real PHI Support

**Status:** Accepted
**Date:** 2026-03-27
**Deciders:** Solution architect

## Context

CareBridge is a post-discharge care coordination platform built as a portfolio project and reference implementation. It is hosted in a public GitHub repository. The platform demonstrates cloud-native healthcare architecture patterns including FHIR interoperability (see [ADR-003](./003-azure-health-data-services-fhir.md)), event-driven microservices (see [ADR-001](./001-microservices-architecture.md)), and Azure-native security (see [ADR-007](./007-workload-identity.md)).

Healthcare software that handles real patient data -- Protected Health Information (PHI) under HIPAA -- carries substantial compliance, legal, and operational obligations: Business Associate Agreements, annual risk assessments, breach notification procedures, workforce training, physical safeguards, and audit controls that meet specific regulatory thresholds. These obligations are appropriate for production healthcare systems but create scope, cost, and liability that are incompatible with a public reference implementation.

The fundamental tension is: the platform must be architecturally realistic (demonstrating patterns that apply to real healthcare systems) while being legally and ethically safe as a publicly visible project with no compliance certification.

## Decision

All patient, clinical, and provider data in CareBridge is **synthetic**. The platform will never process, store, or transmit real Protected Health Information (PHI). This is not a configuration toggle -- it is a hard architectural boundary.

### Synthetic Data Strategy

- **Synthea** (synthetic patient generator) is used to produce realistic patient records, encounters, conditions, medications, and care plans. Synthea generates FHIR R4-compliant bundles that are loaded into Azure Health Data Services FHIR (see [ADR-003](./003-azure-health-data-services-fhir.md)) and projected into internal service databases.
- **Bogus** (.NET fake data library) supplements Synthea output with CareBridge-specific synthetic data: provider profiles, notification preferences, scheduling slots, and care plan template content.
- A **data seeding pipeline** runs as a Kubernetes Job during environment provisioning. It generates a configurable volume of synthetic patients (default: 500 patients with 2-3 encounters each), creating a realistic data volume for demonstrating dashboard views, search, and analytics.
- Synthetic data is deterministic when seeded with a fixed seed value, ensuring reproducible demos and test runs.

### Boundary Enforcement

- The repository README, LICENSE, and this ADR explicitly state that the platform is for synthetic data only and is not certified for PHI.
- No HIPAA-required audit controls (access logging to a tamper-proof store, minimum necessary access verification, breach notification automation) are implemented beyond what the architecture naturally provides. The audit store (see [ADR-006](./006-read-models-cosmos-db.md)) demonstrates the architectural pattern but does not claim compliance.
- Encryption at rest and in transit is enabled (Azure SQL TDE, Cosmos DB encryption, TLS everywhere) because these are baseline Azure defaults, not because the project claims HIPAA compliance.
- No Business Associate Agreement (BAA) is executed with Microsoft for the Azure resources used by CareBridge.

### What This Does Not Mean

This decision does not mean the architecture is incompatible with real PHI. The patterns implemented -- Workload Identity, RBAC, encrypted storage, audit logging, FHIR compliance -- are the same patterns a production system would use. A team adopting CareBridge's architecture for production use would need to add compliance-specific controls (BAA, access reviews, breach notification, workforce training), not replace the architecture.

## Consequences

**Positive:**

- The repository can be public without legal or ethical risk. No possibility of PHI exposure through code, configuration, sample data, or CI/CD artifacts.
- No compliance certification burden. The project avoids the cost and ongoing maintenance of SOC 2, HITRUST, or HIPAA compliance programs, which are inappropriate for a portfolio project.
- Synthetic data can be freely shared in documentation, screenshots, demo videos, and conference presentations without privacy concerns.
- The data seeding pipeline produces consistent, reproducible datasets that make demos reliable and integration tests deterministic.
- Contributors can clone the repository and run the platform locally without handling sensitive data or signing agreements.

**Negative:**

- The platform cannot be deployed directly into a production healthcare environment without adding compliance controls. Organizations evaluating the architecture must understand that compliance is out of scope, not out of reach.
- Synthetic data, while realistic, does not capture the full complexity of real clinical data: edge cases in patient names, addresses, insurance identifiers, and clinical narratives that stress-test validation logic. Real-world data quality issues are not represented.
- The absence of compliance controls (access reviews, minimum necessary enforcement, breach notification) means the platform does not demonstrate the full operational surface of a production healthcare system. These are important but are operational processes, not architectural patterns.
- Some potential evaluators may discount the project because it does not handle real data. The project's documentation must clearly frame this as a deliberate scope decision, not a limitation.

## Alternatives Considered

**De-identified real data:** Using de-identified patient data (stripped of the 18 HIPAA identifiers under Safe Harbor) would provide more realistic clinical content. However, de-identification carries re-identification risk, particularly with small datasets. It also requires a data source with a Data Use Agreement, limits the public nature of the repository, and introduces compliance ambiguity. The marginal realism benefit does not justify the legal and ethical complexity.

**Private repository with real data:** Making the repository private and using test data from a healthcare partner would demonstrate full production fidelity. However, this defeats the primary purpose of a portfolio project -- public visibility. It also introduces data governance obligations, access control for contributors, and liability for data handling. A private reference implementation has fundamentally different goals than a public one.

**Configurable real/synthetic mode:** Building a configuration switch that toggles between synthetic and real data modes would offer flexibility. In practice, this creates a false compliance signal -- the existence of a "real data mode" implies the platform is ready for PHI, when the compliance controls, operational procedures, and legal agreements required for PHI handling are absent. It also doubles the testing surface (every feature must be validated in both modes) for no benefit to the reference implementation's goals. The synthetic-only constraint is a feature, not a limitation to be worked around.

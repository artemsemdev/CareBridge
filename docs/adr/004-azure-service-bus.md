# ADR-004: Azure Service Bus for Asynchronous Domain Event Messaging

**Status:** Accepted
**Date:** 2026-03-27
**Deciders:** Solution architect

## Context

CareBridge's microservices architecture (see [ADR-001](./001-microservices-architecture.md)) requires an asynchronous messaging backbone for inter-service communication. Services must publish domain events (e.g., "PatientDischarged", "CarePlanActivated", "TaskOverdue") that other services consume to trigger downstream workflows. The messaging infrastructure must support reliable delivery, dead-letter handling, ordered processing where required, and pub-sub fan-out.

The platform runs entirely on Azure (see [ADR-002](./002-azure-kubernetes-service.md)), so the messaging choice should integrate natively with Azure identity, monitoring, and autoscaling. Azure offers three primary messaging/eventing services: Service Bus, Event Grid, and Event Hubs. Self-hosted alternatives like RabbitMQ are also viable on AKS.

The choice of messaging infrastructure has direct implications for retry semantics, ordering guarantees, dead-letter queue handling, and the complexity of event-driven workflows like care plan saga orchestration.

## Decision

CareBridge will use Azure Service Bus Standard tier as the asynchronous messaging backbone for all inter-service domain events.

The messaging topology is structured as follows:

- **Topics** are created per domain aggregate that publishes events: `patient-events`, `careplan-events`, `scheduling-events`, `notification-events`.
- **Subscriptions** are created per consuming service per topic. For example, the Care Plan Service subscribes to `patient-events` to receive "PatientDischarged" events and create default care plans. The FHIR Gateway Service subscribes to multiple topics to maintain the FHIR projection (see [ADR-003](./003-azure-health-data-services-fhir.md)).
- **Subscription filters** use SQL-like rules on message properties to route only relevant event types to each subscriber, avoiding unnecessary message processing.
- **Sessions** are enabled on subscriptions that require ordered processing by partition key (e.g., all events for a given patient must be processed in order by the Audit Service).
- **Dead-letter queues** are monitored per subscription. A dedicated DLQ processor re-evaluates and replays or archives failed messages. DLQ depth is exposed as a metric to Azure Monitor (see [ADR-008](./008-opentelemetry-azure-monitor.md)).
- **KEDA ScaledObjects** on AKS use Service Bus queue depth as a scaling trigger, allowing the Notification Service to scale out during high-volume discharge periods (see [ADR-002](./002-azure-kubernetes-service.md)).
- All Service Bus connections authenticate via Workload Identity (see [ADR-007](./007-workload-identity.md)) using managed identity role assignments (Azure Service Bus Data Sender / Data Receiver).

Messages use a standardized envelope schema with correlation IDs that propagate through OpenTelemetry distributed traces (see [ADR-008](./008-opentelemetry-azure-monitor.md)).

## Consequences

**Positive:**

- Topics and subscriptions provide native pub-sub fan-out. A single "PatientDischarged" event reaches the Care Plan Service, Notification Service, FHIR Gateway Service, and Audit Service without the publisher knowing about any of them.
- Dead-letter queues are built in and per-subscription. Failed messages are isolated without blocking the main processing path. This is critical for healthcare workflows where a notification failure should not block care plan creation.
- Session support enables ordered processing per partition key without complex application-level sequencing logic.
- KEDA integration enables event-driven pod autoscaling, which is more cost-efficient than CPU-based HPA for bursty message processing workloads.
- Managed service with 99.9% SLA. No broker infrastructure to manage, patch, or scale.

**Negative:**

- Service Bus Standard tier has per-operation costs that accumulate with high message volumes. For a portfolio project with synthetic data, this is manageable, but cost monitoring is necessary.
- Message size is limited to 256 KB on Standard tier. Clinical documents or large payloads must use the claim-check pattern (store payload in Blob Storage, pass reference in message).
- Service Bus adds latency compared to in-process event dispatch. End-to-end event propagation from publish to consumer processing typically takes 50-200ms, which introduces observable eventual consistency.
- Local development requires either an Azure Service Bus namespace (with associated cost) or a local emulator. The Azure Service Bus emulator in Docker is available but has feature gaps compared to the managed service.

## Alternatives Considered

**Azure Event Grid:** Event Grid is designed for reactive event routing -- high fan-out, low latency, push-based delivery to webhooks or Azure services. It excels at infrastructure events (blob created, resource updated) and simple event routing. However, it lacks session-based ordering, does not provide built-in dead-letter queues with the same richness as Service Bus, and its retry model is less configurable. For workflow-oriented domain events that require ordering guarantees and robust DLQ handling, Service Bus is the better fit.

**Azure Event Hubs:** Event Hubs is an event streaming platform optimized for high-throughput ingestion and analytics workloads. It uses consumer groups and partitions, more akin to Apache Kafka than a message broker. Event Hubs is ideal for telemetry ingestion, log aggregation, and stream processing. CareBridge's domain events are workflow messages, not streams -- they need per-message acknowledgment, dead-letter routing, and subscription filtering, none of which map naturally to Event Hubs' partition-based model.

**RabbitMQ on AKS:** RabbitMQ is a mature, feature-rich message broker that supports AMQP, topic exchanges, and flexible routing. Running RabbitMQ on AKS would provide full control over broker configuration and eliminate per-message costs. However, it requires managing a stateful clustered workload on Kubernetes (persistent volumes, quorum queues, node discovery), which adds significant operational complexity. It also does not integrate with Azure Workload Identity or KEDA's native Service Bus scaler. The operational overhead is not justified when a managed alternative provides equivalent messaging semantics with Azure-native integration.

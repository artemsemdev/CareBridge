# ADR-008: OpenTelemetry with Azure Monitor for Observability

**Status:** Accepted
**Date:** 2026-03-27
**Deciders:** Solution architect

## Context

CareBridge is a distributed microservices platform (see [ADR-001](./001-microservices-architecture.md)) where a single user action -- such as viewing a patient's post-discharge dashboard -- may traverse the BFF, multiple backend services, Service Bus message processing, and Cosmos DB read model queries. Without comprehensive observability, diagnosing latency, failures, and behavioral anomalies across this service mesh is impractical.

The observability stack must cover three pillars: **distributed traces** (request flow across services), **metrics** (throughput, latency, error rates, resource utilization), and **structured logs** (contextual diagnostic output correlated with traces). The instrumentation approach must work consistently across all .NET services and the React frontend.

The question is whether to use a vendor-specific SDK (Application Insights), an open standard (OpenTelemetry), or a third-party platform (Datadog, ELK, Grafana Cloud).

## Decision

CareBridge will use **OpenTelemetry (OTel)** as the instrumentation standard across all services, with **Azure Monitor** (Application Insights, Managed Prometheus, Managed Grafana) as the observability backend.

### Instrumentation Layer

All .NET services use the OpenTelemetry .NET SDK with the following configuration:

- **Traces:** Automatic instrumentation for ASP.NET Core (inbound HTTP), HttpClient (outbound HTTP), Entity Framework Core (database queries), and Azure SDK (Service Bus, Cosmos DB, FHIR client). Custom activity sources for domain-specific operations (care plan state transitions, FHIR projection, notification dispatch).
- **Metrics:** Runtime metrics (GC, thread pool), ASP.NET Core request metrics, and custom meters for business KPIs (discharge events processed per minute, care plan tasks overdue, notification delivery success rate).
- **Logs:** `ILogger` output enriched with trace context (trace ID, span ID) via OpenTelemetry's log bridge. Structured log properties include service name, Kubernetes pod name, and correlation ID from the Service Bus message envelope.
- **Propagation:** W3C TraceContext propagation is used for all HTTP calls. Service Bus messages carry trace parent in application properties, enabling end-to-end trace continuity from API request through async event processing.

### Backend Layer

- **Azure Monitor Exporter** sends traces and logs to Application Insights. The connection is configured via the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable, injected through Helm values per environment.
- **Managed Prometheus** scrapes metrics from each pod's `/metrics` endpoint (exposed by the OpenTelemetry Prometheus exporter). AKS's Azure Monitor agent handles scrape configuration via PodMonitor custom resources.
- **Managed Grafana** provides dashboards built on Prometheus metrics and Application Insights data. Pre-built dashboards cover per-service latency, Service Bus queue depth and DLQ counts, Cosmos DB RU consumption, and care plan workflow completion rates.

### Frontend Observability

The React frontend uses the OpenTelemetry JS SDK to capture page load timing, XHR/fetch call traces, and user interaction spans. These are sent to Application Insights via the OTLP exporter configured in the BFF (see [ADR-009](./009-react-bff-frontend.md)), maintaining trace continuity from browser to backend.

## Consequences

**Positive:**

- Vendor-neutral instrumentation. The OpenTelemetry SDK is a CNCF project with broad industry adoption. If CareBridge's observability backend changes (e.g., from Azure Monitor to Grafana Cloud or Datadog), only the exporter configuration changes -- all instrumentation code remains untouched.
- End-to-end distributed traces span the full request lifecycle: browser, BFF, backend service, Service Bus consumer, Cosmos DB projection. A single trace ID connects every step, enabling rapid root cause analysis.
- Business metrics (discharge throughput, task overdue rates) are first-class observability signals alongside infrastructure metrics. Grafana dashboards can correlate business KPI degradation with infrastructure events.
- Managed Prometheus and Managed Grafana eliminate the operational overhead of self-hosted monitoring infrastructure. AKS-native integration means metrics collection works out of the box with PodMonitor resources.

**Negative:**

- OpenTelemetry's .NET SDK, while stable, has a larger configuration surface than the Application Insights SDK. Setting up auto-instrumentation, custom activity sources, metric instruments, and log enrichment requires deliberate effort in each service's startup code.
- Running both the Azure Monitor exporter (for traces/logs) and the Prometheus exporter (for metrics) means two telemetry pipelines. This is standard practice but adds configuration complexity.
- Managed Grafana dashboards must be created and maintained. Unlike Application Insights' auto-generated dashboards, Grafana requires explicit dashboard-as-code management (JSON models stored in the repo).
- Trace sampling may be necessary in higher-throughput scenarios to control Application Insights ingestion costs. Configuring appropriate sampling rates without losing diagnostic visibility requires tuning.

## Alternatives Considered

**Application Insights SDK only:** The Application Insights SDK for .NET provides excellent auto-instrumentation with minimal configuration. It captures HTTP requests, dependencies, exceptions, and custom events out of the box. However, the SDK is vendor-specific -- instrumentation code is coupled to Azure Monitor. If the platform needed to support an alternative backend (for multi-cloud or cost reasons), all instrumentation would need to be rewritten. OpenTelemetry with the Azure Monitor exporter provides equivalent Azure integration while preserving vendor neutrality. The OpenTelemetry approach is also Microsoft's own recommended path going forward.

**ELK Stack (Elasticsearch, Logstash, Kibana):** ELK is a powerful, flexible observability platform with strong search capabilities. Running ELK on AKS would provide full control over data retention and querying. However, Elasticsearch is resource-intensive, operationally complex to run on Kubernetes (stateful sets, memory tuning, index lifecycle management), and would constitute a significant infrastructure project within the reference implementation. The operational overhead is disproportionate to the observability benefit for this project.

**Datadog:** Datadog provides an excellent unified observability platform with strong Kubernetes integration, APM, and log management. It supports OpenTelemetry ingestion natively. However, Datadog is a third-party SaaS with per-host pricing that adds ongoing cost. For a portfolio project built on Azure, using Azure-native observability tools demonstrates the platform ecosystem more coherently. Datadog would be a strong choice for a production deployment with budget for premium observability tooling.

# Incident Response

**Document type:** Operational runbook
**Last updated:** 2026-03-27

---

## Incident Classification

| Severity | Definition | Example | Response Time |
|---|---|---|---|
| **P1 — Critical** | Core workflow broken. No alerts firing, no cases created, or data loss. | Care-Gap Engine completely down; Service Bus namespace unreachable; all pods in CrashLoopBackOff | 15 minutes |
| **P2 — High** | Major feature degraded. Users affected but workarounds exist. | Notification delivery failing; dashboard loading > 10s; observation ingestion errors > 10% | 1 hour |
| **P3 — Medium** | Minor degradation. Operational impact is limited. | Single service latency elevated; DLQ messages accumulating slowly; one pod restarting | 4 hours |
| **P4 — Low** | Cosmetic or non-urgent. | Grafana dashboard panel not rendering; log noise from deprecated endpoint; non-critical alert firing | Next business day |

---

## Initial Triage Steps

1. **Check Grafana Platform Overview dashboard.** Identify which service(s) show elevated error rates or latency.
2. **Check Azure Service Health.** Rule out Azure-side outages (SQL, Service Bus, Cosmos DB, AKS).
3. **Check AKS pod status:**
   ```bash
   kubectl get pods -n carebridge-app
   kubectl get events -n carebridge-app --sort-by=.lastTimestamp | tail -20
   ```
4. **Check Service Bus DLQ counts** on Service Bus Health dashboard. Growing DLQ = messages failing.
5. **Check recent deployments.** Was anything deployed in the last 2 hours?
   ```bash
   helm history <service> -n carebridge-app | tail -5
   ```
6. **Check Application Insights → Failures** blade for aggregated exception patterns.

---

## Escalation Matrix

| Severity | First Responder | Escalate To | Escalation Trigger |
|---|---|---|---|
| P1 | On-call engineer | Team lead + architect | Not mitigated in 30 minutes |
| P2 | On-call engineer | Team lead | Not mitigated in 2 hours |
| P3 | Assigned engineer | On-call if worsens | Upgraded to P2 |
| P4 | Backlog | — | — |

---

## Communication Template

```
**Incident Update — [P1/P2/P3] [Service/Component]**

**Status:** Investigating / Identified / Mitigating / Resolved
**Started:** 2026-03-27 14:30 UTC
**Impact:** [What users are experiencing]
**Root Cause:** [Known or under investigation]
**Next Update:** [Time]
**Actions Taken:** [List]
```

---

## Post-Incident Review

After every P1 and P2 incident:

1. **Timeline.** What happened, when, and in what order.
2. **Root cause.** Why did it happen? (Not who — blameless review.)
3. **Detection.** How was it discovered? How long before detection?
4. **Resolution.** What fixed it? How long did resolution take?
5. **Prevention.** What changes prevent recurrence? (Code, config, monitoring, process.)
6. **Action items.** Specific, assigned, time-bound.

---

## Common Incident Patterns

| Symptom | Likely Cause | First Action |
|---|---|---|
| All services returning 503 | AKS node pool failure or SQL outage | Check `kubectl get nodes` and Azure SQL health |
| DLQ growing across all topics | Service Bus connectivity issue or shared consumer bug | Check Service Bus namespace health, check OTEL logs for connection errors |
| Single service CrashLoopBackOff | OOM kill, startup failure, bad config | `kubectl describe pod <pod>`, check events and container exit code |
| Dashboard shows stale data | Reporting Service consumer lagging | Check Reporting Service pod health and queue depth |
| No alerts firing | Care-Gap Engine down or disconnected from Service Bus | Check CGE pod status and subscription queue depth |
| Notification delivery 100% failure | External provider outage or template error | Check Notification Service logs, delivery log entries |
| High latency across all APIs | SQL elastic pool DTU exhaustion | Check SQL DTU dashboard, identify expensive queries |

# Service Degradation

**Document type:** Operational runbook
**Last updated:** 2026-03-27

---

## Detecting Degradation

Degradation is typically detected through:
- **Grafana alerts:** Latency increase, error rate spike, HPA scaling events.
- **Dashboard observation:** Stale data, slow page loads.
- **Pod health events:** Restarts, OOM kills, readiness probe failures.

---

## Service Health Check Procedure

Every service exposes three health endpoints:

| Endpoint | Checks | Action if Failing |
|---|---|---|
| `/startup` | Config loaded, initial connections established | Pod won't receive traffic until passing |
| `/ready` | Can reach SQL, Service Bus, Cosmos DB | Pod removed from service endpoints (no traffic) |
| `/healthz` | Process alive, not deadlocked | Pod killed and restarted by kubelet |

```bash
# Check if pods are ready
kubectl get pods -n carebridge-app -o wide

# Check specific pod health
kubectl exec <pod> -n carebridge-app -- curl -s localhost:8080/ready
kubectl exec <pod> -n carebridge-app -- curl -s localhost:8080/healthz
```

---

## Common Degradation Scenarios

### Azure SQL Connection Pool Exhaustion

**Symptoms:** Elevated latency across SQL-backed services, timeout errors in logs.

**Investigation:**
1. Check SQL DTU utilization on Grafana SQL Health dashboard.
2. Check active connections:
   ```kql
   AzureMetrics
   | where ResourceProvider == "MICROSOFT.SQL"
   | where MetricName == "connection_successful"
   | summarize sum(Total) by bin(TimeGenerated, 5m)
   ```
3. Check for long-running queries or deadlocks.

**Mitigation:**
- If a specific query is expensive: identify and optimize it.
- If connection pool is saturated: restart affected service pods to release stale connections.
- Scale elastic pool DTUs if load is legitimately higher.

### Cosmos DB RU Throttling

**Symptoms:** Audit Service or Reporting Service slow. 429 responses in logs.

**Investigation:**
1. Check Cosmos DB dashboard → RU consumption and 429 rate.
2. Check which container is being throttled (audit-events or read-models).
3. Check for cross-partition queries (expensive).

**Mitigation:**
- If serverless: Cosmos auto-scales, but burst limits exist. Reduce request rate or optimize query.
- If query pattern changed: check for missing partition key in queries.
- If sustained load: consider switching from serverless to provisioned autoscale.

### Service Bus Consumer Lag

**Symptoms:** Read models are stale. DLQ not growing, but queue depth is growing.

**Investigation:**
1. Check Service Bus Health dashboard → queue depth per subscription.
2. Check KEDA ScaledObject status: is it scaling the consumer?
   ```bash
   kubectl get scaledobject -n carebridge-app
   kubectl get hpa -n carebridge-app
   ```
3. Check consumer pod logs for processing errors or slow dependency calls.

**Mitigation:**
- If KEDA is not scaling: check ScaledObject configuration and Service Bus connection.
- If pods are at max replicas: increase `maxReplicaCount`.
- If processing is slow: check downstream dependency latency (SQL, Cosmos).

### Memory Pressure / OOM Kill

**Symptoms:** Pod restarts with exit code 137. OOM kill events.

**Investigation:**
```bash
kubectl describe pod <pod> -n carebridge-app | grep -A5 "Last State"
kubectl top pods -n carebridge-app
```

**Mitigation:**
- Increase memory limits in Helm values.
- Check for memory leaks (growing memory over time without release).
- Check if the service is caching too aggressively.

### High CPU

**Symptoms:** High latency, HPA scaling to max replicas.

**Investigation:**
```bash
kubectl top pods -n carebridge-app
kubectl get hpa -n carebridge-app
```

**Mitigation:**
- If legitimate load: increase `maxReplicas` or node pool size.
- If CPU spike without load increase: check for busy-wait loops, inefficient queries, or excessive logging.

---

## Mitigation Actions

| Action | When | Command |
|---|---|---|
| Restart a service | Pod is stuck, connections stale | `kubectl rollout restart deployment/<service> -n carebridge-app` |
| Scale up manually | HPA at max but load is legitimate | `kubectl scale deployment/<service> --replicas=N -n carebridge-app` |
| Disable non-critical features | Core workflow must be preserved | Feature flags via ConfigMap update |
| Redirect to cached data | Read model is stale but available | BFF serves cached dashboard (if implemented) |

---

## When to Escalate

- Service degradation persists > 30 minutes after initial mitigation.
- Multiple services degraded simultaneously (suggests infrastructure issue).
- Azure-side service health incident affecting the region.
- Root cause unclear after 1 hour of investigation.

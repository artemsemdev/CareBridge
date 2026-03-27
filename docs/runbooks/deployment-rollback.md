# Deployment Rollback

**Document type:** Operational runbook
**Last updated:** 2026-03-27

---

## When to Rollback

Rollback when a deployment causes:
- Error rate increase (> 5% above baseline) after deploy.
- Health check failures (pods not becoming ready).
- Broken functionality confirmed by smoke tests or user reports.
- Service Bus DLQ spike correlated with deployment time.

**Do NOT rollback if:**
- The issue existed before the deployment (check error rate timeline).
- The issue is caused by an external dependency (Azure outage, DNS issue).
- A fix-forward is faster and safer than rolling back (e.g., config change).

---

## Pre-Rollback Checks

1. **Confirm the deployment is the cause:**
   ```bash
   # Check when the last deployment happened
   helm history <service> -n carebridge-app | tail -5

   # Check error rate timeline — did errors start after deploy?
   # → Grafana Platform Overview dashboard
   ```
2. **Check if other services were deployed at the same time.** Multiple services deployed simultaneously make root cause harder to identify.
3. **Check if a database migration was applied.** If yes, see [Database Migration Rollback](#database-migration-rollback).

---

## Rollback Procedure

### Step 1: Identify the Target Revision

```bash
# View deployment history
helm history case-service -n carebridge-app

# Output:
# REVISION  STATUS      DESCRIPTION
# 5         superseded  Upgrade complete
# 6         deployed    Upgrade complete    ← current (broken)
# Target: revision 5
```

### Step 2: Execute Rollback

```bash
helm rollback case-service 5 -n carebridge-app
```

### Step 3: Verify Rollback

```bash
# Check pod status
kubectl get pods -l app=case-service -n carebridge-app

# Confirm image version reverted
kubectl get deployment case-service -n carebridge-app \
  -o jsonpath='{.spec.template.spec.containers[0].image}'

# Check health endpoints
kubectl exec <pod> -n carebridge-app -- curl -s localhost:8080/ready

# Monitor error rate on Grafana — should return to baseline
```

### Step 4: If Helm Rollback Fails

```bash
# Manual image rollback
kubectl set image deployment/case-service \
  case-service=acrcarebridge.azurecr.io/case-service:<previous-tag> \
  -n carebridge-app

# Wait for rollout
kubectl rollout status deployment/case-service -n carebridge-app
```

---

## Database Migration Rollback

EF Core migrations in CareBridge follow a **forward-only convention:**

- Migrations must be backward-compatible. New columns are nullable or have defaults. Old columns are not removed in the same release as the code change.
- This means the previous code version can run against the new database schema.

**If a migration breaks backward compatibility (rare):**
1. Do NOT run `dotnet ef database update <previous-migration>` in production.
2. Create a new migration that reverses the breaking change.
3. Deploy the reversal migration as a forward fix.
4. Fix the root cause and deploy correctly.

**Why not reverse migrations:**
- Reverse migrations may drop columns or data. Too risky for production.
- Forward fixes are safer and auditable.

---

## Service Bus Consumer Compatibility

After rollback, verify:
1. The rolled-back consumer version can still process messages that were published by the newer version during the failed deployment.
2. Check DLQ after rollback — if new messages can't be deserialized by the old consumer, they'll DLQ.
3. If the new deployment published events with a new schema version, the old consumer must either handle the new version or those messages will DLQ (inspect and replay after fixing).

---

## Post-Rollback Actions

1. **Notify the team.** Include: what was rolled back, why, and current system status.
2. **Investigate root cause.** What caused the deployment to fail?
3. **Fix forward.** Fix the issue, add a regression test, and redeploy.
4. **Check DLQ.** Replay any messages that failed during the bad deployment or rollback.
5. **Update deployment process** if the failure reveals a gap (missing smoke test, insufficient validation gate).

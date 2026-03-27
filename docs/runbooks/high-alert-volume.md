# High Alert Volume

**Document type:** Operational runbook
**Last updated:** 2026-03-27

---

## Overview

The Care-Gap Engine generates alerts when it detects abnormal readings or missed milestones. Under normal conditions, alert volume correlates with case count and observation rate. A sudden spike in alert volume indicates either a legitimate clinical pattern (bulk discharge), a configuration error, or a system bug.

---

## Possible Causes

| Cause | Indicators |
|---|---|
| **Bulk discharge import** | High case creation rate precedes alert spike. Expected behavior. |
| **Threshold misconfiguration** | Alert spike follows a recent rule/threshold change. Many alerts of the same rule ID. |
| **Rule evaluation bug** | Single rule producing alerts for all cases, or duplicate alerts per observation. |
| **Observation data quality issue** | Many observations failing validation but a subset triggering rules with extreme values. |
| **Duplicate event processing** | Same observation processed multiple times, triggering multiple alerts. |

---

## Impact

When alert volume spikes:
1. **Task Service overwhelmed** — creates thousands of tasks, saturates coordinator work queues.
2. **Notification Service queue backup** — sends excessive notifications, may hit provider rate limits.
3. **Coordinator dashboard flooded** — open alert count becomes unmanageable.
4. **Service Bus queue depth grows** — downstream consumers can't keep pace.

---

## Investigation Steps

1. **Check alert creation rate** on Business Metrics dashboard. Is it 10x+ normal?
2. **Check which rule(s) are firing:**
   ```kql
   AppTraces
   | where Properties.serviceId == "caregap-engine"
   | where Properties.action == "AlertRaised"
   | summarize count() by tostring(Properties.ruleId)
   | top 10 by count_
   ```
3. **Check for recent rule configuration changes.** Were thresholds lowered?
4. **Check observation ingestion rate.** Did a bulk import flood the pipeline?
5. **Check for duplicate events.** Are the same observations triggering multiple alerts?
   ```kql
   AppTraces
   | where Properties.serviceId == "caregap-engine"
   | where Properties.observationId != ""
   | summarize count() by tostring(Properties.observationId)
   | where count_ > 1
   ```

---

## Mitigation

### Immediate: Pause Care-Gap Engine Processing
```bash
# Scale down the Care-Gap Engine to stop generating new alerts
kubectl scale deployment/caregap-engine --replicas=0 -n carebridge-app
```

This stops new alerts while preserving unprocessed messages in Service Bus. Messages will wait and be processed when the engine is restored.

### If Threshold Misconfiguration
1. Identify the misconfigured rule.
2. Update the threshold via admin API or direct SQL update.
3. Scale the Care-Gap Engine back up.

### If Duplicate Events
1. Check Observation Service for dedup failures.
2. Fix the dedup logic.
3. Deploy the fix.
4. Scale the Care-Gap Engine back up.

### Manage Downstream Impact
- If Task Service is overwhelmed: scale up Task Service pods temporarily.
- If Notification Service is flooding: scale down Notification Service to 0 temporarily, then address the notification backlog after alert volume stabilizes.
- Noise alerts can be bulk-dismissed via a maintenance script.

---

## Recovery

1. Restore the Care-Gap Engine: `kubectl scale deployment/caregap-engine --replicas=1 -n carebridge-app`
2. Monitor alert generation rate — should return to normal levels.
3. Verify Service Bus queue depth returns to near zero.
4. Check DLQ for any messages that failed during the spike.
5. Review coordinator dashboard — bulk-dismiss noise alerts if needed.

---

## Prevention

- **Rate limiting on the Care-Gap Engine:** Max alerts per case per hour. If exceeded, log a warning instead of creating more alerts.
- **Alert on alert rate:** Grafana alert when `carebridge_alerts_raised_total` exceeds 5x the trailing 24h average.
- **Rule change audit:** All threshold and rule changes are logged in the audit trail with the previous and new values.
- **Staged rule deployment:** New rules deployed to dev/staging first, monitored before production activation.

# Failed Message Processing

**Document type:** Operational runbook
**Last updated:** 2026-03-27

---

## Overview

Service Bus messages can fail during processing and end up in dead-letter queues (DLQ). This runbook covers how to identify, investigate, and resolve failed messages. See [Observability — Dead-Letter Queue Visibility](../architecture/observability.md#dead-letter-queue-visibility) for monitoring details.

---

## Identifying Failed Messages

1. **Grafana alert:** "DLQ messages detected" fires when any subscription DLQ count > 0.
2. **Service Bus Health dashboard:** Shows DLQ count per topic/subscription.
3. **Azure Portal:** Service Bus → Topic → Subscription → Dead-letter queue → Peek.
4. **Azure CLI:**
   ```bash
   az servicebus topic subscription show \
     --resource-group rg-carebridge-prod \
     --namespace-name sb-carebridge-prod \
     --topic-name observation-events \
     --name caregap-engine-sub \
     --query "countDetails.deadLetterMessageCount"
   ```

---

## Common Failure Reasons

| Reason | Description | Typical Fix |
|---|---|---|
| **Deserialization error** | Message body doesn't match expected schema | Fix producer schema or consumer deserializer. Replay after fix. |
| **Downstream service unavailable** | Consumer can't reach SQL, Cosmos DB, or another dependency | Wait for dependency recovery. Messages will auto-retry. DLQ only after max retries. |
| **Business rule violation** | Message contains data that violates a constraint (e.g., duplicate key) | Inspect data, fix constraint or dedup logic, replay. |
| **Poison message** | Message that will never succeed regardless of retries | Inspect, log, discard with audit note. |
| **Schema version mismatch** | Producer sent a newer schema version the consumer doesn't understand | Deploy updated consumer, then replay. |

---

## Investigation Steps

1. **Peek at DLQ messages** in Azure Portal or via Service Bus Explorer.
2. **Check DLQ message properties:**
   - `DeadLetterReason` — why it was dead-lettered
   - `DeadLetterErrorDescription` — error from last processing attempt
   - `correlationId` — trace back to the originating event
   - `eventType` — which event type failed
3. **Search logs by correlationId:**
   ```kql
   AppTraces
   | where Properties.correlationId == "corr-001"
   | where SeverityLevel >= 3
   | order by TimeGenerated desc
   ```
4. **Check if the issue is transient or permanent:**
   - Transient: dependency was briefly unavailable. Messages may be replayable now.
   - Permanent: schema mismatch, poison data. Requires code fix before replay.

---

## Resolution Procedures

### Fix and Replay
1. Identify and fix the root cause (consumer bug, schema issue, dependency fix).
2. Deploy the fix.
3. Replay DLQ messages using Service Bus Explorer (Azure Portal) or custom replay tool.
4. Verify: no new DLQ messages, consumer logs show successful processing.
5. Confirm consumer idempotency — replay may process messages that partially succeeded before DLQ.

### Discard with Audit Note
For messages that are permanently invalid (corrupt data, irrelevant events):
1. Document the message ID, event type, and reason for discard.
2. Complete (remove) the message from DLQ.
3. Log the discard decision in the operations log.

### Fix Consumer Bug and Redeploy
1. Reproduce the failure locally using the DLQ message content.
2. Fix the consumer code.
3. Add a test case for the failure scenario.
4. Deploy and replay.

---

## Replaying DLQ Messages

```bash
# Using Azure CLI to receive and re-send DLQ messages:
# Note: Custom tooling recommended for production use.

# Peek at DLQ messages (non-destructive):
az servicebus topic subscription peek-lock --dlq \
  --resource-group rg-carebridge-prod \
  --namespace-name sb-carebridge-prod \
  --topic-name alert-events \
  --subscription-name task-service-sub

# Replay: receive from DLQ, send to main topic, complete DLQ message
# (Use Service Bus Explorer tool or custom script)
```

**Before replaying:**
- Confirm the root cause is fixed.
- Confirm consumer idempotency handles duplicates.
- Replay in small batches, monitoring for new errors.

---

## Prevention

- **Schema validation** in consumers before processing. Reject early with clear errors.
- **Contract tests** in CI that validate producer output matches consumer expectations.
- **Circuit breakers** on downstream calls to prevent retry storms against a failing dependency.
- **Monitoring** with alerting on DLQ > 0 to catch failures early.

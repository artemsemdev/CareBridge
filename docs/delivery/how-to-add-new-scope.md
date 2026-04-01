# How to Add New Scope in CareBridge

**Document type:** Delivery governance guide
**Status:** Active
**Created:** 2026-04-01
**Scope:** How to introduce new work into the CareBridge MVP without corrupting the existing delivery baseline

---

## 1. Why This Document Exists

CareBridge already has a defined architecture, an execution plan, epic-level delivery files, and a partially completed implementation. Once a project reaches this level of structure, new work should not be added informally. Silent scope drift makes the roadmap unreliable, weakens the architecture narrative, and makes it hard to explain what was planned versus what was added later.

This document defines the standard way to add new scope while keeping the project historically accurate, technically coherent, and easy to maintain.

---

## 2. Core Rule

**Do not rewrite history.**

If a capability was not part of the original baseline, do not quietly insert it into old completed work as if it had always been there. Add it as a visible, explicit change to the delivery plan.

In practice, this means:

- Fix gaps in already-defined acceptance criteria inside the existing issue or epic.
- Add truly new functionality as new scope.
- Record architectural changes explicitly when they affect service boundaries, contracts, storage, or operational assumptions.

---

## 3. Scope Classification

Before changing any delivery document, classify the work.

### 3.1 Defect or Completion Gap

Use this when the implementation does not satisfy an already-defined requirement.

Examples:

- An acceptance criterion in an existing epic is still not met
- A previously implemented endpoint is broken
- A UI flow does not match the documented behavior

**Recommendation:** Keep the work inside the existing issue, epic, or follow-up bug entry. Do not create a brand-new epic unless the fix expands into new capability.

### 3.2 Scope Extension

Use this when the project is gaining capability that was not part of the original plan.

Examples:

- A new reporting surface
- A new operational workflow
- A new admin tool
- A new service API that was not in the original delivery files

**Recommendation:** Add a new issue set and usually a new epic if the work is meaningful, cross-cutting, or spans multiple slices.

### 3.3 Architectural Change

Use this when the work changes how the system is designed rather than simply adding a feature.

Examples:

- Replacing in-memory storage with a persistent store
- Changing event contracts
- Splitting or merging services
- Changing the gateway aggregation model

**Recommendation:** Update delivery documents and the relevant architecture documents. Create or update an ADR if the decision changes a long-lived technical direction.

### 3.4 Hardening or Operational Improvement

Use this when the work improves reliability, observability, security, performance, or deployment maturity rather than product capability.

Examples:

- OpenTelemetry
- Circuit breakers
- Retry policy changes
- Structured logging
- Smoke tests or end-to-end test automation

**Recommendation:** Put it in the current hardening wave if it fits the existing plan. If it is outside the current plan, add a new issue or epic under the appropriate later wave.

---

## 4. Decision Framework

Use the smallest container that preserves clarity.

| Situation | Recommended home |
|---|---|
| Existing requirement is incomplete or broken | Existing issue or epic |
| Small new addition tightly related to an unfinished epic | New issue inside that epic |
| New feature area with multiple related issues | New epic |
| Cross-cutting change that affects roadmap assumptions | New epic plus architecture updates |
| Idea not yet approved for delivery | Backlog note or proposal, not the main plan yet |

If you are unsure, prefer creating explicitly new scope instead of silently expanding a completed epic.

---

## 5. Standard Workflow for Adding New Scope

### Step 1: Write the change in one sentence

State the proposed work as a crisp outcome, not a vague theme.

Good:

- "Add role-based audit export for platform administrators."
- "Add Playwright-based smoke coverage for critical UI flows."

Weak:

- "Improve quality."
- "Make the app more enterprise."

### Step 2: Decide whether it is baseline work or new scope

Ask:

- Was this already promised in an epic or acceptance criterion?
- Does the current README or delivery plan already claim this exists?
- Does implementing it change the architecture or operating model?

If the answer points to "not previously planned," treat it as new scope.

### Step 3: Choose the planning artifact

For CareBridge, use one of these:

- **Existing issue / epic** for a gap in committed work
- **New issue in an existing epic** for a small adjacent addition
- **New epic file in `docs/delivery/epics/`** for substantial new scope
- **Backlog or proposal note** if the work is not ready to enter the committed plan

Recommended default for meaningful new feature work:

- Create a new epic file under [`docs/delivery/epics/`](/Users/artemsemenov/Desktop/Github/CareBridge/docs/delivery/epics)

### Step 4: Define the work before coding

At minimum, document:

- Why the work exists
- Value delivered
- Dependencies
- Out of scope
- Exit criteria
- Delivery slices
- Execution order
- Individual issues with acceptance criteria

This keeps implementation from drifting and makes later verification possible.

### Step 5: Update the delivery baseline

If the work is officially added, update the summary documents:

- [`docs/delivery/delivery-plan.md`](/Users/artemsemenov/Desktop/Github/CareBridge/docs/delivery/delivery-plan.md)
- [`docs/delivery/README.md`](/Users/artemsemenov/Desktop/Github/CareBridge/docs/delivery/README.md)

Update these only after the new scope has a clear home. The dashboard should summarize scope, not invent it.

### Step 6: Update architecture documents when needed

If the new work changes service responsibilities, APIs, events, storage, security posture, or deployment assumptions, also update the relevant architecture docs, such as:

- [`docs/architecture/solution-architecture.md`](/Users/artemsemenov/Desktop/Github/CareBridge/docs/architecture/solution-architecture.md)
- [`docs/architecture/service-catalog.md`](/Users/artemsemenov/Desktop/Github/CareBridge/docs/architecture/service-catalog.md)
- ADRs in [`docs/adr/`](/Users/artemsemenov/Desktop/Github/CareBridge/docs/adr)

### Step 7: Implement and verify

When the work is implemented, verification should include:

- targeted automated tests
- build/test commands
- any required smoke checks
- evidence mapped back to acceptance criteria

### Step 8: Update documentation after completion

Once delivered, remove stale wording such as "planned," "future," or "scaffold only" from user-facing docs that are no longer accurate.

---

## 6. Required Writing Standard for New Scope

Every new scope addition should be specific enough that another engineer could implement it without inventing the missing half of the requirement.

At minimum, each issue should answer:

- What exactly will exist after this work?
- What will not exist yet?
- What depends on it?
- How will we know it is done?

If those answers are missing, the work is not ready to enter the committed delivery plan.

---

## 7. When to Create a New Epic

Create a new epic when most of the following are true:

- The work delivers a distinct user or platform capability
- It spans multiple issues or slices
- It touches multiple layers such as service, gateway, UI, and docs
- It would make an existing epic feel artificially bloated
- It changes milestone or wave planning

In CareBridge, a new epic is usually the right choice for any substantial addition after the original MVP scope was defined.

---

## 8. When to Extend an Existing Epic Instead

Extend an existing epic only when the new work is genuinely adjacent to that epic's original purpose and does not distort the historical record.

Reasonable cases:

- A missing follow-up issue required to finish the epic cleanly
- A small scope correction discovered during implementation
- A narrow enhancement that belongs naturally to an epic still in progress

Poor cases:

- Adding a new major feature area to a completed epic
- Reframing old work so a later idea looks like it was always planned

---

## 9. Document Update Order

When introducing new committed scope, update documents in this order:

1. Create or update the epic-level source-of-truth document
2. Update the delivery plan
3. Update the delivery dashboard
4. Update architecture and ADR documents if required
5. Update top-level README only when the implementation actually exists

This order keeps summary documents downstream from the source of truth.

---

## 10. Recommended Change Checklist

Before implementation:

- The work is classified correctly
- The new scope has a documented home
- Acceptance criteria are explicit
- Dependencies and out-of-scope items are written down
- Architecture impact has been reviewed

After implementation:

- Code matches the documented scope
- Tests and verification are recorded
- Delivery documents reflect reality
- User-facing docs no longer describe completed work as planned

---

## 11. Practical Examples

### Example A: Add Playwright Smoke Tests

Classification:

- Hardening / operational improvement

Recommended approach:

- Add a new issue under the hardening wave, unless a testing epic is introduced later
- Update the delivery plan and dashboard only after deciding that it is committed work
- Document scope narrowly, for example: dashboard load, case detail load, audit page filter smoke

### Example B: Add FHIR Export for Case Summaries

Classification:

- Scope extension plus architecture change

Recommended approach:

- Create a new epic
- Update service catalog, solution architecture, and possibly an ADR
- Do not quietly append it to a completed MVP epic

### Example C: Fix an Existing Audit Filter That Is Broken

Classification:

- Defect or completion gap

Recommended approach:

- Keep it within the Audit epic or a follow-up bug
- Do not create a new epic unless the fix expands the product scope

---

## 12. Final Guidance

The delivery plan should behave like a contract with your future self. It can evolve, but it should evolve transparently.

When adding new work in CareBridge:

- classify it first
- give it an explicit planning home
- update source-of-truth docs before implementation
- verify against written acceptance criteria
- update summary docs only after the implementation is real

That discipline is what keeps the project believable as an engineering artifact rather than just a pile of code and ideas.

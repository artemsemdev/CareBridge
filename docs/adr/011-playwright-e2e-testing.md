# ADR-011: Playwright for End-to-End Testing

**Status:** Accepted
**Date:** 2026-04-01
**Deciders:** Solution architect

## Context

CareBridge has a mature .NET unit testing layer (xUnit across seven service test projects) but no end-to-end (E2E) or frontend testing infrastructure. The React TypeScript SPA (see [ADR-009](./009-react-bff-frontend.md)) is served through an ASP.NET Core BFF, and the application's core value -- care coordination workflows like discharge follow-up, care plan progression, and gap closure -- spans multiple UI views, API calls through the BFF, and downstream microservice interactions. Unit tests validate individual service logic in isolation; they cannot verify that a care coordinator can navigate from a patient dashboard to a care plan, complete a task, and see the resulting state change reflected across the UI.

The project also lacks frontend component-level testing. The question is whether to introduce a dedicated component testing tool (Vitest + Testing Library) alongside a separate E2E tool, or to adopt a single tool that covers both browser-based integration testing and full E2E workflows.

Three mature options exist for browser-based E2E testing in the JavaScript/TypeScript ecosystem: Playwright, Cypress, and Selenium WebDriver. The choice affects developer experience, CI performance, debugging capability, and how well the tool integrates with the existing Vite + React + TypeScript stack and the .NET backend testing infrastructure.

## Decision

CareBridge will use **Playwright** (via `@playwright/test`) as the E2E and browser integration testing framework for the React frontend.

### Why Playwright

**Multi-browser coverage with a single API.** Playwright ships Chromium, Firefox, and WebKit browser engines. Tests run against all three with a single configuration change. For a healthcare coordination platform where users may be on institutional browsers (often Edge/Chromium or legacy Firefox), verifying cross-browser behavior without maintaining separate test configurations is valuable.

**Built-in test runner and assertions.** `@playwright/test` includes a test runner, assertion library with auto-retrying matchers, fixtures, and parallelism -- no need to assemble a test framework from multiple packages. This reduces dependency surface and configuration overhead for a project that currently has zero frontend testing infrastructure.

**Component testing support.** Playwright's experimental component testing (`@playwright/experimental-ct-react`) can mount individual React components in a real browser context. This offers a path to component-level testing without adding Vitest + Testing Library as a parallel tool. While the component testing API is still experimental, the fallback is straightforward: use Playwright for E2E and add Vitest later if component-level isolation becomes necessary.

**Network interception and API mocking.** Playwright's `page.route()` API intercepts network requests at the browser level. This enables testing the React SPA against mocked BFF responses without running the full backend, which is critical for fast feedback loops during frontend development. For full-stack E2E tests, the same tests can run against the real BFF and backend services.

**Trace viewer and debugging.** Playwright's trace viewer records a timeline of actions, screenshots, network requests, and console logs for each test. When a test fails in CI, the trace artifact provides a complete replay of what happened. For a project with no existing E2E coverage, the ability to diagnose failures without reproducing them locally significantly reduces the cost of maintaining tests.

**Codegen and tooling.** The `playwright codegen` command launches a browser and records user interactions as test code. For a project bootstrapping E2E coverage from zero, this accelerates initial test authoring for key workflows (patient search, care plan creation, task completion) by generating a working skeleton that developers refine.

### Test Architecture

E2E tests will live in `tests/e2e/` at the repository root, alongside the existing `tests/unit/`, `tests/integration/`, and `tests/contract/` directories:

```
tests/
  unit/                          # existing .NET xUnit projects
  integration/                   # reserved for .NET integration tests
  contract/                      # reserved for contract tests
  e2e/
    playwright.config.ts
    package.json                 # separate from frontend package.json
    tests/
      workflows/
        discharge-followup.spec.ts
        care-plan-progression.spec.ts
        gap-closure.spec.ts
      smoke/
        login.spec.ts
        navigation.spec.ts
    fixtures/
      auth.fixture.ts            # authenticated session setup
    helpers/
      api-mocks.ts               # BFF response mocks for isolated UI tests
```

The E2E test package is separate from the frontend `src/web/carebridge-ui/` package to maintain a clear boundary: the frontend is a build artifact, the E2E tests are a verification layer that runs against the deployed artifact.

### Test Modes

1. **Isolated UI tests:** Playwright intercepts BFF API calls and returns mocked responses. Tests verify React component behavior, routing, form validation, and UI state transitions without backend dependencies. Fast, deterministic, suitable for PR checks.

2. **Full-stack E2E tests:** Playwright drives the browser against the real BFF and backend services running via `docker-compose`. Tests verify complete workflows end-to-end: patient lookup, care plan creation, task assignment, notification rendering. Slower, environment-dependent, suitable for nightly or pre-release gates.

### CI Integration

Playwright tests will run in GitHub Actions using the official `mcr.microsoft.com/playwright` Docker image, which includes all browser dependencies pre-installed. Isolated UI tests run on every PR. Full-stack E2E tests run on merges to `master` or on a nightly schedule once Docker Compose-based test environments are available.

Test artifacts (traces, screenshots, video on failure) are uploaded as GitHub Actions artifacts for debugging.

## Consequences

**Positive:**

- The project gains browser-based testing for the first time, covering workflows that unit tests cannot verify: multi-page navigation, form submission, optimistic UI updates via React Query, and cross-service state consistency visible in the UI.
- A single tool (Playwright) covers E2E testing, smoke testing, and potentially component testing, avoiding the configuration and dependency overhead of maintaining multiple browser testing tools.
- Cross-browser testing against Chromium, Firefox, and WebKit is available without additional infrastructure or configuration. This is relevant for healthcare environments where browser standardization is inconsistent.
- The `tests/e2e/` location with a separate `package.json` keeps E2E dependencies (browser binaries, test utilities) isolated from the frontend production build, avoiding bloat in the SPA bundle.
- Trace viewer artifacts provide a complete debugging record for CI failures, reducing the time spent reproducing issues locally.
- Network interception enables fast, deterministic UI tests that run without backend services, suitable for PR-level feedback.

**Negative:**

- Playwright's component testing (`@playwright/experimental-ct-react`) is still experimental. If it does not stabilize, the project may need to add Vitest + Testing Library for component-level isolation testing, resulting in two frontend testing tools.
- E2E tests are inherently more brittle than unit tests. UI changes (element selectors, page structure, copy changes) can break tests that are not written with resilient locator strategies. The team must adopt Playwright's recommended locator patterns (role-based, text-based, test-id) from the start.
- Full-stack E2E tests require the complete Docker Compose environment (SQL Server, RabbitMQ, backend services, BFF) to be running. This environment setup adds CI time and complexity. Until a containerized test environment is automated, full-stack tests are limited to local execution.
- Browser binary downloads (~400 MB for all three engines) add time to CI setup and local `npm install`. The `playwright install --with-deps` step must be cached in CI to avoid repeated downloads.
- Playwright tests execute in Node.js, adding a JavaScript testing runtime to a project whose backend tests are entirely in .NET. Developers must context-switch between xUnit conventions and Playwright conventions, though the boundary is well-defined (backend logic vs. UI behavior).

## Alternatives Considered

**Cypress:** Cypress is a mature E2E testing framework with strong developer experience, an interactive test runner, and a large community. However, Cypress runs tests in a single browser engine (Chromium-based) by default; cross-browser support for Firefox and WebKit requires Cypress Cloud or additional configuration. Cypress also uses a custom test runner that does not support multi-tab or multi-origin scenarios, which may be relevant for OAuth flows in future authentication work. Cypress's network interception (`cy.intercept`) is comparable to Playwright's, but Playwright's multi-browser support and built-in parallelism better fit the project's cross-browser requirements.

**Selenium WebDriver:** Selenium is the most established browser automation framework and supports all major browsers. However, Selenium requires external browser drivers, has a more verbose API, lacks built-in assertions and test runner capabilities, and requires additional tooling (TestContainers, WebDriverManager) for CI integration. The developer experience for writing and debugging tests is significantly lower than Playwright or Cypress. Selenium is better suited for teams with existing Selenium infrastructure or requirements for languages beyond JavaScript/TypeScript.

**Vitest + Testing Library (component testing only):** Adding Vitest with `@testing-library/react` would provide fast, JSDOM-based component tests without a real browser. This covers component rendering, user interaction simulation, and hook testing. However, JSDOM does not execute real browser APIs (layout, navigation, intersection observers), and component tests cannot verify full-page workflows. Vitest would complement but not replace E2E testing. Starting with Playwright allows the project to cover the highest-value gap (E2E workflows) first and add component testing later if the coverage model requires it.

**Playwright for .NET (`Microsoft.Playwright`):** Playwright also provides a .NET binding, which would keep E2E tests in the same language as backend tests. However, the .NET binding has a smaller community, fewer examples, and lags behind the Node.js version in feature releases. The React frontend's tooling ecosystem (Vite, TypeScript, npm) aligns more naturally with the Node.js Playwright package, and the `@playwright/test` runner provides capabilities (component testing, fixture system) not available in the .NET binding.

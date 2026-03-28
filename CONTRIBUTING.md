# Contributing to CareBridge

Thanks for contributing. CareBridge is a portfolio-grade reference implementation of a healthcare care-coordination platform, and contributions should keep that scope clear, technically defensible, and safe to share publicly.

## Before You Start

- Read [README.md](README.md) for project context and architecture.
- Read [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) before participating.
- Read [SECURITY.md](SECURITY.md) before reporting vulnerabilities or handling sensitive material.
- Search existing issues and pull requests before starting duplicate work.
- Open an issue before large or cross-cutting changes so the direction can be aligned first.

## Scope and Guardrails

- This repository uses synthetic data only. Do not add real patient data, PHI, production credentials, or confidential customer information.
- Keep changes aligned with the stated project goals: cloud-native healthcare workflow design, operational maturity, and clear architectural boundaries.
- Avoid changing unrelated files in the same pull request.
- If you change APIs, contracts, infrastructure behavior, or architecture assumptions, update the relevant docs in `docs/`.

## Development Setup

Backend and infrastructure:

```bash
docker-compose up -d
dotnet run --project tools/db-migrator
dotnet run --project tools/synthetic-data-generator
dotnet test CareBridge.sln
```

Frontend:

```bash
cd src/web/carebridge-ui
npm install
npm run lint
npm run build
```

For additional setup details, see [docs/developer/local-development.md](docs/developer/local-development.md).

## Workflow

1. Fork the repository and create a focused branch such as `feature/...`, `fix/...`, or `docs/...`.
2. Make the smallest change that solves the problem cleanly.
3. Add or update tests when behavior changes.
4. Update documentation when commands, architecture, contracts, or user-visible behavior change.
5. Open a pull request with a clear description of what changed and how it was validated.

## Coding Expectations

- Preserve clear service boundaries. Shared code belongs in `src/shared/` only when the dependency is truly cross-cutting.
- Keep public contracts explicit and versionable. If an API or event contract changes, document it.
- Prefer small, reviewable commits and pull requests over broad refactors.
- Match the existing style of the area you touch instead of reformatting unrelated code.
- Do not introduce placeholder integrations, fake compliance claims, or misleading production-readiness statements.

## Pull Request Expectations

Each pull request should:

- Explain the problem being solved.
- Link the related issue when one exists.
- Describe validation performed, including commands run.
- Include screenshots or recordings for meaningful UI changes.
- Call out breaking changes, schema changes, or infrastructure changes explicitly.

## Review Process

- Maintainers may request changes before merging.
- Cross-cutting work may require updates to docs, tests, or ADRs before approval.
- Stale or out-of-scope pull requests may be closed to keep the repo maintainable.

## Reporting Security Issues

Do not open public issues for vulnerabilities. Follow [SECURITY.md](SECURITY.md) instead.

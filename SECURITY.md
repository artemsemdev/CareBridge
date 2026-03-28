# Security Policy

## Supported Versions

CareBridge is maintained as a reference implementation. Security fixes are only guaranteed for the latest state of the default branch.

| Version | Supported |
|---|---|
| Default branch (`master` at the time of writing) | Yes |
| Older commits and snapshots | No |

## Reporting a Vulnerability

Please do not report security vulnerabilities in public GitHub issues, pull requests, or discussions.

Use this process instead:

1. Use GitHub's private vulnerability reporting flow for this repository if the "Report a vulnerability" option is available.
2. If private reporting is not available, contact the repository maintainer privately through the contact options on [@artemsemdev](https://github.com/artemsemdev).
3. Include a concise description, impact, affected components, reproduction steps, and any proposed mitigation.

## What to Report

Please report issues such as:

- Exposed secrets, credentials, tokens, or insecure secret-handling patterns.
- Authentication, authorization, or access-control flaws.
- Vulnerabilities in APIs, infrastructure configuration, or dependency handling.
- Disclosure of sensitive data, including accidental inclusion of real patient data or personally identifiable information.
- Supply-chain risks that materially affect the repository or its build process.

## Response Expectations

- Initial acknowledgment target: within 5 business days.
- Triage target: within 10 business days after acknowledgment.
- Remediation timing depends on severity, exploitability, and maintainer availability.

## Disclosure Policy

- Please give maintainers reasonable time to investigate and prepare a fix before public disclosure.
- If a secret or credential is exposed, revoke or rotate it immediately when possible and include that detail in the report.
- Once the issue is understood and addressed, maintainers may publish a summary so users can assess impact and upgrade guidance.

## Scope Notes

- This repository is a synthetic-data-only reference project, but security issues still matter because the code and infrastructure patterns may be reused elsewhere.
- Low-signal reports without a plausible security impact may be closed after review.

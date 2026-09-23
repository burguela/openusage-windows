# Security Policy

> **About this fork:** [openusage-windows](https://github.com/burguela/openusage-windows) is an unofficial
> fork of OpenUsage. Report vulnerabilities in the Windows app (the tray app, packaging, and Windows-only
> code paths) privately through the fork's
> [security advisories](https://github.com/burguela/openusage-windows/security/advisories/new). The policy
> below is the original project's; use it for issues in the shared code that also affect the official app.

## Reporting a Vulnerability

If you find a security vulnerability in OpenUsage, please report it responsibly. Do not open a public issue.

### Preferred: GitHub Security Advisories

1. Go to the [Security Advisories page](https://github.com/robinebers/openusage/security/advisories/new)
2. Click "Report a vulnerability"
3. Fill in the details

This keeps the report private until a fix is released.

### Alternative: Email

Send details to [rob@robinebers.com](mailto:rob@robinebers.com) with the subject line "OpenUsage Security Report".

## What to Include

- Description of the vulnerability
- Steps to reproduce
- Affected versions
- Impact assessment (what can an attacker do?)

## Response Timeline

- Acknowledgment within 48 hours
- Assessment and plan within 7 days
- Fix released as soon as practical, depending on severity

## Scope

The following are in scope:

- The OpenUsage desktop application
- The built-in providers (credential handling, API calls)
- The local HTTP API
- Build and release infrastructure

The following are out of scope:

- Third-party provider APIs (report to the provider directly)
- Social engineering attacks
- Denial of service attacks

## Supported Versions

Only the latest release is supported with security updates.

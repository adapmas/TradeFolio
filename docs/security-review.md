# Security review (task 5.7)

Reviewed against spec section 16, on <date>. Scope: TradeService, Gateway, shared code, Docker Compose, git history.

## Scan results

| Check | Command | Result |
|---|---|---|
| `.env` not tracked, ignored, never committed | `git ls-files`, `git check-ignore`, `git log -- .env` | RESULT |
| Secret-looking strings in tracked files | `git grep` patterns (task 5.7 step 1b) | RESULT (test-only constants listed below) |
| Secrets in git history | `git log -S` | RESULT |
| Gitleaks full-history scan | docker `zricethezav/gitleaks` | RESULT (or "not run") |
| Logging calls reviewed | step 1d | RESULT; no passwords, tokens, keys or emails logged; sensitive-data logging not enabled |
| Raw SQL from strings | step 1e | RESULT (none; interpolated, parameterized APIs only) |
| Vulnerable / deprecated packages | `dotnet list package --vulnerable --include-transitive` | RESULT |
| Published ports bound to localhost | `docker-compose.yml` | RESULT |

Test-only constants found (not real secrets): LIST THEM HERE, for example the signing keys in the test projects.

## Findings and fixes

| ID | Finding | Severity | Status | Evidence |
|---|---|---|---|---|
| S-01 | No global exception handler: 500s were not in the standard shape, and Development showed a stack trace page | Medium | Fixed | `ApiExceptionHandler`, `ApiExceptionHandlerTests`, `ErrorHandlingApiTests` |
| S-02 | No request body size limit (Kestrel default 30 MB) | Medium | Fixed: 64 KB limit on gateway and service | manual check 5.7 |
| S-03 | `Server` header advertised the web server | Low | Fixed | manual check 5.7 |
| S-04 | JWT carried the user's email although no service uses it | Low | Fixed | `TokenContentTests` |
| S-05 | Temporary `/api/v1/ping` and `/ping/error` endpoints still exposed | Low | Fixed: removed | `ErrorHandlingApiTests` |
| S-06 | EF Core logged every SQL command at Information level | Low | Fixed: Warning | appsettings |
| S-07 | No hardening headers on gateway responses | Low | Fixed | `SecurityHeadersTests` |
| S-08 | ADD ANY NEW FINDING FROM THE SCANS HERE | | | |

## Accepted risks (known, deliberate for this project)

| ID | Risk | Why accepted / what would change it |
|---|---|---|
| A-01 | Registration answers 409 for an existing email, so email addresses can be probed | Common trade-off; limited by the strict login/register rate limit. A "check your email" flow would remove it |
| A-02 | Rate limits are per client IP and per user, with no per-account lockout | Credential stuffing from many IPs is not stopped. Lockout or captcha would be the next step |
| A-03 | Gateway and services share one symmetric signing key (HS256), so any service holding it could mint tokens | Fine for one team and one deployment. Production option: a separate auth service signing with a private key (RS256/ES256), services verify with the public key |
| A-04 | A disabled user's token stays valid for up to 15 minutes | Bounded by the short token lifetime. Revocation list or per-request status check would remove it |
| A-05 | Rate-limit counters live in memory in each gateway instance | A second gateway instance would have its own counters. Production: Redis-backed limits or limits at the load balancer |
| A-06 | TLS and HSTS are not terminated locally | In deployment, TLS ends at the load balancer (task 12.6), which also adds HSTS. The gateway must then trust forwarded headers to see client IPs. `AllowedHosts` must be set to the real host name per environment |

## Re-check before the final audit (12.8)

- [ ] Re-run all scans in this document
- [ ] `npm audit` once the frontend exists
- [ ] CI secret scan is in place (12.3)
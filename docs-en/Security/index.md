# Security review - September 2026

This page records the security review of the shared libraries performed on 2 September 2026,
the fixes that shipped with it, and the items that still need a human decision.

## Scope and method

- Every library under `src/` (25 projects, about 12 000 lines of C#), the test projects, the
  GitHub Actions workflows and the PowerShell automation scripts.
- Static review of all sources in five passes (payments core and providers, EF/Querying,
  MediatR/Email/ML, WebApi/ExceptionHandling, CI), each finding re-verified against the code
  before a fix was written.
- `dotnet list package --vulnerable --include-transitive`: **no vulnerable NuGet packages**
  (direct or transitive). `System.Linq.Dynamic.Core` is pinned above the CVE-2024-51417 fix.
- Every fix is covered by unit tests; the whole solution (build + 900+ tests) was green before commit.

## Findings and status

| # | Severity | Area | Finding | Status |
|---|----------|------|---------|--------|
| 1 | Critical | Payments (tests) | A production CashBill shop secret and matching signatures were committed in a test source file (unused test data). | **Removed** from source. Rotation and history purge require the owner - see [Action items](#action-items-for-the-repository-owner). |
| 2 | High | ExceptionHandling | Default provider returned the innermost exception message (SQL errors, hosts, paths) to anonymous callers as HTTP 400 in every environment. | **Fixed**: 500 + generic message; details only in Development or on explicit opt-in. |
| 3 | High | Payments (all providers) | Signature verification silently degraded to an unkeyed hash when the secret was missing, so anyone could forge a "valid" notification. | **Fixed**: fail-closed in all 9 providers (incl. Stripe). |
| 4 | High | Payments (CashBill) | Webhook error message echoed the expected signature (an oracle for forging notifications); legacy `TransactionStatusChange` performed no verification. | **Fixed**. |
| 5 | High | Payments (Revolut) | Signed payload used `v1:` instead of `v1.`; no timestamp check, so a captured webhook was replayable forever. | **Fixed**: correct format, 300 s tolerance (configurable), multi-signature rotation. |
| 6 | High | EntityFramework | Client-controlled `SortField` passed straight to Dynamic LINQ (navigation traversal, multi-column, schema disclosure); no upper bound on page size. | **Fixed**: identifier validation, allow-list overload, page bounds and overflow checks. |
| 7 | High | MediatR | Default cache key used `ToString()` on properties, so requests differing only in collection contents shared one entry (cross-user data). Logging scope serialized whole requests (secrets, images). | **Fixed**: content-hash keys; scope carries only type name + correlation id. |
| 8 | High | Email | Template variables inserted verbatim into HTML bodies (phishing links from a trusted sender); comma-separated recipient strings added extra recipients; SMTP TLS off by default. | **Fixed**: HTML-encoding by default (`{{{raw}}}` opt-out), single recipient, TLS on. |
| 9 | High | MediatR.ML | Training command read any directory and wrote any file path from the request; classification accepted unbounded, unvalidated bytes. | **Fixed**: paths confined to `TrainingRoot`/`ModelsRoot`; size and signature validation. |
| 10 | Medium | WebApi | Standard resilience handler retried POST/PATCH on every HttpClient, duplicating payments/orders after timeouts. | **Fixed**: unsafe-method retries disabled by default. |
| 11 | Medium | Payments | Non-constant-time signature comparison; case-sensitive header lookup (broken under HTTP/2); unescaped identifiers in gateway URLs; webhook results without a payment id. | **Fixed**: `WebhookSignature`, case-insensitive dictionaries, `PaymentIdentifier`, ids populated. |
| 12 | Medium | Payments (Przelewy24) | Notification/verify signatures computed over the wrong field sets (every genuine notification rejected). | **Fixed** to the documented field order. |
| 13 | Medium | EF UnitOfWork | Transaction filter committed when the action returned 4xx/5xx; commit-failure handling could leave a dangling transaction and hide the root cause. | **Fixed**. |
| 14 | Medium | Email.Office365 | Attachments returned still transfer-encoded and keyed by content type (crash on two attachments of one type). | **Fixed**. |
| 15 | Medium | CI/CD | Write-scoped tokens with persisted credentials, actions pinned to mutable tags, tag name interpolated into shell, NuGet publish without tests and with failures swallowed, unpinned pip installs. | **Fixed**: least-privilege permissions, SHA pins + Dependabot, env-var passing, tests before publish, `--skip-duplicate`, pinned mkdocs-material. |
| 16 | Medium | Payments (Tpay, Adyen) | Webhook verification does not match the vendor protocol (Tpay: form + md5sum + JWS; Adyen: HMAC over colon-separated fields in `additionalData`). Fails closed, so not exploitable, but non-functional. | **Documented**; hardened (fail-closed, constant time). Rewrite tracked in follow-ups. |

## What changed, by library

### Payments
- New `TailoredApps.Shared.Payments.Security.WebhookSignature` (constant-time `FixedTimeEquals`,
  `FixedTimeEqualsIgnoreCase`, `IsSecretConfigured`) and `PaymentIdentifier` (`IsSafe`, `EnsureSafe`).
- `PaymentWebhookRequest.Headers`/`Query` are case-insensitive regardless of the dictionary assigned;
  `TransactionStatusChangePayload.QueryParameters` is never `null`.
- All providers: fail-closed on empty secret/signature, constant-time comparison, `PaymentUniqueId`
  populated from the payload, `Processing` events returned as `Ignore`, identifiers validated before
  URL interpolation, `JsonDocument` disposed.
- CashBill: legacy path verifies the MD5 signature; error messages no longer contain the expected
  value; missing secret -> "Signature verification is not configured.".
- Revolut: `v1.{timestamp}.{body}`, `WebhookToleranceSeconds` (default 300), rotation signatures.
- Przelewy24: documented sign field sets with unescaped-unicode JSON; currency normalised once.
- PayU: SHA-1 supported, unknown algorithms rejected. PayNow: stable `Idempotency-Key`, `EXPIRED`.
- Stripe: missing `WebhookSecret`/header -> `Fail`; async payment events handled with ids.
- Adyen: missing `success` treated as failure; `merchantReference`/`pspReference` in results.
- HotPay: the signed service name is the one sent.

### ExceptionHandling
- `DefaultExceptionHandlingProvider`: validation -> 400; everything else -> 500 with
  `"An unexpected error occurred."`; constructor overloads `()`, `(IHostEnvironment)`, `(bool)`.
- Middleware uses `GetRequiredService`, skips writing when the response has started, clamps the
  status code to 400-599. `ExceptionOccuredResult` honours the model's code. Duplicate DI
  registration removed. Docs and agent prompt rewritten against the real API.

### WebApi
- `HttpClientSettings.RetryUnsafeHttpMethods` (default `false`) -> `Retry.DisableForUnsafeHttpMethods()`.

### MediatR
- `ICachePolicy.GetCacheKey` -> `{Type.FullName}:{SHA-256(JSON)}` via `CacheKeyGenerator`.
- `LoggingBehavior` no longer serializes the request; stack trace no longer concatenated into the
  message template. `CachingBehavior` uses structured log templates.

### Email
- `TokenReplacingMailMessageBuilder`: single-pass regex replacement, HTML-encoded `{{token}}`,
  raw `{{{token}}}`, `HtmlEncodeVariables` option, null-safe arguments.
- `DefaultMessageBuilder`: single-pass, longest-key-first, HTML-encoded by default (`(bool)` ctor).
- `SmtpEmailProvider`: one recipient only (`FormatException` on lists), `CatchAll` required when
  `IsProd = false`, `EnableSsl` defaults to `true`, proper Message-ID.
- `Office365EmailProvider`: attachments decoded and keyed by file name.

### MediatR.ML
- `ImageClassificationOptions.TrainingRoot`, `ModelsRoot`, `MaxImageBytes` (default 10 MB).
- `TrainImageClassificationModelCommandHandler` confines both paths (`ArgumentException` outside the
  roots, `InvalidOperationException` when roots are not configured).
- `ClassifyImageCommandHandler` validates presence, size and JPEG/PNG signature.
  JPEG detection accepts any `FF D8 FF` header.

### EntityFramework / Querying
- `QuerySortingExtensions`: sort field must be a plain identifier; `ApplySorting(..., allowedSortFields)`
  overloads; Dynamic LINQ parse errors rethrown as `ArgumentException` without type details.
- `PagingQuery<T>`: `MaxPageSize` (default 1000, static + per-instance), `Page >= 1`,
  `1 <= Count <= max`, overflow-safe skip.
- `TransactionFilterAttribute`: rollback on 4xx/5xx results; `AggregateException` when both commit and
  rollback fail. `UnitOfWork`: commit/rollback release the transaction in `finally`.

### CI/CD
- All workflows: `permissions: contents: read` by default, `persist-credentials: false`, actions
  pinned to commit SHAs (with version comments), `.github/dependabot.yml` for `github-actions`.
- `ci.yml`: build/test job separated from the `gh-pages` deploy job (only the latter has `contents: write`).
- `release.yml`: `dotnet test` before publishing, single `find`-based push with `--skip-duplicate`,
  API key via environment variable, `environment: nuget` (add required reviewers in repository settings).
- `release-notes.yml`: tag name passed through `env`, not interpolated into the script.
- `bump-deps.yml`: full test run before the PR is created.

## Action items for the repository owner

1. **Rotate the CashBill secret** for the shop named in the removed test data, in the CashBill
   merchant panel. The value has been in git history since commit `ed838d6`; rotation is the only
   reliable remedy. If the repository is or was public, also purge the history
   (`git filter-repo`) and force-push, which is a destructive operation that was deliberately not
   performed here.
2. The sandbox CashBill secret in `tests/TailoredApps.Shared.Payments.Tests/appsettings.json`
   is a test-environment credential; move it to user-secrets/CI secrets if the sandbox shop matters.
3. In repository settings, add required reviewers to the new `nuget` environment so tag pushes
   cannot publish packages without approval, and make the *Docs guard* and *Build & Test PR*
   checks required on `master`.
4. Configure `ImageClassification:TrainingRoot` / `ModelsRoot` in hosts that use the training
   command, and pass `allowedSortFields` to `ApplySorting` for endpoints listing sensitive entities.
5. Review consumers that relied on the old behaviours: 400 with exception message (now 500 generic),
   raw HTML in e-mail variables (now encoded; use `{{{token}}}`), SMTP without TLS (`EnableSsl` now
   `true`), multi-recipient strings in `SendMail`, POST retries via the WebApi resilience defaults.

## Follow-ups (not done in this change)

- Tpay: implement the real notification protocol (form body, `md5sum`, `X-JWS-Signature` with the
  Tpay root certificate). Adyen: HMAC over the documented colon-separated fields from
  `additionalData.hmacSignature`; `/sessions` returns `sessionData`, not a redirect URL.
- Transport failures (5xx, timeouts) are still reported as `PaymentStatusEnum.Rejected` by several
  providers; add an `Unknown` outcome so outages are never mistaken for refusals.
- `CachingBehavior` cannot distinguish a cache miss from a cached default value for value-type
  responses; change `ICache.GetAsync` to return a found flag.
- Amount conversion to minor units truncates and assumes two decimals; add a shared ISO-4217 helper.
- PayU creates a raw `HttpClient` per order; route it through the named client with
  `AllowAutoRedirect = false`. OAuth tokens (PayU, Tpay) are fetched on every call.
- EF: session-level `SET TRANSACTION ISOLATION LEVEL` is SQL-Server-only and redundant;
  multiple `AddUnitOfWork` registrations cross-wire contexts; audit collector skips proxied entities;
  `HasDefaultValue(DateTime.UtcNow)` bakes a constant into migrations.
- Package hygiene: `Nullable` off in most projects, no `PackageReadmeFile` in most packages, floating
  `x.*` versions without a lock file, `SourceRevisionId` from `UtcNow`, dead TFM conditions.
- `PagedAndSortedRequest` duplicates `PagedAndSortedQuery`; add `[Range]` attributes.

## Verifying the fixes locally

```bash
dotnet test TailoredApps.Shared.sln
python -m mkdocs build --strict --config-file mkdocs.yml
python -m mkdocs build --strict --config-file mkdocs-en.yml
dotnet list TailoredApps.Shared.sln package --vulnerable --include-transitive
```

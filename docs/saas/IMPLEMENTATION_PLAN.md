# SaaS Implementation Plan

> **Status:** Phase 0 deliverable (repository audit + plan). No application code was changed to produce it.
> **Date:** 2026-11-13 · **Baseline commit:** `5f94989` on `main`
> **Brief:** `CLAUDE_SAAS_ROADMAP.md` (product owner's SaaS brief)
> **Relation to other docs:** [`docs/PRODUCT_ROADMAP.md`](../PRODUCT_ROADMAP.md) and [`docs/ROADMAP.md`](../ROADMAP.md) remain the long-range product/AI vision. For the road to a **paid launch**, this plan takes precedence. Section 1.3 of `PRODUCT_ROADMAP.md` ("What needs improvement") is out of date: customers, products, payments, lifecycle, persisted totals, time zones, PDFs and the email outbox have all shipped since it was written.

---

## Contents

1. [Executive summary](#1-executive-summary)
2. [How this audit was done](#2-how-this-audit-was-done)
3. [Architecture and project structure](#3-architecture-and-project-structure)
4. [Verified feature inventory](#4-verified-feature-inventory)
5. [README claims vs. code](#5-readme-claims-vs-code)
6. [Security and multi-tenancy review](#6-security-and-multi-tenancy-review)
7. [Authentication and account lifecycle](#7-authentication-and-account-lifecycle)
8. [Financial correctness review](#8-financial-correctness-review)
9. [Subscription and billing readiness](#9-subscription-and-billing-readiness)
10. [Email, background jobs, files and PDFs](#10-email-background-jobs-files-and-pdfs)
11. [Data, migrations and indexes](#11-data-migrations-and-indexes)
12. [Deployment and operations readiness](#12-deployment-and-operations-readiness)
13. [North Macedonia / localization readiness](#13-north-macedonia--localization-readiness)
14. [Recommended MVP scope](#14-recommended-mvp-scope)
15. [Prioritized backlog](#15-prioritized-backlog)
16. [Implementation sequence](#16-implementation-sequence)
17. [Suggested database changes](#17-suggested-database-changes)
18. [Test strategy](#18-test-strategy)
19. [Dependencies and risks](#19-dependencies-and-risks)
20. [Open decisions and blocking questions](#20-open-decisions-and-blocking-questions)
21. [Recommended first slice](#21-recommended-first-slice)

---

## 1. Executive summary

**The core is in better shape than a typical "initial version".** Tenant isolation is enforced in four layers and backed by negative tests. The invoice domain has a proper Draft → Issued → Void state machine, gap-free numbering in a transaction, payments and credit notes with balance checks, optimistic concurrency and an audit trail. Email goes through a transactional outbox with retries and leases. The full test suite passes (267 .NET tests, 38 web tests).

**What stands between this and a paid launch is mostly the SaaS shell around the product, not the product itself:**

| # | Gap | Why it blocks a launch |
|---|---|---|
| 1 | **No account recovery.** Identity's `/forgotPassword` endpoint exists, but no email sender is registered for it (it silently does nothing) and the SPA has no reset page. | Users who forget their password are locked out without platform-admin help. |
| 2 | **Emails contain relative links.** The invitation email says `.../accept-invite?token=…` with no host. There is no public base URL setting. | Invitations sent by real email don't work. Every future email (reset, invoice, billing) needs absolute links too. |
| 3 | **Public endpoints are unprotected.** No rate limiting on sign-up, login, invitations or the coming password reset. No email verification. | Abuse (mass sign-ups, credential stuffing) and, once invoices can be emailed, spam sent under our domain. |
| 4 | **Production hosting isn't configured.** No forwarded-headers handling behind a proxy, no shared Data Protection key store, no security headers, migrations run on startup, no monitoring or error tracking, no backup/restore procedure, and jobs aren't safe across multiple instances. | Sessions break on redeploy or scale-out, audit IPs are wrong, and failures go unnoticed. |
| 5 | **Demo credentials ship in production.** The login page always shows "Try the demo account" (`demo@example.com` / `Demo123!`, hard-coded). `compose.yaml` seeds a **platform admin** `admin@example.com` / `Admin123!`, and nothing stops this in Production. | A compose-based deployment would have a publicly known platform-admin password. |
| 6 | **Subscriptions are "admin-managed only".** The plan model is sound, but read-only enforcement has gaps, tenants can't see their usage, there's no grace-period policy, no subscription history and no payment provider. | Can't sell or enforce plans transparently. |
| 7 | **No self-service data export or company closure.** The CSV export covers invoices only and needs a paid plan feature. | Required by the brief and by GDPR-style expectations. |
| 8 | **Invoices can't be emailed to customers.** The outbox, PDF storage and per-tenant email templates exist, but there's no "Send invoice" use case. | This is the core value a customer pays for. |

**Recommended path:** finish **Phase 1 (SaaS foundation)** in 8 small vertical slices (~4–6 developer-weeks). Launch paid pilots with a **documented manual billing process**, which the existing platform-admin subscription screen already supports. Integrate a payment provider in **Phase 2**, once the merchant country and entity are decided; Stripe availability for a North Macedonia entity must not be assumed. Add AI (Phase 3) and MCP (Phase 4) only after usage metering and rate limiting exist.

**Recommended first slice** ([§21](#21-recommended-first-slice)): *"Safe public entry points"*. It hides the demo login outside demo environments and refuses demo seeding in Production. It adds an absolute public base URL (fixing invitation links) and rate limiting on anonymous and auth endpoints, and it fixes CI so it runs on `main`. It's small and low-risk, and every later slice depends on it.

---

## 2. How this audit was done

- Read every source file in `Domain`, `Application`, `Infrastructure` and `Api` (excluding migration designer files), the web app's router, API client, auth and formatting code, and the relevant pages. Also read the test project structure and the isolation, PDF, payment, credit note and outbox tests, plus `Dockerfile`, `compose.yaml`, CI, `appsettings*.json`, the README and both existing roadmap documents.
- Cross-checked the README's claims against code paths (§5).
- **Baseline test run (2026-11-13, local, Windows):**

| Command | Result |
|---|---|
| `dotnet test --solution ManagementApp.slnx` | **267 total: 260 passed, 0 failed, 7 skipped.** The skipped tests are the Azure Blob storage tests; they need `FILESTORAGE_TEST_AZURE` / Azurite. |
| `npm test` (src/ManagementApp.Web) | **38 passed** (6 files) |
| `npm run lint` | clean |
| `npm run typecheck` | clean |

Size: ~9.4k lines of C# (excluding migrations), ~7.2k lines of TypeScript/TSX, ~5.3k lines of C# tests, 11 migrations, 143 commits.

---

## 3. Architecture and project structure

| Project | Role | Notes from the audit |
|---|---|---|
| `ManagementApp.Domain` | Entities and rules: `Invoice` (state machine, totals, payments, credit notes), `Customer`, `Product`, `Tenant`, `TenantSubscription`, `TenantMembership`, `EmailMessage`, audit types | No dependencies. Money rules are centralised (`Money.Round`, half away from zero, 2 dp). |
| `ManagementApp.Application` | One plain handler class per use case, FluentValidation, DTOs, `SubscriptionGuard`, `TenantClock`, `EmailOutbox`, interfaces (`IFileStorage`, `IEmailSender`, `IInvoicePdfRenderer`, …) | No MediatR or AutoMapper; mapping is explicit. Handlers are registered by hand in `DependencyInjection.cs`. |
| `ManagementApp.Infrastructure` | EF Core `ApplicationDbContext` (query filters, write guard, stored totals, concurrency version), Identity, `TenantAccessResolver`, outbox worker, background jobs, QuestPDF, CSV, local/Azure Blob storage, SMTP/log email | Background work uses in-process `BackgroundService` timers. |
| `ManagementApp.Api` | Minimal API endpoint groups, `TenantResolutionMiddleware`, permission policies, exception → ProblemDetails mapping; serves the SPA in production | One deployable. |
| `ManagementApp.Web` | React 19 + TypeScript (strict), Vite, Tailwind 4, TanStack Query, RHF + Zod | English only. Cookie auth with an `X-Tenant-Id` header. |

The **modular monolith with Clean Architecture is appropriate** for this product and stage. Nothing in the audit justifies changing it. All recommendations below fit the existing handler-per-use-case pattern.

Request pipeline (`Program.cs`): `ExceptionHandler → StatusCodePages → (HSTS/HTTPS redirect outside Development) → StaticFiles → Authentication → TenantResolutionMiddleware → Authorization → endpoints → SPA fallback`. The pipeline has no forwarded-headers, rate-limiting, security-header or CORS middleware. CORS isn't needed while the SPA is same-origin.

---

## 4. Verified feature inventory

Legend: ✅ **Implemented** (code and tests) · 🟡 **Partial** · ❌ **Missing** · 🔧 **Implemented, needs production hardening**

### Core application

| Capability | Status | Evidence / notes |
|---|---|---|
| ASP.NET Core 10 API, Clean Architecture | ✅ | `src/*`, `Directory.Build.props` (nullable, warnings as errors, central package management) |
| React 19 + TS SPA | ✅ | `src/ManagementApp.Web` |
| SQL Server + EF Core, migrations | 🔧 | 11 migrations. Applied by `MigrateAsync()` at startup (`ApplicationDbContextInitialiser`), which isn't safe with several instances and has no pre-migration backup step. |
| Unit + integration tests | ✅ | 267 .NET tests. The API tests use real HTTP over SQLite with the real schema. |
| Docker + GitHub Actions | 🔧 | The image builds, runs as non-root and serves the SPA. **CI triggers on push to `master`, but the default branch is `main`.** Only PRs are built. No CD, no image push, no vulnerability scan. |
| Customers | ✅ | CRUD, soft delete, default currency/terms, invoice snapshot (`Invoice.To`), cross-tenant tests |
| Products / services catalogue | ✅ | CRUD, soft delete, line copied onto the invoice |
| Invoices: line items, discount, tax, multi-currency | ✅ / 🟡 | Totals are computed in the domain and mirrored in the frontend (`money.ts`, with tests). **Tax is one rate per invoice, not per line** (see §13). |
| Draft → Issued → Void lifecycle | ✅ | `Invoice.Issue/Void/EnsureEditable`. Audited. Issued invoices are immutable. |
| Partial payments + history | ✅ | `Invoice.RecordPayment/RemovePayment`, overpayment and future-date checks, concurrency version |
| Credit notes | ✅ | Gap-free `CN-` sequence, proportional tax split, cannot exceed balance, blocks voiding |
| Invoice numbering | ✅ | `InvoiceNumbering.TakeNextInvoiceNumberAsync` inside the issue transaction. Unique `(TenantId, InvoiceNumber)`. The sequence can move back only together with a prefix change. |
| PDF generation | ✅ | QuestPDF. Stored on issue under `tenants/{id}/invoices/{id}/invoice.pdf` and served unchanged. DRAFT/VOID watermark. **English labels only.** |
| CSV export | 🟡 | Invoices only, gated by the `CsvExport` plan feature, loads every invoice into memory |
| Dashboard / financial summary | ✅ / 🔧 | Per-currency totals, monthly figures. `GetFinancialSummaryHandler` loads every invoice into memory, which is fine for small tenants and a P2 for scale. |
| Company profile, branding, invoice settings | ✅ | Settings, logo (magic-byte check, 512 KB), brand colour gated by `CustomBranding` |
| Email outbox, retries, delivery status | ✅ | Transactional outbox, lease-based claiming, retry delays 1/5/15/60 min, Emails page with retry |
| **Send an invoice to a customer by email** | ❌ | Not implemented, though the README says "soon invoices". `TenantEmailSettings.InvoiceEmailSubject/Body` exist but nothing uses them. |
| Recurring invoices | ❌ | |
| Customer-facing payment reminders | ❌ | The only reminder is an **internal** overdue digest to the tenant (§10). |
| Quotes / estimates, expenses | ❌ | Out of scope for the MVP |

### Multi-tenancy and administration

| Capability | Status | Evidence / notes |
|---|---|---|
| Self-service signup creating a tenant | ✅ / 🟡 | `SignupHandler`, transactional. **No email verification, no rate limiting, no terms acceptance, no onboarding.** Defaults are USD / UTC / "Tax". |
| 14-day trial | ✅ | `TenantSubscription.StartTrial`, `SubscriptionPlans.TrialDays = 14` |
| Users in multiple companies + switcher | ✅ | Global `ApplicationUser` + `TenantMembership` |
| Roles and permissions | ✅ | 4 roles, 9 permission flags, per-member overrides, "can't grant what you don't have", last-admin protection |
| Invitations | 🔧 | Hashed tokens, 7-day expiry, one active per email. **The email link is relative and won't work.** |
| Tenant-scoped data access (query filters) | ✅ | Global filter on every `ITenantEntity`. A test fails if one is missing. |
| SaveChanges write guard | ✅ | `EnforceTenantBoundaries` |
| Tenant-aware background jobs | ✅ / 🔧 | `TenantJobRunner` gives each tenant its own scope. **Not safe across multiple instances** (§10). |
| Platform administration | ✅ | Tenants (create/edit/status/subscription/delete with safeguards), users, activity |
| Subscription plans, limits, expiry | 🟡 | See §9 |
| Support impersonation with audit | ✅ | Cookie-only, 1-hour session re-validated on every request, every non-GET request audited, platform admins can't be impersonated |
| Tenant and platform audit logs | ✅ / 🟡 | Covers auth, invoices, payments, members, tenant changes. No retention policy. Audit details hold email addresses (PII). |
| Account self-service (change own password/email, MFA) | ❌ | Identity's `/manage/*` endpoints exist, but there's no UI. |
| Password reset / account recovery | ❌ | See §7 |
| Tenant data export / account closure | ❌ | Platform-admin hard delete only |
| Terms, privacy, support contact | ❌ | |
| Health checks | 🟡 | `/health` checks the DB only. No liveness/readiness split. |
| Rate limiting, security headers | ❌ | |
| Structured logging, error tracking, metrics | ❌ | Default console logging only |

---

## 5. README claims vs. code

| README statement | Finding |
|---|---|
| "Email … invitations, overdue reminders, and soon invoices" | Accurate. Sending invoices isn't built. The invitation emails are **broken in practice** because the link is relative. |
| "self-service sign-up creates a company on a 14-day trial" | Accurate, but there's no verification, onboarding or abuse protection. |
| "Subscription-ready … read-only when expired" | Mostly accurate. Read-only isn't applied to settings, logo, email retry or member updates (§9). |
| "Several API instances can run the worker at once" | True for the **email outbox** only. The scheduled jobs (subscription expiry, overdue digest) would run on every instance. |
| "Data Protection keys on a file volume" (Dockerfile comment) | Accurate. It needs a shared store for any multi-instance or container-redeploy setup. |
| API table lists `POST /api/invoices/{id}/issue` twice | A small doc duplication. |
| "Try the demo account" (Getting started) | Fine for Docker demos, but the button is **hard-coded into the production SPA**. |
| `PRODUCT_ROADMAP.md` §1.3 ("No customers", "single AmountPaid", "no lifecycle", …) | Outdated. All of these have shipped. |

---

## 6. Security and multi-tenancy review

### 6.1 What is done well (keep as-is)

1. **Tenant resolution is server-side and membership-verified** (`TenantResolutionMiddleware` + `TenantAccessResolver`). The client can only *request* a tenant via `X-Tenant-Id` or `?tenantId=`. Inactive users and memberships, and inactive or suspended tenants, are rejected on every request.
2. **Isolation is layered:** a global query filter on every `ITenantEntity` (an empty context matches nothing), a write guard in `SaveChanges`, tenant-prefixed file keys with path validation (`FileStorageKey`), per-tenant job scopes, and a context that can only be set once per scope (`TenantContext.EnsureNotSet`).
3. **Cross-tenant IDs return 404, not 403.** Tests cover invoices, customers, products, payments, credit notes, PDFs, logos, audit logs, exports, numbering, the overdue job and impersonation.
4. **Platform admin is separate** from tenant roles, can only be bootstrapped from configuration, and is closed while impersonating.
5. **Impersonation** is carefully designed: session re-validation, claims kept on cookie refresh, account changes blocked, audit of every changing request from a separate scope.
6. **Invitation tokens** are 256-bit random values, stored as SHA-256 hashes, single-use, expiring and email-bound.
7. **Logo upload** checks magic bytes, so it can't be abused to upload HTML or SVG.

### 6.2 Findings

| ID | Severity | Finding | Recommendation (backlog) |
|---|---|---|---|
| S1 | **High** | `compose.yaml` sets `Database__SeedDemoData=true`, which creates platform admin `admin@example.com` / `Admin123!`. Nothing prevents seeding when `ASPNETCORE_ENVIRONMENT=Production`. The demo login button is always rendered. | Refuse demo seeding in Production (fail fast). Serve a public config flag that controls the demo button. → **SEC-01** |
| S2 | **High** | No rate limiting anywhere: `/api/auth/signup`, `/login`, `/forgotPassword`, `/api/invitations/*`. Identity's default lockout (5 failures → 5 min) is the only brake, and it applies per account, not per IP. | ASP.NET Core rate limiter partitioned by IP for anonymous endpoints, and by user/tenant for authenticated ones. → **SEC-02** |
| S3 | **High** (in production) | No `UseForwardedHeaders`. Behind a TLS-terminating proxy (App Service, container ingress) the app sees HTTP. Cookies may then lack `Secure`, HTTPS redirection misbehaves and audit `IpAddress` records the proxy. | Configure forwarded headers for the chosen host. Set the cookie to `SecurePolicy=Always` and `SameSite=Lax/Strict`. → **OPS-02** |
| S4 | **High** (in production) | Data Protection keys sit on the local filesystem by default. Losing or not sharing them invalidates all cookies, bearer tokens and Identity reset tokens. | `PersistKeysToDbContext` (or blob plus key encryption). → **OPS-03** |
| S5 | Medium | No email verification. Once invoices can be emailed (INV-01), an unverified account can send mail to arbitrary addresses under our sending domain. | Require a verified email before any email to a third party. → **AUTH-03** |
| S6 | Medium | No security headers (CSP, `X-Content-Type-Options`, `frame-ancestors`, `Referrer-Policy`). | → **SEC-03** |
| S7 | Medium | Platform admins (who can impersonate and delete tenants) sign in with a password only. | MFA required for platform admins. → **AUTH-04** |
| S8 | Medium | Isolation depends on `IgnoreQueryFilters()` being used only in platform/job code. It is today (reviewed), but only by convention. | An architecture test that allow-lists the classes that may call `IgnoreQueryFilters()`. → **SEC-05** |
| S9 | Low | Cross-tenant tests are written per feature. A new endpoint can ship without one. | Build an endpoint matrix test from the route table: every tenant endpoint with a foreign ID or tenant returns 403/404. → **SEC-04** |
| S10 | Low | Sign-up reveals whether an email already has an account (Identity `DuplicateEmail`). | Accept for now (common trade-off). Revisit together with email verification. |
| S11 | Low | Audit log `Details` stores emails (failed logins, invitations) with no retention period. Tenant deletion keeps the audit log. | Covered by the retention decision **D8** and **DATA-02**. |
| S12 | Info | Write guard: with **no** tenant context (platform/admin paths) it allows changes to any tenant. This is by design; the only code paths are in `Platform*Handlers`, the jobs and the outbox. | Covered by SEC-05. |

No **critical** tenant-isolation defect was found.

---

## 7. Authentication and account lifecycle

| Area | Current state |
|---|---|
| Mechanism | ASP.NET Core Identity API endpoints (`MapIdentityApi`). Cookie for the SPA, opaque bearer and refresh tokens for API clients. `/register` is blocked in favour of `/api/auth/signup`. |
| Disabled accounts | `ActiveUserConfirmation` repurposes `RequireConfirmedAccount` to mean `IsActive`. Sessions end on the next request (middleware) and the security stamp is updated. |
| Password policy | Identity defaults (6+ characters, digit, upper, lower, symbol). Acceptable. Consider a 10-character minimum and a breached-password check later (P2). |
| Lockout | Identity default (5 attempts / 5 min), per account. |
| **Password reset** | ❌ `/api/auth/forgotPassword` and `/resetPassword` are mapped, but `AddIdentityApiEndpoints` registers a no-op `IEmailSender<ApplicationUser>`, so **no email is ever sent**. The SPA has no "Forgot password?" page. |
| **Email confirmation** | ❌ Not used. `RequireConfirmedAccount` is repurposed, so enabling confirmation needs a separate `EmailConfirmed` check in `ActiveUserConfirmation`. |
| Change own password / profile | ❌ No UI. The only path is a tenant admin resetting a member's password, and only for accounts that belong to that tenant alone. |
| MFA | ❌ No UI. Identity supports TOTP (`/manage/2fa`). |
| Session duration | Identity defaults (14-day sliding cookie with "remember me"). Fine. |

**Design point for password-reset emails:** the outbox is tenant-scoped by construction. `EmailMessage` is an `ITenantEntity`, and `EmailMessage.Create` rejects `Guid.Empty`. Account emails (reset, verification) have no tenant. **Don't** weaken the tenant entity to allow a null tenant. Add a small **platform email path** (`SystemEmailMessage`, not tenant-filtered, only for account emails to the account's own address) that reuses the same sender and retry logic. → **EML-02**

---

## 8. Financial correctness review

| Rule | Status | Notes |
|---|---|---|
| Decimal arithmetic | ✅ | `decimal` everywhere. SQL `decimal(18,2)`; quantity `(18,4)`. |
| Consistent rounding backend ↔ frontend | ✅ | Half away from zero at 2 dp in `Money.Round` and `money.ts`, with tests on both sides |
| Currency precision | 🟡 | Always 2 dp. The UI offers `JPY` (0 dp). Restrict the list to 2-dp currencies or add per-currency precision (P2, **FIN-02**). MKD and EUR are both 2 dp in ISO 4217. |
| Invoice snapshots | ✅ | `From`/`To` text, line copies, PDF stored at issue |
| Issued invoice immutability | ✅ | Domain `EnsureEditable`, tested |
| Payment balance validation | ✅ | Can't exceed balance or be future-dated. Concurrency token on invoices. |
| Credit-note behaviour | ✅ | Proportional tax, the last credit takes the remaining tax exactly, gap-free numbering |
| Concurrency-safe numbering | ✅ | Row-locking increment inside the transaction. The first issue may collide on PK and fail safely. |
| Time-zone-safe overdue | ✅ | `TenantClock` / `Tenant.LocalDate`. Tested in `TenantTimeZoneTests`. |
| Audit of financial mutations | ✅ | Create/update/delete/issue/void/payment/credit note |
| Tax model | 🟡 | **Single invoice-level rate.** Correct for a freelancer charging one VAT rate. Insufficient for mixed-rate invoices (MK has a standard rate and reduced rates). Decision **D6** → **FIN-01**. |
| Payment deletion | ⚠️ Review | Any user with `InvoicesDelete` can delete a payment on an issued invoice. It's audited, which is acceptable, but consider restricting it to `TenantAdmin` or requiring a reason (P2). |

**No financial-correctness defects found.** Any change in this area (per-line tax, currency precision) must start with regression tests that pin the current totals.

---

## 9. Subscription and billing readiness

### 9.1 What exists

- `TenantSubscription` is an owned type on `Tenant`. It holds `PlanCode`, `Status` (Trialing/Active/PastDue/Cancelled/Expired), `BillingCycle`, `TrialEndsAt`, `CurrentPeriodEndsAt`, `MaxUsers`, `MaxInvoicesPerMonth` and `Features` (flags: CsvExport, Reports, AuditLog, CustomBranding).
- **Limits are snapshotted onto the tenant** when a plan is applied, so admins can comp or customise one tenant and catalogue changes don't silently affect existing customers. Good design; keep it.
- `EffectiveStatus(now)` derives Expired from dates, so enforcement doesn't depend on the expiry job having run. Good.
- `SubscriptionGuard` enforces: read-only when Expired or Cancelled (invoice, customer, product, payment, credit note, member-add paths), the monthly invoice limit, the user limit (members plus pending invitations), and the features (export, reports, audit log, brand colour).
- Platform admins can set any plan, status, dates and custom limits (`UpdateSubscriptionHandler`), audited as `tenant.subscription_changed`. **This is already a workable manual-billing back office.**
- The tenant sees plan, status and limits on the Settings page ("Contact support to change your plan").

### 9.2 Gaps

| ID | Gap | Impact |
|---|---|---|
| B1 | The plan catalogue is a static class in the Domain (`SubscriptionPlans`): Trial / Starter €9 / Professional €29 / Enterprise €99. The brief proposes Free / Starter €9 / Professional €19 / Business €39. There's no Free plan, and the price currency is implicit. | Decision **D3**. One central catalogue is fine, but it must match the decided plans. |
| B2 | Read-only isn't enforced on `UpdateTenantSettings`, logo upload/delete, `RetryEmail`, or member update/deactivate/remove. | Expired tenants can still change configuration and send email. That's low data-integrity risk, but inconsistent. |
| B3 | The invoice limit counts **drafts** by `Created` in the **UTC** calendar month. Deleting drafts frees quota, and the check isn't atomic (small overrun possible under concurrency). | Decision **D4**: what counts (created vs issued) and in which time zone. |
| B4 | `PastDue` allows changes **indefinitely**. There's no grace-period end and no "cancel at period end". `Cancelled` is immediately read-only. | The policy must be explicit before taking money (**D3**). |
| B5 | `TenantStatus.Suspended` blocks **all** access, including reading and exporting data. The enum comment suggests using it "for an unpaid bill". | Contradicts "never lose access to data because a payment failed". Non-payment should use subscription states (read-only), never tenant suspension. Reserve suspension for abuse. |
| B6 | No subscription history. Changes exist only as audit-log JSON. | Billing support and reconciliation need an append-only `SubscriptionEvents` table. |
| B7 | No usage visibility (invoices used this month, seats used). | Tenants hit limits by surprise. |
| B8 | No provider fields (customer/subscription IDs), webhook endpoint, idempotency store, reconciliation or receipts. | Phase 2 work, provider-agnostic where possible. |
| B9 | Data export is gated by the `CsvExport` feature. | Portability shouldn't depend on the plan. Make the **full export** (DATA-01) available on every plan, including expired ones. |
| B10 | No AI/usage-credit dimension. | Phase 3 prerequisite. Design it alongside SUB-01 but don't build it yet. |

### 9.3 Payment-provider readiness

- Nothing is wired in, which the README states.
- **The merchant country, entity and provider are open decisions (D1, D2).** Don't assume Stripe can onboard a North Macedonia-registered business; verify it. Realistic options to evaluate:
  1. a provider-supported entity elsewhere, e.g. in the EU, plus Stripe Billing;
  2. a **Merchant of Record** (e.g. Paddle, Lemon Squeezy), which also handles VAT on the SaaS sale itself;
  3. a local acquiring bank or payment gateway, which usually means more custom recurring-billing work;
  4. **manual invoicing / bank transfer at launch**, using the existing admin screens.
- **Recommendation:** launch pilots on option 4 with a written process (**BILL-01**). Meanwhile, build the provider-agnostic parts (subscription events, provider ID fields, webhook event store, `IBillingProvider` port) so the eventual provider is an adapter, not a redesign.

---

## 10. Email, background jobs, files and PDFs

| Area | Finding | Severity |
|---|---|---|
| Outbox | Solid: saved in the caller's transaction, claimed with a conditional update plus a 2-minute lease, at-least-once delivery, 5 attempts, a manual retry UI. | ✅ |
| Sending domain | Every email goes from one platform `Email:FromAddress`, with the tenant name as display name and the tenant's reply-to. Correct for deliverability. Needs SPF/DKIM/DMARC on the sending domain (ops task, OPS-05). | 🔧 |
| Body format | Plain text only. No HTML templates, no localisation, no unsubscribe for the digest. | P2 |
| Bounce/complaint handling | None. The provider's message ID is stored but no delivery webhooks are processed. | P2 |
| **Relative links** | The invitation body has `/accept-invite?token=…`. | **P0** (EML-01) |
| **Overdue digest frequency** | `BackgroundJobScheduler` runs every `IntervalMinutes` (360). Each run emails the tenant a digest whenever any invoice is overdue. There's no "already sent today", so it goes out **4× per day, every day.** | **P1** (EML-03) |
| Scheduled jobs on multiple instances | The scheduler is an in-process `PeriodicTimer` with no lock, so N instances send N digests. | P1 (OPS-07). Enforce a single instance until it's fixed. |
| Job failure visibility | Errors are only logged. No metrics or alerts. | P1 (OBS-*) |
| File storage | `IFileStorage` with Local and Azure Blob implementations, tenant-prefixed keys, safe-path regex, a private container, deletion on tenant delete. Contract tests run against both providers. The Blob tests are skipped in CI because there's no Azurite. | ✅ / 🔧 Add Azurite to CI (P2). |
| PDFs | Rendered on issue and stored. Re-rendered on first download if missing. QuestPDF Community licence (revenue threshold noted in the README). English-only labels. | ✅ / 🟡 Localisation, §13 |
| Logos | Stored in the DB (≤512 KB). Fine. | ✅ |

---

## 11. Data, migrations and indexes

- **Indexes are well chosen** for the current queries: `(TenantId, InvoiceNumber)` unique, `(TenantId, State, DueDate)`, `(TenantId, IssueDate)`, `(TenantId, CustomerId, IssueDate)`, `(TenantId, Number)` unique for credit notes, outbox `(Status, NextAttemptAt)`, audit `(TenantId, Timestamp)`.
- **Forward-only migrations**, and CI checks `has-pending-model-changes`. Migration `AddMultiTenancy` handled existing data carefully (a tenant per existing user).
- **Risks:**
  - `MigrateAsync()` at startup. Use an idempotent migration bundle or script as a pipeline step that runs after a backup and before deploy, and disable auto-migrate in Production via config (**OPS-04**).
  - Migrations are only exercised on SQLite in tests (`EnsureCreated`). There's no CI job that applies the real migration chain to SQL Server. Add a CI job with a SQL Server service container (**OPS-04**).
  - `AuditLogs` and `EmailMessages` grow without bounds. Retention jobs are P2, tied to decision D8.
- Concurrency: invoices have a manual version token. Customers and products don't, but the last-writer-wins risk is low there (P2).

---

## 12. Deployment and operations readiness

| Requirement (brief §8) | Status | Gap |
|---|---|---|
| Separate Dev / Staging / Prod configuration | 🟡 | `appsettings.json` and `.Development.json` only. No staging, no documented production settings. |
| Secrets in a secret manager | 🟡 | Nothing secret is committed (verified: `appsettings*.json` hold only a LocalDB string and empty SMTP values). `compose.yaml` has a default SA password for local use. Production needs Key Vault or environment configuration (documented). |
| HTTPS only | 🔧 | HSTS and redirect are on outside Development, but they need forwarded headers behind a proxy (S3). |
| Backups with tested restore | ❌ | No procedure. |
| Private file storage | ✅ | The Blob container is created private. |
| Migration process | 🔧 | Runs at startup (§11). |
| Rollback plan | ❌ | |
| Monitoring and alerting | ❌ | Console logs and `/health` only. |
| No demo credentials or seeding in production | ❌ | S1 |
| Security review before real data | ❌ | Plan it after Phase 1 slices 1–3. |
| CI | 🔧 | Push trigger on `master` instead of `main`. No SQL Server migration job. No `dotnet list package --vulnerable` or `npm audit`. Dependabot is configured; stale branches for an old `ClientApp` folder remain on the remote (cleanup). |
| CD | ❌ | The image is built but never pushed or deployed. |

**Hosting stance (decision D9):** start with **one** App Service (or equivalent) instance running API + SPA + workers, a managed SQL database with point-in-time restore, private Blob storage, and a transactional email provider over SMTP. Stay on a single instance until OPS-07 (job locking) ships. This matches the brief's "keep the first production deployment simple".

---

## 13. North Macedonia / localization readiness

| Item | Status | Notes |
|---|---|---|
| MKD and EUR currencies | ✅ | Both are in the currency list. Per-currency totals are never mixed. |
| Currency formatting | 🟡 | `Intl.NumberFormat(undefined, …)` uses the browser locale, not the tenant's. Acceptable, but MK customers on English browsers see `MKD 1,234.00`. P2. |
| Company VAT/tax details | ✅ | `TaxNumber`, configurable `TaxLabel` (e.g. "ДДВ"), registration number, address, country |
| Configurable numbering and payment terms | ✅ | Prefix (e.g. `2026-`), padding, next number, terms per tenant and customer |
| Time-zone-safe dates | ✅ | Tenant time zone (`Europe/Skopje` works). Sign-up defaults to `UTC`. ONB-01 should default from the country. |
| Macedonian UI | ❌ | No i18n framework. Phase 5, but **pick the i18n library before more UI text is written** (cheap now, expensive later). |
| **Invoice document language** | ❌ | PDF labels ("Invoice", "Bill to", "Due date", "Total") are English. For local B2B invoicing, a Macedonian or bilingual PDF is likely expected. **Decision D7**. If yes, it's a P1 within Phase 1/5 (FIN-03). |
| Per-line VAT rates | ❌ | **Decision D6.** Many IT and service freelancers charge one rate, so the current model may be enough for the pilot segment. |
| Statutory e-invoicing (UJP) | ❌ | Correctly out of scope. Keep PDF generation separate. **Make no compliance claims** in marketing or the UI (L10N-04 is a watch item). |

---

## 14. Recommended MVP scope

**"Paid pilot" release = existing core + Phase 1 + manual billing.**

**In:**
- Everything ✅ in §4 (customers, catalogue, invoice lifecycle, payments, credit notes, PDFs, CSV, reports, members, audit, impersonation).
- Phase 1 slices 1–8 (§16): safe public endpoints, account recovery, production hosting baseline, entitlements and plan/usage visibility, onboarding, sending invoices by email, data export and closure, observability and runbooks.
- Terms, privacy and support pages with owner-supplied content.
- **Manual billing**: a documented process in which the platform admin applies a plan after a bank transfer or invoice (BILL-01).

**Out (post-launch):** payment-provider checkout (Phase 2), AI (Phase 3), MCP (Phase 4), Macedonian UI translation, recurring invoices, customer reminders, client portal, e-invoicing, MFA for tenant users, quotes and expenses.

---

## 15. Prioritized backlog

**Priority:** P0 = security, financial correctness, data integrity or launch blocker · P1 = required for a usable paid MVP · P2 = can wait.
**Effort** (one developer, including tests and docs): **S** ≤ 1 day · **M** 2–4 days · **L** 1–2 weeks.
**Status:** every item is **Proposed** unless noted.

### Phase 1 — Production-ready SaaS foundation

#### SEC-01 · Demo mode off by default; never seed demo data in Production · P0 · S · **Done**
- **Problem:** hard-coded demo credentials on the login page. A compose deployment seeds a platform admin with a public password.
- **Behaviour:** add a `Demo:Enabled` setting, false by default and true in `appsettings.Development.json` and `compose.yaml`. Expose it through a new anonymous `GET /api/public-config` (`{ demoLogin, supportEmail, termsUrl, privacyUrl }`, extended later). The login page shows the demo button only when the flag is on. Startup **fails** if `Database:SeedDemoData=true` while the environment is Production (unless an explicit `Demo:AllowInProduction`, for a public demo site).
- **Areas:** Api (endpoint, startup check), Infrastructure (initialiser), Web (LoginPage, api client), compose, README.
- **Depends on:** none.
- **Security:** removes known credentials from production.
- **Acceptance:** in Production config the button isn't rendered and the demo users don't exist. Demo seeding in Production stops startup with a clear message. Docker compose still demos as today.
- **Tests:** API test for public-config. Initialiser test for the Production guard. Component test for the hidden/shown button.

#### EML-01 · Absolute links in emails (`App:PublicBaseUrl`) · P0 · S · **Done**
- **Problem:** the invitation email link is relative and unusable.
- **Behaviour:** a required, validated-on-start `App:PublicBaseUrl` option. An `IAppUrls` service in Application builds links (`AcceptInvitation(token)`, later `ResetPassword`, `Invoice`, …). Fix the invitation body.
- **Areas:** Application (interface + InvitationHandlers), Infrastructure/Api (options), appsettings, README.
- **Acceptance:** the queued invitation email contains `https://{host}/accept-invite?token=…`. The app refuses to start in Production without a valid absolute HTTPS base URL.
- **Tests:** update the outbox and invitation tests to assert the full link. Options validation test.

#### SEC-02 · Rate limiting on anonymous and auth endpoints · P0 · S–M · **Done**
- **Problem:** sign-up, login, invitation and (soon) password-reset endpoints can be hammered.
- **Behaviour:** `AddRateLimiter` with named policies: `auth` (per client IP, e.g. 10/min for login, 5/hour for signup and forgot-password), `invitations` (per IP), `authenticated` (per user, generous). Returns 429 with ProblemDetails and `Retry-After`. Limits come from config. Correct client IPs depend on OPS-02.
- **Areas:** Api (Program, endpoint groups), config, README.
- **Security:** must use the forwarded client IP, never a client-controlled header without a trusted-proxy configuration.
- **Acceptance:** exceeding the limit returns 429. Normal use is unaffected. Limits can be changed without a deploy.
- **Tests:** integration test with tiny configured limits (signup 2/min → 3rd call 429).

#### OPS-01 · CI runs on `main` · P1 · S · **Done**
- **Problem:** `.github/workflows/ci.yml` triggers on `push: [master]`. The default branch is `main`, so merges aren't built.
- **Behaviour:** trigger on `main`. Optionally add `dotnet list package --vulnerable` and `npm audit --omit=dev --audit-level=high` as non-blocking steps (OPS-08 makes them blocking later).
- **Acceptance:** a push to `main` starts CI. The README badge reflects `main`.

#### OPS-02 · Reverse proxy and cookie hardening · P0 · S · **Done**
- **Behaviour:** `UseForwardedHeaders` (proto + for) with known proxies/networks from config. Auth cookie `SecurePolicy=Always` outside Development, `SameSite=Lax`, `HttpOnly`. HSTS confirmed.
- **Acceptance:** behind a proxy, `Request.IsHttps` is true, cookies are `Secure`, and audit `IpAddress` shows the client IP.
- **Tests:** an integration test that sends `X-Forwarded-Proto/For` and asserts the scheme, IP and cookie flags.

#### OPS-03 · Shared Data Protection key store · P0 · S (migration) · **Done**
- **Behaviour:** `AddDataProtection().SetApplicationName("ManagementApp").PersistKeysToDbContext<ApplicationDbContext>()` (the context implements `IDataProtectionKeyContext`). Optionally protect keys with a certificate or Key Vault (decision with D9).
- **Acceptance:** sessions survive a container redeploy and work across two instances.
- **Tests:** an infrastructure test that keys are written to the DB.

#### EML-02 · Platform (tenantless) email path · P0 · M (migration) · **Done**
- **Problem:** account emails (password reset, verification) have no tenant. The tenant outbox correctly refuses them.
- **Behaviour:** a `SystemEmailMessage` entity (not `ITenantEntity`), sent by the same worker and sender with the same retry/lease rules. Only queued by account use cases, only to the account's own address. Not shown on any tenant's Emails page. Platform admins see a "System emails" list (P2).
- **Areas:** Domain, Infrastructure (config, processor generalised over both tables), migration.
- **Security:** keep it separate from tenant emails. Never put a reset link in logs; the Log sender must redact URLs with tokens.
- **Acceptance:** a queued system email is delivered and retried like tenant emails. Tenant query filters are unchanged.
- **Tests:** outbox processor tests for system messages. Isolation test that tenant email endpoints never list system emails.

#### AUTH-01 · Password reset end to end · P0 · M · **Done**
- **Behaviour:** register an `IEmailSender<ApplicationUser>` that queues a system email (EML-02) with a link built by EML-01. SPA pages: "Forgot password?" on login, `/reset-password?email=&code=`. Always answer "if an account exists, we sent an email" (no enumeration). Reset invalidates sessions (security stamp). Audited (`auth.password_reset_requested`, `auth.password_reset`). Rate-limited (SEC-02). Blocked while impersonating (existing filter).
- **Depends on:** EML-01, EML-02, SEC-02.
- **Acceptance:** a user resets their password without admin help. Old sessions stop working. The same response is given for unknown emails. Tokens expire (Identity default: 1 day; consider 2 h).
- **Tests:** API flow test (request → read queued email → reset → old cookie rejected → login works). Unknown-email test. Component tests for both pages.

#### AUTH-02 · My account page · P1 · S · **Done**
- Change display name and password (Identity `/manage/info` or a thin endpoint), show sign-in email, sign out of all sessions (security stamp). Blocked while impersonating. Audited.

#### AUTH-03 · Email verification · P1 · M · *decision D5*
- Verification email on sign-up (system email). An unverified account can use the app, but **can't send emails to third parties** (invoices, invitations) until verified. Resend link, rate-limited. `ActiveUserConfirmation` stays about `IsActive`; verification is a separate check.
- **Tests:** an unverified user sending an invoice email gets a 403 with a clear message. Verify-then-send succeeds.

#### AUTH-04 · MFA (TOTP) for platform admins · P1 · M
- Required for any `IsPlatformAdmin` account before `/api/admin/*` is usable. Optional for tenant users (P2). Recovery codes.

#### SEC-03 · Security headers · P1 · S · **Done**
- CSP (self, plus whatever Vite needs; no inline scripts in prod), `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `frame-ancestors 'none'`, `Permissions-Policy`. Test asserts the headers on `/` and `/api/*`.

#### SEC-04 · Tenant isolation endpoint matrix test · P1 · M
- Enumerate `EndpointDataSource`. Every endpoint carrying `TenantMember` or permission policies is called by tenant B with tenant A's resource IDs and with `X-Tenant-Id: A`. Expect 403/404 and no data. New endpoints fail the test until classified.

#### SEC-05 · `IgnoreQueryFilters` allow-list test · P1 · S
- An architecture test (reflection or Roslyn source scan) that fails if `IgnoreQueryFilters()` appears outside `Platform/*`, `InvoiceNumbering`, `InvoicePdfService`, invitations, session/membership resolution, jobs, the outbox processor and `DeleteTenantHandler`.

#### SUB-01 · Central entitlements and plan catalogue · P1 · M · *decision D3* · **Done**
- **Problem:** plan names and prices differ from the brief. There's no Free plan. Limits and features are checked in several handlers.
- **Behaviour:** keep `SubscriptionPlans` as the single catalogue (code is fine at this scale; move it to the DB only if prices change often). Align it to the decided plans, including currency and Free/Business if chosen. Introduce an `Entitlements` read model (`CanChangeData`, `InvoiceQuota{limit,used,resetsAt}`, `SeatQuota`, `Features`) computed in one place and used by both `SubscriptionGuard` and the UI. Existing tenants keep their snapshotted limits; a data migration maps old plan codes.
- **Acceptance:** no plan code or limit literals outside the catalogue (verified by grep in a test). Existing tenants unaffected.

#### SUB-02 · Close read-only gaps · P1 · S · **Done**
- Apply `EnsureCanChangeDataAsync` to settings update, logo upload/delete, email retry, member update/deactivate/remove/reset-password. Decide whether logo upload falls under `CustomBranding` (D3). Keep **reading and exporting** always allowed.
- **Tests:** a theory test over every mutating tenant endpoint with an expired tenant → 403 "read-only".

#### SUB-03 · Invoice quota semantics · P1 · S · *decision D4* · **Done**
- Recommended: count invoices **issued** in the tenant's local calendar month. Drafts are free, and deleting drafts doesn't game it. Enforce at **issue** instead of create. Use a serializable count inside the issue transaction, which already holds the sequence lock, so it's race-free.
- **Tests:** a limit of N lets N issues through and refuses N+1. Drafts never count. The month boundary follows the tenant time zone.

#### SUB-04 · Subscription event history · P1 · S (migration) · **Done**
- Append-only `SubscriptionEvents` (tenant, at, actor, source = `manual|system|provider`, from/to plan and status, period dates, note, provider event ID). Written by `UpdateSubscriptionHandler`, `SubscriptionExpiryJob` and, later, billing webhooks. Shown in the admin tenant page.

#### SUB-05 · Subscription lifecycle policy (grace, cancel, downgrade) · P1 · M · *decision D3* · **Done**
- `PastDue` stays writable until `GraceEndsAt` (configurable, e.g. 14 days), then read-only. `Cancelled` keeps access until `CurrentPeriodEndsAt`, then Expired (read-only, or the Free plan if D3 says so). Downgrade over the limits: existing data and users are kept and only **new** additions are blocked. Non-payment **never** sets `TenantStatus.Suspended`; document suspension as abuse-only. Fix the enum comment.
- **Tests:** domain tests for every transition in `EffectiveStatus` / `AllowsChanges`. API test for read-only after the grace period.

#### SUB-06 · Plan and usage page for tenants · P1 · M · **Done**
- `GET /api/tenant/subscription` returns entitlements and usage (SUB-01). A "Plan & billing" page shows plan, status, trial or period end, invoices used / limit, seats used / limit and included features, with clear upgrade guidance ("Contact us to upgrade" until Phase 2). Banners for trial ending in ≤3 days, past due and read-only.
- **Tests:** API test for usage numbers. Component tests for each banner state.

#### ONB-01 · Onboarding checklist and regional defaults · P1 · M
- After sign-up, a short checklist: company details (address, tax number), country (default from decision D10, e.g. MK) → currency (MKD/EUR), time zone (`Europe/Skopje`), tax label (e.g. "ДДВ"), default tax rate, numbering prefix (e.g. `2026-`), logo, first customer, first invoice. Progress is derived from data, not stored flags where possible. Sign-up gets an optional country field.
- **Acceptance:** a new user reaches "first invoice issued" without help. Defaults are correct for the chosen market.

#### ONB-02 · Empty states and first-run guidance · P1 · S
- Invoices, customers, products and reports pages explain the next step when empty. No sample data in real tenants; the demo tenant stays the showcase.

#### INV-01 · Send invoice to customer by email · P1 · M
- **Behaviour:** `POST /api/invoices/{id}/send` (issued invoices only; permission `InvoicesEdit`). Recipient defaults to the customer email and can be edited. Subject and body use the tenant templates (`TenantEmailSettings.InvoiceEmailSubject/Body`) with placeholders (number, total, due date, company). The stored PDF is attached via the outbox. Audited. Shows "Sent on … to …" on the invoice, from the outbox status. Respects read-only (SUB-02) and verification (AUTH-03). Preview in the UI before sending.
- **Depends on:** EML-01, AUTH-03 (for abuse), SEC-02.
- **Security:** recipient validation; no arbitrary attachment paths (the existing `FileStorageKey` check); per-tenant send rate limit.
- **Tests:** the email is queued with the PDF attachment and the correct sender. Drafts are refused. Cross-tenant 404. Expired tenant 403. Unverified account 403.

#### EML-03 · Overdue digest at most once a day · P1 · S
- Record `LastOverdueDigestSentOn` (tenant-local date) or check the outbox for today's digest. Run at a fixed local hour. Respect `OverdueRemindersEnabled`. Link to the overdue list (absolute URL).
- **Tests:** two job runs on the same day send one email. The next day sends again.

#### DATA-01 · Full tenant data export · P1 · M–L
- A tenant admin requests an export. A background job builds a ZIP in the tenant's storage: JSON and CSV for company settings, customers, products, invoices with items, payments, credit notes, members (emails and roles only), plus the stored invoice PDFs. A link to download it (authenticated, tenant-checked) expires after 7 days. Available on **every plan and status**, including expired. Audited. Rate-limited (one active export).
- **Tests:** the ZIP contains only this tenant's records (two-tenant test). An expired tenant can export. The download is 404 for another tenant.

#### DATA-02 · Company closure and account deletion · P1 · M · *decision D8*
- A tenant admin requests closure. The tenant goes `Inactive` with `ScheduledDeletionAt = now + retention`. Email confirmation goes to all admins, and closure can be reversed by contacting support before that date. Then a job runs the existing `DeleteTenantHandler` logic. Users with no remaining memberships can delete their account (anonymise audit references per D8). The legal retention of issued invoices may require keeping financial records; this depends on **D8** and needs legal review.

#### LEG-01 · Terms, privacy, support contact · P1 · S · *content: product owner*
- Static pages or links from public-config. Sign-up requires accepting the terms: store `TermsAcceptedAt` and `TermsVersion` on the user. Support email in the footer and error pages.

#### BILL-01 · Manual billing process (launch fallback) · P1 · S · **Done**
- `docs/saas/MANUAL_BILLING.md`: how a pilot customer is invoiced (by us, from this app, as a tenant), how payment is confirmed, and how the platform admin applies the plan and period end. Grace and expiry follow SUB-05. Every change produces a SubscriptionEvent (SUB-04).

#### OBS-01 · Structured logs with correlation · P1 · M
- JSON console logging in non-Development environments. A request ID / trace ID in every log. A scope with `tenant.id` and a **pseudonymous** `user.id` (the GUID, never the email). Review existing log statements: `LogEmailSender` logs whole email bodies (fine in dev, never in prod), and the `Granted platform admin to {Email}` log contains PII. Keep or replace per policy.

#### OBS-02 · Error tracking and alerting · P1 · S–M · *decision D9*
- Application Insights or OpenTelemetry → chosen backend. Alerts: 5xx rate, failed jobs, outbox `Failed` count > 0 or oldest `Pending` > 15 min, health check failing, DB DTU/CPU.

#### OBS-03 · Health checks and operational metrics · P1 · S · **Done**
- `/health/live` (process) and `/health/ready` (DB + storage + outbox backlog threshold). Metrics: emails sent/failed, job runs/failures, sign-ups, invoices issued.

#### OPS-04 · Controlled migrations · P1 · S
- CI builds an idempotent EF migration bundle or script artifact. Add a CI job that applies all migrations to SQL Server (service container). Production sets `Database:MigrateOnStartup=false`. Runbook: backup/PITR marker → apply migrations → deploy app → smoke test.

#### OPS-05 · Backup, restore and production runbooks · P0 (launch) · S
- `docs/saas/RUNBOOK.md`: environments, secrets list (where each lives), deploy, migrate, rollback (app: previous image; DB: forward-fix by default, PITR as last resort), restore drill (do one before launch and record the result), file-storage backup (Blob soft delete/versioning), email domain setup (SPF/DKIM/DMARC), incident contacts.

#### OPS-06 · Staging environment and CD · P1 · M
- GitHub Actions: build image → push to registry → deploy to staging on `main` → manual approval → production. Environment-specific secrets in GitHub Environments / Key Vault. Smoke test after deploy.

#### OPS-07 · Scheduled jobs safe across instances · P1 · S · **Done**
- Wrap each scheduled job run in a SQL Server application lock (`sp_getapplock`) or a lease row, as the outbox already does. Until then, document "single instance only".

#### Phase 1 P2 items (do when convenient)

| ID | Item | Notes |
|---|---|---|
| OPS-08 | Blocking vulnerability scans in CI; Azurite in CI so the Blob tests run; delete stale dependabot branches | S |
| FIN-02 | Per-currency precision or a restricted currency list | Remove `JPY` or support 0 dp. Tests first. |
| PERF-01 | Reports and export without loading every invoice | SQL aggregation over stored `Total`/`BalanceDue`. Only matters for large tenants. |
| AUD-01 | Audit log retention and PII minimisation | Depends on D8 |
| EML-04 | HTML email templates, bounce/complaint webhooks | After a provider is chosen |
| SEC-06 | Optional MFA for tenant users | |
| PAY-01 | Restrict payment deletion to admins or require a reason | |

### Phase 2 — Billing and paid subscriptions (after D1/D2)

| ID | Pri | Item | Acceptance highlights |
|---|---|---|---|
| BILL-00 | P0 (blocking) | **Decide** merchant entity, country and provider (D1, D2) | Decision recorded in this file |
| BILL-02 | P1 | Provider-agnostic model: `BillingCustomer` (provider, customer ID), provider subscription ID on `TenantSubscription`, `ProcessedWebhookEvents` (provider + event ID, unique), `IBillingProvider` port (`CreateCheckout`, `CreatePortalSession`, `GetSubscription`) | Migration reviewed. No provider SDK types leak into Application. |
| BILL-03 | P1 | Checkout (hosted page) + signature-verified webhook endpoint + status mapping → `TenantSubscription` + SubscriptionEvents | Duplicate webhook = no duplicate effect (test). Entitlements are granted only on provider confirmation. The client never submits price or plan amounts. |
| BILL-04 | P1 | Reconciliation job (local vs provider) with an admin report and alert | Drift detected and reported. Auto-fix only for safe cases. |
| BILL-05 | P1 | Billing history, receipts and self-service portal link; cancel at period end; plan change with the provider's proration | Failed payment → PastDue → grace → read-only (test with provider test mode) |
| BILL-06 | P2 | Annual billing (10× monthly), promo codes, comped plans via the provider | |

### Phase 3 — AI features (after Phase 1 and the metering design)

| ID | Pri | Item | Notes |
|---|---|---|---|
| AI-00 | P1 | Foundations: `IAiChatService` / `IAiStructuredExtractionService` ports, `AiUsage` table (tenant, user, feature, model, tokens, estimated cost), plan AI credits in entitlements, per-tenant and per-user rate limits, global and tenant kill switch, AI on/off in tenant settings, a data-processing notice | **No AI feature ships before this.** The provider is decision D11. |
| AI-01 | P1 | Natural-language invoice **draft** → validated draft preview → user confirms → existing `CreateInvoiceHandler` | Never issues or sends. Customer matched server-side within the tenant. |
| AI-02 | P2 | Read-only assistant over deterministic queries (stats, overdue, top balances) | The model explains numbers it got from tools and never calculates them itself. |
| AI-03 | P2 | Payment reminder drafting → user edits → sent via the outbox (needs customer reminders INV-03) | |
| AI-04 | P2 | Supplier document extraction | Needs expenses (not in scope), file scanning and storage limits. Latest of the AI items. |

The design in `docs/PRODUCT_ROADMAP.md` §6 is still valid as the target. This plan only sequences it later and makes metering and entitlements a hard prerequisite.

### Phase 4 — MCP and integrations

| ID | Pri | Item |
|---|---|---|
| MCP-00 | P1 | Personal access tokens (hashed, tenant + user bound, scoped permissions ≤ the user's, expiry, revocation, last used) |
| MCP-01 | P1 | Read-only MCP tools over the existing handlers (`ModelContextProtocol.AspNetCore`), audited `via: mcp`, rate-limited, per-tool cross-tenant tests |
| MCP-02 | P2 | Draft-only write tools with explicit confirmation; never platform-admin or impersonation capabilities |
| API-01 | P2 | Public API versioning, outgoing webhooks |

### Phase 5 — Local market readiness and growth

| ID | Pri | Item | Notes |
|---|---|---|---|
| L10N-00 | P1 (cheap now) | Choose an i18n library and extract strings for **new** UI from Phase 1 onwards | Avoids a large retrofit |
| FIN-03 | P1/P2 per D7 | Invoice PDF language per tenant (mk/en, or bilingual) | Labels only. No legal claims. |
| L10N-01 | P2 | Macedonian UI translation | |
| FIN-01 | P2 per D6 | Per-line VAT rates with a per-rate summary on the PDF | Domain change with migration. Regression tests first. |
| INV-02 | P2 | Recurring invoices (drafts by default) on the tenant job runner | Needs OPS-07 |
| INV-03 | P2 | Customer-facing overdue reminders (opt-in, via the outbox) | |
| REP-01 | P2 | Accountant-friendly exports | |
| L10N-04 | Watch | MK statutory e-invoicing: track official requirements; implement only after legal review | No compliance claims until then |
| GRW-01 | P2 | Product analytics events (sign-up, onboarding completed, first invoice issued/sent, trial→paid) | Privacy-reviewed. Feeds the brief's §10 metrics. |

---

## 16. Implementation sequence

Each slice is a vertical, independently releasable change: domain → application → API → UI → migration → tests → docs. Per the git workflow, slices go on one working branch as small commits, with one PR to `main` per agreed batch.

| # | Slice | Backlog items | Effort | Exit criteria |
|---|---|---|---|---|
| **1** ✅ | **Safe public entry points** (done) | SEC-01, EML-01, SEC-02, OPS-01 | ~3–4 d | Demo off in Production; absolute invitation links; 429s on abuse; CI on `main`; all tests green |
| 2 ✅ | Production hosting baseline (done) | OPS-02, OPS-03, SEC-03, OBS-03, OPS-07 | ~3–4 d | Works behind a proxy with Secure cookies; sessions survive redeploy and scale-out; headers present; live/ready health; jobs locked |
| 3 ✅ | Account recovery (done) | EML-02, AUTH-01, AUTH-02 | ~4–5 d | Forgot/reset works end to end via email; users can change their own password |
| 4 ✅ | Entitlements and plan visibility (done) | SUB-01…SUB-06, BILL-01 | ~1.5–2 wk | Server enforces the decided plans; read-only is consistent; tenants see usage and banners; history recorded; manual billing documented |
| 5 | Onboarding and trust | ONB-01, ONB-02, LEG-01, AUTH-03, L10N-00 | ~1–1.5 wk | New user goes from sign-up to first issued invoice alone; terms accepted and recorded; email verified before third-party email |
| 6 | Send invoices | INV-01, EML-03 | ~3–4 d | Customer receives the invoice PDF by email; digest at most daily |
| 7 | Data rights | DATA-01, DATA-02 | ~1–1.5 wk | Full export works on any plan; closure workflow per D8 |
| 8 | Operate it | OBS-01, OBS-02, OPS-04, OPS-05, OPS-06, AUTH-04 | ~1.5 wk | Staging + CD; migrations as a pipeline step; restore drill done; alerts firing in a test; platform admins use MFA |
| — | **Security review and pilot launch** (manual billing) | — | — | Checklist in brief §9 satisfied |
| 9+ | Phase 2 billing → Phase 3 AI foundations → AI-01 → Phase 4 MCP read-only → Phase 5 items by customer demand | | | |

Slices 1–3 have no open decisions and can start immediately. Slice 4 needs **D3/D4**, and slice 5 needs **D5/D10** plus legal texts. Slices 6–8 can be reordered if decisions lag.

---

## 17. Suggested database changes

All are forward-only, additive migrations. None rewrite financial data.

| Slice | Change | Notes |
|---|---|---|
| 2 | `DataProtectionKeys` table | EF Core standard |
| 3 | `SystemEmailMessages` (+ attachments, if ever needed) | Not tenant-filtered; account emails only |
| 4 | `SubscriptionEvents` (append-only); `TenantSubscription` + `GraceEndsAt`, `CancelAtPeriodEnd`; plan-code data migration if plans are renamed | Existing tenants keep their snapshotted limits |
| 5 | `AspNetUsers` + `TermsAcceptedAt`, `TermsVersion` (use Identity's `EmailConfirmed` for verification); optional `Tenant.OnboardingCompletedAt` | Existing users: `EmailConfirmed` backfilled to `true` (they predate verification) — **decision D5** |
| 6 | `Tenant.LastOverdueDigestOn` (or derive from the outbox); optional `Invoice.LastSentAt` (or derive from `EmailMessages`) | Prefer deriving from the outbox to avoid duplicated state |
| 7 | `DataExports` (tenant, requested by, status, storage path, expires at); `Tenant.ScheduledDeletionAt` | |
| Phase 2 | `BillingCustomers`, provider IDs on the subscription, `ProcessedWebhookEvents` (unique provider + event ID) | |
| Phase 3 | `AiUsage`, optionally `AiConversations` | Tenant-owned, retention per D8 |
| Phase 5 | Per-line `InvoiceItem.TaxRate` (+ snapshot) if D6 | Existing invoices: line rate = invoice rate, so totals are unchanged, proven by a migration test |

---

## 18. Test strategy

**Keep:** real-HTTP API tests over SQLite with the real schema, two-tenant negative tests per feature, domain unit tests for money rules, frontend tests mirroring the rounding rules.

**Add:**

| Layer | Additions |
|---|---|
| Architecture | SEC-05 `IgnoreQueryFilters` allow-list; "no plan literals outside the catalogue" (SUB-01); every `ITenantEntity` has a filter (exists) |
| Isolation | SEC-04 endpoint matrix; export ZIP content check (DATA-01); system emails never visible to tenants (EML-02) |
| Subscription | Theory test: every mutating endpoint × {Trialing, Active, PastDue in grace, PastDue after grace, Cancelled before/after period end, Expired}; quota boundaries incl. tenant-time-zone month edges |
| Auth | Reset flow, enumeration-safe responses, session invalidation, rate limits (with tiny configured limits), verification gating, MFA requirement for platform admins |
| Jobs | Digest once per day; lock prevents double runs (two runners in parallel) |
| Migrations | CI job applies the full chain to SQL Server 2022 (container); data-migration tests for plan-code mapping and any future per-line tax change |
| Billing (Phase 2) | Webhook signature rejection, duplicate delivery idempotency, out-of-order events, reconciliation drift |
| AI/MCP (Phases 3–4) | Cross-tenant tool calls, prompt-injection fixtures, "never writes without confirmation", quota exhaustion |
| Frontend | Component tests for new pages and banners; keep `lint` + `typecheck` + `vitest` in CI |
| Manual | Before launch: click-through at phone width, light/dark, on staging with real SMTP |

**Every slice's definition of done:** `dotnet test --solution ManagementApp.slnx`, `npm test`, `npm run lint`, `npm run typecheck`, and the CI migration check all green, with exact results reported.

---

## 19. Dependencies and risks

```
EML-01 ──► AUTH-01 ◄── EML-02        SEC-02 ──► AUTH-01, INV-01, DATA-01
OPS-02 ──► SEC-02 (real client IPs)  SUB-01 ──► SUB-02..06 ──► BILL-02..05
AUTH-03 ──► INV-01                   OPS-07 ──► multi-instance hosting, INV-02
AI-00 ──► AI-01..04                  MCP-00 ──► MCP-01..02
```

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Payment provider unavailable for the intended entity/country | High | High | Manual billing first (BILL-01); provider-agnostic model (BILL-02); decide D1/D2 early |
| Email deliverability (spam folder) for invoices and resets | Medium | High | Transactional provider, SPF/DKIM/DMARC, verified accounts only, rate limits |
| Spam/abuse via free sign-ups sending email | Medium | High | SEC-02 + AUTH-03 before INV-01 |
| Local customers expect Macedonian invoices / per-line VAT | Medium | Medium | Decide D6/D7 with pilot users; L10N-00 now keeps the retrofit cheap |
| Over-claiming compliance (e-invoicing) | Low | High | No compliance claims; L10N-04 watch item; legal review |
| Scaling out before jobs are locked → duplicate emails | Medium | Medium | Single instance until OPS-07 |
| Data loss / failed restore | Low | Critical | PITR, restore drill (OPS-05), Blob soft delete |
| Plan changes breaking existing tenants | Low | Medium | Snapshotted limits (already); plan-code data migration with tests |
| Scope creep (AI/MCP before launch) | Medium | Medium | AI-00 gate; launch definition in §14 |
| QuestPDF licence threshold | Low | Low | README already documents it; revisit at USD 1M revenue |

---

## 20. Open decisions and blocking questions

Only questions the repository can't answer. Each lists what it blocks.

| ID | Decision | Blocks | Recommendation / options |
|---|---|---|---|
| **D1** | Legal business entity and **merchant country** for selling the SaaS | Phase 2 | — (owner/legal) |
| **D2** | **Payment provider** (Stripe via an eligible entity, a Merchant of Record such as Paddle or Lemon Squeezy, a local gateway, or manual-only at first) | Phase 2 | Start manual (BILL-01); evaluate MoR if the entity stays in MK |
| **D3** | **Final plans**: names, prices and **currency** (EUR?), Free plan yes/no, trial length, **grace period**, what happens after trial (read-only vs Free), whether the logo counts as "custom branding" | Slice 4 | **Decided 2026-11-13:** prices in EUR, yearly = 10× monthly. Free €0 (1 user, 10 invoices/month, no extra features), Starter €9 (3 users, 50, CSV export), Professional €19 (10 users, 500, all features), Business €39 (25 users, 2000, all features); 14-day trial (5 users, 100, all features). The old Enterprise plan maps to Business, keeping each tenant's own limits. An ended trial moves to **Free**. **14-day grace** for overdue payments, then read-only. The logo stays available on every plan; only the brand colour is "custom branding". |
| **D4** | What the invoice quota counts | Slice 4 | **Decided 2026-11-13:** invoices **issued** in the tenant's local calendar month; drafts don't count. |
| **D5** | Email verification required at sign-up? Treat existing users as verified? | Slice 5 | Verify before any third-party email; backfill existing users as verified |
| **D6** | Per-line VAT rates needed for the first customers? | FIN-01 timing | Ask pilot users; keep a single rate until a pilot needs more |
| **D7** | Invoice PDF language for MK customers (Macedonian, English, bilingual, per customer) | FIN-03 timing | Per-tenant language at minimum |
| **D8** | **Data retention and deletion policy**: retention of closed tenants' financial records, audit logs, emails, exports; legal review | DATA-02, AUD-01 | Typically: financial records kept for the statutory period; personal data minimised |
| **D9** | Hosting provider/region, error-tracking/monitoring tool, data residency | Slices 2, 8 | One region close to customers (EU); Application Insights or OpenTelemetry + one backend |
| **D10** | Launch market (MK only vs wider Balkans) and launch languages/currencies | ONB-01 defaults, L10N | MK first; MKD + EUR; English UI with Macedonian invoice PDFs |
| D11 | AI provider, data-processing terms, retention | Phase 3 | Defer |
| D12 | Product name, domain, support email, terms/privacy texts | LEG-01, EML-01 (sending domain) | — |

---

## 21. Recommended first slice

### Slice 1: "Safe public entry points" (SEC-01, EML-01, SEC-02, OPS-01)

**What:**
1. `Demo:Enabled` flag and anonymous `GET /api/public-config`. The login page hides the demo button unless it's enabled. Startup refuses demo seeding in Production. `compose.yaml` and Development keep the demo working.
2. `App:PublicBaseUrl` (validated at startup, HTTPS required outside Development) and an `IAppUrls` link builder. The invitation email gets an absolute link.
3. Rate limiting for `/api/auth/*`, `/api/invitations/*` and sign-up (per IP, configurable, 429 + ProblemDetails). Rate limiting is keyed on the connection's remote IP until OPS-02 adds trusted forwarded headers in slice 2, so behind a proxy it's coarse until then. That's acceptable because slice 2 follows immediately.
4. CI trigger: `main`.

**Why first:**
- It fixes the two **concrete defects** found: broken invitation links, and a public platform-admin password if demo seeding reaches a server.
- **Every later slice depends on it.** Password reset, verification, sending invoices and billing redirects all need absolute URLs and rate limits. CI must guard `main` before code starts changing.
- **No open decisions**, no financial-logic changes, one config-only migration-free change set. Low risk, easy to review.
- Small: about 3–4 days including tests and docs.

**Affected projects:** Api (Program, AuthEndpoints, new PublicConfigEndpoints), Application (`IAppUrls`, `InvitationHandlers`), Infrastructure (initialiser guard, options), Web (LoginPage, API client), `compose.yaml`, `appsettings*.json`, `.github/workflows/ci.yml`, README.

**Risks:** rate limits too tight for shared office IPs (mitigate with config and generous defaults). Existing deployments must set `App:PublicBaseUrl` (startup validation makes this explicit, and the README will document it).

**Acceptance criteria:**
- With Production settings, the login page has no demo button and the API won't start with demo seeding enabled.
- The invitation email in the outbox contains an absolute `https://…/accept-invite?token=…` link.
- Repeated sign-up, login or invitation calls from one IP beyond the configured limit return 429 with `Retry-After`.
- A push to `main` triggers CI.
- `dotnet test`, `npm test`, `npm run lint` and `npm run typecheck` all pass, and new tests cover each item.

**Next:** slice 2 (hosting baseline) and slice 3 (account recovery). In parallel, the product owner answers **D3/D4** so slice 4 can start without waiting.

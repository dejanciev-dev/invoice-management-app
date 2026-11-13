# Manual billing (launch process)

> **Status:** in force until a payment provider is integrated (Phase 2, decisions D1/D2).
> **Owner:** platform admin. **Related:** plan catalogue and lifecycle in [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) (SUB-01…SUB-05, decision D3).

Pilot customers pay by bank transfer against an invoice we issue. The app enforces what they've paid for; nothing is charged automatically.

## Plans and prices

Prices are in **EUR**, excluding VAT. Yearly = 10× monthly (two months free). The catalogue lives in one place, `SubscriptionPlans` (`src/ManagementApp.Domain/Tenancy/Subscription.cs`).

| Plan | Monthly | Yearly | Users | Issued invoices / month | Features |
|---|---|---|---|---|---|
| Free | €0 | €0 | 1 | 10 | — |
| Starter | €9 | €90 | 3 | 50 | CSV export |
| Professional | €19 | €190 | 10 | 500 | CSV export, reports, activity log, brand colour |
| Business | €39 | €390 | 25 | 2,000 | Everything |

Every new company gets a **14-day trial** (5 users, 100 invoices, all features). When it ends without a paid plan, the company moves to **Free** on its own and keeps all its data.

## 1. A customer chooses a paid plan

1. The customer emails support with the plan and billing cycle (monthly or yearly).
2. In **our own company** in the app (we bill with the product we sell), create a customer for them if needed, then issue an invoice:
   - one line: `Invoice Manager <Plan>, <cycle>, <period start> – <period end>`, at the catalogue price;
   - VAT as it applies to us and the customer (ask the accountant until this is settled);
   - due in 14 days.
3. Email them the PDF (download it from the invoice page until "send invoice by email", INV-01, ships).

Nothing changes in their subscription yet: they keep their trial or current plan.

## 2. The payment arrives

1. Find the transfer on the bank statement and match it to the invoice number in the reference.
2. Record the payment on our invoice (**Record payment**: amount, date, method *Bank transfer*, the bank reference).
3. In **Admin → Tenants → their company → Subscription**:
   - **Plan**: the one they paid for;
   - **Status**: *Active*;
   - **Billing**: monthly or yearly;
   - **Paid until**: the last day of the paid period (the day before the next period starts);
   - leave **Trial ends** and the custom limits empty, unless a custom deal was agreed;
   - **Note for the history**: `Invoice <number> paid by bank transfer, ref <bank reference>`.
4. Save. Their limits and features change at once, and the change is kept in **Subscription history** with your name and the note.

## 3. Renewals

About **7 days before "Paid until"**, issue the next invoice (step 1.2) for the following period. Until there's a list of upcoming renewals (Phase 2), keep a simple calendar reminder per paying company when you record its payment.

When it's paid, repeat step 2 with the new **Paid until** date.

## 4. When a payment doesn't arrive

The app handles the timeline by itself; you only record what happened.

| When | What the company sees | What the app does |
|---|---|---|
| "Paid until" passes unpaid | A yellow banner: payment overdue, full access until a date | Status becomes **Past due**, with a **14-day grace period** from the end of the paid period |
| Grace period ends | A red banner: read-only | Status becomes **Expired**. They can still sign in, view and **export** all their data; nothing can be created or changed |
| They pay later | Full access again | Do step 2 (status *Active*, new "Paid until"). Nothing is lost |

To give someone more time, set **Status** to *Past due* and **Grace ends** to the new date, with a note. Leaving **Grace ends** empty gives 14 days from now.

Never use **Suspend** (tenant status) for non-payment: it blocks all access, even to their own data. Suspension is for abuse only.

## 5. Changing plan or cancelling

- **Upgrade:** invoice the difference for the rest of the period (pro rata by days, rounded to whole euros), then change the plan once it's paid. For goodwill, it's fine to upgrade at once and invoice in the same step.
- **Downgrade:** change the plan from the next period. Nothing is deleted: existing users and invoices stay, and only new additions beyond the smaller plan's limits are blocked.
- **Cancel:** set **Status** to *Cancelled* and keep **Paid until**. They keep full access until then, and the company becomes read-only afterwards. No refunds for partial periods unless agreed.
- **Move to Free:** set **Plan** to *Free*, **Status** *Active*, and clear **Paid until**.

## 6. Records and checks

- The **Subscription history** of a company shows every change (sign-up, admin changes with name and note, and automatic transitions). It's append-only and kept after a company is deleted.
- Month-end: compare our issued invoices and recorded payments with the bank statement, and compare **Active** paid companies with paid invoices. Any company that's *Active* without a paid invoice for the current period is a mistake to fix.
- Keep our invoices numbered and immutable as for any customer: issued invoices can't be edited, only voided or credited.

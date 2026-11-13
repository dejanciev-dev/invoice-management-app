import { Check } from 'lucide-react'
import { useTenantPlan } from '../api/tenant'
import type { Subscription, Usage } from '../api/types'
import { Alert, Badge, Card, PageHeader, Spinner, tableClass, tdClass, theadClass, thClass } from '../components/ui'
import { formatMoney } from '../lib/format'
import { planFeatureLabels, subscriptionStatusLabels } from '../lib/labels'

const day = (iso: string) => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(iso))

function Meter({ label, usage, detail }: { label: string; usage: Usage; detail?: string }) {
  const percent = usage.limit ? Math.min(100, Math.round((usage.used / usage.limit) * 100)) : 0
  const full = usage.limit !== null && usage.used >= usage.limit

  return (
    <div>
      <div className="flex items-baseline justify-between gap-2 text-sm">
        <span className="font-medium">{label}</span>
        <span className={full ? 'font-medium text-red-700 dark:text-red-300' : 'text-muted'}>
          {usage.limit === null ? `${usage.used} · unlimited` : `${usage.used} of ${usage.limit}`}
        </span>
      </div>
      {usage.limit !== null && (
        <div
          role="progressbar"
          aria-label={label}
          aria-valuemin={0}
          aria-valuemax={usage.limit}
          aria-valuenow={usage.used}
          className="mt-2 h-2 overflow-hidden rounded-full bg-subtle"
        >
          <div className={`h-full rounded-full ${full ? 'bg-red-600' : 'bg-brand'}`} style={{ width: `${percent}%` }} />
        </div>
      )}
      {detail && <p className="mt-1 text-xs text-muted">{detail}</p>}
    </div>
  )
}

/** The dates that matter for the current status, in plain words. */
function statusDetail(s: Subscription): string | null {
  if (s.effectiveStatus === 'Trialing' && s.trialEndsAt) return `Trial ends ${day(s.trialEndsAt)}, then the Free plan applies.`
  if (s.effectiveStatus === 'PastDue' && s.graceEndsAt) return `Payment overdue. Full access until ${day(s.graceEndsAt)}.`
  if (s.effectiveStatus === 'Cancelled' && s.accessEndsAt) return `Cancelled. Full access until ${day(s.accessEndsAt)}.`
  if (s.currentPeriodEndsAt && s.canChangeData) return `Paid until ${day(s.currentPeriodEndsAt)}.`
  return null
}

export function PlanPage() {
  const plan = useTenantPlan()

  if (plan.isPending) return <Spinner />
  if (plan.error) return <Alert>We couldn't load your plan. Please try again.</Alert>

  const { subscription: s, invoices, seats, invoicesResetAt, plans } = plan.data
  const detail = statusDetail(s)

  return (
    <div>
      <PageHeader title="Plan & usage" description="What your plan includes and how much of it you've used this month." />

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="space-y-3 p-5 lg:col-span-1">
          <div className="flex items-center gap-2">
            <h2 className="text-lg font-semibold">{s.planName}</h2>
            <Badge>{subscriptionStatusLabels[s.effectiveStatus]}</Badge>
          </div>
          {detail && <p className="text-sm text-muted">{detail}</p>}
          {!s.canChangeData && <Alert>{s.readOnlyReason} You can still view and export everything.</Alert>}
          <ul className="space-y-1 text-sm">
            {s.features.length === 0 && <li className="text-muted">No extra features</li>}
            {s.features.map((f) => (
              <li key={f} className="flex items-center gap-2">
                <Check className="size-4 text-brand" aria-hidden />
                {planFeatureLabels[f]}
              </li>
            ))}
          </ul>
        </Card>

        <Card className="space-y-5 p-5 lg:col-span-2">
          <Meter label="Invoices issued this month" usage={invoices} detail={`Drafts don't count. The count starts again on ${day(invoicesResetAt)}.`} />
          <Meter label="Users" usage={seats} detail="Active members plus pending invitations." />
        </Card>
      </div>

      <Card className="mt-6 overflow-hidden">
        <h2 className="border-b border-line px-4 py-3 text-sm font-semibold">Plans</h2>
        <div className="overflow-x-auto">
          <table className={tableClass}>
            <thead className={theadClass}>
              <tr>
                <th className={thClass}>Plan</th>
                <th className={thClass}>Price</th>
                <th className={thClass}>Users</th>
                <th className={thClass}>Invoices / month</th>
                <th className={thClass}>Includes</th>
              </tr>
            </thead>
            <tbody>
              {plans.map((p) => (
                <tr key={p.code} className={p.code === s.planCode ? 'bg-subtle/60' : undefined} aria-current={p.code === s.planCode || undefined}>
                  <td className={`${tdClass} font-medium`}>
                    {p.name} {p.code === s.planCode && <Badge>Current</Badge>}
                  </td>
                  <td className={tdClass}>
                    {p.monthlyPrice === 0 ? 'Free' : `${formatMoney(p.monthlyPrice, p.currency)}/month or ${formatMoney(p.yearlyPrice, p.currency)}/year`}
                  </td>
                  <td className={tdClass}>{p.maxUsers ?? 'Unlimited'}</td>
                  <td className={tdClass}>{p.maxInvoicesPerMonth ?? 'Unlimited'}</td>
                  <td className={`${tdClass} text-muted`}>{p.features.map((f) => planFeatureLabels[f]).join(', ') || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="border-t border-line px-4 py-3 text-sm text-muted">
          To change your plan, contact support. Your data is kept whatever you choose, and moving to a smaller plan only stops new
          additions beyond its limits.
        </p>
      </Card>
    </div>
  )
}

import { ArrowLeft } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import {
  adminTenantLogoUrl,
  useAuditLogs,
  useChangeTenantStatus,
  useDeleteTenant,
  usePlans,
  useTenantDetails,
  useSubscriptionEvents,
  useTenantUsers,
  useUpdateSubscription,
  useUpdateTenantSettingsAsAdmin,
  type SubscriptionUpdate,
} from '../../api/admin'
import { ApiError } from '../../api/client'
import type { BillingCycle, SubscriptionEventSource, SubscriptionStatus, TenantDetails, TenantStatus } from '../../api/types'
import { AuditLogTable } from '../../components/AuditLogTable'
import { inputClass } from '../../components/styles'
import { TenantSettingsForm } from '../../components/TenantSettingsForm'
import { Alert, Badge, Button, Card, EmptyRow, Field, Spinner, StatTile, tableClass, tdClass, theadClass, thClass } from '../../components/ui'
import { formatDate, formatMoney } from '../../lib/format'
import { formatDateTime, roleLabels, subscriptionStatusLabels } from '../../lib/labels'
import { NotFoundPage } from '../NotFoundPage'
import { errorText } from '../../lib/errors'
import { ImpersonateButton, TenantStatusBadge } from './shared'

const tabs = ['Overview', 'Users', 'Configuration', 'Activity'] as const
type Tab = (typeof tabs)[number]

export function AdminTenantPage() {
  const id = useParams().id ?? ''
  const details = useTenantDetails(id)
  const [tab, setTab] = useState<Tab>('Overview')

  if (details.isPending) return <Spinner label="Loading tenant" />
  if (details.error instanceof ApiError && details.error.status === 404) return <NotFoundPage message="That tenant doesn't exist." />
  if (details.isError) return <Alert>Could not load the tenant.</Alert>

  const tenant = details.data

  return (
    <>
      <Link to="/admin/tenants" className="mb-4 inline-flex items-center gap-1.5 text-sm text-muted hover:text-ink">
        <ArrowLeft className="size-4" aria-hidden />
        All tenants
      </Link>
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center">
        {tenant.hasLogo && <img src={adminTenantLogoUrl(tenant.id)} alt="" className="size-12 rounded object-contain" />}
        <div className="flex-1">
          <h1 className="text-2xl font-semibold tracking-tight">{tenant.settings.profile.name}</h1>
          <p className="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted">
            <TenantStatusBadge status={tenant.status} />
            {tenant.statusReason && <span>“{tenant.statusReason}”</span>}
            <span>Created {formatDateTime(tenant.created)}</span>
          </p>
        </div>
      </div>

      <div role="tablist" className="mb-6 flex gap-1 overflow-x-auto border-b border-line">
        {tabs.map((t) => (
          <button
            key={t}
            role="tab"
            type="button"
            aria-selected={tab === t}
            onClick={() => setTab(t)}
            className={`-mb-px border-b-2 px-3 py-2 text-sm font-medium ${tab === t ? 'border-brand text-ink' : 'border-transparent text-muted hover:text-ink'}`}
          >
            {t}
          </button>
        ))}
      </div>

      {tab === 'Overview' && <Overview tenant={tenant} />}
      {tab === 'Users' && <TenantUsers tenant={tenant} />}
      {tab === 'Configuration' && <Configuration tenant={tenant} />}
      {tab === 'Activity' && <TenantActivity tenantId={tenant.id} />}
    </>
  )
}

function Overview({ tenant }: { tenant: TenantDetails }) {
  const { stats, settings } = tenant
  return (
    <div className="space-y-6">
      <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile label="Users" value={stats.userCount} detail={`${stats.activeUserCount} active`} />
        <StatTile label="Invoices" value={stats.invoiceCount} detail={`${stats.invoicesThisMonth} this month`} />
        <StatTile label="Plan" value={tenant.subscription.planName} detail={subscriptionStatusLabels[tenant.subscription.effectiveStatus]} />
        <StatTile label="Last activity" value={<span className="text-base">{formatDateTime(stats.lastActivityAt)}</span>} />
      </dl>

      <Card className="overflow-hidden">
        <h2 className="border-b border-line px-4 py-3 text-sm font-semibold">Invoice statistics</h2>
        <table className={tableClass}>
          <thead className={theadClass}>
            <tr>
              <th scope="col" className={thClass}>Currency</th>
              <th scope="col" className={`${thClass} text-right`}>Invoices</th>
              <th scope="col" className={`${thClass} text-right`}>Billed</th>
              <th scope="col" className={`${thClass} text-right`}>Outstanding</th>
              <th scope="col" className={`${thClass} hidden text-right sm:table-cell`}>Overdue</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line tabular-nums">
            {stats.totals.length === 0 && <EmptyRow colSpan={5}>No invoices yet.</EmptyRow>}
            {stats.totals.map((t) => (
              <tr key={t.currency}>
                <td className={tdClass}>{t.currency}</td>
                <td className={`${tdClass} text-right`}>{t.invoiceCount}</td>
                <td className={`${tdClass} text-right`}>{formatMoney(t.billed, t.currency)}</td>
                <td className={`${tdClass} text-right`}>{formatMoney(t.outstanding, t.currency)}</td>
                <td className={`${tdClass} hidden text-right sm:table-cell`}>{formatMoney(t.overdue, t.currency)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>

      <Card className="grid gap-x-8 gap-y-3 p-5 text-sm sm:grid-cols-2">
        <Info label="Registration number" value={settings.profile.registrationNumber} />
        <Info label="Tax / VAT number" value={settings.profile.taxNumber} />
        <Info label="Email" value={settings.profile.email} />
        <Info label="Phone" value={settings.profile.phone} />
        <Info label="Country" value={settings.profile.country} />
        <Info label="Currency · language · time zone" value={`${settings.profile.defaultCurrency} · ${settings.profile.defaultLanguage} · ${settings.profile.timeZone}`} />
        <Info label="Address" value={settings.profile.address} />
      </Card>

      <StatusActions tenant={tenant} />
      <SubscriptionForm tenant={tenant} />
      <SubscriptionHistory tenantId={tenant.id} />
      <DangerZone tenant={tenant} />
    </div>
  )
}

function Info({ label, value }: { label: string; value: string | null }) {
  return (
    <div>
      <dt className="text-xs text-muted">{label}</dt>
      <dd className="whitespace-pre-line">{value || '—'}</dd>
    </div>
  )
}

function StatusActions({ tenant }: { tenant: TenantDetails }) {
  const change = useChangeTenantStatus(tenant.id)
  const [reason, setReason] = useState('')
  const options: { status: TenantStatus; label: string; variant: 'primary' | 'secondary' | 'danger' }[] = [
    { status: 'Active', label: tenant.status === 'Suspended' ? 'Reactivate' : 'Activate', variant: 'primary' },
    { status: 'Inactive', label: 'Deactivate', variant: 'secondary' },
    { status: 'Suspended', label: 'Suspend', variant: 'danger' },
  ]

  return (
    <Card className="p-5">
      <h2 className="text-sm font-semibold">Account status</h2>
      <p className="mt-1 text-xs text-muted">Inactive and suspended tenants cannot be used by anyone, and any impersonation in them ends.</p>
      {change.error && <div className="mt-3"><Alert>{errorText(change.error)}</Alert></div>}
      <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-end">
        <Field label="Reason (required to suspend)" htmlFor="status-reason" className="flex-1">
          <input id="status-reason" value={reason} onChange={(e) => setReason(e.target.value)} className={inputClass} />
        </Field>
        {options
          .filter((o) => o.status !== tenant.status)
          .map((o) => (
            <Button key={o.status} variant={o.variant} loading={change.isPending && change.variables?.status === o.status}
              onClick={() => change.mutate({ status: o.status, reason: reason.trim() || null }, { onSuccess: () => setReason('') })}>
              {o.label}
            </Button>
          ))}
      </div>
    </Card>
  )
}

const subscriptionStatuses: SubscriptionStatus[] = ['Trialing', 'Active', 'PastDue', 'Cancelled', 'Expired']

function SubscriptionForm({ tenant }: { tenant: TenantDetails }) {
  const plans = usePlans()
  const update = useUpdateSubscription(tenant.id)
  const s = tenant.subscription
  const plan = plans.data?.find((p) => p.code === s.planCode)
  const [form, setForm] = useState<SubscriptionUpdate>({
    planCode: s.planCode,
    status: s.status,
    billingCycle: s.billingCycle,
    trialEndsAt: s.trialEndsAt,
    currentPeriodEndsAt: s.currentPeriodEndsAt,
    customMaxUsers: plan && plan.maxUsers !== s.maxUsers ? s.maxUsers : null,
    customMaxInvoicesPerMonth: plan && plan.maxInvoicesPerMonth !== s.maxInvoicesPerMonth ? s.maxInvoicesPerMonth : null,
    graceEndsAt: s.graceEndsAt,
    note: '',
  })
  const date = (value: string | null) => (value ? value.slice(0, 10) : '')
  const toIso = (value: string) => (value ? new Date(`${value}T00:00:00Z`).toISOString() : null)
  const num = (value: string) => (value ? Number(value) : null)

  return (
    <Card className="p-5">
      <h2 className="text-sm font-semibold">Subscription</h2>
      <p className="mt-1 text-xs text-muted">
        Currently {s.planName}: {s.maxUsers ?? 'unlimited'} users, {s.maxInvoicesPerMonth ?? 'unlimited'} invoices a month, features: {s.features.join(', ') || 'none'}. No payment
        provider is connected yet, so changes here take effect immediately.
      </p>
      {update.error && <div className="mt-3"><Alert>{errorText(update.error)}</Alert></div>}
      <form
        onSubmit={(e) => {
          e.preventDefault()
          update.mutate(form)
        }}
        className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4"
      >
        <Field label="Plan" htmlFor="sub-plan">
          <select id="sub-plan" value={form.planCode} onChange={(e) => setForm({ ...form, planCode: e.target.value })} className={inputClass}>
            {(plans.data ?? []).map((p) => (
              <option key={p.code} value={p.code}>
                {p.name} ({formatMoney(p.monthlyPrice, p.currency)}/mo)
              </option>
            ))}
          </select>
        </Field>
        <Field label="Status" htmlFor="sub-status">
          <select id="sub-status" value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value as SubscriptionStatus })} className={inputClass}>
            {subscriptionStatuses.map((st) => (
              <option key={st} value={st}>
                {subscriptionStatusLabels[st]}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Billing" htmlFor="sub-cycle">
          <select id="sub-cycle" value={form.billingCycle} onChange={(e) => setForm({ ...form, billingCycle: e.target.value as BillingCycle })} className={inputClass}>
            <option>Monthly</option>
            <option>Yearly</option>
          </select>
        </Field>
        <Field label="Trial ends" htmlFor="sub-trial">
          <input id="sub-trial" type="date" value={date(form.trialEndsAt)} onChange={(e) => setForm({ ...form, trialEndsAt: toIso(e.target.value) })} className={inputClass} />
        </Field>
        <Field label="Paid until" htmlFor="sub-period">
          <input id="sub-period" type="date" value={date(form.currentPeriodEndsAt)} onChange={(e) => setForm({ ...form, currentPeriodEndsAt: toIso(e.target.value) })} className={inputClass} />
        </Field>
        <Field label="Custom user limit" htmlFor="sub-users" hint="Empty = plan limit">
          <input id="sub-users" type="number" min={1} value={form.customMaxUsers ?? ''} onChange={(e) => setForm({ ...form, customMaxUsers: num(e.target.value) })} className={inputClass} />
        </Field>
        <Field label="Custom invoices / month" htmlFor="sub-invoices" hint="Empty = plan limit">
          <input id="sub-invoices" type="number" min={1} value={form.customMaxInvoicesPerMonth ?? ''} onChange={(e) => setForm({ ...form, customMaxInvoicesPerMonth: num(e.target.value) })} className={inputClass} />
        </Field>
        {form.status === 'PastDue' && (
          <Field label="Grace ends" htmlFor="sub-grace" hint="Empty = 14 days from now; read-only after">
            <input id="sub-grace" type="date" value={date(form.graceEndsAt ?? null)} onChange={(e) => setForm({ ...form, graceEndsAt: toIso(e.target.value) })} className={inputClass} />
          </Field>
        )}
        <Field label="Note for the history" htmlFor="sub-note" hint="e.g. invoice paid by bank transfer" className="sm:col-span-2">
          <input id="sub-note" maxLength={500} value={form.note ?? ''} onChange={(e) => setForm({ ...form, note: e.target.value })} className={inputClass} />
        </Field>
        <div className="flex items-end">
          <Button type="submit" loading={update.isPending}>
            Save subscription
          </Button>
        </div>
      </form>
    </Card>
  )
}

const sourceLabels: Record<SubscriptionEventSource, string> = { Manual: 'Admin', System: 'Automatic', Provider: 'Payment provider' }

function SubscriptionHistory({ tenantId }: { tenantId: string }) {
  const events = useSubscriptionEvents(tenantId)
  const day = (value: string | null) => formatDate(value?.slice(0, 10))

  return (
    <Card className="overflow-hidden">
      <h2 className="border-b border-line px-4 py-3 text-sm font-semibold">Subscription history</h2>
      <div className="overflow-x-auto">
        <table className={tableClass}>
          <thead className={theadClass}>
            <tr>
              <th className={thClass}>When</th>
              <th className={thClass}>By</th>
              <th className={thClass}>Change</th>
              <th className={thClass}>Dates</th>
              <th className={thClass}>Note</th>
            </tr>
          </thead>
          <tbody>
            {events.isPending && <EmptyRow colSpan={5}>Loading…</EmptyRow>}
            {events.data?.length === 0 && <EmptyRow colSpan={5}>No changes recorded yet.</EmptyRow>}
            {events.data?.map((e, i) => (
              <tr key={`${e.at}-${i}`}>
                <td className={tdClass}>{formatDateTime(e.at)}</td>
                <td className={tdClass}>{e.actorEmail ?? sourceLabels[e.source]}</td>
                <td className={tdClass}>
                  {e.fromPlanCode && e.fromStatus ? `${e.fromPlanCode} (${subscriptionStatusLabels[e.fromStatus]}) → ` : ''}
                  {e.toPlanCode} ({subscriptionStatusLabels[e.toStatus]})
                </td>
                <td className={`${tdClass} text-xs text-muted`}>
                  {e.trialEndsAt && <div>Trial ends {day(e.trialEndsAt)}</div>}
                  {e.currentPeriodEndsAt && <div>Paid until {day(e.currentPeriodEndsAt)}</div>}
                  {e.graceEndsAt && <div>Grace ends {day(e.graceEndsAt)}</div>}
                </td>
                <td className={tdClass}>{e.note ?? ''}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

function DangerZone({ tenant }: { tenant: TenantDetails }) {
  const remove = useDeleteTenant(tenant.id)
  const navigate = useNavigate()
  const [confirmName, setConfirmName] = useState('')
  const name = tenant.settings.profile.name

  return (
    <Card className="border-red-300 p-5 dark:border-red-900">
      <h2 className="text-sm font-semibold text-red-700 dark:text-red-300">Delete tenant</h2>
      <p className="mt-1 text-xs text-muted">
        Permanently deletes the company with all its invoices, members and settings. User accounts and the audit log are kept. The tenant must be
        deactivated or suspended first.
      </p>
      {remove.error && <div className="mt-3"><Alert>{errorText(remove.error)}</Alert></div>}
      <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-end">
        <Field label={`Type “${name}” to confirm`} htmlFor="confirm-name" className="flex-1">
          <input id="confirm-name" value={confirmName} onChange={(e) => setConfirmName(e.target.value)} className={inputClass} />
        </Field>
        <Button
          variant="danger"
          disabled={tenant.status === 'Active' || confirmName !== name}
          loading={remove.isPending}
          onClick={() => remove.mutate(confirmName, { onSuccess: () => navigate('/admin/tenants', { replace: true }) })}
        >
          Delete permanently
        </Button>
      </div>
    </Card>
  )
}

function TenantUsers({ tenant }: { tenant: TenantDetails }) {
  const users = useTenantUsers(tenant.id)
  if (users.isPending) return <Spinner label="Loading users" />
  if (users.isError) return <Alert>Could not load users.</Alert>

  return (
    <Card className="overflow-hidden">
      <div className="overflow-x-auto">
        <table className={tableClass}>
          <thead className={theadClass}>
            <tr>
              <th scope="col" className={thClass}>User</th>
              <th scope="col" className={thClass}>Role</th>
              <th scope="col" className={`${thClass} hidden md:table-cell`}>Last sign-in</th>
              <th scope="col" className={`${thClass} text-right`}>Support</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {users.data.length === 0 && <EmptyRow colSpan={4}>No users yet.</EmptyRow>}
            {users.data.map((m) => (
              <tr key={m.userId}>
                <td className={tdClass}>
                  <span className="font-medium">{m.displayName ?? m.email}</span>
                  {m.displayName && <span className="block text-xs text-muted">{m.email}</span>}
                  {!m.isActive && <Badge>Deactivated</Badge>}
                  {!m.accountIsActive && <Badge className="bg-red-500/10 text-red-700 ring-red-500/25">Account disabled</Badge>}
                </td>
                <td className={tdClass}>{roleLabels[m.role]}</td>
                <td className={`${tdClass} hidden text-muted md:table-cell`}>{formatDateTime(m.lastLoginAt)}</td>
                <td className={`${tdClass} text-right`}>
                  {m.isActive && m.accountIsActive && tenant.status === 'Active' ? (
                    <ImpersonateButton userId={m.userId} tenantId={tenant.id} label={m.email} />
                  ) : (
                    <span className="text-xs text-muted">Unavailable</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

function Configuration({ tenant }: { tenant: TenantDetails }) {
  const update = useUpdateTenantSettingsAsAdmin(tenant.id)
  return (
    <TenantSettingsForm
      initial={tenant.settings}
      onSave={(settings) => update.mutate(settings)}
      saving={update.isPending}
      error={update.error}
      saved={update.isSuccess}
    />
  )
}

function TenantActivity({ tenantId }: { tenantId: string }) {
  const [page, setPage] = useState(1)
  const [action, setAction] = useState('')
  const logs = useAuditLogs({ tenantId, page, action: action || undefined })
  return (
    <AuditLogTable
      data={logs.data}
      isPending={logs.isPending}
      isError={logs.isError}
      action={action}
      onActionChange={(value) => {
        setAction(value)
        setPage(1)
      }}
      onPageChange={setPage}
    />
  )
}

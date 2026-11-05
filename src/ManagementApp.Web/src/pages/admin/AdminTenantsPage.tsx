import { Plus, Search } from 'lucide-react'
import { useDeferredValue, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { usePlans, useCreateTenant, useTenants } from '../../api/admin'
import type { TenantStatus } from '../../api/types'
import { inputClass } from '../../components/styles'
import { Alert, Button, Card, EmptyRow, Field, Notice, PageHeader, Pager, Spinner, tableClass, tdClass, theadClass, thClass } from '../../components/ui'
import { currencies } from '../../lib/invoiceForm'
import { formatDateTime, subscriptionStatusLabels } from '../../lib/labels'
import { errorText } from '../../lib/errors'
import { TenantStatusBadge } from './shared'

const statuses: (TenantStatus | undefined)[] = [undefined, 'Active', 'Inactive', 'Suspended']

export function AdminTenantsPage() {
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<TenantStatus | undefined>()
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const deferredSearch = useDeferredValue(search.trim())
  const tenants = useTenants({ search: deferredSearch || undefined, status, page })

  return (
    <>
      <PageHeader
        title="Tenants"
        description="Companies using the platform."
        actions={
          <Button onClick={() => setCreating(!creating)}>
            <Plus className="size-4" aria-hidden />
            New tenant
          </Button>
        }
      />

      {creating && <CreateTenantForm onCancel={() => setCreating(false)} />}

      <Card className="overflow-hidden">
        <div className="flex flex-col gap-3 border-b border-line p-4 lg:flex-row lg:items-center lg:justify-between">
          <div role="tablist" aria-label="Filter by status" className="flex flex-wrap gap-1">
            {statuses.map((s) => (
              <button
                key={s ?? 'all'}
                type="button"
                role="tab"
                aria-selected={status === s}
                onClick={() => {
                  setStatus(s)
                  setPage(1)
                }}
                className={`rounded-md px-3 py-1.5 text-sm font-medium ${status === s ? 'bg-subtle text-ink' : 'text-muted hover:text-ink'}`}
              >
                {s ?? 'All'}
              </button>
            ))}
          </div>
          <div className="relative lg:w-72">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
            <input
              type="search"
              aria-label="Search tenants"
              placeholder="Search name or email"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value)
                setPage(1)
              }}
              className={`${inputClass} pl-9`}
            />
          </div>
        </div>
        {tenants.isPending ? (
          <Spinner label="Loading tenants" />
        ) : tenants.isError ? (
          <div className="p-4">
            <Alert>Could not load tenants.</Alert>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className={tableClass}>
                <thead className={theadClass}>
                  <tr>
                    <th scope="col" className={thClass}>Tenant</th>
                    <th scope="col" className={thClass}>Status</th>
                    <th scope="col" className={`${thClass} hidden md:table-cell`}>Plan</th>
                    <th scope="col" className={`${thClass} text-right`}>Users</th>
                    <th scope="col" className={`${thClass} text-right`}>Invoices</th>
                    <th scope="col" className={`${thClass} hidden lg:table-cell`}>Created</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {tenants.data.items.length === 0 && <EmptyRow colSpan={6}>No tenants found.</EmptyRow>}
                  {tenants.data.items.map((t) => (
                    <tr key={t.id}>
                      <td className={tdClass}>
                        <Link to={`/admin/tenants/${t.id}`} className="font-medium hover:text-brand">
                          {t.name}
                        </Link>
                        <span className="block text-xs text-muted">{[t.email, t.country].filter(Boolean).join(' · ')}</span>
                      </td>
                      <td className={tdClass}>
                        <TenantStatusBadge status={t.status} />
                      </td>
                      <td className={`${tdClass} hidden md:table-cell`}>
                        {t.planCode} <span className="text-xs text-muted">({subscriptionStatusLabels[t.subscriptionStatus]})</span>
                      </td>
                      <td className={`${tdClass} text-right tabular-nums`}>{t.userCount}</td>
                      <td className={`${tdClass} text-right tabular-nums`}>{t.invoiceCount}</td>
                      <td className={`${tdClass} hidden text-muted lg:table-cell`}>{formatDateTime(t.created)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pager page={tenants.data.page} pageSize={tenants.data.pageSize} total={tenants.data.total} onChange={setPage} />
          </>
        )}
      </Card>
    </>
  )
}

function CreateTenantForm({ onCancel }: { onCancel: () => void }) {
  const create = useCreateTenant()
  const plans = usePlans()
  const navigate = useNavigate()
  const [form, setForm] = useState({ name: '', email: '', country: '', currency: 'USD', planCode: 'trial', adminEmail: '', adminPassword: '' })
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })

  if (create.data?.invitationToken) {
    const link = `${window.location.origin}/accept-invite?token=${encodeURIComponent(create.data.invitationToken)}`
    return (
      <Card className="mb-6 space-y-3 p-5">
        <Notice>Tenant created. Send this one-time link to {form.adminEmail} so they can set up their account:</Notice>
        <input readOnly value={link} aria-label="Invitation link" className={`${inputClass} font-mono text-xs`} onFocus={(e) => e.target.select()} />
        <Button onClick={() => navigate(`/admin/tenants/${create.data!.id}`)}>Open tenant</Button>
      </Card>
    )
  }

  return (
    <Card className="mb-6 p-5">
      <h2 className="mb-4 text-sm font-semibold">New tenant</h2>
      <form
        onSubmit={(e) => {
          e.preventDefault()
          create.mutate(
            {
              profile: {
                name: form.name,
                email: form.email || null,
                country: form.country || null,
                defaultCurrency: form.currency,
                defaultLanguage: 'en',
                timeZone: 'UTC',
                registrationNumber: null,
                taxNumber: null,
                address: null,
                phone: null,
              },
              planCode: form.planCode,
              adminEmail: form.adminEmail,
              adminPassword: form.adminPassword || null,
            },
            { onSuccess: (result) => !result.invitationToken && navigate(`/admin/tenants/${result.id}`) },
          )
        }}
        className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3"
      >
        {create.error && <div className="sm:col-span-2 lg:col-span-3"><Alert>{errorText(create.error)}</Alert></div>}
        <Field label="Company name" htmlFor="t-name">
          <input id="t-name" value={form.name} onChange={set('name')} className={inputClass} />
        </Field>
        <Field label="Company email" htmlFor="t-email">
          <input id="t-email" type="email" value={form.email} onChange={set('email')} className={inputClass} />
        </Field>
        <Field label="Country" htmlFor="t-country" hint="Two-letter code">
          <input id="t-country" value={form.country} maxLength={2} onChange={set('country')} className={inputClass} />
        </Field>
        <Field label="Default currency" htmlFor="t-currency">
          <select id="t-currency" value={form.currency} onChange={set('currency')} className={inputClass}>
            {currencies.map((c) => (
              <option key={c}>{c}</option>
            ))}
          </select>
        </Field>
        <Field label="Plan" htmlFor="t-plan">
          <select id="t-plan" value={form.planCode} onChange={set('planCode')} className={inputClass}>
            {(plans.data ?? []).map((p) => (
              <option key={p.code} value={p.code}>
                {p.name}
              </option>
            ))}
          </select>
        </Field>
        <div />
        <Field label="Admin email" htmlFor="t-admin" hint="An existing account becomes the admin; otherwise one is created or invited.">
          <input id="t-admin" type="email" value={form.adminEmail} onChange={set('adminEmail')} className={inputClass} />
        </Field>
        <Field label="Admin password (optional)" htmlFor="t-admin-pw" hint="Leave empty to send an invitation link instead.">
          <input id="t-admin-pw" type="password" autoComplete="new-password" value={form.adminPassword} onChange={set('adminPassword')} className={inputClass} />
        </Field>
        <div className="flex items-end gap-2">
          <Button type="submit" loading={create.isPending}>
            Create tenant
          </Button>
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
        </div>
      </form>
    </Card>
  )
}

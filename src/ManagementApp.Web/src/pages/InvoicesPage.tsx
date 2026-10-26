import { Download, FileText, Plus, Search } from 'lucide-react'
import { useDeferredValue, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { INVOICE_EXPORT_URL, useInvoices } from '../api/invoices'
import type { InvoiceStatus, InvoiceSummary } from '../api/types'
import { StatusBadge } from '../components/StatusBadge'
import { buttonClass, inputClass } from '../components/styles'
import { statusLabels } from '../lib/status'
import { Alert, ButtonLink, Card, PageHeader, Spinner } from '../components/ui'
import { firstLine, formatDate, formatMoney, formatTotalsByCurrency } from '../lib/format'

const statusFilters: (InvoiceStatus | undefined)[] = [undefined, 'Unpaid', 'PartiallyPaid', 'Overdue', 'Paid']

export function InvoicesPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const status = (searchParams.get('status') as InvoiceStatus | null) ?? undefined
  const [search, setSearch] = useState(searchParams.get('search') ?? '')
  const deferredSearch = useDeferredValue(search.trim())

  const filtered = useInvoices({ search: deferredSearch || undefined, status })
  const all = useInvoices()

  const updateParam = (key: string, value: string | undefined) =>
    setSearchParams(
      (params) => {
        if (value) params.set(key, value)
        else params.delete(key)
        return params
      },
      { replace: true },
    )

  return (
    <>
      <PageHeader
        title="Invoices"
        description="Track what you've billed, what's been paid and what's overdue."
        actions={
          <>
            <a href={INVOICE_EXPORT_URL} download className={buttonClass('secondary')}>
              <Download className="size-4" aria-hidden />
              Export CSV
            </a>
            <ButtonLink to="/invoices/new">
              <Plus className="size-4" aria-hidden />
              New invoice
            </ButtonLink>
          </>
        }
      />

      {all.data && <Stats invoices={all.data} />}

      <Card className="overflow-hidden">
        <div className="flex flex-col gap-3 border-b border-line p-4 lg:flex-row lg:items-center lg:justify-between">
          <div role="tablist" aria-label="Filter by status" className="flex flex-wrap gap-1">
            {statusFilters.map((filter) => (
              <button
                key={filter ?? 'all'}
                type="button"
                role="tab"
                aria-selected={status === filter}
                onClick={() => updateParam('status', filter)}
                className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                  status === filter ? 'bg-subtle text-ink' : 'text-muted hover:text-ink'
                }`}
              >
                {filter ? statusLabels[filter] : 'All'}
              </button>
            ))}
          </div>
          <div className="relative lg:w-72">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
            <input
              type="search"
              aria-label="Search invoices"
              placeholder="Search number or client"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value)
                updateParam('search', e.target.value.trim() || undefined)
              }}
              className={`${inputClass} pl-9`}
            />
          </div>
        </div>

        {filtered.isPending ? (
          <Spinner label="Loading invoices" />
        ) : filtered.isError ? (
          <div className="p-4">
            <Alert>Could not load invoices. Please refresh the page.</Alert>
          </div>
        ) : filtered.data.length === 0 ? (
          <EmptyState filtered={Boolean(status || deferredSearch)} />
        ) : (
          <InvoiceTable invoices={filtered.data} />
        )}
      </Card>
    </>
  )
}

function Stats({ invoices }: { invoices: InvoiceSummary[] }) {
  const open = invoices.filter((i) => i.status !== 'Paid')
  const overdue = invoices.filter((i) => i.status === 'Overdue')

  const stats = [
    { label: 'Outstanding', value: formatTotalsByCurrency(open.map((i) => ({ amount: i.balanceDue, currency: i.currency }))), detail: `${open.length} open` },
    { label: 'Overdue', value: formatTotalsByCurrency(overdue.map((i) => ({ amount: i.balanceDue, currency: i.currency }))), detail: `${overdue.length} invoices` },
    { label: 'Total billed', value: formatTotalsByCurrency(invoices.map((i) => ({ amount: i.total, currency: i.currency }))), detail: `${invoices.length} invoices` },
  ]

  return (
    <dl className="mb-6 grid gap-4 sm:grid-cols-3">
      {stats.map((stat) => (
        <Card key={stat.label} className="p-5">
          <dt className="text-sm text-muted">{stat.label}</dt>
          <dd className="mt-1 text-2xl font-semibold tracking-tight tabular-nums">{stat.value}</dd>
          <dd className="mt-1 text-xs text-muted">{stat.detail}</dd>
        </Card>
      ))}
    </dl>
  )
}

function InvoiceTable({ invoices }: { invoices: InvoiceSummary[] }) {
  const navigate = useNavigate()

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead className="bg-subtle/60 text-xs font-medium tracking-wide text-muted uppercase">
          <tr>
            <th scope="col" className="px-4 py-3">Invoice</th>
            <th scope="col" className="px-4 py-3">Client</th>
            <th scope="col" className="hidden px-4 py-3 md:table-cell">Issued</th>
            <th scope="col" className="hidden px-4 py-3 md:table-cell">Due</th>
            <th scope="col" className="px-4 py-3 text-right">Total</th>
            <th scope="col" className="hidden px-4 py-3 text-right sm:table-cell">Balance</th>
            <th scope="col" className="px-4 py-3">Status</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {invoices.map((invoice) => (
            <tr
              key={invoice.id}
              onClick={() => navigate(`/invoices/${invoice.id}`)}
              className="cursor-pointer transition-colors hover:bg-subtle/60"
            >
              <td className="px-4 py-3 font-medium whitespace-nowrap">
                <Link to={`/invoices/${invoice.id}`} onClick={(e) => e.stopPropagation()} className="hover:text-brand">
                  {invoice.invoiceNumber}
                </Link>
              </td>
              <td className="px-4 py-3">{firstLine(invoice.to)}</td>
              <td className="hidden px-4 py-3 whitespace-nowrap text-muted md:table-cell">{formatDate(invoice.issueDate)}</td>
              <td className="hidden px-4 py-3 whitespace-nowrap text-muted md:table-cell">{formatDate(invoice.dueDate)}</td>
              <td className="px-4 py-3 text-right whitespace-nowrap tabular-nums">{formatMoney(invoice.total, invoice.currency)}</td>
              <td className="hidden px-4 py-3 text-right whitespace-nowrap tabular-nums sm:table-cell">
                {formatMoney(invoice.balanceDue, invoice.currency)}
              </td>
              <td className="px-4 py-3">
                <StatusBadge status={invoice.status} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function EmptyState({ filtered }: { filtered: boolean }) {
  return (
    <div className="px-4 py-16 text-center">
      <FileText className="mx-auto size-10 text-muted/60" aria-hidden />
      <h2 className="mt-3 text-sm font-semibold">{filtered ? 'No matching invoices' : 'No invoices yet'}</h2>
      <p className="mt-1 text-sm text-muted">
        {filtered ? 'Try a different search or status.' : 'Create your first invoice to get started.'}
      </p>
      {!filtered && (
        <ButtonLink to="/invoices/new" className="mt-5">
          <Plus className="size-4" aria-hidden />
          New invoice
        </ButtonLink>
      )}
    </div>
  )
}

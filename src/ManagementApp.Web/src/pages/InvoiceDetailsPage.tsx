import { ArrowLeft, Ban, Banknote, Pencil, Printer, Send, Trash2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useCan } from '../api/auth'
import { ApiError } from '../api/client'
import { useDeleteInvoice, useInvoice, useIssueInvoice, useRecordAmountPaid, useVoidInvoice } from '../api/invoices'
import { tenantLogoUrl, useCurrentTenant } from '../api/tenant'
import type { CurrentTenant, Invoice } from '../api/types'
import { StatusBadge } from '../components/StatusBadge'
import { inputClass } from '../components/styles'
import { Alert, Button, ButtonLink, Card, Field, Spinner } from '../components/ui'
import { formatDate, formatMoney } from '../lib/format'
import { formatDateTime } from '../lib/labels'
import { NotFoundPage } from './NotFoundPage'

export function InvoiceDetailsPage() {
  const id = Number(useParams().id)
  if (!Number.isInteger(id)) return <NotFoundPage message="That invoice doesn't exist." />
  return <InvoiceDetails id={id} />
}

function InvoiceDetails({ id }: { id: number }) {
  const { data: invoice, error, isPending } = useInvoice(id)
  const { data: tenant } = useCurrentTenant()

  if (isPending) return <Spinner label="Loading invoice" />
  if (error instanceof ApiError && error.status === 404) {
    return <NotFoundPage message="That invoice doesn't exist or was deleted." />
  }
  if (error) return <Alert>Could not load the invoice. Please refresh the page.</Alert>

  return (
    <>
      <Toolbar invoice={invoice} />
      {invoice.customer && (
        <p className="-mt-3 mb-4 text-sm text-muted print:hidden">
          Customer:{' '}
          {invoice.customer.isDeleted ? (
            <span className="font-medium text-ink">{invoice.customer.name} (deleted)</span>
          ) : (
            <Link to={`/customers/${invoice.customer.id}`} className="font-medium text-ink hover:text-brand">
              {invoice.customer.name}
            </Link>
          )}
        </p>
      )}
      <StateNotice invoice={invoice} />
      <InvoiceDocument invoice={invoice} tenant={tenant} />
    </>
  )
}

type ToolbarMode = 'actions' | 'delete' | 'void' | 'payment'

/**
 * The actions follow the invoice's lifecycle: drafts can be edited, deleted or issued; issued invoices can only have
 * payments recorded or be voided; void invoices are closed.
 */
function Toolbar({ invoice }: { invoice: Invoice }) {
  const navigate = useNavigate()
  const deleteInvoice = useDeleteInvoice()
  const issueInvoice = useIssueInvoice()
  const [mode, setMode] = useState<ToolbarMode>('actions')
  const canEdit = useCan('InvoicesEdit')
  const canDelete = useCan('InvoicesDelete')
  const isDraft = invoice.state === 'Draft'
  const isIssued = invoice.state === 'Issued'

  const remove = () =>
    deleteInvoice.mutate(invoice.id, { onSuccess: () => navigate('/', { replace: true }) })
  const done = () => setMode('actions')

  return (
    <div className="mb-6 print:hidden">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Link to="/" className="inline-flex items-center gap-1.5 text-sm text-muted hover:text-ink">
          <ArrowLeft className="size-4" aria-hidden />
          All invoices
        </Link>
        {mode === 'delete' ? (
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-sm">Delete draft {invoice.invoiceNumber}?</span>
            <Button variant="secondary" onClick={done}>
              Cancel
            </Button>
            <Button variant="danger" onClick={remove} loading={deleteInvoice.isPending}>
              Delete
            </Button>
          </div>
        ) : mode === 'actions' ? (
          <div className="flex flex-wrap gap-2">
            <Button variant="secondary" onClick={() => window.print()}>
              <Printer className="size-4" aria-hidden />
              Print
            </Button>
            {isDraft && canDelete && (
              <Button variant="secondary" onClick={() => setMode('delete')}>
                <Trash2 className="size-4" aria-hidden />
                Delete
              </Button>
            )}
            {isDraft && canEdit && (
              <ButtonLink variant="secondary" to={`/invoices/${invoice.id}/edit`}>
                <Pencil className="size-4" aria-hidden />
                Edit
              </ButtonLink>
            )}
            {isDraft && canEdit && (
              <Button onClick={() => issueInvoice.mutate(invoice.id)} loading={issueInvoice.isPending}>
                <Send className="size-4" aria-hidden />
                Issue
              </Button>
            )}
            {isIssued && canDelete && (
              <Button variant="secondary" onClick={() => setMode('void')}>
                <Ban className="size-4" aria-hidden />
                Void
              </Button>
            )}
            {isIssued && canEdit && (
              <Button onClick={() => setMode('payment')}>
                <Banknote className="size-4" aria-hidden />
                Record payment
              </Button>
            )}
          </div>
        ) : null}
      </div>
      {mode === 'void' && <VoidForm invoice={invoice} onDone={done} />}
      {mode === 'payment' && <PaymentForm invoice={invoice} onDone={done} />}
      {(issueInvoice.error || deleteInvoice.error) && (
        <div className="mt-3">
          <Alert>{(issueInvoice.error ?? deleteInvoice.error)?.message}</Alert>
        </div>
      )}
    </div>
  )
}

function VoidForm({ invoice, onDone }: { invoice: Invoice; onDone: () => void }) {
  const voidInvoice = useVoidInvoice()
  const [reason, setReason] = useState('')

  const submit = (e: FormEvent) => {
    e.preventDefault()
    voidInvoice.mutate({ id: invoice.id, reason: reason.trim() }, { onSuccess: onDone })
  }

  return (
    <Card className="mt-4 p-4">
      <form onSubmit={submit} className="space-y-3">
        <p className="text-sm">
          Voiding cancels invoice <span className="font-medium">{invoice.invoiceNumber}</span>. It stays on record but is no
          longer owed, and it cannot be undone.
        </p>
        <Field label="Reason" htmlFor="voidReason" error={voidInvoice.error instanceof ApiError ? voidInvoice.error.messages.join(' ') : undefined}>
          <input
            id="voidReason"
            required
            maxLength={500}
            autoFocus
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="e.g. Billed to the wrong customer"
            className={inputClass}
          />
        </Field>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variant="secondary" onClick={onDone}>
            Cancel
          </Button>
          <Button type="submit" variant="danger" loading={voidInvoice.isPending} disabled={!reason.trim()}>
            Void invoice
          </Button>
        </div>
      </form>
    </Card>
  )
}

function PaymentForm({ invoice, onDone }: { invoice: Invoice; onDone: () => void }) {
  const recordAmountPaid = useRecordAmountPaid()
  const [amount, setAmount] = useState(String(invoice.amountPaid || invoice.total))
  const value = Number(amount)
  const valid = amount.trim() !== '' && Number.isFinite(value) && value >= 0

  const submit = (e: FormEvent) => {
    e.preventDefault()
    recordAmountPaid.mutate({ id: invoice.id, amountPaid: value }, { onSuccess: onDone })
  }

  return (
    <Card className="mt-4 p-4">
      <form onSubmit={submit} className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <Field
          label={`Total received (${invoice.currency})`}
          htmlFor="amountPaid"
          hint={`Of ${formatMoney(invoice.total, invoice.currency)} billed.`}
          error={recordAmountPaid.error instanceof ApiError ? recordAmountPaid.error.messages.join(' ') : undefined}
          className="sm:w-64"
        >
          <input
            id="amountPaid"
            type="number"
            step="0.01"
            min="0"
            required
            autoFocus
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            className={inputClass}
          />
        </Field>
        <div className="flex gap-2 sm:mb-6">
          <Button variant="secondary" onClick={onDone}>
            Cancel
          </Button>
          <Button type="submit" loading={recordAmountPaid.isPending} disabled={!valid}>
            Save
          </Button>
        </div>
      </form>
    </Card>
  )
}

/** Explains what the invoice's state means for the user, above the document. */
function StateNotice({ invoice }: { invoice: Invoice }) {
  if (invoice.state === 'Draft') {
    return (
      <p className="mb-4 rounded-lg border border-sky-200 bg-sky-50 px-4 py-3 text-sm text-sky-900 dark:border-sky-900/60 dark:bg-sky-950/40 dark:text-sky-200 print:hidden">
        This invoice is a draft. Issue it when it's ready to send: after that it can no longer be edited or deleted.
      </p>
    )
  }

  if (invoice.state === 'Void') {
    return (
      <p className="mb-4 rounded-lg border border-line bg-subtle px-4 py-3 text-sm print:hidden">
        Voided {formatDateTime(invoice.voidedAt)}
        {invoice.voidReason && <>: {invoice.voidReason}</>}
      </p>
    )
  }

  return null
}

function InvoiceDocument({ invoice, tenant }: { invoice: Invoice; tenant?: CurrentTenant }) {
  const money = (amount: number) => formatMoney(amount, invoice.currency)
  const brandColor = tenant?.settings.invoice.brandColor ?? undefined
  const taxLabel = tenant?.settings.invoice.taxLabel ?? 'Tax'
  const footer = tenant?.settings.invoice.footer

  return (
    <Card className="p-6 sm:p-10 print:border-0 print:shadow-none">
      {brandColor && <div className="-mx-6 -mt-6 mb-6 h-1.5 rounded-t-xl sm:-mx-10 sm:-mt-10" style={{ backgroundColor: brandColor }} />}
      <div className="flex flex-col gap-6 sm:flex-row sm:items-start sm:justify-between">
        <div>
          {tenant?.hasLogo && <img src={tenantLogoUrl(tenant.id)} alt={tenant.settings.profile.name} className="mb-4 max-h-14 max-w-48 object-contain" />}
          <p className="text-sm font-medium tracking-wide text-muted uppercase" style={brandColor ? { color: brandColor } : undefined}>
            Invoice
          </p>
          <h1 className="mt-1 text-3xl font-semibold tracking-tight">{invoice.invoiceNumber}</h1>
          <div className="mt-3">
            <StatusBadge status={invoice.status} />
          </div>
        </div>
        <dl className="grid grid-cols-2 gap-x-8 gap-y-3 text-sm sm:text-right">
          <div>
            <dt className="text-muted">Issued</dt>
            <dd className="font-medium">{formatDate(invoice.issueDate)}</dd>
          </div>
          <div>
            <dt className="text-muted">Due</dt>
            <dd className="font-medium">{formatDate(invoice.dueDate)}</dd>
          </div>
          {invoice.paymentTerms && (
            <div className="col-span-2">
              <dt className="text-muted">Terms</dt>
              <dd className="font-medium">{invoice.paymentTerms}</dd>
            </div>
          )}
        </dl>
      </div>

      <div className="mt-10 grid gap-8 sm:grid-cols-2">
        <Party label="From" value={invoice.from} />
        <Party label="Bill to" value={invoice.to} />
      </div>

      <div className="mt-10 overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="border-b border-line text-left text-xs font-medium tracking-wide text-muted uppercase">
            <tr>
              <th scope="col" className="py-3 pr-4">Description</th>
              <th scope="col" className="px-4 py-3 text-right">Qty</th>
              <th scope="col" className="px-4 py-3 text-right">Unit price</th>
              <th scope="col" className="py-3 pl-4 text-right">Amount</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {invoice.items.map((item) => (
              <tr key={item.id}>
                <td className="py-3 pr-4">{item.description}</td>
                <td className="px-4 py-3 text-right tabular-nums">{item.quantity}</td>
                <td className="px-4 py-3 text-right whitespace-nowrap tabular-nums">{money(item.unitPrice)}</td>
                <td className="py-3 pl-4 text-right whitespace-nowrap tabular-nums">{money(item.amount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-6 flex flex-col gap-8 sm:flex-row sm:justify-between">
        <div className="max-w-md text-sm whitespace-pre-line text-muted">{invoice.notes}</div>
        <dl className="w-full space-y-2 text-sm sm:w-72">
          <TotalRow label="Subtotal" value={money(invoice.subtotal)} />
          {invoice.discountAmount > 0 && (
            <TotalRow
              label={invoice.discountType === 'Percentage' ? `Discount (${invoice.discount}%)` : 'Discount'}
              value={`−${money(invoice.discountAmount)}`}
            />
          )}
          {invoice.taxAmount > 0 && (
            <TotalRow
              label={invoice.taxType === 'Percentage' ? `${taxLabel} (${invoice.tax}%)` : taxLabel}
              value={money(invoice.taxAmount)}
            />
          )}
          <TotalRow label="Total" value={money(invoice.total)} strong />
          {invoice.amountPaid > 0 && <TotalRow label="Paid" value={`−${money(invoice.amountPaid)}`} />}
          <div className="flex justify-between rounded-lg bg-subtle px-3 py-2.5 text-base font-semibold">
            <dt>Balance due</dt>
            <dd className="tabular-nums">{money(invoice.balanceDue)}</dd>
          </div>
        </dl>
      </div>

      {footer && <p className="mt-10 border-t border-line pt-4 text-center text-xs whitespace-pre-line text-muted">{footer}</p>}
    </Card>
  )
}

function Party({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs font-medium tracking-wide text-muted uppercase">{label}</p>
      <p className="mt-2 text-sm whitespace-pre-line">{value}</p>
    </div>
  )
}

function TotalRow({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className={`flex justify-between px-3 ${strong ? 'border-t border-line pt-2 font-semibold' : 'text-muted'}`}>
      <dt>{label}</dt>
      <dd className={`tabular-nums ${strong ? '' : 'text-ink'}`}>{value}</dd>
    </div>
  )
}

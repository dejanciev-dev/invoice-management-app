import { ArrowLeft, Ban, Banknote, FileDown, Pencil, Printer, ReceiptText, Send, Trash2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useCan } from '../api/auth'
import { ApiError } from '../api/client'
import {
  invoicePdfUrl,
  useDeleteInvoice,
  useDeletePayment,
  useInvoice,
  useIssueCreditNote,
  useIssueInvoice,
  useRecordPayment,
  useVoidInvoice,
} from '../api/invoices'
import { tenantLogoUrl, useCurrentTenant } from '../api/tenant'
import type { CurrentTenant, Invoice, PaymentMethod } from '../api/types'
import { StatusBadge } from '../components/StatusBadge'
import { buttonClass, inputClass } from '../components/styles'
import { Alert, Button, ButtonLink, Card, EmptyRow, Field, Spinner, tableClass, tdClass, theadClass, thClass } from '../components/ui'
import { formatDate, formatMoney, invoiceTitle, toIsoDate } from '../lib/format'
import { formatDateTime, paymentMethodLabels } from '../lib/labels'
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
      <StateNotice invoice={invoice} nextInvoiceNumber={tenant?.nextInvoiceNumber} />
      <InvoiceDocument invoice={invoice} tenant={tenant} />
      {invoice.state !== 'Draft' && <PaymentHistory invoice={invoice} />}
      {invoice.creditNotes.length > 0 && <CreditNoteList invoice={invoice} />}
    </>
  )
}

type ToolbarMode = 'actions' | 'delete' | 'void' | 'payment' | 'credit'

/**
 * The actions follow the invoice's lifecycle: drafts can be edited, deleted or issued; issued invoices can have
 * payments recorded, be credited, or be voided (unless already credited); void invoices are closed.
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
            <span className="text-sm">Delete this draft?</span>
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
            <a href={invoicePdfUrl(invoice.id)} download className={buttonClass('secondary')}>
              <FileDown className="size-4" aria-hidden />
              PDF
            </a>
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
            {isIssued && canDelete && invoice.creditNotes.length === 0 && (
              <Button variant="secondary" onClick={() => setMode('void')}>
                <Ban className="size-4" aria-hidden />
                Void
              </Button>
            )}
            {isIssued && canEdit && invoice.balanceDue > 0 && (
              <Button variant="secondary" onClick={() => setMode('credit')}>
                <ReceiptText className="size-4" aria-hidden />
                Credit
              </Button>
            )}
            {isIssued && canEdit && invoice.balanceDue > 0 && (
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
      {mode === 'credit' && <CreditNoteForm invoice={invoice} onDone={done} />}
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

/** One payment received. The amount defaults to what is still due, the date to today. */
function PaymentForm({ invoice, onDone }: { invoice: Invoice; onDone: () => void }) {
  const recordPayment = useRecordPayment()
  const today = toIsoDate(new Date())
  const [amount, setAmount] = useState(String(invoice.balanceDue))
  const [date, setDate] = useState(today)
  const [method, setMethod] = useState<PaymentMethod>('BankTransfer')
  const [reference, setReference] = useState('')
  const value = Number(amount)
  const valid = amount.trim() !== '' && Number.isFinite(value) && value > 0 && value <= invoice.balanceDue && Boolean(date)
  const fieldErrors = recordPayment.error instanceof ApiError ? recordPayment.error.problem?.errors : undefined
  const error = (field: string) => fieldErrors?.[field]?.join(' ')

  const submit = (e: FormEvent) => {
    e.preventDefault()
    recordPayment.mutate(
      { id: invoice.id, payment: { amount: value, date, method, reference: reference.trim() || null, notes: null } },
      { onSuccess: onDone },
    )
  }

  return (
    <Card className="mt-4 p-4">
      <form onSubmit={submit} className="space-y-3">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Field
            label={`Amount (${invoice.currency})`}
            htmlFor="paymentAmount"
            hint={`${formatMoney(invoice.balanceDue, invoice.currency)} still due.`}
            error={error('amount')}
          >
            <input
              id="paymentAmount"
              type="number"
              step="0.01"
              min="0.01"
              max={invoice.balanceDue}
              required
              autoFocus
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              className={inputClass}
            />
          </Field>
          <Field label="Received on" htmlFor="paymentDate" error={error('date')}>
            <input id="paymentDate" type="date" required max={today} value={date} onChange={(e) => setDate(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Method" htmlFor="paymentMethod" error={error('method')}>
            <select id="paymentMethod" value={method} onChange={(e) => setMethod(e.target.value as PaymentMethod)} className={inputClass}>
              {Object.entries(paymentMethodLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Reference" htmlFor="paymentReference" error={error('reference')}>
            <input
              id="paymentReference"
              maxLength={100}
              value={reference}
              onChange={(e) => setReference(e.target.value)}
              placeholder="e.g. bank reference"
              className={inputClass}
            />
          </Field>
        </div>
        {recordPayment.error && !fieldErrors && <Alert>{recordPayment.error.message}</Alert>}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onDone}>
            Cancel
          </Button>
          <Button type="submit" loading={recordPayment.isPending} disabled={!valid}>
            Record payment
          </Button>
        </div>
      </form>
    </Card>
  )
}

/** The payments received on an issued or void invoice. Mistakes can be deleted while the invoice is issued. */
function PaymentHistory({ invoice }: { invoice: Invoice }) {
  const deletePayment = useDeletePayment()
  const canDelete = useCan('InvoicesDelete') && invoice.state === 'Issued'
  const [confirmingId, setConfirmingId] = useState<number | null>(null)
  const money = (amount: number) => formatMoney(amount, invoice.currency)

  return (
    <Card className="mt-6 overflow-hidden print:hidden">
      <h2 className="border-b border-line px-4 py-3 text-sm font-semibold">Payments</h2>
      {deletePayment.error && (
        <div className="p-4 pb-0">
          <Alert>{deletePayment.error.message}</Alert>
        </div>
      )}
      <div className="overflow-x-auto">
        <table className={tableClass}>
          <thead className={theadClass}>
            <tr>
              <th scope="col" className={thClass}>Received</th>
              <th scope="col" className={thClass}>Method</th>
              <th scope="col" className={`${thClass} hidden sm:table-cell`}>Reference</th>
              <th scope="col" className={`${thClass} text-right`}>Amount</th>
              {canDelete && (
                <th scope="col" className={thClass}>
                  <span className="sr-only">Actions</span>
                </th>
              )}
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {invoice.payments.length === 0 && <EmptyRow colSpan={canDelete ? 5 : 4}>No payments recorded yet.</EmptyRow>}
            {invoice.payments.map((payment) => (
              <tr key={payment.id}>
                <td className={`${tdClass} whitespace-nowrap`}>{formatDate(payment.date)}</td>
                <td className={tdClass}>{paymentMethodLabels[payment.method]}</td>
                <td className={`${tdClass} hidden text-muted sm:table-cell`} title={payment.notes ?? undefined}>
                  {payment.reference ?? '—'}
                </td>
                <td className={`${tdClass} text-right whitespace-nowrap tabular-nums`}>{money(payment.amount)}</td>
                {canDelete && (
                  <td className={`${tdClass} text-right whitespace-nowrap`}>
                    {confirmingId === payment.id ? (
                      <span className="inline-flex items-center gap-2">
                        <Button variant="secondary" onClick={() => setConfirmingId(null)}>
                          Keep
                        </Button>
                        <Button
                          variant="danger"
                          loading={deletePayment.isPending}
                          onClick={() =>
                            deletePayment.mutate({ id: invoice.id, paymentId: payment.id }, { onSuccess: () => setConfirmingId(null) })
                          }
                        >
                          Delete
                        </Button>
                      </span>
                    ) : (
                      <Button variant="ghost" aria-label={`Delete payment of ${money(payment.amount)}`} onClick={() => setConfirmingId(payment.id)}>
                        <Trash2 className="size-4" aria-hidden />
                      </Button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

/** Credits part or all of the balance due. The amount includes tax; the server splits the tax out. */
function CreditNoteForm({ invoice, onDone }: { invoice: Invoice; onDone: () => void }) {
  const issueCreditNote = useIssueCreditNote()
  const today = toIsoDate(new Date())
  const [amount, setAmount] = useState(String(invoice.balanceDue))
  const [date, setDate] = useState(today)
  const [reason, setReason] = useState('')
  const value = Number(amount)
  const valid =
    amount.trim() !== '' && Number.isFinite(value) && value > 0 && value <= invoice.balanceDue && Boolean(date) && Boolean(reason.trim())
  const fieldErrors = issueCreditNote.error instanceof ApiError ? issueCreditNote.error.problem?.errors : undefined
  const error = (field: string) => fieldErrors?.[field]?.join(' ')

  const submit = (e: FormEvent) => {
    e.preventDefault()
    issueCreditNote.mutate({ id: invoice.id, creditNote: { amount: value, date, reason: reason.trim() } }, { onSuccess: onDone })
  }

  return (
    <Card className="mt-4 p-4">
      <form onSubmit={submit} className="space-y-3">
        <p className="text-sm">
          A credit note reduces what is owed on invoice <span className="font-medium">{invoice.invoiceNumber}</span>. It gets
          its own number and cannot be changed or deleted afterwards.
        </p>
        <div className="grid gap-3 sm:grid-cols-3">
          <Field
            label={`Amount incl. tax (${invoice.currency})`}
            htmlFor="creditAmount"
            hint={`Up to ${formatMoney(invoice.balanceDue, invoice.currency)}.`}
            error={error('amount')}
          >
            <input
              id="creditAmount"
              type="number"
              step="0.01"
              min="0.01"
              max={invoice.balanceDue}
              required
              autoFocus
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              className={inputClass}
            />
          </Field>
          <Field label="Date" htmlFor="creditDate" error={error('date')}>
            <input id="creditDate" type="date" required max={today} value={date} onChange={(e) => setDate(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Reason" htmlFor="creditReason" error={error('reason')}>
            <input
              id="creditReason"
              required
              maxLength={500}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g. Returned goods"
              className={inputClass}
            />
          </Field>
        </div>
        {issueCreditNote.error && !fieldErrors && <Alert>{issueCreditNote.error.message}</Alert>}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onDone}>
            Cancel
          </Button>
          <Button type="submit" loading={issueCreditNote.isPending} disabled={!valid}>
            Issue credit note
          </Button>
        </div>
      </form>
    </Card>
  )
}

function CreditNoteList({ invoice }: { invoice: Invoice }) {
  const money = (amount: number) => formatMoney(amount, invoice.currency)

  return (
    <Card className="mt-6 overflow-hidden print:hidden">
      <h2 className="border-b border-line px-4 py-3 text-sm font-semibold">Credit notes</h2>
      <div className="overflow-x-auto">
        <table className={tableClass}>
          <thead className={theadClass}>
            <tr>
              <th scope="col" className={thClass}>Number</th>
              <th scope="col" className={thClass}>Date</th>
              <th scope="col" className={thClass}>Reason</th>
              <th scope="col" className={`${thClass} hidden text-right sm:table-cell`}>Tax</th>
              <th scope="col" className={`${thClass} text-right`}>Amount</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {invoice.creditNotes.map((creditNote) => (
              <tr key={creditNote.id}>
                <td className={`${tdClass} font-medium whitespace-nowrap`}>{creditNote.number}</td>
                <td className={`${tdClass} whitespace-nowrap`}>{formatDate(creditNote.date)}</td>
                <td className={tdClass}>{creditNote.reason}</td>
                <td className={`${tdClass} hidden text-right whitespace-nowrap text-muted tabular-nums sm:table-cell`}>
                  {money(creditNote.taxAmount)}
                </td>
                <td className={`${tdClass} text-right whitespace-nowrap tabular-nums`}>{money(creditNote.amount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

/** Explains what the invoice's state means for the user, above the document. */
function StateNotice({ invoice, nextInvoiceNumber }: { invoice: Invoice; nextInvoiceNumber?: string }) {
  if (invoice.state === 'Draft') {
    return (
      <p className="mb-4 rounded-lg border border-sky-200 bg-sky-50 px-4 py-3 text-sm text-sky-900 dark:border-sky-900/60 dark:bg-sky-950/40 dark:text-sky-200 print:hidden">
        This invoice is a draft. Issuing it gives it the next invoice number{nextInvoiceNumber && <> ({nextInvoiceNumber})</>} and makes it
        final: after that it can no longer be edited or deleted.
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
          <h1 className="mt-1 text-3xl font-semibold tracking-tight">{invoiceTitle(invoice)}</h1>
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
          {invoice.creditedAmount > 0 && <TotalRow label="Credited" value={`−${money(invoice.creditedAmount)}`} />}
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

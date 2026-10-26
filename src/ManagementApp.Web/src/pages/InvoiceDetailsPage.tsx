import { ArrowLeft, Pencil, Printer, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ApiError } from '../api/client'
import { useDeleteInvoice, useInvoice } from '../api/invoices'
import type { Invoice } from '../api/types'
import { StatusBadge } from '../components/StatusBadge'
import { Alert, Button, ButtonLink, Card, Spinner } from '../components/ui'
import { formatDate, formatMoney } from '../lib/format'
import { NotFoundPage } from './NotFoundPage'

export function InvoiceDetailsPage() {
  const id = Number(useParams().id)
  if (!Number.isInteger(id)) return <NotFoundPage message="That invoice doesn't exist." />
  return <InvoiceDetails id={id} />
}

function InvoiceDetails({ id }: { id: number }) {
  const { data: invoice, error, isPending } = useInvoice(id)

  if (isPending) return <Spinner label="Loading invoice" />
  if (error instanceof ApiError && error.status === 404) {
    return <NotFoundPage message="That invoice doesn't exist or was deleted." />
  }
  if (error) return <Alert>Could not load the invoice. Please refresh the page.</Alert>

  return (
    <>
      <Toolbar invoice={invoice} />
      <InvoiceDocument invoice={invoice} />
    </>
  )
}

function Toolbar({ invoice }: { invoice: Invoice }) {
  const navigate = useNavigate()
  const deleteInvoice = useDeleteInvoice()
  const [confirming, setConfirming] = useState(false)

  const remove = () =>
    deleteInvoice.mutate(invoice.id, { onSuccess: () => navigate('/', { replace: true }) })

  return (
    <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between print:hidden">
      <Link to="/" className="inline-flex items-center gap-1.5 text-sm text-muted hover:text-ink">
        <ArrowLeft className="size-4" aria-hidden />
        All invoices
      </Link>
      {confirming ? (
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm">Delete invoice {invoice.invoiceNumber}?</span>
          <Button variant="secondary" onClick={() => setConfirming(false)}>
            Cancel
          </Button>
          <Button variant="danger" onClick={remove} loading={deleteInvoice.isPending}>
            Delete
          </Button>
        </div>
      ) : (
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" onClick={() => window.print()}>
            <Printer className="size-4" aria-hidden />
            Print
          </Button>
          <Button variant="secondary" onClick={() => setConfirming(true)}>
            <Trash2 className="size-4" aria-hidden />
            Delete
          </Button>
          <ButtonLink to={`/invoices/${invoice.id}/edit`}>
            <Pencil className="size-4" aria-hidden />
            Edit
          </ButtonLink>
        </div>
      )}
    </div>
  )
}

function InvoiceDocument({ invoice }: { invoice: Invoice }) {
  const money = (amount: number) => formatMoney(amount, invoice.currency)

  return (
    <Card className="p-6 sm:p-10 print:border-0 print:shadow-none">
      <div className="flex flex-col gap-6 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p className="text-sm font-medium tracking-wide text-muted uppercase">Invoice</p>
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
              label={invoice.taxType === 'Percentage' ? `Tax (${invoice.tax}%)` : 'Tax'}
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

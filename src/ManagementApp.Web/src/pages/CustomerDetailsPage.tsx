import { ArrowLeft, Pencil, Trash2 } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useCan } from '../api/auth'
import { ApiError } from '../api/client'
import { useCustomer, useDeleteCustomer } from '../api/customers'
import { useCurrentTenant } from '../api/tenant'
import type { Customer } from '../api/types'
import { Alert, Button, ButtonLink, Card, Spinner } from '../components/ui'
import { errorText } from '../lib/errors'
import { NotFoundPage } from './NotFoundPage'

export function CustomerDetailsPage() {
  const id = Number(useParams().id)
  if (!Number.isInteger(id)) return <NotFoundPage message="That customer doesn't exist." />
  return <CustomerDetails id={id} />
}

function CustomerDetails({ id }: { id: number }) {
  const { data: customer, error, isPending } = useCustomer(id)

  if (isPending) return <Spinner label="Loading customer" />
  if (error instanceof ApiError && error.status === 404) {
    return <NotFoundPage message="That customer doesn't exist or was deleted." />
  }
  if (error) return <Alert>Could not load the customer. Please refresh the page.</Alert>

  return (
    <>
      <Toolbar customer={customer} />
      <h1 className="mb-6 text-2xl font-semibold tracking-tight break-words">{customer.name}</h1>
      <div className="grid gap-6 md:grid-cols-2">
        <Card className="p-6">
          <h2 className="text-base font-semibold">Contact</h2>
          <dl className="mt-4 space-y-3 text-sm">
            <Detail label="Email">{customer.email && <a href={`mailto:${customer.email}`} className="break-all hover:text-brand">{customer.email}</a>}</Detail>
            <Detail label="Phone">{customer.phone && <a href={`tel:${customer.phone}`} className="hover:text-brand">{customer.phone}</a>}</Detail>
            <Detail label="Billing address">{customer.address && <span className="whitespace-pre-line">{customer.address}</span>}</Detail>
            <Detail label="Tax number">{customer.taxNumber}</Detail>
          </dl>
        </Card>
        <InvoicingDefaults customer={customer} />
      </div>
    </>
  )
}

function InvoicingDefaults({ customer }: { customer: Customer }) {
  const { data: tenant } = useCurrentTenant()
  const companyCurrency = tenant?.settings.profile.defaultCurrency
  const companyTerms = tenant?.settings.invoice.defaultPaymentTermsDays
  const days = (n: number) => `${n} ${n === 1 ? 'day' : 'days'}`

  return (
    <Card className="p-6">
      <h2 className="text-base font-semibold">Invoicing defaults</h2>
      <dl className="mt-4 space-y-3 text-sm">
        <Detail label="Currency">
          {customer.currency ?? <span className="text-muted">Company default{companyCurrency ? ` (${companyCurrency})` : ''}</span>}
        </Detail>
        <Detail label="Payment terms">
          {customer.paymentTermsDays !== null ? (
            days(customer.paymentTermsDays)
          ) : (
            <span className="text-muted">Company default{companyTerms !== undefined ? ` (${days(companyTerms)})` : ''}</span>
          )}
        </Detail>
        <Detail label="Internal notes">{customer.notes && <span className="whitespace-pre-line">{customer.notes}</span>}</Detail>
      </dl>
    </Card>
  )
}

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-muted">{label}</dt>
      <dd className="mt-0.5 font-medium">{children || <span className="font-normal text-muted">—</span>}</dd>
    </div>
  )
}

function Toolbar({ customer }: { customer: Customer }) {
  const navigate = useNavigate()
  const deleteCustomer = useDeleteCustomer()
  const [confirming, setConfirming] = useState(false)
  const canEdit = useCan('InvoicesEdit')
  const canDelete = useCan('InvoicesDelete')
  const error = errorText(deleteCustomer.error)

  const remove = () => deleteCustomer.mutate(customer.id, { onSuccess: () => navigate('/customers', { replace: true }) })

  return (
    <div className="mb-6 space-y-3">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Link to="/customers" className="inline-flex items-center gap-1.5 text-sm text-muted hover:text-ink">
          <ArrowLeft className="size-4" aria-hidden />
          All customers
        </Link>
        {confirming ? (
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-sm">Delete {customer.name}?</span>
            <Button variant="secondary" onClick={() => setConfirming(false)}>
              Cancel
            </Button>
            <Button variant="danger" onClick={remove} loading={deleteCustomer.isPending}>
              Delete
            </Button>
          </div>
        ) : (
          <div className="flex flex-wrap gap-2">
            {canDelete && (
              <Button variant="secondary" onClick={() => setConfirming(true)}>
                <Trash2 className="size-4" aria-hidden />
                Delete
              </Button>
            )}
            {canEdit && (
              <ButtonLink to={`/customers/${customer.id}/edit`}>
                <Pencil className="size-4" aria-hidden />
                Edit
              </ButtonLink>
            )}
          </div>
        )}
      </div>
      {error && <Alert>{error}</Alert>}
    </div>
  )
}

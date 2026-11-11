import { useNavigate, useParams } from 'react-router'
import { ApiError } from '../api/client'
import { useInvoice, useSaveInvoice } from '../api/invoices'
import { useCurrentTenant } from '../api/tenant'
import { InvoiceForm } from '../components/InvoiceForm'
import { Alert, PageHeader, Spinner } from '../components/ui'
import { invoiceToValues, newInvoiceValues, valuesToRequest, type InvoiceFormValues } from '../lib/invoiceForm'
import { NotFoundPage } from './NotFoundPage'

export function InvoiceEditorPage() {
  const { id } = useParams()
  if (id === undefined) return <CreateNew />

  const invoiceId = Number(id)
  if (!Number.isInteger(invoiceId)) return <NotFoundPage message="That invoice doesn't exist." />
  return <EditExisting id={invoiceId} />
}

/** New invoices start from the company's numbering sequence, currency, terms, tax and details. */
function CreateNew() {
  const { data: tenant, isPending } = useCurrentTenant()
  if (isPending) return <Spinner label="Loading" />

  const defaults = tenant
    ? newInvoiceValues(undefined, {
        invoiceNumber: tenant.nextInvoiceNumber,
        currency: tenant.settings.profile.defaultCurrency,
        paymentTermsDays: tenant.settings.invoice.defaultPaymentTermsDays,
        taxRate: tenant.settings.invoice.defaultTaxRate,
        from: tenant.invoiceSenderDetails,
        notes: tenant.settings.invoice.defaultNotes,
      })
    : newInvoiceValues()

  return <Editor defaultValues={defaults} />
}

function EditExisting({ id }: { id: number }) {
  const { data: invoice, error, isPending } = useInvoice(id)

  if (isPending) return <Spinner label="Loading invoice" />
  if (error instanceof ApiError && error.status === 404) {
    return <NotFoundPage message="That invoice doesn't exist or was deleted." />
  }
  if (error) return <Alert>Could not load the invoice. Please refresh the page.</Alert>

  return <Editor id={id} defaultValues={invoiceToValues(invoice)} />
}

function Editor({ id, defaultValues }: { id?: number; defaultValues: InvoiceFormValues }) {
  const navigate = useNavigate()
  const save = useSaveInvoice(id)
  const { data: tenant } = useCurrentTenant()
  const isNew = id === undefined
  const companyDefaults = tenant && {
    currency: tenant.settings.profile.defaultCurrency,
    paymentTermsDays: tenant.settings.invoice.defaultPaymentTermsDays,
    taxLabel: tenant.settings.invoice.taxLabel,
  }

  const submit = (values: InvoiceFormValues) =>
    save.mutate(valuesToRequest(values), {
      onSuccess: (invoice) => navigate(`/invoices/${invoice.id}`, { replace: !isNew }),
    })

  return (
    <>
      <PageHeader
        title={isNew ? 'New invoice' : `Edit ${defaultValues.invoiceNumber}`}
        description={isNew ? 'Totals update as you type.' : 'Changes are saved when you click Save changes.'}
      />
      <InvoiceForm
        defaultValues={defaultValues}
        onSubmit={submit}
        onCancel={() => navigate(isNew ? '/' : `/invoices/${id}`)}
        submitLabel={isNew ? 'Create invoice' : 'Save changes'}
        submitting={save.isPending}
        serverError={save.error}
        companyDefaults={companyDefaults || undefined}
      />
    </>
  )
}

import { useNavigate, useParams } from 'react-router'
import { ApiError } from '../api/client'
import { useCustomer, useSaveCustomer } from '../api/customers'
import { useCurrentTenant } from '../api/tenant'
import type { SaveCustomerRequest } from '../api/types'
import { CustomerForm } from '../components/CustomerForm'
import { Alert, PageHeader, Spinner } from '../components/ui'
import { customerToRequest, emptyCustomer } from '../lib/customerForm'
import { NotFoundPage } from './NotFoundPage'

export function CustomerEditorPage() {
  const { id } = useParams()
  if (id === undefined) return <Editor defaultValues={emptyCustomer} />

  const customerId = Number(id)
  if (!Number.isInteger(customerId)) return <NotFoundPage message="That customer doesn't exist." />
  return <EditExisting id={customerId} />
}

function EditExisting({ id }: { id: number }) {
  const { data: customer, error, isPending } = useCustomer(id)

  if (isPending) return <Spinner label="Loading customer" />
  if (error instanceof ApiError && error.status === 404) {
    return <NotFoundPage message="That customer doesn't exist or was deleted." />
  }
  if (error) return <Alert>Could not load the customer. Please refresh the page.</Alert>

  return <Editor id={id} defaultValues={customerToRequest(customer)} />
}

function Editor({ id, defaultValues }: { id?: number; defaultValues: SaveCustomerRequest }) {
  const navigate = useNavigate()
  const save = useSaveCustomer(id)
  const { data: tenant } = useCurrentTenant()
  const isNew = id === undefined

  return (
    <>
      <PageHeader
        title={isNew ? 'New customer' : `Edit ${defaultValues.name}`}
        description={isNew ? 'Only the name is required.' : 'Changes are saved when you click Save changes.'}
      />
      <CustomerForm
        defaultValues={defaultValues}
        onSubmit={(request) =>
          save.mutate(request, { onSuccess: (customer) => navigate(`/customers/${customer.id}`, { replace: !isNew }) })
        }
        onCancel={() => navigate(isNew ? '/customers' : `/customers/${id}`)}
        submitLabel={isNew ? 'Create customer' : 'Save changes'}
        submitting={save.isPending}
        serverError={save.error}
        companyCurrency={tenant?.settings.profile.defaultCurrency}
        companyPaymentTermsDays={tenant?.settings.invoice.defaultPaymentTermsDays}
      />
    </>
  )
}

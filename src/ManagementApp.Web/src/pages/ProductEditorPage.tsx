import { ArrowLeft, Trash2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useCan, useCurrentUser } from '../api/auth'
import { ApiError } from '../api/client'
import { useDeleteProduct, useProduct, useSaveProduct } from '../api/products'
import type { SaveProductRequest } from '../api/types'
import { inputClass } from '../components/styles'
import { Alert, Button, Card, Field, PageHeader, Spinner } from '../components/ui'
import { errorText, fieldError, unmappedError } from '../lib/errors'
import { NotFoundPage } from './NotFoundPage'

const emptyProduct: SaveProductRequest = { name: '', unit: null, unitPrice: 0 }

export function ProductEditorPage() {
  const { id } = useParams()
  if (id === undefined) return <Editor defaultValues={emptyProduct} />

  const productId = Number(id)
  if (!Number.isInteger(productId)) return <NotFoundPage message="That product doesn't exist." />
  return <EditExisting id={productId} />
}

function EditExisting({ id }: { id: number }) {
  const { data: product, error, isPending } = useProduct(id)

  if (isPending) return <Spinner label="Loading product" />
  if (error instanceof ApiError && error.status === 404) {
    return <NotFoundPage message="That product doesn't exist or was deleted." />
  }
  if (error) return <Alert>Could not load the product. Please refresh the page.</Alert>

  return <Editor id={id} defaultValues={{ name: product.name, unit: product.unit, unitPrice: product.unitPrice }} />
}

function Editor({ id, defaultValues }: { id?: number; defaultValues: SaveProductRequest }) {
  const navigate = useNavigate()
  const save = useSaveProduct(id)
  const { data: session } = useCurrentUser()
  const currency = session?.activeTenant?.defaultCurrency ?? 'USD'
  const [values, setValues] = useState(defaultValues)
  const [price, setPrice] = useState(String(defaultValues.unitPrice))
  const isNew = id === undefined
  const unmapped = unmappedError(save.error)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    save.mutate(
      { name: values.name.trim(), unit: values.unit?.trim() || null, unitPrice: Number(price) },
      { onSuccess: () => navigate('/products') },
    )
  }

  return (
    <>
      <Link to="/products" className="mb-4 inline-flex items-center gap-1.5 text-sm text-muted hover:text-ink">
        <ArrowLeft className="size-4" aria-hidden />
        All products
      </Link>
      <PageHeader
        title={isNew ? 'New product or service' : `Edit ${defaultValues.name}`}
        description="Invoices copy the name and price when the product is added, so changes here never alter existing invoices."
      />

      <form onSubmit={submit} className="max-w-xl space-y-6">
        {unmapped && <Alert>{unmapped}</Alert>}
        <Card className="grid gap-4 p-6 sm:grid-cols-2">
          <Field label="Name" htmlFor="product-name" error={fieldError(save.error, 'name')} hint="Shown as the invoice line's description." className="sm:col-span-2">
            <input
              id="product-name"
              required
              maxLength={500}
              value={values.name}
              onChange={(e) => setValues({ ...values, name: e.target.value })}
              className={inputClass}
            />
          </Field>
          <Field label={`Unit price (${currency})`} htmlFor="product-price" error={fieldError(save.error, 'unitPrice')}>
            <input
              id="product-price"
              type="number"
              required
              min={0}
              step="0.01"
              inputMode="decimal"
              value={price}
              onChange={(e) => setPrice(e.target.value)}
              className={inputClass}
            />
          </Field>
          <Field label="Unit" htmlFor="product-unit" error={fieldError(save.error, 'unit')} hint='e.g. "hour" or "month". Leave empty for a single item.'>
            <input
              id="product-unit"
              maxLength={20}
              value={values.unit ?? ''}
              onChange={(e) => setValues({ ...values, unit: e.target.value })}
              className={inputClass}
            />
          </Field>
        </Card>

        <div className="flex flex-wrap items-center justify-between gap-2">
          {isNew ? <span /> : <DeleteButton id={id} name={defaultValues.name} />}
          <div className="flex gap-2">
            <Button variant="secondary" onClick={() => navigate('/products')}>
              Cancel
            </Button>
            <Button type="submit" loading={save.isPending}>
              {isNew ? 'Add product' : 'Save changes'}
            </Button>
          </div>
        </div>
      </form>
    </>
  )
}

function DeleteButton({ id, name }: { id: number; name: string }) {
  const navigate = useNavigate()
  const deleteProduct = useDeleteProduct()
  const canDelete = useCan('InvoicesDelete')
  const [confirming, setConfirming] = useState(false)
  const error = errorText(deleteProduct.error)
  if (!canDelete) return <span />

  return confirming ? (
    <div className="flex flex-wrap items-center gap-2">
      <span className="text-sm">Remove {name} from the catalogue?</span>
      <Button variant="secondary" onClick={() => setConfirming(false)}>
        Keep
      </Button>
      <Button
        variant="danger"
        loading={deleteProduct.isPending}
        onClick={() => deleteProduct.mutate(id, { onSuccess: () => navigate('/products', { replace: true }) })}
      >
        Delete
      </Button>
      {error && <p className="w-full text-sm text-red-600 dark:text-red-400">{error}</p>}
    </div>
  ) : (
    <Button variant="secondary" onClick={() => setConfirming(true)}>
      <Trash2 className="size-4" aria-hidden />
      Delete
    </Button>
  )
}

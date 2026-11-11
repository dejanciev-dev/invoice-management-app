import { Package, Plus, Search } from 'lucide-react'
import { useDeferredValue, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { useCan, useCurrentUser } from '../api/auth'
import { useProducts } from '../api/products'
import type { Product } from '../api/types'
import { inputClass } from '../components/styles'
import { Alert, ButtonLink, Card, PageHeader, Pager, Spinner, tableClass, tdClass, thClass, theadClass } from '../components/ui'
import { formatUnitPrice } from '../lib/format'

export function ProductsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const page = Math.max(1, Number(searchParams.get('page')) || 1)
  const [search, setSearch] = useState(searchParams.get('search') ?? '')
  const deferredSearch = useDeferredValue(search.trim())
  const products = useProducts({ search: deferredSearch || undefined, page })
  const canCreate = useCan('InvoicesCreate')

  /** A new search starts again from the first page. */
  const updateParam = (key: 'search' | 'page', value: string | undefined) =>
    setSearchParams(
      (params) => {
        if (value) params.set(key, value)
        else params.delete(key)
        if (key !== 'page') params.delete('page')
        return params
      },
      { replace: true },
    )

  return (
    <>
      <PageHeader
        title="Products & services"
        description="What you sell, with its usual price. Add them to invoices from the catalogue."
        actions={
          canCreate && (
            <ButtonLink to="/products/new">
              <Plus className="size-4" aria-hidden />
              New product
            </ButtonLink>
          )
        }
      />

      <Card className="overflow-hidden">
        <div className="border-b border-line p-4">
          <div className="relative lg:w-72">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
            <input
              type="search"
              aria-label="Search products"
              placeholder="Search by name"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value)
                updateParam('search', e.target.value.trim() || undefined)
              }}
              className={`${inputClass} pl-9`}
            />
          </div>
        </div>

        {products.isPending ? (
          <Spinner label="Loading products" />
        ) : products.isError ? (
          <div className="p-4">
            <Alert>Could not load products. Please refresh the page.</Alert>
          </div>
        ) : products.data.items.length === 0 ? (
          <EmptyState filtered={Boolean(deferredSearch)} canCreate={canCreate} />
        ) : (
          <>
            <ProductTable products={products.data.items} />
            <Pager
              page={products.data.page}
              pageSize={products.data.pageSize}
              total={products.data.total}
              onChange={(next) => updateParam('page', next > 1 ? String(next) : undefined)}
            />
          </>
        )}
      </Card>
    </>
  )
}

function ProductTable({ products }: { products: Product[] }) {
  const navigate = useNavigate()
  const canEdit = useCan('InvoicesEdit')
  const { data: session } = useCurrentUser()
  const currency = session?.activeTenant?.defaultCurrency ?? 'USD'

  return (
    <div className="overflow-x-auto">
      <table className={tableClass}>
        <thead className={theadClass}>
          <tr>
            <th scope="col" className={thClass}>Name</th>
            <th scope="col" className={`${thClass} text-right`}>Price</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {products.map((product) => (
            <tr
              key={product.id}
              onClick={canEdit ? () => navigate(`/products/${product.id}`) : undefined}
              className={canEdit ? 'cursor-pointer transition-colors hover:bg-subtle/60' : undefined}
            >
              <td className={`${tdClass} font-medium`}>
                {canEdit ? (
                  <Link to={`/products/${product.id}`} onClick={(e) => e.stopPropagation()} className="hover:text-brand">
                    {product.name}
                  </Link>
                ) : (
                  product.name
                )}
              </td>
              <td className={`${tdClass} text-right whitespace-nowrap tabular-nums`}>{formatUnitPrice(product, currency)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function EmptyState({ filtered, canCreate }: { filtered: boolean; canCreate: boolean }) {
  return (
    <div className="px-4 py-16 text-center">
      <Package className="mx-auto size-10 text-muted/60" aria-hidden />
      <h2 className="mt-3 text-sm font-semibold">{filtered ? 'No matching products' : 'Your catalogue is empty'}</h2>
      <p className="mt-1 text-sm text-muted">
        {filtered ? 'Try a different search.' : 'Add the products and services you sell to fill in invoice lines faster.'}
      </p>
      {!filtered && canCreate && (
        <ButtonLink to="/products/new" className="mt-5">
          <Plus className="size-4" aria-hidden />
          New product
        </ButtonLink>
      )}
    </div>
  )
}

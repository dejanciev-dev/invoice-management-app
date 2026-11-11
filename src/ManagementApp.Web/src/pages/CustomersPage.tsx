import { Plus, Search, Users } from 'lucide-react'
import { useDeferredValue, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { useCan } from '../api/auth'
import { useCustomers } from '../api/customers'
import type { Customer } from '../api/types'
import { inputClass } from '../components/styles'
import { Alert, ButtonLink, Card, PageHeader, Pager, Spinner, tableClass, tdClass, thClass, theadClass } from '../components/ui'

export function CustomersPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const page = Math.max(1, Number(searchParams.get('page')) || 1)
  const [search, setSearch] = useState(searchParams.get('search') ?? '')
  const deferredSearch = useDeferredValue(search.trim())
  const customers = useCustomers({ search: deferredSearch || undefined, page })
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
        title="Customers"
        description="The people and companies you bill."
        actions={
          canCreate && (
            <ButtonLink to="/customers/new">
              <Plus className="size-4" aria-hidden />
              New customer
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
              aria-label="Search customers"
              placeholder="Search name, email or tax number"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value)
                updateParam('search', e.target.value.trim() || undefined)
              }}
              className={`${inputClass} pl-9`}
            />
          </div>
        </div>

        {customers.isPending ? (
          <Spinner label="Loading customers" />
        ) : customers.isError ? (
          <div className="p-4">
            <Alert>Could not load customers. Please refresh the page.</Alert>
          </div>
        ) : customers.data.items.length === 0 ? (
          <EmptyState filtered={Boolean(deferredSearch)} canCreate={canCreate} />
        ) : (
          <>
            <CustomerTable customers={customers.data.items} />
            <Pager
              page={customers.data.page}
              pageSize={customers.data.pageSize}
              total={customers.data.total}
              onChange={(next) => updateParam('page', next > 1 ? String(next) : undefined)}
            />
          </>
        )}
      </Card>
    </>
  )
}

function CustomerTable({ customers }: { customers: Customer[] }) {
  const navigate = useNavigate()

  return (
    <div className="overflow-x-auto">
      <table className={tableClass}>
        <thead className={theadClass}>
          <tr>
            <th scope="col" className={thClass}>Name</th>
            <th scope="col" className={`${thClass} hidden sm:table-cell`}>Email</th>
            <th scope="col" className={`${thClass} hidden md:table-cell`}>Tax number</th>
            <th scope="col" className={`${thClass} hidden md:table-cell`}>Currency</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {customers.map((customer) => (
            <tr
              key={customer.id}
              onClick={() => navigate(`/customers/${customer.id}`)}
              className="cursor-pointer transition-colors hover:bg-subtle/60"
            >
              <td className={`${tdClass} font-medium`}>
                <Link to={`/customers/${customer.id}`} onClick={(e) => e.stopPropagation()} className="hover:text-brand">
                  {customer.name}
                </Link>
                {/* On phones the email is shown under the name instead of in its own column. */}
                {customer.email && <span className="block font-normal break-all text-muted sm:hidden">{customer.email}</span>}
              </td>
              <td className={`${tdClass} hidden text-muted sm:table-cell`}>{customer.email ?? '—'}</td>
              <td className={`${tdClass} hidden text-muted md:table-cell`}>{customer.taxNumber ?? '—'}</td>
              <td className={`${tdClass} hidden text-muted md:table-cell`}>{customer.currency ?? 'Default'}</td>
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
      <Users className="mx-auto size-10 text-muted/60" aria-hidden />
      <h2 className="mt-3 text-sm font-semibold">{filtered ? 'No matching customers' : 'No customers yet'}</h2>
      <p className="mt-1 text-sm text-muted">
        {filtered ? 'Try a different search.' : 'Save the people and companies you bill, with their details and defaults.'}
      </p>
      {!filtered && canCreate && (
        <ButtonLink to="/customers/new" className="mt-5">
          <Plus className="size-4" aria-hidden />
          New customer
        </ButtonLink>
      )}
    </div>
  )
}

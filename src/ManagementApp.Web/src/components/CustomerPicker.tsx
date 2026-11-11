import { UserRound, X } from 'lucide-react'
import { useState } from 'react'
import { useCustomers } from '../api/customers'
import type { Customer } from '../api/types'
import { SearchCombobox } from './SearchCombobox'
import { Button } from './ui'

const RESULT_LIMIT = 8

interface CustomerPickerProps {
  /** The linked customer, if any. */
  value: { id: number; name: string } | null
  onSelect: (customer: Customer) => void
  onClear: () => void
}

function useCustomerResults(search: string | undefined, enabled: boolean) {
  const results = useCustomers({ search, page: 1, pageSize: RESULT_LIMIT }, { enabled })
  return { items: results.data?.items ?? [], isPending: results.isPending }
}

/** Links an invoice to a saved customer, found by searching the company's customers on the server. */
export function CustomerPicker({ value, onSelect, onClear }: CustomerPickerProps) {
  const [searching, setSearching] = useState(value === null)

  if (value && !searching) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-line bg-subtle/40 px-3 py-2">
        <UserRound className="size-4 shrink-0 text-muted" aria-hidden />
        <span className="min-w-0 flex-1 truncate text-sm font-medium">{value.name}</span>
        <Button variant="ghost" className="px-2 py-1" onClick={() => setSearching(true)}>
          Change
        </Button>
        <Button variant="ghost" className="px-2 py-1" aria-label="Remove customer" onClick={onClear}>
          <X className="size-4" aria-hidden />
        </Button>
      </div>
    )
  }

  return (
    <SearchCombobox<Customer>
      id="customer"
      placeholder="Search your customers"
      listLabel="Customers"
      useResults={useCustomerResults}
      emptyText={(search) => (search ? 'No matching customers' : 'No customers yet')}
      renderItem={(customer) => (
        <>
          <span className="block font-medium">{customer.name}</span>
          {(customer.email || customer.address) && (
            <span className="block truncate text-xs text-muted">{customer.email ?? customer.address?.split('\n')[0]}</span>
          )}
        </>
      )}
      onSelect={(customer) => {
        setSearching(false)
        onSelect(customer)
      }}
      onCancel={value ? () => setSearching(false) : undefined}
    />
  )
}

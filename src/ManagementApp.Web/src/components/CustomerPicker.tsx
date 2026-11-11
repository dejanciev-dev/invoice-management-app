import { Search, UserRound, X } from 'lucide-react'
import { useDeferredValue, useId, useState, type KeyboardEvent } from 'react'
import { useCustomers } from '../api/customers'
import type { Customer } from '../api/types'
import { inputClass } from './styles'
import { Button } from './ui'

const RESULT_LIMIT = 8

interface CustomerPickerProps {
  /** The linked customer, if any. */
  value: { id: number; name: string } | null
  onSelect: (customer: Customer) => void
  onClear: () => void
}

/** Links an invoice to a saved customer, found by searching the company's customers on the server. */
export function CustomerPicker({ value, onSelect, onClear }: CustomerPickerProps) {
  const [searching, setSearching] = useState(value === null)

  if (value && !searching) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-line bg-subtle/40 px-3 py-2">
        <UserRound className="size-4 shrink-0 text-muted" aria-hidden />
        <span className="min-w-0 flex-1 truncate text-sm font-medium">
          {value.name}
        </span>
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
    <CustomerSearch
      onSelect={(customer) => {
        setSearching(false)
        onSelect(customer)
      }}
      onCancel={value ? () => setSearching(false) : undefined}
    />
  )
}

function CustomerSearch({ onSelect, onCancel }: { onSelect: (customer: Customer) => void; onCancel?: () => void }) {
  const listId = useId()
  const [text, setText] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const search = useDeferredValue(text.trim())
  const results = useCustomers({ search: search || undefined, page: 1, pageSize: RESULT_LIMIT }, { enabled: open })
  const customers = results.data?.items ?? []
  const showList = open && (customers.length > 0 || !results.isPending)

  const choose = (customer: Customer) => {
    setOpen(false)
    setText('')
    onSelect(customer)
  }

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setOpen(true)
      setActive((i) => Math.min(i + 1, Math.max(customers.length - 1, 0)))
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      setActive((i) => Math.max(i - 1, 0))
    } else if (event.key === 'Enter' && open && customers[active]) {
      event.preventDefault() // choose the customer instead of submitting the invoice
      choose(customers[active])
    } else if (event.key === 'Escape') {
      if (open) setOpen(false)
      else onCancel?.()
    }
  }

  return (
    <div className="relative">
      <div className="flex gap-2">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
          <input
            id="customer"
            role="combobox"
            aria-expanded={showList}
            aria-controls={listId}
            aria-autocomplete="list"
            aria-activedescendant={showList && customers[active] ? `${listId}-${customers[active].id}` : undefined}
            autoComplete="off"
            placeholder="Search your customers"
            value={text}
            onChange={(e) => {
              setText(e.target.value)
              setActive(0)
              setOpen(true)
            }}
            onFocus={() => setOpen(true)}
            onBlur={() => setOpen(false)}
            onKeyDown={onKeyDown}
            className={`${inputClass} pl-9`}
          />
        </div>
        {onCancel && (
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
        )}
      </div>
      {showList && (
        <ul
          id={listId}
          role="listbox"
          aria-label="Customers"
          className="absolute z-20 mt-1 max-h-72 w-full overflow-auto rounded-lg border border-line bg-surface py-1 text-sm shadow-lg"
        >
          {customers.length === 0 ? (
            <li className="px-3 py-2 text-muted">{search ? 'No matching customers' : 'No customers yet'}</li>
          ) : (
            customers.map((customer, index) => (
              <li
                key={customer.id}
                id={`${listId}-${customer.id}`}
                role="option"
                aria-selected={index === active}
                // Chosen on mouse down, before the input's blur closes the list.
                onMouseDown={(e) => {
                  e.preventDefault()
                  choose(customer)
                }}
                onMouseEnter={() => setActive(index)}
                className={`cursor-pointer px-3 py-2 ${index === active ? 'bg-subtle' : ''}`}
              >
                <span className="block font-medium">{customer.name}</span>
                {(customer.email || customer.address) && (
                  <span className="block truncate text-xs text-muted">{customer.email ?? customer.address?.split('\n')[0]}</span>
                )}
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  )
}

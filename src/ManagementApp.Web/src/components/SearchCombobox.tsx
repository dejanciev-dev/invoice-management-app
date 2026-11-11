import { Search } from 'lucide-react'
import { useDeferredValue, useId, useState, type KeyboardEvent, type ReactNode } from 'react'
import { inputClass } from './styles'
import { Button } from './ui'

export interface SearchResults<T> {
  items: T[]
  isPending: boolean
}

interface SearchComboboxProps<T extends { id: number }> {
  /** The id of the input, for a surrounding label. */
  id: string
  placeholder: string
  /** Label of the result list, e.g. "Customers". */
  listLabel: string
  /** A hook that runs the search; `enabled` is only true while the list is open. Pass a module-level function. */
  useResults: (search: string | undefined, enabled: boolean) => SearchResults<T>
  renderItem: (item: T) => ReactNode
  /** Shown when nothing matches the search (or there is nothing at all). */
  emptyText: (search: string) => string
  onSelect: (item: T) => void
  onCancel?: () => void
}

/**
 * A text box that searches on the server as you type and lists the matches. Arrow keys move through them, Enter picks
 * one (without submitting the surrounding form) and Escape closes the list.
 */
export function SearchCombobox<T extends { id: number }>({
  id,
  placeholder,
  listLabel,
  useResults,
  renderItem,
  emptyText,
  onSelect,
  onCancel,
}: SearchComboboxProps<T>) {
  const listId = useId()
  const [text, setText] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const search = useDeferredValue(text.trim())
  const results = useResults(search || undefined, open)
  const items = results.items
  const showList = open && (items.length > 0 || !results.isPending)

  const choose = (item: T) => {
    setOpen(false)
    setText('')
    onSelect(item)
  }

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setOpen(true)
      setActive((i) => Math.min(i + 1, Math.max(items.length - 1, 0)))
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      setActive((i) => Math.max(i - 1, 0))
    } else if (event.key === 'Enter' && open && items[active]) {
      event.preventDefault() // pick the item instead of submitting the form
      choose(items[active])
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
            id={id}
            role="combobox"
            aria-expanded={showList}
            aria-controls={listId}
            aria-autocomplete="list"
            aria-activedescendant={showList && items[active] ? `${listId}-${items[active].id}` : undefined}
            autoComplete="off"
            placeholder={placeholder}
            value={text}
            onChange={(e) => {
              setText(e.target.value)
              setActive(0)
              setOpen(true)
            }}
            onFocus={() => setOpen(true)}
            // The box keeps focus after a pick, so a click must reopen the list too.
            onClick={() => setOpen(true)}
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
          aria-label={listLabel}
          className="absolute z-20 mt-1 max-h-72 w-full overflow-auto rounded-lg border border-line bg-surface py-1 text-sm shadow-lg"
        >
          {items.length === 0 ? (
            <li className="px-3 py-2 text-muted">{emptyText(search)}</li>
          ) : (
            items.map((item, index) => (
              <li
                key={item.id}
                id={`${listId}-${item.id}`}
                role="option"
                aria-selected={index === active}
                // Picked on mouse down, before the input's blur closes the list.
                onMouseDown={(e) => {
                  e.preventDefault()
                  choose(item)
                }}
                onMouseEnter={() => setActive(index)}
                className={`cursor-pointer px-3 py-2 ${index === active ? 'bg-subtle' : ''}`}
              >
                {renderItem(item)}
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  )
}

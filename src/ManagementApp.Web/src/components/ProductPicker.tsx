import { useProducts } from '../api/products'
import type { Product } from '../api/types'
import { formatUnitPrice } from '../lib/format'
import { SearchCombobox } from './SearchCombobox'

const RESULT_LIMIT = 8

function useProductResults(search: string | undefined, enabled: boolean) {
  const results = useProducts({ search, page: 1, pageSize: RESULT_LIMIT }, { enabled })
  return { items: results.data?.items ?? [], isPending: results.isPending }
}

/** Searches the catalogue and hands back the chosen product, to be added as an invoice line. */
export function ProductPicker({ currency, onSelect }: { currency: string; onSelect: (product: Product) => void }) {
  return (
    <>
      <label htmlFor="catalogue" className="sr-only">
        Add from catalogue
      </label>
      <SearchCombobox<Product>
        id="catalogue"
        placeholder="Add from catalogue"
        listLabel="Products and services"
        useResults={useProductResults}
        emptyText={(search) => (search ? 'No matching products' : 'Your catalogue is empty')}
        renderItem={(product) => (
          <span className="flex items-baseline justify-between gap-3">
            <span className="font-medium">{product.name}</span>
            <span className="shrink-0 text-xs text-muted tabular-nums">{formatUnitPrice(product, currency)}</span>
          </span>
        )}
        onSelect={onSelect}
      />
    </>
  )
}

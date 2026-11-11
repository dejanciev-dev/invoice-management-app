import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiRequest, json, query } from './client'
import type { Paged, Product, SaveProductRequest } from './types'

export const PRODUCT_PAGE_SIZE = 25

export interface ProductFilters {
  search?: string
  page?: number
  pageSize?: number
}

export const productKeys = {
  all: ['products'] as const,
  list: (filters: ProductFilters) => [...productKeys.all, 'list', filters] as const,
  detail: (id: number) => [...productKeys.all, 'detail', id] as const,
}

/** One page of the catalogue sorted by name; searching and paging happen on the server. */
export function useProducts(filters: ProductFilters = {}, { enabled = true }: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: productKeys.list(filters),
    queryFn: () =>
      apiRequest<Paged<Product>>(
        `/products${query({ search: filters.search, page: filters.page, pageSize: filters.pageSize ?? PRODUCT_PAGE_SIZE })}`,
      ),
    placeholderData: keepPreviousData,
    enabled,
  })
}

export function useProduct(id: number) {
  return useQuery({
    queryKey: productKeys.detail(id),
    queryFn: () => apiRequest<Product>(`/products/${id}`),
  })
}

export function useSaveProduct(id?: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: SaveProductRequest) =>
      id === undefined
        ? apiRequest<Product>('/products', { method: 'POST', ...json(request) })
        : apiRequest<Product>(`/products/${id}`, { method: 'PUT', ...json(request) }),
    onSuccess: async (product) => {
      queryClient.setQueryData(productKeys.detail(product.id), product)
      await queryClient.invalidateQueries({ queryKey: productKeys.all, refetchType: 'none' })
    },
  })
}

export function useDeleteProduct() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: number) => apiRequest<void>(`/products/${id}`, { method: 'DELETE' }),
    onSuccess: async (_, id) => {
      queryClient.removeQueries({ queryKey: productKeys.detail(id) })
      await queryClient.invalidateQueries({ queryKey: productKeys.all })
    },
  })
}

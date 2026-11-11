import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiRequest, json, query } from './client'
import type { Customer, Paged, SaveCustomerRequest } from './types'

export const CUSTOMER_PAGE_SIZE = 25

export interface CustomerFilters {
  search?: string
  page?: number
  pageSize?: number
}

export const customerKeys = {
  all: ['customers'] as const,
  list: (filters: CustomerFilters) => [...customerKeys.all, 'list', filters] as const,
  detail: (id: number) => [...customerKeys.all, 'detail', id] as const,
}

/** One page of customers sorted by name; searching (name, email, tax number) and paging happen on the server. */
export function useCustomers(filters: CustomerFilters = {}, { enabled = true }: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: customerKeys.list(filters),
    queryFn: () =>
      apiRequest<Paged<Customer>>(
        `/customers${query({ search: filters.search, page: filters.page, pageSize: filters.pageSize ?? CUSTOMER_PAGE_SIZE })}`,
      ),
    placeholderData: keepPreviousData,
    enabled,
  })
}

export function useCustomer(id: number) {
  return useQuery({
    queryKey: customerKeys.detail(id),
    queryFn: () => apiRequest<Customer>(`/customers/${id}`),
  })
}

export function useSaveCustomer(id?: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: SaveCustomerRequest) =>
      id === undefined
        ? apiRequest<Customer>('/customers', { method: 'POST', ...json(request) })
        : apiRequest<Customer>(`/customers/${id}`, { method: 'PUT', ...json(request) }),
    onSuccess: async (customer) => {
      queryClient.setQueryData(customerKeys.detail(customer.id), customer)
      await queryClient.invalidateQueries({ queryKey: customerKeys.all, refetchType: 'none' })
    },
  })
}

export function useDeleteCustomer() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: number) => apiRequest<void>(`/customers/${id}`, { method: 'DELETE' }),
    onSuccess: async (_, id) => {
      queryClient.removeQueries({ queryKey: customerKeys.detail(id) })
      await queryClient.invalidateQueries({ queryKey: customerKeys.all })
    },
  })
}

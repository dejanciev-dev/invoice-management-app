import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiRequest, json } from './client'
import type { Invoice, InvoiceStatus, InvoiceSummary, SaveInvoiceRequest } from './types'

export interface InvoiceFilters {
  search?: string
  status?: InvoiceStatus
}

export const invoiceKeys = {
  all: ['invoices'] as const,
  list: (filters: InvoiceFilters) => [...invoiceKeys.all, 'list', filters] as const,
  detail: (id: number) => [...invoiceKeys.all, 'detail', id] as const,
}

export const INVOICE_EXPORT_URL = '/api/invoices/export'

export function useInvoices(filters: InvoiceFilters = {}) {
  return useQuery({
    queryKey: invoiceKeys.list(filters),
    queryFn: () => {
      const params = new URLSearchParams()
      if (filters.search) params.set('search', filters.search)
      if (filters.status) params.set('status', filters.status)
      const query = params.size > 0 ? `?${params}` : ''
      return apiRequest<InvoiceSummary[]>(`/invoices${query}`)
    },
    placeholderData: keepPreviousData,
  })
}

export function useInvoice(id: number) {
  return useQuery({
    queryKey: invoiceKeys.detail(id),
    queryFn: () => apiRequest<Invoice>(`/invoices/${id}`),
  })
}

export function useSaveInvoice(id?: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: SaveInvoiceRequest) =>
      id === undefined
        ? apiRequest<Invoice>('/invoices', { method: 'POST', ...json(request) })
        : apiRequest<Invoice>(`/invoices/${id}`, { method: 'PUT', ...json(request) }),
    onSuccess: async (invoice) => {
      queryClient.setQueryData(invoiceKeys.detail(invoice.id), invoice)
      await queryClient.invalidateQueries({ queryKey: invoiceKeys.all, refetchType: 'none' })
    },
  })
}

export function useDeleteInvoice() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: number) => apiRequest<void>(`/invoices/${id}`, { method: 'DELETE' }),
    onSuccess: async (_, id) => {
      queryClient.removeQueries({ queryKey: invoiceKeys.detail(id) })
      await queryClient.invalidateQueries({ queryKey: invoiceKeys.all })
    },
  })
}

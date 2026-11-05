import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiRequest, json, query, withTenant } from './client'
import type { Invoice, InvoiceStats, InvoiceStatus, InvoiceSummary, Paged, SaveInvoiceRequest } from './types'

export const INVOICE_PAGE_SIZE = 25

export interface InvoiceFilters {
  search?: string
  status?: InvoiceStatus
  page?: number
}

export const invoiceKeys = {
  all: ['invoices'] as const,
  list: (filters: InvoiceFilters) => [...invoiceKeys.all, 'list', filters] as const,
  stats: () => [...invoiceKeys.all, 'stats'] as const,
  detail: (id: number) => [...invoiceKeys.all, 'detail', id] as const,
}

/** A plain download link, so the active company travels in the query string. */
export const invoiceExportUrl = () => withTenant('/api/invoices/export')

/** One page of invoices; searching, filtering and paging happen on the server. */
export function useInvoices(filters: InvoiceFilters = {}) {
  return useQuery({
    queryKey: invoiceKeys.list(filters),
    queryFn: () =>
      apiRequest<Paged<InvoiceSummary>>(
        `/invoices${query({ search: filters.search, status: filters.status, page: filters.page, pageSize: INVOICE_PAGE_SIZE })}`,
      ),
    placeholderData: keepPreviousData,
  })
}

/** Counts and amounts per currency for the dashboard tiles, across all invoices. */
export function useInvoiceStats() {
  return useQuery({ queryKey: invoiceKeys.stats(), queryFn: () => apiRequest<InvoiceStats>('/invoices/stats') })
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

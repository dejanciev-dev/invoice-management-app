import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiRequest, json, query, withTenant } from './client'
import { tenantKeys } from './tenant'
import type { Invoice, InvoiceStats, InvoiceStatus, InvoiceSummary, Paged, RecordPaymentRequest, SaveInvoiceRequest } from './types'

export const INVOICE_PAGE_SIZE = 25

export interface InvoiceFilters {
  search?: string
  status?: InvoiceStatus
  customerId?: number
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
        `/invoices${query({
          search: filters.search,
          status: filters.status,
          customerId: filters.customerId,
          page: filters.page,
          pageSize: INVOICE_PAGE_SIZE,
        })}`,
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

/** Shared handling for the lifecycle actions: show the returned invoice and refresh lists and totals. */
function useInvoiceTransition<TInput>(request: (input: TInput) => Promise<Invoice>, onDone?: () => Promise<void>) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: request,
    onSuccess: async (invoice) => {
      queryClient.setQueryData(invoiceKeys.detail(invoice.id), invoice)
      await queryClient.invalidateQueries({ queryKey: invoiceKeys.all, refetchType: 'none' })
      await onDone?.()
    },
  })
}

/** Makes a draft final and payable under the company's next number. It cannot be edited or deleted afterwards. */
export function useIssueInvoice() {
  const queryClient = useQueryClient()
  return useInvoiceTransition(
    (id: number) => apiRequest<Invoice>(`/invoices/${id}/issue`, { method: 'POST' }),
    // Issuing takes the company's next number, which the current-tenant data shows.
    () => queryClient.invalidateQueries({ queryKey: tenantKeys.current, exact: true }),
  )
}

/** Cancels an issued invoice; it stays on record but no longer counts as owed. */
export function useVoidInvoice() {
  return useInvoiceTransition(({ id, reason }: { id: number; reason: string }) =>
    apiRequest<Invoice>(`/invoices/${id}/void`, { method: 'POST', ...json({ reason }) }),
  )
}

/** Records money received on an issued invoice; the server refuses more than the balance due. */
export function useRecordPayment() {
  return useInvoiceTransition(({ id, payment }: { id: number; payment: RecordPaymentRequest }) =>
    apiRequest<Invoice>(`/invoices/${id}/payments`, { method: 'POST', ...json(payment) }),
  )
}

/** Removes a payment recorded by mistake. */
export function useDeletePayment() {
  return useInvoiceTransition(({ id, paymentId }: { id: number; paymentId: number }) =>
    apiRequest<Invoice>(`/invoices/${id}/payments/${paymentId}`, { method: 'DELETE' }),
  )
}

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiRequest, query } from './client'
import type { Email, EmailStatus, Paged } from './types'

export const emailKeys = {
  all: ['emails'] as const,
  list: (page: number, status?: EmailStatus) => [...emailKeys.all, page, status] as const,
}

/** The company's emails, newest first. Polled while any is pending, so statuses update without a reload. */
export function useEmails(page: number, status?: EmailStatus) {
  return useQuery({
    queryKey: emailKeys.list(page, status),
    queryFn: () => apiRequest<Paged<Email>>(`/emails${query({ page, status })}`),
    placeholderData: keepPreviousData,
    refetchInterval: (q) => (q.state.data?.items.some((e) => e.status === 'Pending') ? 5000 : false),
  })
}

/** Queues a failed email again. */
export function useRetryEmail() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => apiRequest<void>(`/emails/${id}/retry`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: emailKeys.all }),
  })
}

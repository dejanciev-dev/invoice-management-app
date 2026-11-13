import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { currentUserKey } from './auth'
import { apiRequest, json, query, withTenant } from './client'
import type {
  AuditLog,
  CreatedInvitation,
  CurrentTenant,
  FinancialSummary,
  Invitation,
  InvitationPreview,
  Member,
  Paged,
  Permission,
  TenantPlan,
  TenantRole,
  TenantSettings,
} from './types'

export const tenantKeys = {
  current: ['tenant'] as const,
  members: ['tenant', 'members'] as const,
  invitations: ['tenant', 'invitations'] as const,
  audit: (page: number, action?: string) => ['tenant', 'audit', page, action] as const,
  reports: ['tenant', 'reports'] as const,
  plan: ['tenant', 'plan'] as const,
}

export const tenantLogoUrl = (version?: string | number) =>
  withTenant(`/api/tenant/logo${version === undefined ? '' : `?v=${version}`}`)

export function useCurrentTenant() {
  return useQuery({ queryKey: tenantKeys.current, queryFn: () => apiRequest<CurrentTenant>('/tenant') })
}

export function useUpdateTenantSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (settings: TenantSettings) =>
      apiRequest<CurrentTenant>('/tenant/settings', { method: 'PUT', ...json(settings) }),
    onSuccess: async (tenant) => {
      queryClient.setQueryData(tenantKeys.current, tenant)
      await queryClient.invalidateQueries({ queryKey: currentUserKey })
    },
  })
}

export function useUploadLogo() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (file: File | null) => {
      if (!file) return apiRequest<void>('/tenant/logo', { method: 'DELETE' })
      const body = new FormData()
      body.append('file', file)
      return apiRequest<void>('/tenant/logo', { method: 'POST', body })
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: tenantKeys.current })
      await queryClient.invalidateQueries({ queryKey: currentUserKey })
    },
  })
}

export function useMembers() {
  return useQuery({ queryKey: tenantKeys.members, queryFn: () => apiRequest<Member[]>('/tenant/users') })
}

export function useInvitations() {
  return useQuery({ queryKey: tenantKeys.invitations, queryFn: () => apiRequest<Invitation[]>('/tenant/users/invitations') })
}

function useMemberMutation<TVariables, TResult = void>(mutationFn: (variables: TVariables) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: tenantKeys.members })
      await queryClient.invalidateQueries({ queryKey: tenantKeys.invitations })
    },
  })
}

export const useCreateMember = () =>
  useMemberMutation((request: { email: string; displayName: string | null; role: TenantRole; password: string }) =>
    apiRequest<void>('/tenant/users', { method: 'POST', ...json(request) }),
  )

export const useUpdateMember = () =>
  useMemberMutation(({ userId, ...request }: { userId: string; displayName: string | null; role: TenantRole; permissions: Permission[] | null }) =>
    apiRequest<void>(`/tenant/users/${encodeURIComponent(userId)}`, { method: 'PUT', ...json(request) }),
  )

export const useSetMemberStatus = () =>
  useMemberMutation(({ userId, isActive }: { userId: string; isActive: boolean }) =>
    apiRequest<void>(`/tenant/users/${encodeURIComponent(userId)}/status`, { method: 'PUT', ...json({ isActive }) }),
  )

export const useResetMemberPassword = () =>
  useMemberMutation(({ userId, newPassword }: { userId: string; newPassword: string }) =>
    apiRequest<void>(`/tenant/users/${encodeURIComponent(userId)}/reset-password`, { method: 'POST', ...json({ newPassword }) }),
  )

export const useRemoveMember = () =>
  useMemberMutation((userId: string) => apiRequest<void>(`/tenant/users/${encodeURIComponent(userId)}`, { method: 'DELETE' }))

export const useInviteMember = () =>
  useMemberMutation((request: { email: string; role: TenantRole }) =>
    apiRequest<CreatedInvitation>('/tenant/users/invitations', { method: 'POST', ...json(request) }),
  )

export const useRevokeInvitation = () =>
  useMemberMutation((id: number) => apiRequest<void>(`/tenant/users/invitations/${id}`, { method: 'DELETE' }))

export function useTenantAuditLogs(page: number, action?: string) {
  return useQuery({
    queryKey: tenantKeys.audit(page, action),
    queryFn: () => apiRequest<Paged<AuditLog>>(`/tenant/audit-logs${query({ page, action })}`),
  })
}

export function useFinancialSummary() {
  return useQuery({ queryKey: tenantKeys.reports, queryFn: () => apiRequest<FinancialSummary>('/reports/summary') })
}

export function useInvitationPreview(token: string) {
  return useQuery({
    queryKey: ['invitation', token],
    queryFn: () => apiRequest<InvitationPreview>(`/invitations/${encodeURIComponent(token)}`),
    retry: false,
    enabled: token.length > 0,
  })
}

export function useAcceptInvitation() {
  return useMutation({
    mutationFn: (request: { token: string; password: string | null; displayName: string | null }) =>
      apiRequest<{ tenantId: string }>('/invitations/accept', { method: 'POST', ...json(request) }),
  })
}

/** The company's plan, status and usage; any member may see it. */
export function useTenantPlan() {
  return useQuery({ queryKey: tenantKeys.plan, queryFn: () => apiRequest<TenantPlan>('/tenant/subscription') })
}

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { currentUserKey } from './auth'
import { apiRequest, json, query, setActiveTenantId } from './client'
import type {
  AuditLog,
  BillingCycle,
  Member,
  Paged,
  Plan,
  PlatformDashboard,
  PlatformUser,
  SubscriptionStatus,
  TenantDetails,
  TenantListItem,
  TenantProfile,
  TenantSettings,
  TenantStatus,
} from './types'

export const adminKeys = {
  all: ['admin'] as const,
  dashboard: ['admin', 'dashboard'] as const,
  plans: ['admin', 'plans'] as const,
  tenants: (filters: object) => ['admin', 'tenants', filters] as const,
  tenant: (id: string) => ['admin', 'tenant', id] as const,
  tenantUsers: (id: string) => ['admin', 'tenant', id, 'users'] as const,
  users: (filters: object) => ['admin', 'users', filters] as const,
  audit: (filters: object) => ['admin', 'audit', filters] as const,
}

export const adminTenantLogoUrl = (id: string) => `/api/admin/tenants/${id}/logo`

export const usePlatformDashboard = () =>
  useQuery({ queryKey: adminKeys.dashboard, queryFn: () => apiRequest<PlatformDashboard>('/admin/dashboard') })

export const usePlans = () =>
  useQuery({ queryKey: adminKeys.plans, queryFn: () => apiRequest<Plan[]>('/admin/plans'), staleTime: Infinity })

export function useTenants(filters: { search?: string; status?: TenantStatus; page: number }) {
  return useQuery({
    queryKey: adminKeys.tenants(filters),
    queryFn: () => apiRequest<Paged<TenantListItem>>(`/admin/tenants${query(filters)}`),
    placeholderData: keepPreviousData,
  })
}

export const useTenantDetails = (id: string) =>
  useQuery({ queryKey: adminKeys.tenant(id), queryFn: () => apiRequest<TenantDetails>(`/admin/tenants/${id}`) })

export const useTenantUsers = (id: string) =>
  useQuery({ queryKey: adminKeys.tenantUsers(id), queryFn: () => apiRequest<Member[]>(`/admin/tenants/${id}/users`) })

export function useAuditLogs(filters: { tenantId?: string; userId?: string; action?: string; page: number }) {
  return useQuery({
    queryKey: adminKeys.audit(filters),
    queryFn: () =>
      apiRequest<Paged<AuditLog>>(
        filters.tenantId
          ? `/admin/tenants/${filters.tenantId}/audit-logs${query({ ...filters, tenantId: undefined })}`
          : `/admin/audit-logs${query(filters)}`,
      ),
    placeholderData: keepPreviousData,
  })
}

export function usePlatformUsers(filters: { search?: string; page: number }) {
  return useQuery({
    queryKey: adminKeys.users(filters),
    queryFn: () => apiRequest<Paged<PlatformUser>>(`/admin/users${query(filters)}`),
    placeholderData: keepPreviousData,
  })
}

function useAdminMutation<TVariables, TResult = void>(mutationFn: (variables: TVariables) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.all }),
  })
}

export const useCreateTenant = () =>
  useAdminMutation((request: { profile: TenantProfile; planCode: string; adminEmail: string; adminPassword: string | null }) =>
    apiRequest<{ id: string; invitationToken: string | null }>('/admin/tenants', { method: 'POST', ...json(request) }),
  )

export const useUpdateTenantSettingsAsAdmin = (id: string) =>
  useAdminMutation((settings: TenantSettings) => apiRequest<unknown>(`/admin/tenants/${id}/settings`, { method: 'PUT', ...json(settings) }))

export const useChangeTenantStatus = (id: string) =>
  useAdminMutation((request: { status: TenantStatus; reason: string | null }) =>
    apiRequest<void>(`/admin/tenants/${id}/status`, { method: 'PUT', ...json(request) }),
  )

export interface SubscriptionUpdate {
  planCode: string
  status: SubscriptionStatus
  billingCycle: BillingCycle
  trialEndsAt: string | null
  currentPeriodEndsAt: string | null
  customMaxUsers: number | null
  customMaxInvoicesPerMonth: number | null
}

export const useUpdateSubscription = (id: string) =>
  useAdminMutation((request: SubscriptionUpdate) =>
    apiRequest<void>(`/admin/tenants/${id}/subscription`, { method: 'PUT', ...json(request) }),
  )

export const useDeleteTenant = (id: string) =>
  useAdminMutation((confirmName: string) =>
    apiRequest<void>(`/admin/tenants/${id}${query({ confirmName })}`, { method: 'DELETE' }),
  )

export const useSetUserStatus = () =>
  useAdminMutation(({ userId, isActive }: { userId: string; isActive: boolean }) =>
    apiRequest<void>(`/admin/users/${encodeURIComponent(userId)}/status`, { method: 'PUT', ...json({ isActive }) }),
  )

/** Starts an impersonation. The server swaps the session cookie, so everything cached belongs to someone else now. */
export function useImpersonate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: { userId: string; tenantId: string; reason: string | null }) =>
      apiRequest<void>('/admin/impersonation', { method: 'POST', ...json(request) }),
    onSuccess: async (_, request) => {
      setActiveTenantId(request.tenantId)
      queryClient.clear()
      await queryClient.invalidateQueries({ queryKey: currentUserKey })
    },
  })
}

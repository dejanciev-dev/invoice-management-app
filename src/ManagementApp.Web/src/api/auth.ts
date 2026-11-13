import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiRequest, json, setActiveTenantId } from './client'
import type { Permission, PublicConfig, Session } from './types'

export const currentUserKey = ['auth', 'me'] as const
export const publicConfigKey = ['public-config'] as const

/** Server settings for the sign-in pages; they only change with a deployment. */
export function usePublicConfig() {
  return useQuery({
    queryKey: publicConfigKey,
    queryFn: () => apiRequest<PublicConfig>('/public-config'),
    staleTime: Infinity,
  })
}

export interface Credentials {
  email: string
  password: string
}

/** The signed-in user with their companies, or `null` when the session cookie is missing or expired. */
export function useCurrentUser() {
  return useQuery({
    queryKey: currentUserKey,
    queryFn: async () => {
      try {
        return await apiRequest<Session>('/me')
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null
        throw error
      }
    },
    staleTime: Infinity,
    retry: false,
  })
}

/** The signed-in session; only use below the route guard, where it is known to exist. */
export function useSession(): Session {
  const { data } = useCurrentUser()
  if (!data) throw new Error('useSession must be used inside an authenticated route.')
  return data
}

export function useCan(permission: Permission): boolean {
  const { data } = useCurrentUser()
  return data?.activeTenant?.permissions.includes(permission) ?? false
}

export function useLogin() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ remember, ...credentials }: Credentials & { remember: boolean }) =>
      apiRequest<void>(`/auth/login?${remember ? 'useCookies' : 'useSessionCookies'}=true`, {
        method: 'POST',
        ...json(credentials),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: currentUserKey }),
  })
}

export function useSignup() {
  return useMutation({
    mutationFn: (request: Credentials & { companyName: string; displayName?: string }) =>
      apiRequest<void>('/auth/signup', { method: 'POST', ...json(request) }),
  })
}

/** Emails a reset link if the account exists; the server answers the same either way. */
export function useRequestPasswordReset() {
  return useMutation({
    mutationFn: (request: { email: string }) =>
      apiRequest<void>('/auth/password/forgot', { method: 'POST', ...json(request) }),
  })
}

export function useResetPassword() {
  return useMutation({
    mutationFn: (request: { email: string; code: string; newPassword: string }) =>
      apiRequest<void>('/auth/password/reset', { method: 'POST', ...json(request) }),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => apiRequest<void>('/auth/logout', { method: 'POST' }),
    onSettled: () => {
      setActiveTenantId(null)
      queryClient.clear()
      queryClient.setQueryData(currentUserKey, null)
    },
  })
}

/**
 * Switches company. Every cached query belongs to the previous company, so the whole cache is dropped; nothing from
 * one tenant can be shown while another is active.
 */
export function useSwitchTenant() {
  const queryClient = useQueryClient()

  return (tenantId: string | null) => {
    setActiveTenantId(tenantId)
    queryClient.clear()
    void queryClient.invalidateQueries({ queryKey: currentUserKey })
  }
}

export function useStopImpersonation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => apiRequest<void>('/impersonation/stop', { method: 'POST' }),
    onSettled: () => {
      setActiveTenantId(null)
      queryClient.clear()
    },
  })
}

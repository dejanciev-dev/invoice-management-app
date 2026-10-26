import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiRequest, json } from './client'
import type { UserInfo } from './types'

export const currentUserKey = ['auth', 'me'] as const

export interface Credentials {
  email: string
  password: string
}

/** The signed-in user, or `null` when the session cookie is missing or expired. */
export function useCurrentUser() {
  return useQuery({
    queryKey: currentUserKey,
    queryFn: async () => {
      try {
        return await apiRequest<UserInfo>('/auth/manage/info')
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null
        throw error
      }
    },
    staleTime: Infinity,
    retry: false,
  })
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

export function useRegister() {
  return useMutation({
    mutationFn: (credentials: Credentials) =>
      apiRequest<void>('/auth/register', { method: 'POST', ...json(credentials) }),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => apiRequest<void>('/auth/logout', { method: 'POST' }),
    onSettled: () => {
      queryClient.clear()
      queryClient.setQueryData(currentUserKey, null)
    },
  })
}

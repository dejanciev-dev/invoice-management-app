import { useEffect } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router'
import { useCurrentUser, useSwitchTenant } from '../api/auth'
import { getActiveTenantId } from '../api/client'
import { FullPageSpinner } from '../components/ui'
import { NoTenantPage } from '../pages/NoTenantPage'

interface RedirectState {
  from?: string
}

export function RequireAuth() {
  const { data: user, isPending } = useCurrentUser()
  const location = useLocation()

  if (isPending) return <FullPageSpinner />
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname } satisfies RedirectState} />

  return <Outlet />
}

/** Login and registration pages; once signed in, send the user back to where they were heading. */
export function PublicOnly() {
  const { data: user, isPending } = useCurrentUser()
  const location = useLocation()

  if (isPending) return <FullPageSpinner />
  if (user) return <Navigate to={(location.state as RedirectState | null)?.from ?? '/'} replace />

  return <Outlet />
}

/**
 * The tenant application. If the remembered company is no longer available (removed, suspended), falls back to the
 * server's default choice; platform admins without a company of their own go to the admin area.
 */
export function RequireTenant() {
  const { data: session } = useCurrentUser()
  const switchTenant = useSwitchTenant()
  const staleSelection = !!session && !session.activeTenant && getActiveTenantId() !== null && session.tenants.some((t) => t.isAvailable)

  useEffect(() => {
    if (staleSelection) switchTenant(null)
  }, [staleSelection, switchTenant])

  if (!session || staleSelection) return <FullPageSpinner />
  if (!session.activeTenant) {
    return session.isPlatformAdmin ? <Navigate to="/admin" replace /> : <NoTenantPage session={session} />
  }

  return <Outlet />
}

/** The platform admin area; the API enforces the same rule, this only keeps others from seeing an empty shell. */
export function RequirePlatformAdmin() {
  const { data: session } = useCurrentUser()
  if (!session?.isPlatformAdmin) return <Navigate to="/" replace />
  return <Outlet />
}

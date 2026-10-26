import { Navigate, Outlet, useLocation } from 'react-router'
import { useCurrentUser } from '../api/auth'
import { FullPageSpinner } from '../components/ui'

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

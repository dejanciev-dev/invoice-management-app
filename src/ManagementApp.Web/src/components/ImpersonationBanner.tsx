import { ShieldAlert } from 'lucide-react'
import { useNavigate } from 'react-router'
import { useCurrentUser, useStopImpersonation } from '../api/auth'
import { formatDateTime } from '../lib/labels'
import { Button } from './ui'

/** Shown on every page while a platform admin is using the app as someone else. */
export function ImpersonationBanner() {
  const { data: session } = useCurrentUser()
  const stop = useStopImpersonation()
  const navigate = useNavigate()

  if (!session?.impersonation) return null

  const end = () => stop.mutate(undefined, { onSettled: () => navigate('/admin', { replace: true }) })

  return (
    <div role="alert" className="sticky top-0 z-20 bg-amber-400 text-amber-950 print:hidden">
      <div className="mx-auto flex max-w-6xl flex-col gap-2 px-4 py-2 text-sm sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <p className="flex items-start gap-2">
          <ShieldAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span>
            <strong>Impersonating {session.displayName ?? session.email}</strong>
            {session.activeTenant && <> in {session.activeTenant.name}</>}. Everything you do is recorded as{' '}
            {session.impersonation.impersonatorEmail}. Ends {formatDateTime(session.impersonation.expiresAt)}.
          </span>
        </p>
        <Button variant="secondary" onClick={end} loading={stop.isPending} className="shrink-0 border-amber-700/30">
          Stop impersonating
        </Button>
      </div>
    </div>
  )
}

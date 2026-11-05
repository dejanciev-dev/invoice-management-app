import { Building2 } from 'lucide-react'
import { useLogout } from '../api/auth'
import type { Session } from '../api/types'
import { Logo } from '../components/Layout'
import { Button, Card } from '../components/ui'

const reasons = {
  Inactive: 'has been deactivated',
  Suspended: 'has been suspended',
} as const

/** Signed in, but without a company to work in: removed, deactivated, or the company was switched off. */
export function NoTenantPage({ session }: { session: Session }) {
  const logout = useLogout()
  const blocked = session.tenants.find((t) => t.status !== 'Active')

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center px-4 py-12">
      <div className="mb-8">
        <Logo />
      </div>
      <Card className="w-full max-w-md p-8 text-center">
        <Building2 className="mx-auto size-10 text-muted/60" aria-hidden />
        <h1 className="mt-4 text-xl font-semibold tracking-tight">No company available</h1>
        <p className="mt-2 text-sm text-muted">
          {blocked && blocked.status !== 'Active'
            ? `${blocked.name} ${reasons[blocked.status]}. Contact support if you think this is a mistake.`
            : 'Your account is not a member of any active company. Ask a company admin to invite you.'}
        </p>
        <p className="mt-1 text-xs text-muted">Signed in as {session.email}</p>
        <Button variant="secondary" className="mt-6" onClick={() => logout.mutate()} loading={logout.isPending}>
          Sign out
        </Button>
      </Card>
    </div>
  )
}

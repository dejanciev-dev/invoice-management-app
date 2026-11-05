import { Building2, Shield } from 'lucide-react'
import { Link, NavLink, Outlet } from 'react-router'
import { useCurrentUser } from '../api/auth'
import { UserMenu } from './Layout'
import { navLinkClass } from './styles'

/** The platform admin area, visibly different from the tenant application. */
export function AdminLayout() {
  const { data: session } = useCurrentUser()
  const hasTenant = session?.tenants.some((t) => t.isAvailable)

  return (
    <div className="min-h-dvh">
      <header className="sticky top-0 z-10 border-b border-line bg-ink text-canvas print:hidden">
        <div className="mx-auto flex min-h-16 max-w-6xl flex-wrap items-center justify-between gap-x-4 gap-y-2 px-4 py-2 sm:px-6">
          <Link to="/admin" className="flex items-center gap-2 font-semibold tracking-tight">
            <span className="grid size-8 place-items-center rounded-lg bg-amber-400 text-amber-950">
              <Shield className="size-4.5" aria-hidden />
            </span>
            Platform admin
          </Link>
          <div className="flex items-center gap-3 [&_*]:text-canvas/80">
            {hasTenant && (
              <Link to="/" className="inline-flex items-center gap-1 text-sm hover:underline">
                <Building2 className="size-4" aria-hidden />
                My company
              </Link>
            )}
            <UserMenu />
          </div>
          <nav aria-label="Platform" className="-mx-1 flex w-full gap-5 overflow-x-auto px-1 pb-1 [&_a]:text-canvas/70 [&_a.active]:text-canvas">
            <NavLink to="/admin" end className={navLinkClass}>
              Dashboard
            </NavLink>
            <NavLink to="/admin/tenants" className={navLinkClass}>
              Tenants
            </NavLink>
            <NavLink to="/admin/users" className={navLinkClass}>
              Users
            </NavLink>
            <NavLink to="/admin/activity" className={navLinkClass}>
              Activity
            </NavLink>
          </nav>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
        <Outlet />
      </main>
    </div>
  )
}

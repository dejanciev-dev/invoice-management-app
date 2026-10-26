import { FileText, LogOut } from 'lucide-react'
import { Link, NavLink, Outlet } from 'react-router'
import { useCurrentUser, useLogout } from '../api/auth'
import { Button } from './ui'

export function Logo() {
  return (
    <Link to="/" className="flex items-center gap-2 font-semibold tracking-tight">
      <span className="grid size-8 place-items-center rounded-lg bg-brand text-on-brand">
        <FileText className="size-4.5" aria-hidden />
      </span>
      Invoice Manager
    </Link>
  )
}

export function Layout() {
  const { data: user } = useCurrentUser()
  const logout = useLogout()

  return (
    <div className="min-h-dvh">
      <header className="sticky top-0 z-10 border-b border-line bg-surface/85 backdrop-blur print:hidden">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
          <div className="flex items-center gap-8">
            <Logo />
            <nav className="hidden sm:block">
              <NavLink
                to="/"
                end
                className={({ isActive }) =>
                  `text-sm font-medium ${isActive ? 'text-ink' : 'text-muted hover:text-ink'}`
                }
              >
                Invoices
              </NavLink>
            </nav>
          </div>
          <div className="flex items-center gap-2">
            <span className="hidden text-sm text-muted md:inline">{user?.email}</span>
            <Button variant="ghost" onClick={() => logout.mutate()} loading={logout.isPending}>
              <LogOut className="size-4" aria-hidden />
              Sign out
            </Button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 print:max-w-none print:p-0">
        <Outlet />
      </main>
    </div>
  )
}

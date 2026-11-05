import { Building2, FileText, LogOut, Shield } from 'lucide-react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router'
import { useCan, useCurrentUser, useLogout, useSwitchTenant } from '../api/auth'
import { tenantLogoUrl } from '../api/tenant'
import type { Permission } from '../api/types'
import { roleLabels } from '../lib/labels'
import { ImpersonationBanner } from './ImpersonationBanner'
import { navLinkClass } from './styles'
import { Button } from './ui'

export function Logo({ to = '/', label = 'Invoice Manager' }: { to?: string; label?: string }) {
  return (
    <Link to={to} className="flex items-center gap-2 font-semibold tracking-tight">
      <span className="grid size-8 place-items-center rounded-lg bg-brand text-on-brand">
        <FileText className="size-4.5" aria-hidden />
      </span>
      {label}
    </Link>
  )
}

const tenantNav: { to: string; label: string; permission: Permission; end?: boolean }[] = [
  { to: '/', label: 'Invoices', permission: 'InvoicesView', end: true },
  { to: '/reports', label: 'Reports', permission: 'ReportsView' },
  { to: '/users', label: 'Users', permission: 'UsersManage' },
  { to: '/activity', label: 'Activity', permission: 'AuditLogView' },
  { to: '/settings', label: 'Settings', permission: 'SettingsManage' },
]


export function Layout() {
  return (
    <div className="min-h-dvh">
      <ImpersonationBanner />
      <header className="sticky top-0 z-10 border-b border-line bg-surface/85 backdrop-blur print:hidden">
        <div className="mx-auto flex min-h-16 max-w-6xl flex-wrap items-center justify-between gap-x-4 gap-y-2 px-4 py-2 sm:px-6">
          <div className="flex min-w-0 items-center gap-6">
            <Logo />
            <TenantSwitcher />
          </div>
          <UserMenu />
          <TenantNav />
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 print:max-w-none print:p-0">
        <Outlet />
      </main>
    </div>
  )
}

function TenantNav() {
  const can = {
    InvoicesView: useCan('InvoicesView'),
    ReportsView: useCan('ReportsView'),
    UsersManage: useCan('UsersManage'),
    AuditLogView: useCan('AuditLogView'),
    SettingsManage: useCan('SettingsManage'),
  } as Record<Permission, boolean>
  const items = tenantNav.filter((item) => can[item.permission])

  return (
    <nav aria-label="Company" className="-mx-1 flex w-full gap-5 overflow-x-auto px-1 pb-1">
      {items.map((item) => (
        <NavLink key={item.to} to={item.to} end={item.end} className={navLinkClass}>
          {item.label}
        </NavLink>
      ))}
    </nav>
  )
}

/** Always shows which company is active, and lets members of several companies switch. */
function TenantSwitcher() {
  const { data: session } = useCurrentUser()
  const switchTenant = useSwitchTenant()
  const navigate = useNavigate()
  const active = session?.activeTenant
  if (!session || !active) return null

  const available = session.tenants.filter((t) => t.isAvailable)
  const logo = active.hasLogo ? (
    <img src={tenantLogoUrl(active.id)} alt="" className="size-7 rounded object-contain" />
  ) : (
    <Building2 className="size-5 text-muted" aria-hidden />
  )

  return (
    <div className="flex min-w-0 items-center gap-2" style={active.brandColor ? { color: active.brandColor } : undefined}>
      {logo}
      {available.length > 1 && !session.impersonation ? (
        <select
          aria-label="Active company"
          value={active.id}
          onChange={(e) => {
            switchTenant(e.target.value)
            navigate('/')
          }}
          className="max-w-48 truncate rounded-md border border-line bg-surface px-2 py-1 text-sm font-medium text-ink"
        >
          {available.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
      ) : (
        <span className="truncate text-sm font-medium text-ink" title="Active company">
          {active.name}
        </span>
      )}
      <span className="hidden text-xs text-muted md:inline">{roleLabels[active.role]}</span>
    </div>
  )
}

export function UserMenu() {
  const { data: user } = useCurrentUser()
  const logout = useLogout()

  return (
    <div className="flex items-center gap-2">
      {user?.isPlatformAdmin && (
        <Link to="/admin" className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm font-medium text-muted hover:text-ink">
          <Shield className="size-4" aria-hidden />
          Admin
        </Link>
      )}
      <span className="hidden text-sm text-muted md:inline">{user?.email}</span>
      {!user?.impersonation && (
        <Button variant="ghost" onClick={() => logout.mutate()} loading={logout.isPending}>
          <LogOut className="size-4" aria-hidden />
          Sign out
        </Button>
      )}
    </div>
  )
}

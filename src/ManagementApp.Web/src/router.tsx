import { createBrowserRouter } from 'react-router'
import { PublicOnly, RequireAuth, RequirePlatformAdmin, RequireTenant } from './auth/RouteGuards'
import { Layout } from './components/Layout'
import { Spinner } from './components/ui'
import { AcceptInvitePage } from './pages/AcceptInvitePage'
import { InvoiceDetailsPage } from './pages/InvoiceDetailsPage'
import { InvoicesPage } from './pages/InvoicesPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { RegisterPage } from './pages/RegisterPage'

// The editor pulls in the form and validation libraries, so it is only loaded when needed.
const lazyEditor = async () => ({ Component: (await import('./pages/InvoiceEditorPage')).InvoiceEditorPage })

// Company administration and the platform admin area are only used by some users, so they are loaded on demand.
const lazy = <T,>(load: () => Promise<T>, pick: (module: T) => React.ComponentType) => async () => ({ Component: pick(await load()) })

export const router = createBrowserRouter([
  {
    element: <PublicOnly />,
    children: [
      { path: '/login', element: <LoginPage /> },
      { path: '/register', element: <RegisterPage /> },
    ],
  },
  // Works signed in or out: an invitation can create an account or be accepted by an existing one.
  { path: '/accept-invite', element: <AcceptInvitePage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        path: '/admin',
        element: <RequirePlatformAdmin />,
        HydrateFallback: Spinner,
        children: [
          {
            lazy: lazy(() => import('./components/AdminLayout'), (m) => m.AdminLayout),
            children: [
              { index: true, lazy: lazy(() => import('./pages/admin/AdminDashboardPage'), (m) => m.AdminDashboardPage) },
              { path: 'tenants', lazy: lazy(() => import('./pages/admin/AdminTenantsPage'), (m) => m.AdminTenantsPage) },
              { path: 'tenants/:id', lazy: lazy(() => import('./pages/admin/AdminTenantPage'), (m) => m.AdminTenantPage) },
              { path: 'users', lazy: lazy(() => import('./pages/admin/AdminUsersPage'), (m) => m.AdminUsersPage) },
              { path: 'activity', lazy: lazy(() => import('./pages/admin/AdminActivityPage'), (m) => m.AdminActivityPage) },
              { path: '*', element: <NotFoundPage /> },
            ],
          },
        ],
      },
      {
        element: <RequireTenant />,
        children: [
          {
            element: <Layout />,
            HydrateFallback: Spinner,
            children: [
              { index: true, element: <InvoicesPage /> },
              { path: 'invoices/new', lazy: lazyEditor },
              { path: 'invoices/:id', element: <InvoiceDetailsPage /> },
              { path: 'invoices/:id/edit', lazy: lazyEditor },
              { path: 'reports', lazy: lazy(() => import('./pages/ReportsPage'), (m) => m.ReportsPage) },
              { path: 'users', lazy: lazy(() => import('./pages/UsersPage'), (m) => m.UsersPage) },
              { path: 'activity', lazy: lazy(() => import('./pages/ActivityPage'), (m) => m.ActivityPage) },
              { path: 'settings', lazy: lazy(() => import('./pages/SettingsPage'), (m) => m.SettingsPage) },
              { path: '*', element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
])

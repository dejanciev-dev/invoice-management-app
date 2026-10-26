import { createBrowserRouter } from 'react-router'
import { PublicOnly, RequireAuth } from './auth/RouteGuards'
import { Layout } from './components/Layout'
import { Spinner } from './components/ui'
import { InvoiceDetailsPage } from './pages/InvoiceDetailsPage'
import { InvoicesPage } from './pages/InvoicesPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { RegisterPage } from './pages/RegisterPage'

// The editor pulls in the form and validation libraries, so it is only loaded when needed.
const lazyEditor = async () => ({ Component: (await import('./pages/InvoiceEditorPage')).InvoiceEditorPage })

export const router = createBrowserRouter([
  {
    element: <PublicOnly />,
    children: [
      { path: '/login', element: <LoginPage /> },
      { path: '/register', element: <RegisterPage /> },
    ],
  },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <Layout />,
        HydrateFallback: Spinner,
        children: [
          { index: true, element: <InvoicesPage /> },
          { path: 'invoices/new', lazy: lazyEditor },
          { path: 'invoices/:id', element: <InvoiceDetailsPage /> },
          { path: 'invoices/:id/edit', lazy: lazyEditor },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
])

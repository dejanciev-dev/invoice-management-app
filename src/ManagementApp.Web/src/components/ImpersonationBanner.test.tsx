import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { currentUserKey } from '../api/auth'
import type { Session } from '../api/types'
import { ImpersonationBanner } from './ImpersonationBanner'

const session: Session = {
  userId: 'u1',
  email: 'jane@acme.example',
  displayName: 'Jane',
  isPlatformAdmin: false,
  impersonation: null,
  tenants: [],
  activeTenant: {
    id: 't1',
    name: 'Acme Ltd',
    role: 'TenantAdmin',
    permissions: ['InvoicesView'],
    hasLogo: false,
    brandColor: null,
    defaultCurrency: 'EUR',
    subscription: {
      planCode: 'trial',
      planName: 'Trial',
      status: 'Trialing',
      effectiveStatus: 'Trialing',
      billingCycle: 'Monthly',
      trialEndsAt: null,
      currentPeriodEndsAt: null,
      maxUsers: 5,
      maxInvoicesPerMonth: 100,
      features: [],
      canChangeData: true,
      readOnlyReason: null,
      graceEndsAt: null,
      accessEndsAt: null,
    },
  },
}

function renderBanner(value: Session) {
  const queryClient = new QueryClient()
  queryClient.setQueryData(currentUserKey, value)
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <ImpersonationBanner />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('ImpersonationBanner', () => {
  it('is hidden in a normal session', () => {
    renderBanner(session)

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('names the impersonated user, the company and the real admin, with a way out', () => {
    renderBanner({
      ...session,
      impersonation: {
        sessionId: 's1',
        impersonatorEmail: 'support@platform.example',
        startedAt: '2026-03-15T09:00:00Z',
        expiresAt: '2026-03-15T10:00:00Z',
      },
    })

    const banner = screen.getByRole('alert')
    expect(banner).toHaveTextContent('Impersonating Jane in Acme Ltd')
    expect(banner).toHaveTextContent('support@platform.example')
    expect(screen.getByRole('button', { name: 'Stop impersonating' })).toBeInTheDocument()
  })
})

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { currentUserKey } from '../api/auth'
import type { Session, Subscription } from '../api/types'
import { subscriptionNotice } from '../lib/subscription'
import { SubscriptionBanner } from './SubscriptionBanner'

const now = new Date('2026-03-15T09:00:00Z')

const active: Subscription = {
  planCode: 'pro',
  planName: 'Professional',
  status: 'Active',
  effectiveStatus: 'Active',
  billingCycle: 'Monthly',
  trialEndsAt: null,
  currentPeriodEndsAt: null,
  maxUsers: 10,
  maxInvoicesPerMonth: 500,
  features: [],
  canChangeData: true,
  readOnlyReason: null,
  graceEndsAt: null,
  accessEndsAt: null,
}

const trialEnding = (days: number): Subscription => ({
  ...active,
  status: 'Trialing',
  effectiveStatus: 'Trialing',
  trialEndsAt: new Date(now.getTime() + days * 86_400_000).toISOString(),
})

describe('subscriptionNotice', () => {
  it('says nothing for a healthy subscription or a trial with time left', () => {
    expect(subscriptionNotice(active, now)).toBeNull()
    expect(subscriptionNotice(trialEnding(4), now)).toBeNull()
  })

  it('reminds of a trial ending within three days, and what comes after', () => {
    expect(subscriptionNotice(trialEnding(3), now)).toMatchObject({ tone: 'info', message: expect.stringMatching(/trial ends in 3 days.*Free plan/) })
    expect(subscriptionNotice(trialEnding(0.5), now)?.message).toMatch(/trial ends tomorrow/)
  })

  it('warns of an overdue payment with the end of the grace period', () => {
    const notice = subscriptionNotice({ ...active, effectiveStatus: 'PastDue', graceEndsAt: '2026-03-29T09:00:00Z' }, now)

    expect(notice?.tone).toBe('warning')
    expect(notice?.message).toMatch(/payment is overdue.*full access until/)
  })

  it('warns that a cancelled subscription runs out', () => {
    const notice = subscriptionNotice({ ...active, effectiveStatus: 'Cancelled', accessEndsAt: '2026-04-01T00:00:00Z' }, now)

    expect(notice?.message).toMatch(/cancelled.*read-only/)
  })

  it('explains read-only, and that data can still be read and exported', () => {
    const notice = subscriptionNotice(
      { ...active, effectiveStatus: 'Expired', canChangeData: false, readOnlyReason: 'Your payment is overdue and the grace period has ended.' },
      now,
    )

    expect(notice).toEqual({
      tone: 'danger',
      message: 'Your payment is overdue and the grace period has ended. You can still view and export everything.',
    })
  })
})

function renderBanner(subscription: Subscription) {
  const queryClient = new QueryClient()
  const session: Session = {
    userId: 'u1',
    email: 'jane@acme.example',
    displayName: null,
    isPlatformAdmin: false,
    impersonation: null,
    tenants: [],
    activeTenant: { id: 't1', name: 'Acme', role: 'TenantAdmin', permissions: [], hasLogo: false, brandColor: null, defaultCurrency: 'EUR', subscription },
  }
  queryClient.setQueryData(currentUserKey, session)
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <SubscriptionBanner now={now} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('SubscriptionBanner', () => {
  it('is hidden when there is nothing to say', () => {
    renderBanner(active)

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('shows a read-only company the reason and a link to its plan', () => {
    renderBanner({ ...active, effectiveStatus: 'Expired', canChangeData: false, readOnlyReason: 'Your subscription has ended.' })

    expect(screen.getByRole('alert')).toHaveTextContent('Your subscription has ended.')
    expect(screen.getByRole('link', { name: 'See your plan' })).toHaveAttribute('href', '/plan')
  })

  it('announces a trial reminder politely', () => {
    renderBanner(trialEnding(2))

    expect(screen.getByRole('status')).toHaveTextContent('Your trial ends in 2 days')
  })
})

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import type { Plan, TenantPlan } from '../api/types'
import { PlanPage } from './PlanPage'

const plan = (code: string, name: string, monthlyPrice: number, maxUsers: number, maxInvoicesPerMonth: number): Plan => ({
  code,
  name,
  maxUsers,
  maxInvoicesPerMonth,
  features: code === 'starter' ? ['CsvExport'] : [],
  monthlyPrice,
  yearlyPrice: monthlyPrice * 10,
  currency: 'EUR',
  isDefault: false,
})

const data: TenantPlan = {
  subscription: {
    planCode: 'starter',
    planName: 'Starter',
    status: 'Active',
    effectiveStatus: 'Active',
    billingCycle: 'Monthly',
    trialEndsAt: null,
    currentPeriodEndsAt: '2026-04-15T00:00:00Z',
    maxUsers: 3,
    maxInvoicesPerMonth: 50,
    features: ['CsvExport'],
    canChangeData: true,
    readOnlyReason: null,
    graceEndsAt: null,
    accessEndsAt: null,
  },
  invoices: { limit: 50, used: 12 },
  invoicesResetAt: '2026-04-01T00:00:00Z',
  seats: { limit: 3, used: 3 },
  plans: [plan('free', 'Free', 0, 1, 10), plan('starter', 'Starter', 9, 3, 50), plan('professional', 'Professional', 19, 10, 500)],
}

beforeEach(() =>
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify(data), { headers: { 'Content-Type': 'application/json' } }))),
)
afterEach(() => vi.unstubAllGlobals())

function renderPage() {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter>
        <PlanPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('PlanPage', () => {
  it('shows this month’s usage against the limits', async () => {
    renderPage()

    const invoices = await screen.findByRole('progressbar', { name: 'Invoices issued this month' })
    expect(invoices).toHaveAttribute('aria-valuenow', '12')
    expect(invoices).toHaveAttribute('aria-valuemax', '50')
    expect(screen.getByText('12 of 50')).toBeInTheDocument()
    expect(screen.getByText('3 of 3')).toBeInTheDocument() // all seats taken
    expect(screen.getByText(/Paid until/)).toBeInTheDocument()
  })

  it('compares the plans and marks the current one', async () => {
    renderPage()

    const current = (await screen.findAllByRole('row')).find((row) => row.getAttribute('aria-current') === 'true')!
    expect(within(current).getByText('Current')).toBeInTheDocument()
    expect(current).toHaveTextContent('Starter')
    const professional = screen.getAllByRole('row').find((row) => row.textContent?.includes('Professional'))!
    expect(professional).toHaveTextContent(/19.*\/month/)
    expect(screen.getByText(/To change your plan, contact support/)).toBeInTheDocument()
  })
})

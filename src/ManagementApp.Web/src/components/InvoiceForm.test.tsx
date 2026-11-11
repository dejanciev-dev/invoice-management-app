import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApiError } from '../api/client'
import type { Customer, Paged } from '../api/types'
import { newInvoiceValues } from '../lib/invoiceForm'
import { InvoiceForm } from './InvoiceForm'

function renderForm(props: Partial<Parameters<typeof InvoiceForm>[0]> = {}) {
  const onSubmit = vi.fn()
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const view = render(
    <QueryClientProvider client={queryClient}>
      <InvoiceForm
        defaultValues={newInvoiceValues('2026-03-01')}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
        submitLabel="Create invoice"
        {...props}
      />
    </QueryClientProvider>,
  )
  return { ...view, onSubmit, user: userEvent.setup() }
}

const globex: Customer = {
  id: 7,
  name: 'Globex',
  email: 'ap@globex.example',
  phone: null,
  address: '1 Globex Way\nSpringfield',
  taxNumber: 'US123',
  currency: 'EUR',
  paymentTermsDays: 14,
  notes: null,
  created: '2026-01-01T00:00:00Z',
}

/** Answers the customer search with the given customers. */
function stubCustomerSearch(customers: Customer[]) {
  const page: Paged<Customer> = { items: customers, total: customers.length, page: 1, pageSize: 8 }
  const fetchMock = vi.fn<typeof fetch>(async () => new Response(JSON.stringify(page), { headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => vi.unstubAllGlobals())

const totals = () => within(screen.getByLabelText('Invoice totals'))

async function fill(user: ReturnType<typeof userEvent.setup>, label: string, value: string) {
  const input = screen.getByLabelText(label)
  await user.clear(input)
  await user.type(input, value)
}

describe('InvoiceForm', () => {
  it('updates line amounts and totals as the user types', async () => {
    const { user } = renderForm()

    await fill(user, 'Item 1 quantity', '2')
    await fill(user, 'Item 1 unit price', '150.50')
    await fill(user, 'Tax', '20')

    expect(screen.getByLabelText('Item 1 amount')).toHaveTextContent('$301.00')
    expect(totals().getByText('$60.20')).toBeInTheDocument()
    expect(totals().getAllByText('$361.20')).toHaveLength(2) // total and balance due
  })

  it('adds and removes items', async () => {
    const { user } = renderForm()

    await user.click(screen.getByRole('button', { name: 'Add item' }))
    expect(screen.getByLabelText('Item 2 description')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Remove item 2' }))
    expect(screen.queryByLabelText('Item 2 description')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Remove item 1' })).toBeDisabled()
  })

  it('shows validation errors instead of submitting an incomplete invoice', async () => {
    const { user, onSubmit } = renderForm()

    await user.click(screen.getByRole('button', { name: 'Create invoice' }))

    expect(await screen.findByText('Invoice number is required')).toBeInTheDocument()
    expect(screen.getByText('Your business details are required')).toBeInTheDocument()
    expect(screen.getByText('Description is required')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('submits the entered values', async () => {
    const { user, onSubmit } = renderForm()

    await user.type(screen.getByLabelText('Invoice number'), 'INV-042')
    await user.type(screen.getByLabelText('From'), 'Acme Ltd')
    await user.type(screen.getByLabelText('Bill to'), 'Globex')
    await user.type(screen.getByLabelText('Item 1 description'), 'Consulting')
    await fill(user, 'Item 1 unit price', '99')
    await user.click(screen.getByRole('button', { name: 'Create invoice' }))

    expect(onSubmit).toHaveBeenCalledOnce()
    expect(onSubmit.mock.calls[0]?.[0]).toMatchObject({
      invoiceNumber: 'INV-042',
      customer: null,
      items: [{ description: 'Consulting', quantity: 1, unitPrice: 99 }],
    })
  })

  it("fills in a chosen customer's details, currency and payment terms", async () => {
    const fetchMock = stubCustomerSearch([globex])
    const { user, onSubmit } = renderForm({ companyDefaults: { currency: 'USD', paymentTermsDays: 30, taxLabel: 'VAT' } })

    await user.type(screen.getByRole('combobox', { name: 'Customer' }), 'glo')
    await user.click(await screen.findByRole('option', { name: /Globex/ }))

    expect(screen.getByLabelText('Bill to')).toHaveValue('Globex\n1 Globex Way\nSpringfield\nVAT no. US123')
    expect(screen.getByLabelText('Currency')).toHaveValue('EUR')
    expect(screen.getByLabelText('Payment terms')).toHaveValue('Net 14')
    expect(screen.getByLabelText('Due date')).toHaveValue('2026-03-15')
    expect(String(fetchMock.mock.lastCall?.[0])).toContain('search=glo')

    await user.type(screen.getByLabelText('Invoice number'), 'INV-042')
    await user.type(screen.getByLabelText('From'), 'Acme Ltd')
    await user.type(screen.getByLabelText('Item 1 description'), 'Consulting')
    await user.click(screen.getByRole('button', { name: 'Create invoice' }))
    expect(onSubmit.mock.calls[0]?.[0]).toMatchObject({ customer: { id: 7, name: 'Globex' } })
  })

  it("uses the company's defaults for a customer without their own, and can unlink the customer", async () => {
    stubCustomerSearch([{ ...globex, currency: null, paymentTermsDays: null }])
    const { user } = renderForm({ companyDefaults: { currency: 'GBP', paymentTermsDays: 0, taxLabel: 'VAT' } })

    await user.click(screen.getByRole('combobox', { name: 'Customer' }))
    await user.keyboard('{ArrowDown}{Enter}')

    expect(screen.getByLabelText('Currency')).toHaveValue('GBP')
    expect(screen.getByLabelText('Payment terms')).toHaveValue('Due on receipt')
    expect(screen.getByLabelText('Due date')).toHaveValue('2026-03-01')

    await user.click(screen.getByRole('button', { name: 'Remove customer' }))
    expect(screen.getByRole('combobox', { name: 'Customer' })).toBeInTheDocument()
    expect(screen.getByLabelText('Bill to')).toHaveValue('Globex\n1 Globex Way\nSpringfield\nVAT no. US123') // the text stays
  })

  it('shows server validation errors next to the matching fields', () => {
    const serverError = new ApiError(400, {
      status: 400,
      errors: {
        invoiceNumber: ["Invoice number 'INV-001' is already in use."],
        'items[0].description': ['Description is too long.'],
      },
    })

    renderForm({ serverError })

    expect(screen.getByText("Invoice number 'INV-001' is already in use.")).toBeInTheDocument()
    expect(screen.getByText('Description is too long.')).toBeInTheDocument()
  })
})

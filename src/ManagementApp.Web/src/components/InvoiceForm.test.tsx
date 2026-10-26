import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApiError } from '../api/client'
import { newInvoiceValues } from '../lib/invoiceForm'
import { InvoiceForm } from './InvoiceForm'

function renderForm(props: Partial<Parameters<typeof InvoiceForm>[0]> = {}) {
  const onSubmit = vi.fn()
  const view = render(
    <InvoiceForm
      defaultValues={newInvoiceValues('2026-03-01')}
      onSubmit={onSubmit}
      onCancel={vi.fn()}
      submitLabel="Create invoice"
      {...props}
    />,
  )
  return { ...view, onSubmit, user: userEvent.setup() }
}

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
      items: [{ description: 'Consulting', quantity: 1, unitPrice: 99 }],
    })
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

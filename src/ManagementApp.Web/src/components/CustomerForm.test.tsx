import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApiError } from '../api/client'
import { emptyCustomer } from '../lib/customerForm'
import { CustomerForm } from './CustomerForm'

function renderForm(props: Partial<Parameters<typeof CustomerForm>[0]> = {}) {
  const onSubmit = vi.fn()
  const view = render(
    <CustomerForm defaultValues={emptyCustomer} onSubmit={onSubmit} onCancel={vi.fn()} submitLabel="Create customer" {...props} />,
  )
  return { ...view, onSubmit, user: userEvent.setup() }
}

describe('CustomerForm', () => {
  it('sends trimmed values, and blank optional fields as null', async () => {
    const { user, onSubmit } = renderForm({ companyCurrency: 'GBP' })

    await user.type(screen.getByLabelText('Name'), '  Globex  ')
    await user.type(screen.getByLabelText('Billing address'), '1 High Street{enter}Springfield')
    await user.type(screen.getByLabelText('Phone'), '   ')
    await user.type(screen.getByLabelText('Payment terms (days)'), '14')
    expect(screen.getByRole('option', { name: 'Company default (GBP)' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Create customer' }))

    expect(onSubmit).toHaveBeenCalledWith({
      name: 'Globex',
      email: null,
      phone: null,
      address: '1 High Street\nSpringfield',
      taxNumber: null,
      currency: null,
      paymentTermsDays: 14,
      notes: null,
    })
  })

  it('does not submit without a name', async () => {
    const { user, onSubmit } = renderForm()

    await user.click(screen.getByRole('button', { name: 'Create customer' }))

    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('shows server validation messages next to their fields', () => {
    const error = new ApiError(400, { title: 'Validation failed', errors: { email: ["'Email' is not a valid email address."] } })

    renderForm({ defaultValues: { ...emptyCustomer, name: 'Globex', email: 'nope' }, serverError: error })

    expect(screen.getByRole('alert')).toHaveTextContent("'Email' is not a valid email address.")
  })
})

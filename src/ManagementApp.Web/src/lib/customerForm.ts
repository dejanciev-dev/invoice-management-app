import type { Customer, SaveCustomerRequest } from '../api/types'

export const emptyCustomer: SaveCustomerRequest = {
  name: '',
  email: null,
  phone: null,
  address: null,
  taxNumber: null,
  currency: null,
  paymentTermsDays: null,
  notes: null,
}

/** The editable fields of a saved customer. */
export const customerToRequest = (customer: Customer): SaveCustomerRequest => ({
  name: customer.name,
  email: customer.email,
  phone: customer.phone,
  address: customer.address,
  taxNumber: customer.taxNumber,
  currency: customer.currency,
  paymentTermsDays: customer.paymentTermsDays,
  notes: customer.notes,
})

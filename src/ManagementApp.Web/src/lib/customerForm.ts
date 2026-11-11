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

/**
 * The "Bill to" text for an invoice: name, billing address and tax number, in the same layout as the company's own
 * details (e.g. "VAT no. GB123").
 */
export function customerBillTo(customer: Pick<Customer, 'name' | 'address' | 'taxNumber'>, taxLabel = 'Tax'): string {
  const lines = [customer.name, customer.address, customer.taxNumber ? `${taxLabel} no. ${customer.taxNumber}` : null]
  return lines
    .filter((line): line is string => Boolean(line?.trim()))
    .map((line) => line.trim())
    .join('\n')
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

import { customerBillTo } from './customerForm'
import { invoiceFormSchema, newInvoiceValues, toFieldPath, valuesToRequest } from './invoiceForm'

describe('newInvoiceValues', () => {
  it('defaults the due date to 30 days after the issue date', () => {
    const values = newInvoiceValues('2026-01-15')

    expect(values.issueDate).toBe('2026-01-15')
    expect(values.dueDate).toBe('2026-02-14')
    expect(values.items).toHaveLength(1)
  })

  it("starts from the company's defaults", () => {
    const values = newInvoiceValues('2026-01-15', {
      currency: 'GBP',
      paymentTermsDays: 14,
      taxRate: 20,
      from: 'Northwind Studio\nLondon',
      notes: 'Thanks!',
    })

    expect(values).toMatchObject({
      currency: 'GBP',
      dueDate: '2026-01-29',
      paymentTerms: 'Net 14',
      tax: 20,
      taxType: 'Percentage',
      from: 'Northwind Studio\nLondon',
      notes: 'Thanks!',
    })
  })
})

describe('invoiceFormSchema', () => {
  const valid = {
    ...newInvoiceValues('2026-01-15'),
    from: 'Acme Ltd',
    to: 'Globex',
    items: [{ description: 'Design', quantity: 1, unitPrice: 100 }],
  }

  it('accepts a complete invoice', () => {
    expect(invoiceFormSchema.safeParse(valid).success).toBe(true)
  })

  it('rejects a due date before the issue date', () => {
    const result = invoiceFormSchema.safeParse({ ...valid, dueDate: '2026-01-01' })

    expect(result.error?.issues[0]?.path).toEqual(['dueDate'])
  })

  it('rejects percentages above 100 but allows large flat amounts', () => {
    expect(invoiceFormSchema.safeParse({ ...valid, discount: 150, discountType: 'Percentage' }).success).toBe(false)
    expect(invoiceFormSchema.safeParse({ ...valid, discount: 150, discountType: 'Flat' }).success).toBe(true)
  })

  it('requires at least one item', () => {
    expect(invoiceFormSchema.safeParse({ ...valid, items: [] }).success).toBe(false)
  })
})

describe('valuesToRequest', () => {
  it('sends empty optional fields as null', () => {
    const request = valuesToRequest({ ...newInvoiceValues('2026-01-15'), dueDate: '', paymentTerms: '  ', notes: '' })

    expect(request.dueDate).toBeNull()
    expect(request.paymentTerms).toBeNull()
    expect(request.notes).toBeNull()
  })

  it('sends the linked customer as its id', () => {
    const values = newInvoiceValues('2026-01-15')

    expect(valuesToRequest({ ...values, customer: { id: 7, name: 'Globex' } })).toMatchObject({ customerId: 7 })
    expect(valuesToRequest(values).customerId).toBeNull()
    expect(valuesToRequest(values)).not.toHaveProperty('customer')
  })
})

describe('toFieldPath', () => {
  it('converts API error paths into form field paths', () => {
    expect(toFieldPath('items[2].unitPrice')).toBe('items.2.unitPrice')
    expect(toFieldPath('currency')).toBe('currency')
    expect(toFieldPath('customerId')).toBe('customer')
  })
})

describe('customerBillTo', () => {
  it('lists the name, address and labelled tax number, skipping what is missing', () => {
    expect(customerBillTo({ name: 'Globex', address: '1 Globex Way\nSpringfield', taxNumber: 'US123' }, 'VAT')).toBe(
      'Globex\n1 Globex Way\nSpringfield\nVAT no. US123',
    )
    expect(customerBillTo({ name: ' Solo Trader ', address: null, taxNumber: null })).toBe('Solo Trader')
  })
})

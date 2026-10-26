import { calculateTotals, roundMoney } from './money'

describe('roundMoney', () => {
  it.each([
    [49.995, 50],
    [1.005, 1.01],
    [2.675, 2.68],
    [-1.005, -1.01],
    [10, 10],
    [0.1 + 0.2, 0.3],
  ])('rounds %d half away from zero to %d', (value, expected) => {
    expect(roundMoney(value)).toBe(expected)
  })

  it('treats non-finite values as zero', () => {
    expect(roundMoney(Number.NaN)).toBe(0)
    expect(roundMoney(Number.POSITIVE_INFINITY)).toBe(0)
  })
})

describe('calculateTotals', () => {
  const base = { discount: 0, discountType: 'Flat', tax: 0, taxType: 'Flat', amountPaid: 0 } as const

  it('matches the API for percentage discount and tax', () => {
    const totals = calculateTotals({
      ...base,
      items: [{ quantity: 2, unitPrice: 150.5 }],
      discount: 10,
      discountType: 'Percentage',
      tax: 20,
      taxType: 'Percentage',
      amountPaid: 50,
    })

    expect(totals).toEqual({
      subtotal: 301,
      discountAmount: 30.1,
      taxAmount: 54.18,
      total: 325.08,
      balanceDue: 275.08,
    })
  })

  it('rounds each line before adding it to the subtotal', () => {
    const totals = calculateTotals({ ...base, items: [{ quantity: 0.5, unitPrice: 99.99 }, { quantity: 2, unitPrice: 150.5 }] })

    expect(totals.subtotal).toBe(351)
  })

  it('caps a flat discount at the subtotal', () => {
    const totals = calculateTotals({ ...base, items: [{ quantity: 1, unitPrice: 40 }], discount: 100 })

    expect(totals.discountAmount).toBe(40)
    expect(totals.total).toBe(0)
  })

  it('ignores fields the user has cleared', () => {
    const totals = calculateTotals({ ...base, items: [{ quantity: Number.NaN, unitPrice: 10 }], tax: Number.NaN })

    expect(totals.total).toBe(0)
  })
})

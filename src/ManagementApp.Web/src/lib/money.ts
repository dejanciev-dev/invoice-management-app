import type { AdjustmentType } from '../api/types'

/**
 * Rounds to two decimals, half away from zero, matching the API's decimal rounding.
 * Shifting the exponent through a string avoids binary errors such as 49.995 * 100 = 4999.4999...
 */
export function roundMoney(value: number): number {
  if (!Number.isFinite(value) || Math.abs(value) < 1e-6) return 0
  const rounded = Number(`${Math.round(Number(`${Math.abs(value)}e2`))}e-2`)
  return Math.sign(value) * rounded
}

export interface TotalsInput {
  items: { quantity: number; unitPrice: number }[]
  discount: number
  discountType: AdjustmentType
  tax: number
  taxType: AdjustmentType
  amountPaid: number
}

export interface Totals {
  subtotal: number
  discountAmount: number
  taxAmount: number
  total: number
  balanceDue: number
}

const orZero = (value: number) => (Number.isFinite(value) ? value : 0)

/** Mirrors Invoice's calculations in the domain so the editor can preview totals as the user types. */
export function calculateTotals(input: TotalsInput): Totals {
  const subtotal = roundMoney(
    input.items.reduce((sum, item) => sum + roundMoney(orZero(item.quantity) * orZero(item.unitPrice)), 0),
  )

  const discount = orZero(input.discount)
  const discountAmount =
    input.discountType === 'Percentage' ? roundMoney((subtotal * discount) / 100) : Math.min(discount, subtotal)

  const tax = orZero(input.tax)
  const taxAmount = input.taxType === 'Percentage' ? roundMoney(((subtotal - discountAmount) * tax) / 100) : tax

  const total = roundMoney(subtotal - discountAmount + taxAmount)
  return { subtotal, discountAmount, taxAmount, total, balanceDue: roundMoney(total - orZero(input.amountPaid)) }
}

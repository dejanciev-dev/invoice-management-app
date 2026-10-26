export function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount)
  } catch {
    // Unknown currency code: fall back to a plain number with the code appended.
    return `${amount.toFixed(2)} ${currency}`
  }
}

/** Formats an ISO date (yyyy-MM-dd) without shifting it through the local time zone. */
export function formatDate(isoDate: string | null | undefined): string {
  if (!isoDate) return '—'
  const [year, month, day] = isoDate.split('-').map(Number)
  if (!year || !month || !day) return isoDate
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(year, month - 1, day))
}

export function toIsoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

export function addDays(isoDate: string, days: number): string {
  const [year, month, day] = isoDate.split('-').map(Number)
  return toIsoDate(new Date(year!, month! - 1, day! + days))
}

/** The first line of a multi-line address, e.g. the company name. */
export const firstLine = (text: string) => text.split('\n')[0]?.trim() ?? text

/** Adds up amounts per currency, e.g. "$1,200.00 + €80.00". */
export function formatTotalsByCurrency(entries: { amount: number; currency: string }[]): string {
  const totals = new Map<string, number>()
  for (const { amount, currency } of entries) {
    totals.set(currency, (totals.get(currency) ?? 0) + amount)
  }
  if (totals.size === 0) return formatMoney(0, 'USD')
  return [...totals].map(([currency, amount]) => formatMoney(amount, currency)).join(' + ')
}

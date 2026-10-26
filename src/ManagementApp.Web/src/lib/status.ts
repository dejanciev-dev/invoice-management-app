import type { InvoiceStatus } from '../api/types'

export const statusLabels: Record<InvoiceStatus, string> = {
  Unpaid: 'Unpaid',
  PartiallyPaid: 'Partially paid',
  Paid: 'Paid',
  Overdue: 'Overdue',
}

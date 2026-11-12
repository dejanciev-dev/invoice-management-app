import type { InvoiceStatus } from '../api/types'

export const statusLabels: Record<InvoiceStatus, string> = {
  Draft: 'Draft',
  Unpaid: 'Unpaid',
  PartiallyPaid: 'Partially paid',
  Paid: 'Paid',
  Overdue: 'Overdue',
  Void: 'Void',
}

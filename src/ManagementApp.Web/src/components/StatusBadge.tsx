import type { InvoiceStatus } from '../api/types'
import { statusLabels } from '../lib/status'

const styles: Record<InvoiceStatus, string> = {
  Draft: 'bg-sky-500/10 text-sky-800 ring-sky-500/25 dark:text-sky-300',
  Unpaid: 'bg-slate-500/10 text-slate-700 ring-slate-500/20 dark:text-slate-300',
  PartiallyPaid: 'bg-amber-500/10 text-amber-800 ring-amber-500/25 dark:text-amber-300',
  Paid: 'bg-emerald-500/10 text-emerald-800 ring-emerald-500/25 dark:text-emerald-300',
  Overdue: 'bg-red-500/10 text-red-700 ring-red-500/25 dark:text-red-300',
  Void: 'bg-transparent text-muted ring-line line-through',
}

export function StatusBadge({ status }: { status: InvoiceStatus }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${styles[status]}`}>
      {statusLabels[status]}
    </span>
  )
}

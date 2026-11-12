import { ApiError } from '../api/client'
import { useFinancialSummary } from '../api/tenant'
import { StatusBadge } from '../components/StatusBadge'
import { Alert, Card, EmptyRow, PageHeader, Spinner, StatTile, tableClass, tdClass, theadClass, thClass } from '../components/ui'
import { formatMoney } from '../lib/format'

export function ReportsPage() {
  const summary = useFinancialSummary()

  if (summary.isPending) return <Spinner label="Loading reports" />
  if (summary.isError) {
    const message = summary.error instanceof ApiError && summary.error.status === 403 ? summary.error.message : 'Could not load reports.'
    return <Alert>{message}</Alert>
  }

  const { totals, byStatus, monthly } = summary.data

  return (
    <>
      <PageHeader title="Financial reports" description="Totals are shown per currency and never converted or mixed." />

      <dl className="mb-6 grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-6">
        {byStatus.map((s) => (
          <StatTile key={s.status} label="Invoices" value={s.count} detail={<StatusBadge status={s.status} />} />
        ))}
      </dl>

      <Card className="mb-6 overflow-hidden">
        <h2 className="border-b border-line px-4 py-3 text-sm font-semibold">By currency</h2>
        <div className="overflow-x-auto">
          <table className={tableClass}>
            <thead className={theadClass}>
              <tr>
                <th scope="col" className={thClass}>Currency</th>
                <th scope="col" className={`${thClass} text-right`}>Invoices</th>
                <th scope="col" className={`${thClass} text-right`}>Billed</th>
                <th scope="col" className={`${thClass} text-right`}>Paid</th>
                <th scope="col" className={`${thClass} text-right`}>Outstanding</th>
                <th scope="col" className={`${thClass} text-right`}>Overdue</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line tabular-nums">
              {totals.length === 0 && <EmptyRow colSpan={6}>No invoices yet.</EmptyRow>}
              {totals.map((t) => (
                <tr key={t.currency}>
                  <td className={`${tdClass} font-medium`}>{t.currency}</td>
                  <td className={`${tdClass} text-right`}>{t.invoiceCount}</td>
                  <td className={`${tdClass} text-right`}>{formatMoney(t.billed, t.currency)}</td>
                  <td className={`${tdClass} text-right`}>{formatMoney(t.paid, t.currency)}</td>
                  <td className={`${tdClass} text-right`}>{formatMoney(t.outstanding, t.currency)}</td>
                  <td className={`${tdClass} text-right text-red-700 dark:text-red-300`}>{formatMoney(t.overdue, t.currency)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <Card className="overflow-hidden">
        <h2 className="border-b border-line px-4 py-3 text-sm font-semibold">Last 12 months (by issue date)</h2>
        <div className="overflow-x-auto">
          <table className={tableClass}>
            <thead className={theadClass}>
              <tr>
                <th scope="col" className={thClass}>Month</th>
                <th scope="col" className={thClass}>Currency</th>
                <th scope="col" className={`${thClass} text-right`}>Billed</th>
                <th scope="col" className={`${thClass} text-right`}>Paid</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line tabular-nums">
              {monthly.length === 0 && <EmptyRow colSpan={4}>Nothing invoiced in the last 12 months.</EmptyRow>}
              {monthly.map((m) => (
                <tr key={`${m.month}-${m.currency}`}>
                  <td className={tdClass}>{m.month}</td>
                  <td className={tdClass}>{m.currency}</td>
                  <td className={`${tdClass} text-right`}>{formatMoney(m.billed, m.currency)}</td>
                  <td className={`${tdClass} text-right`}>{formatMoney(m.paid, m.currency)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </>
  )
}

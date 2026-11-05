import type { AuditLog, Paged } from '../api/types'
import { actionLabel, auditActionFilters, formatDateTime } from '../lib/labels'
import { inputClass } from './styles'
import { Alert, Badge, Card, EmptyRow, Pager, Spinner, tableClass, tdClass, theadClass, thClass } from './ui'

interface AuditLogTableProps {
  data: Paged<AuditLog> | undefined
  isPending: boolean
  isError: boolean
  action: string
  onActionChange: (action: string) => void
  onPageChange: (page: number) => void
  showTenant?: boolean
}

export function AuditLogTable({ data, isPending, isError, action, onActionChange, onPageChange, showTenant }: AuditLogTableProps) {
  return (
    <Card className="overflow-hidden">
      <div className="flex justify-end border-b border-line p-4">
        <select aria-label="Filter activity" value={action} onChange={(e) => onActionChange(e.target.value)} className={`${inputClass} sm:w-56`}>
          {auditActionFilters.map((f) => (
            <option key={f.value} value={f.value}>
              {f.label}
            </option>
          ))}
        </select>
      </div>
      {isPending ? (
        <Spinner label="Loading activity" />
      ) : isError || !data ? (
        <div className="p-4">
          <Alert>Could not load the activity log.</Alert>
        </div>
      ) : (
        <>
          <div className="overflow-x-auto">
            <table className={tableClass}>
              <thead className={theadClass}>
                <tr>
                  <th scope="col" className={thClass}>When</th>
                  <th scope="col" className={thClass}>Activity</th>
                  {showTenant && <th scope="col" className={thClass}>Company</th>}
                  <th scope="col" className={thClass}>By</th>
                  <th scope="col" className={`${thClass} hidden lg:table-cell`}>Details</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {data.items.length === 0 && <EmptyRow colSpan={showTenant ? 5 : 4}>No activity yet.</EmptyRow>}
                {data.items.map((log) => (
                  <tr key={log.id}>
                    <td className={`${tdClass} whitespace-nowrap text-muted`}>{formatDateTime(log.timestamp)}</td>
                    <td className={tdClass}>
                      <span className="font-medium">{actionLabel(log.action)}</span>
                      {log.entityType && <span className="block text-xs text-muted">{log.entityType} {log.entityId}</span>}
                    </td>
                    {showTenant && <td className={tdClass}>{log.tenantName ?? (log.tenantId ? 'Deleted company' : 'Platform')}</td>}
                    <td className={tdClass}>
                      <span>{log.userEmail ?? '—'}</span>
                      {log.impersonatorEmail && (
                        <Badge className="mt-1 block w-fit bg-amber-400/15 text-amber-800 ring-amber-500/30 dark:text-amber-300">
                          via {log.impersonatorEmail}
                        </Badge>
                      )}
                    </td>
                    <td className={`${tdClass} hidden max-w-sm truncate font-mono text-xs text-muted lg:table-cell`} title={log.details ?? undefined}>
                      {log.details}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pager page={data.page} pageSize={data.pageSize} total={data.total} onChange={onPageChange} />
        </>
      )}
    </Card>
  )
}

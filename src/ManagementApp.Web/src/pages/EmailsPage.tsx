import { RotateCw } from 'lucide-react'
import { useState } from 'react'
import { useEmails, useRetryEmail } from '../api/emails'
import type { EmailStatus } from '../api/types'
import { Alert, Badge, Button, Card, EmptyRow, PageHeader, Pager, Spinner, tableClass, tdClass, theadClass, thClass } from '../components/ui'
import { formatDateTime } from '../lib/labels'

const statusFilters: (EmailStatus | undefined)[] = [undefined, 'Pending', 'Sent', 'Failed']

const statusStyles: Record<EmailStatus, string> = {
  Pending: 'bg-sky-500/10 text-sky-800 ring-sky-500/25 dark:text-sky-300',
  Sent: 'bg-emerald-500/10 text-emerald-800 ring-emerald-500/25 dark:text-emerald-300',
  Failed: 'bg-red-500/10 text-red-700 ring-red-500/25 dark:text-red-300',
}

/** Every email the company sent or is about to send, with its delivery status; failed ones can be retried. */
export function EmailsPage() {
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState<EmailStatus | undefined>()
  const emails = useEmails(page, status)
  const retry = useRetryEmail()

  return (
    <>
      <PageHeader
        title="Emails"
        description="Emails sent on behalf of your company, such as invitations and reminders. A failed send is retried automatically for about an hour and a half."
      />
      <Card className="overflow-hidden">
        <div role="tablist" aria-label="Filter by status" className="flex flex-wrap gap-1 border-b border-line p-4">
          {statusFilters.map((filter) => (
            <button
              key={filter ?? 'all'}
              type="button"
              role="tab"
              aria-selected={status === filter}
              onClick={() => {
                setStatus(filter)
                setPage(1)
              }}
              className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                status === filter ? 'bg-subtle text-ink' : 'text-muted hover:text-ink'
              }`}
            >
              {filter ?? 'All'}
            </button>
          ))}
        </div>
        {retry.error && (
          <div className="p-4 pb-0">
            <Alert>{retry.error.message}</Alert>
          </div>
        )}
        {emails.isPending ? (
          <Spinner label="Loading emails" />
        ) : emails.isError ? (
          <div className="p-4">
            <Alert>Could not load the emails. Please refresh the page.</Alert>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className={tableClass}>
                <thead className={theadClass}>
                  <tr>
                    <th scope="col" className={thClass}>Email</th>
                    <th scope="col" className={`${thClass} hidden md:table-cell`}>Queued</th>
                    <th scope="col" className={thClass}>Status</th>
                    <th scope="col" className={thClass}>
                      <span className="sr-only">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {emails.data.items.length === 0 && <EmptyRow colSpan={4}>No emails yet.</EmptyRow>}
                  {emails.data.items.map((email) => (
                    <tr key={email.id}>
                      <td className={tdClass}>
                        <p className="font-medium">{email.subject}</p>
                        <p className="text-muted">
                          To {email.to}
                          {email.attachments.length > 0 && <> · {email.attachments.join(', ')}</>}
                        </p>
                      </td>
                      <td className={`${tdClass} hidden whitespace-nowrap text-muted md:table-cell`}>{formatDateTime(email.createdAt)}</td>
                      <td className={tdClass}>
                        <Badge className={statusStyles[email.status]}>{email.status}</Badge>
                        <p className="mt-1 text-xs text-muted">
                          {email.status === 'Sent' && <>Sent {formatDateTime(email.sentAt)}</>}
                          {email.status === 'Pending' && email.attempts > 0 && <>Retrying {formatDateTime(email.nextAttemptAt)}</>}
                          {email.status === 'Failed' && <>Gave up after {email.attempts} attempts</>}
                        </p>
                        {email.lastError && email.status !== 'Sent' && (
                          <p className="mt-1 max-w-xs text-xs break-words text-red-700 dark:text-red-300">{email.lastError}</p>
                        )}
                      </td>
                      <td className={`${tdClass} text-right`}>
                        {email.status === 'Failed' && (
                          <Button
                            variant="secondary"
                            onClick={() => retry.mutate(email.id)}
                            loading={retry.isPending && retry.variables === email.id}
                          >
                            <RotateCw className="size-4" aria-hidden />
                            Retry
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pager page={emails.data.page} pageSize={emails.data.pageSize} total={emails.data.total} onChange={setPage} />
          </>
        )}
      </Card>
    </>
  )
}

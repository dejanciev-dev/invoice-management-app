import { useState } from 'react'
import { useTenantAuditLogs } from '../api/tenant'
import { AuditLogTable } from '../components/AuditLogTable'
import { PageHeader } from '../components/ui'

export function ActivityPage() {
  const [page, setPage] = useState(1)
  const [action, setAction] = useState('')
  const logs = useTenantAuditLogs(page, action || undefined)

  return (
    <>
      <PageHeader title="Activity" description="Who did what in your company, including any work done by platform support." />
      <AuditLogTable
        data={logs.data}
        isPending={logs.isPending}
        isError={logs.isError}
        action={action}
        onActionChange={(value) => {
          setAction(value)
          setPage(1)
        }}
        onPageChange={setPage}
      />
    </>
  )
}

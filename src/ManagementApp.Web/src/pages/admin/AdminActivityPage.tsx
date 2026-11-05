import { useState } from 'react'
import { useAuditLogs } from '../../api/admin'
import { AuditLogTable } from '../../components/AuditLogTable'
import { PageHeader } from '../../components/ui'

export function AdminActivityPage() {
  const [page, setPage] = useState(1)
  const [action, setAction] = useState('')
  const logs = useAuditLogs({ page, action: action || undefined })

  return (
    <>
      <PageHeader title="Platform activity" description="Everything recorded across all tenants, including impersonation sessions." />
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
        showTenant
      />
    </>
  )
}

import { UserRoundCog } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useImpersonate } from '../../api/admin'
import { errorText } from '../../lib/errors'
import type { TenantStatus } from '../../api/types'
import { inputClass } from '../../components/styles'
import { Badge, Button } from '../../components/ui'
import { tenantStatusStyles } from '../../lib/labels'

export function TenantStatusBadge({ status }: { status: TenantStatus }) {
  return <Badge className={tenantStatusStyles[status]}>{status}</Badge>
}

/** Asks for a reason, then signs the admin in as the user. The reason goes into the audit log. */
export function ImpersonateButton({ userId, tenantId, label }: { userId: string; tenantId: string; label: string }) {
  const impersonate = useImpersonate()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')

  if (!open) {
    return (
      <Button variant="ghost" onClick={() => setOpen(true)} aria-label={`Impersonate ${label}`}>
        <UserRoundCog className="size-4" aria-hidden />
        Impersonate
      </Button>
    )
  }

  return (
    <form
      className="flex flex-col items-end gap-2"
      onSubmit={(e) => {
        e.preventDefault()
        impersonate.mutate({ userId, tenantId, reason: reason.trim() || null }, { onSuccess: () => navigate('/', { replace: true }) })
      }}
    >
      <input
        autoFocus
        aria-label="Reason for impersonating"
        placeholder="Reason, e.g. support ticket"
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        className={`${inputClass} w-60`}
      />
      {impersonate.error && <p className="max-w-60 text-xs text-red-600">{errorText(impersonate.error)}</p>}
      <div className="flex gap-2">
        <Button variant="secondary" onClick={() => setOpen(false)}>
          Cancel
        </Button>
        <Button type="submit" loading={impersonate.isPending}>
          Start
        </Button>
      </div>
    </form>
  )
}

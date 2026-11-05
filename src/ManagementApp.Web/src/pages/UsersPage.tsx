import { Copy, KeyRound, Pencil, UserMinus, UserPlus } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useSession } from '../api/auth'
import { errorText } from '../lib/errors'
import {
  useCreateMember,
  useInvitations,
  useInviteMember,
  useMembers,
  useRemoveMember,
  useResetMemberPassword,
  useRevokeInvitation,
  useSetMemberStatus,
  useUpdateMember,
} from '../api/tenant'
import type { Member, Permission, TenantRole } from '../api/types'
import { inputClass } from '../components/styles'
import { Alert, Badge, Button, Card, EmptyRow, Field, Notice, PageHeader, Spinner, tableClass, tdClass, theadClass, thClass } from '../components/ui'
import { allPermissions, formatDateTime, permissionLabels, roleDescriptions, roleLabels, roles } from '../lib/labels'

export function UsersPage() {
  const members = useMembers()
  const [panel, setPanel] = useState<'invite' | 'create' | null>(null)

  return (
    <>
      <PageHeader
        title="Users"
        description="People in your company, their roles and what they can do."
        actions={
          <>
            <Button variant="secondary" onClick={() => setPanel(panel === 'create' ? null : 'create')}>
              Create user
            </Button>
            <Button onClick={() => setPanel(panel === 'invite' ? null : 'invite')}>
              <UserPlus className="size-4" aria-hidden />
              Invite
            </Button>
          </>
        }
      />

      {panel === 'invite' && <InviteForm onDone={() => setPanel(null)} />}
      {panel === 'create' && <CreateUserForm onDone={() => setPanel(null)} />}

      <Card className="mb-6 overflow-hidden">
        {members.isPending ? (
          <Spinner label="Loading users" />
        ) : members.isError ? (
          <div className="p-4">
            <Alert>Could not load users.</Alert>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className={tableClass}>
              <thead className={theadClass}>
                <tr>
                  <th scope="col" className={thClass}>User</th>
                  <th scope="col" className={thClass}>Role</th>
                  <th scope="col" className={`${thClass} hidden md:table-cell`}>Last sign-in</th>
                  <th scope="col" className={`${thClass} text-right`}>Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {members.data.map((member) => (
                  <MemberRow key={member.userId} member={member} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <PendingInvitations />
    </>
  )
}

function MemberRow({ member }: { member: Member }) {
  const session = useSession()
  const isSelf = member.userId === session.userId
  const [mode, setMode] = useState<'edit' | 'password' | 'remove' | null>(null)
  const setStatus = useSetMemberStatus()

  return (
    <>
      <tr className={member.isActive ? '' : 'opacity-60'}>
        <td className={tdClass}>
          <span className="font-medium">{member.displayName ?? member.email}</span>
          {isSelf && <span className="ml-2 text-xs text-muted">(you)</span>}
          {member.displayName && <span className="block text-xs text-muted">{member.email}</span>}
          {!member.isActive && <Badge className="mt-1 bg-slate-500/10 text-slate-700 ring-slate-500/20 dark:text-slate-300">Deactivated</Badge>}
          {!member.accountIsActive && <Badge className="mt-1 bg-red-500/10 text-red-700 ring-red-500/25">Account disabled</Badge>}
        </td>
        <td className={tdClass}>
          {roleLabels[member.role]}
          <span className="block text-xs text-muted">{member.permissions.length} permissions</span>
        </td>
        <td className={`${tdClass} hidden text-muted md:table-cell`}>{formatDateTime(member.lastLoginAt)}</td>
        <td className={`${tdClass} text-right`}>
          {!isSelf && (
            <div className="flex flex-wrap justify-end gap-1">
              <Button variant="ghost" className="px-2" aria-label={`Edit ${member.email}`} onClick={() => setMode(mode === 'edit' ? null : 'edit')}>
                <Pencil className="size-4" aria-hidden />
              </Button>
              {member.accountManagedByTenant && (
                <Button variant="ghost" className="px-2" aria-label={`Reset password for ${member.email}`} onClick={() => setMode(mode === 'password' ? null : 'password')}>
                  <KeyRound className="size-4" aria-hidden />
                </Button>
              )}
              <Button
                variant="ghost"
                loading={setStatus.isPending}
                onClick={() => setStatus.mutate({ userId: member.userId, isActive: !member.isActive })}
              >
                {member.isActive ? 'Deactivate' : 'Activate'}
              </Button>
              <Button variant="ghost" className="px-2" aria-label={`Remove ${member.email}`} onClick={() => setMode(mode === 'remove' ? null : 'remove')}>
                <UserMinus className="size-4" aria-hidden />
              </Button>
            </div>
          )}
          {setStatus.error && <p className="mt-1 text-xs text-red-600">{errorText(setStatus.error)}</p>}
        </td>
      </tr>
      {mode && (
        <tr className="bg-subtle/40">
          <td colSpan={4} className="px-4 py-4">
            {mode === 'edit' && <EditMember member={member} onDone={() => setMode(null)} />}
            {mode === 'password' && <ResetPassword member={member} onDone={() => setMode(null)} />}
            {mode === 'remove' && <RemoveMember member={member} onDone={() => setMode(null)} />}
          </td>
        </tr>
      )}
    </>
  )
}

function EditMember({ member, onDone }: { member: Member; onDone: () => void }) {
  const update = useUpdateMember()
  const [displayName, setDisplayName] = useState(member.displayName ?? '')
  const [role, setRole] = useState<TenantRole>(member.role)
  const [permissions, setPermissions] = useState<Permission[]>(member.permissions)
  const [customised, setCustomised] = useState(false)

  const toggle = (permission: Permission) => {
    setCustomised(true)
    setPermissions((current) => (current.includes(permission) ? current.filter((p) => p !== permission) : [...current, permission]))
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    update.mutate(
      {
        userId: member.userId,
        displayName: displayName.trim() || null,
        role,
        // Changing the role without touching the checkboxes resets to that role's defaults.
        permissions: customised || role === member.role ? permissions : null,
      },
      { onSuccess: onDone },
    )
  }

  return (
    <form onSubmit={submit} className="space-y-4">
      {update.error && <Alert>{errorText(update.error)}</Alert>}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" htmlFor={`name-${member.userId}`} hint={member.accountManagedByTenant ? undefined : 'This person also belongs to another company, so only they can change their name.'}>
          <input id={`name-${member.userId}`} value={displayName} disabled={!member.accountManagedByTenant} onChange={(e) => setDisplayName(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Role" htmlFor={`role-${member.userId}`} hint={roleDescriptions[role]}>
          <select
            id={`role-${member.userId}`}
            value={role}
            onChange={(e) => {
              setRole(e.target.value as TenantRole)
              setCustomised(false)
            }}
            className={inputClass}
          >
            {roles.map((r) => (
              <option key={r} value={r}>
                {roleLabels[r]}
              </option>
            ))}
          </select>
        </Field>
      </div>
      {role !== 'TenantAdmin' && (role === member.role || customised) && (
        <fieldset>
          <legend className="mb-2 text-sm font-medium">Permissions</legend>
          <div className="grid gap-2 sm:grid-cols-3">
            {allPermissions.map((permission) => (
              <label key={permission} className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={permissions.includes(permission)} onChange={() => toggle(permission)} className="size-4 accent-brand" />
                {permissionLabels[permission]}
              </label>
            ))}
          </div>
        </fieldset>
      )}
      <div className="flex gap-2">
        <Button type="submit" loading={update.isPending}>
          Save
        </Button>
        <Button variant="secondary" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  )
}

function ResetPassword({ member, onDone }: { member: Member; onDone: () => void }) {
  const reset = useResetMemberPassword()
  const [newPassword, setNewPassword] = useState('')

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        reset.mutate({ userId: member.userId, newPassword }, { onSuccess: onDone })
      }}
      className="flex flex-col gap-3 sm:flex-row sm:items-end"
    >
      <Field label={`New password for ${member.email}`} htmlFor={`pw-${member.userId}`} error={errorText(reset.error) ?? undefined} className="flex-1">
        <input id={`pw-${member.userId}`} type="password" autoComplete="new-password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} className={inputClass} />
      </Field>
      <Button type="submit" loading={reset.isPending}>
        Set password
      </Button>
      <Button variant="secondary" onClick={onDone}>
        Cancel
      </Button>
    </form>
  )
}

function RemoveMember({ member, onDone }: { member: Member; onDone: () => void }) {
  const remove = useRemoveMember()
  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
      <p className="flex-1 text-sm">
        Remove <strong>{member.email}</strong> from the company? Their account stays, but they lose access to this company.
      </p>
      {remove.error && <p className="text-xs text-red-600">{errorText(remove.error)}</p>}
      <Button variant="danger" loading={remove.isPending} onClick={() => remove.mutate(member.userId, { onSuccess: onDone })}>
        Remove
      </Button>
      <Button variant="secondary" onClick={onDone}>
        Cancel
      </Button>
    </div>
  )
}

function InviteForm({ onDone }: { onDone: () => void }) {
  const invite = useInviteMember()
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<TenantRole>('Employee')
  const link = invite.data ? `${window.location.origin}/accept-invite?token=${encodeURIComponent(invite.data.token)}` : null

  return (
    <Card className="mb-6 p-5">
      <h2 className="mb-4 text-sm font-semibold">Invite someone</h2>
      {link ? (
        <div className="space-y-3">
          <Notice>Invitation created for {invite.data?.invitation.email}. Share this link with them; it works once and expires in 7 days.</Notice>
          <div className="flex gap-2">
            <input readOnly value={link} aria-label="Invitation link" className={`${inputClass} font-mono text-xs`} onFocus={(e) => e.target.select()} />
            <Button variant="secondary" onClick={() => void navigator.clipboard?.writeText(link)}>
              <Copy className="size-4" aria-hidden />
              Copy
            </Button>
          </div>
          <Button variant="secondary" onClick={onDone}>
            Done
          </Button>
        </div>
      ) : (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            invite.mutate({ email, role })
          }}
          className="grid gap-4 sm:grid-cols-[1fr_12rem_auto] sm:items-end"
        >
          <Field label="Email" htmlFor="invite-email" error={errorText(invite.error) ?? undefined}>
            <input id="invite-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} className={inputClass} />
          </Field>
          <RoleSelect id="invite-role" value={role} onChange={setRole} />
          <Button type="submit" loading={invite.isPending}>
            Send invitation
          </Button>
        </form>
      )}
    </Card>
  )
}

function CreateUserForm({ onDone }: { onDone: () => void }) {
  const create = useCreateMember()
  const [form, setForm] = useState({ email: '', displayName: '', password: '', role: 'Employee' as TenantRole })

  return (
    <Card className="mb-6 p-5">
      <h2 className="mb-1 text-sm font-semibold">Create a user</h2>
      <p className="mb-4 text-xs text-muted">Creates a new account with a temporary password you give them. For existing accounts, send an invitation.</p>
      <form
        onSubmit={(e) => {
          e.preventDefault()
          create.mutate({ ...form, displayName: form.displayName.trim() || null }, { onSuccess: onDone })
        }}
        className="grid gap-4 sm:grid-cols-2"
      >
        {create.error && <div className="sm:col-span-2"><Alert>{errorText(create.error)}</Alert></div>}
        <Field label="Email" htmlFor="create-email">
          <input id="create-email" type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} className={inputClass} />
        </Field>
        <Field label="Name" htmlFor="create-name">
          <input id="create-name" value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} className={inputClass} />
        </Field>
        <Field label="Temporary password" htmlFor="create-password">
          <input id="create-password" type="password" autoComplete="new-password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} className={inputClass} />
        </Field>
        <RoleSelect id="create-role" value={form.role} onChange={(role) => setForm({ ...form, role })} />
        <div className="flex gap-2 sm:col-span-2">
          <Button type="submit" loading={create.isPending}>
            Create user
          </Button>
          <Button variant="secondary" onClick={onDone}>
            Cancel
          </Button>
        </div>
      </form>
    </Card>
  )
}

function RoleSelect({ id, value, onChange }: { id: string; value: TenantRole; onChange: (role: TenantRole) => void }) {
  return (
    <Field label="Role" htmlFor={id}>
      <select id={id} value={value} onChange={(e) => onChange(e.target.value as TenantRole)} className={inputClass}>
        {roles.map((r) => (
          <option key={r} value={r}>
            {roleLabels[r]}
          </option>
        ))}
      </select>
    </Field>
  )
}

function PendingInvitations() {
  const invitations = useInvitations()
  const revoke = useRevokeInvitation()
  if (!invitations.data || invitations.data.length === 0) return null

  return (
    <Card className="overflow-hidden">
      <h2 className="border-b border-line px-4 py-3 text-sm font-semibold">Pending invitations</h2>
      <table className={tableClass}>
        <tbody className="divide-y divide-line">
          {invitations.data.length === 0 && <EmptyRow colSpan={3}>No pending invitations.</EmptyRow>}
          {invitations.data.map((invitation) => (
            <tr key={invitation.id}>
              <td className={tdClass}>{invitation.email}</td>
              <td className={`${tdClass} text-muted`}>
                {roleLabels[invitation.role]} · expires {formatDateTime(invitation.expiresAt)}
              </td>
              <td className={`${tdClass} text-right`}>
                <Button variant="ghost" onClick={() => revoke.mutate(invitation.id)}>
                  Revoke
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </Card>
  )
}

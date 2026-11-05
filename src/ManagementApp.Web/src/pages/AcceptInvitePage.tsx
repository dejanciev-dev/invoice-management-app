import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { useCurrentUser, useLogin, useSwitchTenant } from '../api/auth'
import { ApiError } from '../api/client'
import { useAcceptInvitation, useInvitationPreview } from '../api/tenant'
import { AuthShell } from '../components/AuthShell'
import { inputClass } from '../components/styles'
import { Alert, Button, Field, FullPageSpinner } from '../components/ui'
import { roleLabels } from '../lib/labels'

export function AcceptInvitePage() {
  const token = useSearchParams()[0].get('token') ?? ''
  const preview = useInvitationPreview(token)
  const { data: session, isPending: sessionPending } = useCurrentUser()
  const accept = useAcceptInvitation()
  const login = useLogin()
  const switchTenant = useSwitchTenant()
  const navigate = useNavigate()
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')

  if (preview.isPending || sessionPending) return <FullPageSpinner />

  if (!token || preview.isError) {
    return (
      <AuthShell title="Invitation not found" subtitle="" footer={<Link to="/login" className="font-medium text-brand">Go to sign in</Link>}>
        <Alert>This invitation link is invalid, has expired or was already used. Ask your admin for a new one.</Alert>
      </AuthShell>
    )
  }

  const invitation = preview.data
  const signedInAsInvitee = session && session.email.toLowerCase() === invitation.email.toLowerCase()

  const finish = (tenantId: string) => {
    switchTenant(tenantId)
    navigate('/', { replace: true })
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    accept.mutate(
      { token, password: session ? null : password, displayName: displayName.trim() || null },
      {
        onSuccess: ({ tenantId }) => {
          if (session) finish(tenantId)
          else login.mutate({ email: invitation.email, password, remember: true }, { onSuccess: () => finish(tenantId) })
        },
      },
    )
  }

  const error = accept.error instanceof ApiError ? accept.error.messages.join(' ') : accept.error && 'Something went wrong.'

  return (
    <AuthShell
      title={`Join ${invitation.tenantName}`}
      subtitle={`You've been invited as ${roleLabels[invitation.role].toLowerCase()} (${invitation.email}).`}
      footer={<Link to="/" className="font-medium text-brand">Back to the app</Link>}
    >
      <form onSubmit={submit} className="space-y-4" noValidate>
        {error && <Alert>{error}</Alert>}
        {session && !signedInAsInvitee ? (
          <Alert>
            You're signed in as {session.email}, but this invitation is for {invitation.email}. Sign out and sign in with
            that address to accept it.
          </Alert>
        ) : session ? (
          <Button type="submit" className="w-full" loading={accept.isPending}>
            Accept invitation
          </Button>
        ) : invitation.accountExists ? (
          <Alert>
            You already have an account. <Link to="/login" state={{ from: `/accept-invite?token=${encodeURIComponent(token)}` }} className="font-medium underline">Sign in</Link> to accept the invitation.
          </Alert>
        ) : (
          <>
            <Field label="Your name" htmlFor="displayName">
              <input id="displayName" autoComplete="name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} className={inputClass} />
            </Field>
            <Field label="Choose a password" htmlFor="password" hint="At least 6 characters with upper and lower case letters, a digit and a symbol.">
              <input id="password" type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} className={inputClass} />
            </Field>
            <Button type="submit" className="w-full" loading={accept.isPending || login.isPending}>
              Create account and join
            </Button>
          </>
        )}
      </form>
    </AuthShell>
  )
}

import { useState, type FormEvent, type ReactNode } from 'react'
import { useChangePassword, useSession, useSignOutEverywhere, useUpdateProfile } from '../api/auth'
import { ApiError } from '../api/client'
import { inputClass } from '../components/styles'
import { Alert, Button, Card, Field, Notice, PageHeader } from '../components/ui'

function Section({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <Card className="p-5 sm:p-6">
      <h2 className="text-sm font-semibold">{title}</h2>
      <p className="mt-1 text-xs text-muted">{description}</p>
      <div className="mt-4">{children}</div>
    </Card>
  )
}

function errorMessages(error: unknown): string[] {
  if (!error) return []
  return error instanceof ApiError ? error.messages : ['Something went wrong. Please try again.']
}

function ErrorList({ error }: { error: unknown }) {
  const messages = errorMessages(error)
  if (messages.length === 0) return null
  return (
    <Alert>
      <ul className="list-inside list-disc space-y-0.5">
        {messages.map((message) => (
          <li key={message}>{message}</li>
        ))}
      </ul>
    </Alert>
  )
}

function ProfileSection() {
  const session = useSession()
  const update = useUpdateProfile()
  const [displayName, setDisplayName] = useState(session.displayName ?? '')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    update.mutate({ displayName: displayName.trim() || null })
  }

  return (
    <Section title="Profile" description="How you appear to the other people in your companies.">
      <form onSubmit={submit} className="space-y-4" noValidate>
        <ErrorList error={update.error} />
        {update.isSuccess && <Notice>Your profile has been saved.</Notice>}
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Email" htmlFor="email" hint="You sign in with this address.">
            <input id="email" type="email" value={session.email} readOnly disabled className={inputClass} />
          </Field>
          <Field label="Display name" htmlFor="displayName">
            <input
              id="displayName"
              autoComplete="name"
              maxLength={100}
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              className={inputClass}
            />
          </Field>
        </div>
        <Button type="submit" loading={update.isPending}>
          Save profile
        </Button>
      </form>
    </Section>
  )
}

function PasswordSection() {
  const change = useChangePassword()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [mismatch, setMismatch] = useState(false)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (newPassword !== confirmPassword) {
      setMismatch(true)
      return
    }
    setMismatch(false)
    change.mutate(
      { currentPassword, newPassword },
      {
        onSuccess: () => {
          setCurrentPassword('')
          setNewPassword('')
          setConfirmPassword('')
        },
      },
    )
  }

  return (
    <Section title="Password" description="Changing it signs you out on every other device; you stay signed in here.">
      <form onSubmit={submit} className="space-y-4" noValidate>
        <ErrorList error={change.error} />
        {change.isSuccess && <Notice>Your password has been changed. Other devices have been signed out.</Notice>}
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Current password" htmlFor="currentPassword">
            <input
              id="currentPassword"
              type="password"
              autoComplete="current-password"
              required
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              className={inputClass}
            />
          </Field>
          <Field label="New password" htmlFor="newPassword">
            <input
              id="newPassword"
              type="password"
              autoComplete="new-password"
              required
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              className={inputClass}
            />
          </Field>
          <Field label="Confirm new password" htmlFor="confirmPassword" error={mismatch ? 'Passwords do not match.' : undefined}>
            <input
              id="confirmPassword"
              type="password"
              autoComplete="new-password"
              required
              aria-invalid={mismatch || undefined}
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              className={inputClass}
            />
          </Field>
        </div>
        <Button type="submit" loading={change.isPending} disabled={!currentPassword || !newPassword}>
          Change password
        </Button>
      </form>
    </Section>
  )
}

function SessionsSection() {
  const signOut = useSignOutEverywhere()

  return (
    <Section
      title="Sessions"
      description="Signed in on a device you no longer use, or think someone else has your password? End every session, this one included."
    >
      <div className="space-y-4">
        <ErrorList error={signOut.error} />
        <Button
          variant="secondary"
          loading={signOut.isPending}
          onClick={() => {
            if (window.confirm('Sign out on every device, including this one?')) signOut.mutate()
          }}
        >
          Sign out everywhere
        </Button>
      </div>
    </Section>
  )
}

export function AccountPage() {
  return (
    <div>
      <PageHeader title="My account" description="Your sign-in details. They're the same in every company you belong to." />
      <div className="space-y-6">
        <ProfileSection />
        <PasswordSection />
        <SessionsSection />
      </div>
    </div>
  )
}

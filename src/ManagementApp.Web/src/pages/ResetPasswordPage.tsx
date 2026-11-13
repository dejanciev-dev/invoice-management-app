import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useResetPassword } from '../api/auth'
import { ApiError } from '../api/client'
import { AuthShell } from '../components/AuthShell'
import { inputClass } from '../components/styles'
import { Alert, Button, ButtonLink, Field, Notice } from '../components/ui'

/** Opened from the link in a password reset email: `/reset-password?email=…&code=…`. */
export function ResetPasswordPage() {
  const [params] = useSearchParams()
  const email = params.get('email') ?? ''
  const code = params.get('code') ?? ''
  const reset = useResetPassword()
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [mismatch, setMismatch] = useState(false)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (password !== confirmPassword) {
      setMismatch(true)
      return
    }
    setMismatch(false)
    reset.mutate({ email, code, newPassword: password })
  }

  const errors =
    reset.error instanceof ApiError
      ? reset.error.status === 429
        ? ['Too many attempts. Please wait a minute and try again.']
        : reset.error.messages
      : reset.error
        ? ['Something went wrong. Please try again.']
        : []

  const footer = (
    <Link to="/forgot-password" className="font-medium text-brand hover:text-brand-strong">
      Ask for a new link
    </Link>
  )

  if (!email || !code) {
    return (
      <AuthShell title="Reset your password" subtitle="This link is incomplete." footer={footer}>
        <Alert>Open the link from the email again, or ask for a new one.</Alert>
      </AuthShell>
    )
  }

  if (reset.isSuccess) {
    return (
      <AuthShell title="Password changed" subtitle={email} footer={null}>
        <div className="space-y-4">
          <Notice>Your password has been changed, and you've been signed out on every device.</Notice>
          <ButtonLink to="/login" className="w-full">
            Sign in
          </ButtonLink>
        </div>
      </AuthShell>
    )
  }

  return (
    <AuthShell title="Choose a new password" subtitle={email} footer={footer}>
      <form onSubmit={submit} className="space-y-4" noValidate>
        {errors.length > 0 && (
          <Alert>
            <ul className="list-inside list-disc space-y-0.5">
              {errors.map((message) => (
                <li key={message}>{message}</li>
              ))}
            </ul>
          </Alert>
        )}
        <Field
          label="New password"
          htmlFor="password"
          hint="At least 6 characters with upper and lower case letters, a digit and a symbol."
        >
          <input
            id="password"
            type="password"
            autoComplete="new-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
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
        <Button type="submit" className="w-full" loading={reset.isPending}>
          Change password
        </Button>
      </form>
    </AuthShell>
  )
}

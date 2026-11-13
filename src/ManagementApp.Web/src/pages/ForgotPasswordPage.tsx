import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useRequestPasswordReset } from '../api/auth'
import { ApiError } from '../api/client'
import { AuthShell } from '../components/AuthShell'
import { inputClass } from '../components/styles'
import { Alert, Button, Field, Notice } from '../components/ui'

export function ForgotPasswordPage() {
  const request = useRequestPasswordReset()
  const [email, setEmail] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    request.mutate({ email: email.trim() })
  }

  const error =
    request.error instanceof ApiError && request.error.status === 429
      ? 'Too many requests. Please wait a while and try again.'
      : request.error instanceof ApiError && request.error.status === 400
        ? 'Enter a valid email address.'
        : request.error && 'Something went wrong. Please try again.'

  return (
    <AuthShell
      title="Forgot your password?"
      subtitle="We'll email you a link to choose a new one."
      footer={
        <Link to="/login" className="font-medium text-brand hover:text-brand-strong">
          Back to sign in
        </Link>
      }
    >
      {request.isSuccess ? (
        <Notice>
          If an account exists for <strong>{email.trim()}</strong>, we've sent it a link to reset the password. The link
          works for 2 hours. Check your spam folder if it doesn't arrive.
        </Notice>
      ) : (
        <form onSubmit={submit} className="space-y-4" noValidate>
          {error && <Alert>{error}</Alert>}
          <Field label="Email" htmlFor="email">
            <input
              id="email"
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className={inputClass}
            />
          </Field>
          <Button type="submit" className="w-full" loading={request.isPending}>
            Send reset link
          </Button>
        </form>
      )}
    </AuthShell>
  )
}

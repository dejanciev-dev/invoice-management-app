import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useLogin, useSignup } from '../api/auth'
import { ApiError } from '../api/client'
import { AuthShell } from '../components/AuthShell'
import { inputClass } from '../components/styles'
import { Alert, Button, Field } from '../components/ui'

export function RegisterPage() {
  const signup = useSignup()
  const login = useLogin()
  const [companyName, setCompanyName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [mismatch, setMismatch] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (password !== confirmPassword) {
      setMismatch(true)
      return
    }
    setMismatch(false)

    await signup.mutateAsync({ companyName, email, password })
    login.mutate({ email, password, remember: true })
  }

  const errors =
    signup.error instanceof ApiError
      ? signup.error.messages
      : signup.error || login.error
        ? ['Something went wrong. Please try again.']
        : []

  return (
    <AuthShell
      title="Create your company account"
      subtitle="Start a free 14-day trial. You'll be the admin of your company."
      footer={
        <>
          Already have an account?{' '}
          <Link to="/login" className="font-medium text-brand hover:text-brand-strong">
            Sign in
          </Link>
        </>
      }
    >
      <form onSubmit={(e) => void submit(e).catch(() => {})} className="space-y-4" noValidate>
        {errors.length > 0 && (
          <Alert>
            <ul className="list-inside list-disc space-y-0.5">
              {errors.map((message) => (
                <li key={message}>{message}</li>
              ))}
            </ul>
          </Alert>
        )}
        <Field label="Company name" htmlFor="companyName">
          <input
            id="companyName"
            autoComplete="organization"
            required
            value={companyName}
            onChange={(e) => setCompanyName(e.target.value)}
            className={inputClass}
          />
        </Field>
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
        <Field
          label="Password"
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
        <Field label="Confirm password" htmlFor="confirmPassword" error={mismatch ? 'Passwords do not match.' : undefined}>
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
        <Button type="submit" className="w-full" loading={signup.isPending || login.isPending}>
          Create account
        </Button>
      </form>
    </AuthShell>
  )
}

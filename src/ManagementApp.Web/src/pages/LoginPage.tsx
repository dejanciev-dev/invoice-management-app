import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useLogin } from '../api/auth'
import { ApiError } from '../api/client'
import { AuthShell } from '../components/AuthShell'
import { inputClass } from '../components/styles'
import { Alert, Button, Field } from '../components/ui'

const DEMO_ACCOUNT = { email: 'demo@example.com', password: 'Demo123!' }

export function LoginPage() {
  const login = useLogin()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [remember, setRemember] = useState(true)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    login.mutate({ email, password, remember })
  }

  const useDemoAccount = () => {
    setEmail(DEMO_ACCOUNT.email)
    setPassword(DEMO_ACCOUNT.password)
    login.mutate({ ...DEMO_ACCOUNT, remember })
  }

  const error =
    login.error instanceof ApiError && login.error.status === 401
      ? 'Incorrect email or password.'
      : login.error && 'Something went wrong. Please try again.'

  return (
    <AuthShell
      title="Welcome back"
      subtitle="Sign in to manage your invoices."
      footer={
        <>
          New here?{' '}
          <Link to="/register" className="font-medium text-brand hover:text-brand-strong">
            Create an account
          </Link>
        </>
      }
    >
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
        <Field label="Password" htmlFor="password">
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className={inputClass}
          />
        </Field>
        <label className="flex items-center gap-2 text-sm text-muted">
          <input
            type="checkbox"
            checked={remember}
            onChange={(e) => setRemember(e.target.checked)}
            className="size-4 rounded border-line accent-brand"
          />
          Keep me signed in
        </label>
        <Button type="submit" className="w-full" loading={login.isPending}>
          Sign in
        </Button>
        <Button variant="secondary" className="w-full" onClick={useDemoAccount} disabled={login.isPending}>
          Try the demo account
        </Button>
      </form>
    </AuthShell>
  )
}

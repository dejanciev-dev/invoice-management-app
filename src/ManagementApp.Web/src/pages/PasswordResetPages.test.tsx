import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { ForgotPasswordPage } from './ForgotPasswordPage'
import { ResetPasswordPage } from './ResetPasswordPage'

const fetchMock = vi.fn<typeof fetch>()

beforeEach(() => vi.stubGlobal('fetch', fetchMock))
afterEach(() => {
  vi.unstubAllGlobals()
  fetchMock.mockReset()
})

function renderAt(url: string, page: React.ReactNode) {
  return {
    user: userEvent.setup(),
    ...render(
      <QueryClientProvider client={new QueryClient()}>
        <MemoryRouter initialEntries={[url]}>{page}</MemoryRouter>
      </QueryClientProvider>,
    ),
  }
}

const sentBody = () => JSON.parse(fetchMock.mock.calls[0]?.[1]?.body as string) as Record<string, string>

describe('ForgotPasswordPage', () => {
  it('asks for a link and gives the same answer whether or not the account exists', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))
    const { user } = renderAt('/forgot-password', <ForgotPasswordPage />)

    await user.type(screen.getByLabelText('Email'), ' jane@acme.example ')
    await user.click(screen.getByRole('button', { name: 'Send reset link' }))

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/auth/password/forgot')
    expect(sentBody()).toEqual({ email: 'jane@acme.example' })
    expect(await screen.findByText(/If an account exists for/)).toHaveTextContent('jane@acme.example')
  })
})

describe('ResetPasswordPage', () => {
  it('sends the email and code from the link with the new password', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))
    const { user } = renderAt('/reset-password?email=jane%40acme.example&code=abc-123', <ResetPasswordPage />)

    await user.type(screen.getByLabelText('New password'), 'N3w-Passw0rd!')
    await user.type(screen.getByLabelText('Confirm new password'), 'N3w-Passw0rd!')
    await user.click(screen.getByRole('button', { name: 'Change password' }))

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/auth/password/reset')
    expect(sentBody()).toEqual({ email: 'jane@acme.example', code: 'abc-123', newPassword: 'N3w-Passw0rd!' })
    expect(await screen.findByText(/signed out on every device/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/login')
  })

  it('does not send mismatched passwords', async () => {
    const { user } = renderAt('/reset-password?email=jane%40acme.example&code=abc-123', <ResetPasswordPage />)

    await user.type(screen.getByLabelText('New password'), 'N3w-Passw0rd!')
    await user.type(screen.getByLabelText('Confirm new password'), 'Other-Passw0rd!')
    await user.click(screen.getByRole('button', { name: 'Change password' }))

    expect(screen.getByText('Passwords do not match.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('shows why an expired link was refused', async () => {
    const problem = { status: 400, errors: { code: ['This link is invalid or has expired. Ask for a new one.'] } }
    fetchMock.mockResolvedValue(new Response(JSON.stringify(problem), { status: 400, headers: { 'Content-Type': 'application/problem+json' } }))
    const { user } = renderAt('/reset-password?email=jane%40acme.example&code=old', <ResetPasswordPage />)

    await user.type(screen.getByLabelText('New password'), 'N3w-Passw0rd!')
    await user.type(screen.getByLabelText('Confirm new password'), 'N3w-Passw0rd!')
    await user.click(screen.getByRole('button', { name: 'Change password' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('This link is invalid or has expired.')
    expect(screen.getByRole('link', { name: 'Ask for a new link' })).toHaveAttribute('href', '/forgot-password')
  })

  it('explains an incomplete link', () => {
    renderAt('/reset-password?email=jane%40acme.example', <ResetPasswordPage />)

    expect(screen.getByRole('alert')).toHaveTextContent('Open the link from the email again')
  })
})

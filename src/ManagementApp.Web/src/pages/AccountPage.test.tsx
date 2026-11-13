import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { currentUserKey } from '../api/auth'
import type { Session } from '../api/types'
import { AccountPage } from './AccountPage'

const session: Session = {
  userId: 'u1',
  email: 'jane@acme.example',
  displayName: 'Jane',
  isPlatformAdmin: false,
  impersonation: null,
  tenants: [],
  activeTenant: null,
}

const fetchMock = vi.fn<typeof fetch>()

beforeEach(() => {
  fetchMock.mockImplementation(async () => new Response(null, { status: 204 }))
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => {
  vi.unstubAllGlobals()
  fetchMock.mockReset()
})

function renderPage() {
  const queryClient = new QueryClient()
  queryClient.setQueryData(currentUserKey, session)
  return {
    user: userEvent.setup(),
    ...render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <AccountPage />
        </MemoryRouter>
      </QueryClientProvider>,
    ),
  }
}

const call = (url: string) => fetchMock.mock.calls.find(([input]) => input === url)

describe('AccountPage', () => {
  it('shows the sign-in email and saves the display name', async () => {
    const { user } = renderPage()

    expect(screen.getByLabelText('Email')).toHaveValue('jane@acme.example')
    await user.clear(screen.getByLabelText('Display name'))
    await user.type(screen.getByLabelText('Display name'), '  Jane Doe ')
    await user.click(screen.getByRole('button', { name: 'Save profile' }))

    const request = call('/api/account/profile')
    expect(request?.[1]?.method).toBe('PUT')
    expect(JSON.parse(request?.[1]?.body as string)).toEqual({ displayName: 'Jane Doe' })
    expect(await screen.findByText('Your profile has been saved.')).toBeInTheDocument()
  })

  it('changes the password with the current one', async () => {
    const { user } = renderPage()

    await user.type(screen.getByLabelText('Current password'), 'Old-Passw0rd!')
    await user.type(screen.getByLabelText('New password'), 'N3w-Passw0rd!')
    await user.type(screen.getByLabelText('Confirm new password'), 'N3w-Passw0rd!')
    await user.click(screen.getByRole('button', { name: 'Change password' }))

    expect(JSON.parse(call('/api/account/password')?.[1]?.body as string)).toEqual({
      currentPassword: 'Old-Passw0rd!',
      newPassword: 'N3w-Passw0rd!',
    })
    expect(await screen.findByText(/Other devices have been signed out/)).toBeInTheDocument()
    expect(screen.getByLabelText('Current password')).toHaveValue('')
  })

  it('does not send mismatched new passwords', async () => {
    const { user } = renderPage()

    await user.type(screen.getByLabelText('Current password'), 'Old-Passw0rd!')
    await user.type(screen.getByLabelText('New password'), 'N3w-Passw0rd!')
    await user.type(screen.getByLabelText('Confirm new password'), 'Typo-Passw0rd!')
    await user.click(screen.getByRole('button', { name: 'Change password' }))

    expect(screen.getByText('Passwords do not match.')).toBeInTheDocument()
    expect(call('/api/account/password')).toBeUndefined()
  })

  it('shows why the server refused a password change', async () => {
    const problem = { status: 400, errors: { currentPassword: ['The current password is incorrect.'] } }
    fetchMock.mockResolvedValue(new Response(JSON.stringify(problem), { status: 400, headers: { 'Content-Type': 'application/problem+json' } }))
    const { user } = renderPage()

    await user.type(screen.getByLabelText('Current password'), 'Wrong-Passw0rd!')
    await user.type(screen.getByLabelText('New password'), 'N3w-Passw0rd!')
    await user.type(screen.getByLabelText('Confirm new password'), 'N3w-Passw0rd!')
    await user.click(screen.getByRole('button', { name: 'Change password' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The current password is incorrect.')
  })
})

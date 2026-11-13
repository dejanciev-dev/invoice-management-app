import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { publicConfigKey } from '../api/auth'
import type { PublicConfig } from '../api/types'
import { LoginPage } from './LoginPage'

function renderLogin(config: PublicConfig) {
  const queryClient = new QueryClient()
  queryClient.setQueryData(publicConfigKey, config)
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('LoginPage', () => {
  it('hides the demo account unless the server enables it', () => {
    renderLogin({ demoLogin: false })

    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Try the demo account' })).not.toBeInTheDocument()
  })

  it('offers the demo account when the server enables it', () => {
    renderLogin({ demoLogin: true })

    expect(screen.getByRole('button', { name: 'Try the demo account' })).toBeInTheDocument()
  })
})

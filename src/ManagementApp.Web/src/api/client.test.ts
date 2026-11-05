import { apiRequest, getActiveTenantId, setActiveTenantId, withTenant } from './client'

describe('tenant selection', () => {
  const fetchMock = vi.fn()

  beforeEach(() => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ ok: true }), { headers: { 'Content-Type': 'application/json' } }))
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    setActiveTenantId(null)
    vi.unstubAllGlobals()
    fetchMock.mockReset()
  })

  it('sends the chosen company with every request', async () => {
    setActiveTenantId('tenant-a')

    await apiRequest('/invoices')

    const headers = fetchMock.mock.calls[0]?.[1]?.headers as Headers
    expect(headers.get('X-Tenant-Id')).toBe('tenant-a')
    expect(localStorage.getItem('activeTenantId')).toBe('tenant-a')
  })

  it('sends no company header until one is chosen', async () => {
    await apiRequest('/invoices')

    const headers = fetchMock.mock.calls[0]?.[1]?.headers as Headers
    expect(headers.has('X-Tenant-Id')).toBe(false)
    expect(getActiveTenantId()).toBeNull()
  })

  it('adds the company to plain links, which cannot send headers', () => {
    expect(withTenant('/api/invoices/export')).toBe('/api/invoices/export')

    setActiveTenantId('tenant-b')

    expect(withTenant('/api/invoices/export')).toBe('/api/invoices/export?tenantId=tenant-b')
    expect(withTenant('/api/tenant/logo?v=1')).toBe('/api/tenant/logo?v=1&tenantId=tenant-b')
  })

  it('returns nothing for empty responses such as 201 Created without a body', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 201 }))

    await expect(apiRequest('/tenant/users', { method: 'POST' })).resolves.toBeUndefined()
  })
})

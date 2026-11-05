import type { ProblemDetails } from './types'

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | undefined

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  /** Every validation message, flattened; useful when errors cannot be mapped to a field. */
  get messages(): string[] {
    const errors = this.problem?.errors
    return errors ? Object.values(errors).flat() : [this.message]
  }
}

const TENANT_STORAGE_KEY = 'activeTenantId'
let activeTenantId: string | null = readStoredTenant()

function readStoredTenant(): string | null {
  try {
    return localStorage.getItem(TENANT_STORAGE_KEY)
  } catch {
    return null
  }
}

/**
 * The company the user chose to work in. It is only a request: the server checks the user's membership on every call
 * and ignores or rejects a tenant the user does not belong to.
 */
export function getActiveTenantId(): string | null {
  return activeTenantId
}

export function setActiveTenantId(tenantId: string | null) {
  activeTenantId = tenantId
  try {
    if (tenantId) localStorage.setItem(TENANT_STORAGE_KEY, tenantId)
    else localStorage.removeItem(TENANT_STORAGE_KEY)
  } catch {
    // Storage can be unavailable (private mode); the choice then lasts until the page is reloaded.
  }
}

/** For plain links and images, which cannot send headers: adds the active tenant to the query string. */
export function withTenant(url: string): string {
  if (!activeTenantId) return url
  return `${url}${url.includes('?') ? '&' : '?'}tenantId=${encodeURIComponent(activeTenantId)}`
}

export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body !== undefined && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  if (activeTenantId && !headers.has('X-Tenant-Id')) {
    headers.set('X-Tenant-Id', activeTenantId)
  }

  const response = await fetch(`/api${path}`, { ...init, headers, credentials: 'same-origin' })

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
  }

  if (response.status === 204 || response.headers.get('Content-Length') === '0') {
    return undefined as T
  }

  // Some responses (e.g. 201 Created with only a Location header) have no body and no Content-Length.
  const text = await response.text()
  if (text.length === 0) return undefined as T
  const contentType = response.headers.get('Content-Type') ?? ''
  return (contentType.includes('json') ? JSON.parse(text) : text) as T
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return undefined
  }
}

export const json = (body: unknown): Pick<RequestInit, 'body'> => ({ body: JSON.stringify(body) })

/** Builds a query string from the defined values, e.g. `?search=x&page=2`. */
export function query(params: Record<string, string | number | undefined | null>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  }
  return search.size > 0 ? `?${search}` : ''
}

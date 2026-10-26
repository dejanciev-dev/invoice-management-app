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

export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body !== undefined && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(`/api${path}`, { ...init, headers, credentials: 'same-origin' })

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
  }

  if (response.status === 204 || response.headers.get('Content-Length') === '0') {
    return undefined as T
  }

  const contentType = response.headers.get('Content-Type') ?? ''
  return (contentType.includes('json') ? response.json() : response.text()) as Promise<T>
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return undefined
  }
}

export const json = (body: unknown): Pick<RequestInit, 'body'> => ({ body: JSON.stringify(body) })

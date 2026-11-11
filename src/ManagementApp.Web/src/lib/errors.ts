import { ApiError } from '../api/client'

/** A readable message for a failed request, preferring the server's own validation messages. */
export const errorText = (error: unknown): string | null =>
  error instanceof ApiError ? error.messages.join(' ') : error ? 'Something went wrong. Please try again.' : null

/** The server's first validation message for one field (matched case-insensitively, e.g. `profile.name`). */
export function fieldError(error: unknown, path: string): string | undefined {
  if (!(error instanceof ApiError)) return undefined
  const errors = error.problem?.errors ?? {}
  const key = Object.keys(errors).find((k) => k.toLowerCase() === path.toLowerCase())
  return key ? errors[key]?.[0] : undefined
}

/** A message for a failed request that cannot be shown next to a field, or null when every error belongs to one. */
export const unmappedError = (error: unknown): string | null =>
  error instanceof ApiError ? (error.problem?.errors ? null : error.message) : error ? 'Something went wrong. Please try again.' : null

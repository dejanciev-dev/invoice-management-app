import { ApiError } from '../api/client'

/** A readable message for a failed request, preferring the server's own validation messages. */
export const errorText = (error: unknown): string | null =>
  error instanceof ApiError ? error.messages.join(' ') : error ? 'Something went wrong. Please try again.' : null

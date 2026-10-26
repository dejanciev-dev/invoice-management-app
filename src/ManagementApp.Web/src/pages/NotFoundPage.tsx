import { ButtonLink } from '../components/ui'

export function NotFoundPage({ message = "The page you're looking for doesn't exist." }: { message?: string }) {
  return (
    <div className="py-24 text-center">
      <p className="text-sm font-semibold text-brand">404</p>
      <h1 className="mt-2 text-2xl font-semibold tracking-tight">Not found</h1>
      <p className="mt-2 text-sm text-muted">{message}</p>
      <ButtonLink to="/" variant="secondary" className="mt-6">
        Back to invoices
      </ButtonLink>
    </div>
  )
}

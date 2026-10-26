import type { ReactNode } from 'react'
import { Logo } from './Layout'
import { Card } from './ui'

export function AuthShell({ title, subtitle, children, footer }: {
  title: string
  subtitle: string
  children: ReactNode
  footer: ReactNode
}) {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center px-4 py-12">
      <div className="mb-8">
        <Logo />
      </div>
      <Card className="w-full max-w-sm p-6 sm:p-8">
        <h1 className="text-xl font-semibold tracking-tight">{title}</h1>
        <p className="mt-1 mb-6 text-sm text-muted">{subtitle}</p>
        {children}
      </Card>
      <p className="mt-6 text-sm text-muted">{footer}</p>
    </div>
  )
}

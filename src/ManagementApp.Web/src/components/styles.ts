export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost'

const variants: Record<ButtonVariant, string> = {
  primary: 'bg-brand text-on-brand hover:bg-brand-strong shadow-sm',
  secondary: 'bg-surface text-ink border border-line hover:bg-subtle shadow-sm',
  danger: 'bg-red-600 text-white hover:bg-red-700 shadow-sm',
  ghost: 'text-muted hover:text-ink hover:bg-subtle',
}

const buttonBase =
  'inline-flex items-center justify-center gap-2 rounded-lg px-3.5 py-2 text-sm font-medium transition-colors ' +
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:opacity-60 disabled:pointer-events-none'

export const buttonClass = (variant: ButtonVariant = 'primary') => `${buttonBase} ${variants[variant]}`

export const inputClass =
  'block w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink shadow-xs placeholder:text-muted/70 ' +
  'focus:border-brand focus:outline-none focus:ring-2 focus:ring-brand/25 aria-invalid:border-red-500 aria-invalid:ring-red-500/20'

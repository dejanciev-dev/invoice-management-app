import { CircleAlert, Info, Lock } from 'lucide-react'
import { Link } from 'react-router'
import { useCurrentUser } from '../api/auth'
import { subscriptionNotice, type SubscriptionNotice } from '../lib/subscription'

const toneClass: Record<SubscriptionNotice['tone'], string> = {
  danger: 'border-red-200 bg-red-50 text-red-900 dark:border-red-900/60 dark:bg-red-950/40 dark:text-red-100',
  warning: 'border-amber-200 bg-amber-50 text-amber-950 dark:border-amber-900/60 dark:bg-amber-950/40 dark:text-amber-100',
  info: 'border-sky-200 bg-sky-50 text-sky-950 dark:border-sky-900/60 dark:bg-sky-950/40 dark:text-sky-100',
}

const toneIcon = { danger: Lock, warning: CircleAlert, info: Info }

/** Tells the company about a read-only subscription, an overdue payment or a trial that is about to end. */
export function SubscriptionBanner({ now = new Date() }: { now?: Date }) {
  const { data: session } = useCurrentUser()
  const subscription = session?.activeTenant?.subscription
  const notice = subscription ? subscriptionNotice(subscription, now) : null
  if (!notice) return null

  const Icon = toneIcon[notice.tone]
  return (
    <div role={notice.tone === 'info' ? 'status' : 'alert'} className={`border-b print:hidden ${toneClass[notice.tone]}`}>
      <div className="mx-auto flex max-w-6xl flex-col gap-1 px-4 py-2 text-sm sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <p className="flex items-start gap-2">
          <Icon className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span>{notice.message}</span>
        </p>
        <Link to="/plan" className="shrink-0 font-medium underline underline-offset-2">
          See your plan
        </Link>
      </div>
    </div>
  )
}

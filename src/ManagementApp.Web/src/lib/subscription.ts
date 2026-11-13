import type { Subscription } from '../api/types'

export interface SubscriptionNotice {
  tone: 'danger' | 'warning' | 'info'
  message: string
}

/** How many days before the end of a trial the company is reminded. */
export const TRIAL_REMINDER_DAYS = 3

const day = (iso: string) => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(iso))

/**
 * What the company needs to know about its subscription right now, if anything: read-only, an overdue payment, a
 * cancellation running out, or a trial about to end. The server decides the state; this only words it.
 */
export function subscriptionNotice(subscription: Subscription, now: Date): SubscriptionNotice | null {
  const { effectiveStatus: status } = subscription

  if (!subscription.canChangeData) {
    return {
      tone: 'danger',
      message: `${subscription.readOnlyReason ?? 'Your subscription has ended.'} You can still view and export everything.`,
    }
  }

  if (status === 'PastDue') {
    return {
      tone: 'warning',
      message: subscription.graceEndsAt
        ? `Your payment is overdue. You have full access until ${day(subscription.graceEndsAt)}; after that your data is read-only until it's paid.`
        : 'Your payment is overdue. Please pay it to keep full access.',
    }
  }

  if (status === 'Cancelled' && subscription.accessEndsAt) {
    return {
      tone: 'warning',
      message: `Your subscription is cancelled. You have full access until ${day(subscription.accessEndsAt)}; after that your data is read-only.`,
    }
  }

  if (status === 'Trialing' && subscription.trialEndsAt) {
    const daysLeft = Math.ceil((new Date(subscription.trialEndsAt).getTime() - now.getTime()) / 86_400_000)
    if (daysLeft <= TRIAL_REMINDER_DAYS) {
      const when = daysLeft <= 1 ? 'tomorrow' : `in ${daysLeft} days`
      return {
        tone: 'info',
        message: `Your trial ends ${when} (${day(subscription.trialEndsAt)}). Then you'll move to the Free plan, unless you choose a paid one.`,
      }
    }
  }

  return null
}

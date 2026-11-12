import type { PaymentMethod, Permission, SubscriptionStatus, TenantRole, TenantStatus } from '../api/types'

export const roles: TenantRole[] = ['TenantAdmin', 'Manager', 'Accountant', 'Employee']

export const roleLabels: Record<TenantRole, string> = {
  TenantAdmin: 'Admin',
  Manager: 'Manager',
  Accountant: 'Accountant',
  Employee: 'Employee',
}

export const roleDescriptions: Record<TenantRole, string> = {
  TenantAdmin: 'Full access, including users and settings',
  Manager: 'Manages invoices and sees reports',
  Accountant: 'Creates and edits invoices, exports and reports',
  Employee: 'Views and creates invoices; more can be granted',
}

export const permissionLabels: Record<Permission, string> = {
  InvoicesView: 'View invoices',
  InvoicesCreate: 'Create invoices',
  InvoicesEdit: 'Edit invoices and payments',
  InvoicesDelete: 'Delete invoices',
  InvoicesExport: 'Export invoices',
  ReportsView: 'View financial reports',
  UsersManage: 'Manage users',
  SettingsManage: 'Manage company settings',
  AuditLogView: 'View the activity log',
}

export const allPermissions = Object.keys(permissionLabels) as Permission[]

export const tenantStatusStyles: Record<TenantStatus, string> = {
  Active: 'bg-emerald-500/10 text-emerald-800 ring-emerald-500/25 dark:text-emerald-300',
  Inactive: 'bg-slate-500/10 text-slate-700 ring-slate-500/20 dark:text-slate-300',
  Suspended: 'bg-red-500/10 text-red-700 ring-red-500/25 dark:text-red-300',
}

export const subscriptionStatusLabels: Record<SubscriptionStatus, string> = {
  Trialing: 'Trial',
  Active: 'Active',
  PastDue: 'Past due',
  Cancelled: 'Cancelled',
  Expired: 'Expired',
}

const actionLabels: Record<string, string> = {
  'auth.login': 'Signed in',
  'auth.login_failed': 'Failed sign-in',
  'auth.logout': 'Signed out',
  'auth.signup': 'Signed up',
  'tenant.created': 'Company created',
  'tenant.updated': 'Company updated',
  'tenant.settings_updated': 'Settings changed',
  'tenant.status_changed': 'Company status changed',
  'tenant.subscription_changed': 'Subscription changed',
  'tenant.deleted': 'Company deleted',
  'tenant.logo_changed': 'Logo changed',
  'user.created': 'User created',
  'user.invited': 'User invited',
  'user.invitation_revoked': 'Invitation revoked',
  'user.invitation_accepted': 'Invitation accepted',
  'user.updated': 'User updated',
  'user.role_changed': 'Role changed',
  'user.permissions_changed': 'Permissions changed',
  'user.activated': 'User activated',
  'user.deactivated': 'User deactivated',
  'user.password_reset': 'Password reset',
  'user.removed': 'User removed',
  'platform.user_status_changed': 'Account enabled/disabled',
  'invoice.created': 'Invoice created',
  'invoice.updated': 'Invoice updated',
  'invoice.deleted': 'Invoice deleted',
  'invoice.issued': 'Invoice issued',
  'invoice.voided': 'Invoice voided',
  'invoice.payment_changed': 'Payment changed',
  'payment.recorded': 'Payment recorded',
  'payment.deleted': 'Payment deleted',
  'credit_note.issued': 'Credit note issued',
  'invoice.exported': 'Invoices exported',
  'impersonation.started': 'Impersonation started',
  'impersonation.ended': 'Impersonation ended',
  'impersonation.request': 'Action while impersonating',
  'notification.overdue_reminder': 'Overdue reminder sent',
  'subscription.expired': 'Subscription expired',
}

export const actionLabel = (action: string) => actionLabels[action] ?? action

export const auditActionFilters = [
  { value: '', label: 'All activity' },
  { value: 'invoice.', label: 'Invoices' },
  { value: 'payment.', label: 'Payments' },
  { value: 'credit_note.', label: 'Credit notes' },
  { value: 'user.', label: 'Users' },
  { value: 'tenant.', label: 'Company' },
  { value: 'auth.', label: 'Sign-ins' },
  { value: 'impersonation.', label: 'Impersonation' },
]

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—'
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso))
}

export const paymentMethodLabels: Record<PaymentMethod, string> = {
  BankTransfer: 'Bank transfer',
  Card: 'Card',
  Cash: 'Cash',
  Cheque: 'Cheque',
  Other: 'Other',
}

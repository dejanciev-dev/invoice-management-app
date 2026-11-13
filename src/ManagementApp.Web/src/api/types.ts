export type AdjustmentType = 'Flat' | 'Percentage'

export type EmailStatus = 'Pending' | 'Sent' | 'Failed'

/** An email in the company's outbox and what happened to it. */
export interface Email {
  id: number
  to: string
  subject: string
  status: EmailStatus
  attempts: number
  createdAt: string
  sentAt: string | null
  /** When a pending email will be (re)tried. */
  nextAttemptAt: string | null
  lastError: string | null
  relatedEntityType: string | null
  relatedEntityId: string | null
  /** File names of the attachments. */
  attachments: string[]
}

/** Drafts and void invoices show their state; issued invoices show their payment progress. */
export type InvoiceStatus = 'Draft' | 'Unpaid' | 'PartiallyPaid' | 'Paid' | 'Overdue' | 'Credited' | 'Void'

/** Drafts can be edited and deleted; issued invoices are final and can only be voided. */
export type InvoiceState = 'Draft' | 'Issued' | 'Void'

export type PaymentMethod = 'BankTransfer' | 'Card' | 'Cash' | 'Cheque' | 'Other'

export interface RecordPaymentRequest {
  amount: number
  /** The day the money was received (not in the future). */
  date: string
  method: PaymentMethod
  reference: string | null
  notes: string | null
}

export interface IssueCreditNoteRequest {
  /** Including tax; at most the balance due. */
  amount: number
  date: string
  reason: string
}

export interface CreditNote {
  id: number
  number: string
  date: string
  reason: string
  amount: number
  taxAmount: number
  netAmount: number
}

export interface Payment extends RecordPaymentRequest {
  id: number
  recordedAt: string
}

export interface InvoiceItem {
  id: number
  description: string
  quantity: number
  unitPrice: number
  amount: number
}

export interface Invoice {
  id: number
  /** Null while a draft: issuing assigns the next number of the company's sequence. */
  invoiceNumber: string | null
  from: string
  to: string
  issueDate: string
  dueDate: string | null
  paymentTerms: string | null
  notes: string | null
  currency: string
  discount: number
  discountType: AdjustmentType
  tax: number
  taxType: AdjustmentType
  /** The sum of `payments`. */
  amountPaid: number
  items: InvoiceItem[]
  subtotal: number
  discountAmount: number
  taxAmount: number
  total: number
  balanceDue: number
  status: InvoiceStatus
  state: InvoiceState
  issuedAt: string | null
  voidedAt: string | null
  voidReason: string | null
  /** The saved customer the invoice was made for, if any; `to` is the printed snapshot of their details. */
  customer: InvoiceCustomer | null
  /** Money received, oldest first; `amountPaid` is their sum. */
  payments: Payment[]
  /** The sum of `creditNotes`; already taken off `balanceDue`. */
  creditedAmount: number
  creditNotes: CreditNote[]
}

export interface InvoiceCustomer {
  id: number
  name: string
  /** Deleted customers are still named on their invoices, but have no page to link to. */
  isDeleted: boolean
}

export interface InvoiceSummary {
  id: number
  invoiceNumber: string | null
  to: string
  issueDate: string
  dueDate: string | null
  currency: string
  total: number
  balanceDue: number
  status: InvoiceStatus
}

export interface SaveCustomerRequest {
  name: string
  email: string | null
  phone: string | null
  /** The billing address, several lines. */
  address: string | null
  taxNumber: string | null
  /** Null means the company's default currency. */
  currency: string | null
  /** Null means the company's default payment terms. */
  paymentTermsDays: number | null
  /** Internal, never printed on an invoice. */
  notes: string | null
}

export interface Customer extends SaveCustomerRequest {
  id: number
  created: string
}

export interface SaveProductRequest {
  /** Also the description of the invoice line it is added as. */
  name: string
  /** What the price is per, e.g. "hour"; null for a single item. */
  unit: string | null
  /** In the company's default currency. */
  unitPrice: number
}

export interface Product extends SaveProductRequest {
  id: number
  created: string
}

export interface CurrencyInvoiceStats {
  currency: string
  invoiceCount: number
  billed: number
  openCount: number
  outstanding: number
  overdueCount: number
  overdue: number
}

export interface InvoiceStats {
  invoiceCount: number
  openCount: number
  overdueCount: number
  /** Drafts are not included in the other figures, which cover issued invoices only. */
  draftCount: number
  currencies: CurrencyInvoiceStats[]
}

export interface SaveInvoiceRequest {
  customerId: number | null
  from: string
  to: string
  issueDate: string
  dueDate: string | null
  paymentTerms: string | null
  notes: string | null
  currency: string
  discount: number
  discountType: AdjustmentType
  tax: number
  taxType: AdjustmentType
  items: { description: string; quantity: number; unitPrice: number }[]
}

// ---- Tenancy -------------------------------------------------------------------------------------------------------

export type TenantRole = 'Employee' | 'Accountant' | 'Manager' | 'TenantAdmin'

export type Permission =
  | 'InvoicesView'
  | 'InvoicesCreate'
  | 'InvoicesEdit'
  | 'InvoicesDelete'
  | 'InvoicesExport'
  | 'ReportsView'
  | 'UsersManage'
  | 'SettingsManage'
  | 'AuditLogView'

export type TenantStatus = 'Active' | 'Inactive' | 'Suspended'

export type SubscriptionStatus = 'Trialing' | 'Active' | 'PastDue' | 'Cancelled' | 'Expired'

export type BillingCycle = 'Monthly' | 'Yearly'

export type PlanFeature = 'CsvExport' | 'Reports' | 'AuditLog' | 'CustomBranding'

export interface Subscription {
  planCode: string
  planName: string
  status: SubscriptionStatus
  effectiveStatus: SubscriptionStatus
  billingCycle: BillingCycle
  trialEndsAt: string | null
  currentPeriodEndsAt: string | null
  maxUsers: number | null
  maxInvoicesPerMonth: number | null
  features: PlanFeature[]
  /** False when the company is read-only; reading and exporting always work. */
  canChangeData: boolean
  readOnlyReason: string | null
  /** For an overdue payment: when the company becomes read-only. */
  graceEndsAt: string | null
  /** For a cancelled subscription: when the company becomes read-only. */
  accessEndsAt: string | null
}

export interface TenantAccess {
  id: string
  name: string
  role: TenantRole
  status: TenantStatus
  isAvailable: boolean
}

export interface ActiveTenant {
  id: string
  name: string
  role: TenantRole
  permissions: Permission[]
  hasLogo: boolean
  brandColor: string | null
  defaultCurrency: string
  subscription: Subscription
}

/** Settings for the pages shown before sign-in. */
export interface PublicConfig {
  demoLogin: boolean
}

export interface Session {
  userId: string
  email: string
  displayName: string | null
  isPlatformAdmin: boolean
  impersonation: { sessionId: string; impersonatorEmail: string; startedAt: string; expiresAt: string } | null
  tenants: TenantAccess[]
  activeTenant: ActiveTenant | null
}

export interface TenantProfile {
  name: string
  registrationNumber: string | null
  taxNumber: string | null
  address: string | null
  country: string | null
  email: string | null
  phone: string | null
  defaultCurrency: string
  defaultLanguage: string
  timeZone: string
}

export interface TenantSettings {
  profile: TenantProfile
  invoice: {
    numberPrefix: string
    creditNotePrefix: string
    nextNumber: number
    numberPadding: number
    defaultPaymentTermsDays: number
    defaultTaxRate: number
    taxLabel: string
    defaultNotes: string | null
    footer: string | null
    brandColor: string | null
  }
  email: {
    senderName: string | null
    replyToEmail: string | null
    invoiceEmailSubject: string | null
    invoiceEmailBody: string | null
  }
  notifications: {
    overdueRemindersEnabled: boolean
    notificationEmail: string | null
    weeklySummaryEnabled: boolean
  }
}

export interface CurrentTenant {
  id: string
  status: TenantStatus
  hasLogo: boolean
  nextInvoiceNumber: string
  invoiceSenderDetails: string
  settings: TenantSettings
  subscription: Subscription
}

export interface Member {
  userId: string
  email: string
  displayName: string | null
  role: TenantRole
  permissions: Permission[]
  isActive: boolean
  accountIsActive: boolean
  joinedAt: string
  lastLoginAt: string | null
  accountManagedByTenant: boolean
}

export interface Invitation {
  id: number
  email: string
  role: TenantRole
  expiresAt: string
  createdAt: string
}

export interface CreatedInvitation {
  invitation: Invitation
  token: string
}

export interface InvitationPreview {
  tenantName: string
  email: string
  role: TenantRole
  accountExists: boolean
}

export interface AuditLog {
  id: number
  tenantId: string | null
  tenantName: string | null
  userId: string | null
  userEmail: string | null
  impersonatorUserId: string | null
  impersonatorEmail: string | null
  action: string
  entityType: string | null
  entityId: string | null
  timestamp: string
  details: string | null
  ipAddress: string | null
}

export interface Paged<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface CurrencyTotals {
  currency: string
  invoiceCount: number
  billed: number
  paid: number
  outstanding: number
  overdue: number
}

export interface FinancialSummary {
  totals: CurrencyTotals[]
  byStatus: { status: InvoiceStatus; count: number }[]
  monthly: { month: string; currency: string; billed: number; paid: number }[]
}

// ---- Platform admin ------------------------------------------------------------------------------------------------

export interface TenantListItem {
  id: string
  name: string
  email: string | null
  country: string | null
  status: TenantStatus
  planCode: string
  subscriptionStatus: SubscriptionStatus
  userCount: number
  invoiceCount: number
  created: string
}

export interface PlatformDashboard {
  totalTenants: number
  activeTenants: number
  inactiveTenants: number
  suspendedTenants: number
  totalUsers: number
  totalInvoices: number
  invoicesLast30Days: number
  tenantsCreatedLast30Days: number
  recentTenants: TenantListItem[]
  recentActivity: AuditLog[]
}

export interface TenantDetails {
  id: string
  status: TenantStatus
  statusReason: string | null
  statusChangedAt: string | null
  created: string
  hasLogo: boolean
  settings: TenantSettings
  subscription: Subscription
  stats: {
    userCount: number
    activeUserCount: number
    invoiceCount: number
    invoicesThisMonth: number
    totals: CurrencyTotals[]
    lastActivityAt: string | null
  }
}

export interface Plan {
  code: string
  name: string
  maxUsers: number | null
  maxInvoicesPerMonth: number | null
  features: PlanFeature[]
  monthlyPrice: number
  yearlyPrice: number
  currency: string
  /** The plan new companies start on. */
  isDefault: boolean
}

export interface PlatformUser {
  id: string
  email: string
  displayName: string | null
  isActive: boolean
  isPlatformAdmin: boolean
  createdAt: string
  lastLoginAt: string | null
  tenants: { tenantId: string; tenantName: string; role: TenantRole; isActive: boolean }[]
}

/** RFC 9457 problem details, as returned by the API for every error. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

export type SubscriptionEventSource = 'Manual' | 'System' | 'Provider'

/** One entry of a tenant's subscription history. */
export interface SubscriptionEvent {
  at: string
  source: SubscriptionEventSource
  actorEmail: string | null
  fromPlanCode: string | null
  fromStatus: SubscriptionStatus | null
  toPlanCode: string
  toStatus: SubscriptionStatus
  trialEndsAt: string | null
  currentPeriodEndsAt: string | null
  graceEndsAt: string | null
  maxUsers: number | null
  maxInvoicesPerMonth: number | null
  note: string | null
}

export interface Usage {
  /** Null means unlimited. */
  limit: number | null
  used: number
}

/** The company's plan with this month's usage, and the plans it could move to. */
export interface TenantPlan {
  subscription: Subscription
  invoices: Usage
  invoicesResetAt: string
  seats: Usage
  plans: Plan[]
}

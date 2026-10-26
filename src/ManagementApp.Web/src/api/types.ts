export type AdjustmentType = 'Flat' | 'Percentage'

export type InvoiceStatus = 'Unpaid' | 'PartiallyPaid' | 'Paid' | 'Overdue'

export interface InvoiceItem {
  id: number
  description: string
  quantity: number
  unitPrice: number
  amount: number
}

export interface Invoice {
  id: number
  invoiceNumber: string
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
  amountPaid: number
  items: InvoiceItem[]
  subtotal: number
  discountAmount: number
  taxAmount: number
  total: number
  balanceDue: number
  status: InvoiceStatus
}

export interface InvoiceSummary {
  id: number
  invoiceNumber: string
  to: string
  issueDate: string
  dueDate: string | null
  currency: string
  total: number
  balanceDue: number
  status: InvoiceStatus
}

export interface SaveInvoiceRequest {
  invoiceNumber: string
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
  amountPaid: number
  items: { description: string; quantity: number; unitPrice: number }[]
}

export interface UserInfo {
  email: string
  isEmailConfirmed: boolean
}

/** RFC 9457 problem details, as returned by the API for every error. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

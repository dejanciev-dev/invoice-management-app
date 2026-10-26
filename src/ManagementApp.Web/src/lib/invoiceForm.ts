import { z } from 'zod'
import type { Invoice, SaveInvoiceRequest } from '../api/types'
import { addDays, toIsoDate } from './format'

export const currencies = ['USD', 'EUR', 'GBP', 'CHF', 'CAD', 'AUD', 'JPY', 'SEK', 'MKD'] as const

const amount = (label: string) =>
  z.number({ error: `${label} is required` }).min(0, `${label} cannot be negative`)

const adjustmentType = z.enum(['Flat', 'Percentage'])

export const invoiceFormSchema = z
  .object({
    invoiceNumber: z.string().trim().min(1, 'Invoice number is required').max(50),
    currency: z.string().regex(/^[A-Z]{3}$/, 'Use a three-letter currency code'),
    issueDate: z.string().min(1, 'Issue date is required'),
    dueDate: z.string(),
    paymentTerms: z.string().max(200),
    from: z.string().trim().min(1, 'Your business details are required').max(1000),
    to: z.string().trim().min(1, 'Client details are required').max(1000),
    items: z
      .array(
        z.object({
          description: z.string().trim().min(1, 'Description is required').max(500),
          quantity: z.number({ error: 'Required' }).positive('Must be more than 0'),
          unitPrice: amount('Price'),
        }),
      )
      .min(1, 'Add at least one item'),
    discount: amount('Discount'),
    discountType: adjustmentType,
    tax: amount('Tax'),
    taxType: adjustmentType,
    amountPaid: amount('Amount paid'),
    notes: z.string().max(2000),
  })
  .refine((v) => !v.dueDate || v.dueDate >= v.issueDate, {
    path: ['dueDate'],
    message: 'Due date cannot be before the issue date',
  })
  .refine((v) => v.discountType === 'Flat' || v.discount <= 100, {
    path: ['discount'],
    message: 'A percentage cannot exceed 100',
  })
  .refine((v) => v.taxType === 'Flat' || v.tax <= 100, {
    path: ['tax'],
    message: 'A percentage cannot exceed 100',
  })

export type InvoiceFormValues = z.infer<typeof invoiceFormSchema>

export const emptyItem = { description: '', quantity: 1, unitPrice: 0 }

export function newInvoiceValues(today = toIsoDate(new Date())): InvoiceFormValues {
  return {
    invoiceNumber: '',
    currency: 'USD',
    issueDate: today,
    dueDate: addDays(today, 30),
    paymentTerms: 'Net 30',
    from: '',
    to: '',
    items: [emptyItem],
    discount: 0,
    discountType: 'Percentage',
    tax: 0,
    taxType: 'Percentage',
    amountPaid: 0,
    notes: '',
  }
}

export function invoiceToValues(invoice: Invoice): InvoiceFormValues {
  return {
    invoiceNumber: invoice.invoiceNumber,
    currency: invoice.currency,
    issueDate: invoice.issueDate,
    dueDate: invoice.dueDate ?? '',
    paymentTerms: invoice.paymentTerms ?? '',
    from: invoice.from,
    to: invoice.to,
    items: invoice.items.map(({ description, quantity, unitPrice }) => ({ description, quantity, unitPrice })),
    discount: invoice.discount,
    discountType: invoice.discountType,
    tax: invoice.tax,
    taxType: invoice.taxType,
    amountPaid: invoice.amountPaid,
    notes: invoice.notes ?? '',
  }
}

export function valuesToRequest(values: InvoiceFormValues): SaveInvoiceRequest {
  const optional = (value: string) => value.trim() || null
  return {
    ...values,
    dueDate: values.dueDate || null,
    paymentTerms: optional(values.paymentTerms),
    notes: optional(values.notes),
  }
}

/** Converts an API error path such as "items[0].unitPrice" into a form field path ("items.0.unitPrice"). */
export const toFieldPath = (apiPath: string) => apiPath.replace(/\[(\d+)\]/g, '.$1')

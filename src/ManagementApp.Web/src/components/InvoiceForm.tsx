import { zodResolver } from '@hookform/resolvers/zod'
import { Plus, Trash2 } from 'lucide-react'
import { useEffect, type ReactNode } from 'react'
import { useFieldArray, useForm, useWatch, type Control, type FieldPath } from 'react-hook-form'
import { ApiError } from '../api/client'
import { formatMoney } from '../lib/format'
import { currencies, emptyItem, invoiceFormSchema, toFieldPath, type InvoiceFormValues } from '../lib/invoiceForm'
import { calculateTotals } from '../lib/money'
import { inputClass } from './styles'
import { Alert, Button, Card, Field } from './ui'

interface InvoiceFormProps {
  defaultValues: InvoiceFormValues
  onSubmit: (values: InvoiceFormValues) => void
  onCancel: () => void
  submitLabel: string
  submitting?: boolean
  serverError?: unknown
}

export function InvoiceForm({ defaultValues, onSubmit, onCancel, submitLabel, submitting, serverError }: InvoiceFormProps) {
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<InvoiceFormValues>({ resolver: zodResolver(invoiceFormSchema), defaultValues })
  const items = useFieldArray({ control, name: 'items' })

  // Show the API's validation errors (e.g. a duplicate invoice number) next to the matching fields.
  const unmappedErrors = useServerErrors(serverError, setError)

  const number = { valueAsNumber: true } as const

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="grid gap-6 lg:grid-cols-[1fr_20rem]">
      <div className="space-y-6">
        {unmappedErrors.length > 0 && (
          <Alert>
            {unmappedErrors.map((message) => (
              <p key={message}>{message}</p>
            ))}
          </Alert>
        )}

        <Section title="Details">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Invoice number" htmlFor="invoiceNumber" error={errors.invoiceNumber?.message}>
              <input id="invoiceNumber" {...register('invoiceNumber')} aria-invalid={!!errors.invoiceNumber} placeholder="INV-2026-001" className={inputClass} />
            </Field>
            <Field label="Currency" htmlFor="currency" error={errors.currency?.message}>
              <select id="currency" {...register('currency')} className={inputClass}>
                {currencies.map((code) => (
                  <option key={code}>{code}</option>
                ))}
              </select>
            </Field>
            <Field label="Issue date" htmlFor="issueDate" error={errors.issueDate?.message}>
              <input id="issueDate" type="date" {...register('issueDate')} aria-invalid={!!errors.issueDate} className={inputClass} />
            </Field>
            <Field label="Due date" htmlFor="dueDate" error={errors.dueDate?.message}>
              <input id="dueDate" type="date" {...register('dueDate')} aria-invalid={!!errors.dueDate} className={inputClass} />
            </Field>
            <Field label="Payment terms" htmlFor="paymentTerms" error={errors.paymentTerms?.message} className="sm:col-span-2">
              <input id="paymentTerms" {...register('paymentTerms')} placeholder="Net 30" className={inputClass} />
            </Field>
          </div>
        </Section>

        <Section title="Parties">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="From" htmlFor="from" error={errors.from?.message}>
              <textarea id="from" rows={4} {...register('from')} aria-invalid={!!errors.from} placeholder={'Your business\nStreet address\nCity, Country'} className={inputClass} />
            </Field>
            <Field label="Bill to" htmlFor="to" error={errors.to?.message}>
              <textarea id="to" rows={4} {...register('to')} aria-invalid={!!errors.to} placeholder={'Client name\nStreet address\nCity, Country'} className={inputClass} />
            </Field>
          </div>
        </Section>

        <Section title="Items">
          <div className="space-y-3">
            <div className="hidden grid-cols-[1fr_6rem_8rem_7rem_2.5rem] gap-3 text-xs font-medium tracking-wide text-muted uppercase sm:grid">
              <span>Description</span>
              <span className="text-right">Qty</span>
              <span className="text-right">Unit price</span>
              <span className="text-right">Amount</span>
              <span />
            </div>
            {items.fields.map((field, index) => {
              const itemErrors = errors.items?.[index]
              return (
                <div key={field.id} className="grid grid-cols-2 gap-3 rounded-lg border border-line p-3 sm:grid-cols-[1fr_6rem_8rem_7rem_2.5rem] sm:items-start sm:border-0 sm:p-0">
                  <div className="col-span-2 sm:col-span-1">
                    <input aria-label={`Item ${index + 1} description`} {...register(`items.${index}.description`)} aria-invalid={!!itemErrors?.description} placeholder="Description" className={inputClass} />
                    <FieldError message={itemErrors?.description?.message} />
                  </div>
                  <div>
                    <input aria-label={`Item ${index + 1} quantity`} type="number" step="any" min="0" {...register(`items.${index}.quantity`, number)} aria-invalid={!!itemErrors?.quantity} className={`${inputClass} text-right`} />
                    <FieldError message={itemErrors?.quantity?.message} />
                  </div>
                  <div>
                    <input aria-label={`Item ${index + 1} unit price`} type="number" step="0.01" min="0" {...register(`items.${index}.unitPrice`, number)} aria-invalid={!!itemErrors?.unitPrice} className={`${inputClass} text-right`} />
                    <FieldError message={itemErrors?.unitPrice?.message} />
                  </div>
                  <LineAmount control={control} index={index} />
                  <Button variant="ghost" aria-label={`Remove item ${index + 1}`} onClick={() => items.remove(index)} disabled={items.fields.length === 1} className="justify-self-end px-2.5">
                    <Trash2 className="size-4" aria-hidden />
                  </Button>
                </div>
              )
            })}
            <FieldError message={errors.items?.root?.message ?? errors.items?.message} />
            <Button variant="secondary" onClick={() => items.append(emptyItem)}>
              <Plus className="size-4" aria-hidden />
              Add item
            </Button>
          </div>
        </Section>

        <Section title="Discount, tax and payments">
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Discount" htmlFor="discount" error={errors.discount?.message}>
              <div className="flex gap-2">
                <input id="discount" type="number" step="0.01" min="0" {...register('discount', number)} aria-invalid={!!errors.discount} className={inputClass} />
                <select aria-label="Discount type" {...register('discountType')} className={`${inputClass} w-20`}>
                  <option value="Percentage">%</option>
                  <option value="Flat">Flat</option>
                </select>
              </div>
            </Field>
            <Field label="Tax" htmlFor="tax" error={errors.tax?.message}>
              <div className="flex gap-2">
                <input id="tax" type="number" step="0.01" min="0" {...register('tax', number)} aria-invalid={!!errors.tax} className={inputClass} />
                <select aria-label="Tax type" {...register('taxType')} className={`${inputClass} w-20`}>
                  <option value="Percentage">%</option>
                  <option value="Flat">Flat</option>
                </select>
              </div>
            </Field>
            <Field label="Amount paid" htmlFor="amountPaid" error={errors.amountPaid?.message}>
              <input id="amountPaid" type="number" step="0.01" min="0" {...register('amountPaid', number)} aria-invalid={!!errors.amountPaid} className={inputClass} />
            </Field>
          </div>
        </Section>

        <Section title="Notes">
          <Field label="Notes for your client" htmlFor="notes" error={errors.notes?.message}>
            <textarea id="notes" rows={3} {...register('notes')} placeholder="Thank you for your business!" className={inputClass} />
          </Field>
        </Section>
      </div>

      <aside className="lg:sticky lg:top-24 lg:self-start">
        <Card className="p-5">
          <h2 className="text-sm font-semibold">Summary</h2>
          <Summary control={control} />
          <div className="mt-5 flex flex-col gap-2">
            <Button type="submit" loading={submitting}>
              {submitLabel}
            </Button>
            <Button variant="secondary" onClick={onCancel} disabled={submitting}>
              Cancel
            </Button>
          </div>
        </Card>
      </aside>
    </form>
  )
}

function useServerErrors(error: unknown, setError: ReturnType<typeof useForm<InvoiceFormValues>>['setError']): string[] {
  const problem = error instanceof ApiError ? error.problem : undefined
  const fieldErrors = problem?.errors

  useEffect(() => {
    for (const [path, messages] of Object.entries(fieldErrors ?? {})) {
      setError(toFieldPath(path) as FieldPath<InvoiceFormValues>, { type: 'server', message: messages[0] })
    }
  }, [fieldErrors, setError])

  if (!error) return []
  if (!fieldErrors) return ['Something went wrong while saving. Please try again.']
  return []
}

function Summary({ control }: { control: Control<InvoiceFormValues> }) {
  const values = useWatch({ control })
  const currency = values.currency ?? 'USD'
  const totals = calculateTotals({
    items: (values.items ?? []).map((i) => ({ quantity: i?.quantity ?? 0, unitPrice: i?.unitPrice ?? 0 })),
    discount: values.discount ?? 0,
    discountType: values.discountType ?? 'Percentage',
    tax: values.tax ?? 0,
    taxType: values.taxType ?? 'Percentage',
    amountPaid: values.amountPaid ?? 0,
  })
  const money = (amount: number) => formatMoney(amount, currency)

  return (
    <dl className="mt-4 space-y-2 text-sm" aria-label="Invoice totals">
      <SummaryRow label="Subtotal" value={money(totals.subtotal)} />
      <SummaryRow label="Discount" value={`−${money(totals.discountAmount)}`} />
      <SummaryRow label="Tax" value={money(totals.taxAmount)} />
      <SummaryRow label="Total" value={money(totals.total)} strong />
      <SummaryRow label="Paid" value={`−${money(values.amountPaid || 0)}`} />
      <SummaryRow label="Balance due" value={money(totals.balanceDue)} strong />
    </dl>
  )
}

function LineAmount({ control, index }: { control: Control<InvoiceFormValues>; index: number }) {
  const [quantity, unitPrice, currency] = useWatch({
    control,
    name: [`items.${index}.quantity`, `items.${index}.unitPrice`, 'currency'],
  })
  const { subtotal } = calculateTotals({
    items: [{ quantity, unitPrice }],
    discount: 0,
    discountType: 'Flat',
    tax: 0,
    taxType: 'Flat',
    amountPaid: 0,
  })

  return (
    <p className="self-center text-right text-sm tabular-nums sm:pt-2" aria-label={`Item ${index + 1} amount`}>
      {formatMoney(subtotal, currency)}
    </p>
  )
}

function SummaryRow({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className={`flex justify-between ${strong ? 'border-t border-line pt-2 font-semibold' : 'text-muted'}`}>
      <dt>{label}</dt>
      <dd className={`tabular-nums ${strong ? '' : 'text-ink'}`}>{value}</dd>
    </div>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card className="p-5 sm:p-6">
      <h2 className="mb-4 text-sm font-semibold">{title}</h2>
      {children}
    </Card>
  )
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null
  return (
    <p role="alert" className="mt-1 text-xs text-red-600 dark:text-red-400">
      {message}
    </p>
  )
}

import { useState, type FormEvent } from 'react'
import type { SaveCustomerRequest } from '../api/types'
import { fieldError, unmappedError } from '../lib/errors'
import { currencies } from '../lib/invoiceForm'
import { inputClass } from './styles'
import { Alert, Button, Card, Field } from './ui'

type TextKey = Exclude<keyof SaveCustomerRequest, 'paymentTermsDays'>

interface CustomerFormProps {
  defaultValues: SaveCustomerRequest
  onSubmit: (request: SaveCustomerRequest) => void
  onCancel: () => void
  submitLabel: string
  submitting?: boolean
  serverError?: unknown
  /** The company's own defaults, shown for the fields left empty. */
  companyCurrency?: string
  companyPaymentTermsDays?: number
}

export function CustomerForm({
  defaultValues,
  onSubmit,
  onCancel,
  submitLabel,
  submitting,
  serverError,
  companyCurrency,
  companyPaymentTermsDays,
}: CustomerFormProps) {
  const [values, setValues] = useState(defaultValues)
  const set = <K extends keyof SaveCustomerRequest>(key: K, value: SaveCustomerRequest[K]) =>
    setValues((current) => ({ ...current, [key]: value }))

  const submit = (event: FormEvent) => {
    event.preventDefault()
    // Blank optional fields are sent as null: "not set" rather than an empty value.
    const blankToNull = (value: string | null) => (value?.trim() ? value.trim() : null)
    onSubmit({
      name: values.name.trim(),
      email: blankToNull(values.email),
      phone: blankToNull(values.phone),
      address: blankToNull(values.address),
      taxNumber: blankToNull(values.taxNumber),
      currency: values.currency || null,
      paymentTermsDays: values.paymentTermsDays,
      notes: blankToNull(values.notes),
    })
  }

  const text = (key: TextKey, label: string, props: { type?: string; required?: boolean; autoComplete?: string } = {}) => (
    <Field label={label} htmlFor={`customer-${key}`} error={fieldError(serverError, key)}>
      <input
        id={`customer-${key}`}
        type={props.type ?? 'text'}
        required={props.required}
        autoComplete={props.autoComplete ?? 'off'}
        value={values[key] ?? ''}
        onChange={(e) => set(key, e.target.value)}
        className={inputClass}
      />
    </Field>
  )

  const area = (key: TextKey, label: string, hint?: string) => (
    <Field label={label} htmlFor={`customer-${key}`} hint={hint} error={fieldError(serverError, key)} className="sm:col-span-2">
      <textarea
        id={`customer-${key}`}
        rows={3}
        value={values[key] ?? ''}
        onChange={(e) => set(key, e.target.value)}
        className={inputClass}
      />
    </Field>
  )

  const currencyOptions: string[] =
    values.currency && !currencies.includes(values.currency as (typeof currencies)[number]) ? [values.currency, ...currencies] : [...currencies]
  const unmapped = unmappedError(serverError)

  return (
    <form onSubmit={submit} className="space-y-6">
      {unmapped && <Alert>{unmapped}</Alert>}

      <Card className="p-6">
        <h2 className="text-base font-semibold">Contact</h2>
        <div className="mt-4 grid gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">{text('name', 'Name', { required: true, autoComplete: 'organization' })}</div>
          {text('email', 'Email', { type: 'email' })}
          {text('phone', 'Phone', { type: 'tel' })}
          {area('address', 'Billing address', 'As it should appear on invoices.')}
          {text('taxNumber', 'Tax number')}
        </div>
      </Card>

      <Card className="p-6">
        <h2 className="text-base font-semibold">Invoicing defaults</h2>
        <p className="mt-1 text-xs text-muted">Leave empty to use your company's defaults.</p>
        <div className="mt-4 grid gap-4 sm:grid-cols-2">
          <Field label="Currency" htmlFor="customer-currency" error={fieldError(serverError, 'currency')}>
            <select
              id="customer-currency"
              value={values.currency ?? ''}
              onChange={(e) => set('currency', e.target.value || null)}
              className={inputClass}
            >
              <option value="">Company default{companyCurrency ? ` (${companyCurrency})` : ''}</option>
              {currencyOptions.map((code) => (
                <option key={code} value={code}>
                  {code}
                </option>
              ))}
            </select>
          </Field>
          <Field
            label="Payment terms (days)"
            htmlFor="customer-paymentTermsDays"
            error={fieldError(serverError, 'paymentTermsDays')}
          >
            <input
              id="customer-paymentTermsDays"
              type="number"
              inputMode="numeric"
              min={0}
              max={365}
              step={1}
              placeholder={companyPaymentTermsDays !== undefined ? `Company default (${companyPaymentTermsDays})` : 'Company default'}
              value={values.paymentTermsDays ?? ''}
              onChange={(e) => set('paymentTermsDays', e.target.value === '' ? null : Number(e.target.value))}
              className={inputClass}
            />
          </Field>
          {area('notes', 'Internal notes', 'Only visible to your team, never printed on invoices.')}
        </div>
      </Card>

      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" loading={submitting}>
          {submitLabel}
        </Button>
      </div>
    </form>
  )
}

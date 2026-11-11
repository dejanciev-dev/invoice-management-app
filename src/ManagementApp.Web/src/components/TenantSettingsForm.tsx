import { useState, type FormEvent, type ReactNode } from 'react'
import { ApiError } from '../api/client'
import type { TenantSettings } from '../api/types'
import { fieldError } from '../lib/errors'
import { currencies } from '../lib/invoiceForm'
import { inputClass } from './styles'
import { Alert, Button, Card, Field, Notice } from './ui'

interface TenantSettingsFormProps {
  initial: TenantSettings
  onSave: (settings: TenantSettings) => void
  saving: boolean
  error: unknown
  saved: boolean
  /** Extra sections rendered between the built-in ones, e.g. the logo upload. */
  branding?: ReactNode
}

type Section = keyof TenantSettings

/** Company profile, invoicing, localisation, email and notification settings, in one form. */
export function TenantSettingsForm({ initial, onSave, saving, error, saved, branding }: TenantSettingsFormProps) {
  const [settings, setSettings] = useState(initial)

  const set = <S extends Section, K extends keyof TenantSettings[S]>(section: S, key: K, value: TenantSettings[S][K]) =>
    setSettings((current) => ({ ...current, [section]: { ...current[section], [key]: value } }))

  const text = <S extends Section>(section: S, key: keyof TenantSettings[S] & string, label: string, props: { type?: string; hint?: string; placeholder?: string } = {}) => {
    const id = `${section}-${key}`
    const value = settings[section][key] as unknown as string | null
    return (
      <Field label={label} htmlFor={id} hint={props.hint} error={fieldError(error, `${section}.${key}`)}>
        <input
          id={id}
          type={props.type ?? 'text'}
          value={value ?? ''}
          placeholder={props.placeholder}
          onChange={(e) => set(section, key, (props.type === 'number' ? Number(e.target.value) : e.target.value) as TenantSettings[S][typeof key])}
          className={inputClass}
        />
      </Field>
    )
  }

  const area = <S extends Section>(section: S, key: keyof TenantSettings[S] & string, label: string, hint?: string) => {
    const id = `${section}-${key}`
    return (
      <Field label={label} htmlFor={id} hint={hint} error={fieldError(error, `${section}.${key}`)}>
        <textarea
          id={id}
          rows={3}
          value={(settings[section][key] as unknown as string | null) ?? ''}
          onChange={(e) => set(section, key, e.target.value as TenantSettings[S][typeof key])}
          className={inputClass}
        />
      </Field>
    )
  }

  const check = <S extends Section>(section: S, key: keyof TenantSettings[S] & string, label: string) => (
    <label className="flex items-center gap-2 text-sm">
      <input
        type="checkbox"
        checked={settings[section][key] as unknown as boolean}
        onChange={(e) => set(section, key, e.target.checked as TenantSettings[S][typeof key])}
        className="size-4 accent-brand"
      />
      {label}
    </label>
  )

  const submit = (event: FormEvent) => {
    event.preventDefault()
    onSave(settings)
  }

  const currencyOptions = currencies.includes(settings.profile.defaultCurrency as (typeof currencies)[number])
    ? currencies
    : [settings.profile.defaultCurrency, ...currencies]
  const unmapped = error instanceof ApiError && !error.problem?.errors ? error.message : error && !(error instanceof ApiError) ? 'Something went wrong.' : null

  return (
    <form onSubmit={submit} noValidate className="space-y-6">
      {saved && <Notice>Settings saved.</Notice>}
      {unmapped && <Alert>{unmapped}</Alert>}
      {error instanceof ApiError && error.problem?.errors && <Alert>Please correct the highlighted fields.</Alert>}

      <SettingsSection title="Company profile" description="Shown on your invoices.">
        {text('profile', 'name', 'Company name')}
        {text('profile', 'registrationNumber', 'Registration number')}
        {text('profile', 'taxNumber', 'Tax / VAT number')}
        {text('profile', 'email', 'Email', { type: 'email' })}
        {text('profile', 'phone', 'Phone')}
        {text('profile', 'country', 'Country', { hint: 'Two-letter code, e.g. GB or US.' })}
        <div className="sm:col-span-2">{area('profile', 'address', 'Address')}</div>
      </SettingsSection>

      <SettingsSection title="Currency and localization">
        <Field label="Default currency" htmlFor="profile-defaultCurrency" error={fieldError(error, 'profile.defaultCurrency')}>
          <select id="profile-defaultCurrency" value={settings.profile.defaultCurrency} onChange={(e) => set('profile', 'defaultCurrency', e.target.value)} className={inputClass}>
            {currencyOptions.map((code) => (
              <option key={code}>{code}</option>
            ))}
          </select>
        </Field>
        {text('profile', 'defaultLanguage', 'Language', { hint: 'e.g. en, de or fr-CH' })}
        {text('profile', 'timeZone', 'Time zone', { hint: 'e.g. Europe/London or America/New_York' })}
      </SettingsSection>

      <SettingsSection title="Invoice numbering and defaults">
        {text('invoice', 'numberPrefix', 'Number prefix', { placeholder: 'INV-' })}
        {text('invoice', 'nextNumber', 'Next number', { type: 'number' })}
        {text('invoice', 'numberPadding', 'Digits', { type: 'number', hint: 'The number is zero-padded to this length.' })}
        {text('invoice', 'defaultPaymentTermsDays', 'Payment terms (days)', { type: 'number' })}
        {text('invoice', 'taxLabel', 'Tax name', { placeholder: 'VAT' })}
        {text('invoice', 'defaultTaxRate', 'Default tax rate (%)', { type: 'number' })}
        <div className="sm:col-span-2">{area('invoice', 'defaultNotes', 'Default notes on new invoices')}</div>
        <div className="sm:col-span-2">{area('invoice', 'footer', 'Invoice footer', 'Printed at the bottom of every invoice, e.g. bank details.')}</div>
      </SettingsSection>

      <SettingsSection title="Branding">
        <Field label="Brand colour" htmlFor="invoice-brandColor" error={fieldError(error, 'invoice.brandColor')} hint="Used on your invoices.">
          <div className="flex gap-2">
            <input
              id="invoice-brandColor"
              value={settings.invoice.brandColor ?? ''}
              placeholder="#4f46e5"
              onChange={(e) => set('invoice', 'brandColor', e.target.value || null)}
              className={inputClass}
            />
            <input
              type="color"
              aria-label="Pick brand colour"
              value={/^#[0-9a-f]{6}$/i.test(settings.invoice.brandColor ?? '') ? settings.invoice.brandColor! : '#4f46e5'}
              onChange={(e) => set('invoice', 'brandColor', e.target.value)}
              className="h-9 w-12 cursor-pointer rounded border border-line bg-surface"
            />
          </div>
        </Field>
        {branding}
      </SettingsSection>

      <SettingsSection title="Email" description="Used when invoices and notifications are emailed on your behalf.">
        {text('email', 'senderName', 'Sender name')}
        {text('email', 'replyToEmail', 'Reply-to address', { type: 'email' })}
        <div className="sm:col-span-2">{text('email', 'invoiceEmailSubject', 'Invoice email subject')}</div>
        <div className="sm:col-span-2">{area('email', 'invoiceEmailBody', 'Invoice email message')}</div>
      </SettingsSection>

      <SettingsSection title="Notifications">
        {text('notifications', 'notificationEmail', 'Send notifications to', { type: 'email', hint: 'Defaults to the company email.' })}
        <div className="space-y-2 self-end pb-2">
          {check('notifications', 'overdueRemindersEnabled', 'Daily digest of overdue invoices')}
          {check('notifications', 'weeklySummaryEnabled', 'Weekly summary')}
        </div>
      </SettingsSection>

      <div className="flex justify-end">
        <Button type="submit" loading={saving}>
          Save settings
        </Button>
      </div>
    </form>
  )
}

function SettingsSection({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return (
    <Card className="p-5 sm:p-6">
      <h2 className="text-sm font-semibold">{title}</h2>
      {description && <p className="mt-1 text-xs text-muted">{description}</p>}
      <div className="mt-4 grid gap-4 sm:grid-cols-2">{children}</div>
    </Card>
  )
}

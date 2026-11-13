import { ImageUp, Trash2 } from 'lucide-react'
import { useRef, useState } from 'react'
import { Link } from 'react-router'
import { ApiError } from '../api/client'
import { tenantLogoUrl, useCurrentTenant, useUpdateTenantSettings, useUploadLogo } from '../api/tenant'
import { TenantSettingsForm } from '../components/TenantSettingsForm'
import { Alert, Badge, Button, Card, Field, PageHeader, Spinner } from '../components/ui'
import { formatDateTime, subscriptionStatusLabels } from '../lib/labels'

export function SettingsPage() {
  const tenant = useCurrentTenant()
  const update = useUpdateTenantSettings()

  if (tenant.isPending) return <Spinner label="Loading settings" />
  if (tenant.isError) return <Alert>Could not load the company settings.</Alert>

  const { subscription } = tenant.data

  return (
    <>
      <PageHeader title="Company settings" description="Your company profile, invoice configuration and preferences." />

      <Card className="mb-6 flex flex-col gap-2 p-5 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-sm font-semibold">
            {subscription.planName} plan <Badge>{subscriptionStatusLabels[subscription.effectiveStatus]}</Badge>
          </p>
          <p className="mt-1 text-xs text-muted">
            {subscription.maxUsers ?? 'Unlimited'} users · {subscription.maxInvoicesPerMonth ?? 'Unlimited'} invoices per month
            {subscription.trialEndsAt && subscription.status === 'Trialing' && <> · trial ends {formatDateTime(subscription.trialEndsAt)}</>}
            {subscription.currentPeriodEndsAt && <> · renews {formatDateTime(subscription.currentPeriodEndsAt)}</>}
          </p>
        </div>
        <Link to="/plan" className="text-sm font-medium text-brand hover:text-brand-strong">
          Plan &amp; usage
        </Link>
      </Card>

      <TenantSettingsForm
        key={tenant.dataUpdatedAt}
        initial={tenant.data.settings}
        onSave={(settings) => update.mutate(settings)}
        saving={update.isPending}
        error={update.error}
        saved={update.isSuccess}
        branding={<LogoUpload hasLogo={tenant.data.hasLogo} />}
      />
    </>
  )
}

function LogoUpload({ hasLogo }: { hasLogo: boolean }) {
  const upload = useUploadLogo()
  const input = useRef<HTMLInputElement>(null)
  const [version, setVersion] = useState(() => Date.now())

  const change = (file: File | null) =>
    upload.mutate(file, {
      onSuccess: () => setVersion(Date.now()),
      onSettled: () => {
        if (input.current) input.current.value = ''
      },
    })

  return (
    <Field label="Logo" htmlFor="logo" hint="PNG, JPEG or WebP, up to 512 KB." error={upload.error instanceof ApiError ? upload.error.messages.join(' ') : undefined}>
      <div className="flex items-center gap-3">
        <div className="grid size-14 place-items-center overflow-hidden rounded-lg border border-line bg-subtle">
          {hasLogo ? <img src={tenantLogoUrl(version)} alt="Company logo" className="max-h-full max-w-full object-contain" /> : <ImageUp className="size-5 text-muted" aria-hidden />}
        </div>
        <input ref={input} id="logo" type="file" accept="image/png,image/jpeg,image/webp" className="sr-only" onChange={(e) => change(e.target.files?.[0] ?? null)} />
        <Button variant="secondary" loading={upload.isPending} onClick={() => input.current?.click()}>
          {hasLogo ? 'Replace' : 'Upload'}
        </Button>
        {hasLogo && (
          <Button variant="ghost" aria-label="Remove logo" onClick={() => change(null)}>
            <Trash2 className="size-4" aria-hidden />
          </Button>
        )}
      </div>
    </Field>
  )
}

import { Link } from 'react-router'
import { usePlatformDashboard } from '../../api/admin'
import { AuditLogTable } from '../../components/AuditLogTable'
import { Alert, ButtonLink, Card, PageHeader, Spinner, StatTile, tableClass, tdClass, theadClass, thClass } from '../../components/ui'
import { formatDateTime } from '../../lib/labels'
import { TenantStatusBadge } from './shared'

export function AdminDashboardPage() {
  const dashboard = usePlatformDashboard()

  if (dashboard.isPending) return <Spinner label="Loading dashboard" />
  if (dashboard.isError) return <Alert>Could not load the dashboard.</Alert>

  const d = dashboard.data

  return (
    <>
      <PageHeader
        title="Platform overview"
        description="Every tenant, user and invoice on the platform."
        actions={<ButtonLink to="/admin/tenants">Manage tenants</ButtonLink>}
      />

      <dl className="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile label="Tenants" value={d.totalTenants} detail={`${d.tenantsCreatedLast30Days} new in 30 days`} />
        <StatTile label="Active / inactive / suspended" value={`${d.activeTenants} / ${d.inactiveTenants} / ${d.suspendedTenants}`} />
        <StatTile label="Users" value={d.totalUsers} />
        <StatTile label="Invoices" value={d.totalInvoices} detail={`${d.invoicesLast30Days} in the last 30 days`} />
      </dl>

      <Card className="mb-6 overflow-hidden">
        <h2 className="border-b border-line px-4 py-3 text-sm font-semibold">Newest tenants</h2>
        <div className="overflow-x-auto">
          <table className={tableClass}>
            <thead className={theadClass}>
              <tr>
                <th scope="col" className={thClass}>Tenant</th>
                <th scope="col" className={thClass}>Status</th>
                <th scope="col" className={`${thClass} text-right`}>Users</th>
                <th scope="col" className={`${thClass} text-right`}>Invoices</th>
                <th scope="col" className={`${thClass} hidden sm:table-cell`}>Created</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {d.recentTenants.map((t) => (
                <tr key={t.id}>
                  <td className={tdClass}>
                    <Link to={`/admin/tenants/${t.id}`} className="font-medium hover:text-brand">
                      {t.name}
                    </Link>
                  </td>
                  <td className={tdClass}>
                    <TenantStatusBadge status={t.status} />
                  </td>
                  <td className={`${tdClass} text-right tabular-nums`}>{t.userCount}</td>
                  <td className={`${tdClass} text-right tabular-nums`}>{t.invoiceCount}</td>
                  <td className={`${tdClass} hidden text-muted sm:table-cell`}>{formatDateTime(t.created)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <h2 className="mb-3 text-sm font-semibold">Recent activity</h2>
      <AuditLogTable
        data={{ items: d.recentActivity, total: d.recentActivity.length, page: 1, pageSize: d.recentActivity.length || 1 }}
        isPending={false}
        isError={false}
        action=""
        onActionChange={() => {}}
        onPageChange={() => {}}
        showTenant
      />
      <p className="mt-3 text-sm">
        <Link to="/admin/activity" className="font-medium text-brand">
          See all activity →
        </Link>
      </p>
    </>
  )
}

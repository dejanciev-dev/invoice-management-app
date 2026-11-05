import { Search } from 'lucide-react'
import { useDeferredValue, useState } from 'react'
import { Link } from 'react-router'
import { usePlatformUsers, useSetUserStatus } from '../../api/admin'
import { useSession } from '../../api/auth'
import { inputClass } from '../../components/styles'
import { Alert, Badge, Button, Card, EmptyRow, PageHeader, Pager, Spinner, tableClass, tdClass, theadClass, thClass } from '../../components/ui'
import { formatDateTime, roleLabels } from '../../lib/labels'
import { errorText } from '../../lib/errors'
import { ImpersonateButton } from './shared'

export function AdminUsersPage() {
  const session = useSession()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const deferredSearch = useDeferredValue(search.trim())
  const users = usePlatformUsers({ search: deferredSearch || undefined, page })
  const setStatus = useSetUserStatus()

  return (
    <>
      <PageHeader title="Users" description="Every account on the platform and the companies it belongs to." />
      {setStatus.error && <div className="mb-4"><Alert>{errorText(setStatus.error)}</Alert></div>}
      <Card className="overflow-hidden">
        <div className="border-b border-line p-4">
          <div className="relative sm:w-80">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
            <input
              type="search"
              aria-label="Search users"
              placeholder="Search email or name"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value)
                setPage(1)
              }}
              className={`${inputClass} pl-9`}
            />
          </div>
        </div>
        {users.isPending ? (
          <Spinner label="Loading users" />
        ) : users.isError ? (
          <div className="p-4">
            <Alert>Could not load users.</Alert>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className={tableClass}>
                <thead className={theadClass}>
                  <tr>
                    <th scope="col" className={thClass}>User</th>
                    <th scope="col" className={thClass}>Companies</th>
                    <th scope="col" className={`${thClass} hidden md:table-cell`}>Last sign-in</th>
                    <th scope="col" className={`${thClass} text-right`}>Account</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {users.data.items.length === 0 && <EmptyRow colSpan={4}>No users found.</EmptyRow>}
                  {users.data.items.map((u) => (
                    <tr key={u.id} className={u.isActive ? '' : 'opacity-60'}>
                      <td className={tdClass}>
                        <span className="font-medium">{u.displayName ?? u.email}</span>
                        {u.displayName && <span className="block text-xs text-muted">{u.email}</span>}
                        {u.isPlatformAdmin && <Badge className="mt-1 bg-amber-400/15 text-amber-800 ring-amber-500/30 dark:text-amber-300">Platform admin</Badge>}
                      </td>
                      <td className={tdClass}>
                        {u.tenants.length === 0 && <span className="text-muted">—</span>}
                        <ul className="space-y-1">
                          {u.tenants.map((t) => (
                            <li key={t.tenantId} className="flex flex-wrap items-center gap-2">
                              <Link to={`/admin/tenants/${t.tenantId}`} className="hover:text-brand">
                                {t.tenantName}
                              </Link>
                              <span className="text-xs text-muted">
                                {roleLabels[t.role]}
                                {!t.isActive && ' · deactivated'}
                              </span>
                              {u.isActive && t.isActive && !u.isPlatformAdmin && (
                                <ImpersonateButton userId={u.id} tenantId={t.tenantId} label={`${u.email} in ${t.tenantName}`} />
                              )}
                            </li>
                          ))}
                        </ul>
                      </td>
                      <td className={`${tdClass} hidden text-muted md:table-cell`}>{formatDateTime(u.lastLoginAt)}</td>
                      <td className={`${tdClass} text-right`}>
                        {u.id !== session.userId && (
                          <Button variant="ghost" onClick={() => setStatus.mutate({ userId: u.id, isActive: !u.isActive })}>
                            {u.isActive ? 'Disable' : 'Enable'}
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pager page={users.data.page} pageSize={users.data.pageSize} total={users.data.total} onChange={setPage} />
          </>
        )}
      </Card>
    </>
  )
}

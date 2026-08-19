import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Skeleton } from '@/components/ui/Skeleton'
import { getAccessToken } from '@/lib/api'
import { useSiteSettings } from '@/lib/cms'
import {
  DataTable,
  Hint,
  PageHeader,
  Panel,
  Pill,
  StatCard,
  TextAreaField,
  TextField,
  Toggle,
} from '../components/ui'
import { Drawer } from '../components/overlays'
import { describeErrorText, formatInr, istToday } from '../lib/format'
import {
  useCorporateAccounts,
  useCorporateUsage,
  useRetireCorporate,
  useSaveCorporate,
  type CorporateAccountRow,
} from '../lib/module4-api'

/**
 * Corporate memberships (Module 4.6). Two things live here: the agreements themselves, and
 * the usage report HR asks for at renewal.
 *
 * The usage report leads with how many enrolled employees actually turn up, because that is
 * the number a company renews on — a seat nobody uses is a seat they cut next year.
 */
export function CorporatePage() {
  const { data: accounts, isLoading } = useCorporateAccounts()
  const [editing, setEditing] = useState<CorporateAccountRow | 'new' | null>(null)
  const [usageFor, setUsageFor] = useState<CorporateAccountRow | null>(null)

  const live = (accounts ?? []).filter((account) => account.status === 'Live')
  const seats = (accounts ?? []).reduce((total, account) => total + account.seatsUsed, 0)

  return (
    <>
      <PageHeader
        eyebrow="Partnerships"
        title="Corporate accounts"
        lead="Company agreements employees enrol into themselves with a code, and the usage export their HR team asks for."
        actions={
          <Button size="sm" icon="plus" onClick={() => setEditing('new')}>
            New account
          </Button>
        }
      >
        <div className="grid gap-3 sm:grid-cols-3">
          <StatCard label="Live agreements" value={String(live.length)} sub="Inside their dates and active" />
          <StatCard label="Employees enrolled" value={String(seats)} sub="Across every account" tone="accent" />
          <StatCard
            label="Accounts on file"
            value={String(accounts?.length ?? 0)}
            sub="Including scheduled and retired"
          />
        </div>
      </PageHeader>

      <Panel padded={false}>
        <DataTable
          rows={accounts ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          onRowClick={(row) => setUsageFor(row)}
          emptyHeadline="No corporate agreements yet"
          emptyBody="Add one and hand the code to the company's HR contact — their people enrol themselves."
          emptyAction={
            <Button size="sm" icon="plus" onClick={() => setEditing('new')}>
              New account
            </Button>
          }
          columns={[
            {
              key: 'company',
              header: 'Company',
              cell: (row) => (
                <div className="min-w-0">
                  <p className="truncate font-medium">{row.companyName}</p>
                  <p className="truncate text-[0.75rem] text-smoke">
                    {row.hrContactName} · {row.hrContactEmail}
                  </p>
                </div>
              ),
            },
            {
              key: 'code',
              header: 'Code',
              width: '9rem',
              cell: (row) => <code className="text-[0.8125rem] tracking-wide">{row.code}</code>,
            },
            {
              key: 'benefit',
              header: 'Benefit',
              width: '10rem',
              cell: (row) => (
                <span className="text-[0.8125rem]">
                  {row.discountPercent}% off
                  {row.waiveAdmissionFee && <span className="text-smoke"> · no joining fee</span>}
                </span>
              ),
            },
            {
              key: 'seats',
              header: 'Seats',
              width: '8rem',
              align: 'right',
              cell: (row) => (
                <span className="tabular-nums text-[0.8125rem]">
                  {row.seatsUsed}
                  {row.seatCap != null ? ` / ${row.seatCap}` : ' / ∞'}
                </span>
              ),
            },
            {
              key: 'window',
              header: 'Runs',
              width: '11rem',
              cell: (row) => (
                <span className="text-[0.8125rem] text-smoke">
                  {row.validFrom} → {row.validTo}
                </span>
              ),
            },
            {
              key: 'status',
              header: 'Status',
              width: '7rem',
              cell: (row) => (
                <Pill
                  tone={
                    row.status === 'Live'
                      ? 'success'
                      : row.status === 'Full' || row.status === 'Scheduled'
                        ? 'warn'
                        : 'neutral'
                  }
                >
                  {row.status}
                </Pill>
              ),
            },
            {
              key: 'edit',
              header: '',
              width: '5rem',
              cell: (row) => (
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={(event) => {
                    event.stopPropagation()
                    setEditing(row)
                  }}
                >
                  Edit
                </Button>
              ),
            },
          ]}
        />
      </Panel>

      <AccountDrawer account={editing} onClose={() => setEditing(null)} />
      <UsageDrawer account={usageFor} onClose={() => setUsageFor(null)} />
    </>
  )
}

/* ---------------------------------------------------------------- editor */

function AccountDrawer({
  account,
  onClose,
}: {
  account: CorporateAccountRow | 'new' | null
  onClose: () => void
}) {
  const isNew = account === 'new'
  const row = account === 'new' || account === null ? null : account
  const { data: settings } = useSiteSettings()
  const save = useSaveCorporate()
  const retire = useRetireCorporate()
  const [error, setError] = useState<string | null>(null)

  const [form, setForm] = useState(() => defaults(row))
  const [seeded, setSeeded] = useState<number | 'new' | null>(null)

  // Re-seed when a different account is opened, without a useEffect racing the render.
  const key = isNew ? 'new' : (row?.id ?? null)
  if (account !== null && seeded !== key) {
    setSeeded(key)
    setForm(defaults(row))
  }

  const submit = () => {
    setError(null)
    save.mutate(
      { id: row?.id, ...form },
      { onSuccess: onClose, onError: (err) => setError(describeErrorText(err)) },
    )
  }

  return (
    <Drawer
      open={account != null}
      onClose={onClose}
      title={isNew ? 'New corporate account' : (row?.companyName ?? 'Account')}
      description="The code is what employees type. Everything else is the benefit it unlocks."
      footer={
        <>
          {row && row.isActive && (
            <Button
              variant="ghost"
              onClick={() => retire.mutate(row.id, { onSuccess: onClose })}
              disabled={retire.isPending}
            >
              Retire
            </Button>
          )}
          <Button icon="check" onClick={submit} disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save'}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label="Company name"
            required
            value={form.companyName}
            onChange={(event) => setForm({ ...form, companyName: event.target.value })}
          />
          <TextField
            label="Code"
            required
            value={form.code}
            onChange={(event) => setForm({ ...form, code: event.target.value.toUpperCase() })}
            hint={isNew ? 'Uppercase, no spaces. Employees type this.' : 'Changing this strands anyone already holding it.'}
          />
        </div>

        <TextField
          label="Work email domain"
          value={form.domain}
          onChange={(event) => setForm({ ...form, domain: event.target.value })}
          hint="Optional. When set, enrolment checks the employee's work address against it."
          placeholder="acme.in"
        />

        <div className="grid gap-4 sm:grid-cols-3">
          <TextField
            label="HR contact"
            required
            value={form.hrContactName}
            onChange={(event) => setForm({ ...form, hrContactName: event.target.value })}
          />
          <TextField
            label="HR email"
            type="email"
            required
            value={form.hrContactEmail}
            onChange={(event) => setForm({ ...form, hrContactEmail: event.target.value })}
          />
          <TextField
            label="HR phone"
            value={form.hrContactPhone}
            onChange={(event) => setForm({ ...form, hrContactPhone: event.target.value })}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-3">
          <TextField
            label="Discount"
            type="number"
            min={0}
            max={75}
            value={form.discountPercent}
            onChange={(event) => setForm({ ...form, discountPercent: Number(event.target.value) })}
            hint="Percent off the branch price"
          />
          <TextField
            label="Seat cap"
            type="number"
            min={0}
            value={form.seatCap}
            onChange={(event) => setForm({ ...form, seatCap: event.target.value })}
            hint="Blank for unlimited"
          />
          <TextField
            label="Branch scope"
            value={form.branchScope}
            onChange={(event) => setForm({ ...form, branchScope: event.target.value })}
            hint={`Branch ids, comma-separated. Blank = every branch. (${(settings?.branches ?? [])
              .map((branch) => `${branch.id}=${branch.name.replace('FORGE ', '')}`)
              .join(', ')})`}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label="Runs from"
            type="date"
            value={form.validFrom}
            onChange={(event) => setForm({ ...form, validFrom: event.target.value })}
          />
          <TextField
            label="Runs to"
            type="date"
            value={form.validTo}
            onChange={(event) => setForm({ ...form, validTo: event.target.value })}
          />
        </div>

        <Toggle
          label="Waive the joining fee"
          checked={form.waiveAdmissionFee}
          onChange={(value) => setForm({ ...form, waiveAdmissionFee: value })}
        />
        <Toggle
          label="Accepting enrolments"
          checked={form.isActive}
          onChange={(value) => setForm({ ...form, isActive: value })}
        />

        <TextAreaField
          label="Internal note"
          rows={2}
          value={form.notes}
          onChange={(event) => setForm({ ...form, notes: event.target.value })}
        />

        {error && (
          <p className="rounded-[var(--radius-input)] border border-[var(--accent-hot)]/40 p-3 text-[0.8125rem] text-[var(--accent-hot)]" role="alert">
            {error}
          </p>
        )}
      </div>
    </Drawer>
  )
}

function defaults(row: CorporateAccountRow | null) {
  return {
    companyName: row?.companyName ?? '',
    code: row?.code ?? '',
    domain: row?.domain ?? '',
    hrContactName: row?.hrContactName ?? '',
    hrContactEmail: row?.hrContactEmail ?? '',
    hrContactPhone: row?.hrContactPhone ?? '',
    discountPercent: row?.discountPercent ?? 15,
    waiveAdmissionFee: row?.waiveAdmissionFee ?? true,
    seatCap: row?.seatCap != null ? String(row.seatCap) : '',
    branchScope: row?.branchScope ?? '',
    validFrom: row?.validFrom ?? istToday(),
    validTo: row?.validTo ?? istToday(365),
    isActive: row?.isActive ?? true,
    notes: row?.notes ?? '',
  }
}

/* ---------------------------------------------------------------- usage */

function UsageDrawer({ account, onClose }: { account: CorporateAccountRow | null; onClose: () => void }) {
  const [from, setFrom] = useState(istToday(-89))
  const [to, setTo] = useState(istToday())
  const { data, isLoading } = useCorporateUsage(account?.id ?? null, from, to)

  /**
   * The CSV comes back as an authenticated file download. The access token lives in memory
   * only, so a plain link would 401 — fetch it with the header, then hand the blob to the
   * browser. Nothing is written to disk on our side.
   */
  const download = async () => {
    if (!account) return
    const response = await fetch(
      `/api/admin/corporate/${account.id}/usage.csv?from=${from}&to=${to}`,
      { headers: { Authorization: `Bearer ${getAccessToken() ?? ''}` }, credentials: 'include' },
    )
    if (!response.ok) return
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `${account.code}-usage-${from}-${to}.csv`
    anchor.click()
    URL.revokeObjectURL(url)
  }

  return (
    <Drawer
      open={account != null}
      onClose={onClose}
      width="xl"
      title={account ? `${account.companyName} — usage` : 'Usage'}
      description="Who is enrolled, and whether they actually train. This is the report HR renews on."
      footer={
        <Button icon="arrow-up-right" onClick={download}>
          Export CSV for HR
        </Button>
      }
    >
      <div className="mb-5 grid gap-4 sm:grid-cols-2">
        <TextField label="From" type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
        <TextField label="To" type="date" value={to} onChange={(event) => setTo(event.target.value)} />
      </div>

      {isLoading && <Skeleton className="h-48 w-full" />}

      {data && (
        <>
          <div className="mb-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
            <StatCard label="Enrolled" value={String(data.seatsUsed)} sub="Active seats" />
            <StatCard label="Actually training" value={String(data.activeUsers)} sub="Visited in this window" tone="accent" />
            <StatCard label="Never visited" value={String(data.neverVisited)} sub="Enrolled, no check-in" tone="warn" />
            <StatCard label="Invoiced" value={formatInr(data.totalInvoiced)} sub="In this window" />
          </div>

          {data.neverVisited > 0 && (
            <Hint icon="sparkles">
              {data.neverVisited} enrolled {data.neverVisited === 1 ? 'employee has' : 'employees have'} not been in
              during this window. Worth flagging to {account?.hrContactName.split(' ')[0]} before the renewal
              conversation, not after it.
            </Hint>
          )}

          <div className="mt-5">
            <DataTable
              rows={data.rows}
              rowKey={(row) => row.memberId}
              emptyHeadline="Nobody has enrolled yet"
              emptyBody="Hand the code to the HR contact — employees enrol themselves from the member portal."
              columns={[
                {
                  key: 'name',
                  header: 'Employee',
                  cell: (row) => (
                    <div className="min-w-0">
                      <p className="truncate font-medium">{row.name}</p>
                      <p className="truncate text-[0.75rem] text-smoke">
                        {row.employeeId ?? row.memberCode} · {row.branch.replace('FORGE ', '')}
                      </p>
                    </div>
                  ),
                },
                { key: 'plan', header: 'Plan', cell: (row) => row.plan ?? <span className="text-smoke">—</span> },
                {
                  key: 'visits',
                  header: 'Visits',
                  align: 'right',
                  width: '6rem',
                  cell: (row) => (
                    <span className={row.visits === 0 ? 'text-[var(--accent)] tabular-nums' : 'tabular-nums'}>
                      {row.visits}
                    </span>
                  ),
                },
                {
                  key: 'classes',
                  header: 'Classes',
                  align: 'right',
                  width: '6rem',
                  cell: (row) => <span className="tabular-nums">{row.classesAttended}</span>,
                },
                {
                  key: 'last',
                  header: 'Last visit',
                  width: '8rem',
                  cell: (row) => (
                    <span className="text-[0.8125rem] text-smoke">{row.lastVisitOn ?? 'never'}</span>
                  ),
                },
                {
                  key: 'status',
                  header: '',
                  width: '6rem',
                  cell: (row) =>
                    row.isActive ? (
                      <Pill tone="success">Active</Pill>
                    ) : (
                      <Pill tone="neutral">Ended</Pill>
                    ),
                },
              ]}
            />
          </div>
        </>
      )}
    </Drawer>
  )
}

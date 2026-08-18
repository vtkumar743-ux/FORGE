import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { useSiteSettings } from '@/lib/cms'
import {
  memberExportUrl,
  useMemberMutations,
  useMembers,
  type MemberQuery,
} from '../lib/admin-api'
import { describeErrorText, formatInr, formatIsoDate } from '../lib/format'
import { genderNames, memberStatusNames, type MemberListRow } from '../lib/types'
import { Drawer, useToast } from '../components/overlays'
import {
  Avatar,
  DataTable,
  FilterChip,
  InlineError,
  PageHeader,
  Pagination,
  Panel,
  Pill,
  RiskPill,
  SelectField,
  StatusPill,
  TextAreaField,
  TextField,
  Toggle,
} from '../components/ui'

/**
 * The member book. Filters are in the URL so a view the owner uses daily — "Whitefield,
 * expiring soon" — is a bookmark, and bulk actions operate on the current selection
 * rather than the whole filter, which is the difference between a tool and a trap.
 */
export function MembersPage() {
  const [params, setParams] = useSearchParams()
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const mutations = useMemberMutations()

  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [createOpen, setCreateOpen] = useState(false)
  const [importOpen, setImportOpen] = useState(false)

  const query = useMemo<MemberQuery>(
    () => ({
      q: params.get('q') ?? undefined,
      branchId: params.get('branchId') ? Number(params.get('branchId')) : undefined,
      status: params.get('status') ? Number(params.get('status')) : undefined,
      churn: params.get('churn') ? Number(params.get('churn')) : undefined,
      expiringSoon: params.get('expiringSoon') === '1' || undefined,
      hasDues: params.get('hasDues') === '1' || undefined,
      tag: params.get('tag') ?? undefined,
      sort: params.get('sort') ?? 'recent',
      page: Number(params.get('page') ?? 1),
      pageSize: 25,
    }),
    [params],
  )

  const { data, isLoading, isFetching } = useMembers(query)

  function setParam(key: string, value: string | undefined) {
    const next = new URLSearchParams(params)
    if (value === undefined || value === '') next.delete(key)
    else next.set(key, value)
    // Any filter change resets to page one; staying on page 7 of a new filter is a dead end.
    if (key !== 'page') next.delete('page')
    setParams(next, { replace: true })
    setSelected(new Set())
  }

  function toggle(id: number) {
    setSelected((current) => {
      const next = new Set(current)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  async function bulk(action: string, extra: Record<string, unknown>) {
    try {
      const result = await mutations.bulk.mutateAsync({
        memberIds: [...selected],
        action,
        ...extra,
      })
      toast.success(`${(result as { updated: number }).updated} member(s) updated`)
      setSelected(new Set())
    } catch (error) {
      toast.error('Bulk action failed', describeErrorText(error))
    }
  }

  const rows = data?.items ?? []
  const allSelected = rows.length > 0 && rows.every((row) => selected.has(row.id))

  return (
    <>
      <PageHeader
        eyebrow="People"
        title="Members"
        lead={data ? `${data.total.toLocaleString('en-IN')} on the book.` : undefined}
        actions={
          <>
            <Button variant="ghost" size="sm" icon="arrow-up-right" onClick={() => setImportOpen(true)}>
              Import CSV
            </Button>
            <a
              href={memberExportUrl({ branchId: query.branchId, status: query.status })}
              className="inline-flex h-9 items-center gap-2 rounded-full border border-[var(--hairline-strong)] px-4 text-[0.8125rem] font-medium text-bone transition-colors hover:border-[var(--accent-line)] hover:text-accent"
            >
              <Icon name="share" size={15} />
              Export
            </a>
            <Button size="sm" icon="plus" onClick={() => setCreateOpen(true)}>
              New member
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <TextField
              placeholder="Name, code, phone or email"
              defaultValue={query.q ?? ''}
              onChange={(event) => setParam('q', event.target.value || undefined)}
              className="min-w-[16rem] flex-1"
              aria-label="Search members"
            />
            <SelectField
              value={String(query.sort)}
              onChange={(event) => setParam('sort', event.target.value)}
              aria-label="Sort"
              className="w-44"
            >
              <option value="recent">Newest first</option>
              <option value="name">Name A–Z</option>
              <option value="code">Member code</option>
              <option value="lastVisit">Last visit</option>
              <option value="churn">Churn score</option>
              <option value="joined">Joined date</option>
            </SelectField>
          </div>

          <div className="flex flex-wrap gap-2">
            <FilterChip active={!query.branchId} onClick={() => setParam('branchId', undefined)}>
              All branches
            </FilterChip>
            {(settings?.branches ?? []).map((branch) => (
              <FilterChip
                key={branch.id}
                active={query.branchId === branch.id}
                onClick={() => setParam('branchId', String(branch.id))}
              >
                {branch.name.replace('FORGE ', '')}
              </FilterChip>
            ))}
            <span className="mx-1 w-px self-stretch bg-[var(--hairline)]" />
            {memberStatusNames.map((name, index) => (
              <FilterChip
                key={name}
                active={query.status === index}
                onClick={() => setParam('status', query.status === index ? undefined : String(index))}
              >
                {name}
              </FilterChip>
            ))}
            <span className="mx-1 w-px self-stretch bg-[var(--hairline)]" />
            <FilterChip
              active={query.expiringSoon === true}
              onClick={() => setParam('expiringSoon', query.expiringSoon ? undefined : '1')}
            >
              Expiring in 7d
            </FilterChip>
            <FilterChip
              active={query.hasDues === true}
              onClick={() => setParam('hasDues', query.hasDues ? undefined : '1')}
            >
              Has dues
            </FilterChip>
            <FilterChip
              active={query.churn === 3}
              onClick={() => setParam('churn', query.churn === 3 ? undefined : '3')}
            >
              Red risk
            </FilterChip>
          </div>
        </div>
      </PageHeader>

      {selected.size > 0 && (
        <div className="mb-4 flex flex-wrap items-center gap-3 rounded-[var(--radius-card)] border border-[var(--accent-line)] bg-[var(--accent-soft)] px-4 py-3">
          <span className="text-[0.875rem] font-medium">{selected.size} selected</span>
          <div className="flex-1" />
          <Button
            variant="outline"
            size="sm"
            onClick={() => void bulk('addTag', { tag: window.prompt('Tag to add?') ?? '' })}
          >
            Add tag
          </Button>
          <Button variant="outline" size="sm" onClick={() => void bulk('setStatus', { status: 3 })}>
            Freeze
          </Button>
          <Button variant="outline" size="sm" onClick={() => void bulk('setStatus', { status: 2 })}>
            Mark active
          </Button>
          <Button variant="ghost" size="sm" onClick={() => setSelected(new Set())}>
            Clear
          </Button>
        </div>
      )}

      <Panel padded={false} className={isFetching ? 'opacity-70 transition-opacity' : undefined}>
        <DataTable
          rows={rows}
          rowKey={(row) => row.id}
          loading={isLoading}
          emptyHeadline="No members match those filters"
          emptyBody="Widen the branch or status filters, or clear the search box."
          columns={[
            {
              key: 'select',
              width: '2.5rem',
              header: (
                <input
                  type="checkbox"
                  checked={allSelected}
                  onChange={() =>
                    setSelected(allSelected ? new Set() : new Set(rows.map((row) => row.id)))
                  }
                  aria-label="Select all on this page"
                  className="size-3.5 accent-[var(--accent)]"
                />
              ),
              cell: (row) => (
                <input
                  type="checkbox"
                  checked={selected.has(row.id)}
                  onChange={() => toggle(row.id)}
                  onClick={(event) => event.stopPropagation()}
                  aria-label={`Select ${row.fullName}`}
                  className="size-3.5 accent-[var(--accent)]"
                />
              ),
            },
            {
              key: 'member',
              header: 'Member',
              cell: (row) => (
                <Link to={`/admin/members/${row.id}`} className="flex items-center gap-3">
                  <Avatar src={row.photoUrl} name={row.fullName} size={34} />
                  <div className="min-w-0">
                    <p className="truncate font-medium">{row.fullName}</p>
                    <p className="numeric truncate text-[0.75rem] text-smoke">
                      {row.memberCode} · {row.phone}
                    </p>
                  </div>
                </Link>
              ),
            },
            {
              key: 'branch',
              header: 'Branch',
              cell: (row) => <span className="text-smoke">{row.branchName.replace('FORGE ', '')}</span>,
            },
            { key: 'status', header: 'Status', cell: (row) => <StatusPill status={memberStatusNames[row.status] ?? '—'} /> },
            {
              key: 'plan',
              header: 'Membership',
              cell: (row) =>
                row.planName ? (
                  <div>
                    <p className="truncate text-[0.8125rem]">{row.planName}</p>
                    <p
                      className={
                        (row.daysLeft ?? 99) <= 7
                          ? 'numeric text-[0.75rem] text-accent-hot'
                          : 'numeric text-[0.75rem] text-smoke'
                      }
                    >
                      {row.daysLeft !== null && row.daysLeft !== undefined && row.daysLeft >= 0
                        ? `${row.daysLeft}d left`
                        : formatIsoDate(row.membershipEndsOn)}
                    </p>
                  </div>
                ) : (
                  <span className="text-smoke">—</span>
                ),
            },
            {
              key: 'dues',
              header: 'Dues',
              align: 'right',
              cell: (row) =>
                row.duesOutstanding > 0 ? (
                  <span className="numeric text-accent-hot">{formatInr(row.duesOutstanding)}</span>
                ) : (
                  <span className="text-smoke">—</span>
                ),
            },
            {
              key: 'visit',
              header: 'Last visit',
              align: 'right',
              cell: (row) => (
                <span className="numeric text-[0.8125rem] text-smoke">
                  {row.lastVisitOn ? formatIsoDate(row.lastVisitOn) : 'never'}
                </span>
              ),
            },
            {
              key: 'streak',
              header: 'Streak',
              align: 'right',
              cell: (row) =>
                row.currentStreakDays > 0 ? (
                  <Pill tone="accent" icon="flame">
                    {row.currentStreakDays}
                  </Pill>
                ) : (
                  <span className="text-smoke">—</span>
                ),
            },
            { key: 'risk', header: 'Risk', align: 'right', cell: (row) => <RiskPill band={row.churnRisk} /> },
          ]}
        />
        {data && (
          <Pagination
            page={data.page}
            pageCount={data.pageCount}
            total={data.total}
            pageSize={data.pageSize}
            onPage={(page) => setParam('page', String(page))}
          />
        )}
      </Panel>

      <MemberFormDrawer open={createOpen} onClose={() => setCreateOpen(false)} />
      <ImportDrawer open={importOpen} onClose={() => setImportOpen(false)} />
    </>
  )
}

/* ---------------------------------------------------------------- create / edit */

export function MemberFormDrawer({
  open,
  onClose,
  member,
}: {
  open: boolean
  onClose: () => void
  member?: MemberListRow & { profile?: Record<string, unknown> }
}) {
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const mutations = useMemberMutations()
  const [error, setError] = useState<string | null>(null)

  const [form, setForm] = useState<Record<string, unknown>>(() => ({
    fullName: member?.fullName ?? '',
    phone: member?.phone ?? '',
    email: member?.email ?? '',
    homeBranchId: member?.branchId ?? 0,
    status: member?.status ?? 0,
    gender: 0,
    consentMarketing: true,
    ...(member?.profile ?? {}),
  }))

  function set(key: string, value: unknown) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  async function submit() {
    setError(null)
    const body = {
      ...form,
      homeBranchId: Number(form.homeBranchId),
      status: Number(form.status),
      gender: Number(form.gender),
      heightCm: form.heightCm ? Number(form.heightCm) : undefined,
      startWeightKg: form.startWeightKg ? Number(form.startWeightKg) : undefined,
    }

    try {
      if (member) await mutations.update.mutateAsync({ id: member.id, body })
      else await mutations.create.mutateAsync(body)
      toast.success(member ? 'Member updated' : 'Member added')
      onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  const branches = settings?.branches ?? []

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={member ? `Edit ${member.fullName}` : 'New member'}
      description={
        member
          ? 'Changes here also update the login attached to this person.'
          : 'Creates the member and a portal login. Leave the password blank to force a reset on first sign-in.'
      }
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button size="sm" icon="check" onClick={() => void submit()} loading={mutations.create.isPending || mutations.update.isPending}>
            {member ? 'Save changes' : 'Create member'}
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {error && <InlineError>{error}</InlineError>}

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label="Full name"
            required
            value={String(form.fullName ?? '')}
            onChange={(event) => set('fullName', event.target.value)}
          />
          <TextField
            label="Mobile"
            required
            inputMode="tel"
            addon="+91"
            value={String(form.phone ?? '')}
            onChange={(event) => set('phone', event.target.value)}
          />
          <TextField
            label="Email"
            type="email"
            hint="Optional. A placeholder address is generated when blank."
            value={String(form.email ?? '')}
            onChange={(event) => set('email', event.target.value)}
          />
          <SelectField
            label="Home branch"
            required
            value={String(form.homeBranchId ?? '')}
            onChange={(event) => set('homeBranchId', event.target.value)}
          >
            <option value="">Choose a branch</option>
            {branches.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </SelectField>
          <SelectField label="Status" value={String(form.status ?? 0)} onChange={(event) => set('status', event.target.value)}>
            {memberStatusNames.map((name, index) => (
              <option key={name} value={index}>
                {name}
              </option>
            ))}
          </SelectField>
          <SelectField label="Gender" value={String(form.gender ?? 0)} onChange={(event) => set('gender', event.target.value)}>
            {genderNames.map((name, index) => (
              <option key={name} value={index}>
                {name}
              </option>
            ))}
          </SelectField>
          <TextField
            label="Date of birth"
            type="date"
            value={String(form.dateOfBirth ?? '')}
            onChange={(event) => set('dateOfBirth', event.target.value)}
          />
          <TextField
            label="Primary goal"
            placeholder="Fat loss, strength, first 5K…"
            value={String(form.primaryGoal ?? '')}
            onChange={(event) => set('primaryGoal', event.target.value)}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField label="City" value={String(form.city ?? '')} onChange={(event) => set('city', event.target.value)} />
          <TextField label="Pincode" value={String(form.pincode ?? '')} onChange={(event) => set('pincode', event.target.value)} />
          <TextField
            label="Emergency contact"
            value={String(form.emergencyContactName ?? '')}
            onChange={(event) => set('emergencyContactName', event.target.value)}
          />
          <TextField
            label="Emergency number"
            value={String(form.emergencyContactPhone ?? '')}
            onChange={(event) => set('emergencyContactPhone', event.target.value)}
          />
          <TextField
            label="Height (cm)"
            type="number"
            value={String(form.heightCm ?? '')}
            onChange={(event) => set('heightCm', event.target.value)}
          />
          <TextField
            label="Starting weight (kg)"
            type="number"
            value={String(form.startWeightKg ?? '')}
            onChange={(event) => set('startWeightKg', event.target.value)}
          />
        </div>

        <TextAreaField
          label="Medical notes"
          hint="Anything a coach must know before putting a bar in their hands."
          rows={3}
          value={String(form.medicalNotes ?? '')}
          onChange={(event) => set('medicalNotes', event.target.value)}
        />
        <TextAreaField
          label="Injury notes"
          rows={2}
          value={String(form.injuryNotes ?? '')}
          onChange={(event) => set('injuryNotes', event.target.value)}
        />
        <TextField
          label="Tags"
          hint="Comma separated: vip, pt-client, corporate."
          value={String(form.tags ?? '')}
          onChange={(event) => set('tags', event.target.value)}
        />

        <div className="space-y-3 rounded-[0.625rem] border border-[var(--hairline)] p-4">
          <Toggle
            label="Waiver signed"
            hint="Timestamped the moment this is switched on."
            checked={Boolean(form.waiverSigned)}
            onChange={(value) => set('waiverSigned', value)}
          />
          <Toggle
            label="Marketing consent"
            checked={Boolean(form.consentMarketing)}
            onChange={(value) => set('consentMarketing', value)}
          />
          <Toggle
            label="Leaderboard opt-in"
            checked={Boolean(form.consentLeaderboard)}
            onChange={(value) => set('consentLeaderboard', value)}
          />
          <Toggle
            label="Transformation showcase consent"
            hint="Required before this member can appear in the public gallery."
            checked={Boolean(form.consentTransformationShowcase)}
            onChange={(value) => set('consentTransformationShowcase', value)}
          />
        </div>

        {!member && (
          <TextField
            label="Initial password"
            hint="Leave blank to generate one and force a change at first sign-in."
            value={String(form.initialPassword ?? '')}
            onChange={(event) => set('initialPassword', event.target.value)}
          />
        )}
      </div>
    </Drawer>
  )
}

/* ---------------------------------------------------------------- import */

function ImportDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const mutations = useMemberMutations()
  const [file, setFile] = useState<File | null>(null)
  const [branchId, setBranchId] = useState('')
  const [result, setResult] = useState<{ imported: number; skipped: string[]; skippedCount: number } | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function submit() {
    if (!file) return setError('Choose a CSV file.')
    if (!branchId) return setError('Pick the branch for rows with no branch column.')
    setError(null)

    try {
      const outcome = await mutations.importCsv.mutateAsync({ file, defaultBranchId: Number(branchId) })
      setResult(outcome)
      toast.success(`${outcome.imported} member(s) imported`)
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Import members"
      description="Same columns the export writes. Rows are validated one at a time — a bad row is reported and skipped, never a failed file."
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Close
          </Button>
          <Button size="sm" icon="check" onClick={() => void submit()} loading={mutations.importCsv.isPending}>
            Import
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {error && <InlineError>{error}</InlineError>}

        <div>
          <label htmlFor="import-file" className="mb-1.5 block text-[0.8125rem] font-medium">
            CSV file
          </label>
          <input
            id="import-file"
            type="file"
            accept=".csv,text/csv"
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
            className="w-full text-[0.8125rem] text-smoke file:mr-3 file:rounded-full file:border file:border-[var(--hairline-strong)] file:bg-transparent file:px-3 file:py-1.5 file:text-[0.8125rem] file:text-bone"
          />
        </div>

        <SelectField
          label="Default branch"
          hint="Used for any row whose Branch column is blank or unrecognised."
          value={branchId}
          onChange={(event) => setBranchId(event.target.value)}
        >
          <option value="">Choose a branch</option>
          {(settings?.branches ?? []).map((branch) => (
            <option key={branch.id} value={branch.id}>
              {branch.name}
            </option>
          ))}
        </SelectField>

        <p className="rounded-[0.625rem] border border-[var(--hairline)] px-4 py-3 text-[0.8125rem] leading-relaxed text-smoke">
          Required columns: <code className="text-bone">FullName</code> and <code className="text-bone">Phone</code>.
          Optional: Email, Branch, City, Tags, Goal, JoinedOn, DateOfBirth.
        </p>

        {result && (
          <div className="rounded-[0.625rem] border border-[var(--hairline)] p-4">
            <p className="text-[0.875rem] font-medium">
              {result.imported} imported · {result.skippedCount} skipped
            </p>
            {result.skipped.length > 0 && (
              <ul className="mt-2 max-h-56 space-y-1 overflow-y-auto text-[0.75rem] text-smoke">
                {result.skipped.map((line) => (
                  <li key={line}>{line}</li>
                ))}
              </ul>
            )}
          </div>
        )}
      </div>
    </Drawer>
  )
}

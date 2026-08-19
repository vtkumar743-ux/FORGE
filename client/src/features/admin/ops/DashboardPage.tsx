import { useState } from 'react'
import { Link } from 'react-router-dom'
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { useSiteSettings } from '@/lib/cms'
import { useDashboard } from '../lib/admin-api'
import { delta, formatInr, formatInrCompact, formatIsoDate } from '../lib/format'
import { Avatar, DataTable, FilterChip, PageHeader, Panel, RiskPill, StatCard } from '../components/ui'
import type { TimeSeriesPoint } from '../lib/types'
import { telLink } from '@/lib/utils'

/**
 * The owner's first screen. Every tile is a live query rather than a nightly rollup, and
 * every number that can be acted on links to the list that acts on it — a dashboard that
 * only reports is a dashboard nobody opens twice.
 */
export function DashboardPage() {
  const [branchId, setBranchId] = useState<number | undefined>()
  const [days, setDays] = useState(30)
  const { data: settings } = useSiteSettings()
  const { data, isLoading } = useDashboard(branchId, days)

  const kpis = data?.kpis

  return (
    <>
      <PageHeader
        eyebrow="Network overview"
        title="Today at a glance"
        lead={data ? `Live as of ${data.generatedAtIst} IST.` : 'Reading the live book…'}
        actions={
          <>
            {[7, 30, 90].map((option) => (
              <FilterChip key={option} active={days === option} onClick={() => setDays(option)}>
                {option}d
              </FilterChip>
            ))}
          </>
        }
      >
        <div className="flex flex-wrap gap-2">
          <FilterChip active={branchId === undefined} onClick={() => setBranchId(undefined)}>
            All branches
          </FilterChip>
          {(settings?.branches ?? []).map((branch) => (
            <FilterChip key={branch.id} active={branchId === branch.id} onClick={() => setBranchId(branch.id)}>
              {branch.name.replace('FORGE ', '')}
            </FilterChip>
          ))}
        </div>
      </PageHeader>

      {isLoading || !kpis ? (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {Array.from({ length: 8 }).map((_, index) => (
            <Skeleton key={index} className="h-32 w-full" />
          ))}
        </div>
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <StatCard
              label="Active members"
              value={kpis.activeMembers.toLocaleString('en-IN')}
              delta={delta(kpis.activeMembers, kpis.activeMembersLastMonth)}
              sub="vs last month"
              icon="users"
            />
            <StatCard
              label="MRR"
              value={formatInrCompact(kpis.mrr)}
              delta={delta(kpis.mrr, kpis.mrrLastMonth)}
              sub="normalised to a month"
              icon="trending-up"
              tone="accent"
            />
            <StatCard
              label="Check-ins today"
              value={kpis.checkInsToday.toLocaleString('en-IN')}
              delta={delta(kpis.checkInsToday, kpis.checkInsYesterday)}
              sub={`${kpis.onFloorNow} on the floor now`}
              icon="qr"
            />
            <StatCard
              label="Dues outstanding"
              value={formatInrCompact(kpis.duesOutstanding)}
              sub={`${kpis.duesInvoiceCount} invoice${kpis.duesInvoiceCount === 1 ? '' : 's'}`}
              icon="clock"
              tone={kpis.duesOutstanding > 0 ? 'warn' : 'neutral'}
            />
          </div>

          <div className="mt-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <StatCard
              label="Expiring in 7 days"
              value={String(kpis.expiringInSevenDays)}
              sub="renewals to chase"
              icon="calendar"
              tone={kpis.expiringInSevenDays > 0 ? 'warn' : 'neutral'}
            />
            <StatCard
              label="New leads this week"
              value={String(kpis.newLeadsThisWeek)}
              sub={`${kpis.leadsAwaitingFirstResponse} awaiting first response`}
              icon="flag"
              tone={kpis.leadsAwaitingFirstResponse > 0 ? 'warn' : 'neutral'}
            />
            <StatCard
              label="Collected this month"
              value={formatInrCompact(kpis.revenueThisMonth)}
              delta={delta(kpis.revenueThisMonth, kpis.revenueLastMonth)}
              sub="payments captured"
              icon="medal"
            />
            <StatCard
              label="At churn risk"
              value={String(kpis.atRiskMembers)}
              sub={`${kpis.classesThisWeek} classes on this week`}
              icon="gauge"
              tone={kpis.atRiskMembers > 0 ? 'warn' : 'neutral'}
            />
          </div>

          {/* ---- charts ---- */}
          <div className="mt-6 grid gap-5 xl:grid-cols-[1.6fr_1fr]">
            <Panel title="Revenue collected" description={`Payments captured over the last ${days} days.`}>
              <SeriesChart data={data.revenue} kind="area" money />
            </Panel>
            <Panel title="Plan mix" description="Where the recurring revenue actually comes from.">
              {data.planMix.length === 0 ? (
                <p className="py-8 text-center text-[0.875rem] text-smoke">No live subscriptions yet.</p>
              ) : (
                <ul className="space-y-3">
                  {data.planMix.slice(0, 6).map((row) => {
                    const max = Math.max(...data.planMix.map((p) => p.mrr), 1)
                    return (
                      <li key={row.planName}>
                        <div className="flex items-baseline justify-between gap-3">
                          <span className="truncate text-[0.875rem]">{row.planName}</span>
                          <span className="numeric shrink-0 text-[0.8125rem] text-smoke">
                            {formatInrCompact(row.mrr)} · {row.subscriptions}
                          </span>
                        </div>
                        <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-[var(--steel)]">
                          <div
                            className="h-full rounded-full bg-accent transition-[width] duration-500 ease-out"
                            style={{ width: `${(row.mrr / max) * 100}%` }}
                          />
                        </div>
                      </li>
                    )
                  })}
                </ul>
              )}
            </Panel>
          </div>

          <div className="mt-5 grid gap-5 xl:grid-cols-2">
            <Panel title="Footfall" description="Check-ins per day across the network.">
              <SeriesChart data={data.footfall} kind="bar" />
            </Panel>
            <Panel title="New memberships" description="Subscriptions starting each day.">
              <SeriesChart data={data.joins} kind="bar" />
            </Panel>
          </div>

          {/* ---- branch comparison ---- */}
          <Panel
            className="mt-5"
            title="Branch comparison"
            description="The same numbers, per site, so a weak branch cannot hide inside the total."
            padded={false}
          >
            <DataTable
              rows={data.branches}
              rowKey={(row) => row.branchId}
              columns={[
                {
                  key: 'name',
                  header: 'Branch',
                  cell: (row) => <span className="font-medium">{row.name}</span>,
                },
                { key: 'members', header: 'Members', align: 'right', cell: (row) => <span className="numeric">{row.activeMembers}</span> },
                { key: 'mrr', header: 'MRR', align: 'right', cell: (row) => <span className="numeric">{formatInrCompact(row.mrr)}</span> },
                {
                  key: 'revenue',
                  header: 'This month',
                  align: 'right',
                  cell: (row) => <span className="numeric">{formatInrCompact(row.revenueThisMonth)}</span>,
                },
                {
                  key: 'dues',
                  header: 'Dues',
                  align: 'right',
                  cell: (row) => (
                    <span className={row.duesOutstanding > 0 ? 'numeric text-accent-hot' : 'numeric text-smoke'}>
                      {formatInrCompact(row.duesOutstanding)}
                    </span>
                  ),
                },
                {
                  key: 'today',
                  header: 'Check-ins',
                  align: 'right',
                  cell: (row) => <span className="numeric">{row.checkInsToday}</span>,
                },
                {
                  key: 'floor',
                  header: 'On floor',
                  align: 'right',
                  cell: (row) => (
                    <span className="numeric">
                      {row.onFloorNow}
                      <span className="text-smoke">/{row.capacity}</span>
                    </span>
                  ),
                },
                {
                  key: 'fill',
                  header: 'Class fill',
                  align: 'right',
                  cell: (row) => (
                    <div className="flex items-center justify-end gap-2">
                      <div className="h-1.5 w-16 overflow-hidden rounded-full bg-[var(--steel)]">
                        <div
                          className="h-full rounded-full bg-accent"
                          style={{ width: `${Math.min(100, row.classFillPercent)}%` }}
                        />
                      </div>
                      <span className="numeric w-9 text-right text-[0.8125rem]">{row.classFillPercent}%</span>
                    </div>
                  ),
                },
              ]}
            />
          </Panel>

          {/* ---- action lists ---- */}
          <div className="mt-5 grid gap-5 xl:grid-cols-2">
            <Panel
              title="Churn radar"
              description="Members drifting away, worst first. Reach out before the renewal date does the talking."
              padded={false}
              actions={
                <Link
                  to="/admin/members?churn=3"
                  className="inline-flex items-center gap-1.5 text-[0.8125rem] text-accent underline-offset-4 hover:underline"
                >
                  Open the list
                  <Icon name="arrow-right" size={14} />
                </Link>
              }
            >
              <DataTable
                rows={data.churnRisk}
                rowKey={(row) => row.memberId}
                emptyHeadline="Nobody is drifting"
                emptyBody="Every active member has visited recently and has nothing outstanding."
                columns={[
                  {
                    key: 'member',
                    header: 'Member',
                    cell: (row) => (
                      <Link to={`/admin/members/${row.memberId}`} className="flex items-center gap-2.5">
                        <div className="min-w-0">
                          <p className="truncate font-medium">{row.fullName}</p>
                          <p className="numeric truncate text-[0.75rem] text-smoke">{row.memberCode}</p>
                        </div>
                      </Link>
                    ),
                  },
                  { key: 'risk', header: 'Risk', cell: (row) => <RiskPill band={row.band} /> },
                  {
                    key: 'seen',
                    header: 'Last seen',
                    align: 'right',
                    cell: (row) => (
                      <span className="numeric text-[0.8125rem] text-smoke">
                        {row.daysSinceVisit >= 999 ? 'never' : `${row.daysSinceVisit}d ago`}
                      </span>
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
                ]}
              />
            </Panel>

            <Panel
              title="Expiring in 7 days"
              description="Renewal conversations, ordered by how little time is left."
              padded={false}
            >
              <DataTable
                rows={data.expiring}
                rowKey={(row) => row.subscriptionId}
                emptyHeadline="No renewals due this week"
                emptyBody="Nothing on the book runs out in the next seven days."
                columns={[
                  {
                    key: 'member',
                    header: 'Member',
                    cell: (row) => (
                      <Link to={`/admin/members/${row.memberId}`}>
                        <p className="font-medium">{row.fullName}</p>
                        <p className="truncate text-[0.75rem] text-smoke">{row.planName}</p>
                      </Link>
                    ),
                  },
                  {
                    key: 'ends',
                    header: 'Ends',
                    align: 'right',
                    cell: (row) => (
                      <div>
                        <p className="numeric text-[0.8125rem]">{formatIsoDate(row.endsOn)}</p>
                        <p
                          className={
                            row.daysLeft <= 2 ? 'numeric text-[0.75rem] text-accent-hot' : 'numeric text-[0.75rem] text-smoke'
                          }
                        >
                          {row.daysLeft === 0 ? 'today' : `${row.daysLeft}d left`}
                        </p>
                      </div>
                    ),
                  },
                  {
                    key: 'value',
                    header: 'Value',
                    align: 'right',
                    cell: (row) => <span className="numeric">{formatInr(row.priceCharged)}</span>,
                  },
                  {
                    key: 'call',
                    header: '',
                    align: 'right',
                    cell: (row) => (
                      <a
                        href={telLink(row.phone)}
                        className="inline-flex size-8 items-center justify-center rounded-full border border-[var(--hairline-strong)] text-smoke transition-colors hover:border-[var(--accent-line)] hover:text-accent"
                        aria-label={`Call ${row.fullName}`}
                      >
                        <Icon name="phone" size={14} />
                      </a>
                    ),
                  },
                ]}
              />
            </Panel>
          </div>

          <Panel
            className="mt-5"
            title="Latest enquiries"
            description="Straight off the website and the desk. The clock starts the moment they land."
            padded={false}
          >
            <DataTable
              rows={data.recentLeads}
              rowKey={(row) => row.id}
              emptyHeadline="No open enquiries"
              emptyBody="Every lead in the pipeline has been closed out."
              columns={[
                {
                  key: 'name',
                  header: 'Lead',
                  cell: (row) => (
                    <Link to="/admin/leads" className="flex items-center gap-2.5">
                      <Avatar name={row.fullName} size={30} />
                      <div className="min-w-0">
                        <p className="truncate font-medium">{row.fullName}</p>
                        <p className="numeric truncate text-[0.75rem] text-smoke">{row.reference}</p>
                      </div>
                    </Link>
                  ),
                },
                { key: 'branch', header: 'Branch', cell: (row) => <span className="text-smoke">{row.branchName ?? '—'}</span> },
                { key: 'goal', header: 'Goal', cell: (row) => <span className="text-smoke">{row.goal ?? '—'}</span> },
                {
                  key: 'age',
                  header: 'Age',
                  align: 'right',
                  cell: (row) => <span className="numeric text-[0.8125rem] text-smoke">{row.ageDays}d</span>,
                },
                {
                  key: 'due',
                  header: 'Follow-up',
                  align: 'right',
                  cell: (row) =>
                    row.isOverdue ? (
                      <span className="text-[0.8125rem] font-medium text-accent-hot">overdue</span>
                    ) : (
                      <span className="text-[0.8125rem] text-smoke">
                        {row.openFollowUps} open
                      </span>
                    ),
                },
              ]}
            />
          </Panel>
        </>
      )}
    </>
  )
}

/* ---------------------------------------------------------------- chart */

function SeriesChart({
  data,
  kind,
  money,
}: {
  data: TimeSeriesPoint[]
  kind: 'area' | 'bar'
  money?: boolean
}) {
  // Long windows would render an unreadable axis; thin the labels instead of the data.
  const tickEvery = Math.max(1, Math.ceil(data.length / 8))
  const axis = { stroke: 'var(--smoke)', fontSize: 11, tickLine: false, axisLine: false } as const

  const tooltip = (
    <Tooltip
      cursor={{ fill: 'var(--accent-soft)' }}
      contentStyle={{
        background: 'var(--carbon)',
        border: '1px solid var(--hairline-strong)',
        borderRadius: '0.625rem',
        fontSize: '0.8125rem',
        color: 'var(--bone)',
      }}
      formatter={(value) => {
        const amount = Number(value ?? 0)
        return [money ? formatInr(amount) : amount.toLocaleString('en-IN'), '']
      }}
      labelFormatter={(label) => String(label)}
    />
  )

  return (
    <div className="h-56 w-full">
      <ResponsiveContainer width="100%" height="100%">
        {kind === 'area' ? (
          <AreaChart data={data} margin={{ top: 8, right: 4, bottom: 0, left: -12 }}>
            <defs>
              <linearGradient id="revenueFill" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="var(--accent)" stopOpacity={0.32} />
                <stop offset="100%" stopColor="var(--accent)" stopOpacity={0} />
              </linearGradient>
            </defs>
            <CartesianGrid stroke="var(--hairline)" vertical={false} />
            <XAxis dataKey="label" interval={tickEvery - 1} {...axis} />
            <YAxis {...axis} width={54} tickFormatter={(value: number) => formatInrCompact(value)} />
            {tooltip}
            <Area
              type="monotone"
              dataKey="value"
              stroke="var(--accent)"
              strokeWidth={2}
              fill="url(#revenueFill)"
              dot={false}
            />
          </AreaChart>
        ) : (
          <BarChart data={data} margin={{ top: 8, right: 4, bottom: 0, left: -20 }}>
            <CartesianGrid stroke="var(--hairline)" vertical={false} />
            <XAxis dataKey="label" interval={tickEvery - 1} {...axis} />
            <YAxis {...axis} width={38} allowDecimals={false} />
            {tooltip}
            <Bar dataKey="value" fill="var(--accent)" radius={[3, 3, 0, 0]} maxBarSize={26} />
          </BarChart>
        )}
      </ResponsiveContainer>
    </div>
  )
}

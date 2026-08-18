import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { useOccupancy, useSiteSettings } from '@/lib/cms'
import { cn } from '@/lib/utils'
import { useAbsentees, useAttendanceActions, useAttendanceToday, useHeatmap } from '../lib/admin-api'
import { describeErrorText, formatIsoDate, formatIstTime, istToday } from '../lib/format'
import { checkInSourceNames, weekdayNames } from '../lib/types'
import { useToast } from '../components/overlays'
import {
  Avatar,
  DataTable,
  FilterChip,
  PageHeader,
  Panel,
  Pill,
  StatCard,
  TextField,
} from '../components/ui'

/**
 * The floor view. Live occupancy, today's visits, the peak-hours heatmap that tells the
 * owner where to add classes, and the absentee list that feeds win-back.
 */
export function AttendancePage() {
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const { data: occupancy } = useOccupancy()
  const actions = useAttendanceActions()

  const [branchId, setBranchId] = useState<number | undefined>()
  const [date, setDate] = useState(istToday())
  const [heatmapDays, setHeatmapDays] = useState(28)
  const [selected, setSelected] = useState<Set<number>>(new Set())

  const { data: today } = useAttendanceToday({ branchId, date })
  const { data: heatmap } = useHeatmap({ branchId, days: heatmapDays })
  const { data: absentees } = useAbsentees({ branchId, days: 10 })

  const isToday = date === istToday()

  async function sendWinBack() {
    if (selected.size === 0) return
    try {
      const result = await actions.winBack.mutateAsync({ memberIds: [...selected] })
      toast.success(`Win-back sent to ${result.members} member(s)`)
      setSelected(new Set())
    } catch (error) {
      toast.error('Could not send', describeErrorText(error))
    }
  }

  return (
    <>
      <PageHeader
        eyebrow="Floor"
        title="Attendance"
        lead="Check-ins, occupancy and the people who have stopped turning up."
        actions={
          <>
            {isToday && branchId && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() =>
                  void actions.checkOutAll
                    .mutateAsync(branchId)
                    .then((result) => toast.success(`${(result as { closed: number }).closed} visit(s) closed`))
                }
              >
                Close all visits
              </Button>
            )}
            <Link
              to="/admin/attendance/kiosk"
              className="inline-flex h-9 items-center gap-2 rounded-full bg-accent px-4 text-[0.8125rem] font-medium text-ink transition-[filter] hover:brightness-110"
            >
              <Icon name="qr" size={15} />
              Open kiosk
            </Link>
          </>
        }
      >
        <div className="flex flex-wrap items-center gap-2">
          <FilterChip active={!branchId} onClick={() => setBranchId(undefined)}>
            All branches
          </FilterChip>
          {(settings?.branches ?? []).map((branch) => (
            <FilterChip key={branch.id} active={branchId === branch.id} onClick={() => setBranchId(branch.id)}>
              {branch.name.replace('FORGE ', '')}
            </FilterChip>
          ))}
          <div className="ml-auto">
            <TextField type="date" value={date} onChange={(event) => setDate(event.target.value)} aria-label="Date" />
          </div>
        </div>
      </PageHeader>

      {/* ---- live occupancy ---- */}
      <div className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label={isToday ? 'Visits today' : 'Visits'} value={String(today?.total ?? 0)} icon="qr" />
        <StatCard label="On the floor now" value={String(today?.onFloor ?? 0)} icon="users" tone="accent" />
        <StatCard
          label="Entries refused"
          value={String(today?.refused ?? 0)}
          sub="expired plans and dues"
          icon="lock"
          tone={(today?.refused ?? 0) > 0 ? 'warn' : 'neutral'}
        />
        <StatCard
          label="Absent 10+ days"
          value={String(absentees?.length ?? 0)}
          sub="active memberships"
          icon="clock"
          tone={(absentees?.length ?? 0) > 0 ? 'warn' : 'neutral'}
        />
      </div>

      <Panel className="mb-5" title="Live occupancy" description="Open visits against the comfortable head-count for each floor.">
        <ul className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {(occupancy ?? []).map((entry) => {
            const band = ['Comfortable', 'Busy', 'Peak'][entry.band] ?? 'Comfortable'
            const tone = entry.band === 2 ? 'var(--accent-hot)' : entry.band === 1 ? 'var(--accent)' : 'var(--success)'
            return (
              <li key={entry.branchId}>
                <div className="flex items-baseline justify-between gap-2">
                  <span className="text-[0.875rem] font-medium">{entry.branchName.replace('FORGE ', '')}</span>
                  <span className="numeric text-[0.8125rem] text-smoke">
                    {entry.currentCount}/{entry.capacity}
                  </span>
                </div>
                <div className="mt-2 h-2 overflow-hidden rounded-full bg-[var(--steel)]">
                  <div
                    className="h-full rounded-full transition-[width] duration-500 ease-out"
                    style={{ width: `${entry.percentFull}%`, background: tone }}
                  />
                </div>
                <p className="mt-1.5 text-[0.75rem]" style={{ color: tone }}>
                  {band} · {entry.percentFull}%
                </p>
              </li>
            )
          })}
        </ul>
      </Panel>

      {/* ---- heatmap ---- */}
      <Panel
        className="mb-5"
        title="Peak hours"
        description={
          heatmap
            ? `${heatmap.totalVisits.toLocaleString('en-IN')} visits over ${heatmap.daysCovered} days. Busiest: ${heatmap.peakLabel}.`
            : 'Reading the last four weeks…'
        }
        actions={
          <>
            {[14, 28, 90].map((option) => (
              <FilterChip key={option} active={heatmapDays === option} onClick={() => setHeatmapDays(option)}>
                {option}d
              </FilterChip>
            ))}
          </>
        }
      >
        {heatmap && <Heatmap cells={heatmap.cells} peak={heatmap.peakCount} />}
      </Panel>

      {/* ---- today ---- */}
      <Panel
        className="mb-5"
        title={isToday ? "Today's check-ins" : `Check-ins on ${formatIsoDate(date)}`}
        padded={false}
      >
        <DataTable
          rows={today?.rows ?? []}
          rowKey={(row) => row.id}
          emptyHeadline="No visits recorded"
          emptyBody="Scan a member QR at the kiosk, or check someone in from the desk."
          columns={[
            {
              key: 'member',
              header: 'Member',
              cell: (row) => (
                <Link to={`/admin/members/${row.memberId}`} className="flex items-center gap-2.5">
                  <Avatar src={row.photoUrl} name={row.fullName} size={30} />
                  <div className="min-w-0">
                    <p className="truncate font-medium">{row.fullName}</p>
                    <p className="numeric truncate text-[0.75rem] text-smoke">{row.memberCode}</p>
                  </div>
                </Link>
              ),
            },
            { key: 'branch', header: 'Branch', cell: (row) => <span className="text-smoke">{row.branchName.replace('FORGE ', '')}</span> },
            { key: 'in', header: 'In', cell: (row) => <span className="numeric">{formatIstTime(row.checkInAtUtc)}</span> },
            {
              key: 'out',
              header: 'Out',
              cell: (row) =>
                row.checkOutAtUtc ? (
                  <span className="numeric">{formatIstTime(row.checkOutAtUtc)}</span>
                ) : row.wasBlocked ? (
                  <span className="text-smoke">—</span>
                ) : (
                  <Pill tone="success">on floor</Pill>
                ),
            },
            {
              key: 'duration',
              header: 'Stay',
              align: 'right',
              cell: (row) => (
                <span className="numeric text-[0.8125rem] text-smoke">
                  {row.durationMinutes ? `${row.durationMinutes} min` : '—'}
                </span>
              ),
            },
            { key: 'source', header: 'Source', cell: (row) => <Pill tone="muted">{checkInSourceNames[row.source]}</Pill> },
            {
              key: 'class',
              header: 'Class',
              cell: (row) => <span className="text-smoke">{row.className ?? '—'}</span>,
            },
            {
              key: 'status',
              header: '',
              align: 'right',
              cell: (row) =>
                row.wasBlocked ? (
                  <Pill tone="danger" icon="lock">
                    {row.blockReason ?? 'refused'}
                  </Pill>
                ) : row.checkOutAtUtc ? null : (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => void actions.checkOut.mutateAsync(row.id)}
                  >
                    Check out
                  </Button>
                ),
            },
          ]}
        />
      </Panel>

      {/* ---- absentees ---- */}
      <Panel
        title="Absentee alerts"
        description="Active memberships with no visit in ten days. The sweep messages them automatically once a fortnight; this is the manual nudge."
        padded={false}
        actions={
          selected.size > 0 && (
            <Button size="sm" icon="share" onClick={() => void sendWinBack()} loading={actions.winBack.isPending}>
              Send win-back to {selected.size}
            </Button>
          )
        }
      >
        <DataTable
          rows={absentees ?? []}
          rowKey={(row) => row.memberId}
          emptyHeadline="Everyone is showing up"
          emptyBody="No active member has been away for ten days or more."
          columns={[
            {
              key: 'select',
              width: '2.5rem',
              header: '',
              cell: (row) => (
                <input
                  type="checkbox"
                  checked={selected.has(row.memberId)}
                  onChange={() =>
                    setSelected((current) => {
                      const next = new Set(current)
                      if (next.has(row.memberId)) next.delete(row.memberId)
                      else next.add(row.memberId)
                      return next
                    })
                  }
                  aria-label={`Select ${row.fullName}`}
                  className="size-3.5 accent-[var(--accent)]"
                />
              ),
            },
            {
              key: 'member',
              header: 'Member',
              cell: (row) => (
                <Link to={`/admin/members/${row.memberId}`}>
                  <p className="font-medium">{row.fullName}</p>
                  <p className="numeric text-[0.75rem] text-smoke">
                    {row.memberCode} · {row.branchName.replace('FORGE ', '')}
                  </p>
                </Link>
              ),
            },
            {
              key: 'away',
              header: 'Away',
              align: 'right',
              cell: (row) => (
                <span className={row.daysSinceVisit > 30 ? 'numeric text-accent-hot' : 'numeric'}>
                  {row.daysSinceVisit >= 999 ? 'never came' : `${row.daysSinceVisit}d`}
                </span>
              ),
            },
            { key: 'plan', header: 'Plan', cell: (row) => <span className="text-smoke">{row.planName ?? '—'}</span> },
            {
              key: 'sent',
              header: 'Win-back',
              align: 'right',
              cell: (row) =>
                row.winBackSent ? (
                  <Pill tone="muted">sent</Pill>
                ) : (
                  <span className="text-[0.8125rem] text-smoke">not yet</span>
                ),
            },
            {
              key: 'call',
              header: '',
              align: 'right',
              cell: (row) => (
                <a
                  href={`https://wa.me/91${row.phone}`}
                  target="_blank"
                  rel="noreferrer noopener"
                  className="inline-flex size-8 items-center justify-center rounded-full border border-[var(--hairline-strong)] text-smoke transition-colors hover:border-success/50 hover:text-success"
                  aria-label={`WhatsApp ${row.fullName}`}
                >
                  <Icon name="share" size={14} />
                </a>
              ),
            },
          ]}
        />
      </Panel>
    </>
  )
}

/* ---------------------------------------------------------------- heatmap */

function Heatmap({ cells, peak }: { cells: { dayOfWeek: number; hour: number; count: number }[]; peak: number }) {
  const lookup = useMemo(() => {
    const map = new Map<string, number>()
    for (const cell of cells) map.set(`${cell.dayOfWeek}-${cell.hour}`, cell.count)
    return map
  }, [cells])

  // Gyms open at 5:30 and close by 23:00; rendering midnight-to-dawn would be dead space.
  const hours = Array.from({ length: 18 }, (_, index) => index + 5)

  return (
    <div className="overflow-x-auto">
      <table className="border-separate border-spacing-[2px]">
        <thead>
          <tr>
            <th className="w-10" />
            {hours.map((hour) => (
              <th
                key={hour}
                scope="col"
                className="numeric w-7 pb-1 text-center text-[0.625rem] font-normal text-smoke"
              >
                {hour % 3 === 0 ? hour : ''}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {[1, 2, 3, 4, 5, 6, 0].map((day) => (
            <tr key={day}>
              <th scope="row" className="pr-2 text-right text-[0.6875rem] font-normal text-smoke">
                {weekdayNames[day].slice(0, 3)}
              </th>
              {hours.map((hour) => {
                const count = lookup.get(`${day}-${hour}`) ?? 0
                const intensity = peak === 0 ? 0 : count / peak
                return (
                  <td key={hour}>
                    <div
                      title={`${weekdayNames[day]} ${hour}:00 — ${count} visit${count === 1 ? '' : 's'}`}
                      className={cn(
                        'size-7 rounded-[3px] transition-transform duration-150 hover:scale-110',
                        count === 0 && 'bg-[var(--steel)]',
                      )}
                      style={
                        count > 0
                          ? {
                              // One hue, varying weight — a rainbow heatmap reads as decoration.
                              background: `color-mix(in srgb, var(--accent) ${Math.round(
                                14 + intensity * 86,
                              )}%, var(--steel))`,
                            }
                          : undefined
                      }
                    />
                  </td>
                )
              })}
            </tr>
          ))}
        </tbody>
      </table>

      <div className="mt-3 flex items-center gap-2 text-[0.6875rem] text-smoke">
        <span>Quiet</span>
        {[14, 35, 55, 75, 100].map((step) => (
          <span
            key={step}
            className="size-3 rounded-[2px]"
            style={{ background: `color-mix(in srgb, var(--accent) ${step}%, var(--steel))` }}
          />
        ))}
        <span>Peak ({peak})</span>
      </div>
    </div>
  )
}

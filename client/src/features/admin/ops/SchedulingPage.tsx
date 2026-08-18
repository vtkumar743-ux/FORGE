import { useMemo, useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { useSiteSettings } from '@/lib/cms'
import { cn } from '@/lib/utils'
import {
  useAttendanceActions,
  useClassFormats,
  useRooms,
  useRoster,
  useSchedules,
  useSchedulingActions,
  useSessions,
  useTrainerOptions,
} from '../lib/admin-api'
import { describeErrorText, formatIsoDate, istToday, to12Hour } from '../lib/format'
import {
  bookingStatusNames,
  classIntensityNames,
  classLevelNames,
  sessionStatusNames,
  weekdayNames,
  type ConflictRow,
  type ScheduleRow,
} from '../lib/types'
import { ConfirmDialog, Drawer, useToast } from '../components/overlays'
import {
  Avatar,
  DataTable,
  FilterChip,
  InlineError,
  PageHeader,
  Panel,
  Pill,
  SelectField,
  StatusPill,
  TextAreaField,
  TextField,
  Toggle,
} from '../components/ui'

type Tab = 'timetable' | 'sessions' | 'formats'

/**
 * Classes and scheduling. The weekly grid is the recurring rule set; the sessions list is
 * the concrete occurrences those rules produced. Editing a rule updates its future
 * occurrences in place rather than deleting them, because members hold bookings against
 * those exact rows.
 */
export function SchedulingPage() {
  const { data: settings } = useSiteSettings()
  const [tab, setTab] = useState<Tab>('timetable')
  const [branchId, setBranchId] = useState<number | undefined>()
  const [editing, setEditing] = useState<ScheduleRow | 'new' | null>(null)

  const { data: schedules, isLoading } = useSchedules({ branchId })

  return (
    <>
      <PageHeader
        eyebrow="Programming"
        title="Classes & scheduling"
        lead="One shared format library, a per-branch recurring timetable, and the occurrences members actually book."
        actions={
          tab === 'timetable' && (
            <Button size="sm" icon="plus" onClick={() => setEditing('new')}>
              Add slot
            </Button>
          )
        }
      >
        <div className="flex flex-wrap items-center gap-2">
          {(['timetable', 'sessions', 'formats'] as Tab[]).map((option) => (
            <FilterChip key={option} active={tab === option} onClick={() => setTab(option)}>
              {option === 'timetable' ? 'Weekly timetable' : option === 'sessions' ? 'Sessions' : 'Class formats'}
            </FilterChip>
          ))}
          <span className="mx-1 w-px self-stretch bg-[var(--hairline)]" />
          <FilterChip active={!branchId} onClick={() => setBranchId(undefined)}>
            All branches
          </FilterChip>
          {(settings?.branches ?? []).map((branch) => (
            <FilterChip key={branch.id} active={branchId === branch.id} onClick={() => setBranchId(branch.id)}>
              {branch.name.replace('FORGE ', '')}
            </FilterChip>
          ))}
        </div>
      </PageHeader>

      {tab === 'timetable' && (
        <WeeklyGrid
          schedules={schedules ?? []}
          loading={isLoading}
          onEdit={setEditing}
          onAdd={() => setEditing('new')}
        />
      )}
      {tab === 'sessions' && <SessionsTab branchId={branchId} />}
      {tab === 'formats' && <FormatsTab />}

      <ScheduleDrawer
        value={editing}
        onClose={() => setEditing(null)}
        defaultBranchId={branchId ?? settings?.branches[0]?.id}
      />
    </>
  )
}

/* ---------------------------------------------------------------- weekly grid */

function WeeklyGrid({
  schedules,
  loading,
  onEdit,
  onAdd,
}: {
  schedules: ScheduleRow[]
  loading: boolean
  onEdit: (row: ScheduleRow) => void
  onAdd: () => void
}) {
  const byDay = useMemo(() => {
    const map = new Map<number, ScheduleRow[]>()
    for (let day = 0; day < 7; day++) map.set(day, [])
    for (const row of schedules) map.get(row.dayOfWeek)?.push(row)
    for (const list of map.values()) list.sort((a, b) => a.startTime.localeCompare(b.startTime))
    return map
  }, [schedules])

  if (loading) {
    return (
      <div className="grid gap-3 lg:grid-cols-4 xl:grid-cols-7">
        {Array.from({ length: 7 }).map((_, index) => (
          <Skeleton key={index} className="h-72 w-full" />
        ))}
      </div>
    )
  }

  if (schedules.length === 0) {
    return (
      <Panel>
        <div className="py-12 text-center">
          <p className="display-m text-[1.25rem]">No timetable yet</p>
          <p className="measure mx-auto mt-2 text-[0.875rem] leading-relaxed text-smoke">
            Add a weekly slot and the occurrences are materialised four weeks ahead, ready to book.
          </p>
          <Button size="sm" icon="plus" className="mt-5" onClick={onAdd}>
            Add the first slot
          </Button>
        </div>
      </Panel>
    )
  }

  // Monday first — a gym week does not start on Sunday.
  const order = [1, 2, 3, 4, 5, 6, 0]

  return (
    <div className="grid gap-3 lg:grid-cols-4 xl:grid-cols-7">
      {order.map((day) => {
        const rows = byDay.get(day) ?? []
        return (
          <section key={day} className="rounded-[var(--radius-card)] border border-[var(--hairline)] bg-carbon">
            <header className="flex items-baseline justify-between border-b border-[var(--hairline)] px-3.5 py-2.5">
              <h2 className="text-[0.8125rem] font-semibold">{weekdayNames[day]}</h2>
              <span className="numeric text-[0.75rem] text-smoke">{rows.length}</span>
            </header>
            <div className="space-y-2 p-2.5">
              {rows.length === 0 && <p className="py-5 text-center text-[0.75rem] text-smoke">Rest day.</p>}
              {rows.map((row) => (
                <button
                  key={row.id}
                  type="button"
                  onClick={() => onEdit(row)}
                  className={cn(
                    'group w-full rounded-[0.625rem] border p-2.5 text-left transition-[border-color,transform] duration-200',
                    'hover:-translate-y-0.5 hover:border-[var(--accent-line)]',
                    row.isActive ? 'border-[var(--hairline)]' : 'border-dashed border-[var(--hairline)] opacity-55',
                  )}
                >
                  <div className="flex items-baseline justify-between gap-2">
                    <span className="numeric text-[0.8125rem] font-semibold text-accent">
                      {to12Hour(row.startTime)}
                    </span>
                    <span className="numeric text-[0.6875rem] text-smoke">{row.durationMinutes}m</span>
                  </div>
                  <p className="mt-1 truncate text-[0.8125rem] font-medium">{row.formatName}</p>
                  <p className="truncate text-[0.6875rem] text-smoke">
                    {row.trainerName}
                    {row.roomName ? ` · ${row.roomName}` : ''}
                  </p>
                  <div className="mt-1.5 flex items-center gap-1.5">
                    <div className="h-1 flex-1 overflow-hidden rounded-full bg-[var(--steel)]">
                      <div
                        className="h-full rounded-full bg-accent"
                        style={{ width: `${Math.min(100, row.averageFillPercent)}%` }}
                      />
                    </div>
                    <span className="numeric text-[0.625rem] text-smoke">{row.averageFillPercent}%</span>
                  </div>
                </button>
              ))}
            </div>
          </section>
        )
      })}
    </div>
  )
}

/* ---------------------------------------------------------------- schedule drawer */

function ScheduleDrawer({
  value,
  onClose,
  defaultBranchId,
}: {
  value: ScheduleRow | 'new' | null
  onClose: () => void
  defaultBranchId?: number
}) {
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const { data: formats } = useClassFormats()
  const actions = useSchedulingActions()
  const isNew = value === 'new'
  const row = isNew ? null : value

  const [form, setForm] = useState<Record<string, string | boolean>>({})
  const [signature, setSignature] = useState<string | null>(null)
  const [conflicts, setConflicts] = useState<ConflictRow[]>([])
  const [error, setError] = useState<string | null>(null)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const key = value === null ? null : isNew ? 'new' : `edit-${row?.id}`
  if (key !== null && signature !== key) {
    setSignature(key)
    setConflicts([])
    setError(null)
    setForm({
      branchId: String(row?.branchId ?? defaultBranchId ?? ''),
      classFormatId: String(row?.classFormatId ?? ''),
      roomId: row?.roomId ? String(row.roomId) : '',
      trainerId: String(row?.trainerId ?? ''),
      dayOfWeek: String(row?.dayOfWeek ?? 1),
      startTime: row?.startTime ?? '07:00',
      durationMinutes: String(row?.durationMinutes ?? 45),
      capacity: String(row?.capacity ?? 20),
      effectiveFrom: row?.effectiveFrom ?? istToday(),
      effectiveTo: row?.effectiveTo ?? '',
      bookingOpensHoursBefore: String(row?.bookingOpensHoursBefore ?? 72),
      cancelCutoffHoursBefore: String(row?.cancelCutoffHoursBefore ?? 4),
      waitlistEnabled: row?.waitlistEnabled ?? true,
      waitlistCapacity: String(row?.waitlistCapacity ?? 10),
      isActive: row?.isActive ?? true,
      materialiseWeeks: '4',
    })
  }

  const branchId = form.branchId ? Number(form.branchId) : undefined
  const { data: rooms } = useRooms(branchId)
  const { data: trainers } = useTrainerOptions()

  function set(field: string, next: string | boolean) {
    setForm((current) => ({ ...current, [field]: next }))
    setConflicts([])
  }

  function payload(ignoreConflicts: boolean) {
    return {
      branchId: Number(form.branchId),
      classFormatId: Number(form.classFormatId),
      roomId: form.roomId ? Number(form.roomId) : undefined,
      trainerId: Number(form.trainerId),
      dayOfWeek: Number(form.dayOfWeek),
      startTime: String(form.startTime),
      durationMinutes: Number(form.durationMinutes),
      capacity: Number(form.capacity),
      effectiveFrom: String(form.effectiveFrom),
      effectiveTo: form.effectiveTo ? String(form.effectiveTo) : undefined,
      bookingOpensHoursBefore: Number(form.bookingOpensHoursBefore),
      cancelCutoffHoursBefore: Number(form.cancelCutoffHoursBefore),
      waitlistEnabled: Boolean(form.waitlistEnabled),
      waitlistCapacity: Number(form.waitlistCapacity),
      isActive: Boolean(form.isActive),
      materialiseWeeks: Number(form.materialiseWeeks),
      ignoreConflicts,
    }
  }

  async function check() {
    if (!form.branchId || !form.trainerId) return
    try {
      const found = await actions.checkConflicts.mutateAsync({
        branchId: Number(form.branchId),
        trainerId: Number(form.trainerId),
        roomId: form.roomId ? Number(form.roomId) : undefined,
        dayOfWeek: Number(form.dayOfWeek),
        startTime: String(form.startTime),
        durationMinutes: Number(form.durationMinutes),
        effectiveFrom: String(form.effectiveFrom),
        effectiveTo: form.effectiveTo || undefined,
        ignoreScheduleId: row?.id,
      })
      setConflicts(found)
      if (found.length === 0) toast.success('No clashes', 'The coach and the room are both free.')
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  async function submit(force = false) {
    if (!form.branchId || !form.classFormatId || !form.trainerId) {
      return setError('Branch, format and coach are all required.')
    }
    setError(null)

    try {
      if (isNew) await actions.createSchedule.mutateAsync(payload(force))
      else if (row) await actions.updateSchedule.mutateAsync({ id: row.id, body: payload(force) })
      toast.success(isNew ? 'Slot added' : 'Slot updated', 'Occurrences have been refreshed.')
      onClose()
    } catch (cause) {
      // A 409 carries the conflict list; surface it rather than a generic failure.
      const response = (cause as { response?: { status?: number; data?: { conflicts?: ConflictRow[] } } }).response
      if (response?.status === 409 && response.data?.conflicts) {
        setConflicts(response.data.conflicts)
        setError('This slot clashes with something already on the timetable.')
      } else {
        setError(describeErrorText(cause))
      }
    }
  }

  const selectedFormat = (formats ?? []).find((f) => f.id === Number(form.classFormatId))

  return (
    <>
      <Drawer
        open={value !== null}
        onClose={onClose}
        title={isNew ? 'Add a timetable slot' : `${row?.formatName} · ${weekdayNames[row?.dayOfWeek ?? 0]}`}
        description="A recurring weekly rule. Occurrences are generated four weeks ahead and topped up automatically."
        width="lg"
        footer={
          <>
            {!isNew && (
              <Button variant="ghost" size="sm" onClick={() => setConfirmDelete(true)}>
                Retire slot
              </Button>
            )}
            <div className="flex-1" />
            <Button variant="outline" size="sm" onClick={() => void check()} loading={actions.checkConflicts.isPending}>
              Check clashes
            </Button>
            <Button
              size="sm"
              icon="check"
              onClick={() => void submit(conflicts.length > 0)}
              loading={actions.createSchedule.isPending || actions.updateSchedule.isPending}
            >
              {conflicts.length > 0 ? 'Save anyway' : 'Save slot'}
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          {error && <InlineError>{error}</InlineError>}

          {conflicts.length > 0 && (
            <div className="rounded-[var(--radius-card)] border border-accent-hot/45 bg-[color-mix(in_srgb,var(--accent-hot)_7%,transparent)] p-4">
              <p className="flex items-center gap-2 text-[0.875rem] font-medium text-accent-hot">
                <Icon name="x" size={15} />
                {conflicts.length} clash{conflicts.length === 1 ? '' : 'es'}
              </p>
              <ul className="mt-2 space-y-1 text-[0.8125rem] leading-relaxed text-bone">
                {conflicts.map((conflict) => (
                  <li key={`${conflict.kind}-${conflict.conflictingScheduleId}`}>
                    <Pill tone="danger" className="mr-2">
                      {conflict.kind}
                    </Pill>
                    {conflict.message}
                  </li>
                ))}
              </ul>
            </div>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <SelectField label="Branch" required value={String(form.branchId ?? '')} onChange={(event) => set('branchId', event.target.value)}>
              <option value="">Choose a branch</option>
              {(settings?.branches ?? []).map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </SelectField>

            <SelectField
              label="Class format"
              required
              value={String(form.classFormatId ?? '')}
              onChange={(event) => {
                const format = (formats ?? []).find((f) => f.id === Number(event.target.value))
                set('classFormatId', event.target.value)
                // Seed duration and capacity from the format so the common case is one click.
                if (format) {
                  setForm((current) => ({
                    ...current,
                    classFormatId: event.target.value,
                    durationMinutes: String(format.defaultDurationMinutes),
                    capacity: String(format.defaultCapacity),
                  }))
                }
              }}
            >
              <option value="">Choose a format</option>
              {(formats ?? [])
                .filter((format) => format.isActive)
                .map((format) => (
                  <option key={format.id} value={format.id}>
                    {format.name}
                  </option>
                ))}
            </SelectField>

            <SelectField label="Coach" required value={String(form.trainerId ?? '')} onChange={(event) => set('trainerId', event.target.value)}>
              <option value="">Choose a coach</option>
              {(trainers ?? []).map((trainer) => (
                <option key={trainer.id} value={trainer.id}>
                  {trainer.fullName}
                </option>
              ))}
            </SelectField>

            <SelectField label="Room" value={String(form.roomId ?? '')} onChange={(event) => set('roomId', event.target.value)}>
              <option value="">No room assigned</option>
              {(rooms ?? []).map((room) => (
                <option key={room.id} value={room.id}>
                  {room.name} ({room.capacity})
                </option>
              ))}
            </SelectField>

            <SelectField label="Day" value={String(form.dayOfWeek ?? 1)} onChange={(event) => set('dayOfWeek', event.target.value)}>
              {[1, 2, 3, 4, 5, 6, 0].map((day) => (
                <option key={day} value={day}>
                  {weekdayNames[day]}
                </option>
              ))}
            </SelectField>

            <TextField label="Start time" type="time" value={String(form.startTime ?? '')} onChange={(event) => set('startTime', event.target.value)} />
            <TextField
              label="Duration (minutes)"
              type="number"
              value={String(form.durationMinutes ?? '')}
              onChange={(event) => set('durationMinutes', event.target.value)}
              hint={selectedFormat ? `${selectedFormat.name} usually runs ${selectedFormat.defaultDurationMinutes} minutes.` : undefined}
            />
            <TextField label="Capacity" type="number" value={String(form.capacity ?? '')} onChange={(event) => set('capacity', event.target.value)} />
            <TextField label="Runs from" type="date" value={String(form.effectiveFrom ?? '')} onChange={(event) => set('effectiveFrom', event.target.value)} />
            <TextField
              label="Runs until"
              type="date"
              hint="Blank means it runs indefinitely."
              value={String(form.effectiveTo ?? '')}
              onChange={(event) => set('effectiveTo', event.target.value)}
            />
          </div>

          <div className="grid gap-4 rounded-[0.625rem] border border-[var(--hairline)] p-4 sm:grid-cols-2">
            <TextField
              label="Booking opens (hours before)"
              type="number"
              value={String(form.bookingOpensHoursBefore ?? '')}
              onChange={(event) => set('bookingOpensHoursBefore', event.target.value)}
            />
            <TextField
              label="Free-cancel cut-off (hours)"
              type="number"
              hint="Inside this window a cancel counts as a late cancel."
              value={String(form.cancelCutoffHoursBefore ?? '')}
              onChange={(event) => set('cancelCutoffHoursBefore', event.target.value)}
            />
            <div className="sm:col-span-2">
              <Toggle
                label="Waitlist"
                hint="Full classes queue members and promote them automatically when a spot frees up."
                checked={Boolean(form.waitlistEnabled)}
                onChange={(next) => set('waitlistEnabled', next)}
              />
            </div>
            {form.waitlistEnabled && (
              <TextField
                label="Waitlist capacity"
                type="number"
                value={String(form.waitlistCapacity ?? '')}
                onChange={(event) => set('waitlistCapacity', event.target.value)}
              />
            )}
            <TextField
              label="Materialise weeks ahead"
              type="number"
              hint="How far forward to create bookable occurrences right now."
              value={String(form.materialiseWeeks ?? '4')}
              onChange={(event) => set('materialiseWeeks', event.target.value)}
            />
            <div className="sm:col-span-2">
              <Toggle label="Active" checked={Boolean(form.isActive)} onChange={(next) => set('isActive', next)} />
            </div>
          </div>
        </div>
      </Drawer>

      <ConfirmDialog
        open={confirmDelete}
        onClose={() => setConfirmDelete(false)}
        title="Retire this slot?"
        body="The rule stops today and unbooked future occurrences are removed. Occurrences members have already booked stay, so nobody loses a class."
        confirmLabel="Retire slot"
        tone="danger"
        loading={actions.removeSchedule.isPending}
        onConfirm={() => {
          if (!row) return
          void actions.removeSchedule
            .mutateAsync(row.id)
            .then(() => {
              toast.success('Slot retired')
              setConfirmDelete(false)
              onClose()
            })
            .catch((cause) => toast.error('Could not retire', describeErrorText(cause)))
        }}
      />
    </>
  )
}

/* ---------------------------------------------------------------- sessions */

function SessionsTab({ branchId }: { branchId?: number }) {
  const [from, setFrom] = useState(istToday())
  const [days, setDays] = useState(7)
  const [rosterFor, setRosterFor] = useState<number | null>(null)
  const { data: sessions, isLoading } = useSessions({ branchId, from, days })

  const grouped = useMemo(() => {
    const map = new Map<string, typeof sessions>()
    for (const session of sessions ?? []) {
      const list = map.get(session.date) ?? []
      list.push(session)
      map.set(session.date, list)
    }
    return [...map.entries()]
  }, [sessions])

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <TextField type="date" value={from} onChange={(event) => setFrom(event.target.value)} aria-label="From date" />
        {[7, 14, 28].map((option) => (
          <FilterChip key={option} active={days === option} onClick={() => setDays(option)}>
            {option} days
          </FilterChip>
        ))}
      </div>

      {isLoading && <Skeleton className="h-96 w-full" />}

      {!isLoading && grouped.length === 0 && (
        <Panel>
          <p className="py-10 text-center text-[0.875rem] text-smoke">
            No sessions in that window. Add a timetable slot, or materialise more weeks from an existing one.
          </p>
        </Panel>
      )}

      <div className="space-y-5">
        {grouped.map(([date, rows]) => (
          <Panel key={date} title={formatIsoDate(date)} padded={false}>
            <DataTable
              rows={rows ?? []}
              rowKey={(row) => row.id}
              dense
              onRowClick={(row) => setRosterFor(row.id)}
              columns={[
                {
                  key: 'time',
                  header: 'Time',
                  width: '8rem',
                  cell: (row) => (
                    <span className="numeric font-medium text-accent">{to12Hour(row.startTime)}</span>
                  ),
                },
                {
                  key: 'class',
                  header: 'Class',
                  cell: (row) => (
                    <div>
                      <p className="font-medium">{row.formatName}</p>
                      <p className="text-[0.75rem] text-smoke">
                        {row.branchName.replace('FORGE ', '')}
                        {row.roomName ? ` · ${row.roomName}` : ''}
                      </p>
                    </div>
                  ),
                },
                {
                  key: 'coach',
                  header: 'Coach',
                  cell: (row) => (
                    <span className="flex items-center gap-2">
                      {row.trainerName}
                      {row.isSubstitute && <Pill tone="warn">sub</Pill>}
                    </span>
                  ),
                },
                {
                  key: 'fill',
                  header: 'Booked',
                  align: 'right',
                  cell: (row) => (
                    <div className="flex items-center justify-end gap-2">
                      <span className="numeric">
                        {row.bookedCount}
                        <span className="text-smoke">/{row.capacity}</span>
                      </span>
                      <div className="h-1.5 w-14 overflow-hidden rounded-full bg-[var(--steel)]">
                        <div
                          className={cn('h-full rounded-full', row.fillPercent >= 90 ? 'bg-accent-hot' : 'bg-accent')}
                          style={{ width: `${Math.min(100, row.fillPercent)}%` }}
                        />
                      </div>
                    </div>
                  ),
                },
                {
                  key: 'waitlist',
                  header: 'Waitlist',
                  align: 'right',
                  cell: (row) =>
                    row.waitlistCount > 0 ? <Pill tone="warn">{row.waitlistCount}</Pill> : <span className="text-smoke">—</span>,
                },
                {
                  key: 'status',
                  header: 'Status',
                  align: 'right',
                  cell: (row) => <StatusPill status={sessionStatusNames[row.status] ?? '—'} />,
                },
              ]}
            />
          </Panel>
        ))}
      </div>

      <RosterDrawer sessionId={rosterFor} onClose={() => setRosterFor(null)} />
    </>
  )
}

function RosterDrawer({ sessionId, onClose }: { sessionId: number | null; onClose: () => void }) {
  const toast = useToast()
  const { data } = useRoster(sessionId)
  const actions = useSchedulingActions()
  const attendance = useAttendanceActions()
  const { data: trainers } = useTrainerOptions()

  const [cancelOpen, setCancelOpen] = useState(false)
  const [cancelReason, setCancelReason] = useState('Coach unavailable.')
  const [notify, setNotify] = useState(true)
  const [addQuery, setAddQuery] = useState('')
  const [addMatches, setAddMatches] = useState<{ id: number; fullName: string; memberCode: string }[]>([])

  async function search(term: string) {
    setAddQuery(term)
    if (term.trim().length < 2) return setAddMatches([])
    setAddMatches(await attendance.lookup.mutateAsync(term))
  }

  return (
    <Drawer
      open={sessionId !== null}
      onClose={onClose}
      title={data ? `${data.session.formatName} · ${to12Hour(data.session.startTime)}` : 'Roster'}
      description={
        data
          ? `${formatIsoDate(data.session.date)} · ${data.session.branchName} · ${data.session.trainerName}`
          : undefined
      }
      width="lg"
      footer={
        data && data.session.status === 0 ? (
          <>
            <Button variant="ghost" size="sm" onClick={() => setCancelOpen(true)}>
              Cancel class
            </Button>
            <div className="flex-1" />
            <SelectField
              aria-label="Substitute coach"
              value=""
              onChange={(event) =>
                void actions.substitute
                  .mutateAsync({ id: data.session.id, trainerId: event.target.value ? Number(event.target.value) : null })
                  .then(() => toast.success('Coach updated'))
                  .catch((error) => toast.error('Could not substitute', describeErrorText(error)))
              }
              className="w-52"
            >
              <option value="">Assign a substitute…</option>
              {(trainers ?? []).map((trainer) => (
                <option key={trainer.id} value={trainer.id}>
                  {trainer.fullName}
                </option>
              ))}
            </SelectField>
          </>
        ) : null
      }
    >
      {!data ? (
        <Skeleton className="h-64 w-full" />
      ) : (
        <div className="space-y-6">
          <div className="grid grid-cols-3 gap-3">
            <Stat label="Booked" value={`${data.session.bookedCount}/${data.session.capacity}`} />
            <Stat label="Waitlist" value={String(data.session.waitlistCount)} />
            <Stat label="Attended" value={String(data.session.attendedCount)} />
          </div>

          {data.session.status === 3 && (
            <InlineError>Class cancelled — {data.session.cancellationReason}</InlineError>
          )}

          <ul className="space-y-2">
            {data.roster.map((entry) => (
              <li
                key={entry.bookingId}
                className="flex items-center gap-3 rounded-[0.625rem] border border-[var(--hairline)] p-3"
              >
                <Avatar src={entry.photoUrl} name={entry.fullName} size={34} />
                <div className="min-w-0 flex-1">
                  <p className="truncate text-[0.875rem] font-medium">{entry.fullName}</p>
                  <p className="numeric truncate text-[0.75rem] text-smoke">
                    {entry.memberCode}
                    {entry.wasPromoted ? ' · promoted from waitlist' : ''}
                    {entry.noShowsLast90Days > 0 ? ` · ${entry.noShowsLast90Days} no-shows in 90d` : ''}
                  </p>
                </div>
                {entry.status === 1 ? (
                  <Pill tone="warn">#{entry.waitlistPosition}</Pill>
                ) : (
                  <StatusPill status={bookingStatusNames[entry.status] ?? '—'} />
                )}
                {data.session.status !== 3 && (
                  <div className="flex shrink-0 items-center gap-1">
                    <button
                      type="button"
                      onClick={() =>
                        void actions.mark.mutateAsync({ id: entry.bookingId, status: 2 }).then(() => toast.success('Marked attended'))
                      }
                      className="rounded-full p-1.5 text-smoke transition-colors hover:text-success"
                      aria-label="Mark attended"
                    >
                      <Icon name="check" size={15} />
                    </button>
                    <button
                      type="button"
                      onClick={() =>
                        void actions.mark.mutateAsync({ id: entry.bookingId, status: 3 }).then(() => toast.success('Marked no-show'))
                      }
                      className="rounded-full p-1.5 text-smoke transition-colors hover:text-accent-hot"
                      aria-label="Mark no-show"
                    >
                      <Icon name="minus" size={15} />
                    </button>
                    <button
                      type="button"
                      onClick={() =>
                        void actions.cancelBooking
                          .mutateAsync({ id: entry.bookingId, reason: 'Cancelled at the desk' })
                          .then((result) => {
                            const promoted = (result as { promoted: number }).promoted
                            toast.success(
                              'Booking cancelled',
                              promoted > 0 ? `${promoted} member promoted off the waitlist.` : undefined,
                            )
                          })
                      }
                      className="rounded-full p-1.5 text-smoke transition-colors hover:text-accent-hot"
                      aria-label="Cancel booking"
                    >
                      <Icon name="x" size={15} />
                    </button>
                  </div>
                )}
              </li>
            ))}
            {data.roster.length === 0 && (
              <li className="py-8 text-center text-[0.875rem] text-smoke">Nobody booked yet.</li>
            )}
          </ul>

          {cancelOpen && (
            <div className="rounded-[var(--radius-card)] border border-accent-hot/45 bg-[color-mix(in_srgb,var(--accent-hot)_6%,transparent)] p-4">
              <p className="text-[0.875rem] font-medium text-accent-hot">Cancel this class?</p>
              <p className="mt-1 text-[0.8125rem] leading-relaxed text-smoke">
                Every booking is released and any class credit is returned.
              </p>
              <div className="mt-3">
                <TextAreaField
                  label="Reason members will see"
                  rows={2}
                  value={cancelReason}
                  onChange={(event) => setCancelReason(event.target.value)}
                />
              </div>
              <div className="mt-3">
                <Toggle label="Notify members" checked={notify} onChange={setNotify} />
              </div>
              <div className="mt-4 flex justify-end gap-2">
                <Button variant="ghost" size="sm" onClick={() => setCancelOpen(false)}>
                  Keep the class
                </Button>
                <Button
                  variant="danger"
                  size="sm"
                  loading={actions.cancelSession.isPending}
                  onClick={() =>
                    void actions.cancelSession
                      .mutateAsync({ id: data.session.id, reason: cancelReason, notifyMembers: notify })
                      .then((result) => {
                        toast.success(
                          'Class cancelled',
                          `${(result as { cancelledBookings: number }).cancelledBookings} booking(s) released.`,
                        )
                        setCancelOpen(false)
                        onClose()
                      })
                      .catch((error) => toast.error('Could not cancel', describeErrorText(error)))
                  }
                >
                  Cancel class
                </Button>
              </div>
            </div>
          )}

          {data.session.status === 0 && (
            <div className="rounded-[0.625rem] border border-dashed border-[var(--hairline-strong)] p-4">
              <p className="caption mb-2">Add a member from the desk</p>
              <TextField
                placeholder="Name, code or number"
                value={addQuery}
                onChange={(event) => void search(event.target.value)}
                aria-label="Search members"
              />
              {addMatches.length > 0 && (
                <ul className="mt-2 space-y-1">
                  {addMatches.map((match) => (
                    <li key={match.id}>
                      <button
                        type="button"
                        onClick={() =>
                          void actions.book
                            .mutateAsync({ sessionId: data.session.id, memberId: match.id, allowWaitlist: true })
                            .then((result) => {
                              toast.success(
                                (result as { status: string }).status === 'Waitlisted' ? 'Added to the waitlist' : 'Booked',
                              )
                              setAddQuery('')
                              setAddMatches([])
                            })
                            .catch((error) => toast.error('Could not book', describeErrorText(error)))
                        }
                        className="flex w-full items-center justify-between gap-3 rounded-[0.5rem] border border-[var(--hairline)] px-3 py-2 text-left text-[0.8125rem] transition-colors hover:border-[var(--accent-line)]"
                      >
                        <span>{match.fullName}</span>
                        <span className="numeric text-smoke">{match.memberCode}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </div>
      )}

    </Drawer>
  )
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[0.625rem] border border-[var(--hairline)] p-3 text-center">
      <p className="caption">{label}</p>
      <p className="numeric mt-1.5 text-[1.25rem] font-semibold">{value}</p>
    </div>
  )
}

/* ---------------------------------------------------------------- formats */

function FormatsTab() {
  const toast = useToast()
  const { data: formats, isLoading } = useClassFormats()
  const actions = useSchedulingActions()
  const [editing, setEditing] = useState<number | 'new' | null>(null)

  const current = typeof editing === 'number' ? (formats ?? []).find((f) => f.id === editing) : undefined
  const [form, setForm] = useState<Record<string, string | boolean>>({})
  const [signature, setSignature] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const key = editing === null ? null : String(editing)
  if (key !== null && signature !== key) {
    setSignature(key)
    setError(null)
    setForm({
      name: current?.name ?? '',
      slug: current?.slug ?? '',
      shortDescription: current?.shortDescription ?? '',
      description: current?.description ?? '',
      defaultDurationMinutes: String(current?.defaultDurationMinutes ?? 45),
      defaultCapacity: String(current?.defaultCapacity ?? 20),
      level: String(current?.level ?? 0),
      intensity: String(current?.intensity ?? 1),
      estimatedCalories: String(current?.estimatedCalories ?? 400),
      coverImageUrl: current?.coverImageUrl ?? '',
      iconKey: current?.iconKey ?? '',
      tags: current?.tags ?? '',
      showOnWebsite: current?.showOnWebsite ?? true,
      isActive: current?.isActive ?? true,
      displayOrder: String(current?.displayOrder ?? 0),
    })
  }

  async function submit() {
    setError(null)
    const body = {
      name: String(form.name),
      slug: String(form.slug || form.name).toLowerCase().replace(/[^a-z0-9]+/g, '-'),
      shortDescription: String(form.shortDescription),
      description: String(form.description),
      defaultDurationMinutes: Number(form.defaultDurationMinutes),
      defaultCapacity: Number(form.defaultCapacity),
      level: Number(form.level),
      intensity: Number(form.intensity),
      estimatedCalories: Number(form.estimatedCalories),
      coverImageUrl: form.coverImageUrl || undefined,
      iconKey: form.iconKey || undefined,
      tags: form.tags || undefined,
      showOnWebsite: Boolean(form.showOnWebsite),
      isActive: Boolean(form.isActive),
      displayOrder: Number(form.displayOrder),
    }

    try {
      if (editing === 'new') await actions.createFormat.mutateAsync(body)
      else if (typeof editing === 'number') await actions.updateFormat.mutateAsync({ id: editing, body })
      toast.success('Format saved')
      setEditing(null)
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <>
      <Panel
        padded={false}
        title="Class format library"
        description="Shared across every branch — edit a format once and every timetable slot that uses it follows."
        actions={
          <Button size="sm" icon="plus" onClick={() => setEditing('new')}>
            New format
          </Button>
        }
      >
        <DataTable
          rows={formats ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          onRowClick={(row) => setEditing(row.id)}
          emptyHeadline="No class formats"
          columns={[
            {
              key: 'name',
              header: 'Format',
              cell: (row) => (
                <div className="flex items-center gap-3">
                  {row.coverImageUrl && (
                    <img src={row.coverImageUrl} alt="" className="graded size-9 rounded-[0.5rem] object-cover" loading="lazy" />
                  )}
                  <div className="min-w-0">
                    <p className="truncate font-medium">{row.name}</p>
                    <p className="truncate text-[0.75rem] text-smoke">{row.shortDescription}</p>
                  </div>
                </div>
              ),
            },
            { key: 'level', header: 'Level', cell: (row) => <Pill>{classLevelNames[row.level]}</Pill> },
            { key: 'intensity', header: 'Intensity', cell: (row) => <Pill tone="muted">{classIntensityNames[row.intensity]}</Pill> },
            {
              key: 'defaults',
              header: 'Defaults',
              align: 'right',
              cell: (row) => (
                <span className="numeric text-[0.8125rem] text-smoke">
                  {row.defaultDurationMinutes}m · {row.defaultCapacity} spots
                </span>
              ),
            },
            { key: 'slots', header: 'Weekly slots', align: 'right', cell: (row) => <span className="numeric">{row.weeklySlots}</span> },
            {
              key: 'flags',
              header: '',
              align: 'right',
              cell: (row) => (
                <div className="flex justify-end gap-1.5">
                  {!row.isActive && <Pill tone="muted">retired</Pill>}
                  {!row.showOnWebsite && <Pill tone="muted">hidden</Pill>}
                </div>
              ),
            },
          ]}
        />
      </Panel>

      <Drawer
        open={editing !== null}
        onClose={() => setEditing(null)}
        title={editing === 'new' ? 'New class format' : `Edit ${current?.name ?? 'format'}`}
        description="Formats are network-wide. Duration and capacity here become the defaults for new timetable slots."
        footer={
          <>
            <Button variant="ghost" size="sm" onClick={() => setEditing(null)}>
              Cancel
            </Button>
            <Button size="sm" icon="check" onClick={() => void submit()} loading={actions.createFormat.isPending || actions.updateFormat.isPending}>
              Save format
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          {error && <InlineError>{error}</InlineError>}
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField label="Name" required value={String(form.name ?? '')} onChange={(event) => setForm((c) => ({ ...c, name: event.target.value }))} />
            <TextField label="Slug" hint="Used in URLs and the timetable filter." value={String(form.slug ?? '')} onChange={(event) => setForm((c) => ({ ...c, slug: event.target.value }))} />
            <SelectField label="Level" value={String(form.level ?? 0)} onChange={(event) => setForm((c) => ({ ...c, level: event.target.value }))}>
              {classLevelNames.map((name, index) => (
                <option key={name} value={index}>
                  {name === 'AllLevels' ? 'All levels' : name}
                </option>
              ))}
            </SelectField>
            <SelectField label="Intensity" value={String(form.intensity ?? 1)} onChange={(event) => setForm((c) => ({ ...c, intensity: event.target.value }))}>
              {classIntensityNames.map((name, index) => (
                <option key={name} value={index}>
                  {name}
                </option>
              ))}
            </SelectField>
            <TextField label="Default duration (min)" type="number" value={String(form.defaultDurationMinutes ?? '')} onChange={(event) => setForm((c) => ({ ...c, defaultDurationMinutes: event.target.value }))} />
            <TextField label="Default capacity" type="number" value={String(form.defaultCapacity ?? '')} onChange={(event) => setForm((c) => ({ ...c, defaultCapacity: event.target.value }))} />
            <TextField label="Estimated calories" type="number" value={String(form.estimatedCalories ?? '')} onChange={(event) => setForm((c) => ({ ...c, estimatedCalories: event.target.value }))} />
            <TextField label="Icon key" hint="A key from the inline SVG registry — barbell, kettlebell, lotus…" value={String(form.iconKey ?? '')} onChange={(event) => setForm((c) => ({ ...c, iconKey: event.target.value }))} />
          </div>
          <TextField label="Short description" hint="One line on the class card." value={String(form.shortDescription ?? '')} onChange={(event) => setForm((c) => ({ ...c, shortDescription: event.target.value }))} />
          <TextAreaField label="Full description" rows={4} value={String(form.description ?? '')} onChange={(event) => setForm((c) => ({ ...c, description: event.target.value }))} />
          <TextField label="Cover image URL" value={String(form.coverImageUrl ?? '')} onChange={(event) => setForm((c) => ({ ...c, coverImageUrl: event.target.value }))} />
          <TextField label="Tags" hint="Comma separated — these become the timetable filter pills." value={String(form.tags ?? '')} onChange={(event) => setForm((c) => ({ ...c, tags: event.target.value }))} />
          <div className="space-y-3 rounded-[0.625rem] border border-[var(--hairline)] p-4">
            <Toggle label="Show on the website" checked={Boolean(form.showOnWebsite)} onChange={(next) => setForm((c) => ({ ...c, showOnWebsite: next }))} />
            <Toggle label="Active" checked={Boolean(form.isActive)} onChange={(next) => setForm((c) => ({ ...c, isActive: next }))} />
          </div>
        </div>
      </Drawer>
    </>
  )
}

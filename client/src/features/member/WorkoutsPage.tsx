import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Badge } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, Skeleton } from '@/components/ui/Skeleton'
import { cn, formatDate, todayIso } from '@/lib/utils'
import { describeErrorText } from '@/features/admin/lib/format'
import { useDeleteSet, useLogSet, useProgram, useWorkoutHistory } from './lib/portal-api'
import { shareText, shareToWhatsApp } from './lib/share'
import { RestTimer } from './components/RestTimer'
import { PrBanner } from './components/PrBanner'
import { Field, InlineNote, Panel, PillToggle, PortalHeading, StatTile } from './components/ui'
import type { PrCelebration, ProgramDay, ProgramExercise, SetRow } from './lib/types'

/**
 * Workouts (Module 3 — Workouts): the assigned programme, the day you are about to
 * train, and the set logger with a rest timer and PR detection.
 *
 * The logger is built for a phone held in one hand between sets: the weight and rep
 * fields pre-fill from the last set you did, logging is one tap, and the rest timer
 * starts itself the moment a set lands.
 */
export function WorkoutsPage() {
  const { data, isLoading, isError, error } = useProgram()
  const [params, setParams] = useSearchParams()
  const [celebration, setCelebration] = useState<PrCelebration | null>(null)

  const requestedDayId = Number(params.get('day')) || null

  // Default to the first training day; a rest day is not where anyone opens this screen.
  const activeDay = useMemo<ProgramDay | null>(() => {
    const list = data?.days ?? []
    if (list.length === 0) return null
    return list.find((day) => day.id === requestedDayId) ?? list.find((day) => !day.isRestDay) ?? list[0]
  }, [data, requestedDayId])

  if (isLoading) {
    return (
      <div>
        <PortalHeading eyebrow="Training" title="Your programme" />
        <Skeleton className="h-96" />
      </div>
    )
  }

  if (isError || !data) {
    const notAssigned = (error as { response?: { status?: number } })?.response?.status === 404
    return (
      <div>
        <PortalHeading eyebrow="Training" title="Your programme" />
        <EmptyState
          icon="barbell"
          headline={notAssigned ? 'No programme assigned yet' : 'We could not load your programme'}
          body={
            notAssigned
              ? 'Ask any coach on the floor and they will write one against your goal and your injury history. Your logged sets are kept either way, so nothing you do today is lost.'
              : 'The connection dropped. Reload and it should come straight back.'
          }
          actionLabel="Back to home"
          actionTo="/portal"
        />
        <div className="mt-8">
          <History />
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-8">
      <PortalHeading
        eyebrow={`Week ${data.weekNumber} of ${data.durationWeeks} · ${data.authorName}`}
        title={data.name}
        lead={data.description ?? undefined}
      />

      {celebration && (
        <PrBanner
          celebration={celebration}
          onDismiss={() => setCelebration(null)}
          onShare={() => shareText(celebration.shareText)}
          onWhatsApp={() => shareToWhatsApp(celebration.shareText)}
        />
      )}

      <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
        <StatTile label="Goal" value={data.goal ?? 'General'} icon="flag" />
        <StatTile label="Days a week" value={data.daysPerWeek} icon="calendar" />
        <StatTile
          label="Coach"
          value={data.trainerName ?? 'The gym'}
          sub={data.startsOn ? `Started ${formatDate(data.startsOn)}` : undefined}
          icon="users"
        />
        <StatTile
          label="Block ends"
          value={data.endsOn ? formatDate(data.endsOn) : '—'}
          sub="Then your coach writes the next one"
          icon="clock"
        />
      </div>

      <div className="-mx-[var(--gutter)] overflow-x-auto px-[var(--gutter)] pb-1">
        <div className="flex gap-2" role="tablist" aria-label="Programme days">
          {data.days.map((day) => {
            const active = day.id === activeDay?.id
            return (
              <button
                key={day.id}
                type="button"
                role="tab"
                aria-selected={active}
                onClick={() => setParams({ day: String(day.id) }, { replace: true })}
                className={cn(
                  'flex min-h-[4.5rem] w-[9.5rem] shrink-0 flex-col justify-center gap-1 rounded-[var(--radius-card)] border px-4 text-left transition-colors duration-200',
                  active
                    ? 'border-accent bg-[var(--accent-soft)]'
                    : 'border-[var(--hairline)] hover:border-bone/30',
                )}
              >
                <span className={cn('caption text-[0.5625rem]', active && 'text-accent')}>Day {day.dayIndex}</span>
                <span className={cn('truncate text-[0.9375rem]', active ? 'text-bone' : 'text-smoke')}>
                  {day.title}
                </span>
                <span className="text-[0.6875rem] text-smoke/75">
                  {day.isRestDay ? 'Rest' : `${day.exerciseCount} lifts · ~${day.estimatedMinutes} min`}
                </span>
              </button>
            )
          })}
        </div>
      </div>

      {activeDay && <DayView day={activeDay} onPr={setCelebration} />}

      <History />
    </div>
  )
}

/* ---------------------------------------------------------------- day */

function DayView({ day, onPr }: { day: ProgramDay; onPr: (celebration: PrCelebration) => void }) {
  const [resting, setResting] = useState<{ seconds: number; label: string } | null>(null)

  if (day.isRestDay) {
    return (
      <Panel title={day.title}>
        <div className="flex items-start gap-4">
          <span className="grid size-11 shrink-0 place-items-center rounded-full border border-[var(--hairline)] text-accent">
            <Icon name="lotus" size={20} />
          </span>
          <div>
            <p className="text-[0.9375rem] text-bone">{day.focus ?? 'Recovery'}</p>
            <p className="measure mt-2 text-[0.875rem] leading-relaxed text-smoke">
              {day.notes ?? 'Walk, stretch, sleep. Recovery is where the adaptation happens.'}
            </p>
          </div>
        </div>
      </Panel>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="display-m text-[1.375rem] text-bone">{day.title}</h2>
          <p className="mt-1.5 text-[0.8125rem] text-smoke">
            {day.focus ? `${day.focus} · ` : ''}
            {day.totalSets} working sets · about {day.estimatedMinutes} minutes
            {day.lastPerformedOn ? ` · last done ${formatDate(day.lastPerformedOn)}` : ''}
          </p>
        </div>
      </div>

      {resting && (
        <RestTimer
          seconds={resting.seconds}
          label={resting.label}
          onDismiss={() => setResting(null)}
        />
      )}

      <div className="space-y-4">
        {day.exercises.map((exercise, index) => (
          <ExerciseCard
            key={exercise.id}
            exercise={exercise}
            position={index + 1}
            onPr={onPr}
            onLogged={(seconds, label) => setResting({ seconds, label })}
          />
        ))}
      </div>
    </div>
  )
}

/* ---------------------------------------------------------------- exercise */

function ExerciseCard({
  exercise,
  position,
  onPr,
  onLogged,
}: {
  exercise: ProgramExercise
  position: number
  onPr: (celebration: PrCelebration) => void
  onLogged: (seconds: number, label: string) => void
}) {
  const log = useLogSet()
  const remove = useDeleteSet()
  const [open, setOpen] = useState(exercise.todaySets.length > 0 || position === 1)
  const [error, setError] = useState<string | null>(null)

  // Pre-fill from what they did last time; the number they want is almost never zero.
  const suggested = exercise.todaySets.at(-1) ?? exercise.lastSession.at(-1)
  const [weight, setWeight] = useState(() => String(suggested?.weightKg ?? exercise.targetWeightKg ?? ''))
  const [reps, setReps] = useState(() => String(suggested?.reps ?? firstRep(exercise.repScheme)))
  const [rpe, setRpe] = useState('')

  useEffect(() => {
    const latest = exercise.todaySets.at(-1)
    if (latest) {
      setWeight(String(latest.weightKg))
      setReps(String(latest.reps))
    }
  }, [exercise.todaySets])

  const nextSetNumber = exercise.todaySets.length + 1
  const complete = exercise.todaySets.length >= exercise.sets

  function submit() {
    setError(null)
    const weightValue = Number(weight)
    const repsValue = Number(reps)
    if (!Number.isFinite(repsValue) || repsValue < 1) {
      setError('How many reps?')
      return
    }

    log.mutate(
      {
        exerciseId: exercise.exerciseId,
        programExerciseId: exercise.id,
        setNumber: nextSetNumber,
        reps: repsValue,
        weightKg: Number.isFinite(weightValue) ? weightValue : 0,
        rpe: rpe ? Number(rpe) : undefined,
        performedOn: todayIso(),
      },
      {
        onSuccess: (result) => {
          if (result.celebration) onPr(result.celebration)
          onLogged(exercise.restSeconds, `${exercise.name} · set ${nextSetNumber} of ${exercise.sets}`)
        },
        onError: (failure) => setError(describeErrorText(failure)),
      },
    )
  }

  return (
    <article
      className={cn(
        'overflow-hidden rounded-[var(--radius-card)] border bg-carbon',
        complete ? 'border-success/40' : 'border-[var(--hairline)]',
      )}
    >
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        className="flex w-full items-center gap-4 px-5 py-4 text-left transition-colors hover:bg-steel/30"
      >
        <span
          className={cn(
            'numeric grid size-9 shrink-0 place-items-center rounded-full border text-[0.8125rem] font-semibold',
            complete ? 'border-success/50 text-success' : 'border-[var(--hairline-strong)] text-smoke',
          )}
        >
          {complete ? <Icon name="check" size={16} strokeWidth={2.2} /> : position}
        </span>

        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-center gap-2">
            <span className="display-m text-[1.0625rem] text-bone">{exercise.name}</span>
            {exercise.supersetGroup && <Badge tone="accent">Superset {exercise.supersetGroup}</Badge>}
            {exercise.isStrengthTracked && <Badge>PR tracked</Badge>}
          </span>
          <span className="mt-1 block text-[0.8125rem] text-smoke">
            {exercise.sets} × {exercise.repScheme} · {exercise.restSeconds}s rest
            {exercise.lastSessionOn ? ` · last ${formatDate(exercise.lastSessionOn)}` : ''}
          </span>
        </span>

        <span className="flex shrink-0 items-center gap-3">
          <span className="numeric text-[0.8125rem] text-smoke">
            {exercise.todaySets.length}/{exercise.sets}
          </span>
          <Icon name="chevron-down" size={16} className={cn('text-smoke transition-transform', open && 'rotate-180')} />
        </span>
      </button>

      {open && (
        <div className="border-t border-[var(--hairline)] px-5 py-5">
          {exercise.cues && (
            <p className="measure mb-4 text-[0.8125rem] leading-relaxed text-smoke">
              <span className="text-accent">Cue · </span>
              {exercise.cues}
            </p>
          )}

          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,22rem)]">
            <div>
              <h4 className="caption mb-2.5">Today</h4>
              {exercise.todaySets.length === 0 ? (
                <p className="text-[0.8125rem] text-smoke">Nothing logged yet. First set below.</p>
              ) : (
                <ul className="space-y-1.5">
                  {exercise.todaySets.map((set) => (
                    <SetLine key={set.id} set={set} onRemove={() => remove.mutate(set.id)} />
                  ))}
                </ul>
              )}

              {exercise.lastSession.length > 0 && (
                <>
                  <h4 className="caption mb-2.5 mt-5">
                    Last time{exercise.lastSessionOn ? ` · ${formatDate(exercise.lastSessionOn)}` : ''}
                  </h4>
                  <ul className="space-y-1.5 opacity-70">
                    {exercise.lastSession.map((set) => (
                      <SetLine key={set.id} set={set} muted />
                    ))}
                  </ul>
                </>
              )}

              {exercise.bestE1Rm != null && (
                <p className="numeric mt-4 text-[0.75rem] text-smoke">
                  Best estimated 1RM {exercise.bestE1Rm} kg
                </p>
              )}
            </div>

            <div className="rounded-[var(--radius-card)] border border-[var(--hairline)] bg-ink/40 p-4">
              <p className="caption mb-3">Log set {nextSetNumber}</p>
              <div className="grid grid-cols-3 gap-3">
                <Field label="kg">
                  <input
                    className="field-input"
                    inputMode="decimal"
                    value={weight}
                    onChange={(event) => setWeight(event.target.value)}
                    aria-label="Weight in kilograms"
                  />
                </Field>
                <Field label="Reps">
                  <input
                    className="field-input"
                    inputMode="numeric"
                    value={reps}
                    onChange={(event) => setReps(event.target.value)}
                    aria-label="Repetitions"
                  />
                </Field>
                <Field label="RPE">
                  <input
                    className="field-input"
                    inputMode="numeric"
                    placeholder="—"
                    value={rpe}
                    onChange={(event) => setRpe(event.target.value)}
                    aria-label="Rate of perceived exertion"
                  />
                </Field>
              </div>

              <Button fullWidth className="mt-4" loading={log.isPending} onClick={submit}>
                {complete ? 'Log an extra set' : `Log set ${nextSetNumber}`}
              </Button>

              {error && (
                <InlineNote tone="danger" icon="x" className="mt-3">
                  {error}
                </InlineNote>
              )}
              <p className="mt-3 text-[0.6875rem] leading-relaxed text-smoke/75">
                The rest timer starts itself on a logged set. Records are compared on estimated 1RM, so a heavier set
                for fewer reps and a lighter one for more are judged on the same scale.
              </p>
            </div>
          </div>
        </div>
      )}
    </article>
  )
}

function SetLine({ set, muted, onRemove }: { set: SetRow; muted?: boolean; onRemove?: () => void }) {
  return (
    <li
      className={cn(
        'flex items-center justify-between gap-3 rounded-full border px-3.5 py-2 text-[0.8125rem]',
        set.isPersonalRecord ? 'border-[var(--accent-line)] bg-[var(--accent-soft)]' : 'border-[var(--hairline)]',
        muted && 'border-dashed',
      )}
    >
      <span className="numeric flex items-center gap-2.5 text-bone">
        <span className="text-smoke">#{set.setNumber}</span>
        {set.weightKg > 0 ? `${set.weightKg} kg × ${set.reps}` : `${set.reps} reps`}
        {set.rpe && <span className="text-smoke">RPE {set.rpe}</span>}
        {set.isPersonalRecord && (
          <span className="inline-flex items-center gap-1 text-accent">
            <Icon name="trophy" size={12} />
            PR
          </span>
        )}
      </span>
      {onRemove && (
        <button
          type="button"
          onClick={onRemove}
          aria-label={`Remove set ${set.setNumber}`}
          className="grid size-7 place-items-center rounded-full text-smoke transition-colors hover:bg-steel hover:text-accent-hot"
        >
          <Icon name="x" size={13} />
        </button>
      )}
    </li>
  )
}

/* ---------------------------------------------------------------- history */

function History() {
  const { data, isLoading } = useWorkoutHistory()
  const [range, setRange] = useState<'recent' | 'all'>('recent')

  if (isLoading) return <Skeleton className="h-40" />
  if (!data || data.length === 0) {
    return (
      <Panel title="Training history">
        <EmptyState
          icon="barbell"
          headline="No sets logged yet"
          body="Log your first set above and this becomes a record of every session you have trained here."
        />
      </Panel>
    )
  }

  const rows = range === 'recent' ? data.slice(0, 8) : data

  return (
    <Panel
      title="Training history"
      description="Every session you have logged, newest first."
      actions={
        data.length > 8 ? (
          <PillToggle
            ariaLabel="How much history"
            value={range}
            onChange={(value) => setRange(value as 'recent' | 'all')}
            options={[
              { value: 'recent', label: 'Recent' },
              { value: 'all', label: `All ${data.length}` },
            ]}
          />
        ) : undefined
      }
      padded={false}
    >
      <ul className="divide-y divide-[var(--hairline)]">
        {rows.map((row) => (
          <li key={row.date} className="flex flex-wrap items-center justify-between gap-3 px-5 py-3.5">
            <div className="min-w-0">
              <p className="text-[0.875rem] text-bone">{formatDate(row.date)}</p>
              <p className="mt-0.5 truncate text-[0.75rem] text-smoke">{row.exercises.join(' · ')}</p>
            </div>
            <div className="flex items-center gap-4 text-[0.8125rem]">
              <span className="numeric text-smoke">{row.sets} sets</span>
              <span className="numeric text-smoke">{Math.round(row.volume).toLocaleString('en-IN')} kg</span>
              {row.personalRecords > 0 && (
                <Badge tone="accent" icon="trophy">
                  {row.personalRecords} PR
                </Badge>
              )}
            </div>
          </li>
        ))}
      </ul>
    </Panel>
  )
}

/* ---------------------------------------------------------------- helpers */

/** "8-12" → 8, "10 each" → 10, "45s" → 10 as a sane fallback. */
function firstRep(scheme: string): number {
  const match = scheme.match(/\d+/)
  const value = match ? Number(match[0]) : 10
  return value > 0 && value <= 100 ? value : 10
}


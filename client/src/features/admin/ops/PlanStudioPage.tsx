import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { cn } from '@/lib/utils'
import {
  DataTable,
  FilterChip,
  Hint,
  PageHeader,
  Panel,
  Pill,
  SelectField,
  TextAreaField,
  TextField,
  Toggle,
} from '../components/ui'
import { Drawer } from '../components/overlays'
import { describeErrorText, relativeTime } from '../lib/format'
import {
  useDietDraft,
  useDiscardPlan,
  useGenerateDiet,
  useGenerateWorkout,
  usePlanEngine,
  usePlanQueue,
  usePublishPlan,
  useUpdateDietDraft,
  useUpdateWorkoutDraft,
  useWorkoutDraft,
  type DietDraft,
  type WorkoutDraft,
} from '../lib/module4-api'

/**
 * The plan studio (Module 4.2): generate a training block or an eating plan for a member,
 * read it, adjust it, publish it.
 *
 * Nothing here reaches a member until someone presses Publish. That is the whole point of
 * the screen — the generator writes a draft, a human signs it off, and the plan the member
 * follows always has a name attached to it.
 */
export function PlanStudioPage() {
  const params = useParams<{ memberId?: string }>()
  const memberId = params.memberId ? Number(params.memberId) : undefined

  const { data: engine } = usePlanEngine()
  const { data: queue, isLoading } = usePlanQueue(memberId)
  const [tab, setTab] = useState<'workouts' | 'diets'>('workouts')
  const [openWorkout, setOpenWorkout] = useState<number | null>(null)
  const [openDiet, setOpenDiet] = useState<number | null>(null)
  const [generateFor, setGenerateFor] = useState<number | null>(memberId ?? null)

  return (
    <>
      <PageHeader
        eyebrow="Training"
        title="Plan studio"
        lead="Generate a programme or a plate for a member, review it, then publish. Members only ever see what has been published."
        actions={
          memberId ? (
            <Button size="sm" icon="sparkles" onClick={() => setGenerateFor(memberId)}>
              Generate for this member
            </Button>
          ) : undefined
        }
      >
        {engine && (
          <div
            className={cn(
              'flex flex-wrap items-center gap-3 rounded-[var(--radius-input)] border p-4',
              engine.aiAvailable ? 'border-[var(--accent)]/40' : 'border-[var(--hairline)]',
            )}
          >
            <Icon
              name={engine.aiAvailable ? 'sparkles' : 'gauge'}
              size={18}
              className={engine.aiAvailable ? 'text-accent' : 'text-smoke'}
              aria-hidden="true"
            />
            <div className="min-w-0">
              <p className="text-[0.875rem] font-medium">
                {engine.aiAvailable ? 'Claude is writing drafts' : 'Rule-based programmer is writing drafts'}
              </p>
              <p className="text-[0.8125rem] text-smoke">{engine.description}</p>
            </div>
            <Pill tone={engine.aiAvailable ? 'accent' : 'neutral'}>{engine.engine}</Pill>
          </div>
        )}
      </PageHeader>

      <Panel className="mb-5">
        <div className="flex flex-wrap gap-2">
          <FilterChip active={tab === 'workouts'} onClick={() => setTab('workouts')} count={queue?.workouts.length}>
            Training drafts
          </FilterChip>
          <FilterChip active={tab === 'diets'} onClick={() => setTab('diets')} count={queue?.diets.length}>
            Eating drafts
          </FilterChip>
        </div>
      </Panel>

      <Panel padded={false}>
        {tab === 'workouts' ? (
          <DataTable
            rows={queue?.workouts ?? []}
            rowKey={(row) => row.id}
            loading={isLoading}
            onRowClick={(row) => setOpenWorkout(row.id)}
            emptyHeadline="No training drafts waiting"
            emptyBody="Open a member and press Generate, or write a programme by hand from their profile."
            columns={[
              {
                key: 'member',
                header: 'Member',
                cell: (row) => (
                  <div className="min-w-0">
                    <p className="truncate font-medium">{row.memberName}</p>
                    <p className="truncate text-[0.75rem] text-smoke">{row.memberCode}</p>
                  </div>
                ),
              },
              { key: 'name', header: 'Programme', cell: (row) => row.name },
              {
                key: 'shape',
                header: 'Shape',
                width: '11rem',
                cell: (row) => (
                  <span className="text-[0.8125rem] text-smoke">
                    {row.daysPerWeek} days · {row.durationWeeks} weeks · {row.days} sessions
                  </span>
                ),
              },
              {
                key: 'author',
                header: 'Source',
                width: '8rem',
                cell: (row) => (
                  <Pill tone={row.author === 1 ? 'accent' : 'neutral'}>
                    {row.author === 1 ? 'AI draft' : 'Rule-based'}
                  </Pill>
                ),
              },
              {
                key: 'created',
                header: 'Created',
                width: '8rem',
                cell: (row) => <span className="text-[0.8125rem] text-smoke">{relativeTime(row.createdAtUtc)}</span>,
              },
            ]}
          />
        ) : (
          <DataTable
            rows={queue?.diets ?? []}
            rowKey={(row) => row.id}
            loading={isLoading}
            onRowClick={(row) => setOpenDiet(row.id)}
            emptyHeadline="No eating drafts waiting"
            emptyBody="Generate one from a member's profile — it uses their latest scan weight and their goal."
            columns={[
              {
                key: 'member',
                header: 'Member',
                cell: (row) => (
                  <div className="min-w-0">
                    <p className="truncate font-medium">{row.memberName}</p>
                    <p className="truncate text-[0.75rem] text-smoke">{row.memberCode}</p>
                  </div>
                ),
              },
              { key: 'name', header: 'Plan', cell: (row) => row.name },
              {
                key: 'macros',
                header: 'Targets',
                width: '13rem',
                cell: (row) => (
                  <span className="text-[0.8125rem] tabular-nums text-smoke">
                    {row.targetCalories} kcal · {row.proteinGrams}g protein
                  </span>
                ),
              },
              {
                key: 'author',
                header: 'Source',
                width: '8rem',
                cell: (row) => (
                  <Pill tone={row.author === 1 ? 'accent' : 'neutral'}>
                    {row.author === 1 ? 'AI draft' : 'Rule-based'}
                  </Pill>
                ),
              },
              {
                key: 'created',
                header: 'Created',
                width: '8rem',
                cell: (row) => <span className="text-[0.8125rem] text-smoke">{relativeTime(row.createdAtUtc)}</span>,
              },
            ]}
          />
        )}
      </Panel>

      <GenerateDrawer
        memberId={generateFor}
        onClose={() => setGenerateFor(null)}
        onGenerated={(kind, id) => (kind === 'workout' ? setOpenWorkout(id) : setOpenDiet(id))}
      />
      <WorkoutReviewDrawer id={openWorkout} onClose={() => setOpenWorkout(null)} />
      <DietReviewDrawer id={openDiet} onClose={() => setOpenDiet(null)} />
    </>
  )
}

/* ---------------------------------------------------------------- generate */

function GenerateDrawer({
  memberId,
  onClose,
  onGenerated,
}: {
  memberId: number | null
  onClose: () => void
  onGenerated: (kind: 'workout' | 'diet', id: number) => void
}) {
  const [kind, setKind] = useState<'workout' | 'diet'>('workout')
  const [goal, setGoal] = useState('')
  const [daysPerWeek, setDaysPerWeek] = useState(4)
  const [durationWeeks, setDurationWeeks] = useState(6)
  const [level, setLevel] = useState<number | ''>('')
  const [isVegetarian, setIsVegetarian] = useState(false)
  const [trainerNote, setTrainerNote] = useState('')
  const [error, setError] = useState<string | null>(null)

  const workout = useGenerateWorkout()
  const diet = useGenerateDiet()
  const pending = workout.isPending || diet.isPending

  const run = () => {
    if (memberId == null) return
    setError(null)
    const payload = {
      memberId,
      goal: goal.trim() || undefined,
      level: level === '' ? undefined : Number(level),
      daysPerWeek,
      durationWeeks,
      isVegetarian,
      trainerNote: trainerNote.trim() || undefined,
    }
    const options = {
      onSuccess: (draft: { id: number }) => {
        onClose()
        onGenerated(kind, draft.id)
      },
      onError: (err: unknown) => setError(describeErrorText(err)),
    }
    if (kind === 'workout') workout.mutate(payload, options)
    else diet.mutate(payload, options)
  }

  return (
    <Drawer
      open={memberId != null}
      onClose={onClose}
      title="Generate a draft"
      description="Everything below is a starting point — the member's own history, injuries and latest scan are read automatically."
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button icon="sparkles" onClick={run} disabled={pending}>
            {pending ? 'Writing the draft…' : 'Generate'}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="flex gap-2">
          <FilterChip active={kind === 'workout'} onClick={() => setKind('workout')}>
            Training programme
          </FilterChip>
          <FilterChip active={kind === 'diet'} onClick={() => setKind('diet')}>
            Eating plan
          </FilterChip>
        </div>

        <TextField
          label="Goal"
          value={goal}
          onChange={(event) => setGoal(event.target.value)}
          placeholder="Leave blank to use the goal on their profile"
          hint="Fat loss, strength, muscle, endurance — plain words are fine."
        />

        {kind === 'workout' && (
          <div className="grid gap-4 sm:grid-cols-3">
            <TextField
              label="Days a week"
              type="number"
              min={2}
              max={6}
              value={daysPerWeek}
              onChange={(event) => setDaysPerWeek(Number(event.target.value))}
            />
            <TextField
              label="Block length"
              type="number"
              min={2}
              max={16}
              value={durationWeeks}
              onChange={(event) => setDurationWeeks(Number(event.target.value))}
              hint="Weeks"
            />
            <SelectField
              label="Experience"
              value={String(level)}
              onChange={(event) => setLevel(event.target.value === '' ? '' : Number(event.target.value))}
            >
              <option value="">From their history</option>
              <option value="1">Beginner</option>
              <option value="2">Intermediate</option>
              <option value="3">Advanced</option>
            </SelectField>
          </div>
        )}

        {kind === 'diet' && (
          <Toggle label="Vegetarian" checked={isVegetarian} onChange={setIsVegetarian} />
        )}

        <TextAreaField
          label="Note for the generator"
          rows={3}
          value={trainerNote}
          onChange={(event) => setTrainerNote(event.target.value)}
          placeholder="Anything the history will not show — a competition date, a shift pattern, a knee that flares up on Fridays."
        />

        <Hint icon="lock">
          The draft lands unpublished. Nobody sees it until you read it and press Publish.
        </Hint>

        {error && (
          <p className="rounded-[var(--radius-input)] border border-[var(--accent-hot)]/40 p-3 text-[0.8125rem] text-[var(--accent-hot)]" role="alert">
            {error}
          </p>
        )}
      </div>
    </Drawer>
  )
}

/* ---------------------------------------------------------------- review */

function DraftProvenance({ draft }: { draft: WorkoutDraft | DietDraft }) {
  return (
    <div className="mb-5 rounded-[var(--radius-input)] border border-[var(--hairline)] p-4">
      <div className="flex flex-wrap items-center gap-2">
        <Pill tone={draft.author === 1 ? 'accent' : 'neutral'}>{draft.authorLabel}</Pill>
        {draft.engine && <span className="text-[0.75rem] text-smoke">{draft.engine}</span>}
      </div>
      {/* When the model was unreachable the draft says so, rather than quietly looking
          identical to one Claude wrote. */}
      {draft.fallbackReason && (
        <p className="mt-2.5 flex items-start gap-2 text-[0.8125rem] text-[var(--accent)]">
          <Icon name="sparkles" size={14} className="mt-0.5 shrink-0" aria-hidden="true" />
          AI unavailable, rule-based plan used — {draft.fallbackReason}
        </p>
      )}
      {draft.memberName && (
        <p className="mt-2 text-[0.8125rem] text-smoke">
          For{' '}
          <Link to={`/admin/members/${draft.memberId}`} className="hover:text-accent">
            {draft.memberName}
          </Link>{' '}
          · {draft.memberCode} · {relativeTime(draft.createdAtUtc)}
        </p>
      )}
    </div>
  )
}

function WorkoutReviewDrawer({ id, onClose }: { id: number | null; onClose: () => void }) {
  const { data: draft, isLoading } = useWorkoutDraft(id)
  const publish = usePublishPlan()
  const discard = useDiscardPlan()
  const update = useUpdateWorkoutDraft()
  const [message, setMessage] = useState<string | null>(null)

  const published = draft?.status === 2

  return (
    <Drawer
      open={id != null}
      onClose={onClose}
      width="xl"
      title={draft?.name ?? 'Training draft'}
      description={draft?.description ?? undefined}
      footer={
        <>
          <Button
            variant="ghost"
            onClick={() => {
              if (id == null) return
              discard.mutate(
                { kind: 'workout', id },
                { onSuccess: onClose, onError: (error) => setMessage(describeErrorText(error)) },
              )
            }}
            disabled={published || discard.isPending}
          >
            Discard
          </Button>
          <Button
            icon="check"
            onClick={() => {
              if (id == null) return
              publish.mutate(
                { kind: 'workout', id },
                {
                  onSuccess: () => {
                    setMessage('Published. The member has been told it is ready.')
                    setTimeout(onClose, 1400)
                  },
                  onError: (error) => setMessage(describeErrorText(error)),
                },
              )
            }}
            disabled={published || publish.isPending}
          >
            {published ? 'Published' : publish.isPending ? 'Publishing…' : 'Publish to member'}
          </Button>
        </>
      }
    >
      {isLoading && <Skeleton className="h-64 w-full" />}

      {draft && (
        <>
          <DraftProvenance draft={draft} />

          <div className="space-y-4">
            {draft.days.map((day) => (
              <section
                key={day.id}
                className={cn(
                  'rounded-[var(--radius-input)] border p-4',
                  day.isRestDay ? 'border-dashed border-[var(--hairline)]' : 'border-[var(--hairline)]',
                )}
              >
                <header className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
                  <h3 className="text-[0.9375rem] font-semibold">
                    Day {day.dayIndex} · {day.title}
                  </h3>
                  {day.focus && <span className="text-[0.8125rem] text-smoke">{day.focus}</span>}
                </header>

                {day.isRestDay ? (
                  <p className="text-[0.875rem] text-smoke">{day.notes ?? 'Rest.'}</p>
                ) : (
                  <ul className="divide-y divide-[var(--hairline)]">
                    {day.exercises.map((exercise) => (
                      <li key={exercise.id} className="flex flex-wrap items-baseline gap-x-4 gap-y-1 py-2.5">
                        <span className="min-w-[12rem] flex-1 font-medium">
                          {exercise.supersetGroup && (
                            <span className="mr-2 rounded bg-[var(--hairline)] px-1.5 py-0.5 text-[0.6875rem]">
                              {exercise.supersetGroup}
                            </span>
                          )}
                          {exercise.name}
                        </span>
                        <span className="tabular-nums text-[0.875rem]">
                          {exercise.sets} × {exercise.repScheme}
                        </span>
                        <span className="text-[0.8125rem] text-smoke">{exercise.restSeconds}s rest</span>
                        {exercise.targetWeightKg != null && (
                          <span className="text-[0.8125rem] text-accent tabular-nums">
                            {exercise.targetWeightKg} kg
                          </span>
                        )}
                        {exercise.notes && (
                          <span className="w-full text-[0.8125rem] text-smoke">{exercise.notes}</span>
                        )}
                      </li>
                    ))}
                  </ul>
                )}

                {!day.isRestDay && day.notes && (
                  <p className="mt-3 text-[0.8125rem] text-smoke">{day.notes}</p>
                )}
              </section>
            ))}
          </div>

          {!published && (
            <div className="mt-5">
              <TextAreaField
                label="Programme note the member reads"
                rows={3}
                defaultValue={draft.description ?? ''}
                onBlur={(event) =>
                  update.mutate({ id: draft.id, description: event.target.value })
                }
                hint="Saved when you click away."
              />
            </div>
          )}

          {message && (
            <p className="mt-5 rounded-[var(--radius-input)] border border-[var(--hairline)] p-3 text-[0.8125rem]" role="status">
              {message}
            </p>
          )}
        </>
      )}
    </Drawer>
  )
}

function DietReviewDrawer({ id, onClose }: { id: number | null; onClose: () => void }) {
  const { data: draft, isLoading } = useDietDraft(id)
  const publish = usePublishPlan()
  const discard = useDiscardPlan()
  const update = useUpdateDietDraft()
  const [message, setMessage] = useState<string | null>(null)

  const published = draft?.status === 2
  const mealTotal = (draft?.meals ?? []).reduce((total, meal) => total + meal.calories, 0)

  return (
    <Drawer
      open={id != null}
      onClose={onClose}
      width="lg"
      title={draft?.name ?? 'Eating draft'}
      footer={
        <>
          <Button
            variant="ghost"
            onClick={() => {
              if (id == null) return
              discard.mutate(
                { kind: 'diet', id },
                { onSuccess: onClose, onError: (error) => setMessage(describeErrorText(error)) },
              )
            }}
            disabled={published || discard.isPending}
          >
            Discard
          </Button>
          <Button
            icon="check"
            onClick={() => {
              if (id == null) return
              publish.mutate(
                { kind: 'diet', id },
                {
                  onSuccess: () => {
                    setMessage('Published.')
                    setTimeout(onClose, 1400)
                  },
                  onError: (error) => setMessage(describeErrorText(error)),
                },
              )
            }}
            disabled={published || publish.isPending}
          >
            {published ? 'Published' : 'Publish to member'}
          </Button>
        </>
      }
    >
      {isLoading && <Skeleton className="h-64 w-full" />}

      {draft && (
        <>
          <DraftProvenance draft={draft} />

          <div className="mb-5 grid grid-cols-4 gap-3">
            {[
              { label: 'Calories', value: `${draft.targetCalories}` },
              { label: 'Protein', value: `${draft.proteinGrams}g` },
              { label: 'Carbs', value: `${draft.carbGrams}g` },
              { label: 'Fat', value: `${draft.fatGrams}g` },
            ].map((tile) => (
              <div key={tile.label} className="rounded-[var(--radius-input)] border border-[var(--hairline)] p-3">
                <p className="text-[0.6875rem] uppercase tracking-[0.12em] text-smoke">{tile.label}</p>
                <p className="mt-1 text-[1.125rem] font-semibold tabular-nums">{tile.value}</p>
              </div>
            ))}
          </div>

          {/* The meals have to add up to the target, or the plan is two different plans. */}
          {Math.abs(mealTotal - draft.targetCalories) > draft.targetCalories * 0.08 && (
            <p className="mb-4 flex items-start gap-2 rounded-[var(--radius-input)] border border-[var(--accent)]/40 p-3 text-[0.8125rem] text-[var(--accent)]">
              <Icon name="sparkles" size={14} className="mt-0.5 shrink-0" aria-hidden="true" />
              The meals add up to {mealTotal} kcal against a {draft.targetCalories} kcal target. Adjust before publishing.
            </p>
          )}

          <ul className="space-y-3">
            {draft.meals.map((meal) => (
              <li key={meal.id} className="rounded-[var(--radius-input)] border border-[var(--hairline)] p-4">
                <div className="flex flex-wrap items-baseline justify-between gap-2">
                  <h3 className="text-[0.9375rem] font-semibold">
                    {meal.slotLabel} · {meal.title}
                  </h3>
                  <span className="text-[0.8125rem] tabular-nums text-smoke">
                    {meal.calories} kcal · {meal.proteinGrams}P / {meal.carbGrams}C / {meal.fatGrams}F
                  </span>
                </div>
                <p className="mt-1.5 text-[0.875rem]">{meal.items}</p>
                {meal.timingHint && <p className="mt-1 text-[0.8125rem] text-smoke">{meal.timingHint}</p>}
              </li>
            ))}
          </ul>

          {!published && (
            <div className="mt-5">
              <TextAreaField
                label="Note the member reads"
                rows={3}
                defaultValue={draft.notes ?? ''}
                onBlur={(event) => update.mutate({ id: draft.id, notes: event.target.value })}
                hint="Saved when you click away."
              />
            </div>
          )}

          {message && (
            <p className="mt-5 rounded-[var(--radius-input)] border border-[var(--hairline)] p-3 text-[0.8125rem]" role="status">
              {message}
            </p>
          )}
        </>
      )}
    </Drawer>
  )
}

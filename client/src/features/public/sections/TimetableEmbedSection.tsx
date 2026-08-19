import { useMemo, useState } from 'react'
import { Reveal } from '@/components/ui/Reveal'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, Skeleton } from '@/components/ui/Skeleton'
import { Button } from '@/components/ui/Button'
import { useSiteSettings } from '@/lib/cms'
import { useTimetable, type ClassSession } from '@/lib/public-api'
import { SectionHeader } from '../components/SectionHeader'
import { ClassSessionCard } from '../components/ClassCard'
import { useBranchScope } from './context'
import { cn, formatDayHeading, formatDayTab } from '@/lib/utils'
import type { TimetableEmbedContent } from './schemas'

/* ============================================================================
   Public timetable (Module 1.3, 03 §7)

   Day tabs across the top, filter pills below, sessions grouped by day. No login
   required to browse — booking is what needs an account, and the trial CTA on
   every row is the route in for someone who does not have one.

   Facets come back with the results, so a filter never offers a combination that
   returns nothing. The day tab is client-side over one seven-day fetch: the whole
   window is a few hundred rows, and re-fetching per tab would make the tabs feel
   slower than the scroll they replace.
   ============================================================================ */

const TIME_BUCKETS = ['Early morning', 'Morning', 'Midday', 'Evening', 'Late evening']
const LEVELS = ['Beginner', 'Intermediate', 'Advanced']

export function TimetableEmbedSection({ content }: { content: TimetableEmbedContent }) {
  const branchScope = useBranchScope()
  const { data: settings } = useSiteSettings()
  const branches = settings?.branches ?? []

  // SectionHeader renders an h2 only when the section has a headline. Without one, the day
  // heading is this section's top heading and has to be an h2, or the page skips a level.
  const DayHeading = content.headline ? 'h3' : 'h2'

  const lockedBranch = content.lockBranch ? (branchScope ?? content.defaultBranchSlug) : undefined
  const [branch, setBranch] = useState<string | undefined>(lockedBranch ?? content.defaultBranchSlug)
  const [format, setFormat] = useState<string | undefined>()
  const [trainer, setTrainer] = useState<string | undefined>()
  const [timeOfDay, setTimeOfDay] = useState<string | undefined>()
  const [level, setLevel] = useState<string | undefined>()
  const [activeDay, setActiveDay] = useState<string | undefined>()

  const { data, isLoading, isFetching, isError } = useTimetable({
    branchSlug: lockedBranch ?? branch,
    formatSlug: format,
    trainerSlug: trainer,
    timeOfDay,
    level,
    days: content.daysVisible,
  })

  const sessions = useMemo(() => data?.sessions ?? [], [data])

  // Day tabs come from the requested window, not the results, so the tabs stay put
  // when a filter empties a day rather than the strip reshuffling under the cursor.
  const days = useMemo(() => {
    if (!data) return []
    const list: string[] = []
    const cursor = new Date(`${data.fromDate}T00:00:00`)
    const end = new Date(`${data.toDate}T00:00:00`)
    while (cursor <= end) {
      list.push(
        `${cursor.getFullYear()}-${String(cursor.getMonth() + 1).padStart(2, '0')}-${String(cursor.getDate()).padStart(2, '0')}`,
      )
      cursor.setDate(cursor.getDate() + 1)
    }
    return list
  }, [data])

  const countsByDay = useMemo(() => {
    const counts = new Map<string, number>()
    for (const session of sessions) counts.set(session.date, (counts.get(session.date) ?? 0) + 1)
    return counts
  }, [sessions])

  const selectedDay = activeDay && days.includes(activeDay) ? activeDay : days[0]
  const visible = sessions.filter((session) => session.date === selectedDay)

  const filtersApplied = Boolean(format || trainer || timeOfDay || level || (!lockedBranch && branch))
  const showFilter = (name: string) => content.filters.includes(name as never)

  function clearFilters() {
    setFormat(undefined)
    setTrainer(undefined)
    setTimeOfDay(undefined)
    setLevel(undefined)
    if (!lockedBranch) setBranch(content.defaultBranchSlug)
  }

  return (
    <section className="section-y bg-carbon" id="timetable">
      <div className="shell">
        <SectionHeader eyebrow={content.eyebrow} headline={content.headline} align="split" />

        {/* Filters */}
        <Reveal className="mt-10 space-y-5">
          <div className="flex flex-wrap items-center gap-3">
            {showFilter('branch') && !lockedBranch && (
              <FilterSelect
                label="Branch"
                value={branch ?? ''}
                onChange={(value) => setBranch(value || undefined)}
                options={[
                  { value: '', label: 'All branches' },
                  ...branches.map((entry) => ({ value: entry.slug, label: entry.name.replace('FORGE ', '') })),
                ]}
              />
            )}

            {showFilter('format') && (
              <FilterSelect
                label="Class"
                value={format ?? ''}
                onChange={(value) => setFormat(value || undefined)}
                options={[
                  { value: '', label: 'All formats' },
                  ...(data?.formats ?? []).map((option) => ({
                    value: option.slug,
                    label: `${option.name} (${option.count})`,
                  })),
                ]}
              />
            )}

            {showFilter('trainer') && (
              <FilterSelect
                label="Coach"
                value={trainer ?? ''}
                onChange={(value) => setTrainer(value || undefined)}
                options={[
                  { value: '', label: 'Any coach' },
                  ...(data?.trainers ?? []).map((option) => ({
                    value: option.slug,
                    label: `${option.name} (${option.count})`,
                  })),
                ]}
              />
            )}

            {showFilter('level') && (
              <FilterSelect
                label="Level"
                value={level ?? ''}
                onChange={(value) => setLevel(value || undefined)}
                options={[{ value: '', label: 'All levels' }, ...LEVELS.map((entry) => ({ value: entry, label: entry }))]}
              />
            )}

            {filtersApplied && (
              <Button variant="ghost" size="sm" icon="x" onClick={clearFilters}>
                Clear
              </Button>
            )}
          </div>

          {showFilter('timeOfDay') && (
            <div className="flex flex-wrap gap-2" role="group" aria-label="Time of day">
              <Pill active={!timeOfDay} onClick={() => setTimeOfDay(undefined)}>
                Any time
              </Pill>
              {TIME_BUCKETS.map((bucket) => (
                <Pill key={bucket} active={timeOfDay === bucket} onClick={() => setTimeOfDay(bucket)}>
                  {bucket}
                </Pill>
              ))}
            </div>
          )}
        </Reveal>

        {/* Day tabs */}
        {days.length > 0 && (
          <div
            className="mt-9 flex gap-2 overflow-x-auto border-y border-[var(--hairline)] py-3"
            role="tablist"
            aria-label="Choose a day"
          >
            {days.map((day) => {
              const tab = formatDayTab(day)
              const count = countsByDay.get(day) ?? 0
              const selected = day === selectedDay

              return (
                <button
                  key={day}
                  type="button"
                  role="tab"
                  aria-selected={selected}
                  onClick={() => setActiveDay(day)}
                  className={cn(
                    'flex min-w-[4.5rem] shrink-0 flex-col items-center rounded-[var(--radius-card)] px-4 py-2.5',
                    'transition-colors duration-200 ease-out',
                    selected ? 'bg-accent text-ink' : 'text-smoke hover:bg-steel hover:text-bone',
                  )}
                >
                  <span className="text-[0.6875rem] uppercase tracking-[0.08em]">
                    {tab.isToday ? 'Today' : tab.weekday}
                  </span>
                  <span className="numeric mt-0.5 text-[1.125rem] font-semibold">{tab.day}</span>
                  <span className={cn('numeric text-[0.625rem]', selected ? 'text-ink/70' : 'text-smoke')}>
                    {count} {count === 1 ? 'class' : 'classes'}
                  </span>
                </button>
              )
            })}
          </div>
        )}

        {/* Sessions */}
        <div className="mt-9" aria-busy={isFetching}>
          {isLoading && (
            <div className="space-y-3">
              {Array.from({ length: 5 }).map((_, index) => (
                <Skeleton key={index} className="h-[8.5rem] w-full" />
              ))}
            </div>
          )}

          {isError && (
            <EmptyState
              icon="calendar"
              headline="The timetable is not loading"
              body="Refresh the page, or call the branch — the desk has today's schedule in front of them."
              actionLabel="Contact us"
              actionTo="/contact"
            />
          )}

          {!isLoading && !isError && (
            <>
              {selectedDay && visible.length > 0 && (
                <>
                  <DayHeading className="caption mb-5 flex items-center gap-4">
                    {formatDayHeading(selectedDay)}
                    <span aria-hidden className="h-px flex-1 bg-[var(--hairline)]" />
                    <span className="numeric text-smoke">{visible.length}</span>
                  </DayHeading>

                  <div className="space-y-3">
                    {visible.map((session: ClassSession, index: number) => (
                      <Reveal key={session.id} delay={Math.min(0.25, index * 0.04)} distance={16} amount={0.05}>
                        <ClassSessionCard
                          session={session}
                          showBranch={!lockedBranch}
                          trialCtaLabel={content.trialCtaLabel}
                        />
                      </Reveal>
                    ))}
                  </div>
                </>
              )}

              {visible.length === 0 && (
                <EmptyState
                  icon="calendar"
                  headline="Nothing on this day"
                  body={content.emptyState}
                  actionLabel={filtersApplied ? undefined : 'Book a free trial'}
                  actionTo={filtersApplied ? undefined : '/free-trial'}
                />
              )}
            </>
          )}
        </div>

        <p className="mt-8 flex items-center gap-2.5 text-[0.8125rem] text-smoke">
          <Icon name="clock" size={14} className="text-accent" />
          All times are IST. Booking opens 72 hours ahead and the waitlist promotes itself when a spot frees up.
        </p>
      </div>
    </section>
  )
}

function FilterSelect({
  label,
  value,
  onChange,
  options,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  options: Array<{ value: string; label: string }>
}) {
  return (
    <label className="inline-flex items-center gap-2.5 text-[0.75rem] uppercase tracking-[0.08em] text-smoke">
      {label}
      <select
        className="field-input h-10 w-auto min-w-[10rem] py-0 text-[0.8125rem] normal-case tracking-normal"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  )
}

function Pill({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={cn(
        'rounded-full border px-4 py-2 text-[0.8125rem] transition-colors duration-200 ease-out',
        active
          ? 'border-accent bg-accent text-ink'
          : 'border-[var(--hairline-strong)] text-smoke hover:border-accent hover:text-accent',
      )}
    >
      {children}
    </button>
  )
}

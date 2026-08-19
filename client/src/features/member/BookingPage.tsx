import { useMemo, useState } from 'react'
import { Badge } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, Skeleton } from '@/components/ui/Skeleton'
import { cn, formatClock, formatDayHeading, formatDayTab, todayIso } from '@/lib/utils'
import { describeErrorText, relativeTime } from '@/features/admin/lib/format'
import { useCancelBooking, useMyBookings, useTimetable, type TimetableParams } from './lib/portal-api'
import { SessionCard } from './components/SessionCard'
import { InlineNote, PillToggle, PortalHeading } from './components/ui'
import { RatingPromptCard } from './components/RatingPrompt'
import type { PortalBooking } from './lib/types'

const TIME_BUCKETS = ['Early morning', 'Morning', 'Midday', 'Evening', 'Late evening']
const HORIZON_DAYS = 14

/**
 * Booking (Module 3 — Booking). Day tabs across a fortnight, filter pills that only
 * ever offer live combinations, and one-tap booking with optimistic UI. The second
 * tab is the member's own bookings — upcoming, then the history they can rate.
 */
export function BookingPage() {
  const [tab, setTab] = useState<'timetable' | 'mine'>('timetable')

  return (
    <div>
      <PortalHeading
        eyebrow="Classes"
        title="Book a class"
        lead="Every format across all three branches. Spots update live — the ring on each card is the real count."
      />

      <div className="mb-7 flex gap-2 border-b border-[var(--hairline)]">
        <TabButton active={tab === 'timetable'} onClick={() => setTab('timetable')}>
          Timetable
        </TabButton>
        <TabButton active={tab === 'mine'} onClick={() => setTab('mine')}>
          My classes
        </TabButton>
      </div>

      {tab === 'timetable' ? <Timetable /> : <MyClasses />}
    </div>
  )
}

function TabButton({
  active,
  onClick,
  children,
}: {
  active: boolean
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-current={active ? 'page' : undefined}
      className={cn(
        '-mb-px min-h-11 border-b-2 px-4 text-[0.9375rem] transition-colors duration-200',
        active ? 'border-accent text-accent' : 'border-transparent text-smoke hover:text-bone',
      )}
    >
      {children}
    </button>
  )
}

/* ---------------------------------------------------------------- timetable */

function Timetable() {
  const [day, setDay] = useState(todayIso())
  const [branchId, setBranchId] = useState<number | undefined>(undefined)
  const [formatSlug, setFormatSlug] = useState<string | undefined>(undefined)
  const [timeOfDay, setTimeOfDay] = useState<string | undefined>(undefined)

  const params: TimetableParams = { from: day, days: 1, branchId, formatSlug, timeOfDay }
  const { data, isLoading, isPlaceholderData } = useTimetable(params)

  // The day rail always covers the full horizon, whatever the filters return.
  const days = useMemo(
    () => Array.from({ length: HORIZON_DAYS }, (_, index) => todayIso(index)),
    [],
  )

  const sessions = data?.sessions ?? []

  return (
    <div className="space-y-6">
      {data?.bookingBlockedReason && (
        <InlineNote tone="warn" icon="lock">
          {data.bookingBlockedReason}
        </InlineNote>
      )}
      {data?.classCreditsRemaining != null && !data.bookingBlockedReason && (
        <InlineNote icon="calendar-check">
          {data.classCreditsRemaining} class credit{data.classCreditsRemaining === 1 ? '' : 's'} left on your pack. A
          cancelled booking hands its credit straight back.
        </InlineNote>
      )}

      {/* Day rail — horizontally scrollable on a phone, never wrapping into two rows. */}
      <div className="-mx-[var(--gutter)] overflow-x-auto px-[var(--gutter)] pb-1">
        <div className="flex gap-2" role="tablist" aria-label="Choose a day">
          {days.map((iso) => {
            const meta = formatDayTab(iso)
            const active = iso === day
            return (
              <button
                key={iso}
                type="button"
                role="tab"
                aria-selected={active}
                onClick={() => setDay(iso)}
                className={cn(
                  'flex min-h-[4.25rem] w-[3.75rem] shrink-0 flex-col items-center justify-center gap-1 rounded-[var(--radius-card)] border transition-colors duration-200',
                  active
                    ? 'border-accent bg-[var(--accent-soft)] text-accent'
                    : 'border-[var(--hairline)] text-smoke hover:border-bone/30 hover:text-bone',
                )}
              >
                <span className="text-[0.625rem] uppercase tracking-[0.08em]">{meta.weekday}</span>
                <span className="numeric display-m text-[1.125rem] leading-none">{meta.day}</span>
                {meta.isToday && <span className="size-1 rounded-full bg-current" aria-hidden />}
              </button>
            )
          })}
        </div>
      </div>

      <div className="space-y-3">
        {(data?.branches.length ?? 0) > 1 && (
          <PillToggle
            ariaLabel="Branch"
            value={branchId ? String(branchId) : 'all'}
            onChange={(value) => setBranchId(value === 'all' ? undefined : Number(value))}
            options={[
              { value: 'all', label: 'All branches' },
              ...(data?.branches ?? []).map((branch) => ({
                value: String(branch.id),
                label: branch.name.replace('FORGE ', '') + (branch.isHome ? ' · home' : ''),
              })),
            ]}
          />
        )}

        {(data?.formats.length ?? 0) > 1 && (
          <PillToggle
            ariaLabel="Class format"
            value={formatSlug ?? 'all'}
            onChange={(value) => setFormatSlug(value === 'all' ? undefined : value)}
            options={[
              { value: 'all', label: 'All formats' },
              ...(data?.formats ?? []).map((format) => ({
                value: format.slug,
                label: format.name,
                count: format.count,
              })),
            ]}
          />
        )}

        <PillToggle
          ariaLabel="Time of day"
          value={timeOfDay ?? 'all'}
          onChange={(value) => setTimeOfDay(value === 'all' ? undefined : value)}
          options={[{ value: 'all', label: 'Any time' }, ...TIME_BUCKETS.map((bucket) => ({ value: bucket, label: bucket }))]}
        />
      </div>

      {isLoading ? (
        <div className="space-y-3" aria-busy="true">
          {Array.from({ length: 5 }).map((_, index) => (
            <Skeleton key={index} className="h-28" />
          ))}
        </div>
      ) : sessions.length === 0 ? (
        <EmptyState
          icon="calendar"
          headline="Nothing on with those filters"
          body="Try another day, or clear the format filter — the timetable is thinner mid-morning on weekdays."
        />
      ) : (
        <div className={cn('space-y-3 transition-opacity duration-200', isPlaceholderData && 'opacity-60')}>
          <h2 className="caption">{formatDayHeading(day)}</h2>
          {sessions.map((session) => (
            <SessionCard key={session.id} session={session} />
          ))}
        </div>
      )}
    </div>
  )
}

/* ---------------------------------------------------------------- my classes */

function MyClasses() {
  const [scope, setScope] = useState<'upcoming' | 'past'>('upcoming')
  const { data, isLoading } = useMyBookings(scope)
  const cancel = useCancelBooking()
  const [error, setError] = useState<string | null>(null)
  const [rating, setRating] = useState<PortalBooking | null>(null)

  return (
    <div className="space-y-5">
      <PillToggle
        ariaLabel="Which bookings"
        value={scope}
        onChange={(value) => setScope(value as 'upcoming' | 'past')}
        options={[
          { value: 'upcoming', label: 'Upcoming' },
          { value: 'past', label: 'History' },
        ]}
      />

      {error && (
        <InlineNote tone="danger" icon="x">
          {error}
        </InlineNote>
      )}

      {rating && (
        <RatingPromptCard
          prompt={{
            bookingId: rating.id,
            sessionId: rating.sessionId,
            formatName: rating.formatName,
            trainerId: 0,
            trainerName: rating.trainerName,
            trainerPortraitUrl: rating.trainerPortraitUrl,
            date: rating.date,
            startTime: rating.startTime,
          }}
          onDone={() => setRating(null)}
        />
      )}

      {isLoading ? (
        <div className="space-y-3" aria-busy="true">
          {Array.from({ length: 3 }).map((_, index) => (
            <Skeleton key={index} className="h-24" />
          ))}
        </div>
      ) : (data?.length ?? 0) === 0 ? (
        <EmptyState
          icon="calendar"
          headline={scope === 'upcoming' ? 'Nothing booked' : 'No history yet'}
          body={
            scope === 'upcoming'
              ? 'Book from the timetable and it will appear here with a cancel option.'
              : 'Classes you attend show up here, and you can rate the coach afterwards.'
          }
        />
      ) : (
        <ul className="space-y-3">
          {data!.map((booking) => (
            <li
              key={booking.id}
              className="flex flex-wrap items-center gap-4 rounded-[var(--radius-card)] border border-[var(--hairline)] bg-carbon p-4 sm:p-5"
            >
              <img
                src={booking.coverImageUrl}
                alt=""
                loading="lazy"
                className="graded hidden size-16 rounded-[10px] object-cover sm:block"
              />
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <h3 className="display-m text-[1.0625rem] text-bone">{booking.formatName}</h3>
                  <StatusBadge booking={booking} />
                </div>
                <p className="mt-1.5 text-[0.8125rem] text-smoke">
                  {new Date(`${booking.date}T00:00:00`).toLocaleDateString('en-IN', {
                    weekday: 'short',
                    day: 'numeric',
                    month: 'short',
                  })}{' '}
                  · {formatClock(booking.startTime)} · {booking.trainerName} ·{' '}
                  {booking.branchName.replace('FORGE ', '')}
                  {booking.roomName ? ` · ${booking.roomName}` : ''}
                </p>
                {booking.checkedInAtUtc && (
                  <p className="mt-1 flex items-center gap-1.5 text-[0.75rem] text-success">
                    <Icon name="check" size={12} strokeWidth={2.2} />
                    Checked in {relativeTime(booking.checkedInAtUtc)}
                  </p>
                )}
                {booking.ratingScore != null && (
                  <p className="mt-1 flex items-center gap-1 text-[0.75rem] text-accent">
                    {Array.from({ length: booking.ratingScore }).map((_, index) => (
                      <Icon key={index} name="star" size={12} className="fill-accent" />
                    ))}
                    <span className="ml-1 text-smoke">you rated this</span>
                  </p>
                )}
              </div>

              <div className="flex flex-wrap items-center gap-2">
                {booking.canRate && (
                  <Button size="sm" variant="outline" icon="star" onClick={() => setRating(booking)}>
                    Rate
                  </Button>
                )}
                {booking.canCancel && (
                  <Button
                    size="sm"
                    variant="ghost"
                    loading={cancel.isPending}
                    onClick={() => {
                      setError(null)
                      cancel.mutate(
                        { bookingId: booking.id, sessionId: booking.sessionId },
                        { onError: (failure) => setError(describeErrorText(failure)) },
                      )
                    }}
                  >
                    {booking.isLateCancelWindow ? 'Cancel (late)' : 'Cancel'}
                  </Button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function StatusBadge({ booking }: { booking: PortalBooking }) {
  if (booking.status === 1) return <Badge tone="accent">Waitlist #{booking.waitlistPosition}</Badge>
  if (booking.status === 2) return <Badge tone="success">Attended</Badge>
  if (booking.status === 3) return <Badge tone="hot">No-show</Badge>
  if (booking.status === 4) return <Badge>Cancelled</Badge>
  return <Badge tone="accent">Booked</Badge>
}

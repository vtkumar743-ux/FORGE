import { Link } from 'react-router-dom'
import { Badge, CapacityRing, Card, CardMedia, CardTitle } from '@/components/ui/Card'
import { Icon, isIconName } from '@/components/ui/Icon'
import { ButtonLink } from '@/components/ui/Button'
import { cn, formatClock } from '@/lib/utils'
import type { ClassFormat, ClassSession } from '@/lib/public-api'

/* ============================================================================
   Class cards (03 §7): cult.fit's information density — duration, coach, level,
   spots left, one-tap book — at 2–3× the image size, with hover choreography and
   an SVG capacity ring. The density is the point: a card you have to click to
   learn anything from is a thumbnail, not a class card.
   ============================================================================ */

export function ClassFormatCard({
  format,
  showCapacityRing = true,
  showSpotsLeft = true,
  trialCtaLabel = 'Book free trial',
}: {
  format: ClassFormat
  showCapacityRing?: boolean
  showSpotsLeft?: boolean
  trialCtaLabel?: string
}) {
  const next = format.nextSession

  return (
    <Card padded={false} interactive className="flex h-full flex-col">
      <CardMedia src={format.coverImageUrl} alt={`${format.name} class in progress`} ratio="16/10" scrim>
        <div className="absolute inset-x-0 bottom-0 flex items-end justify-between gap-3 p-5">
          <div className="flex flex-wrap items-center gap-2">
            <Badge>{format.levelName}</Badge>
            <Badge icon="clock">{format.durationMinutes} min</Badge>
          </div>
          {showCapacityRing && next && (
            <CapacityRing
              filled={next.bookedCount}
              total={next.capacity}
              label={`${next.spotsLeft} of ${next.capacity} spots left in the next ${format.name}`}
            />
          )}
        </div>

        {isIconName(format.iconKey) && (
          <span className="absolute left-5 top-5 inline-flex size-10 items-center justify-center rounded-full border border-[var(--hairline-strong)] bg-ink/55 text-accent backdrop-blur-sm">
            <Icon name={format.iconKey} size={19} />
          </span>
        )}
      </CardMedia>

      <div className="flex flex-1 flex-col p-6">
        <CardTitle>{format.name}</CardTitle>
        <p className="mt-3 flex-1 text-[0.9375rem] leading-relaxed text-smoke">{format.shortDescription}</p>

        <dl className="mt-5 grid grid-cols-2 gap-x-4 gap-y-3 border-t border-[var(--hairline)] pt-5 text-[0.8125rem]">
          <Detail label="Next class">
            {next ? `${formatClock(next.startTime)} · ${next.branchName.replace('FORGE ', '')}` : 'Check timetable'}
          </Detail>
          <Detail label="Coach">{next ? next.trainerName : `${format.branchSlugs.length} branches`}</Detail>
          <Detail label="Cap">{format.capacity} people</Detail>
          <Detail label="This week">
            {format.upcomingSessionCount} {format.upcomingSessionCount === 1 ? 'session' : 'sessions'}
          </Detail>
        </dl>

        <div className="mt-6 flex items-center justify-between gap-3">
          <ButtonLink to="/free-trial" size="sm">
            {trialCtaLabel}
          </ButtonLink>
          {showSpotsLeft && next && (
            <span
              className={cn(
                'numeric text-[0.8125rem]',
                next.spotsLeft === 0 ? 'text-accent-hot' : next.spotsLeft <= 3 ? 'text-accent' : 'text-smoke',
              )}
            >
              {next.spotsLeft === 0 ? 'Waitlist only' : `${next.spotsLeft} spots left`}
            </span>
          )}
        </div>
      </div>
    </Card>
  )
}

/** A single dated session — the row the timetable renders. */
export function ClassSessionCard({
  session,
  showBranch = true,
  trialCtaLabel = 'Book free trial',
}: {
  session: ClassSession
  showBranch?: boolean
  trialCtaLabel?: string
}) {
  const full = session.spotsLeft === 0

  return (
    <article
      className={cn(
        'group grid grid-cols-[auto_1fr] items-start gap-x-5 gap-y-4 rounded-[var(--radius-card)] border p-5',
        'border-[var(--hairline)] bg-carbon transition-colors duration-200 ease-out hover:border-[var(--accent-line)]',
        'sm:grid-cols-[7rem_1fr_auto] sm:items-center',
      )}
    >
      <div className="min-w-0">
        <p className="numeric display-m text-[1.375rem] text-bone">{formatClock(session.startTime)}</p>
        <p className="mt-1 text-[0.75rem] text-smoke">
          {session.durationMinutes} min · ends {formatClock(session.endTime)}
        </p>
      </div>

      <div className="min-w-0">
        <h3 className="display-m text-[1.125rem] text-bone">
          <span className="underline-slide">{session.formatName}</span>
        </h3>
        <p className="mt-1.5 flex flex-wrap items-center gap-x-2.5 gap-y-1 text-[0.8125rem] text-smoke">
          <Link to={`/trainers/${session.trainerSlug}`} className="hover:text-accent">
            {session.trainerName}
          </Link>
          {session.isSubstitute && <span className="text-accent">(covering)</span>}
          {showBranch && (
            <>
              <span aria-hidden>·</span>
              <span>{session.branchName.replace('FORGE ', '')}</span>
            </>
          )}
          {session.roomName && (
            <>
              <span aria-hidden>·</span>
              <span>{session.roomName}</span>
            </>
          )}
        </p>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <Badge>{session.levelName}</Badge>
          <Badge>{session.timeOfDay}</Badge>
        </div>
      </div>

      <div className="col-span-2 flex items-center justify-between gap-4 border-t border-[var(--hairline)] pt-4 sm:col-span-1 sm:flex-col sm:items-end sm:gap-3 sm:border-0 sm:pt-0">
        <div className="flex items-center gap-3">
          <CapacityRing
            filled={session.bookedCount}
            total={session.capacity}
            label={`${session.spotsLeft} of ${session.capacity} spots left`}
          />
          <span className={cn('numeric text-[0.8125rem]', full ? 'text-accent-hot' : 'text-smoke')}>
            {full ? `${session.waitlistCount} waiting` : `${session.spotsLeft} left`}
          </span>
        </div>
        <ButtonLink
          to={`/free-trial?class=${session.formatSlug}&branch=${session.branchSlug}&date=${session.date}`}
          size="sm"
          variant={full ? 'outline' : 'primary'}
        >
          {full ? 'Join waitlist' : trialCtaLabel}
        </ButtonLink>
      </div>
    </article>
  )
}

function Detail({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="caption text-[0.625rem]">{label}</dt>
      <dd className="mt-1 truncate text-bone/85">{children}</dd>
    </div>
  )
}

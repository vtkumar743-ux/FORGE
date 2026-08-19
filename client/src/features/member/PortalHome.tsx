import { Link } from 'react-router-dom'
import { ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Reveal, RevealGroup } from '@/components/ui/Reveal'
import { EmptyState, Skeleton, SkeletonText } from '@/components/ui/Skeleton'
import { useLiveOccupancy } from '@/lib/realtime'
import { OccupancyMeter } from '@/features/public/components/OccupancyMeter'
import { formatInr, formatDate } from '@/lib/utils'
import { relativeTime } from '@/features/admin/lib/format'
import { usePortalHome, useDismissCelebration } from './lib/portal-api'
import { shareText, shareToWhatsApp } from './lib/share'
import { PrBanner } from './components/PrBanner'
import { RatingPromptCard } from './components/RatingPrompt'
import { SessionCard } from './components/SessionCard'
import { InlineNote, Panel, StatTile, StreakCalendar, StreakFlame } from './components/ui'

/**
 * Portal home (Module 3 — Home): today's classes, the streak, live occupancy at
 * the member's branch, what is owed and when, and the one thing they should do
 * next. Everything above the fold answers "should I go in today?".
 */
export function PortalHome() {
  const { data, isLoading, isError } = usePortalHome()
  const dismiss = useDismissCelebration()

  // The meter on this screen is the same one the public site shows, pushed live (Module 4.1).
  // Hooks run before the loading guards below, so the slug is read defensively.
  const homeSlug = data?.homeBranchOccupancy?.branchSlug
  const { updates } = useLiveOccupancy(homeSlug ? [homeSlug] : [], { enabled: !!homeSlug })

  if (isLoading) return <HomeSkeleton />

  if (isError || !data) {
    return (
      <EmptyState
        icon="x"
        headline="We could not load your portal"
        body="The connection dropped or your session expired. Reload the page and it should come straight back."
        actionLabel="Back to the site"
        actionTo="/"
      />
    )
  }

  const { member, membership, streak, todaysClasses, nextClass, program } = data
  // A pushed reading wins over the one the home payload was built with.
  const homeBranchOccupancy = (homeSlug ? updates[homeSlug] : undefined) ?? data.homeBranchOccupancy
  const greeting = greetingFor(new Date())

  return (
    <div className="space-y-8">
      <Reveal>
        <p className="caption">
          {member.memberCode} · {member.homeBranchName}
        </p>
        <h1 className="display-l mt-3 text-[clamp(2rem,5vw,3.25rem)] text-bone">
          {greeting}, {member.firstName ?? member.fullName.split(' ')[0]}.
        </h1>
        <p className="measure mt-3 text-body-l leading-relaxed text-smoke">{headline(data)}</p>
      </Reveal>

      {data.pendingCelebration && (
        <PrBanner
          celebration={data.pendingCelebration}
          onDismiss={() => dismiss.mutate(data.pendingCelebration!.logId)}
          onShare={() => shareText(data.pendingCelebration!.shareText)}
          onWhatsApp={() => shareToWhatsApp(data.pendingCelebration!.shareText)}
        />
      )}

      {data.ratingPrompts.length > 0 && (
        <Reveal>
          <RatingPromptCard prompt={data.ratingPrompts[0]} />
        </Reveal>
      )}

      <RevealGroup className="grid gap-5 lg:grid-cols-3" stagger={0.06}>
        <Panel title="Your streak" className="lg:col-span-2">
          <StreakFlame streak={streak} />
          <div className="mt-6 border-t border-[var(--hairline)] pt-5">
            <StreakCalendar days={streak.calendar} />
          </div>
        </Panel>

        <Panel title={`${member.homeBranchName.replace('FORGE ', '')} right now`}>
          {homeBranchOccupancy ? (
            <>
              <OccupancyMeter occupancy={homeBranchOccupancy} size={118} />
              <p className="mt-5 border-t border-[var(--hairline)] pt-4 text-[0.8125rem] leading-relaxed text-smoke">
                Read straight off the check-in desk. It moves as people scan in and out.
              </p>
            </>
          ) : (
            <p className="text-[0.875rem] text-smoke">No reading available for your branch.</p>
          )}
        </Panel>
      </RevealGroup>

      <RevealGroup className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4" stagger={0.05}>
        <StatTile
          label="Membership"
          value={membership ? `${membership.daysLeft}d` : '—'}
          sub={
            membership
              ? `${membership.planName} · ends ${formatDate(membership.endsOn)}`
              : 'No active plan. Pick one to start booking.'
          }
          icon="medal"
          tone={membership && membership.daysLeft <= 7 ? 'warn' : 'neutral'}
        />
        <StatTile
          label="Outstanding"
          value={data.duesOutstanding > 0 ? formatInr(data.duesOutstanding) : '₹0'}
          sub={
            data.nextPayment
              ? `${data.nextPayment.invoiceNumber} due ${formatDate(data.nextPayment.dueOn)}`
              : 'Nothing owed. Everything settled.'
          }
          icon="clock"
          tone={data.duesOutstanding > 0 ? 'warn' : 'success'}
        />
        <StatTile
          label="Class credits"
          value={membership && membership.classCreditsRemaining > 0 ? membership.classCreditsRemaining : 'Unlimited'}
          sub={membership?.accessScopeName ?? 'Buy a plan to book classes'}
          icon="calendar-check"
        />
        <StatTile
          label="Referral credit"
          value={formatInr(data.referralCredits)}
          sub="Earned from friends who joined on your code"
          icon="share"
          tone={data.referralCredits > 0 ? 'accent' : 'neutral'}
        />
      </RevealGroup>

      <Reveal>
        <Panel
          title={todaysClasses.length > 0 ? 'Today' : 'Your next class'}
          description={
            todaysClasses.length > 0
              ? 'Scan your QR at the desk to mark yourself in.'
              : nextClass
                ? 'Nothing booked today — this is what is next.'
                : undefined
          }
          actions={
            <ButtonLink to="/portal/book" size="sm" variant="outline" icon="arrow-right" iconAfter>
              Timetable
            </ButtonLink>
          }
          padded={false}
        >
          <div className="space-y-3 p-5">
            {todaysClasses.length > 0 ? (
              todaysClasses.map((session) => <SessionCard key={session.id} session={session} />)
            ) : nextClass ? (
              <SessionCard session={nextClass} showDate />
            ) : (
              <EmptyState
                icon="calendar"
                headline="Nothing booked yet"
                body="The timetable runs across all three branches. Book a class and it will show up here with your QR."
                actionLabel="Find a class"
                actionTo="/portal/book"
              />
            )}
          </div>
        </Panel>
      </Reveal>

      <RevealGroup className="grid gap-5 lg:grid-cols-2" stagger={0.06}>
        <Panel
          title="Training"
          actions={
            <ButtonLink to="/portal/workouts" size="sm" variant="ghost" icon="arrow-right" iconAfter>
              Open
            </ButtonLink>
          }
        >
          {program ? (
            <div>
              <p className="caption">
                Week {program.weekNumber} of {program.durationWeeks} · {program.daysPerWeek} days a week
              </p>
              <h3 className="display-m mt-2.5 text-[1.25rem] text-bone">{program.name}</h3>
              {program.trainerName && (
                <p className="mt-1.5 text-[0.8125rem] text-smoke">Written by {program.trainerName}</p>
              )}
              <div className="mt-5 flex flex-wrap items-center gap-3 border-t border-[var(--hairline)] pt-5">
                <div className="min-w-0 flex-1">
                  <p className="caption text-[0.5625rem]">Up next</p>
                  <p className="mt-1 truncate text-[0.9375rem] text-bone">{program.nextDayTitle ?? 'Day 1'}</p>
                </div>
                <ButtonLink to={`/portal/workouts?day=${program.nextDayId ?? ''}`} size="sm" icon="barbell">
                  Start session
                </ButtonLink>
              </div>
              <p className="mt-4 text-[0.75rem] text-smoke">
                {program.sessionsLogged} session{program.sessionsLogged === 1 ? '' : 's'} logged on this programme.
              </p>
            </div>
          ) : (
            <EmptyState
              icon="barbell"
              headline="No programme assigned yet"
              body="Ask a coach on the floor and they will write one. You can still log any lift from the library today."
              actionLabel="Log a lift"
              actionTo="/portal/workouts"
            />
          )}
        </Panel>

        <Panel
          title="What's new"
          actions={
            <ButtonLink to="/portal/notifications" size="sm" variant="ghost" icon="arrow-right" iconAfter>
              All
            </ButtonLink>
          }
          padded={false}
        >
          {data.announcements.length > 0 ? (
            <ul className="divide-y divide-[var(--hairline)]">
              {data.announcements.map((note) => (
                <li key={note.id}>
                  <Link
                    to={note.actionUrl?.startsWith('/portal') ? note.actionUrl : '/portal/notifications'}
                    className="flex items-start gap-3 px-5 py-4 transition-colors hover:bg-steel/40"
                  >
                    <span
                      aria-hidden
                      className={`mt-1.5 size-2 shrink-0 rounded-full ${note.isRead ? 'bg-steel' : 'bg-accent'}`}
                    />
                    <span className="min-w-0">
                      <span className="block text-[0.9375rem] text-bone">{note.title}</span>
                      <span className="mt-1 block truncate text-[0.8125rem] text-smoke">{note.body}</span>
                      <span className="mt-1.5 block text-[0.6875rem] text-smoke">
                        {relativeTime(note.createdAtUtc)}
                      </span>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <div className="p-5">
              <EmptyState
                icon="mail"
                headline="Nothing to read"
                body="Booking confirmations, waitlist promotions and payment reminders land here."
              />
            </div>
          )}
        </Panel>
      </RevealGroup>

      {data.newBadges.length > 0 && (
        <Reveal>
          <Panel title="New badges" description="Earned since you last looked.">
            <div className="flex flex-wrap gap-3">
              {data.newBadges.map((badge) => (
                <div
                  key={badge.id}
                  className="flex items-center gap-3 rounded-full border border-[var(--accent-line)] bg-[var(--accent-soft)] py-2 pl-3 pr-4"
                >
                  <Icon name={(badge.iconKey as never) ?? 'medal'} size={18} className="text-accent" />
                  <div>
                    <p className="text-[0.8125rem] font-medium text-bone">{badge.name}</p>
                    <p className="text-[0.6875rem] text-smoke">{badge.tier}</p>
                  </div>
                </div>
              ))}
            </div>
            <ButtonLink to="/portal/progress" size="sm" variant="ghost" className="mt-4">
              See all badges
            </ButtonLink>
          </Panel>
        </Reveal>
      )}

      {membership?.pendingFreezeRequest && (
        <Reveal>
          <InlineNote tone="warn" icon="snowflake">
            Your freeze request for {formatDate(membership.pendingFreezeRequest.requestedFrom)} –{' '}
            {formatDate(membership.pendingFreezeRequest.requestedTo)} is with the desk.{' '}
            <Link to="/portal/membership" className="underline underline-offset-4">
              Check its status
            </Link>
            .
          </InlineNote>
        </Reveal>
      )}

      {!membership && (
        <Reveal>
          <Panel>
            <div className="flex flex-wrap items-center justify-between gap-5">
              <div className="min-w-0">
                <h2 className="display-m text-[1.375rem] text-bone">You have no active plan</h2>
                <p className="measure mt-2 text-[0.9375rem] leading-relaxed text-smoke">
                  Booking, the QR check-in and your programme all unlock the moment a membership is live. Renewing
                  takes about a minute.
                </p>
              </div>
              <div className="flex flex-wrap gap-2.5">
                <ButtonLink to="/portal/membership" size="md" magnetic>
                  See plans
                </ButtonLink>
                <ButtonLink to="/plans" size="md" variant="outline">
                  Compare on the site
                </ButtonLink>
              </div>
            </div>
          </Panel>
        </Reveal>
      )}
    </div>
  )
}

/* ---------------------------------------------------------------- helpers */

function greetingFor(now: Date): string {
  const hour = now.getHours()
  if (hour < 5) return 'Still up'
  if (hour < 12) return 'Morning'
  if (hour < 17) return 'Afternoon'
  return 'Evening'
}

/** One line that says the most useful true thing about today. */
function headline(data: NonNullable<ReturnType<typeof usePortalHome>['data']>): string {
  if (!data.membership) return 'Your membership is not active. Pick a plan and booking opens straight away.'
  if (data.membership.statusName === 'Frozen')
    return `Your membership is frozen until ${formatDate(data.membership.freezeEndsOn ?? data.membership.endsOn)}.`
  if (data.todaysClasses.length > 0) {
    const first = data.todaysClasses[0]
    return `${first.formatName} with ${first.trainerName} at ${first.startTime} today. Scan your QR at the desk.`
  }
  if (data.duesOutstanding > 0) return `${formatInr(data.duesOutstanding)} is outstanding. Clearing it keeps everything open.`
  if (data.membership.daysLeft <= 7)
    return `Your plan ends in ${data.membership.daysLeft} day${data.membership.daysLeft === 1 ? '' : 's'}. Renew and you keep every day you have paid for.`
  if (data.streak.currentStreakDays > 0)
    return `${data.streak.currentStreakDays} days unbroken. Nothing booked today — the floor is open until late.`
  return 'Nothing booked today. The timetable runs from 6 AM to 9 PM across all three branches.'
}

function HomeSkeleton() {
  return (
    <div className="space-y-8" aria-busy="true">
      <div>
        <Skeleton rounded="pill" className="h-3 w-40" />
        <Skeleton className="mt-4 h-14 w-2/3" />
        <Skeleton rounded="pill" className="mt-4 h-4 w-full max-w-xl" />
      </div>
      <div className="grid gap-5 lg:grid-cols-3">
        <Skeleton className="h-64 lg:col-span-2" />
        <Skeleton className="h-64" />
      </div>
      <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }).map((_, index) => (
          <Skeleton key={index} className="h-32" />
        ))}
      </div>
      <SkeletonText lines={3} />
    </div>
  )
}

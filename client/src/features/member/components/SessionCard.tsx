import { useEffect, useState } from 'react'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import { Badge, CapacityRing } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { cn, formatClock } from '@/lib/utils'
import { describeErrorText } from '@/features/admin/lib/format'
import { useBookClass, useCancelBooking } from '../lib/portal-api'
import { DrawnCheck } from './ui'
import type { PortalSession } from '../lib/types'

/**
 * One bookable class (03 §7): cult.fit's density — time, duration, coach, level,
 * room, spots left and a one-tap Book — with the capacity ring carrying the fill.
 *
 * The tap is optimistic: the ring moves and the button flips before the network
 * answers (03 §6, and the NFR that names booking specifically). If the server
 * refuses, the card snaps back and says why in the same place the button was.
 */
export function SessionCard({
  session,
  showDate = false,
  onBooked,
}: {
  session: PortalSession
  showDate?: boolean
  onBooked?: (waitlisted: boolean) => void
}) {
  const book = useBookClass()
  const cancel = useCancelBooking()
  const reduced = useReducedMotion()
  const [flash, setFlash] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!flash) return
    const timer = window.setTimeout(() => setFlash(false), 1400)
    return () => window.clearTimeout(timer)
  }, [flash])

  const booked = session.myBookingStatus === 0
  const waitlisted = session.myBookingStatus === 1
  const full = session.spotsLeft === 0
  const busy = book.isPending || cancel.isPending

  function handleBook() {
    setError(null)
    book.mutate(
      { sessionId: session.id },
      {
        onSuccess: (result) => {
          setFlash(true)
          onBooked?.(result.status === 1)
        },
        onError: (failure) => setError(describeErrorText(failure, 'That did not go through.')),
      },
    )
  }

  function handleCancel() {
    setError(null)
    cancel.mutate(
      { bookingId: session.myBookingId!, sessionId: session.id },
      { onError: (failure) => setError(describeErrorText(failure, 'Could not cancel that.')) },
    )
  }

  return (
    <article
      className={cn(
        'group relative grid grid-cols-[4.5rem_1fr] items-start gap-x-4 gap-y-4 overflow-hidden rounded-[var(--radius-card)] border p-4 sm:grid-cols-[5.5rem_1fr_auto] sm:items-center sm:gap-x-5 sm:p-5',
        'bg-carbon transition-colors duration-200 ease-out',
        booked || waitlisted ? 'border-[var(--accent-line)]' : 'border-[var(--hairline)] hover:border-bone/25',
      )}
    >
      <AnimatePresence>
        {flash && (
          <motion.div
            key="flash"
            initial={reduced ? { opacity: 1 } : { opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="absolute inset-0 z-10 grid place-items-center bg-carbon/95"
          >
            <div className="flex flex-col items-center gap-2">
              <DrawnCheck size={44} />
              <p className="text-[0.8125rem] font-medium text-bone">
                {waitlisted ? `Waitlisted — number ${session.myWaitlistPosition ?? 1}` : 'You are in'}
              </p>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      <div className="min-w-0">
        <p className="numeric display-m text-[1.25rem] leading-none text-bone">{formatClock(session.startTime)}</p>
        <p className="mt-1.5 text-[0.6875rem] leading-tight text-smoke">
          {showDate && (
            <>
              {new Date(`${session.date}T00:00:00`).toLocaleDateString('en-IN', { weekday: 'short', day: 'numeric', month: 'short' })}
              <br />
            </>
          )}
          {session.durationMinutes} min
        </p>
      </div>

      <div className="min-w-0">
        <h3 className="display-m text-[1.0625rem] leading-tight text-bone">{session.formatName}</h3>
        <p className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-[0.8125rem] text-smoke">
          <span>{session.trainerName}</span>
          {session.isSubstitute && <span className="text-accent">(covering)</span>}
          <span aria-hidden>·</span>
          <span>{session.branchName.replace('FORGE ', '')}</span>
          {session.roomName && (
            <>
              <span aria-hidden>·</span>
              <span>{session.roomName}</span>
            </>
          )}
        </p>
        <div className="mt-2.5 flex flex-wrap items-center gap-2">
          <Badge>{session.levelName}</Badge>
          {session.estimatedCalories > 0 && <Badge icon="flame">~{session.estimatedCalories} kcal</Badge>}
          {waitlisted && (
            <Badge tone="accent" icon="clock">
              Waitlist #{session.myWaitlistPosition}
            </Badge>
          )}
          {booked && (
            <Badge tone="success" icon="check">
              Booked
            </Badge>
          )}
        </div>
      </div>

      <div className="col-span-2 flex items-center justify-between gap-4 border-t border-[var(--hairline)] pt-4 sm:col-span-1 sm:flex-col sm:items-end sm:gap-3 sm:border-0 sm:pt-0">
        <div className="flex items-center gap-3">
          <CapacityRing
            filled={session.bookedCount}
            total={session.capacity}
            label={`${session.spotsLeft} of ${session.capacity} spots left`}
          />
          <span
            className={cn(
              'numeric text-[0.8125rem]',
              full ? 'text-accent-hot' : session.spotsLeft <= 3 ? 'text-accent' : 'text-smoke',
            )}
          >
            {full ? `${session.waitlistCount} waiting` : `${session.spotsLeft} left`}
          </span>
        </div>

        {session.canCancel ? (
          <Button variant="outline" size="sm" loading={cancel.isPending} onClick={handleCancel}>
            {session.isLateCancelWindow ? 'Cancel (late)' : 'Cancel'}
          </Button>
        ) : session.canBook ? (
          <Button size="sm" loading={busy} onClick={handleBook}>
            Book
          </Button>
        ) : session.canJoinWaitlist ? (
          <Button variant="outline" size="sm" loading={busy} onClick={handleBook}>
            Join waitlist
          </Button>
        ) : (
          <span className="max-w-[16rem] text-right text-[0.75rem] leading-snug text-smoke/80">
            {session.blockedReason ?? 'Not bookable'}
          </span>
        )}
      </div>

      {error && (
        <p role="alert" className="col-span-2 flex items-start gap-2 text-[0.75rem] leading-relaxed text-accent-hot sm:col-span-3">
          <Icon name="x" size={13} className="mt-0.5 shrink-0" />
          {error}
        </p>
      )}
    </article>
  )
}

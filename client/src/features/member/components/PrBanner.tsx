import { motion, useReducedMotion } from 'motion/react'
import { Icon } from '@/components/ui/Icon'
import { Button } from '@/components/ui/Button'
import { cn } from '@/lib/utils'
import type { PrCelebration } from '../lib/types'

/**
 * The PR celebration banner (Module 4.5, surfaced here because the record is
 * detected the instant a set is logged).
 *
 * It is a banner, not a modal: a member mid-session should not have to dismiss a
 * dialog before logging their next set. The copy comes from the API so the same
 * words appear here, in the notification, and on the share card.
 */
export function PrBanner({
  celebration,
  onDismiss,
  onShare,
  onWhatsApp,
  className,
}: {
  celebration: PrCelebration
  onDismiss?: () => void
  onShare?: () => void
  onWhatsApp?: () => void
  className?: string
}) {
  const reduced = useReducedMotion()

  return (
    <motion.aside
      role="status"
      aria-live="polite"
      initial={reduced ? false : { opacity: 0, y: -14 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: [0.16, 1, 0.3, 1] }}
      className={cn(
        'relative overflow-hidden rounded-[var(--radius-card)] border border-[var(--accent-line)]',
        'bg-[color-mix(in_srgb,var(--accent)_10%,var(--carbon))] p-5 sm:p-6',
        className,
      )}
    >
      {/* Gold sweep — the one place a gradient is allowed, because it is a moment. */}
      {!reduced && (
        <motion.span
          aria-hidden
          initial={{ x: '-120%' }}
          animate={{ x: '120%' }}
          transition={{ duration: 1.4, ease: 'easeOut', delay: 0.15 }}
          className="pointer-events-none absolute inset-y-0 w-1/3 bg-[linear-gradient(90deg,transparent,color-mix(in_srgb,var(--accent)_22%,transparent),transparent)]"
        />
      )}

      <div className="relative flex flex-wrap items-start justify-between gap-5">
        <div className="flex min-w-0 items-start gap-4">
          <span className="grid size-12 shrink-0 place-items-center rounded-full border border-[var(--accent-line)] text-accent">
            <Icon name="trophy" size={24} />
          </span>
          <div className="min-w-0">
            <p className="caption text-accent">{celebration.headline}</p>
            <h2 className="display-m mt-2 text-[1.375rem] text-bone">{celebration.exerciseName}</h2>
            <p className="mt-2 text-[0.9375rem] leading-relaxed text-smoke">{celebration.message}</p>
            <dl className="mt-4 flex flex-wrap gap-x-7 gap-y-2 text-[0.8125rem]">
              <Metric label="Lifted" value={`${celebration.weightKg} kg × ${celebration.reps}`} />
              <Metric label="Est. 1RM" value={`${celebration.estimatedOneRepMax} kg`} />
              {celebration.previousBestE1Rm != null && (
                <Metric label="Previous best" value={`${celebration.previousBestE1Rm} kg`} />
              )}
            </dl>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2.5">
          {onWhatsApp && (
            <Button variant="outline" size="sm" icon="share" onClick={onWhatsApp}>
              Send on WhatsApp
            </Button>
          )}
          {onShare && (
            <Button variant="ghost" size="sm" icon="share" onClick={onShare}>
              Share
            </Button>
          )}
          {onDismiss && (
            <Button variant="ghost" size="sm" onClick={onDismiss}>
              Got it
            </Button>
          )}
        </div>
      </div>
    </motion.aside>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="caption text-[0.5625rem]">{label}</dt>
      <dd className="numeric mt-1 text-bone">{value}</dd>
    </div>
  )
}

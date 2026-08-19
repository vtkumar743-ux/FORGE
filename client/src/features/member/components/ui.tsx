import { useEffect, useId, useRef, type ReactNode } from 'react'
import { motion, useReducedMotion } from 'motion/react'
import { Icon, type IconName } from '@/components/ui/Icon'
import { Button } from '@/components/ui/Button'
import { cn } from '@/lib/utils'
import type { CalendarDay, PortalStreak } from '../lib/types'

/* ============================================================================
   Portal UI kit

   The member portal runs the dark surface — same tokens, same radii, same motion
   signature as the public site (03 §2–6). It is thumb-first: primary actions sit
   in the lower half of a phone screen, hit targets never go below 44px, and every
   panel is a card on --carbon with a hairline rather than a shadow.
   ============================================================================ */

export function PortalHeading({
  eyebrow,
  title,
  lead,
  actions,
}: {
  eyebrow?: string
  title: string
  lead?: string
  actions?: ReactNode
}) {
  return (
    <div className="mb-7 flex flex-wrap items-end justify-between gap-4">
      <div className="min-w-0">
        {eyebrow && <p className="caption">{eyebrow}</p>}
        <h1 className="display-l mt-2.5 text-[clamp(1.75rem,4vw,2.75rem)] text-bone">{title}</h1>
        {lead && <p className="measure mt-3 text-[0.9375rem] leading-relaxed text-smoke">{lead}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2.5">{actions}</div>}
    </div>
  )
}

export function Panel({
  title,
  description,
  actions,
  children,
  className,
  padded = true,
}: {
  title?: string
  description?: string
  actions?: ReactNode
  children: ReactNode
  className?: string
  padded?: boolean
}) {
  return (
    <section
      className={cn(
        'overflow-hidden rounded-[var(--radius-card)] border border-[var(--hairline)] bg-carbon',
        className,
      )}
    >
      {(title || actions) && (
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--hairline)] px-5 py-4">
          <div className="min-w-0">
            {title && <h2 className="text-[0.9375rem] font-semibold tracking-[-0.01em] text-bone">{title}</h2>}
            {description && <p className="mt-1 text-[0.8125rem] leading-relaxed text-smoke">{description}</p>}
          </div>
          {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
        </header>
      )}
      <div className={cn(padded && 'p-5')}>{children}</div>
    </section>
  )
}

export function StatTile({
  label,
  value,
  sub,
  icon,
  tone = 'neutral',
}: {
  label: string
  value: ReactNode
  sub?: string
  icon?: IconName
  tone?: 'neutral' | 'accent' | 'success' | 'warn'
}) {
  const tones = {
    neutral: 'border-[var(--hairline)] bg-carbon',
    accent: 'border-[var(--accent-line)] bg-[color-mix(in_srgb,var(--accent)_7%,var(--carbon))]',
    success: 'border-success/35 bg-[color-mix(in_srgb,var(--success)_7%,var(--carbon))]',
    warn: 'border-accent-hot/35 bg-[color-mix(in_srgb,var(--accent-hot)_6%,var(--carbon))]',
  } as const

  return (
    <div className={cn('rounded-[var(--radius-card)] border p-5', tones[tone])}>
      <div className="flex items-start justify-between gap-3">
        <p className="caption">{label}</p>
        {icon && <Icon name={icon} size={17} className="text-smoke" />}
      </div>
      <p className="numeric display-m mt-3 text-[1.625rem] leading-none text-bone">{value}</p>
      {sub && <p className="mt-2 text-[0.75rem] leading-relaxed text-smoke">{sub}</p>}
    </div>
  )
}

/* ---------------------------------------------------------------- streak */

/**
 * The streak flame. The ring fills toward the member's own longest streak rather
 * than an arbitrary target — beating yourself is the only comparison that holds
 * for a member who has trained here for three years and one who joined in March.
 */
export function StreakFlame({ streak, size = 108 }: { streak: PortalStreak; size?: number }) {
  const target = Math.max(7, streak.longestStreakDays)
  const ratio = Math.min(1, streak.currentStreakDays / target)
  const stroke = 6
  const radius = (size - stroke) / 2
  const circumference = 2 * Math.PI * radius
  const isLive = streak.currentStreakDays > 0

  return (
    <div className="flex items-center gap-5">
      <div className="relative shrink-0" style={{ width: size, height: size }}>
        <svg
          width={size}
          height={size}
          viewBox={`0 0 ${size} ${size}`}
          className="-rotate-90"
          role="img"
          aria-label={`${streak.currentStreakDays}-day streak, personal best ${streak.longestStreakDays} days`}
        >
          <circle cx={size / 2} cy={size / 2} r={radius} fill="none" stroke="var(--steel)" strokeWidth={stroke} />
          <circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke={isLive ? 'var(--accent)' : 'var(--steel)'}
            strokeWidth={stroke}
            strokeLinecap="round"
            strokeDasharray={circumference}
            strokeDashoffset={circumference * (1 - ratio)}
            className="transition-[stroke-dashoffset] duration-[600ms] ease-out"
          />
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <Icon name="flame" size={20} className={isLive ? 'text-accent' : 'text-bone/25'} />
          <span className="numeric display-m mt-1 text-[1.5rem] leading-none text-bone">
            {streak.currentStreakDays}
          </span>
          <span className="caption text-[0.5625rem]">day{streak.currentStreakDays === 1 ? '' : 's'}</span>
        </div>
      </div>

      <div className="min-w-0">
        <p className="text-[0.9375rem] font-medium text-bone">
          {isLive ? 'Streak running' : 'No streak yet'}
        </p>
        <p className="mt-1.5 text-[0.875rem] leading-relaxed text-smoke">
          {isLive
            ? streak.currentStreakDays >= streak.longestStreakDays
              ? 'This is your longest run so far. Do not break it today.'
              : `Personal best is ${streak.longestStreakDays} days. ${
                  streak.longestStreakDays - streak.currentStreakDays
                } to go.`
            : 'Check in today and it starts at one.'}
        </p>
        <p className="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1 text-[0.75rem] text-smoke/75">
          <span className="numeric">{streak.visitsThisWeek} this week</span>
          <span aria-hidden>·</span>
          <span className="numeric">{streak.visitsThisMonth} this month</span>
        </p>
      </div>
    </div>
  )
}

/** Five weeks of visits as a dot grid — a habit you can see at a glance. */
export function StreakCalendar({ days }: { days: CalendarDay[] }) {
  return (
    <div>
      <div className="grid grid-cols-7 gap-1.5" role="list" aria-label="Attendance over the last five weeks">
        {days.map((day) => (
          <div
            key={day.date}
            role="listitem"
            title={`${day.date}${day.visited ? ' — visited' : ''}${
              day.classCount > 0 ? ` · ${day.classCount} class${day.classCount === 1 ? '' : 'es'}` : ''
            }`}
            className={cn(
              'aspect-square rounded-[5px] border transition-colors duration-200',
              day.visited
                ? day.classCount > 0
                  ? 'border-accent bg-accent'
                  : 'border-[var(--accent-line)] bg-[color-mix(in_srgb,var(--accent)_35%,transparent)]'
                : 'border-[var(--hairline)] bg-steel/40',
              day.isToday && 'ring-2 ring-bone/50 ring-offset-2 ring-offset-[var(--carbon)]',
            )}
          />
        ))}
      </div>
      <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1.5 text-[0.6875rem] text-smoke">
        <Legend className="bg-accent border-accent" label="Class attended" />
        <Legend className="bg-[color-mix(in_srgb,var(--accent)_35%,transparent)] border-[var(--accent-line)]" label="Gym visit" />
        <Legend className="bg-steel/40 border-[var(--hairline)]" label="No visit" />
      </div>
    </div>
  )
}

function Legend({ className, label }: { className: string; label: string }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <span aria-hidden className={cn('size-2.5 rounded-[3px] border', className)} />
      {label}
    </span>
  )
}

/* ---------------------------------------------------------------- overlays */

/**
 * Bottom sheet on a phone, centred dialog on a desktop. Focus is trapped to the
 * panel and Escape closes it, because a modal a keyboard user can tab out of is
 * a modal that has silently stopped being one.
 */
export function Sheet({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  width = 'md',
}: {
  open: boolean
  onClose: () => void
  title: string
  description?: string
  children: ReactNode
  footer?: ReactNode
  width?: 'sm' | 'md' | 'lg'
}) {
  const panelRef = useRef<HTMLDivElement>(null)
  const titleId = useId()
  const reduced = useReducedMotion()

  useEffect(() => {
    if (!open) return

    const previous = document.activeElement as HTMLElement | null
    document.body.style.overflow = 'hidden'
    panelRef.current?.focus()

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.stopPropagation()
        onClose()
        return
      }
      if (event.key !== 'Tab' || !panelRef.current) return

      const focusable = panelRef.current.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
      )
      if (focusable.length === 0) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      document.body.style.overflow = ''
      previous?.focus()
    }
  }, [open, onClose])

  if (!open) return null

  const widths = { sm: 'sm:max-w-md', md: 'sm:max-w-xl', lg: 'sm:max-w-3xl' } as const

  return (
    <div className="fixed inset-0 z-[var(--z-overlay)] flex items-end justify-center sm:items-center">
      <button
        type="button"
        aria-label="Close"
        onClick={onClose}
        className="absolute inset-0 h-full w-full cursor-default bg-ink/80 backdrop-blur-sm"
      />
      <motion.div
        ref={panelRef}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        initial={reduced ? false : { opacity: 0, y: 24 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.28, ease: [0.16, 1, 0.3, 1] }}
        className={cn(
          'relative flex max-h-[92dvh] w-full flex-col overflow-hidden border border-[var(--hairline-strong)] bg-carbon',
          'rounded-t-[var(--radius-sheet)] sm:rounded-[var(--radius-sheet)]',
          widths[width],
        )}
      >
        <header className="flex items-start justify-between gap-4 border-b border-[var(--hairline)] px-5 py-4">
          <div className="min-w-0">
            <h2 id={titleId} className="display-m text-[1.125rem] text-bone">
              {title}
            </h2>
            {description && <p className="mt-1.5 text-[0.8125rem] leading-relaxed text-smoke">{description}</p>}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="-mr-1 -mt-1 grid size-9 shrink-0 place-items-center rounded-full text-smoke transition-colors hover:bg-steel hover:text-bone"
          >
            <Icon name="x" size={18} />
          </button>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5">{children}</div>

        {footer && (
          <footer className="flex flex-wrap items-center justify-end gap-2.5 border-t border-[var(--hairline)] px-5 py-4">
            {footer}
          </footer>
        )}
      </motion.div>
    </div>
  )
}

/* ---------------------------------------------------------------- forms */

export function Field({
  label,
  hint,
  error,
  children,
  className,
}: {
  label: string
  hint?: string
  error?: string | null
  children: ReactNode
  className?: string
}) {
  return (
    <label className={cn('block', className)}>
      <span className="caption block text-[0.6875rem]">{label}</span>
      <div className="mt-2">{children}</div>
      {error ? (
        <span className="mt-1.5 block text-[0.75rem] text-accent-hot">{error}</span>
      ) : (
        hint && <span className="mt-1.5 block text-[0.75rem] leading-relaxed text-smoke/80">{hint}</span>
      )}
    </label>
  )
}

/** Filter pills. The active one is gold because it is a live control, not decoration. */
export function PillToggle({
  options,
  value,
  onChange,
  ariaLabel,
}: {
  options: { value: string; label: string; count?: number }[]
  value: string
  onChange: (value: string) => void
  ariaLabel: string
}) {
  return (
    <div className="flex flex-wrap gap-2" role="group" aria-label={ariaLabel}>
      {options.map((option) => {
        const active = option.value === value
        return (
          <button
            key={option.value}
            type="button"
            onClick={() => onChange(option.value)}
            aria-pressed={active}
            className={cn(
              'inline-flex min-h-9 items-center gap-1.5 rounded-full border px-3.5 text-[0.8125rem] transition-colors duration-200',
              active
                ? 'border-accent bg-[var(--accent-soft)] text-accent'
                : 'border-[var(--hairline-strong)] text-smoke hover:border-bone/35 hover:text-bone',
            )}
          >
            {option.label}
            {option.count != null && <span className="numeric text-[0.6875rem] opacity-70">{option.count}</span>}
          </button>
        )
      })}
    </div>
  )
}

/* ---------------------------------------------------------------- feedback */

/**
 * One-line confirmation under an action. Not a toast in a corner: the member is
 * looking at the thing they just tapped, which is where the answer belongs.
 */
export function InlineNote({
  tone = 'neutral',
  icon,
  children,
  className,
}: {
  tone?: 'neutral' | 'success' | 'warn' | 'danger'
  icon?: IconName
  children: ReactNode
  className?: string
}) {
  const tones = {
    neutral: 'border-[var(--hairline-strong)] text-smoke',
    success: 'border-success/40 bg-[color-mix(in_srgb,var(--success)_9%,transparent)] text-success',
    warn: 'border-[var(--accent-line)] bg-[var(--accent-soft)] text-accent',
    danger: 'border-accent-hot/45 bg-[color-mix(in_srgb,var(--accent-hot)_9%,transparent)] text-accent-hot',
  } as const

  return (
    <p
      role={tone === 'danger' ? 'alert' : undefined}
      className={cn(
        'flex items-start gap-2.5 rounded-[var(--radius-card)] border px-4 py-3 text-[0.8125rem] leading-relaxed',
        tones[tone],
        className,
      )}
    >
      {icon && <Icon name={icon} size={15} className="mt-0.5 shrink-0" />}
      <span>{children}</span>
    </p>
  )
}

/** The checkmark that draws itself (03 §6 booking feedback). */
export function DrawnCheck({ size = 56 }: { size?: number }) {
  const reduced = useReducedMotion()
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 52 52"
      fill="none"
      stroke="currentColor"
      strokeWidth={2.5}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
      className="text-success"
    >
      <motion.circle
        cx="26"
        cy="26"
        r="23"
        initial={reduced ? false : { pathLength: 0 }}
        animate={{ pathLength: 1 }}
        transition={{ duration: 0.45, ease: 'easeOut' }}
      />
      <motion.path
        d="M15 27l8 8 15-16"
        initial={reduced ? false : { pathLength: 0 }}
        animate={{ pathLength: 1 }}
        transition={{ duration: 0.35, delay: 0.3, ease: 'easeOut' }}
      />
    </svg>
  )
}

export function DangerButton(props: React.ComponentProps<typeof Button>) {
  return <Button variant="outline" {...props} className={cn('hover:!border-accent-hot hover:!text-accent-hot', props.className)} />
}

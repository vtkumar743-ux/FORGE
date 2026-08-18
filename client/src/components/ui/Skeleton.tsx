import { cn } from '@/lib/utils'
import { Icon, type IconName } from './Icon'
import { ButtonLink } from './Button'

/** Skeletons everywhere, spinners nowhere (03 §6). */
export function Skeleton({ className, rounded = 'card' }: { className?: string; rounded?: 'card' | 'pill' | 'none' }) {
  return (
    <div
      aria-hidden
      className={cn(
        'skeleton',
        rounded === 'pill' && 'rounded-full',
        rounded === 'none' && 'rounded-none',
        className,
      )}
    />
  )
}

export function SkeletonText({ lines = 3, className }: { lines?: number; className?: string }) {
  return (
    <div className={cn('space-y-2.5', className)} aria-hidden>
      {Array.from({ length: lines }).map((_, index) => (
        <Skeleton
          key={index}
          rounded="pill"
          className={cn('h-3', index === lines - 1 ? 'w-2/5' : index % 2 === 0 ? 'w-full' : 'w-11/12')}
        />
      ))}
    </div>
  )
}

/** Announces loading to assistive tech without rendering a visible spinner. */
export function LoadingRegion({ label = 'Loading', className }: { label?: string; className?: string }) {
  return (
    <div className={className} role="status" aria-live="polite">
      <span className="sr-only">{label}</span>
    </div>
  )
}

/**
 * Real empty states, not blank panels (03 §9.5). Every list in the admin panel and
 * the portal routes through this.
 */
export function EmptyState({
  icon = 'sparkles',
  headline,
  body,
  actionLabel,
  actionTo,
  className,
}: {
  icon?: IconName
  headline: string
  body?: string
  actionLabel?: string
  actionTo?: string
  className?: string
}) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center rounded-[var(--radius-card)]',
        'border border-dashed border-[var(--hairline-strong)] px-8 py-14 text-center',
        className,
      )}
    >
      <span className="mb-5 inline-flex size-12 items-center justify-center rounded-full border border-[var(--hairline)] text-accent">
        <Icon name={icon} size={22} />
      </span>
      <h3 className="display-m text-[1.25rem] text-bone">{headline}</h3>
      {body && <p className="measure mt-2.5 text-[0.9375rem] leading-relaxed text-smoke">{body}</p>}
      {actionLabel && actionTo && (
        <ButtonLink to={actionTo} size="sm" className="mt-6">
          {actionLabel}
        </ButtonLink>
      )}
    </div>
  )
}

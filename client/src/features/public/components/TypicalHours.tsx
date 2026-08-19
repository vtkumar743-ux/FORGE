import { useMemo, useState } from 'react'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { useTypicalHours } from '@/lib/public-api'
import { cn } from '@/lib/utils'

/**
 * The typical-busy-hours chart that sits beside the live gauge (Module 4.1).
 *
 * The gauge answers "should I come now". This answers "when should I come" — which is the
 * question someone actually has when they open the page from their desk at 3 PM. Bars are
 * the eight-week average for that weekday hour, on the IST wall clock, with today's column
 * selected on arrival and the current hour marked.
 */

const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'] as const

export function TypicalHours({
  branchSlug,
  className,
  enabled = true,
}: {
  branchSlug: string | undefined
  className?: string
  enabled?: boolean
}) {
  // The reader is standing in India; the browser may not be.
  const nowIst = useMemo(() => new Date(Date.now() + 5.5 * 60 * 60 * 1000), [])
  const todayIndex = nowIst.getUTCDay()
  const currentHour = nowIst.getUTCHours()

  const [day, setDay] = useState(todayIndex)
  const { data, isLoading } = useTypicalHours(branchSlug, enabled)

  const bars = useMemo(
    () => (data?.hours ?? []).filter((point) => point.dayOfWeek === day).sort((a, b) => a.hour - b.hour),
    [data, day],
  )

  const peak = Math.max(1, ...bars.map((bar) => bar.percentOfCapacity))
  const hasData = bars.some((bar) => bar.averageVisits > 0)

  if (!enabled) return null

  if (isLoading) {
    return (
      <div className={cn('space-y-3', className)} aria-busy="true">
        <Skeleton className="h-4 w-40" rounded="pill" />
        <Skeleton className="h-28 w-full" />
      </div>
    )
  }

  return (
    <div className={cn('space-y-4', className)}>
      <div className="flex flex-wrap items-baseline justify-between gap-3">
        <h4 className="text-caption tracking-[0.14em] text-smoke uppercase">Typically busy</h4>
        {data?.busiestLabel && (
          <p className="text-caption text-smoke">
            Busiest <span className="text-bone">{data.busiestLabel}</span>
          </p>
        )}
      </div>

      <div role="tablist" aria-label="Day of week" className="flex flex-wrap gap-1.5">
        {DAYS.map((label, index) => (
          <button
            key={label}
            type="button"
            role="tab"
            aria-selected={day === index}
            onClick={() => setDay(index)}
            className={cn(
              'rounded-full px-3 py-1 text-caption transition-colors duration-200',
              'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--accent)]',
              day === index
                ? 'bg-bone text-ink'
                : 'border border-steel text-smoke hover:border-bone/40 hover:text-bone',
            )}
          >
            {label}
            {index === todayIndex && <span className="sr-only"> (today)</span>}
          </button>
        ))}
      </div>

      {hasData ? (
        <>
          <div className="flex h-28 items-end gap-[3px]" aria-hidden="true">
            {bars.map((bar) => {
              const height = Math.max(4, Math.round((bar.percentOfCapacity / peak) * 100))
              const isNow = day === todayIndex && bar.hour === currentHour
              return (
                <div
                  key={bar.hour}
                  className="group relative flex-1"
                  style={{ height: '100%' }}
                  title={`${bar.label} · typically ${bar.percentOfCapacity}% full`}
                >
                  <div
                    className={cn(
                      'absolute bottom-0 w-full rounded-t-[2px] transition-colors duration-200',
                      isNow ? 'bg-[var(--accent)]' : 'bg-steel group-hover:bg-bone/40',
                    )}
                    style={{ height: `${height}%` }}
                  />
                </div>
              )
            })}
          </div>

          <div className="flex justify-between text-caption text-smoke">
            <span>6 AM</span>
            <span>2 PM</span>
            <span>11 PM</span>
          </div>

          {/* The chart is decorative to a screen reader; this sentence is the content. */}
          <p className="sr-only">
            {DAYS[day]}: busiest around{' '}
            {bars.reduce((best, bar) => (bar.percentOfCapacity > best.percentOfCapacity ? bar : best), bars[0]).label}.
          </p>

          {day === todayIndex && (
            <p className="flex items-center gap-2 text-caption text-smoke">
              <Icon name="clock" className="size-3.5" aria-hidden="true" />
              Right now is typically{' '}
              <span className="text-bone">
                {bars.find((bar) => bar.hour === currentHour)?.percentOfCapacity ?? 0}% full
              </span>
            </p>
          )}
        </>
      ) : (
        <p className="text-body-s text-smoke">
          Not enough history at this branch yet — the pattern appears once a few weeks of visits are in.
        </p>
      )}
    </div>
  )
}

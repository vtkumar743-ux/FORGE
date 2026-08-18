import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { cn } from '@/lib/utils'
import type { BranchOccupancy } from '@/lib/cms'

/**
 * The live occupancy gauge (03 §7) — a 240° SVG arc per branch, reading Comfortable /
 * Busy / Peak. Almost no gym publishes how full its floor is, which is exactly why we
 * made it a visual signature rather than a number in a corner.
 *
 * Phase 1 feeds it from the polling endpoint. Phase 4 swaps the source for the SignalR
 * push; the component takes the same payload either way, so nothing here changes.
 */

const BANDS = [
  { label: 'Comfortable', tone: 'var(--success)', copy: 'Racks free, no waiting.' },
  { label: 'Busy', tone: 'var(--accent)', copy: 'Filling up — expect to share a rack.' },
  { label: 'Peak', tone: 'var(--accent-hot)', copy: 'Full floor. Try after 9 PM.' },
] as const

export function OccupancyMeter({
  occupancy,
  size = 132,
  showCopy = true,
  className,
}: {
  occupancy: BranchOccupancy | undefined
  size?: number
  showCopy?: boolean
  className?: string
}) {
  if (!occupancy) return <OccupancyMeterSkeleton size={size} className={className} />

  const band = BANDS[Math.min(2, Math.max(0, occupancy.band))]
  const ratio = Math.min(1, Math.max(0, occupancy.percentFull / 100))

  // A 240° sweep starting bottom-left: the open gap reads as a gauge, not a pie chart.
  const stroke = 8
  const radius = (size - stroke) / 2
  const circumference = 2 * Math.PI * radius
  const arc = circumference * (240 / 360)

  return (
    <div className={cn('flex items-center gap-5', className)}>
      <div className="relative shrink-0" style={{ width: size, height: size }}>
        <svg
          width={size}
          height={size}
          viewBox={`0 0 ${size} ${size}`}
          style={{ transform: 'rotate(150deg)' }}
          role="img"
          aria-label={`${occupancy.branchName}: ${band.label}, ${occupancy.percentFull}% full`}
        >
          <circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke="var(--steel)"
            strokeWidth={stroke}
            strokeLinecap="round"
            strokeDasharray={`${arc} ${circumference}`}
          />
          <circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke={band.tone}
            strokeWidth={stroke}
            strokeLinecap="round"
            strokeDasharray={`${arc * ratio} ${circumference}`}
            className="transition-[stroke-dasharray,stroke] duration-[400ms] ease-out"
          />
        </svg>

        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className="numeric display-m text-[1.75rem] leading-none" style={{ color: band.tone }}>
            {occupancy.percentFull}
            <span className="text-[0.9rem] align-super">%</span>
          </span>
          <span className="caption mt-1 text-[0.625rem]">full</span>
        </div>
      </div>

      <div className="min-w-0">
        <p className="flex items-center gap-2 text-[0.9375rem] font-medium text-bone">
          <span
            aria-hidden
            className="inline-block size-2 rounded-full"
            style={{ backgroundColor: band.tone, boxShadow: `0 0 0 4px color-mix(in srgb, ${band.tone} 22%, transparent)` }}
          />
          {band.label}
        </p>
        {showCopy && <p className="mt-1.5 text-[0.875rem] leading-relaxed text-smoke">{band.copy}</p>}
        <p className="mt-2 flex items-center gap-1.5 text-[0.75rem] text-smoke/70">
          <Icon name="users" size={13} />
          <span className="numeric">
            {occupancy.currentCount} of {occupancy.capacity}
          </span>
          <span aria-hidden>·</span>
          <span>updated live</span>
        </p>
      </div>
    </div>
  )
}

export function OccupancyMeterSkeleton({ size = 132, className }: { size?: number; className?: string }) {
  return (
    <div className={cn('flex items-center gap-5', className)} aria-busy="true">
      <div className="skeleton shrink-0 rounded-full" style={{ width: size, height: size }} aria-hidden />
      <div className="w-full space-y-2">
        <Skeleton rounded="pill" className="h-4 w-28" />
        <Skeleton rounded="pill" className="h-3 w-40" />
      </div>
      <span className="sr-only">Reading the floor</span>
    </div>
  )
}

/** Compact inline variant for cards where the full gauge would dominate. */
export function OccupancyChip({ occupancy }: { occupancy: BranchOccupancy | undefined }) {
  if (!occupancy) return <Skeleton rounded="pill" className="h-7 w-32" />

  const band = BANDS[Math.min(2, Math.max(0, occupancy.band))]

  return (
    <span
      className="inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-[0.75rem] font-medium"
      style={{ borderColor: `color-mix(in srgb, ${band.tone} 45%, transparent)`, color: band.tone }}
    >
      <span aria-hidden className="inline-block size-1.5 animate-pulse rounded-full" style={{ backgroundColor: band.tone }} />
      {band.label}
      <span className="numeric text-smoke">{occupancy.percentFull}%</span>
    </span>
  )
}

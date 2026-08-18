import { useCallback, useEffect, useRef, useState } from 'react'
import { Icon } from '@/components/ui/Icon'
import { cn } from '@/lib/utils'

/**
 * Before/after comparison slider (03 §7). Drag, or use the arrow keys — the handle is a
 * real range input underneath, so it is keyboard-operable and announced correctly without
 * re-implementing a slider's semantics in divs.
 *
 * Both images are clipped from the same box rather than cross-faded, so the two frames
 * stay pixel-aligned and the comparison is honest.
 */
export function BeforeAfterSlider({
  beforeUrl,
  afterUrl,
  alt,
  handleLabel = 'Drag to compare',
  ratio = '3/4',
  className,
}: {
  beforeUrl: string
  afterUrl: string
  alt: string
  handleLabel?: string
  ratio?: string
  className?: string
}) {
  const [position, setPosition] = useState(50)
  const [dragging, setDragging] = useState(false)
  const frameRef = useRef<HTMLDivElement>(null)

  const moveTo = useCallback((clientX: number) => {
    const frame = frameRef.current
    if (!frame) return
    const rect = frame.getBoundingClientRect()
    const next = ((clientX - rect.left) / rect.width) * 100
    setPosition(Math.min(100, Math.max(0, next)))
  }, [])

  useEffect(() => {
    if (!dragging) return

    const onMove = (event: PointerEvent) => moveTo(event.clientX)
    const onUp = () => setDragging(false)

    window.addEventListener('pointermove', onMove)
    window.addEventListener('pointerup', onUp)
    return () => {
      window.removeEventListener('pointermove', onMove)
      window.removeEventListener('pointerup', onUp)
    }
  }, [dragging, moveTo])

  return (
    <div
      ref={frameRef}
      className={cn('relative select-none overflow-hidden rounded-[var(--radius-card)] bg-steel', className)}
      style={{ aspectRatio: ratio }}
      onPointerDown={(event) => {
        setDragging(true)
        moveTo(event.clientX)
      }}
    >
      <img
        src={afterUrl}
        alt={`${alt} — after`}
        loading="lazy"
        decoding="async"
        className="graded absolute inset-0 h-full w-full object-cover"
        draggable={false}
      />
      <img
        src={beforeUrl}
        alt={`${alt} — before`}
        loading="lazy"
        decoding="async"
        className="graded absolute inset-0 h-full w-full object-cover"
        style={{ clipPath: `inset(0 ${100 - position}% 0 0)` }}
        draggable={false}
      />

      <span className="pointer-events-none absolute left-4 top-4 rounded-full border border-[var(--hairline-strong)] bg-ink/65 px-2.5 py-1 text-[0.6875rem] uppercase tracking-[0.08em] text-bone/80 backdrop-blur-sm">
        Before
      </span>
      <span className="pointer-events-none absolute right-4 top-4 rounded-full border border-[var(--accent-line)] bg-ink/65 px-2.5 py-1 text-[0.6875rem] uppercase tracking-[0.08em] text-accent backdrop-blur-sm">
        After
      </span>

      {/* The visible seam and grip. */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-y-0 w-px bg-accent"
        style={{ left: `${position}%` }}
      />
      <div
        aria-hidden
        className={cn(
          'pointer-events-none absolute top-1/2 flex size-11 -translate-x-1/2 -translate-y-1/2 items-center justify-center',
          'rounded-full border border-accent bg-ink/80 text-accent backdrop-blur-sm transition-transform duration-200 ease-out',
          dragging && 'scale-110',
        )}
        style={{ left: `${position}%` }}
      >
        <Icon name="chevron-left" size={15} />
        <Icon name="chevron-right" size={15} className="-ml-1" />
      </div>

      {/* The actual control: invisible, full-width, and the only thing focus lands on. */}
      <input
        type="range"
        min={0}
        max={100}
        value={Math.round(position)}
        onChange={(event) => setPosition(Number(event.target.value))}
        aria-label={`${handleLabel} — ${alt}`}
        className="absolute inset-0 h-full w-full cursor-ew-resize opacity-0"
      />

      <span className="pointer-events-none absolute inset-x-0 bottom-4 text-center text-[0.6875rem] uppercase tracking-[0.08em] text-bone/55">
        {handleLabel}
      </span>
    </div>
  )
}

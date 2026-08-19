import { useEffect, useRef, useState } from 'react'
import { Icon } from '@/components/ui/Icon'
import { cn } from '@/lib/utils'

/**
 * Rest timer.
 *
 * It counts against a wall-clock deadline rather than decrementing a counter on a
 * setInterval tick: a phone that sleeps between sets — which is what phones do
 * between sets — freezes the interval, and a timer that quietly loses 90 seconds
 * is worse than no timer. Coming back to the screen re-reads the clock.
 */
export function RestTimer({
  seconds,
  onDone,
  onDismiss,
  label,
}: {
  seconds: number
  onDone?: () => void
  onDismiss?: () => void
  label?: string
}) {
  const [remaining, setRemaining] = useState(seconds)
  const [running, setRunning] = useState(true)
  const deadlineRef = useRef<number>(Date.now() + seconds * 1000)
  const firedRef = useRef(false)

  useEffect(() => {
    deadlineRef.current = Date.now() + seconds * 1000
    firedRef.current = false
    setRemaining(seconds)
    setRunning(true)
  }, [seconds])

  useEffect(() => {
    if (!running) return

    function tick() {
      const left = Math.max(0, Math.round((deadlineRef.current - Date.now()) / 1000))
      setRemaining(left)
      if (left === 0 && !firedRef.current) {
        firedRef.current = true
        setRunning(false)
        onDone?.()
      }
    }

    tick()
    const timer = window.setInterval(tick, 250)
    // A backgrounded tab throttles intervals; re-reading on return corrects the drift.
    const onVisible = () => tick()
    document.addEventListener('visibilitychange', onVisible)

    return () => {
      window.clearInterval(timer)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [running, onDone])

  const ratio = seconds === 0 ? 0 : remaining / seconds
  const size = 76
  const stroke = 5
  const radius = (size - stroke) / 2
  const circumference = 2 * Math.PI * radius
  const done = remaining === 0

  function adjust(delta: number) {
    deadlineRef.current = Math.max(Date.now(), deadlineRef.current + delta * 1000)
    firedRef.current = false
    setRunning(true)
    setRemaining(Math.max(0, Math.round((deadlineRef.current - Date.now()) / 1000)))
  }

  return (
    <div
      role="timer"
      aria-live="off"
      className={cn(
        'flex items-center gap-4 rounded-[var(--radius-card)] border px-4 py-3.5',
        done
          ? 'border-success/45 bg-[color-mix(in_srgb,var(--success)_8%,var(--carbon))]'
          : 'border-[var(--accent-line)] bg-[var(--accent-soft)]',
      )}
    >
      <div className="relative shrink-0" style={{ width: size, height: size }}>
        <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} className="-rotate-90" aria-hidden>
          <circle cx={size / 2} cy={size / 2} r={radius} fill="none" stroke="var(--steel)" strokeWidth={stroke} />
          <circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke={done ? 'var(--success)' : 'var(--accent)'}
            strokeWidth={stroke}
            strokeLinecap="round"
            strokeDasharray={circumference}
            strokeDashoffset={circumference * (1 - ratio)}
            style={{ transition: 'stroke-dashoffset 250ms linear' }}
          />
        </svg>
        <span className="numeric absolute inset-0 grid place-items-center text-[1.0625rem] font-semibold text-bone">
          {done ? 'Go' : formatSeconds(remaining)}
        </span>
      </div>

      <div className="min-w-0 flex-1">
        <p className={cn('text-[0.875rem] font-medium', done ? 'text-success' : 'text-accent')}>
          {done ? 'Rest over — next set' : 'Resting'}
        </p>
        {label && <p className="mt-0.5 truncate text-[0.75rem] text-smoke">{label}</p>}
        <div className="mt-2 flex flex-wrap items-center gap-1.5">
          <TimerButton onClick={() => adjust(-15)} ariaLabel="Take 15 seconds off">
            −15s
          </TimerButton>
          <TimerButton onClick={() => adjust(30)} ariaLabel="Add 30 seconds">
            +30s
          </TimerButton>
          <TimerButton onClick={() => setRunning((value) => !value)} ariaLabel={running ? 'Pause' : 'Resume'}>
            {running ? 'Pause' : 'Resume'}
          </TimerButton>
          {onDismiss && (
            <button
              type="button"
              onClick={onDismiss}
              aria-label="Dismiss the timer"
              className="grid size-8 place-items-center rounded-full text-smoke transition-colors hover:bg-steel hover:text-bone"
            >
              <Icon name="x" size={14} />
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

function TimerButton({
  onClick,
  ariaLabel,
  children,
}: {
  onClick: () => void
  ariaLabel: string
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={ariaLabel}
      className="min-h-8 rounded-full border border-[var(--hairline-strong)] px-2.5 text-[0.6875rem] text-smoke transition-colors hover:border-bone/35 hover:text-bone"
    >
      {children}
    </button>
  )
}

function formatSeconds(total: number): string {
  const minutes = Math.floor(total / 60)
  const seconds = total % 60
  return minutes > 0 ? `${minutes}:${String(seconds).padStart(2, '0')}` : String(seconds)
}

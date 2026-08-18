import { useEffect, useRef, useState } from 'react'
import { useInView, useReducedMotion } from 'motion/react'
import { Reveal } from '@/components/ui/Reveal'
import { formatCount } from '@/lib/utils'
import type { StatBandContent } from './schemas'

/**
 * Animated stat band (03 §6). Big numbers count up once, on viewport entry. Gold is
 * allowed here because a key metric is one of the three sanctioned accent uses.
 */
export function StatBandSection({ content }: { content: StatBandContent }) {
  const ref = useRef<HTMLDivElement>(null)
  const inView = useInView(ref, { once: true, amount: 0.35 })

  return (
    <section className="section-y bg-ink" aria-label="Key numbers">
      <div className="shell">
        {content.eyebrow && (
          <Reveal>
            <p className="caption">{content.eyebrow}</p>
          </Reveal>
        )}

        <div
          ref={ref}
          className="mt-10 grid gap-x-8 gap-y-12 border-t border-[var(--hairline)] pt-12 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5"
        >
          {content.stats.map((stat, index) => (
            <Reveal key={stat.label} delay={index * 0.07}>
              <div>
                <p className="numeric display-l text-accent">
                  {stat.prefix}
                  <CountUp value={stat.value} active={inView} />
                  {stat.suffix}
                </p>
                <p className="mt-3 max-w-[16rem] text-[0.9375rem] leading-snug text-smoke">
                  {stat.label}
                </p>
              </div>
            </Reveal>
          ))}
        </div>

        {content.footnote && (
          <Reveal delay={0.1}>
            <p className="mt-10 max-w-2xl text-[0.75rem] leading-relaxed text-smoke/70">
              {content.footnote}
            </p>
          </Reveal>
        )}
      </div>
    </section>
  )
}

/** Eased count-up driven by rAF, so it never blocks paint or fights React state batching. */
function CountUp({ value, active, duration = 1400 }: { value: number; active: boolean; duration?: number }) {
  const reduced = useReducedMotion()
  const [display, setDisplay] = useState(reduced ? value : 0)

  useEffect(() => {
    if (!active || reduced) {
      if (reduced) setDisplay(value)
      return
    }

    let frame = 0
    const start = performance.now()

    const tick = (now: number) => {
      const progress = Math.min(1, (now - start) / duration)
      // easeOutExpo — fast then settling, which reads as confident rather than mechanical.
      const eased = progress === 1 ? 1 : 1 - Math.pow(2, -10 * progress)
      setDisplay(Math.round(value * eased))
      if (progress < 1) frame = requestAnimationFrame(tick)
    }

    frame = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(frame)
  }, [active, value, duration, reduced])

  return <>{formatCount(display)}</>
}

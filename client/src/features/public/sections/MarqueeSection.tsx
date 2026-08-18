import { cn } from '@/lib/utils'
import type { MarqueeContent } from './schemas'

/**
 * Marquee ticker strip (03 §5): auto-scrolling outlined display type used as a
 * divider between major sections. The list is duplicated once so the CSS keyframe
 * can translate -50% and loop seamlessly. Pauses on hover; frozen under
 * prefers-reduced-motion by the global base-layer rule.
 */
export function MarqueeSection({ content }: { content: MarqueeContent }) {
  const items = content.items
  const isOutline = content.style === 'outline'

  return (
    <section
      className="marquee hairline-t hairline-b relative overflow-hidden bg-ink py-7"
      aria-label="What we train"
    >
      <div
        className="marquee-track items-baseline gap-8"
        data-direction={content.direction}
        style={{ ['--marquee-duration' as string]: `${content.speedSeconds}s` }}
      >
        {[0, 1].map((copy) => (
          <ul
            key={copy}
            className="flex shrink-0 items-baseline gap-8"
            aria-hidden={copy === 1 || undefined}
          >
            {items.map((item, index) => (
              <li key={`${copy}-${item}-${index}`} className="flex shrink-0 items-baseline gap-8">
                <span
                  className={cn(
                    'font-display text-[clamp(1.75rem,4vw,3rem)] font-semibold uppercase leading-none tracking-[-0.01em]',
                    isOutline ? 'text-outline' : 'text-bone/85',
                  )}
                >
                  {item}
                </span>
                <span aria-hidden className="text-[1.25rem] leading-none text-accent/70">
                  {content.separator}
                </span>
              </li>
            ))}
          </ul>
        ))}
      </div>
    </section>
  )
}

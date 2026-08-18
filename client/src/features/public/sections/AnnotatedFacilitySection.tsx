import { useState } from 'react'
import { Reveal } from '@/components/ui/Reveal'
import { SectionHeader } from '../components/SectionHeader'
import { cn } from '@/lib/utils'
import type { AnnotatedFacilityContent } from './schemas'

/**
 * Annotated facility photograph (03 §4, the Gymshark technique): thin gold leader lines
 * from a dot on the equipment to a named label. It answers "what is actually on this
 * floor" far better than a bulleted amenity list, and it is the section that makes a
 * stock interior shot read as our floor.
 *
 * Coordinates are percentages, so the annotations survive any crop or aspect change.
 * Below the image the same callouts render as a plain list — that is the mobile layout
 * and, not coincidentally, the accessible one.
 */
export function AnnotatedFacilitySection({ content }: { content: AnnotatedFacilityContent }) {
  const [active, setActive] = useState<number | null>(null)
  const lineColour = content.leaderLineColor === 'bone' ? 'var(--bone)' : 'var(--accent)'

  return (
    <section className="section-y bg-ink">
      <div className="shell">
        <SectionHeader eyebrow={content.eyebrow} headline={content.headline} />

        <Reveal distance={32} className="mt-12">
          <figure className="relative overflow-hidden rounded-[var(--radius-card)] bg-steel">
            <img
              src={content.imageUrl}
              alt={content.imageAlt ?? ''}
              loading="lazy"
              decoding="async"
              className="graded aspect-[16/9] w-full object-cover"
            />
            <div aria-hidden className="absolute inset-0 bg-ink/25" />

            {/* Hidden below md: at phone width the leader lines would overlap into noise. */}
            <div className="pointer-events-none absolute inset-0 hidden md:block">
              {content.callouts.map((callout, index) => {
                const goesLeft = callout.x > 55
                const isActive = active === index

                return (
                  <div
                    key={callout.label}
                    className="pointer-events-auto absolute"
                    style={{ left: `${callout.x}%`, top: `${callout.y}%` }}
                    onMouseEnter={() => setActive(index)}
                    onMouseLeave={() => setActive(null)}
                  >
                    <span
                      aria-hidden
                      className="absolute -left-1.5 -top-1.5 block size-3 rounded-full transition-transform duration-200 ease-out"
                      style={{
                        backgroundColor: lineColour,
                        // A soft halo so the dot survives a busy patch of photograph.
                        boxShadow: `0 0 0 6px color-mix(in srgb, ${lineColour} 18%, transparent)`,
                        transform: isActive ? 'scale(1.25)' : 'scale(1)',
                      }}
                    />
                    <span
                      aria-hidden
                      className="absolute top-0 block h-px transition-[width] duration-300 ease-out"
                      style={{
                        backgroundColor: lineColour,
                        width: isActive ? '3.5rem' : '2.5rem',
                        left: goesLeft ? undefined : 0,
                        right: goesLeft ? 0 : undefined,
                      }}
                    />
                    <div
                      className={cn(
                        'absolute top-0 w-max max-w-[16rem] -translate-y-1/2 rounded-[var(--radius-card)]',
                        'border border-[var(--hairline-strong)] bg-ink/80 px-3.5 py-2.5 backdrop-blur-sm',
                        'transition-[opacity,transform] duration-200 ease-out',
                        goesLeft ? 'right-[3.75rem] text-right' : 'left-[3.75rem]',
                        isActive ? 'opacity-100' : 'opacity-90',
                      )}
                      style={{ borderColor: isActive ? lineColour : undefined }}
                    >
                      <p className="text-[0.8125rem] font-medium text-bone">{callout.label}</p>
                      {callout.detail && <p className="mt-1 text-[0.75rem] leading-snug text-smoke">{callout.detail}</p>}
                    </div>
                  </div>
                )
              })}
            </div>
          </figure>
        </Reveal>

        <ul className="mt-8 grid gap-x-10 gap-y-4 sm:grid-cols-2 md:hidden">
          {content.callouts.map((callout) => (
            <li key={callout.label} className="flex gap-3">
              <span aria-hidden className="mt-2 size-2 shrink-0 rounded-full" style={{ backgroundColor: lineColour }} />
              <div>
                <p className="text-[0.9375rem] text-bone">{callout.label}</p>
                {callout.detail && <p className="mt-0.5 text-[0.8125rem] leading-snug text-smoke">{callout.detail}</p>}
              </div>
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}

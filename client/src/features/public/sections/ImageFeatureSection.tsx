import { Reveal } from '@/components/ui/Reveal'
import { ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { cn } from '@/lib/utils'
import type { ImageFeatureContent } from './schemas'

/**
 * Image + copy, alternating sides down the page (03 §5, §9.3). `full` breaks the shell for
 * a 100vw band — the rhythm the layout grammar asks for: full-bleed image, constrained
 * text, bento, full-bleed again.
 */
export function ImageFeatureSection({ content }: { content: ImageFeatureContent }) {
  if (content.imagePosition === 'full') {
    return (
      <section className="relative isolate grain overflow-hidden">
        <img
          src={content.imageUrl}
          alt={content.imageAlt ?? ''}
          loading="lazy"
          decoding="async"
          className="graded h-[min(70svh,38rem)] w-full object-cover"
        />
        <div aria-hidden className="absolute inset-0 bg-gradient-to-t from-ink via-ink/60 to-ink/20" />

        <div className="absolute inset-0 flex items-end">
          <div className="shell pb-16">
            <div className="max-w-2xl">
              {content.eyebrow && <p className="caption">{content.eyebrow}</p>}
              <h2 className="display-l mt-5 text-bone">{content.headline}</h2>
              {content.body && <p className="measure mt-6 text-body-l leading-relaxed text-bone/80">{content.body}</p>}
              {content.cta && (
                <ButtonLink to={content.cta.href} className="mt-8" magnetic>
                  {content.cta.label}
                </ButtonLink>
              )}
            </div>
          </div>
        </div>
      </section>
    )
  }

  const imageRight = content.imagePosition === 'right'

  return (
    <section className="section-y bg-ink">
      <div className="shell grid items-center gap-12 lg:grid-cols-12 lg:gap-16">
        <div className={cn('lg:col-span-6', imageRight ? 'lg:order-1' : 'lg:order-2')}>
          {content.eyebrow && (
            <Reveal>
              <p className="caption">{content.eyebrow}</p>
            </Reveal>
          )}

          <Reveal delay={0.05}>
            <h2 className="display-l mt-5 text-bone">{content.headline}</h2>
          </Reveal>

          {content.body && (
            <Reveal delay={0.1}>
              <p className="measure mt-6 text-body-l leading-relaxed text-smoke">{content.body}</p>
            </Reveal>
          )}

          {content.bullets && content.bullets.length > 0 && (
            <Reveal delay={0.15}>
              <ul className="mt-7 space-y-3">
                {content.bullets.map((bullet) => (
                  <li key={bullet} className="flex items-start gap-2.5 text-[0.9375rem] leading-snug text-bone/85">
                    <Icon name="check" size={16} strokeWidth={2} className="mt-0.5 shrink-0 text-accent" />
                    {bullet}
                  </li>
                ))}
              </ul>
            </Reveal>
          )}

          {content.cta && (
            <Reveal delay={0.2}>
              <ButtonLink to={content.cta.href} variant="outline" className="mt-9" icon="arrow-right" iconAfter>
                {content.cta.label}
              </ButtonLink>
            </Reveal>
          )}
        </div>

        <Reveal distance={36} className={cn('lg:col-span-6', imageRight ? 'lg:order-2' : 'lg:order-1')}>
          <figure className="overflow-hidden rounded-[var(--radius-card)]">
            <img
              src={content.imageUrl}
              alt={content.imageAlt ?? ''}
              loading="lazy"
              decoding="async"
              className="graded aspect-[4/3] w-full object-cover"
            />
          </figure>
        </Reveal>
      </div>
    </section>
  )
}

import { Reveal, KineticHeading } from '@/components/ui/Reveal'
import { cn } from '@/lib/utils'
import type { ManifestoContent } from './schemas'
import { Photo } from '@/components/ui/Photo'

/**
 * Full-bleed image ↔ constrained-text alternation (03 §5, the Third Space rhythm).
 * imagePosition alternates down the page so the layout is asymmetric on purpose.
 */
export function ManifestoSection({ content }: { content: ManifestoContent }) {
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

          <KineticHeading className="display-l mt-5 text-bone" as="h2">
            {content.headline}
          </KineticHeading>

          <Reveal delay={0.08}>
            <p className="measure mt-7 text-body-l leading-relaxed text-smoke">{content.body}</p>
          </Reveal>

          {content.signatureLine && (
            <Reveal delay={0.14}>
              <p className="mt-8 flex items-center gap-3 text-[0.8125rem] uppercase tracking-[0.08em] text-bone/60">
                <span aria-hidden className="h-px w-10 bg-accent" />
                {content.signatureLine}
              </p>
            </Reveal>
          )}
        </div>

        {content.imageUrl && (
          <Reveal
            distance={36}
            className={cn('lg:col-span-6', imageRight ? 'lg:order-2' : 'lg:order-1')}
          >
            <figure className="relative overflow-hidden rounded-[var(--radius-card)]">
              <Photo
                src={content.imageUrl}
                alt={content.imageAlt ?? ''}
                sizes="(min-width: 1024px) 40vw, 100vw"
                className="aspect-[4/5] w-full object-cover"
              />
            </figure>
          </Reveal>
        )}
      </div>
    </section>
  )
}

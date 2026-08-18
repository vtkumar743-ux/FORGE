import { Reveal } from '@/components/ui/Reveal'
import { Badge } from '@/components/ui/Card'
import { EmptyState, Skeleton, SkeletonText } from '@/components/ui/Skeleton'
import { useTransformations } from '@/lib/public-api'
import { SectionHeader } from '../components/SectionHeader'
import { BeforeAfterSlider } from '../components/BeforeAfterSlider'
import { useBranchScope } from './context'
import { cn } from '@/lib/utils'
import type { TransformationSliderContent } from './schemas'

/**
 * Transformation gallery (Module 1.7, 03 §7). Every card names the real duration, the
 * program and the coach — the honesty is the differentiator, since the genre is famous
 * for eight-week miracles with no timeline attached.
 *
 * Consent is enforced server-side; nothing without a signed release reaches this endpoint.
 */
export function TransformationSliderSection({ content }: { content: TransformationSliderContent }) {
  const branchSlug = useBranchScope()
  const { data, isLoading, isError } = useTransformations(branchSlug)

  const items = content.showAll ? (data ?? []) : (data ?? []).slice(0, content.limit ?? 3)
  const isGrid = content.layout === 'grid' || content.showAll

  return (
    <section className="section-y bg-ink">
      <div className="shell">
        <SectionHeader
          eyebrow={content.eyebrow}
          headline={content.headline}
          body={content.body}
          cta={content.cta}
          align="split"
        />

        {isLoading && (
          <div className="mt-14 grid gap-6 sm:grid-cols-2 lg:grid-cols-3" aria-busy="true">
            {Array.from({ length: 3 }).map((_, index) => (
              <div key={index} className="space-y-4">
                <Skeleton className="aspect-[3/4] w-full" />
                <Skeleton rounded="pill" className="h-5 w-1/2" />
                <SkeletonText lines={2} />
              </div>
            ))}
          </div>
        )}

        {(isError || (!isLoading && items.length === 0)) && (
          <EmptyState
            className="mt-14"
            icon="trending-up"
            headline="Nothing published here yet"
            body="We only publish transformations with written consent, so this gallery fills up slowly and on purpose."
            actionLabel="Start your own"
            actionTo="/free-trial"
          />
        )}

        {items.length > 0 && (
          <div className={cn('mt-14 grid gap-x-6 gap-y-12', isGrid ? 'sm:grid-cols-2 lg:grid-cols-3' : 'lg:grid-cols-3')}>
            {items.map((item, index) => (
              <Reveal key={item.id} delay={Math.min(0.3, index * 0.08)}>
                <article>
                  <BeforeAfterSlider
                    beforeUrl={item.beforeImageUrl}
                    afterUrl={item.afterImageUrl}
                    alt={`${item.memberDisplayName}, ${item.durationWeeks} weeks`}
                    handleLabel={content.sliderHandleLabel}
                  />

                  <div className="mt-5">
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge tone="accent">{item.durationWeeks} weeks</Badge>
                      {item.branchName && <Badge>{item.branchName.replace('FORGE ', '')}</Badge>}
                    </div>

                    <h3 className="display-m mt-4 text-[1.25rem] text-bone">{item.memberDisplayName}</h3>
                    <p className="mt-2 text-[0.875rem] text-smoke">{item.program}</p>

                    {content.showWeights && item.weightBeforeKg != null && item.weightAfterKg != null && (
                      <p className="numeric mt-3 flex items-center gap-2 text-[0.9375rem] text-bone/85">
                        <span className="text-smoke">{item.weightBeforeKg} kg</span>
                        <span aria-hidden className="text-accent">→</span>
                        <span>{item.weightAfterKg} kg</span>
                      </p>
                    )}

                    {content.showStory && item.story && (
                      <p className="mt-4 text-[0.9375rem] leading-relaxed text-smoke">{item.story}</p>
                    )}

                    {item.trainerName && (
                      <p className="mt-4 flex items-center gap-2.5 text-[0.75rem] uppercase tracking-[0.08em] text-bone/55">
                        <span aria-hidden className="h-px w-6 bg-accent" />
                        Coached by {item.trainerName}
                      </p>
                    )}
                  </div>
                </article>
              </Reveal>
            ))}
          </div>
        )}
      </div>
    </section>
  )
}

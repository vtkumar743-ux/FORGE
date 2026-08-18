import { Reveal } from '@/components/ui/Reveal'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, Skeleton, SkeletonText } from '@/components/ui/Skeleton'
import { useTestimonials, type Testimonial } from '@/lib/public-api'
import { SectionHeader } from '../components/SectionHeader'
import { useBranchScope } from './context'
import { cn } from '@/lib/utils'
import type { TestimonialWallContent } from './schemas'

/**
 * Testimonials and the Google rating (Module 1.8). Quotes are set in display type at a
 * size that assumes someone will actually read one, rather than three equal cards of grey
 * body copy — the pattern that makes a review section look like every other gym's.
 */
export function TestimonialWallSection({ content }: { content: TestimonialWallContent }) {
  const branchSlug = useBranchScope()
  const { data, isLoading, isError } = useTestimonials({
    featuredOnly: content.featuredOnly,
    branchSlug,
    limit: content.limit,
  })

  const items = data ?? []
  const isMasonry = content.layout === 'masonry'

  return (
    <section className="section-y bg-carbon">
      <div className="shell">
        <SectionHeader eyebrow={content.eyebrow} headline={content.headline} align="split">
          {content.showGoogleRating && content.googleRating != null && (
            <Reveal delay={0.12}>
              <a
                href={content.googleReviewUrl ?? '#'}
                target="_blank"
                rel="noreferrer noopener"
                className="mt-7 inline-flex items-center gap-3 rounded-full border border-[var(--hairline-strong)] px-4 py-2.5 transition-colors duration-200 ease-out hover:border-accent"
              >
                <span className="flex" aria-hidden>
                  {Array.from({ length: 5 }).map((_, index) => (
                    <Icon
                      key={index}
                      name="star"
                      size={15}
                      className={index < Math.round(content.googleRating ?? 0) ? 'text-accent' : 'text-steel'}
                    />
                  ))}
                </span>
                <span className="numeric text-[0.9375rem] text-bone">{content.googleRating.toFixed(1)}</span>
                {content.googleReviewCount != null && (
                  <span className="text-[0.8125rem] text-smoke">
                    from {content.googleReviewCount.toLocaleString('en-IN')} Google reviews
                  </span>
                )}
              </a>
            </Reveal>
          )}
        </SectionHeader>

        {isLoading && (
          <div className="mt-14 grid gap-5 md:grid-cols-3" aria-busy="true">
            {Array.from({ length: 3 }).map((_, index) => (
              <div key={index} className="space-y-4 rounded-[var(--radius-card)] border border-[var(--hairline)] p-7">
                <SkeletonText lines={4} />
                <Skeleton rounded="pill" className="h-4 w-1/2" />
              </div>
            ))}
          </div>
        )}

        {(isError || (!isLoading && items.length === 0)) && (
          <EmptyState
            className="mt-14"
            icon="star"
            headline="No member quotes here yet"
            body="Ask at the desk — the members on the floor at 7 PM will give you an unfiltered answer."
            actionLabel="Book a tour"
            actionTo="/contact"
          />
        )}

        {items.length > 0 && (
          <div
            className={cn(
              'mt-14',
              isMasonry
                ? 'columns-1 gap-5 sm:columns-2 lg:columns-3 [&>*]:mb-5 [&>*]:break-inside-avoid'
                : 'grid gap-5 md:grid-cols-3',
            )}
          >
            {items.map((testimonial, index) => (
              <Reveal key={testimonial.id} delay={Math.min(0.3, index * 0.07)} className={cn(isMasonry && 'break-inside-avoid')}>
                <TestimonialCard testimonial={testimonial} />
              </Reveal>
            ))}
          </div>
        )}
      </div>
    </section>
  )
}

function TestimonialCard({ testimonial }: { testimonial: Testimonial }) {
  return (
    <figure className="flex h-full flex-col rounded-[var(--radius-card)] border border-[var(--hairline)] bg-ink p-7">
      <Icon name="sparkles" size={20} className="text-accent" />

      <blockquote className="mt-5 flex-1">
        <p className="text-[1.0625rem] leading-[1.6] text-bone/90">“{testimonial.quote}”</p>
      </blockquote>

      <figcaption className="mt-7 flex items-center gap-3.5 border-t border-[var(--hairline)] pt-5">
        {testimonial.authorPhotoUrl ? (
          <img
            src={testimonial.authorPhotoUrl}
            alt=""
            loading="lazy"
            decoding="async"
            className="graded size-11 shrink-0 rounded-full object-cover"
          />
        ) : (
          <span
            aria-hidden
            className="flex size-11 shrink-0 items-center justify-center rounded-full border border-[var(--hairline-strong)] text-[0.875rem] text-smoke"
          >
            {testimonial.authorName.charAt(0)}
          </span>
        )}
        <div className="min-w-0">
          <p className="truncate text-[0.9375rem] text-bone">{testimonial.authorName}</p>
          <p className="truncate text-[0.75rem] text-smoke">
            {[testimonial.authorRole, testimonial.branchName?.replace('FORGE ', '')].filter(Boolean).join(' · ')}
          </p>
        </div>
      </figcaption>

      {testimonial.program && (
        <p className="mt-4 text-[0.75rem] uppercase tracking-[0.08em] text-accent/80">{testimonial.program}</p>
      )}
    </figure>
  )
}

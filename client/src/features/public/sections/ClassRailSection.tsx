import { Reveal } from '@/components/ui/Reveal'
import { EmptyState, Skeleton, SkeletonText } from '@/components/ui/Skeleton'
import { useClassFormats } from '@/lib/public-api'
import { SectionHeader } from '../components/SectionHeader'
import { ClassFormatCard } from '../components/ClassCard'
import { useBranchScope } from './context'
import { cn } from '@/lib/utils'
import type { ClassRailContent } from './schemas'

/**
 * Class-format rail (Module 1.1 / 1.3). The CMS decides which formats appear and in what
 * order; everything shown on the card — the next real session, its coach, spots left —
 * comes from the live timetable, so a card never advertises a class that stopped running.
 *
 * `layout: rail` scrolls horizontally with snap points on narrow screens and settles into
 * a grid on wide ones. `layout: grid` is the full-library treatment on /classes.
 */
export function ClassRailSection({ content }: { content: ClassRailContent }) {
  const branchSlug = useBranchScope()
  const { data: formats, isLoading, isError } = useClassFormats(branchSlug)

  // The CMS's slug order is the editorial order; showAll falls back to the library order.
  const selected = (() => {
    if (!formats) return []
    if (content.showAll || content.formatSlugs.length === 0) return formats
    const bySlug = new Map(formats.map((format) => [format.slug, format]))
    return content.formatSlugs.map((slug) => bySlug.get(slug)).filter((format) => format !== undefined)
  })()

  const isRail = content.layout === 'rail'

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

        {isLoading && <RailSkeleton className="mt-14" />}

        {isError && (
          <EmptyState
            className="mt-14"
            icon="calendar"
            headline="The timetable is not loading"
            body="Refresh the page, or call the branch and we will read you today's classes."
            actionLabel="Contact us"
            actionTo="/contact"
          />
        )}

        {!isLoading && !isError && selected.length === 0 && (
          <EmptyState
            className="mt-14"
            icon="calendar"
            headline="No formats scheduled here yet"
            body="Every branch runs the core strength and conditioning classes. Check the full timetable for the nearest one."
            actionLabel="See the timetable"
            actionTo="/classes"
          />
        )}

        {selected.length > 0 && (
          <div
            className={cn(
              'mt-14',
              isRail
                ? 'flex snap-x snap-mandatory gap-5 overflow-x-auto pb-4 lg:grid lg:grid-cols-3 lg:overflow-visible lg:pb-0'
                : 'grid gap-5 sm:grid-cols-2 lg:grid-cols-3',
            )}
          >
            {selected.map((format, index) => (
              <Reveal
                key={format.slug}
                delay={Math.min(0.35, index * 0.07)}
                className={cn(isRail && 'w-[19rem] shrink-0 snap-start lg:w-auto')}
              >
                <ClassFormatCard
                  format={format}
                  showCapacityRing={content.showCapacityRing}
                  showSpotsLeft={content.showSpotsLeft}
                />
              </Reveal>
            ))}
          </div>
        )}
      </div>
    </section>
  )
}

function RailSkeleton({ className }: { className?: string }) {
  return (
    <div className={cn('grid gap-5 sm:grid-cols-2 lg:grid-cols-3', className)} aria-busy="true">
      {Array.from({ length: 3 }).map((_, index) => (
        <div key={index} className="overflow-hidden rounded-[var(--radius-card)] border border-[var(--hairline)]">
          <Skeleton rounded="none" className="aspect-[16/10] w-full" />
          <div className="space-y-4 p-6">
            <Skeleton rounded="pill" className="h-5 w-2/3" />
            <SkeletonText lines={2} />
            <Skeleton rounded="pill" className="h-9 w-36" />
          </div>
        </div>
      ))}
    </div>
  )
}

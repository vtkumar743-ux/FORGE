import { Reveal } from '@/components/ui/Reveal'
import { EmptyState, Skeleton, SkeletonText } from '@/components/ui/Skeleton'
import { useTrainers, type Trainer } from '@/lib/public-api'
import { SectionHeader } from '../components/SectionHeader'
import { TrainerCard } from '../components/TrainerCard'
import { useBranchScope } from './context'
import type { TrainerHighlightContent } from './schemas'

/**
 * Trainer cards (Module 1.4). The home page picks four by slug; /trainers shows the whole
 * roster grouped by branch, because "which coach is at my gym" is the question the page is
 * actually answering.
 */
export function TrainerHighlightSection({ content }: { content: TrainerHighlightContent }) {
  const branchScope = useBranchScope()
  const { data: trainers, isLoading, isError } = useTrainers(branchScope)

  const selected = (() => {
    if (!trainers) return []
    if (content.showAll || content.trainerSlugs.length === 0) return trainers
    const bySlug = new Map(trainers.map((trainer) => [trainer.slug, trainer]))
    return content.trainerSlugs.map((slug) => bySlug.get(slug)).filter((trainer) => trainer !== undefined)
  })()

  const groups = content.groupByBranch ? groupByBranch(selected) : [{ name: null, trainers: selected }]

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

        {isLoading && <TrainerSkeleton />}

        {isError && (
          <EmptyState
            className="mt-14"
            icon="users"
            headline="The coach list is not loading"
            body="Refresh, or WhatsApp the branch and we will tell you who is on the floor today."
            actionLabel="Contact us"
            actionTo="/contact"
          />
        )}

        {!isLoading && !isError && selected.length === 0 && (
          <EmptyState
            className="mt-14"
            icon="users"
            headline="No coaches listed for this branch yet"
            body="Every branch is staffed by full-time coaches. See the full roster and the branch each one works."
            actionLabel="Meet the coaches"
            actionTo="/trainers"
          />
        )}

        {groups.map((group) => (
          <div key={group.name ?? 'all'} className="mt-14">
            {group.name && (
              <Reveal>
                <h3 className="caption mb-8 flex items-center gap-4">
                  {group.name.replace('FORGE ', '')}
                  <span aria-hidden className="h-px flex-1 bg-[var(--hairline)]" />
                  <span className="numeric text-smoke/60">{group.trainers.length}</span>
                </h3>
              </Reveal>
            )}

            <div className="grid gap-x-5 gap-y-12 sm:grid-cols-2 lg:grid-cols-4">
              {group.trainers.map((trainer, index) => (
                <Reveal key={trainer.slug} delay={Math.min(0.35, index * 0.07)}>
                  <TrainerCard
                    trainer={trainer}
                    duotoneOnHover={content.duotoneOnHover}
                    showRating={content.showRatings}
                    showPtPrice={content.showPtPrice}
                  />
                </Reveal>
              ))}
            </div>
          </div>
        ))}
      </div>
    </section>
  )
}

/** Preserves the roster's display order within each branch, and branch order across them. */
function groupByBranch(trainers: Trainer[]): Array<{ name: string | null; trainers: Trainer[] }> {
  const groups = new Map<string, Trainer[]>()
  for (const trainer of trainers) {
    const existing = groups.get(trainer.branchName)
    if (existing) existing.push(trainer)
    else groups.set(trainer.branchName, [trainer])
  }
  return [...groups.entries()].map(([name, members]) => ({ name, trainers: members }))
}

function TrainerSkeleton() {
  return (
    <div className="mt-14 grid gap-x-5 gap-y-12 sm:grid-cols-2 lg:grid-cols-4" aria-busy="true">
      {Array.from({ length: 4 }).map((_, index) => (
        <div key={index} className="space-y-4">
          <Skeleton className="aspect-[3/4] w-full" />
          <Skeleton rounded="pill" className="h-5 w-2/3" />
          <SkeletonText lines={1} />
        </div>
      ))}
    </div>
  )
}

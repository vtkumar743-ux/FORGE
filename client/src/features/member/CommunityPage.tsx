import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, Skeleton } from '@/components/ui/Skeleton'
import { Reveal } from '@/components/ui/Reveal'
import { cn } from '@/lib/utils'
import { PortalHeading, Panel, PillToggle } from './components/ui'
import { useCommunityFeed, useLikePost, type FeedPostRow } from './lib/portal-api'

/**
 * The community feed (Module 4.5). Personal records and streak milestones post themselves
 * when a member has opted into leaderboard sharing; the gym's own announcements sit pinned
 * above them.
 *
 * There is no composer. A gym feed people can post into is a moderation job the owner did
 * not ask for — this one is a noticeboard of things that actually happened on the floor.
 */
export function CommunityPage() {
  const [scope, setScope] = useState<'branch' | 'network'>('branch')
  const { data, isLoading } = useCommunityFeed(scope)
  const like = useLikePost()

  return (
    <div className="space-y-7">
      <PortalHeading
        eyebrow="Community"
        title="The floor this week"
        lead="Records set, streaks kept, and anything the gym needs you to know."
        actions={
          <PillToggle
            options={[
              { value: 'branch', label: 'My branch' },
              { value: 'network', label: 'All branches' },
            ]}
            value={scope}
            onChange={(value) => setScope(value as 'branch' | 'network')}
            ariaLabel="Feed scope"
          />
        }
      />

      {data?.consentPrompt && (
        <Panel className="border-[var(--accent)]/30">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="min-w-0">
              <p className="text-[0.9375rem] text-bone">Your records are private</p>
              <p className="mt-1 text-body-s text-smoke">{data.consentPrompt}</p>
            </div>
            <Link
              to="/portal/profile"
              className="shrink-0 rounded-full border border-[var(--accent)] px-4 py-2 text-caption text-[var(--accent)] transition-colors hover:bg-[var(--accent)] hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--accent)]"
            >
              Open profile
            </Link>
          </div>
        </Panel>
      )}

      {isLoading && (
        <div className="space-y-4" aria-busy="true">
          {Array.from({ length: 4 }).map((_, index) => (
            <Skeleton key={index} className="h-28 w-full" />
          ))}
        </div>
      )}

      {!isLoading && (data?.posts.length ?? 0) === 0 && (
        <EmptyState
          icon="trophy"
          headline="Nothing on the board yet"
          body={
            scope === 'branch'
              ? 'No records from your branch this week. Log a set that beats your best and yours will be the first.'
              : 'No records across the network yet this week.'
          }
          actionLabel="Log a workout"
          actionTo="/portal/workouts"
        />
      )}

      <div className="space-y-4">
        {(data?.posts ?? []).map((post, index) => (
          <Reveal key={post.id} delay={Math.min(0.24, index * 0.05)}>
            <FeedCard post={post} onLike={() => like.mutate(post.id)} />
          </Reveal>
        ))}
      </div>
    </div>
  )
}

function FeedCard({ post, onLike }: { post: FeedPostRow; onLike: () => void }) {
  const tone = TONE[post.kind] ?? TONE.Announcement

  return (
    <article
      className={cn(
        'rounded-[var(--radius-card)] border bg-carbon p-5 transition-colors duration-200',
        post.isPinned ? 'border-[var(--accent)]/40' : 'border-[var(--hairline)]',
        post.isMine && 'ring-1 ring-[var(--accent)]/25',
      )}
    >
      <div className="flex items-start gap-4">
        <span
          className={cn('mt-0.5 grid size-10 shrink-0 place-items-center rounded-full', tone.chip)}
          aria-hidden="true"
        >
          <Icon name={tone.icon} className="size-[1.125rem]" />
        </span>

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1">
            {post.isPinned && (
              <span className="rounded-full bg-[var(--accent)] px-2 py-0.5 text-[0.6875rem] font-medium tracking-wide text-ink">
                Pinned
              </span>
            )}
            <p className="text-[0.9375rem] leading-snug text-bone">{post.title}</p>
          </div>

          {post.body && <p className="mt-1.5 text-body-s text-smoke">{post.body}</p>}

          {/* The numbers behind a record, spelled out rather than implied by the headline. */}
          {post.kind === 'PersonalRecord' && post.meta && <PrDetail meta={post.meta} />}

          <p className="mt-3 flex flex-wrap items-center gap-x-2 gap-y-1 text-caption text-smoke">
            <span>{post.ago}</span>
            {post.branchName && (
              <>
                <span aria-hidden="true">·</span>
                <span>{post.branchName}</span>
              </>
            )}
            {post.isMine && (
              <>
                <span aria-hidden="true">·</span>
                <span className="text-[var(--accent)]">You</span>
              </>
            )}
          </p>
        </div>

        <button
          type="button"
          onClick={onLike}
          className="flex shrink-0 items-center gap-1.5 rounded-full border border-[var(--hairline)] px-3 py-1.5 text-caption text-smoke transition-colors duration-200 hover:border-[var(--accent)] hover:text-[var(--accent)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--accent)]"
          aria-label={`Cheer this on. ${post.likeCount} so far.`}
        >
          <Icon name="flame" className="size-3.5" aria-hidden="true" />
          {post.likeCount}
        </button>
      </div>
    </article>
  )
}

function PrDetail({ meta }: { meta: Record<string, unknown> }) {
  const lift = typeof meta.lift === 'string' ? meta.lift : null
  const weight = typeof meta.weightKg === 'number' ? meta.weightKg : null
  const reps = typeof meta.reps === 'number' ? meta.reps : null
  const previous = typeof meta.previousBest === 'number' ? meta.previousBest : null
  const e1rm = typeof meta.e1rm === 'number' ? meta.e1rm : null
  const streak = typeof meta.streak === 'number' ? meta.streak : null

  if (streak) return null
  if (!lift && !weight) return null

  const gain = e1rm != null && previous != null ? e1rm - previous : null

  return (
    <dl className="mt-3 flex flex-wrap gap-x-6 gap-y-2">
      {weight != null && reps != null && (
        <div>
          <dt className="text-caption text-smoke">Set</dt>
          <dd className="text-[1.0625rem] text-bone tabular-nums">
            {weight} kg × {reps}
          </dd>
        </div>
      )}
      {e1rm != null && (
        <div>
          <dt className="text-caption text-smoke">Estimated 1RM</dt>
          <dd className="text-[1.0625rem] text-bone tabular-nums">{e1rm.toFixed(1)} kg</dd>
        </div>
      )}
      {gain != null && gain > 0 && (
        <div>
          <dt className="text-caption text-smoke">Beat their best by</dt>
          <dd className="text-[1.0625rem] text-[var(--accent)] tabular-nums">+{gain.toFixed(1)} kg</dd>
        </div>
      )}
    </dl>
  )
}

const TONE: Record<string, { icon: 'trophy' | 'flame' | 'sparkles' | 'medal'; chip: string }> = {
  PersonalRecord: { icon: 'trophy', chip: 'bg-[var(--accent)]/15 text-[var(--accent)]' },
  Milestone: { icon: 'flame', chip: 'bg-[var(--accent-hot)]/15 text-[var(--accent-hot)]' },
  Announcement: { icon: 'sparkles', chip: 'bg-bone/10 text-bone' },
  Transformation: { icon: 'medal', chip: 'bg-bone/10 text-bone' },
  ChallengeResult: { icon: 'medal', chip: 'bg-bone/10 text-bone' },
}

import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Reveal } from '@/components/ui/Reveal'
import { Badge, CardMedia } from '@/components/ui/Card'
import { EmptyState, Skeleton, SkeletonText } from '@/components/ui/Skeleton'
import { useJournal, type BlogSummary } from '@/lib/public-api'
import { SectionHeader } from '../components/SectionHeader'
import { cn, formatDate } from '@/lib/utils'
import type { BlogRailContent } from './schemas'

/**
 * Journal listing (Module 1.9). The featured-plus-grid layout gives the two lead posts a
 * 16:9 cover at double width and drops the rest into a three-up grid — asymmetry on
 * purpose (03 §9.3) rather than nine identical tiles.
 *
 * Tag filtering is client-side: the whole published set is a few dozen rows, so a round
 * trip per pill would be slower and no more correct.
 */
export function BlogRailSection({ content }: { content: BlogRailContent }) {
  const [activeTag, setActiveTag] = useState<string | null>(null)
  const { data, isLoading, isError } = useJournal()

  const posts = data ?? []
  const tags = [...new Set(posts.flatMap((post) => post.tags))].sort()
  const filtered = activeTag ? posts.filter((post) => post.tags.includes(activeTag)) : posts
  const visible = filtered.slice(0, content.pageSize)

  const featured = content.layout === 'featured-plus-grid' ? visible.slice(0, content.featuredCount) : []
  const rest = content.layout === 'featured-plus-grid' ? visible.slice(content.featuredCount) : visible

  return (
    <section className="section-y bg-ink">
      <div className="shell">
        <SectionHeader eyebrow={content.eyebrow} headline={content.headline} cta={content.cta} align="split" />

        {tags.length > 1 && (
          <Reveal className="mt-10">
            <div className="flex flex-wrap gap-2" role="group" aria-label="Filter by topic">
              <TagPill active={activeTag === null} onClick={() => setActiveTag(null)}>
                Everything
              </TagPill>
              {tags.map((tag) => (
                <TagPill key={tag} active={activeTag === tag} onClick={() => setActiveTag(tag)}>
                  {tag.replace(/-/g, ' ')}
                </TagPill>
              ))}
            </div>
          </Reveal>
        )}

        {isLoading && (
          <div className="mt-12 grid gap-8 sm:grid-cols-2 lg:grid-cols-3" aria-busy="true">
            {Array.from({ length: 3 }).map((_, index) => (
              <div key={index} className="space-y-4">
                <Skeleton className="aspect-[16/10] w-full" />
                <Skeleton rounded="pill" className="h-5 w-4/5" />
                <SkeletonText lines={2} />
              </div>
            ))}
          </div>
        )}

        {(isError || (!isLoading && visible.length === 0)) && (
          <EmptyState
            className="mt-12"
            icon="sparkles"
            headline={activeTag ? 'Nothing under that topic yet' : 'Nothing published yet'}
            body={activeTag ? 'Clear the filter to see everything the coaches have written.' : content.emptyState}
            actionLabel="Back to home"
            actionTo="/"
          />
        )}

        {featured.length > 0 && (
          <div className="mt-12 grid gap-8 lg:grid-cols-2">
            {featured.map((post, index) => (
              <Reveal key={post.id} delay={index * 0.08}>
                <PostCard post={post} content={content} size="large" />
              </Reveal>
            ))}
          </div>
        )}

        {rest.length > 0 && (
          <div className={cn('grid gap-8 sm:grid-cols-2 lg:grid-cols-3', featured.length > 0 ? 'mt-8' : 'mt-12')}>
            {rest.map((post, index) => (
              <Reveal key={post.id} delay={Math.min(0.3, index * 0.07)}>
                <PostCard post={post} content={content} size="small" />
              </Reveal>
            ))}
          </div>
        )}
      </div>
    </section>
  )
}

function PostCard({
  post,
  content,
  size,
}: {
  post: BlogSummary
  content: BlogRailContent
  size: 'large' | 'small'
}) {
  return (
    <article className="group h-full">
      <Link to={`/journal/${post.slug}`} className="flex h-full flex-col focus-visible:outline-offset-4">
        <CardMedia
          src={post.coverImageUrl}
          alt={post.title}
          ratio={size === 'large' ? '16/9' : '16/10'}
          className="rounded-[var(--radius-card)]"
        />

        <div className={cn('flex flex-1 flex-col', size === 'large' ? 'mt-6' : 'mt-5')}>
          {content.showTags && post.tags.length > 0 && (
            <div className="mb-3.5 flex flex-wrap gap-2">
              {post.tags.slice(0, 2).map((tag) => (
                <Badge key={tag}>{tag.replace(/-/g, ' ')}</Badge>
              ))}
            </div>
          )}

          <h3
            className={cn(
              'display-m leading-[1.08] text-bone',
              size === 'large' ? 'text-[1.625rem]' : 'text-[1.1875rem]',
            )}
          >
            <span className="underline-slide">{post.title}</span>
          </h3>

          <p className="mt-3 flex-1 text-[0.9375rem] leading-relaxed text-smoke">{post.excerpt}</p>

          <p className="mt-5 flex flex-wrap items-center gap-x-2 gap-y-1 text-[0.75rem] text-smoke/70">
            {content.showAuthor && <span className="text-bone/70">{post.authorName}</span>}
            {content.showAuthor && post.authorRole && <span>· {post.authorRole}</span>}
            {post.publishedAtUtc && (
              <>
                <span aria-hidden>·</span>
                <time dateTime={post.publishedAtUtc}>{formatDate(post.publishedAtUtc)}</time>
              </>
            )}
            {content.showReadTime && (
              <>
                <span aria-hidden>·</span>
                <span>{post.readMinutes} min read</span>
              </>
            )}
          </p>
        </div>
      </Link>
    </article>
  )
}

function TagPill({
  active,
  onClick,
  children,
}: {
  active: boolean
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={cn(
        'rounded-full border px-4 py-2 text-[0.8125rem] capitalize transition-colors duration-200 ease-out',
        active
          ? 'border-accent bg-accent text-ink'
          : 'border-[var(--hairline-strong)] text-smoke hover:border-accent hover:text-accent',
      )}
    >
      {children}
    </button>
  )
}

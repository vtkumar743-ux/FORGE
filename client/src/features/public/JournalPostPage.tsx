import { Link, useParams } from 'react-router-dom'
import { Reveal } from '@/components/ui/Reveal'
import { Badge, CardMedia } from '@/components/ui/Card'
import { ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, Skeleton, SkeletonText } from '@/components/ui/Skeleton'
import { Seo } from '@/components/Seo'
import { setting, useSiteSettings } from '@/lib/cms'
import { useJournalPost } from '@/lib/public-api'
import { formatDate } from '@/lib/utils'

/**
 * One journal article (Module 1.9). Body arrives as structured blocks rather than HTML, so
 * the typography is ours no matter who writes the post — and no editor can paste markup
 * that breaks out of the type scale.
 *
 * Article-level JSON-LD is built here from the post's own fields, which is what gets the
 * author and date into a search result rather than just the title.
 */
export function JournalPostPage() {
  const { slug = '' } = useParams<{ slug: string }>()
  const { data: settings } = useSiteSettings()
  const { data: post, isLoading, isError } = useJournalPost(slug)

  if (isLoading) return <PostSkeleton />

  if (isError || !post) {
    return (
      <div className="shell section-y">
        <EmptyState
          icon="sparkles"
          headline="That piece is not here"
          body="It may have been renamed or unpublished. Everything the coaches have written is in the journal."
          actionLabel="Back to the journal"
          actionTo="/journal"
        />
      </div>
    )
  }

  return (
    <>
      <Seo
        seo={{
          title: post.seoTitle,
          description: post.seoDescription,
          ogImageUrl: post.ogImageUrl ?? setting(settings, 'seo.defaultOgImage'),
          noIndex: false,
          structuredData: {
            '@context': 'https://schema.org',
            '@type': 'Article',
            headline: post.title,
            description: post.excerpt,
            image: post.coverImageUrl,
            datePublished: post.publishedAtUtc,
            author: { '@type': 'Person', name: post.authorName, jobTitle: post.authorRole },
            publisher: { '@type': 'Organization', name: setting(settings, 'seo.organisationName', 'FORGE') },
            keywords: post.tags.join(', '),
          },
        }}
        titleSuffix={setting(settings, 'seo.titleSuffix', ' · FORGE Bengaluru')}
      />

      <article>
        <header className="section-y bg-ink pb-0">
          <div className="shell">
            <Reveal>
              <Link
                to="/journal"
                className="caption inline-flex items-center gap-2 text-smoke transition-colors hover:text-accent"
              >
                <Icon name="chevron-left" size={14} />
                The journal
              </Link>
            </Reveal>

            <div className="measure mt-10">
              {post.tags.length > 0 && (
                <Reveal>
                  <div className="flex flex-wrap gap-2">
                    {post.tags.map((tag) => (
                      <Badge key={tag}>{tag.replace(/-/g, ' ')}</Badge>
                    ))}
                  </div>
                </Reveal>
              )}

              <Reveal delay={0.05}>
                <h1 className="display-l mt-6 text-bone">{post.title}</h1>
              </Reveal>

              <Reveal delay={0.1}>
                <p className="mt-6 text-body-l leading-relaxed text-smoke">{post.excerpt}</p>
              </Reveal>

              <Reveal delay={0.15}>
                <p className="mt-8 flex flex-wrap items-center gap-x-2.5 gap-y-1 border-t border-[var(--hairline)] pt-6 text-[0.8125rem] text-smoke">
                  <span className="text-bone">{post.authorName}</span>
                  {post.authorRole && <span>· {post.authorRole}</span>}
                  {post.publishedAtUtc && (
                    <>
                      <span aria-hidden>·</span>
                      <time dateTime={post.publishedAtUtc}>{formatDate(post.publishedAtUtc)}</time>
                    </>
                  )}
                  <span aria-hidden>·</span>
                  <span>{post.readMinutes} min read</span>
                </p>
              </Reveal>
            </div>
          </div>
        </header>

        {post.coverImageUrl && (
          <Reveal distance={32} className="mt-14">
            <div className="shell">
              <figure className="overflow-hidden rounded-[var(--radius-card)]">
                <img
                  src={post.coverImageUrl}
                  alt={post.title}
                  loading="eager"
                  fetchPriority="high"
                  decoding="async"
                  className="graded aspect-[21/9] w-full object-cover"
                />
              </figure>
            </div>
          </Reveal>
        )}

        <div className="shell section-y">
          <div className="measure space-y-7">
            {post.body.map((block, index) => (
              <Reveal key={index} delay={Math.min(0.25, index * 0.05)} amount={0.1}>
                {block.type === 'heading' ? (
                  <h2 className="display-m mt-12 text-[1.5rem] text-bone">{block.text}</h2>
                ) : block.type === 'quote' ? (
                  <blockquote className="border-l-2 border-accent pl-6">
                    <p className="text-[1.25rem] leading-relaxed text-bone/90">{block.text}</p>
                  </blockquote>
                ) : block.type === 'image' && block.url ? (
                  <figure className="my-10 overflow-hidden rounded-[var(--radius-card)]">
                    <img
                      src={block.url}
                      alt={block.alt ?? ''}
                      loading="lazy"
                      decoding="async"
                      className="graded w-full object-cover"
                    />
                  </figure>
                ) : (
                  <p className="text-[1.0625rem] leading-[1.75] text-smoke">{block.text}</p>
                )}
              </Reveal>
            ))}
          </div>

          <Reveal className="measure mt-16">
            <div className="rounded-[var(--radius-card)] border border-[var(--accent-line)] bg-carbon p-8">
              <p className="caption">Written on the floor</p>
              <p className="mt-4 text-[1.0625rem] leading-relaxed text-bone/85">
                {post.authorName} coaches at FORGE. Come and be coached by them for a day, free — no card details, no
                obligation.
              </p>
              <ButtonLink to="/free-trial" className="mt-7" magnetic>
                Book a free trial
              </ButtonLink>
            </div>
          </Reveal>
        </div>

        {post.related.length > 0 && (
          <section className="section-y bg-carbon">
            <div className="shell">
              <h2 className="display-l text-bone">Keep reading</h2>
              <div className="mt-12 grid gap-8 sm:grid-cols-2 lg:grid-cols-3">
                {post.related.map((related, index) => (
                  <Reveal key={related.id} delay={Math.min(0.25, index * 0.07)}>
                    <Link to={`/journal/${related.slug}`} className="group block focus-visible:outline-offset-4">
                      <CardMedia
                        src={related.coverImageUrl}
                        alt={related.title}
                        ratio="16/10"
                        className="rounded-[var(--radius-card)]"
                      />
                      <h3 className="display-m mt-5 text-[1.1875rem] text-bone">
                        <span className="underline-slide">{related.title}</span>
                      </h3>
                      <p className="mt-2.5 text-[0.875rem] text-smoke">
                        {related.authorName} · {related.readMinutes} min read
                      </p>
                    </Link>
                  </Reveal>
                ))}
              </div>
            </div>
          </section>
        )}
      </article>
    </>
  )
}

function PostSkeleton() {
  return (
    <div className="shell section-y" aria-busy="true">
      <div className="measure space-y-6">
        <Skeleton rounded="pill" className="h-3 w-28" />
        <Skeleton className="h-16 w-full" />
        <SkeletonText lines={3} />
      </div>
      <Skeleton className="mt-14 aspect-[21/9] w-full" />
      <div className="measure mt-14 space-y-6">
        <SkeletonText lines={6} />
        <SkeletonText lines={5} />
      </div>
    </div>
  )
}

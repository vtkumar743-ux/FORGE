import { Link, useParams } from 'react-router-dom'
import { Reveal } from '@/components/ui/Reveal'
import { Badge } from '@/components/ui/Card'
import { ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, Skeleton, SkeletonText } from '@/components/ui/Skeleton'
import { Seo } from '@/components/Seo'
import { setting, useSiteSettings } from '@/lib/cms'
import { useTimetable, useTrainer, useTrainers } from '@/lib/public-api'
import { TrainerCard } from './components/TrainerCard'
import { ClassSessionCard } from './components/ClassCard'
import { formatInr } from '@/lib/utils'

/**
 * A coach's profile (Module 1.4). Not a CMS page: the roster is operational data, so a
 * new hire appears here the moment they are added in the admin panel rather than needing
 * a page to be built for them.
 *
 * The classes list is the live timetable filtered to this coach — it doubles as proof
 * that the profile is current, since a coach who has stopped teaching shows an empty week.
 */
export function TrainerDetailPage() {
  const { slug = '' } = useParams<{ slug: string }>()
  const { data: settings } = useSiteSettings()
  const { data: trainer, isLoading, isError } = useTrainer(slug)
  const { data: roster } = useTrainers()
  const { data: timetable } = useTimetable({ trainerSlug: slug, days: 7 }, Boolean(trainer))

  if (isLoading) return <TrainerSkeleton />

  if (isError || !trainer) {
    return (
      <div className="shell section-y">
        <EmptyState
          icon="users"
          headline="No coach at that address"
          body="They may have moved on, or the link is out of date. The full roster is one click away."
          actionLabel="Meet the coaches"
          actionTo="/trainers"
        />
      </div>
    )
  }

  const sessions = timetable?.sessions ?? []
  const others = (roster ?? []).filter((entry) => entry.slug !== trainer.slug).slice(0, 4)

  return (
    <>
      <Seo
        seo={{
          title: `${trainer.fullName} — ${trainer.headline}`,
          description: trainer.bio.slice(0, 180),
          ogImageUrl: trainer.portraitUrl ?? setting(settings, 'seo.defaultOgImage'),
          noIndex: false,
          structuredData: {
            '@context': 'https://schema.org',
            '@type': 'Person',
            name: trainer.fullName,
            jobTitle: trainer.headline,
            image: trainer.portraitUrl,
            worksFor: { '@type': 'HealthClub', name: trainer.branchName },
            knowsAbout: trainer.specialties,
          },
        }}
        titleSuffix={setting(settings, 'seo.titleSuffix', ' · FORGE Bengaluru')}
      />

      <article>
        <section className="section-y bg-ink">
          <div className="shell">
            <Reveal>
              <Link
                to="/trainers"
                className="caption inline-flex items-center gap-2 text-smoke transition-colors hover:text-accent"
              >
                <Icon name="chevron-left" size={14} />
                All coaches
              </Link>
            </Reveal>

            <div className="mt-10 grid gap-12 lg:grid-cols-12 lg:gap-16">
              <Reveal distance={32} className="lg:col-span-5">
                <figure className="overflow-hidden rounded-[var(--radius-card)] bg-steel">
                  {trainer.portraitUrl ? (
                    <img
                      src={trainer.portraitUrl}
                      alt={`${trainer.fullName}, ${trainer.headline}`}
                      loading="eager"
                      fetchPriority="high"
                      decoding="async"
                      className="graded aspect-[3/4] w-full object-cover"
                    />
                  ) : (
                    <div className="flex aspect-[3/4] items-center justify-center">
                      <Icon name="users" size={40} className="text-bone/20" />
                    </div>
                  )}
                </figure>
              </Reveal>

              <div className="lg:col-span-7">
                <Reveal>
                  <p className="caption">{trainer.branchName.replace('FORGE ', '')}</p>
                </Reveal>

                <Reveal delay={0.05}>
                  <h1 className="display-l mt-5 text-bone">{trainer.fullName}</h1>
                </Reveal>

                <Reveal delay={0.1}>
                  <p className="mt-4 text-body-l text-accent">{trainer.headline}</p>
                </Reveal>

                <Reveal delay={0.15}>
                  <p className="measure mt-7 text-[1.0625rem] leading-relaxed text-smoke">{trainer.bio}</p>
                </Reveal>

                <Reveal delay={0.2}>
                  <dl className="mt-10 grid gap-x-8 gap-y-6 border-t border-[var(--hairline)] pt-8 sm:grid-cols-3">
                    <Stat label="Experience" value={`${trainer.yearsExperience} years`} />
                    <Stat label="Classes a week" value={String(trainer.weeklyClassCount)} />
                    {trainer.ratingCount > 0 && (
                      <Stat
                        label={`Rated by ${trainer.ratingCount} members`}
                        value={`${trainer.averageRating.toFixed(1)} / 5`}
                      />
                    )}
                  </dl>
                </Reveal>

                {trainer.specialties.length > 0 && (
                  <Reveal delay={0.24}>
                    <div className="mt-9">
                      <p className="caption mb-3.5 text-[0.625rem]">Specialties</p>
                      <div className="flex flex-wrap gap-2">
                        {trainer.specialties.map((specialty) => (
                          <Badge key={specialty} tone="accent">
                            {specialty}
                          </Badge>
                        ))}
                      </div>
                    </div>
                  </Reveal>
                )}

                {trainer.certifications.length > 0 && (
                  <Reveal delay={0.28}>
                    <div className="mt-7">
                      <p className="caption mb-3.5 text-[0.625rem]">Certifications</p>
                      <ul className="space-y-2">
                        {trainer.certifications.map((certification) => (
                          <li key={certification} className="flex items-center gap-2.5 text-[0.9375rem] text-bone/85">
                            <Icon name="medal" size={15} className="shrink-0 text-accent" />
                            {certification}
                          </li>
                        ))}
                      </ul>
                    </div>
                  </Reveal>
                )}

                <Reveal delay={0.32}>
                  <div className="mt-10 flex flex-wrap items-center gap-3">
                    {trainer.acceptsPtClients && (
                      <ButtonLink to={`/free-trial?intent=pt&branch=${trainer.branchSlug}`} size="lg" magnetic>
                        Book a PT session
                      </ButtonLink>
                    )}
                    <ButtonLink to="/free-trial" variant="outline" size="lg">
                      Book a free trial
                    </ButtonLink>
                    {trainer.instagramUrl && (
                      <ButtonLink
                        href={trainer.instagramUrl}
                        target="_blank"
                        variant="ghost"
                        size="lg"
                        icon="instagram"
                        ariaLabel={`${trainer.fullName} on Instagram`}
                      >
                        Instagram
                      </ButtonLink>
                    )}
                  </div>

                  {trainer.acceptsPtClients && trainer.ptSessionPrice > 0 && (
                    <p className="mt-4 text-[0.8125rem] text-smoke">
                      Personal training from{' '}
                      <span className="numeric text-bone">{formatInr(trainer.ptSessionPrice)}</span> a session · requires
                      an active membership
                    </p>
                  )}
                </Reveal>
              </div>
            </div>
          </div>
        </section>

        {trainer.demoVideoUrl && (
          <section className="bg-ink pb-8">
            <div className="shell">
              <Reveal>
                <video
                  src={trainer.demoVideoUrl}
                  controls
                  playsInline
                  preload="none"
                  poster={trainer.portraitUrl ?? undefined}
                  className="graded w-full rounded-[var(--radius-card)]"
                />
              </Reveal>
            </div>
          </section>
        )}

        <section className="section-y bg-carbon">
          <div className="shell">
            <h2 className="display-l text-bone">This week with {trainer.fullName.split(' ')[0]}</h2>

            {sessions.length > 0 ? (
              <div className="mt-10 space-y-3">
                {sessions.map((session, index) => (
                  <Reveal key={session.id} delay={Math.min(0.25, index * 0.04)} distance={16} amount={0.05}>
                    <ClassSessionCard session={session} />
                  </Reveal>
                ))}
              </div>
            ) : (
              <EmptyState
                className="mt-10"
                icon="calendar"
                headline="No group classes this week"
                body={`${trainer.fullName.split(' ')[0]} is on the floor and taking one-to-one sessions. Ask the desk for a slot.`}
                actionLabel="Enquire about PT"
                actionTo={`/free-trial?intent=pt&branch=${trainer.branchSlug}`}
              />
            )}
          </div>
        </section>

        {others.length > 0 && (
          <section className="section-y bg-ink">
            <div className="shell">
              <h2 className="display-l text-bone">The rest of the team</h2>
              <div className="mt-12 grid gap-x-5 gap-y-12 sm:grid-cols-2 lg:grid-cols-4">
                {others.map((other, index) => (
                  <Reveal key={other.slug} delay={Math.min(0.3, index * 0.07)}>
                    <TrainerCard trainer={other} />
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

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="caption text-[0.625rem]">{label}</dt>
      <dd className="numeric display-m mt-2 text-[1.5rem] text-accent">{value}</dd>
    </div>
  )
}

function TrainerSkeleton() {
  return (
    <div className="shell section-y grid gap-12 lg:grid-cols-12" aria-busy="true">
      <Skeleton className="aspect-[3/4] w-full lg:col-span-5" />
      <div className="space-y-6 lg:col-span-7">
        <Skeleton rounded="pill" className="h-3 w-32" />
        <Skeleton className="h-14 w-2/3" />
        <SkeletonText lines={5} />
        <Skeleton rounded="pill" className="h-14 w-52" />
      </div>
    </div>
  )
}

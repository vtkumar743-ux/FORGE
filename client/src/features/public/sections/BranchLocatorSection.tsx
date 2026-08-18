import { Link } from 'react-router-dom'
import { Reveal } from '@/components/ui/Reveal'
import { ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import {
  setting,
  settingFlag,
  useOccupancy,
  useSiteSettings,
  type BranchOccupancy,
  type BranchSummary,
} from '@/lib/cms'
import { SectionHeader } from '../components/SectionHeader'
import { OccupancyChip, OccupancyMeter } from '../components/OccupancyMeter'
import { cn, whatsappLink } from '@/lib/utils'
import type { BranchLocatorContent } from './schemas'

/**
 * Branch locator with the live occupancy meter (Module 1.2). Publishing how full each
 * floor is right now is the one feature on this site that no gym in the city runs, so it
 * gets the visual weight — a full gauge per branch, not a line of small print.
 *
 * The meter is hidden entirely when the owner turns `features.liveOccupancy` off in site
 * settings, rather than showing a dead dial.
 */
export function BranchLocatorSection({ content }: { content: BranchLocatorContent }) {
  const { data: settings, isLoading } = useSiteSettings()
  const liveEnabled = settingFlag(settings, 'features.liveOccupancy', true) && content.showLiveOccupancy
  const { data: occupancy } = useOccupancy(liveEnabled)

  const branches = [...(settings?.branches ?? [])].sort((a, b) =>
    content.sortBy === 'name' ? a.name.localeCompare(b.name) : a.displayOrder - b.displayOrder,
  )

  const occupancyBySlug = new Map((occupancy ?? []).map((entry) => [entry.branchSlug, entry]))
  const isList = content.layout === 'list'

  return (
    <section className="section-y bg-ink">
      <div className="shell">
        <SectionHeader eyebrow={content.eyebrow} headline={content.headline} body={content.body} align="split" />

        {isLoading && (
          <div className="mt-14 grid gap-5 lg:grid-cols-3" aria-busy="true">
            {Array.from({ length: 3 }).map((_, index) => (
              <Skeleton key={index} className="h-[26rem] w-full" />
            ))}
          </div>
        )}

        <div className={cn('mt-14 grid gap-5', isList ? 'gap-6' : 'lg:grid-cols-3')}>
          {branches.map((branch, index) => (
            <Reveal key={branch.slug} delay={Math.min(0.3, index * 0.08)}>
              <BranchPanel
                branch={branch}
                content={content}
                occupancy={occupancyBySlug.get(branch.slug)}
                liveEnabled={liveEnabled}
                whatsappPrefill={setting(settings, 'contact.whatsappPrefill')}
                horizontal={isList}
              />
            </Reveal>
          ))}
        </div>

        {content.nearestFirstPrompt && (
          <p className="mt-8 text-[0.8125rem] text-smoke/70">
            {/* Honest about what we do not do yet: no geolocation prompt until it is wired. */}
            All three sit within 14 km of each other. {content.nearestFirstPrompt.replace(/^Use my location to /, 'To ')} — open
            any branch page for its map and directions.
          </p>
        )}
      </div>
    </section>
  )
}

function BranchPanel({
  branch,
  content,
  occupancy,
  liveEnabled,
  whatsappPrefill,
  horizontal,
}: {
  branch: BranchSummary
  content: BranchLocatorContent
  occupancy: BranchOccupancy | undefined
  liveEnabled: boolean
  whatsappPrefill: string
  horizontal: boolean
}) {
  return (
    <article
      className={cn(
        'group flex overflow-hidden rounded-[var(--radius-card)] border border-[var(--hairline)] bg-carbon',
        'transition-colors duration-300 ease-out hover:border-[var(--accent-line)]',
        horizontal ? 'flex-col md:flex-row' : 'h-full flex-col',
      )}
    >
      <Link
        to={`/branches/${branch.slug}`}
        className={cn('relative block overflow-hidden bg-steel', horizontal ? 'md:w-2/5' : '')}
        aria-label={`${branch.name} branch page`}
      >
        {branch.heroImageUrl ? (
          <img
            src={branch.heroImageUrl}
            alt={`Inside ${branch.name}`}
            loading="lazy"
            decoding="async"
            className={cn(
              'graded w-full object-cover transition-transform duration-[520ms] ease-out',
              'group-hover:scale-[1.04] motion-reduce:group-hover:scale-100',
              horizontal ? 'h-56 md:h-full' : 'aspect-[16/10]',
            )}
          />
        ) : (
          <div className="flex aspect-[16/10] items-center justify-center">
            <Icon name="map-pin" size={30} className="text-bone/20" />
          </div>
        )}

        {liveEnabled && (
          <span className="absolute left-4 top-4">
            <OccupancyChip occupancy={occupancy} />
          </span>
        )}
      </Link>

      <div className={cn('flex flex-1 flex-col p-7', horizontal && 'md:w-3/5')}>
        <h3 className="display-m text-[1.375rem] text-bone">
          <Link to={`/branches/${branch.slug}`}>
            <span className="underline-slide">{branch.name.replace('FORGE ', '')}</span>
          </Link>
        </h3>

        {branch.shortPitch && <p className="mt-3 text-[0.9375rem] leading-relaxed text-smoke">{branch.shortPitch}</p>}

        <address className="mt-5 space-y-2 not-italic text-[0.875rem] text-smoke">
          <p className="flex items-start gap-2.5">
            <Icon name="map-pin" size={15} className="mt-0.5 shrink-0 text-accent" />
            <span>
              {branch.addressLine1}
              {branch.addressLine2 && <>, {branch.addressLine2}</>}, {branch.city} {branch.pincode}
            </span>
          </p>

          {content.showTimings && (
            <p className="flex items-start gap-2.5">
              <Icon name="clock" size={15} className="mt-0.5 shrink-0 text-accent" />
              <span className="numeric">
                Mon–Fri {branch.weekdayHours} · Sat–Sun {branch.weekendHours}
              </span>
            </p>
          )}

          {content.showPhone && (
            <p className="flex items-start gap-2.5">
              <Icon name="phone" size={15} className="mt-0.5 shrink-0 text-accent" />
              <a href={`tel:${branch.phone}`} className="hover:text-accent">
                {branch.phone}
              </a>
            </p>
          )}

          {content.showEmail && (
            <p className="flex items-start gap-2.5">
              <Icon name="mail" size={15} className="mt-0.5 shrink-0 text-accent" />
              <a href={`mailto:${branch.email}`} className="hover:text-accent">
                {branch.email}
              </a>
            </p>
          )}
        </address>

        {liveEnabled && (
          <div className="mt-6 border-t border-[var(--hairline)] pt-6">
            <OccupancyMeter occupancy={occupancy} size={104} />
          </div>
        )}

        <div className="mt-auto flex flex-wrap gap-3 pt-7">
          <ButtonLink to={`/free-trial?branch=${branch.slug}`} size="sm">
            Book a free trial
          </ButtonLink>
          {content.showWhatsApp && branch.whatsAppNumber && (
            <ButtonLink
              href={whatsappLink(branch.whatsAppNumber, whatsappPrefill)}
              target="_blank"
              variant="outline"
              size="sm"
            >
              WhatsApp
            </ButtonLink>
          )}
          {content.showMap && branch.googleMapsUrl && (
            <ButtonLink href={branch.googleMapsUrl} target="_blank" variant="ghost" size="sm" icon="arrow-up-right" iconAfter>
              Directions
            </ButtonLink>
          )}
        </div>
      </div>
    </article>
  )
}

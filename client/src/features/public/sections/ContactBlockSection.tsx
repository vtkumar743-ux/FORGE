import { Reveal } from '@/components/ui/Reveal'
import { Icon, type IconName } from '@/components/ui/Icon'
import { ButtonLink } from '@/components/ui/Button'
import { setting, settingFlag, useLiveOccupancyFeed, useSiteSettings } from '@/lib/cms'
import { SectionHeader } from '../components/SectionHeader'
import { OccupancyMeter } from '../components/OccupancyMeter'
import { TypicalHours } from '../components/TypicalHours'
import { useBranchScope } from './context'
import { whatsappLink } from '@/lib/utils'
import type { ContactBlockContent } from './schemas'

/**
 * Two jobs, one section type: the per-branch "getting here" panel (address, map, timings,
 * parking, live occupancy) and the generic enquiry-routing list on /contact. Which one
 * renders is decided by the content the CMS holds, not by a second section type the owner
 * would have to choose between.
 *
 * The map is an OpenStreetMap embed rather than Google's: no API key to leak, no consent
 * banner, and a "directions" link straight into Google Maps for the people who want it.
 */
export function ContactBlockSection({ content }: { content: ContactBlockContent }) {
  const branchSlug = useBranchScope()
  const { data: settings } = useSiteSettings()
  const liveEnabled = settingFlag(settings, 'features.liveOccupancy', true) && content.showLiveOccupancy
  const { occupancy } = useLiveOccupancyFeed(branchSlug ? [branchSlug] : [], liveEnabled && !!branchSlug)

  const branchOccupancy = occupancy.find((entry) => entry.branchSlug === branchSlug)
  const isRowList = Array.isArray(content.rows) && content.rows.length > 0

  return (
    <section className="section-y bg-carbon">
      <div className="shell">
        <SectionHeader headline={content.headline} body={content.body} />

        {isRowList ? (
          <div className="mt-12 grid gap-4 md:grid-cols-2">
            {content.rows!.map((row, index) => (
              <Reveal key={row.label} delay={Math.min(0.3, index * 0.07)}>
                <div className="flex h-full gap-4 rounded-[var(--radius-card)] border border-[var(--hairline)] bg-ink p-6">
                  <span className="mt-0.5 inline-flex size-9 shrink-0 items-center justify-center rounded-full border border-[var(--accent-line)] text-accent">
                    <Icon name={rowIcon(row.type)} size={16} />
                  </span>
                  <div className="min-w-0">
                    <p className="caption text-[0.625rem]">{row.label}</p>
                    <p className="mt-2 break-words text-[1.0625rem] text-bone">
                      <a href={rowHref(row.type, row.value)} className="hover:text-accent">
                        {row.type === 'whatsapp' ? formatPhone(row.value) : row.value}
                      </a>
                    </p>
                    {row.note && <p className="mt-2 text-[0.8125rem] leading-relaxed text-smoke">{row.note}</p>}
                  </div>
                </div>
              </Reveal>
            ))}
          </div>
        ) : (
          <div className="mt-12 grid gap-10 lg:grid-cols-12">
            <Reveal className="lg:col-span-5">
              <div className="space-y-8">
                {content.addressLines && content.addressLines.length > 0 && (
                  <Detail icon="map-pin" label="Address">
                    <address className="not-italic">
                      {content.addressLines.filter(Boolean).map((line) => (
                        <span key={line} className="block">
                          {line}
                        </span>
                      ))}
                    </address>
                  </Detail>
                )}

                {content.timings && content.timings.length > 0 && (
                  <Detail icon="clock" label="Opening hours">
                    <dl className="space-y-1.5">
                      {content.timings.map((timing) => (
                        <div key={timing.label} className="flex flex-wrap gap-x-3">
                          <dt className="text-smoke">{timing.label}</dt>
                          <dd className="numeric text-bone">{timing.value}</dd>
                        </div>
                      ))}
                    </dl>
                  </Detail>
                )}

                {content.parkingNote && (
                  <Detail icon="car" label="Parking">
                    <p className="leading-relaxed text-smoke">{content.parkingNote}</p>
                  </Detail>
                )}

                <div className="flex flex-wrap gap-3">
                  {content.phone && (
                    <ButtonLink href={`tel:${content.phone}`} variant="outline" size="sm" icon="phone">
                      {formatPhone(content.phone)}
                    </ButtonLink>
                  )}
                  {content.whatsapp && (
                    <ButtonLink
                      href={whatsappLink(content.whatsapp, setting(settings, 'contact.whatsappPrefill'))}
                      target="_blank"
                      size="sm"
                    >
                      WhatsApp the desk
                    </ButtonLink>
                  )}
                  {content.mapUrl && (
                    <ButtonLink href={content.mapUrl} target="_blank" variant="ghost" size="sm" icon="arrow-up-right" iconAfter>
                      Directions
                    </ButtonLink>
                  )}
                </div>

                {liveEnabled && branchOccupancy && (
                  <div className="border-t border-[var(--hairline)] pt-8">
                    <p className="caption mb-4">On the floor right now</p>
                    <OccupancyMeter occupancy={branchOccupancy} />
                    {/* The gauge says "now"; the chart says "when" — together they answer the
                        whole question someone has before they get in the car. */}
                    <TypicalHours branchSlug={branchSlug} className="mt-8" enabled={liveEnabled} />
                  </div>
                )}
              </div>
            </Reveal>

            {content.latitude != null && content.longitude != null && (
              <Reveal delay={0.1} className="lg:col-span-7">
                <div className="overflow-hidden rounded-[var(--radius-card)] border border-[var(--hairline)]">
                  <iframe
                    title={`Map of ${content.headline ?? 'the branch'}`}
                    src={osmEmbed(content.latitude, content.longitude)}
                    loading="lazy"
                    referrerPolicy="no-referrer-when-downgrade"
                    className="h-[24rem] w-full border-0 grayscale-[0.85] contrast-[1.1] invert-[0.92] hue-rotate-180"
                  />
                </div>
              </Reveal>
            )}
          </div>
        )}

        {content.responseNote && (
          <Reveal delay={0.15}>
            <p className="mt-10 flex items-center gap-2.5 text-[0.875rem] text-smoke">
              <Icon name="clock" size={15} className="text-accent" />
              {content.responseNote}
            </p>
          </Reveal>
        )}
      </div>
    </section>
  )
}

function Detail({ icon, label, children }: { icon: IconName; label: string; children: React.ReactNode }) {
  return (
    <div className="flex gap-4">
      <span className="mt-0.5 inline-flex size-9 shrink-0 items-center justify-center rounded-full border border-[var(--accent-line)] text-accent">
        <Icon name={icon} size={16} />
      </span>
      <div className="min-w-0">
        <p className="caption text-[0.625rem]">{label}</p>
        <div className="mt-2 text-[0.9375rem] text-bone/85">{children}</div>
      </div>
    </div>
  )
}

/**
 * A dark-mode map without a tile-server API key: invert the light OSM tiles and rotate the
 * hue back, which lands close enough to the palette that it stops being a white rectangle
 * punched through the page.
 */
function osmEmbed(lat: number, lon: number): string {
  const d = 0.006
  const bbox = [lon - d, lat - d / 2, lon + d, lat + d / 2].join(',')
  return `https://www.openstreetmap.org/export/embed.html?bbox=${bbox}&layer=mapnik&marker=${lat},${lon}`
}

function rowIcon(type: string): IconName {
  if (type === 'email') return 'mail'
  if (type === 'whatsapp') return 'share'
  if (type === 'phone') return 'phone'
  return 'arrow-right'
}

function rowHref(type: string, value: string): string {
  if (type === 'email') return `mailto:${value}`
  if (type === 'whatsapp') return whatsappLink(value)
  if (type === 'phone') return `tel:${value}`
  return '#'
}

/** "+919148120500" → "+91 91481 20500" — Indian mobiles read in 5-5 grouping. */
function formatPhone(raw: string): string {
  const digits = raw.replace(/\D/g, '')
  if (digits.length === 12 && digits.startsWith('91')) return `+91 ${digits.slice(2, 7)} ${digits.slice(7)}`
  if (digits.length === 10) return `${digits.slice(0, 5)} ${digits.slice(5)}`
  return raw
}

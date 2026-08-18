import { Reveal } from '@/components/ui/Reveal'
import { Icon, isIconName } from '@/components/ui/Icon'
import { SectionHeader } from '../components/SectionHeader'
import { cn } from '@/lib/utils'
import type { AmenityBentoContent } from './schemas'

/**
 * Bento grid (03 §5): mixed 1×1, 2×1, 1×2 and 2×2 tiles so the facility reads as a floor
 * plan rather than a row of equal cards. Photo tiles carry a scrim and set their copy on
 * the image; text-only tiles (hours, parking) invert to a raised surface, which is what
 * stops the grid looking like a contact sheet.
 *
 * Each tile reveals individually rather than through <RevealGroup>: the group wraps every
 * child in its own element, which would sit between the grid and the tile and swallow the
 * column spans.
 */

const SPANS: Record<string, string> = {
  '1x1': 'md:col-span-1 md:row-span-1',
  '2x1': 'md:col-span-2 md:row-span-1',
  '1x2': 'md:col-span-1 md:row-span-2',
  '2x2': 'md:col-span-2 md:row-span-2',
}

export function AmenityBentoSection({ content }: { content: AmenityBentoContent }) {
  return (
    <section className="section-y bg-ink">
      <div className="shell">
        <SectionHeader eyebrow={content.eyebrow} headline={content.headline} body={content.body} align="split" />

        <div className="mt-14 grid auto-rows-[15rem] grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-4">
          {content.tiles.map((tile, index) => {
            const hasImage = Boolean(tile.imageUrl)

            return (
              <Reveal
                key={tile.title}
                delay={Math.min(0.4, index * 0.06)}
                distance={24}
                className={cn(SPANS[tile.size], 'min-h-[15rem]')}
              >
                <article
                  className={cn(
                    'group relative isolate flex h-full flex-col justify-end overflow-hidden rounded-[var(--radius-card)]',
                    'border border-[var(--hairline)] transition-colors duration-300 ease-out hover:border-[var(--accent-line)]',
                    hasImage ? 'bg-steel' : 'bg-carbon',
                  )}
                >
                  {hasImage && (
                    <>
                      <img
                        src={tile.imageUrl}
                        alt={tile.imageAlt ?? ''}
                        loading="lazy"
                        decoding="async"
                        className="graded absolute inset-0 -z-10 h-full w-full object-cover transition-transform duration-[600ms] ease-out group-hover:scale-[1.05] motion-reduce:group-hover:scale-100"
                      />
                      <div aria-hidden className="absolute inset-0 -z-10 bg-gradient-to-t from-ink via-ink/55 to-ink/10" />
                    </>
                  )}

                  <div className="p-6">
                    {isIconName(tile.iconKey) && (
                      <span
                        className={cn(
                          'mb-4 inline-flex size-10 items-center justify-center rounded-full border text-accent',
                          hasImage
                            ? 'border-[var(--hairline-strong)] bg-ink/50 backdrop-blur-sm'
                            : 'border-[var(--accent-line)]',
                        )}
                      >
                        <Icon name={tile.iconKey} size={19} />
                      </span>
                    )}

                    <h3 className="display-m text-[1.125rem] leading-tight text-bone">{tile.title}</h3>
                    {tile.body && (
                      <p className="mt-2.5 max-w-[36ch] text-[0.875rem] leading-relaxed text-smoke">{tile.body}</p>
                    )}
                  </div>
                </article>
              </Reveal>
            )
          })}
        </div>
      </div>
    </section>
  )
}

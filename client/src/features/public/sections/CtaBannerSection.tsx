import { ButtonLink } from '@/components/ui/Button'
import { Reveal } from '@/components/ui/Reveal'
import { setting, useSiteSettings } from '@/lib/cms'
import { whatsappLink } from '@/lib/utils'
import type { CtaBannerContent } from './schemas'
import { Photo } from '@/components/ui/Photo'

/**
 * Closing conversion block. `href: "whatsapp"` is a CMS keyword resolved here from
 * site settings, so the owner can change the WhatsApp number in one place and every
 * banner across the site follows.
 */
export function CtaBannerSection({ content }: { content: CtaBannerContent }) {
  const { data: settings } = useSiteSettings()

  const resolve = (href: string) =>
    href === 'whatsapp'
      ? whatsappLink(setting(settings, 'contact.whatsapp'), setting(settings, 'contact.whatsappPrefill'))
      : href

  const isExternal = (href: string) => href === 'whatsapp' || href.startsWith('http') || href.startsWith('tel:')

  return (
    <section className="section-y bg-ink">
      <div className="shell">
        <Reveal>
          <div className="grain relative overflow-hidden rounded-[var(--radius-sheet)] border border-[var(--hairline)] bg-carbon">
            {content.imageUrl && (
              <Photo
                src={content.imageUrl}
                alt={content.imageAlt ?? ''}
                sizes="100vw"
                className="absolute inset-0 h-full w-full object-cover opacity-30"
              />
            )}
            <div
              aria-hidden
              className="absolute inset-0"
              style={{ background: 'linear-gradient(100deg, rgb(10 10 10 / 0.94) 20%, rgb(10 10 10 / 0.55) 100%)' }}
            />

            <div className="relative z-10 max-w-3xl px-6 py-14 sm:px-12 sm:py-20">
              <h2 className="display-l text-bone">{content.headline}</h2>

              {content.body && (
                <p className="measure mt-6 text-body-l leading-relaxed text-smoke">{content.body}</p>
              )}

              <div className="mt-9 flex flex-wrap items-center gap-3">
                {content.primaryCta &&
                  (isExternal(content.primaryCta.href) ? (
                    <ButtonLink href={resolve(content.primaryCta.href)} target="_blank" size="lg" magnetic>
                      {content.primaryCta.label}
                    </ButtonLink>
                  ) : (
                    <ButtonLink to={content.primaryCta.href} size="lg" magnetic>
                      {content.primaryCta.label}
                    </ButtonLink>
                  ))}

                {content.secondaryCta &&
                  (isExternal(content.secondaryCta.href) ? (
                    <ButtonLink
                      href={resolve(content.secondaryCta.href)}
                      target="_blank"
                      variant="outline"
                      size="lg"
                    >
                      {content.secondaryCta.label}
                    </ButtonLink>
                  ) : (
                    <ButtonLink to={content.secondaryCta.href} variant="outline" size="lg">
                      {content.secondaryCta.label}
                    </ButtonLink>
                  ))}
              </div>

              {content.microcopy && (
                <p className="mt-6 text-[0.8125rem] text-smoke/80">{content.microcopy}</p>
              )}
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  )
}

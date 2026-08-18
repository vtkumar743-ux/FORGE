import { useEffect, useState } from 'react'
import { Reveal } from '@/components/ui/Reveal'
import { ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { useOffer } from '@/lib/public-api'
import { useBranchScope } from './context'
import { formatDate } from '@/lib/utils'
import type { OfferBannerContent } from './schemas'

/**
 * Seasonal offer banner (Module 1.5). The copy is CMS-authored, but the code and the
 * expiry come from the coupon itself — so an offer that has run out of uses or passed its
 * end date takes its own banner down without anyone remembering to remove it.
 *
 * If the CMS advertises a code the coupon table does not have live, the banner does not
 * render. Publishing a promise the checkout will reject is worse than publishing nothing.
 */
export function OfferBannerSection({ content }: { content: OfferBannerContent }) {
  const branchSlug = useBranchScope()
  const { data: offer } = useOffer(branchSlug)

  if (!content.enabled || !offer) return null
  if (content.couponCode && content.couponCode.toUpperCase() !== offer.code.toUpperCase()) return null

  return (
    <section className="bg-ink pb-4 pt-2">
      <div className="shell">
        <Reveal distance={20}>
          <div className="grain relative isolate overflow-hidden rounded-[var(--radius-card)] border border-[var(--accent-line)] bg-carbon px-6 py-7 sm:px-9">
            <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
              <div className="min-w-0">
                <p className="caption flex items-center gap-2 text-accent">
                  <Icon name="sparkles" size={14} />
                  Limited offer
                </p>
                <h2 className="display-m mt-3 text-[clamp(1.375rem,2.4vw,1.875rem)] text-bone">{content.headline}</h2>
                {content.body && <p className="mt-3 max-w-2xl text-[0.9375rem] leading-relaxed text-smoke">{content.body}</p>}
              </div>

              <div className="flex shrink-0 flex-col items-start gap-4 sm:flex-row sm:items-center">
                <div className="text-left">
                  <p className="caption text-[0.625rem]">Code</p>
                  <p className="numeric mt-1 rounded-full border border-dashed border-accent px-4 py-2 text-[0.9375rem] tracking-[0.12em] text-accent">
                    {offer.code}
                  </p>
                </div>

                {content.urgencyStyle === 'countdown' ? (
                  <Countdown deadline={offer.validToUtc} />
                ) : content.urgencyStyle === 'date' ? (
                  <p className="text-[0.8125rem] text-smoke">Ends {formatDate(offer.validTo)}</p>
                ) : null}

                {content.cta && (
                  <ButtonLink to={content.cta.href} size="md" magnetic>
                    {content.cta.label}
                  </ButtonLink>
                )}
              </div>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  )
}

/** Ticks once a minute — a per-second countdown on a three-week offer is theatre. */
function Countdown({ deadline }: { deadline: string }) {
  const [remaining, setRemaining] = useState(() => new Date(deadline).getTime() - Date.now())

  useEffect(() => {
    const timer = window.setInterval(() => setRemaining(new Date(deadline).getTime() - Date.now()), 60_000)
    return () => window.clearInterval(timer)
  }, [deadline])

  if (remaining <= 0) return null

  const days = Math.floor(remaining / 86_400_000)
  const hours = Math.floor((remaining % 86_400_000) / 3_600_000)
  const minutes = Math.floor((remaining % 3_600_000) / 60_000)

  return (
    <div className="text-left">
      <p className="caption text-[0.625rem]">Ends in</p>
      <p className="numeric mt-1 flex items-baseline gap-1.5 text-[0.9375rem] text-bone">
        {days > 0 && (
          <>
            <span className="text-accent">{days}</span>d
          </>
        )}
        <span className="text-accent">{hours}</span>h<span className="text-accent">{minutes}</span>m
      </p>
    </div>
  )
}

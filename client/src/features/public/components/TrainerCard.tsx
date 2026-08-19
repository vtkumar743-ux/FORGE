import { Link } from 'react-router-dom'
import { Badge } from '@/components/ui/Card'
import { Icon } from '@/components/ui/Icon'
import { cn } from '@/lib/utils'
import type { Trainer } from '@/lib/public-api'
import { Photo } from '@/components/ui/Photo'

/**
 * Trainer card (03 §7): 3:4 portrait that goes duotone on hover, specialty badges,
 * years and certifications, and a route into the profile where "Book PT" lives.
 *
 * The duotone is a CSS filter chain rather than a second graded asset, so it costs
 * no extra bytes and stays consistent if the palette is re-tokenised in the CMS.
 */
export function TrainerCard({
  trainer,
  duotoneOnHover = true,
  showRating = false,
  showPtPrice = false,
}: {
  trainer: Trainer
  duotoneOnHover?: boolean
  showRating?: boolean
  showPtPrice?: boolean
}) {
  return (
    <article className="group">
      <Link to={`/trainers/${trainer.slug}`} className="block focus-visible:outline-offset-4">
        <div className="relative overflow-hidden rounded-[var(--radius-card)] bg-steel">
          {trainer.portraitUrl ? (
            <Photo
              src={trainer.portraitUrl}
              alt={`${trainer.fullName}, ${trainer.headline}`}
              sizes="(min-width: 1024px) 25vw, (min-width: 640px) 50vw, 100vw"
              className={cn(
                'aspect-[3/4] w-full object-cover',
                'transition-[transform,filter] duration-[520ms] ease-out',
                'group-hover:scale-[1.04] motion-reduce:group-hover:scale-100',
                duotoneOnHover && 'group-hover:[filter:grayscale(1)_contrast(1.25)_sepia(0.4)_hue-rotate(-12deg)_saturate(2.2)_brightness(0.9)]',
              )}
            />
          ) : (
            <div className="flex aspect-[3/4] w-full items-center justify-center">
              <Icon name="users" size={34} className="text-bone/20" />
            </div>
          )}

          <div aria-hidden className="pointer-events-none absolute inset-0 bg-gradient-to-t from-ink/85 via-ink/10 to-transparent" />

          <div className="absolute inset-x-0 bottom-0 p-5">
            <h3 className="display-m text-[1.25rem] text-bone">
              <span className="underline-slide">{trainer.fullName}</span>
            </h3>
            <p className="mt-1.5 text-[0.8125rem] leading-snug text-bone/75">{trainer.headline}</p>
          </div>

          {showRating && trainer.ratingCount > 0 && (
            <span className="absolute right-4 top-4 inline-flex items-center gap-1.5 rounded-full border border-[var(--hairline-strong)] bg-ink/60 px-2.5 py-1 text-[0.75rem] text-bone backdrop-blur-sm">
              <Icon name="star" size={13} className="text-accent" />
              <span className="numeric">{trainer.averageRating.toFixed(1)}</span>
            </span>
          )}
        </div>
      </Link>

      <div className="mt-4 flex flex-wrap items-center gap-2">
        {trainer.specialties.slice(0, 3).map((specialty) => (
          <Badge key={specialty}>{specialty}</Badge>
        ))}
      </div>

      <p className="mt-3 text-[0.8125rem] text-smoke">
        {trainer.yearsExperience} yrs
        {trainer.certifications.length > 0 && <> · {trainer.certifications.slice(0, 2).join(' · ')}</>}
        {' · '}
        {trainer.branchName.replace('FORGE ', '')}
      </p>

      {showPtPrice && trainer.acceptsPtClients && (
        <p className="mt-2 text-[0.8125rem] text-bone/70">
          <span className="numeric text-accent">₹{trainer.ptSessionPrice.toLocaleString('en-IN')}</span> per PT session
        </p>
      )}
    </article>
  )
}

import type { ReactNode } from 'react'
import { KineticHeading, Reveal } from '@/components/ui/Reveal'
import { ButtonLink } from '@/components/ui/Button'
import { cn } from '@/lib/utils'
import type { Cta } from '@/lib/cms'

/**
 * The eyebrow / headline / lead-paragraph stack every section opens with. One component
 * so the vertical rhythm and the kinetic-weight moment are identical down the page —
 * mismatched section headings are the fastest way a site starts reading as assembled.
 */
export function SectionHeader({
  eyebrow,
  headline,
  body,
  cta,
  align = 'left',
  kinetic = false,
  className,
  children,
}: {
  eyebrow?: string
  headline?: string
  body?: string
  cta?: Cta | null
  align?: 'left' | 'split'
  /** Reserved for the two section breaks allowed the kinetic treatment (03 §3). */
  kinetic?: boolean
  className?: string
  children?: ReactNode
}) {
  if (!eyebrow && !headline && !body && !cta && !children) return null

  return (
    <div
      className={cn(
        align === 'split' && 'flex flex-col gap-8 lg:flex-row lg:items-end lg:justify-between',
        className,
      )}
    >
      <div className={cn(align === 'split' && 'lg:max-w-2xl')}>
        {eyebrow && (
          <Reveal>
            <p className="caption">{eyebrow}</p>
          </Reveal>
        )}

        {headline &&
          (kinetic ? (
            <KineticHeading as="h2" className="display-l mt-5 text-bone">
              {headline}
            </KineticHeading>
          ) : (
            <Reveal delay={0.05}>
              <h2 className="display-l mt-5 text-bone">{headline}</h2>
            </Reveal>
          ))}

        {body && (
          <Reveal delay={0.1}>
            <p className="measure mt-6 text-body-l leading-relaxed text-smoke">{body}</p>
          </Reveal>
        )}

        {children}
      </div>

      {cta && (
        <Reveal delay={0.14} className={cn(align === 'split' ? 'shrink-0' : 'mt-8')}>
          <ButtonLink to={cta.href} variant="outline" icon="arrow-right" iconAfter>
            {cta.label}
          </ButtonLink>
        </Reveal>
      )}
    </div>
  )
}

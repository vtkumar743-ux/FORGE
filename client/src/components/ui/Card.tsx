import { forwardRef, type HTMLAttributes, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { cn } from '@/lib/utils'
import { Icon, type IconName } from './Icon'

/* ============================================================================
   Card

   One radius token (--radius-card, 16px) for every card in the product. Hover
   choreography per 03 §6: scale 1.03, secondary-image swap, gold underline slide
   on the title. Depth comes from a hairline and a raised surface, not shadows —
   drop shadows on near-black read as grey smudges.
   ============================================================================ */

type Surface = 'carbon' | 'transparent' | 'outline'

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  surface?: Surface
  /** Adds the lift + border-warm hover treatment. Off for static content tiles. */
  interactive?: boolean
  /** Wraps the card in a router link, keeping the whole tile clickable. */
  to?: string
  href?: string
  padded?: boolean
  children: ReactNode
}

const surfaces: Record<Surface, string> = {
  carbon: 'bg-carbon border border-[var(--hairline)]',
  transparent: 'bg-transparent',
  outline: 'bg-transparent border border-[var(--hairline)]',
}

export const Card = forwardRef<HTMLDivElement, CardProps>(function Card(
  { surface = 'carbon', interactive = false, to, href, padded = true, className, children, ...rest },
  ref,
) {
  const classes = cn(
    'group relative overflow-hidden rounded-[var(--radius-card)]',
    surfaces[surface],
    padded && 'p-6',
    interactive &&
      'transition-[transform,border-color,background-color] duration-[260ms] ease-out ' +
        'hover:-translate-y-0.5 hover:scale-[1.008] hover:border-[var(--accent-line)] ' +
        'motion-reduce:hover:translate-y-0 motion-reduce:hover:scale-100',
    className,
  )

  const content = (
    <div ref={ref} className={classes} {...rest}>
      {children}
    </div>
  )

  if (to) return <Link to={to} className="block focus-visible:outline-offset-4">{content}</Link>
  if (href) {
    return (
      <a href={href} target="_blank" rel="noreferrer noopener" className="block focus-visible:outline-offset-4">
        {content}
      </a>
    )
  }
  return content
})

/* ---------------------------------------------------------------- media */

export interface CardMediaProps {
  src?: string | null
  alt: string
  /** Swapped in on hover — the Gymshark secondary-image technique (03 §6). */
  hoverSrc?: string | null
  /** CSS aspect ratio, e.g. "3/4" for trainer portraits, "16/9" for facility shots. */
  ratio?: string
  className?: string
  /** Adds the dark gradient so overlaid text always clears AA contrast. */
  scrim?: boolean
  children?: ReactNode
  /** Base64 placeholder from MediaAssets so nothing ever flashes as a grey box. */
  blurDataUrl?: string | null
  eager?: boolean
}

export function CardMedia({
  src,
  alt,
  hoverSrc,
  ratio = '4/3',
  className,
  scrim = false,
  children,
  blurDataUrl,
  eager = false,
}: CardMediaProps) {
  return (
    <div
      className={cn('relative overflow-hidden bg-steel', scrim && 'scrim', className)}
      style={{
        aspectRatio: ratio,
        backgroundImage: blurDataUrl ? `url(${blurDataUrl})` : undefined,
        backgroundSize: 'cover',
        backgroundPosition: 'center',
      }}
    >
      {src ? (
        <>
          <img
            src={src}
            alt={alt}
            loading={eager ? 'eager' : 'lazy'}
            decoding="async"
            fetchPriority={eager ? 'high' : 'auto'}
            className={cn(
              'graded h-full w-full object-cover transition-[transform,opacity] duration-[500ms] ease-out',
              'group-hover:scale-[1.04] motion-reduce:group-hover:scale-100',
              hoverSrc && 'group-hover:opacity-0',
            )}
          />
          {hoverSrc && (
            <img
              src={hoverSrc}
              alt=""
              aria-hidden
              loading="lazy"
              decoding="async"
              className="graded absolute inset-0 h-full w-full scale-[1.04] object-cover opacity-0 transition-opacity duration-[500ms] ease-out group-hover:opacity-100"
            />
          )}
        </>
      ) : (
        // Never a grey box: an on-brand mark holds the frame if a CMS image is missing.
        <div className="flex h-full w-full items-center justify-center bg-steel">
          <Icon name="barbell" size={32} className="text-bone/20" />
        </div>
      )}
      {children}
    </div>
  )
}

/* ---------------------------------------------------------------- parts */

export function CardTitle({
  children,
  className,
  as: Tag = 'h3',
  underline = true,
}: {
  children: ReactNode
  className?: string
  as?: 'h2' | 'h3' | 'h4'
  underline?: boolean
}) {
  return (
    <Tag className={cn('display-m text-[1.375rem] leading-[1.05] text-bone', className)}>
      <span className={cn(underline && 'underline-slide')}>{children}</span>
    </Tag>
  )
}

export function CardBody({ children, className }: { children: ReactNode; className?: string }) {
  return <p className={cn('text-[0.9375rem] leading-relaxed text-smoke', className)}>{children}</p>
}

/** Uppercase micro-label: level, duration, branch, category. */
export function Badge({
  children,
  tone = 'neutral',
  icon,
  className,
}: {
  children: ReactNode
  tone?: 'neutral' | 'accent' | 'hot' | 'success'
  icon?: IconName
  className?: string
}) {
  const tones = {
    neutral: 'border-[var(--hairline-strong)] text-smoke',
    accent: 'border-[var(--accent-line)] text-accent',
    hot: 'border-accent-hot/50 text-accent-hot',
    success: 'border-success/40 text-success',
  } as const

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1',
        'text-[0.6875rem] font-medium uppercase tracking-[0.08em]',
        tones[tone],
        className,
      )}
    >
      {icon && <Icon name={icon} size={12} strokeWidth={2} />}
      {children}
    </span>
  )
}

/**
 * Capacity ring (03 §7). An SVG arc showing how full a class is — the visual the
 * class cards and the occupancy meter both build on.
 */
export function CapacityRing({
  filled,
  total,
  size = 44,
  label,
}: {
  filled: number
  total: number
  size?: number
  label?: string
}) {
  const safeTotal = Math.max(1, total)
  const ratio = Math.min(1, filled / safeTotal)
  const radius = (size - 5) / 2
  const circumference = 2 * Math.PI * radius
  const spotsLeft = Math.max(0, total - filled)

  // Gold while there is room, red once it is nearly full — urgency, not decoration.
  const stroke = ratio >= 0.9 ? 'var(--accent-hot)' : ratio >= 0.7 ? 'var(--accent)' : 'var(--success)'

  return (
    <div
      className="relative inline-flex items-center justify-center"
      style={{ width: size, height: size }}
      role="img"
      aria-label={label ?? `${spotsLeft} of ${total} spots left`}
    >
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} className="-rotate-90">
        <circle cx={size / 2} cy={size / 2} r={radius} fill="none" stroke="var(--steel)" strokeWidth={3} />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke={stroke}
          strokeWidth={3}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={circumference * (1 - ratio)}
          className="transition-[stroke-dashoffset] duration-[400ms] ease-out"
        />
      </svg>
      <span className="numeric absolute text-[0.6875rem] font-semibold text-bone">{spotsLeft}</span>
    </div>
  )
}

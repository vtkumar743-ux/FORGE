import { useRef, useState, type ButtonHTMLAttributes, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { motion, useReducedMotion } from 'motion/react'
import { cn } from '@/lib/utils'
import { Icon, type IconName } from './Icon'

/* ============================================================================
   Buttons

   Triple-intent CTA system (03 §7, David Lloyd pattern):
     primary   — gold fill, one per view. The accent is only ever an action.
     outline   — hairline border, the secondary intent ("Book a tour").
     ghost     — text-and-underline, the tertiary intent ("View plans").
   One radius token (pill) for every interactive element, always.
   ============================================================================ */

type Variant = 'primary' | 'outline' | 'ghost' | 'danger'
type Size = 'sm' | 'md' | 'lg'

const base =
  'group relative inline-flex items-center justify-center gap-2 rounded-full font-medium ' +
  'transition-[background-color,color,border-color,box-shadow,transform] duration-200 ease-out ' +
  'select-none whitespace-nowrap disabled:pointer-events-none disabled:opacity-45'

const variants: Record<Variant, string> = {
  primary:
    'bg-accent text-ink hover:brightness-[1.08] active:brightness-95 ' +
    'shadow-[0_10px_30px_-14px_color-mix(in_srgb,var(--accent)_60%,transparent)]',
  outline:
    'border border-[var(--hairline-strong)] text-bone hover:border-accent hover:text-accent bg-transparent',
  ghost:
    'text-bone/85 hover:text-accent bg-transparent px-0 underline-offset-[6px] hover:underline decoration-accent/60',
  danger: 'bg-accent-hot text-bone hover:brightness-110',
}

const sizes: Record<Size, string> = {
  sm: 'h-9 px-4 text-[0.8125rem] tracking-[0.02em]',
  md: 'h-11 px-6 text-[0.9375rem]',
  lg: 'h-14 px-8 text-base',
}

interface CommonProps {
  variant?: Variant
  size?: Size
  icon?: IconName
  /** Renders the icon after the label instead of before it. */
  iconAfter?: boolean
  loading?: boolean
  fullWidth?: boolean
  /**
   * Magnetic hover (03 §6): the button drifts ≤6px toward the cursor. Reserved
   * for primary conversion CTAs — everywhere would be noise.
   */
  magnetic?: boolean
  className?: string
  children?: ReactNode
}

/**
 * The drag/animation handler names collide between React's DOM events and Framer
 * Motion's gesture callbacks, so they are omitted from the DOM half of the props.
 */
type ButtonDomProps = Omit<
  ButtonHTMLAttributes<HTMLButtonElement>,
  | 'className'
  | 'children'
  | 'style'
  | 'onDrag'
  | 'onDragStart'
  | 'onDragEnd'
  | 'onDragEnter'
  | 'onDragLeave'
  | 'onDragOver'
  | 'onDrop'
  | 'onAnimationStart'
  | 'onAnimationEnd'
  | 'onAnimationIteration'
  | 'onTransitionEnd'
>

export interface ButtonProps extends CommonProps, ButtonDomProps {}

export function Button({
  variant = 'primary',
  size = 'md',
  icon,
  iconAfter = false,
  loading = false,
  fullWidth = false,
  magnetic = false,
  className,
  children,
  disabled,
  ...rest
}: ButtonProps) {
  const ref = useRef<HTMLButtonElement>(null)
  const [offset, setOffset] = useState({ x: 0, y: 0 })
  const reduced = useReducedMotion()
  const isMagnetic = magnetic && !reduced

  function handleMove(event: React.MouseEvent<HTMLButtonElement>) {
    if (!isMagnetic || !ref.current) return
    const rect = ref.current.getBoundingClientRect()
    const dx = event.clientX - (rect.left + rect.width / 2)
    const dy = event.clientY - (rect.top + rect.height / 2)
    // Clamp to 6px so it reads as weight, not as a bug.
    setOffset({
      x: Math.max(-6, Math.min(6, dx * 0.25)),
      y: Math.max(-6, Math.min(6, dy * 0.35)),
    })
  }

  return (
    <motion.button
      ref={ref}
      type={rest.type ?? 'button'}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={cn(base, variants[variant], sizes[size], fullWidth && 'w-full', className)}
      onMouseMove={handleMove}
      onMouseLeave={() => setOffset({ x: 0, y: 0 })}
      animate={isMagnetic ? { x: offset.x, y: offset.y } : undefined}
      transition={{ type: 'spring', stiffness: 320, damping: 22, mass: 0.4 }}
      {...rest}
    >
      <ButtonInner icon={icon} iconAfter={iconAfter} loading={loading} size={size}>
        {children}
      </ButtonInner>
    </motion.button>
  )
}

export interface ButtonLinkProps extends CommonProps {
  to?: string
  href?: string
  target?: string
  rel?: string
  ariaLabel?: string
  onClick?: () => void
}

/** Same visual system for navigation. Internal routes use <Link>, external use <a>. */
export function ButtonLink({
  variant = 'primary',
  size = 'md',
  icon,
  iconAfter = false,
  loading = false,
  fullWidth = false,
  className,
  children,
  to,
  href,
  target,
  rel,
  ariaLabel,
  onClick,
}: ButtonLinkProps) {
  const classes = cn(base, variants[variant], sizes[size], fullWidth && 'w-full', className)
  const inner = (
    <ButtonInner icon={icon} iconAfter={iconAfter} loading={loading} size={size}>
      {children}
    </ButtonInner>
  )

  if (to) {
    return (
      <Link to={to} className={classes} aria-label={ariaLabel} onClick={onClick}>
        {inner}
      </Link>
    )
  }

  return (
    <a
      href={href}
      target={target}
      rel={rel ?? (target === '_blank' ? 'noreferrer noopener' : undefined)}
      className={classes}
      aria-label={ariaLabel}
      onClick={onClick}
    >
      {inner}
    </a>
  )
}

function ButtonInner({
  icon,
  iconAfter,
  loading,
  size,
  children,
}: {
  icon?: IconName
  iconAfter?: boolean
  loading?: boolean
  size: Size
  children?: ReactNode
}) {
  const iconSize = size === 'sm' ? 16 : size === 'lg' ? 20 : 18

  if (loading) {
    return (
      <>
        <Icon name="loader" size={iconSize} className="animate-spin" />
        <span>{children}</span>
      </>
    )
  }

  return (
    <>
      {icon && !iconAfter && <Icon name={icon} size={iconSize} />}
      <span>{children}</span>
      {icon && iconAfter && (
        <Icon
          name={icon}
          size={iconSize}
          className="transition-transform duration-200 ease-out group-hover:translate-x-0.5"
        />
      )}
    </>
  )
}

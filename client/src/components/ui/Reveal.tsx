import { motion, useReducedMotion, type HTMLMotionProps } from 'motion/react'
import { Children, type ElementType, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

/* ============================================================================
   Scroll reveals (03 §6): 24–40px translate-up + fade, 60–100ms stagger across
   card groups. One component, used everywhere, so the whole site shares a single
   motion signature — and short-circuits entirely under prefers-reduced-motion.
   ============================================================================ */

export interface RevealProps extends Omit<HTMLMotionProps<'div'>, 'children'> {
  children: ReactNode
  /** Travel distance in px. Spec range is 24–40. */
  distance?: number
  delay?: number
  duration?: number
  as?: ElementType
  /** Fraction of the element that must be visible before it plays. */
  amount?: number
  className?: string
}

export function Reveal({
  children,
  distance = 28,
  delay = 0,
  duration = 0.55,
  as = 'div',
  amount = 0.25,
  className,
  ...rest
}: RevealProps) {
  const reduced = useReducedMotion()
  const Component = motion[as as 'div'] ?? motion.div

  // Reduced motion: render in final position, no transform, no fade-in delay.
  if (reduced) {
    return (
      <Component className={className} {...rest}>
        {children}
      </Component>
    )
  }

  return (
    <Component
      className={className}
      initial={{ opacity: 0, y: distance }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, amount }}
      transition={{ duration, delay, ease: [0.16, 1, 0.3, 1] }}
      {...rest}
    >
      {children}
    </Component>
  )
}

export interface RevealGroupProps {
  children: ReactNode
  /** Seconds between each child. Spec range is 0.06–0.1. */
  stagger?: number
  distance?: number
  delay?: number
  className?: string
  as?: ElementType
  amount?: number
}

/**
 * Staggers direct children. Each child is wrapped so callers can pass plain
 * markup — cards, list items, stat tiles — without touching motion themselves.
 */
export function RevealGroup({
  children,
  stagger = 0.08,
  distance = 28,
  delay = 0,
  className,
  as = 'div',
  amount = 0.15,
}: RevealGroupProps) {
  const reduced = useReducedMotion()
  const Component = (motion[as as 'div'] ?? motion.div) as typeof motion.div

  if (reduced) {
    const Plain = (as ?? 'div') as ElementType
    return <Plain className={className}>{children}</Plain>
  }

  return (
    <Component
      className={className}
      initial="hidden"
      whileInView="shown"
      viewport={{ once: true, amount }}
      variants={{
        hidden: {},
        shown: { transition: { staggerChildren: stagger, delayChildren: delay } },
      }}
    >
      {Children.map(children, (child, index) => (
        <motion.div
          key={index}
          variants={{
            hidden: { opacity: 0, y: distance },
            shown: { opacity: 1, y: 0, transition: { duration: 0.55, ease: [0.16, 1, 0.3, 1] } },
          }}
        >
          {child}
        </motion.div>
      ))}
    </Component>
  )
}

/**
 * Kinetic headline (03 §3 signature move): the variable-font weight animates as
 * the line enters the viewport. Clash Display's axis runs 200–700, so the range
 * is 400→700 rather than the 400→800 the spec names.
 */
export interface KineticHeadingProps {
  children: string
  className?: string
  as?: 'h1' | 'h2' | 'h3'
  from?: number
  to?: number
}

export function KineticHeading({
  children,
  className,
  as: Tag = 'h2',
  from = 400,
  to = 700,
}: KineticHeadingProps) {
  const reduced = useReducedMotion()
  const MotionTag = motion[Tag]

  if (reduced) {
    return <Tag className={cn(className)} style={{ fontWeight: to }}>{children}</Tag>
  }

  return (
    <MotionTag
      className={className}
      initial={{ fontWeight: from, opacity: 0, y: 18 }}
      whileInView={{ fontWeight: to, opacity: 1, y: 0 }}
      viewport={{ once: true, amount: 0.5 }}
      transition={{ duration: 0.9, ease: [0.16, 1, 0.3, 1] }}
    >
      {children}
    </MotionTag>
  )
}

import {
  ArrowRight,
  ArrowUpRight,
  Bike,
  Calendar,
  CalendarCheck,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Clock,
  Car,
  Dumbbell,
  Flag,
  Flame,
  Gauge,
  Loader2,
  Lock,
  LogOut,
  Mail,
  MapPin,
  Medal,
  Menu,
  Minus,
  Phone,
  Plus,
  QrCode,
  Share2,
  Snowflake,
  Sparkles,
  Star,
  Train,
  TrendingUp,
  Trophy,
  Users,
  Wind,
  X,
  type LucideIcon,
} from 'lucide-react'
import { cn } from '@/lib/utils'

/* ============================================================================
   Inline SVG only (03 §8). Lucide renders true <svg> elements — no icon font,
   no emoji, ever. Brand-specific marks that Lucide has no equivalent for are
   hand-authored below on the same 24×24 grid with stroke="currentColor".
   ============================================================================ */

function BarbellIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" {...props}>
      <path d="M3 8.5v7M6 6v12M18 6v12M21 8.5v7M6 12h12" />
    </svg>
  )
}

function KettlebellIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M9 6.5a3 3 0 0 1 6 0" />
      <path d="M9.4 7.6C7.4 8.8 6 11 6 13.6 6 16.6 8.2 19 12 19s6-2.4 6-5.4c0-2.6-1.4-4.8-3.4-6" />
    </svg>
  )
}

function BoxingGloveIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M6 9a4 4 0 0 1 4-4h3a4 4 0 0 1 4 4v3.5a3.5 3.5 0 0 1-3.5 3.5H9.5A3.5 3.5 0 0 1 6 12.5Z" />
      <path d="M6 10.5H4.8A1.8 1.8 0 0 0 3 12.3v0a1.8 1.8 0 0 0 1.8 1.8H6" />
      <path d="M7.5 16v2.5A1.5 1.5 0 0 0 9 20h6a1.5 1.5 0 0 0 1.5-1.5V16" />
    </svg>
  )
}

function BodyScanIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M4 7V5.5A1.5 1.5 0 0 1 5.5 4H7M17 4h1.5A1.5 1.5 0 0 1 20 5.5V7M20 17v1.5a1.5 1.5 0 0 1-1.5 1.5H17M7 20H5.5A1.5 1.5 0 0 1 4 18.5V17" />
      <circle cx="12" cy="9" r="1.6" />
      <path d="M9.2 19v-3.2c0-.9.5-1.6 1.2-2l.4-.2M14.8 19v-3.2c0-.9-.5-1.6-1.2-2l-.4-.2" />
    </svg>
  )
}

function ShakerIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M8 3h8v2.5l-.7 1.2A3 3 0 0 0 15 8.2V19a2 2 0 0 1-2 2h-2a2 2 0 0 1-2-2V8.2a3 3 0 0 0-.3-1.5L8 5.5Z" />
      <path d="M8.6 11.5h6.8M8.6 15h6.8" />
    </svg>
  )
}

function StretchIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <circle cx="9" cy="5" r="1.8" />
      <path d="M9 7.5v4.2l-3 2.8M9 11.7l4.5 1.3 2.5 4.5M6 14.5 4.5 20M13.5 13l5-1.5" />
    </svg>
  )
}

function LotusIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M12 5c1.8 1.6 2.7 3.6 2.7 6 0 1.4-.3 2.6-.9 3.7-.6-1.1-.9-2.3-.9-3.7 0-2.4.9-4.4 2.7-6" />
      <path d="M12 5c-1.8 1.6-2.7 3.6-2.7 6 0 1.4.3 2.6.9 3.7" />
      <path d="M4 12c2.3.2 4.1 1 5.4 2.4M20 12c-2.3.2-4.1 1-5.4 2.4" />
      <path d="M4.5 15.5c2 3 4.5 4.5 7.5 4.5s5.5-1.5 7.5-4.5" />
    </svg>
  )
}

function CoreIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M8 4h8M8 20h8" />
      <path d="M9 4c0 3-1.5 5-1.5 8S9 17 9 20M15 4c0 3 1.5 5 1.5 8S15 17 15 20" />
      <path d="M7.8 12h8.4" />
    </svg>
  )
}

function RunIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <circle cx="14.5" cy="4.8" r="1.8" />
      <path d="M13 8.2 9.6 10l1.2 3.4-2.3 3.1L6 20M10.8 13.4l3.4 1 1.4 5.6M13 8.2l3.6 1.4 2.4-1.2" />
    </svg>
  )
}

function StudioIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <rect x="3" y="4" width="18" height="14" rx="1.5" />
      <path d="M9 4v14M15 4v14M3 20h18" />
    </svg>
  )
}

function SpaIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <path d="M12 12c0-4 2-7 5-8.5.5 4-1 7.5-5 8.5ZM12 12c0-4-2-7-5-8.5-.5 4 1 7.5 5 8.5Z" />
      <path d="M4 14c2.5 4 5 6 8 6s5.5-2 8-6" />
    </svg>
  )
}

/* Lucide v1 dropped brand marks for trademark reasons, so the three social glyphs
   we need are hand-authored on the same 24×24 stroke grid. */

function InstagramIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" {...props}>
      <rect x="3" y="3" width="18" height="18" rx="5" />
      <circle cx="12" cy="12" r="3.8" />
      <circle cx="17.2" cy="6.8" r="0.9" fill="currentColor" stroke="none" />
    </svg>
  )
}

function YoutubeIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <rect x="2" y="5" width="20" height="14" rx="4" />
      <path d="M10.4 9.3l4.6 2.7-4.6 2.7Z" />
    </svg>
  )
}

function LinkedinIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" {...props}>
      <rect x="3" y="3" width="18" height="18" rx="3" />
      <path d="M7.2 10.4v6.4M7.2 7.6v.1M11.2 16.8v-6.4M11.2 12.8c0-1.4 1-2.4 2.4-2.4s2.4 1 2.4 2.4v4" />
    </svg>
  )
}

/** Every icon the app can render, addressed by a stable string key. */
export const icons = {
  // brand / training
  barbell: BarbellIcon as unknown as LucideIcon,
  kettlebell: KettlebellIcon as unknown as LucideIcon,
  'boxing-glove': BoxingGloveIcon as unknown as LucideIcon,
  'body-scan': BodyScanIcon as unknown as LucideIcon,
  shaker: ShakerIcon as unknown as LucideIcon,
  stretch: StretchIcon as unknown as LucideIcon,
  lotus: LotusIcon as unknown as LucideIcon,
  core: CoreIcon as unknown as LucideIcon,
  run: RunIcon as unknown as LucideIcon,
  studio: StudioIcon as unknown as LucideIcon,
  spa: SpaIcon as unknown as LucideIcon,
  dumbbell: Dumbbell,
  bike: Bike,
  flame: Flame,
  snowflake: Snowflake,
  wind: Wind,
  gauge: Gauge,
  // ui
  'arrow-right': ArrowRight,
  'arrow-up-right': ArrowUpRight,
  check: Check,
  'chevron-down': ChevronDown,
  'chevron-left': ChevronLeft,
  'chevron-right': ChevronRight,
  clock: Clock,
  car: Car,
  train: Train,
  calendar: Calendar,
  'calendar-check': CalendarCheck,
  loader: Loader2,
  lock: Lock,
  'log-out': LogOut,
  mail: Mail,
  'map-pin': MapPin,
  menu: Menu,
  minus: Minus,
  phone: Phone,
  plus: Plus,
  qr: QrCode,
  share: Share2,
  sparkles: Sparkles,
  star: Star,
  'trending-up': TrendingUp,
  users: Users,
  x: X,
  // achievement
  flag: Flag,
  medal: Medal,
  trophy: Trophy,
  // social
  instagram: InstagramIcon as unknown as LucideIcon,
  youtube: YoutubeIcon as unknown as LucideIcon,
  linkedin: LinkedinIcon as unknown as LucideIcon,
} as const

export type IconName = keyof typeof icons

export interface IconProps extends Omit<React.SVGProps<SVGSVGElement>, 'name'> {
  name: IconName
  /** Pixel size for both axes. 24 is the design-system default. */
  size?: number
  strokeWidth?: number
  /** Decorative by default; pass a label when the icon carries the only meaning. */
  label?: string
}

export function Icon({ name, size = 24, strokeWidth = 1.5, label, className, ...rest }: IconProps) {
  const Glyph = icons[name]

  if (!Glyph) {
    if (import.meta.env.DEV) console.warn(`[Icon] unknown icon "${name}"`)
    return null
  }

  return (
    <Glyph
      width={size}
      height={size}
      strokeWidth={strokeWidth}
      className={cn('shrink-0', className)}
      aria-hidden={label ? undefined : true}
      role={label ? 'img' : undefined}
      aria-label={label}
      focusable="false"
      {...rest}
    />
  )
}

/** True when a CMS-supplied iconKey maps to something we can actually render. */
export function isIconName(value: string | undefined | null): value is IconName {
  return typeof value === 'string' && value in icons
}

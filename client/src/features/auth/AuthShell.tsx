import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'
import { Icon } from '@/components/ui/Icon'
import { setting, useSiteSettings } from '@/lib/cms'
import { cn } from '@/lib/utils'
import { Photo } from '@/components/ui/Photo'

/**
 * Split auth layout: graded photography on one side, the form on the other. Same dark-luxe
 * tokens as the public site, so signing in does not feel like leaving the brand.
 */
export function AuthShell({
  eyebrow,
  headline,
  subhead,
  posterUrl,
  posterAlt,
  children,
  wide = false,
}: {
  eyebrow: string
  headline: string
  subhead?: string
  posterUrl?: string
  posterAlt?: string
  children: ReactNode
  wide?: boolean
}) {
  const { data: settings } = useSiteSettings()
  const brand = setting(settings, 'brand.name', 'FORGE')

  return (
    <div className="grid min-h-dvh bg-ink lg:grid-cols-2">
      {/* Photography panel — hidden on small screens where it would only cost bandwidth. */}
      <aside className="grain relative hidden overflow-hidden lg:block">
        {posterUrl && (
          <Photo
            src={posterUrl}
            alt={posterAlt ?? ''}
            sizes="(min-width: 1024px) 50vw, 100vw"
            className="absolute inset-0 h-full w-full object-cover"
          />
        )}
        <div
          aria-hidden
          className="absolute inset-0"
          style={{ background: 'linear-gradient(to top, rgb(10 10 10 / 0.92), rgb(10 10 10 / 0.45))' }}
        />
        <div className="relative z-10 flex h-full flex-col justify-between p-12">
          <Link to="/" className="inline-flex items-center gap-2.5 text-bone">
            <Icon name="barbell" size={26} className="text-accent" />
            <span className="font-display text-[1.25rem] font-semibold uppercase tracking-[0.02em]">
              {brand}
            </span>
          </Link>
          <div>
            <p className="display-l max-w-md text-bone">
              {setting(settings, 'brand.tagline', 'Strength, built in Bengaluru.')}
            </p>
            <p className="mt-5 max-w-sm text-[0.9375rem] leading-relaxed text-bone/70">
              Koramangala · Indiranagar · Whitefield. One membership, one app, one login.
            </p>
          </div>
        </div>
      </aside>

      <div className="flex flex-col justify-center px-6 py-16 sm:px-12">
        <div className={cn('mx-auto w-full', wide ? 'max-w-xl' : 'max-w-md')}>
          <Link
            to="/"
            className="group mb-10 inline-flex items-center gap-2 text-[0.8125rem] text-smoke transition-colors hover:text-accent lg:hidden"
          >
            <Icon name="chevron-left" size={15} />
            Back to site
          </Link>

          <p className="caption">{eyebrow}</p>
          <h1 className="display-l mt-4 text-bone">{headline}</h1>
          {subhead && (
            <p className="mt-4 text-[0.9375rem] leading-relaxed text-smoke">{subhead}</p>
          )}

          <div className="mt-10">{children}</div>
        </div>
      </div>
    </div>
  )
}

/** Labelled field with inline validation copy, wired for screen readers. */
export function Field({
  label,
  htmlFor,
  error,
  help,
  children,
  optional = false,
}: {
  label: string
  htmlFor: string
  error?: string
  help?: string
  children: ReactNode
  optional?: boolean
}) {
  return (
    <div>
      <div className="mb-2 flex items-baseline justify-between gap-3">
        <label htmlFor={htmlFor} className="text-[0.8125rem] font-medium text-bone">
          {label}
        </label>
        {optional && <span className="text-[0.75rem] text-smoke">Optional</span>}
      </div>
      {children}
      {help && !error && <p className="mt-1.5 text-[0.75rem] text-smoke">{help}</p>}
      {error && (
        <p role="alert" className="mt-1.5 flex items-center gap-1.5 text-[0.75rem] text-accent-hot">
          <Icon name="x" size={12} strokeWidth={2.4} />
          {error}
        </p>
      )}
    </div>
  )
}

export function FormError({ message }: { message: string | null }) {
  if (!message) return null
  return (
    <div
      role="alert"
      className="flex items-start gap-2.5 rounded-[var(--radius-card)] border border-accent-hot/40 bg-accent-hot/10 px-4 py-3 text-[0.875rem] text-bone"
    >
      <Icon name="x" size={16} strokeWidth={2.2} className="mt-0.5 text-accent-hot" />
      <span>{message}</span>
    </div>
  )
}

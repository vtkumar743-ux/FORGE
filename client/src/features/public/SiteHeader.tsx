import { useEffect, useState } from 'react'
import { Link, NavLink, useLocation } from 'react-router-dom'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import { Icon } from '@/components/ui/Icon'
import { ButtonLink } from '@/components/ui/Button'
import { useAuth } from '@/lib/auth'
import { setting, useSiteSettings, type SiteSettings } from '@/lib/cms'
import { cn, whatsappLink } from '@/lib/utils'

/**
 * Navigation by experience, not by SKU (03 §1, the David Lloyd fix for cult.fit's
 * six competing membership items in the nav).
 */
const navigation = [
  { label: 'Train', to: '/branches' },
  { label: 'Classes', to: '/classes' },
  { label: 'Coaches', to: '/trainers' },
  { label: 'Plans', to: '/plans' },
  { label: 'Results', to: '/transformations' },
  { label: 'Journal', to: '/journal' },
]

export function SiteHeader() {
  const { data: settings } = useSiteSettings()
  const { isAuthenticated, isAdmin } = useAuth()
  const [scrolled, setScrolled] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const location = useLocation()

  // Transparent over the hero, solid once scrolled past it.
  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 24)
    onScroll()
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  useEffect(() => setMenuOpen(false), [location.pathname])

  // Lock the page behind the mobile sheet.
  useEffect(() => {
    document.body.style.overflow = menuOpen ? 'hidden' : ''
    return () => {
      document.body.style.overflow = ''
    }
  }, [menuOpen])

  const brand = setting(settings, 'brand.name', 'FORGE')

  return (
    <>
      <AnnouncementBar settings={settings} />

      <header
        className={cn(
          'sticky top-0 z-[var(--z-header)] transition-[background-color,border-color,backdrop-filter] duration-300 ease-out',
          scrolled
            ? 'border-b border-[var(--hairline)] bg-ink/85 backdrop-blur-xl'
            : 'border-b border-transparent bg-transparent',
        )}
      >
        <div className="shell flex h-[4.5rem] items-center justify-between gap-6">
          <Link
            to="/"
            className="group flex items-center gap-2.5"
            aria-label={`${brand} — home`}
          >
            <BrandMark />
            <span className="font-display text-[1.375rem] font-semibold uppercase leading-none tracking-[0.02em] text-bone">
              {brand}
            </span>
          </Link>

          <nav aria-label="Main" className="hidden items-center gap-1 lg:flex">
            {navigation.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  cn(
                    'relative rounded-full px-3.5 py-2 text-[0.9375rem] transition-colors duration-200',
                    isActive ? 'text-accent' : 'text-bone/75 hover:text-bone',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    {item.label}
                    {isActive && (
                      <span
                        aria-hidden
                        className="absolute inset-x-3.5 -bottom-0.5 h-px bg-accent"
                      />
                    )}
                  </>
                )}
              </NavLink>
            ))}
          </nav>

          <div className="flex items-center gap-2">
            {isAuthenticated ? (
              <ButtonLink to={isAdmin ? '/admin' : '/portal'} variant="outline" size="sm" icon="arrow-up-right" iconAfter>
                {isAdmin ? 'Admin' : 'My account'}
              </ButtonLink>
            ) : (
              <ButtonLink to="/login" variant="ghost" size="sm" className="hidden px-3 sm:inline-flex">
                Sign in
              </ButtonLink>
            )}

            <ButtonLink to="/free-trial" size="sm" className="hidden sm:inline-flex">
              Free trial
            </ButtonLink>

            <button
              type="button"
              onClick={() => setMenuOpen(true)}
              aria-label="Open menu"
              aria-expanded={menuOpen}
              className="inline-flex size-10 items-center justify-center rounded-full border border-[var(--hairline-strong)] text-bone transition-colors hover:border-accent hover:text-accent lg:hidden"
            >
              <Icon name="menu" size={18} />
            </button>
          </div>
        </div>
      </header>

      <MobileMenu open={menuOpen} onClose={() => setMenuOpen(false)} settings={settings} />
    </>
  )
}

/** The animated logo mark — inline SVG, gold sleeves, bone bar. */
function BrandMark() {
  return (
    <svg
      viewBox="0 0 32 32"
      className="size-8"
      aria-hidden
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
    >
      <g className="text-accent" strokeWidth={2.4}>
        <path d="M6.5 11.5v9M9.5 9.5v13M22.5 9.5v13M25.5 11.5v9" />
      </g>
      <path
        d="M9.5 16h13"
        className="text-bone transition-transform duration-300 ease-out group-hover:scale-x-110"
        strokeWidth={2.2}
        style={{ transformOrigin: 'center' }}
      />
    </svg>
  )
}

function AnnouncementBar({ settings }: { settings: SiteSettings | undefined }) {
  const [dismissed, setDismissed] = useState(false)
  const announcement = settings?.announcement

  if (!announcement || dismissed) return null

  return (
    <div className="relative bg-accent text-ink">
      <div className="shell flex min-h-10 items-center justify-center gap-3 py-2 text-center">
        <p className="text-[0.8125rem] font-medium leading-snug">
          {announcement.text}
          {announcement.linkUrl && announcement.linkLabel && (
            <Link
              to={announcement.linkUrl}
              className="ml-2 inline-flex items-center gap-1 font-semibold underline underline-offset-2"
            >
              {announcement.linkLabel}
              <Icon name="arrow-right" size={13} strokeWidth={2.2} />
            </Link>
          )}
        </p>
        <button
          type="button"
          onClick={() => setDismissed(true)}
          aria-label="Dismiss announcement"
          className="absolute right-4 top-1/2 -translate-y-1/2 rounded-full p-1 transition-opacity hover:opacity-60"
        >
          <Icon name="x" size={14} strokeWidth={2.2} />
        </button>
      </div>
    </div>
  )
}

function MobileMenu({
  open,
  onClose,
  settings,
}: {
  open: boolean
  onClose: () => void
  settings: SiteSettings | undefined
}) {
  const { isAuthenticated, isAdmin } = useAuth()
  const reduced = useReducedMotion()

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-[var(--z-overlay)] lg:hidden"
          initial={reduced ? false : { opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={reduced ? { opacity: 1 } : { opacity: 0 }}
          transition={{ duration: reduced ? 0 : 0.2 }}
        >
          <div className="absolute inset-0 bg-ink/80 backdrop-blur-sm" onClick={onClose} />
          <motion.div
            role="dialog"
            aria-modal="true"
            aria-label="Menu"
            className="grain absolute inset-y-0 right-0 flex w-full max-w-sm flex-col bg-carbon"
            // A full-width slide is exactly the motion someone with vestibular sensitivity
            // turned the setting off for; under reduce the sheet simply appears.
            initial={reduced ? false : { x: '100%' }}
            animate={{ x: 0 }}
            exit={reduced ? { x: 0 } : { x: '100%' }}
            transition={{ duration: reduced ? 0 : 0.32, ease: [0.16, 1, 0.3, 1] }}
          >
            <div className="hairline-b flex h-[4.5rem] items-center justify-between px-6">
              <span className="caption">Menu</span>
              <button
                type="button"
                onClick={onClose}
                aria-label="Close menu"
                className="inline-flex size-10 items-center justify-center rounded-full border border-[var(--hairline-strong)] text-bone"
              >
                <Icon name="x" size={18} />
              </button>
            </div>

            <nav aria-label="Mobile" className="relative z-10 flex-1 overflow-y-auto px-6 py-8">
              <ul className="space-y-1">
                {navigation.map((item) => (
                  <li key={item.to}>
                    <NavLink
                      to={item.to}
                      className={({ isActive }) =>
                        cn(
                          'display-m flex items-center justify-between py-3 text-[1.5rem]',
                          isActive ? 'text-accent' : 'text-bone',
                        )
                      }
                    >
                      {item.label}
                      <Icon name="arrow-up-right" size={18} className="text-smoke" />
                    </NavLink>
                  </li>
                ))}
              </ul>

              <div className="hairline-t mt-8 space-y-3 pt-8">
                <ButtonLink to="/free-trial" fullWidth>
                  Book a free trial
                </ButtonLink>
                <ButtonLink
                  href={whatsappLink(
                    setting(settings, 'contact.whatsapp'),
                    setting(settings, 'contact.whatsappPrefill'),
                  )}
                  target="_blank"
                  variant="outline"
                  fullWidth
                >
                  WhatsApp us
                </ButtonLink>
                <ButtonLink
                  to={isAuthenticated ? (isAdmin ? '/admin' : '/portal') : '/login'}
                  variant="ghost"
                  fullWidth
                  className="justify-center"
                >
                  {isAuthenticated ? (isAdmin ? 'Admin panel' : 'My account') : 'Sign in'}
                </ButtonLink>
              </div>
            </nav>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}

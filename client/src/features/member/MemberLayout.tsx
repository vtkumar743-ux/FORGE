import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { Icon, type IconName } from '@/components/ui/Icon'
import { Button } from '@/components/ui/Button'
import { useAuth } from '@/lib/auth'
import { useSiteSettings } from '@/lib/cms'
import { cn } from '@/lib/utils'
import { usePortalHome } from './lib/portal-api'

/* ============================================================================
   Member portal shell

   Dark surface, same tokens as the public site. Navigation is a bottom tab bar
   on a phone and a horizontal rail on a desktop — a member opens this standing
   at a squat rack with one hand free, so the five things they came for are one
   thumb-reach away and everything else lives behind "More".
   ============================================================================ */

interface Tab {
  to: string
  label: string
  icon: IconName
  end?: boolean
}

const TABS: Tab[] = [
  { to: '/portal', label: 'Home', icon: 'flame', end: true },
  { to: '/portal/book', label: 'Book', icon: 'calendar' },
  { to: '/portal/qr', label: 'My QR', icon: 'qr' },
  { to: '/portal/workouts', label: 'Train', icon: 'barbell' },
  { to: '/portal/progress', label: 'Progress', icon: 'trending-up' },
]

const MORE: Tab[] = [
  { to: '/portal/community', label: 'Community', icon: 'trophy' },
  { to: '/portal/membership', label: 'Membership', icon: 'medal' },
  { to: '/portal/referrals', label: 'Refer a friend', icon: 'share' },
  { to: '/portal/notifications', label: 'Notifications', icon: 'mail' },
  { to: '/portal/profile', label: 'Profile', icon: 'users' },
]

export function MemberLayout() {
  const { user, logout } = useAuth()
  const { data: settings } = useSiteSettings()
  const { data: home } = usePortalHome()
  const location = useLocation()
  const [moreOpen, setMoreOpen] = useState(false)

  // A menu that survives navigation is a menu covering the page you just opened.
  useEffect(() => setMoreOpen(false), [location.pathname])

  const brand = settings?.values['brand.name'] ?? 'FORGE'
  const unread = home?.unreadNotifications ?? 0
  const firstName = home?.member.firstName ?? user?.fullName?.split(' ')[0] ?? 'Member'

  return (
    <div className="grain relative min-h-dvh bg-ink">
      <header className="hairline-b sticky top-0 z-[var(--z-header)] bg-ink/92 backdrop-blur-xl">
        <div className="shell flex h-[4.25rem] items-center justify-between gap-4">
          <Link to="/portal" className="flex shrink-0 items-center gap-2.5">
            <Icon name="barbell" size={22} className="text-accent" />
            <span className="font-display text-[1.0625rem] font-semibold uppercase tracking-[0.02em] text-bone">
              {brand}
            </span>
          </Link>

          <nav className="hidden items-center gap-1 lg:flex" aria-label="Portal">
            {[...TABS, ...MORE].map((tab) => (
              <NavLink
                key={tab.to}
                to={tab.to}
                end={tab.end}
                className={({ isActive }) =>
                  cn(
                    'relative rounded-full px-3.5 py-2 text-[0.875rem] transition-colors duration-200',
                    isActive ? 'text-accent' : 'text-smoke hover:text-bone',
                  )
                }
              >
                {tab.label}
                {tab.to === '/portal/notifications' && unread > 0 && (
                  <span className="numeric absolute -right-0.5 -top-0.5 grid min-w-[1.125rem] place-items-center rounded-full bg-accent px-1 text-[0.625rem] font-semibold text-ink">
                    {unread > 9 ? '9+' : unread}
                  </span>
                )}
              </NavLink>
            ))}
          </nav>

          <div className="flex items-center gap-2">
            <Link
              to="/portal/notifications"
              className="relative grid size-10 place-items-center rounded-full text-smoke transition-colors hover:bg-carbon hover:text-bone lg:hidden"
              aria-label={unread > 0 ? `Notifications, ${unread} unread` : 'Notifications'}
            >
              <Icon name="mail" size={19} />
              {unread > 0 && (
                <span className="absolute right-2 top-2 size-2 rounded-full bg-accent ring-2 ring-ink" aria-hidden />
              )}
            </Link>
            <Button variant="ghost" size="sm" icon="log-out" onClick={() => void logout()} className="hidden sm:inline-flex">
              Sign out
            </Button>
            <span className="hidden text-[0.8125rem] text-smoke xl:inline">{firstName}</span>
          </div>
        </div>
      </header>

      {/* pb clears the mobile tab bar, which is fixed over the content */}
      <main className="shell pb-32 pt-8 lg:pb-20 lg:pt-12">
        <Outlet />
      </main>

      {/* Mobile tab bar. Five destinations plus More — six is where a thumb starts missing. */}
      <nav
        className="fixed inset-x-0 bottom-0 z-[var(--z-header)] border-t border-[var(--hairline)] bg-ink/95 backdrop-blur-xl lg:hidden"
        aria-label="Portal"
      >
        <div className="mx-auto flex max-w-2xl items-stretch justify-between px-1 pb-[env(safe-area-inset-bottom)]">
          {TABS.map((tab) => (
            <NavLink
              key={tab.to}
              to={tab.to}
              end={tab.end}
              className={({ isActive }) =>
                cn(
                  'flex min-h-[3.75rem] flex-1 flex-col items-center justify-center gap-1 rounded-xl px-1 py-2 text-[0.625rem] uppercase tracking-[0.06em] transition-colors duration-200',
                  isActive ? 'text-accent' : 'text-smoke',
                )
              }
            >
              {({ isActive }) => (
                <>
                  <Icon name={tab.icon} size={20} strokeWidth={isActive ? 1.9 : 1.5} />
                  {tab.label}
                </>
              )}
            </NavLink>
          ))}
          <button
            type="button"
            onClick={() => setMoreOpen((open) => !open)}
            aria-expanded={moreOpen}
            className={cn(
              'flex min-h-[3.75rem] flex-1 flex-col items-center justify-center gap-1 rounded-xl px-1 py-2 text-[0.625rem] uppercase tracking-[0.06em] transition-colors duration-200',
              moreOpen ? 'text-accent' : 'text-smoke',
            )}
          >
            <Icon name={moreOpen ? 'x' : 'menu'} size={20} />
            More
          </button>
        </div>
      </nav>

      {moreOpen && (
        <>
          <button
            type="button"
            aria-label="Close menu"
            onClick={() => setMoreOpen(false)}
            className="fixed inset-0 z-[var(--z-header)] cursor-default bg-ink/70 backdrop-blur-sm lg:hidden"
          />
          <div className="fixed inset-x-3 bottom-[4.75rem] z-[var(--z-overlay)] overflow-hidden rounded-[var(--radius-sheet)] border border-[var(--hairline-strong)] bg-carbon lg:hidden">
            {MORE.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className="flex items-center justify-between gap-3 border-b border-[var(--hairline)] px-5 py-4 text-[0.9375rem] text-bone last:border-0"
              >
                <span className="flex items-center gap-3">
                  <Icon name={item.icon} size={18} className="text-accent" />
                  {item.label}
                </span>
                {item.to === '/portal/notifications' && unread > 0 ? (
                  <span className="numeric rounded-full bg-accent px-2 py-0.5 text-[0.6875rem] font-semibold text-ink">
                    {unread}
                  </span>
                ) : (
                  <Icon name="chevron-right" size={16} className="text-smoke" />
                )}
              </NavLink>
            ))}
            <button
              type="button"
              onClick={() => void logout()}
              className="flex w-full items-center gap-3 px-5 py-4 text-left text-[0.9375rem] text-smoke"
            >
              <Icon name="log-out" size={18} />
              Sign out
            </button>
          </div>
        </>
      )}
    </div>
  )
}

import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { Icon, type IconName } from '@/components/ui/Icon'
import { Button } from '@/components/ui/Button'
import { LoadingRegion, Skeleton } from '@/components/ui/Skeleton'
import { useAuth } from '@/lib/auth'
import { useOccupancy, useSiteSettings } from '@/lib/cms'
import { cn } from '@/lib/utils'
import { ToastProvider } from './components/overlays'
import { Pill } from './components/ui'

interface NavItem {
  to: string
  label: string
  icon: IconName
  end?: boolean
}

interface NavGroup {
  title: string
  items: NavItem[]
}

/**
 * Navigation is grouped the way the work is: the website the owner publishes, and the
 * operation they run. Ops sits first because that is the daily job; the CMS is where
 * they go deliberately.
 */
const groups: NavGroup[] = [
  {
    title: 'Operations',
    items: [
      { to: '/admin', label: 'Dashboard', icon: 'gauge', end: true },
      { to: '/admin/members', label: 'Members', icon: 'users' },
      { to: '/admin/leads', label: 'Leads', icon: 'flag' },
      { to: '/admin/attendance', label: 'Attendance', icon: 'qr' },
      { to: '/admin/scheduling', label: 'Classes', icon: 'calendar' },
    ],
  },
  {
    title: 'Money',
    items: [
      { to: '/admin/billing/plans', label: 'Plans & coupons', icon: 'medal' },
      { to: '/admin/billing/invoices', label: 'Invoices', icon: 'trending-up' },
      { to: '/admin/billing/collections', label: 'Collections', icon: 'clock' },
    ],
  },
  {
    title: 'Website',
    items: [
      { to: '/admin/cms', label: 'Pages', icon: 'sparkles' },
      { to: '/admin/cms/content', label: 'Content library', icon: 'star' },
      { to: '/admin/cms/media', label: 'Media', icon: 'studio' },
      { to: '/admin/cms/settings', label: 'Site settings', icon: 'lock' },
    ],
  },
]

export function AdminLayout() {
  const { user, logout } = useAuth()
  const { data: settings } = useSiteSettings()
  const { data: occupancy } = useOccupancy()
  const [mobileOpen, setMobileOpen] = useState(false)
  const location = useLocation()

  // A tap on a nav item should close the sheet, not leave it covering the page it opened.
  useEffect(() => setMobileOpen(false), [location.pathname])

  const brand = settings?.values['brand.name'] ?? 'FORGE'
  const onFloor = occupancy?.reduce((total, entry) => total + entry.currentCount, 0) ?? 0

  return (
    <ToastProvider>
      <div className="theme-light flex min-h-dvh">
        {/* ---- sidebar ---- */}
        <aside
          className={cn(
            'fixed inset-y-0 left-0 z-[var(--z-overlay)] flex w-[15.5rem] flex-col border-r border-[var(--hairline)]',
            'bg-carbon transition-transform duration-300 ease-out lg:static lg:translate-x-0',
            mobileOpen ? 'translate-x-0' : '-translate-x-full',
          )}
        >
          <div className="flex h-[4.25rem] shrink-0 items-center gap-2.5 border-b border-[var(--hairline)] px-5">
            <Link to="/admin" className="flex min-w-0 items-center gap-2.5">
              <Icon name="barbell" size={22} />
              <span className="font-display truncate text-[1rem] font-semibold uppercase tracking-[0.02em]">
                {brand}
              </span>
            </Link>
            <Pill tone="accent" className="ml-auto">
              Admin
            </Pill>
          </div>

          <nav className="min-h-0 flex-1 overflow-y-auto px-3 py-4" aria-label="Admin sections">
            {groups.map((group) => (
              <div key={group.title} className="mb-5 last:mb-0">
                <p className="caption mb-1.5 px-3 text-[0.6875rem]">{group.title}</p>
                <ul className="space-y-0.5">
                  {group.items.map((item) => (
                    <li key={item.to}>
                      <NavLink
                        to={item.to}
                        end={item.end}
                        className={({ isActive }) =>
                          cn(
                            'flex items-center gap-2.5 rounded-[0.625rem] px-3 py-2 text-[0.875rem]',
                            'transition-colors duration-150',
                            isActive
                              ? 'bg-[var(--accent-soft)] font-medium text-bone'
                              : 'text-smoke hover:bg-[color-mix(in_srgb,var(--bone)_5%,transparent)] hover:text-bone',
                          )
                        }
                      >
                        {({ isActive }) => (
                          <>
                            <Icon
                              name={item.icon}
                              size={17}
                              className={isActive ? 'text-accent' : 'text-smoke'}
                            />
                            {item.label}
                          </>
                        )}
                      </NavLink>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </nav>

          <div className="shrink-0 border-t border-[var(--hairline)] p-3">
            <Link
              to="/admin/attendance/kiosk"
              className="mb-2 flex items-center gap-2.5 rounded-[0.625rem] border border-[var(--accent-line)] bg-[var(--accent-soft)] px-3 py-2.5 text-[0.8125rem] font-medium text-bone transition-colors hover:border-accent"
            >
              <Icon name="qr" size={17} className="text-accent" />
              Open desk kiosk
            </Link>
            <div className="flex items-center justify-between gap-2 px-1">
              <div className="min-w-0">
                <p className="truncate text-[0.8125rem] font-medium">{user?.fullName}</p>
                <p className="truncate text-[0.6875rem] text-smoke">{user?.email}</p>
              </div>
              <button
                type="button"
                onClick={() => void logout()}
                className="shrink-0 rounded-full p-1.5 text-smoke transition-colors hover:text-accent-hot"
                aria-label="Sign out"
              >
                <Icon name="log-out" size={16} />
              </button>
            </div>
          </div>
        </aside>

        {mobileOpen && (
          <button
            type="button"
            aria-label="Close navigation"
            onClick={() => setMobileOpen(false)}
            className="fixed inset-0 z-[calc(var(--z-overlay)-1)] cursor-default bg-black/35 lg:hidden"
          />
        )}

        {/* ---- main ---- */}
        <div className="flex min-w-0 flex-1 flex-col">
          <header className="sticky top-0 z-[var(--z-header)] flex h-[4.25rem] shrink-0 items-center gap-3 border-b border-[var(--hairline)] bg-[color-mix(in_srgb,var(--carbon)_92%,transparent)] px-5 backdrop-blur-xl">
            <button
              type="button"
              onClick={() => setMobileOpen(true)}
              className="-ml-1 rounded-full p-2 text-smoke transition-colors hover:text-bone lg:hidden"
              aria-label="Open navigation"
            >
              <Icon name="menu" size={20} />
            </button>

            <div className="flex-1" />

            <div className="hidden items-center gap-2 rounded-full border border-[var(--hairline)] px-3 py-1.5 sm:flex">
              <span className="relative flex size-2">
                <span className="absolute inline-flex size-full animate-ping rounded-full bg-success opacity-60" />
                <span className="relative inline-flex size-2 rounded-full bg-success" />
              </span>
              <span className="numeric text-[0.8125rem] text-smoke">
                <strong className="font-semibold text-bone">{onFloor}</strong> on the floor
              </span>
            </div>

            <Button variant="outline" size="sm" icon="arrow-up-right" iconAfter onClick={() => window.open('/', '_blank')}>
              View site
            </Button>
          </header>

          <main className="min-w-0 flex-1 px-5 py-8 lg:px-8">
            <Outlet />
          </main>
        </div>
      </div>
    </ToastProvider>
  )
}

/** Route-level fallback that keeps the admin chrome in place while a page loads. */
export function AdminRouteFallback() {
  return (
    <div className="space-y-5">
      <LoadingRegion label="Loading" />
      <Skeleton className="h-8 w-64" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {Array.from({ length: 4 }).map((_, index) => (
          <Skeleton key={index} className="h-28 w-full" />
        ))}
      </div>
      <Skeleton className="h-72 w-full" />
    </div>
  )
}

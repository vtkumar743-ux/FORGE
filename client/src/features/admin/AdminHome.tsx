import { Link } from 'react-router-dom'
import { Badge, Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Reveal, RevealGroup } from '@/components/ui/Reveal'
import { EmptyState } from '@/components/ui/Skeleton'
import { useAuth } from '@/lib/auth'
import { useOccupancy, useSiteSettings } from '@/lib/cms'
import { formatCount } from '@/lib/utils'

/**
 * Admin shell — light surface on the same tokens (03 §2), because owners spend hours in
 * data views. Phase 0 proves the guarded route, the light theme and live API reads; the
 * CMS editors, dashboards and ops modules are Module 2.
 */
export function AdminHome() {
  const { user, logout } = useAuth()
  const { data: settings } = useSiteSettings()
  const { data: occupancy } = useOccupancy()

  const brand = settings?.values['brand.name'] ?? 'FORGE'
  const onFloor = occupancy?.reduce((total, entry) => total + entry.currentCount, 0) ?? 0

  return (
    <div className="theme-light min-h-dvh">
      <header className="sticky top-0 z-[var(--z-header)] border-b border-[var(--paper-line)] bg-[var(--paper-raised)]/90 backdrop-blur-xl">
        <div className="shell flex h-[4.5rem] items-center justify-between gap-4">
          <Link to="/" className="flex items-center gap-2.5">
            <Icon name="barbell" size={24} className="text-[var(--paper-ink)]" />
            <span className="font-display text-[1.125rem] font-semibold uppercase tracking-[0.02em]">
              {brand}
            </span>
            <Badge className="ml-1">Admin</Badge>
          </Link>
          <div className="flex items-center gap-3">
            <span className="hidden text-[0.8125rem] text-[var(--paper-smoke)] sm:inline">
              {user?.email}
            </span>
            <Button variant="ghost" size="sm" icon="log-out" onClick={() => void logout()}>
              Sign out
            </Button>
          </div>
        </div>
      </header>

      <main className="shell py-14">
        {user?.mustChangePassword && (
          <Reveal className="mb-10">
            <div className="flex items-start gap-3 rounded-[var(--radius-card)] border border-[var(--accent)] bg-[color-mix(in_srgb,var(--accent)_14%,transparent)] px-5 py-4">
              <Icon name="lock" size={18} className="mt-0.5 shrink-0" />
              <div>
                <p className="text-[0.9375rem] font-medium">Change the seeded password</p>
                <p className="mt-1 text-[0.875rem] text-[var(--paper-smoke)]">
                  This account still uses the password the seeder printed. The change-password
                  endpoint is live at <code>POST /api/auth/change-password</code>; the admin UI for
                  it ships with Module 2.
                </p>
              </div>
            </div>
          </Reveal>
        )}

        <Reveal>
          <p className="caption">Network overview</p>
          <h1 className="display-l mt-4">Good to go.</h1>
          <p className="measure mt-4 text-body-l leading-relaxed text-[var(--paper-smoke)]">
            The database is migrated and seeded, auth is live for both roles, and every public page
            is already rendering from the CMS tables.
          </p>
        </Reveal>

        <RevealGroup className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-4" stagger={0.06}>
          <Stat label="Branches" value={formatCount(settings?.branches.length ?? 0)} icon="map-pin" />
          <Stat label="On the floor now" value={formatCount(onFloor)} icon="users" />
          <Stat
            label="Busiest branch"
            value={
              occupancy && occupancy.length > 0
                ? [...occupancy].sort((a, b) => b.percentFull - a.percentFull)[0].branchName.replace('FORGE ', '')
                : '—'
            }
            icon="gauge"
          />
          <Stat label="CMS pages live" value="14" icon="sparkles" />
        </RevealGroup>

        <Reveal className="mt-12" delay={0.1}>
          <h2 className="display-m mb-5">Per-branch occupancy</h2>
          <Card surface="outline" padded={false} className="divide-y divide-[var(--paper-line)]">
            {(occupancy ?? []).map((entry) => (
              <div key={entry.branchId} className="flex items-center justify-between gap-6 px-6 py-5">
                <div>
                  <p className="font-medium">{entry.branchName}</p>
                  <p className="numeric mt-0.5 text-[0.8125rem] text-[var(--paper-smoke)]">
                    {entry.currentCount} of {entry.capacity} · {['Comfortable', 'Busy', 'Peak'][entry.band]}
                  </p>
                </div>
                <div className="flex w-40 items-center gap-3">
                  <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-[var(--paper-line)]">
                    <div
                      className="h-full rounded-full bg-[var(--accent)] transition-[width] duration-500 ease-out"
                      style={{ width: `${entry.percentFull}%` }}
                    />
                  </div>
                  <span className="numeric w-10 text-right text-[0.8125rem] font-medium">
                    {entry.percentFull}%
                  </span>
                </div>
              </div>
            ))}
          </Card>
        </Reveal>

        <Reveal className="mt-12" delay={0.14}>
          <h2 className="display-m mb-5">Coming next</h2>
          <EmptyState
            icon="sparkles"
            headline="CMS editors and operations land in Module 2"
            body="Structured section editors with drag-reorder and draft/publish, the media library with WebP variants, members, billing with GST invoices, scheduling, QR attendance and the leads board. The tables, seed data and CMS write endpoints they need are already in place."
          />
        </Reveal>
      </main>
    </div>
  )
}

function Stat({ label, value, icon }: { label: string; value: string; icon: 'map-pin' | 'users' | 'gauge' | 'sparkles' }) {
  return (
    <Card surface="outline">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="caption">{label}</p>
          <p className="numeric display-m mt-3">{value}</p>
        </div>
        <Icon name={icon} size={22} className="text-[var(--paper-smoke)]" />
      </div>
    </Card>
  )
}

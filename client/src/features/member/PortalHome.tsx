import { Link } from 'react-router-dom'
import { Badge, Card, CardBody, CardTitle } from '@/components/ui/Card'
import { Button, ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Reveal, RevealGroup } from '@/components/ui/Reveal'
import { EmptyState } from '@/components/ui/Skeleton'
import { useAuth } from '@/lib/auth'
import { useOccupancy, useSiteSettings } from '@/lib/cms'

/**
 * Member portal home — the Phase-0 authenticated shell. It renders the member's real
 * identity and the live occupancy meter from the API; the booking flow, QR, workout
 * logging and progress views are Module 3 (Phase 3 prompt).
 */
export function PortalHome() {
  const { user, logout } = useAuth()
  const { data: settings } = useSiteSettings()
  const { data: occupancy } = useOccupancy()

  const homeBranch = occupancy?.find((entry) => entry.branchId === user?.homeBranchId)
  const brand = settings?.values['brand.name'] ?? 'FORGE'

  return (
    <div className="min-h-dvh bg-ink">
      <header className="hairline-b sticky top-0 z-[var(--z-header)] bg-ink/90 backdrop-blur-xl">
        <div className="shell flex h-[4.5rem] items-center justify-between gap-4">
          <Link to="/" className="flex items-center gap-2.5">
            <Icon name="barbell" size={24} className="text-accent" />
            <span className="font-display text-[1.125rem] font-semibold uppercase tracking-[0.02em] text-bone">
              {brand}
            </span>
            <Badge className="ml-1">Portal</Badge>
          </Link>
          <Button variant="ghost" size="sm" icon="log-out" onClick={() => void logout()}>
            Sign out
          </Button>
        </div>
      </header>

      <main className="shell py-14">
        <Reveal>
          <p className="caption">{user?.memberCode ?? 'Member'}</p>
          <h1 className="display-l mt-4 text-bone">
            {user?.fullName?.split(' ')[0] ?? 'Welcome'}.
          </h1>
          <p className="measure mt-4 text-body-l leading-relaxed text-smoke">
            {user?.homeBranchName
              ? `Your home branch is ${user.homeBranchName}.`
              : 'Pick a home branch from your profile to see its timetable and occupancy.'}
          </p>
        </Reveal>

        <RevealGroup className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-3" stagger={0.07}>
          <Card>
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="caption">Your home floor right now</p>
                <p className="numeric display-m mt-3 text-accent">
                  {homeBranch ? `${homeBranch.percentFull}%` : '—'}
                </p>
                <p className="mt-2 text-[0.875rem] text-smoke">
                  {homeBranch
                    ? `${homeBranch.currentCount} of ${homeBranch.capacity} on the floor · ${
                        ['Comfortable', 'Busy', 'Peak'][homeBranch.band]
                      }`
                    : 'No reading available'}
                </p>
              </div>
              <Icon name="gauge" size={26} className="text-bone/30" />
            </div>
          </Card>

          <Card>
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="caption">Membership</p>
                <p className="display-m mt-3 text-bone">Not active</p>
                <p className="mt-2 text-[0.875rem] text-smoke">
                  Buy a plan to start booking classes and checking in with your QR.
                </p>
              </div>
              <Icon name="qr" size={26} className="text-bone/30" />
            </div>
            <ButtonLink to="/plans" size="sm" className="mt-5">
              See plans
            </ButtonLink>
          </Card>

          <Card>
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="caption">Free trial</p>
                <p className="display-m mt-3 text-bone">One day, free</p>
                <p className="mt-2 text-[0.875rem] text-smoke">
                  Any class on the timetable, the full floor and a coach walking you through it.
                </p>
              </div>
              <Icon name="calendar-check" size={26} className="text-bone/30" />
            </div>
            <ButtonLink to="/free-trial" size="sm" variant="outline" className="mt-5">
              Book a trial
            </ButtonLink>
          </Card>
        </RevealGroup>

        <Reveal className="mt-12" delay={0.1}>
          <CardTitle as="h2" underline={false} className="mb-5">
            Coming next
          </CardTitle>
          <EmptyState
            icon="calendar"
            headline="Bookings, workouts and progress land next"
            body="Class booking with waitlist auto-promotion, your check-in QR, the workout logger with rest timer and PR detection, and body-scan history are built in Module 3. Your account and login already work — nothing here needs redoing."
            actionLabel="Browse the timetable"
            actionTo="/classes"
          />
        </Reveal>

        <Reveal className="mt-10" delay={0.14}>
          <CardBody>
            Signed in as {user?.email ?? user?.phone} · roles: {user?.roles.join(', ')}
          </CardBody>
        </Reveal>
      </main>
    </div>
  )
}

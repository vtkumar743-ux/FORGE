import { useEffect, useState } from 'react'
import { QRCodeSVG } from 'qrcode.react'
import { Badge } from '@/components/ui/Card'
import { ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { formatDate } from '@/lib/utils'
import { usePortalCard } from './lib/portal-api'
import { InlineNote, Panel, PortalHeading } from './components/ui'

/**
 * My QR (Module 3 — My QR): the digital membership card the desk scans.
 *
 * The code is drawn as an SVG rather than fetched as an image, so it stays sharp
 * on any screen and works with no network — which is the state a member is in
 * when they are standing at a turnstile in a basement gym.
 */
export function MyQrPage() {
  const { data, isLoading } = usePortalCard()
  const [now, setNow] = useState(() => new Date())

  // The clock under the code is what tells the person at the desk the screen is live
  // and not a screenshot someone forwarded on WhatsApp.
  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 30_000)
    return () => window.clearInterval(timer)
  }, [])

  if (isLoading || !data) {
    return (
      <div>
        <PortalHeading eyebrow="Check in" title="My QR" />
        <Skeleton className="mx-auto h-[26rem] w-full max-w-sm" />
      </div>
    )
  }

  return (
    <div>
      <PortalHeading
        eyebrow="Check in"
        title="My QR"
        lead="Show this at the desk. One scan marks your visit, advances your streak and checks you into any class you have booked today."
      />

      <div className="grid gap-6 lg:grid-cols-[minmax(0,22rem)_1fr]">
        {/* The card itself. Bone on black inverted for the code: scanners want contrast,
            and a gold-on-black QR is the one place style would cost function. */}
        <div
          className={`relative overflow-hidden rounded-[var(--radius-sheet)] border p-6 ${
            data.isUsable ? 'border-[var(--accent-line)]' : 'border-accent-hot/50'
          } bg-carbon`}
        >
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="caption">{data.homeBranchName}</p>
              <h2 className="display-m mt-2 truncate text-[1.25rem] text-bone">{data.fullName}</h2>
              <p className="numeric mt-1 text-[0.8125rem] text-smoke">{data.memberCode}</p>
            </div>
            <Icon name="barbell" size={26} className="shrink-0 text-accent" />
          </div>

          <div className="mt-6 flex justify-center rounded-[var(--radius-card)] bg-bone p-5">
            <QRCodeSVG
              value={data.qrToken}
              size={200}
              level="M"
              bgColor="#F5F3EE"
              fgColor="#0A0A0A"
              title={`Membership QR for ${data.memberCode}`}
            />
          </div>

          <dl className="mt-6 grid grid-cols-2 gap-x-4 gap-y-4 border-t border-[var(--hairline)] pt-5 text-[0.8125rem]">
            <div>
              <dt className="caption text-[0.5625rem]">Plan</dt>
              <dd className="mt-1 truncate text-bone">{data.planName ?? 'No active plan'}</dd>
            </div>
            <div>
              <dt className="caption text-[0.5625rem]">Valid to</dt>
              <dd className="numeric mt-1 text-bone">
                {data.validUntil ? formatDate(data.validUntil) : '—'}
              </dd>
            </div>
          </dl>

          <div className="mt-5 flex items-center justify-between gap-3">
            {data.isUsable ? (
              <Badge tone="success" icon="check">
                Ready to scan
              </Badge>
            ) : (
              <Badge tone="hot" icon="lock">
                Needs the desk
              </Badge>
            )}
            <span className="numeric text-[0.6875rem] text-smoke">
              {now.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: true })}
            </span>
          </div>
        </div>

        <div className="space-y-5">
          {!data.isUsable && data.blockReason && (
            <InlineNote tone="danger" icon="lock">
              {data.blockReason}
            </InlineNote>
          )}

          {data.isUsable && data.daysLeft != null && data.daysLeft <= 7 && (
            <InlineNote tone="warn" icon="clock">
              Your membership ends in {data.daysLeft} day{data.daysLeft === 1 ? '' : 's'}. Renew before it lapses and
              you keep every day you have paid for.
            </InlineNote>
          )}

          <Panel title="How check-in works">
            <ol className="space-y-4 text-[0.9375rem] leading-relaxed text-smoke">
              <Step index={1}>
                Hold the code under the scanner at the desk, or hand your phone over — the tablet reads it either way.
              </Step>
              <Step index={2}>
                The desk sees your plan, expiry and today's classes on one screen. Dues are a conversation, not a
                locked door.
              </Step>
              <Step index={3}>
                Your streak advances on the first scan of the day, and a class you have booked is marked attended
                automatically.
              </Step>
            </ol>
            <p className="mt-5 border-t border-[var(--hairline)] pt-4 text-[0.8125rem] leading-relaxed text-smoke/80">
              Turn your screen brightness up if the scanner struggles. The code never changes, so it works offline and
              a screenshot of it works too — treat it like a key.
            </p>
          </Panel>

          <div className="flex flex-wrap gap-2.5">
            <ButtonLink to="/portal/book" size="sm" variant="outline" icon="calendar">
              Book a class first
            </ButtonLink>
            <ButtonLink to="/portal/membership" size="sm" variant="ghost">
              Membership and invoices
            </ButtonLink>
          </div>
        </div>
      </div>
    </div>
  )
}

function Step({ index, children }: { index: number; children: React.ReactNode }) {
  return (
    <li className="flex gap-3.5">
      <span className="numeric grid size-7 shrink-0 place-items-center rounded-full border border-[var(--accent-line)] text-[0.75rem] font-semibold text-accent">
        {index}
      </span>
      <span>{children}</span>
    </li>
  )
}

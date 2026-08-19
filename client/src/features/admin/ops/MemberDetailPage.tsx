import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { QRCodeSVG } from 'qrcode.react'
import { Button } from '@/components/ui/Button'
import { Icon, type IconName } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { useBillingActions, useDecideFreeze, useFreezeRequests, useMember, useMemberMutations } from '../lib/admin-api'
import { describeErrorText, formatInr, formatIsoDate, formatIstDateTime, istToday, relativeTime } from '../lib/format'
import {
  genderNames,
  invoiceStatusNames,
  memberStatusNames,
  subscriptionStatusNames,
} from '../lib/types'
import { ConfirmDialog, Drawer, useToast } from '../components/overlays'
import {
  Avatar,
  DataTable,
  Hint,
  InlineError,
  PageHeader,
  Panel,
  Pill,
  RiskPill,
  SelectField,
  StatusPill,
  TextField,
} from '../components/ui'
import { MemberFormDrawer } from './MembersPage'
import { RecordPaymentDrawer, SellPlanDrawer } from './billing-drawers'
import { formatPhone, telLink } from '@/lib/utils'

/**
 * One member, everything about them. The timeline is the point: joins, payments, visits and
 * bookings merged in one reverse-chronological feed, so the desk can answer "what happened
 * with this person" without opening four other screens.
 */
export function MemberDetailPage() {
  const { id } = useParams()
  const memberId = Number(id)
  const toast = useToast()
  const { data, isLoading } = useMember(Number.isFinite(memberId) ? memberId : null)
  const mutations = useMemberMutations()
  const billing = useBillingActions()

  const [editOpen, setEditOpen] = useState(false)
  const [sellOpen, setSellOpen] = useState(false)
  const [qrOpen, setQrOpen] = useState(false)
  const [freezeFor, setFreezeFor] = useState<number | null>(null)
  const [cancelFor, setCancelFor] = useState<number | null>(null)
  const [payFor, setPayFor] = useState<{ id: number; number: string; due: number } | null>(null)

  if (isLoading || !data) {
    return (
      <div className="space-y-5">
        <Skeleton className="h-9 w-64" />
        <div className="grid gap-5 lg:grid-cols-[1.4fr_1fr]">
          <Skeleton className="h-96 w-full" />
          <Skeleton className="h-96 w-full" />
        </div>
      </div>
    )
  }

  const { summary, profile, stats } = data
  const activeSubscription = data.subscriptions.find((s) => s.status === 1 || s.status === 2)

  return (
    <>
      <PageHeader
        eyebrow={
          <>
            <Link to="/admin/members" className="hover:text-accent">
              Members
            </Link>{' '}
            / {summary.memberCode}
          </>
        }
        title={summary.fullName}
        actions={
          <>
            <Button variant="ghost" size="sm" icon="qr" onClick={() => setQrOpen(true)}>
              Member QR
            </Button>
            <Button variant="outline" size="sm" onClick={() => setEditOpen(true)}>
              Edit profile
            </Button>
            <Button size="sm" icon="plus" onClick={() => setSellOpen(true)}>
              Sell membership
            </Button>
          </>
        }
      />

      <div className="grid gap-5 xl:grid-cols-[1fr_20rem]">
        <div className="min-w-0 space-y-5">
          {/* ---- header card ---- */}
          <Panel>
            <div className="flex flex-wrap items-start gap-5">
              <Avatar src={summary.photoUrl} name={summary.fullName} size={72} />
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <StatusPill status={memberStatusNames[summary.status] ?? '—'} />
                  <RiskPill band={summary.churnRisk} />
                  {summary.currentStreakDays > 0 && (
                    <Pill tone="accent" icon="flame">
                      {summary.currentStreakDays}-day streak
                    </Pill>
                  )}
                  {!profile.waiverSigned && <Pill tone="danger">Waiver unsigned</Pill>}
                  {summary.tags.map((tag) => (
                    <Pill key={tag}>{tag}</Pill>
                  ))}
                </div>
                <dl className="mt-4 grid gap-x-8 gap-y-2.5 text-[0.875rem] sm:grid-cols-2 lg:grid-cols-3">
                  <Detail label="Mobile" value={formatPhone(summary.phone)} href={telLink(summary.phone)} />
                  <Detail label="Email" value={summary.email ?? '—'} />
                  <Detail label="Home branch" value={summary.branchName} />
                  <Detail label="Joined" value={formatIsoDate(summary.joinedOn)} />
                  <Detail label="Date of birth" value={formatIsoDate(profile.dateOfBirth)} />
                  <Detail label="Gender" value={genderNames[profile.gender] ?? '—'} />
                  <Detail label="Goal" value={profile.primaryGoal ?? '—'} />
                  <Detail label="Emergency" value={profile.emergencyContactName ?? '—'} />
                  <Detail label="Referral code" value={profile.referralCode ?? '—'} />
                </dl>
              </div>
            </div>

            {(profile.medicalNotes || profile.injuryNotes) && (
              <div className="mt-5">
                <Hint icon="lock">
                  {profile.medicalNotes && (
                    <p>
                      <strong>Medical:</strong> {profile.medicalNotes}
                    </p>
                  )}
                  {profile.injuryNotes && (
                    <p className={profile.medicalNotes ? 'mt-1' : undefined}>
                      <strong>Injuries:</strong> {profile.injuryNotes}
                    </p>
                  )}
                </Hint>
              </div>
            )}
          </Panel>

          <FreezeRequests memberId={memberId} />

          {/* ---- memberships ---- */}
          <Panel
            title="Memberships"
            description="Every plan this member has held, newest first."
            padded={false}
            actions={
              activeSubscription && (
                <>
                  <Button variant="ghost" size="sm" onClick={() => setFreezeFor(activeSubscription.id)}>
                    Freeze
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => setCancelFor(activeSubscription.id)}>
                    Cancel
                  </Button>
                </>
              )
            }
          >
            <DataTable
              rows={data.subscriptions}
              rowKey={(row) => row.id}
              emptyHeadline="No membership yet"
              emptyBody="Sell a plan to turn this person into an active member."
              columns={[
                {
                  key: 'plan',
                  header: 'Plan',
                  cell: (row) => (
                    <div>
                      <p className="font-medium">{row.planName}</p>
                      <p className="text-[0.75rem] text-smoke">{row.branchName}</p>
                    </div>
                  ),
                },
                {
                  key: 'status',
                  header: 'Status',
                  cell: (row) => <StatusPill status={subscriptionStatusNames[row.status] ?? '—'} />,
                },
                {
                  key: 'window',
                  header: 'Window',
                  cell: (row) => (
                    <span className="numeric text-[0.8125rem] text-smoke">
                      {formatIsoDate(row.startsOn)} → {formatIsoDate(row.endsOn)}
                    </span>
                  ),
                },
                {
                  key: 'left',
                  header: 'Left',
                  align: 'right',
                  cell: (row) =>
                    row.status === 1 ? (
                      <span className={row.daysLeft <= 7 ? 'numeric text-accent-hot' : 'numeric'}>{row.daysLeft}d</span>
                    ) : (
                      <span className="text-smoke">—</span>
                    ),
                },
                {
                  key: 'credits',
                  header: 'Credits',
                  align: 'right',
                  cell: (row) =>
                    row.classCreditsRemaining || row.ptCreditsRemaining ? (
                      <span className="numeric text-[0.8125rem]">
                        {row.classCreditsRemaining} cls · {row.ptCreditsRemaining} PT
                      </span>
                    ) : (
                      <span className="text-smoke">—</span>
                    ),
                },
                {
                  key: 'price',
                  header: 'Paid',
                  align: 'right',
                  cell: (row) => <span className="numeric">{formatInr(row.priceCharged)}</span>,
                },
              ]}
            />
          </Panel>

          {/* ---- invoices ---- */}
          <Panel title="Invoices" description="GST invoices raised against this member." padded={false}>
            <DataTable
              rows={data.invoices}
              rowKey={(row) => row.id}
              emptyHeadline="Nothing billed yet"
              columns={[
                {
                  key: 'number',
                  header: 'Invoice',
                  cell: (row) => (
                    <Link to={`/admin/billing/invoices/${row.id}`} className="numeric font-medium hover:text-accent">
                      {row.invoiceNumber}
                    </Link>
                  ),
                },
                { key: 'issued', header: 'Issued', cell: (row) => <span className="numeric text-[0.8125rem] text-smoke">{formatIsoDate(row.issuedOn)}</span> },
                { key: 'status', header: 'Status', cell: (row) => <StatusPill status={invoiceStatusNames[row.status] ?? '—'} /> },
                { key: 'total', header: 'Total', align: 'right', cell: (row) => <span className="numeric">{formatInr(row.grandTotal)}</span> },
                {
                  key: 'due',
                  header: 'Due',
                  align: 'right',
                  cell: (row) =>
                    row.amountDue > 0 ? (
                      <span className="numeric text-accent-hot">{formatInr(row.amountDue)}</span>
                    ) : (
                      <span className="text-success">paid</span>
                    ),
                },
                {
                  key: 'action',
                  header: '',
                  align: 'right',
                  cell: (row) =>
                    row.amountDue > 0 ? (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setPayFor({ id: row.id, number: row.invoiceNumber, due: row.amountDue })}
                      >
                        Collect
                      </Button>
                    ) : null,
                },
              ]}
            />
          </Panel>

          {/* ---- timeline ---- */}
          <Panel title="Activity" description="Everything that has happened, newest first.">
            {data.timeline.length === 0 ? (
              <p className="py-6 text-center text-[0.875rem] text-smoke">Nothing recorded yet.</p>
            ) : (
              <ol className="relative space-y-5 pl-6">
                <span className="absolute left-[0.3125rem] top-1.5 bottom-1.5 w-px bg-[var(--hairline)]" aria-hidden />
                {data.timeline.map((entry, index) => (
                  <li key={`${entry.atUtc}-${index}`} className="relative">
                    <span
                      aria-hidden
                      className="absolute -left-6 top-1.5 flex size-2.5 items-center justify-center rounded-full border-2 border-[var(--carbon)] bg-[var(--steel)]"
                      style={{ background: timelineColour(entry.kind) }}
                    />
                    <div className="flex flex-wrap items-baseline justify-between gap-2">
                      <p className="text-[0.875rem] font-medium">{entry.title}</p>
                      <span className="numeric text-[0.75rem] text-smoke">{formatIstDateTime(entry.atUtc)}</span>
                    </div>
                    {entry.detail && <p className="mt-0.5 text-[0.8125rem] text-smoke">{entry.detail}</p>}
                    {entry.amount && <p className="numeric mt-0.5 text-[0.8125rem] text-accent">{entry.amount}</p>}
                  </li>
                ))}
              </ol>
            )}
          </Panel>
        </div>

        {/* ---- sidebar ---- */}
        <div className="space-y-5">
          <Panel title="At a glance">
            <dl className="space-y-3 text-[0.875rem]">
              <Metric icon="qr" label="Total visits" value={String(stats.totalVisits)} sub={`${stats.visitsLast30Days} in 30 days`} />
              <Metric icon="calendar-check" label="Classes attended" value={String(stats.classesAttended)} sub={`${stats.noShows} no-shows`} />
              <Metric icon="flame" label="Longest streak" value={`${stats.longestStreakDays} days`} />
              <Metric icon="medal" label="Lifetime value" value={formatInr(stats.lifetimeValue)} />
              <Metric icon="gauge" label="Churn score" value={String(stats.churnScore)} sub={`last seen ${summary.lastVisitOn ? relativeTime(new Date(summary.lastVisitOn)) : 'never'}`} />
            </dl>
          </Panel>

          <Panel title="Upcoming classes" padded={false}>
            {data.upcomingBookings.length === 0 ? (
              <p className="px-5 py-6 text-center text-[0.8125rem] text-smoke">Nothing booked.</p>
            ) : (
              <ul className="divide-y divide-[var(--hairline)]">
                {data.upcomingBookings.map((booking) => (
                  <li key={booking.id} className="flex items-center justify-between gap-3 px-5 py-3">
                    <div className="min-w-0">
                      <p className="truncate text-[0.875rem] font-medium">{booking.formatName}</p>
                      <p className="numeric truncate text-[0.75rem] text-smoke">
                        {formatIsoDate(booking.date)} · {booking.startTime} · {booking.trainerName}
                      </p>
                    </div>
                    {booking.status === 1 && <Pill tone="warn">#{booking.waitlistPosition}</Pill>}
                  </li>
                ))}
              </ul>
            )}
          </Panel>

          <Panel title="Status">
            <SelectField
              label="Lifecycle"
              hint="Members are retired by status, never deleted — the payment trail must outlive the record."
              value={String(summary.status)}
              onChange={(event) =>
                void mutations.setStatus
                  .mutateAsync({ id: summary.id, status: Number(event.target.value) })
                  .then(() => toast.success('Status updated'))
                  .catch((error) => toast.error('Could not update', describeErrorText(error)))
              }
            >
              {memberStatusNames.map((name, index) => (
                <option key={name} value={index}>
                  {name}
                </option>
              ))}
            </SelectField>
          </Panel>
        </div>
      </div>

      {/* ---- overlays ---- */}
      <MemberFormDrawer
        open={editOpen}
        onClose={() => setEditOpen(false)}
        member={{ ...summary, profile: profile as unknown as Record<string, unknown> }}
      />

      <SellPlanDrawer
        open={sellOpen}
        onClose={() => setSellOpen(false)}
        memberId={summary.id}
        memberName={summary.fullName}
        defaultBranchId={summary.branchId}
      />

      {payFor && (
        <RecordPaymentDrawer
          open
          onClose={() => setPayFor(null)}
          invoiceId={payFor.id}
          invoiceNumber={payFor.number}
          amountDue={payFor.due}
          memberName={summary.fullName}
        />
      )}

      <FreezeDrawer
        subscriptionId={freezeFor}
        onClose={() => setFreezeFor(null)}
        freezeDaysAllowed={activeSubscription?.freezeDaysAllowed ?? 0}
        freezeDaysUsed={activeSubscription?.freezeDaysUsed ?? 0}
      />

      <ConfirmDialog
        open={cancelFor !== null}
        onClose={() => setCancelFor(null)}
        title="Cancel this membership?"
        body="The subscription is closed and auto-renew is switched off. Invoices and payments already raised are untouched."
        confirmLabel="Cancel membership"
        tone="danger"
        loading={billing.cancel.isPending}
        onConfirm={() => {
          if (cancelFor === null) return
          void billing.cancel
            .mutateAsync({ id: cancelFor, reason: 'Cancelled at the desk' })
            .then(() => {
              toast.success('Membership cancelled')
              setCancelFor(null)
            })
            .catch((error) => toast.error('Could not cancel', describeErrorText(error)))
        }}
      />

      <Drawer open={qrOpen} onClose={() => setQrOpen(false)} title="Member QR" description="Scanned at the desk kiosk to check in.">
        <div className="flex flex-col items-center gap-5 py-6">
          <div className="rounded-[var(--radius-card)] bg-white p-5">
            <QRCodeSVG value={profile.qrToken} size={200} level="M" />
          </div>
          <div className="text-center">
            <p className="display-m text-[1.25rem]">{summary.fullName}</p>
            <p className="numeric mt-1 text-[0.875rem] text-smoke">{summary.memberCode}</p>
            <p className="mt-3 text-[0.75rem] text-smoke">
              {summary.planName ?? 'No active plan'}
              {summary.membershipEndsOn ? ` · valid to ${formatIsoDate(summary.membershipEndsOn)}` : ''}
            </p>
          </div>
        </div>
      </Drawer>
    </>
  )
}

/* ---------------------------------------------------------------- pieces */

function Detail({ label, value, href }: { label: string; value: string; href?: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-[0.6875rem] uppercase tracking-[0.08em] text-smoke">{label}</dt>
      <dd className="truncate">
        {href ? (
          <a href={href} className="hover:text-accent">
            {value}
          </a>
        ) : (
          value
        )}
      </dd>
    </div>
  )
}

function Metric({ icon, label, value, sub }: { icon: IconName; label: string; value: string; sub?: string }) {
  return (
    <div className="flex items-start gap-3">
      <Icon name={icon} size={16} className="mt-0.5 shrink-0 text-smoke" />
      <div className="min-w-0 flex-1">
        <dt className="text-[0.75rem] text-smoke">{label}</dt>
        <dd className="numeric text-[0.9375rem] font-medium">{value}</dd>
        {sub && <p className="text-[0.75rem] text-smoke">{sub}</p>}
      </div>
    </div>
  )
}

function timelineColour(kind: string): string {
  switch (kind) {
    case 'payment':
      return 'var(--success)'
    case 'membership':
      return 'var(--accent)'
    case 'blocked':
    case 'cancellation':
      return 'var(--accent-hot)'
    default:
      return 'var(--steel)'
  }
}

function FreezeDrawer({
  subscriptionId,
  onClose,
  freezeDaysAllowed,
  freezeDaysUsed,
}: {
  subscriptionId: number | null
  onClose: () => void
  freezeDaysAllowed: number
  freezeDaysUsed: number
}) {
  const toast = useToast()
  const billing = useBillingActions()
  const [from, setFrom] = useState(istToday())
  const [to, setTo] = useState(istToday(14))
  const [error, setError] = useState<string | null>(null)

  async function submit() {
    if (subscriptionId === null) return
    setError(null)
    try {
      await billing.freeze.mutateAsync({ id: subscriptionId, from, to })
      toast.success('Membership frozen', 'The end date has moved out by the frozen days.')
      onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <Drawer
      open={subscriptionId !== null}
      onClose={onClose}
      title="Freeze membership"
      description="Frozen days are added back to the end date, so no paid time is lost."
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button size="sm" icon="snowflake" onClick={() => void submit()} loading={billing.freeze.isPending}>
            Freeze
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {error && <InlineError>{error}</InlineError>}
        <Hint icon="snowflake">
          This plan allows {freezeDaysAllowed} freeze days; {freezeDaysUsed} have been used.
        </Hint>
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField label="From" type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
          <TextField label="To" type="date" value={to} onChange={(event) => setTo(event.target.value)} />
        </div>
      </div>
    </Drawer>
  )
}

/**
 * Freeze asks the member raised in the portal. It sits above the membership table
 * because it is the one thing on this screen that is waiting on the desk rather
 * than merely recording what already happened.
 */
function FreezeRequests({ memberId }: { memberId: number }) {
  const { data } = useFreezeRequests(memberId)
  const decide = useDecideFreeze()
  const toast = useToast()
  const [declining, setDeclining] = useState<number | null>(null)
  const [note, setNote] = useState('')

  const pending = data ?? []
  if (pending.length === 0) return null

  function answer(id: number, approve: boolean, reason?: string) {
    decide.mutate(
      { id, approve, note: reason },
      {
        onSuccess: () => {
          toast.success(approve ? 'Freeze applied' : 'Request declined', 'The member has been notified.')
          setDeclining(null)
          setNote('')
        },
        onError: (error) => toast.error('That did not go through', describeErrorText(error)),
      },
    )
  }

  return (
    <Panel
      title="Freeze requests"
      description="Raised by the member in the portal. Approving applies the freeze and pushes the end date out by the same number of days."
      padded={false}
    >
      <ul className="divide-y divide-[var(--hairline)]">
        {pending.map((request) => (
          <li key={request.id} className="px-5 py-4">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="min-w-0">
                <p className="text-[0.9375rem]">
                  {request.planName} · <span className="numeric">{request.days}</span> days from{' '}
                  {formatIsoDate(request.requestedFrom)} to {formatIsoDate(request.requestedTo)}
                </p>
                <p className="mt-1.5 text-[0.8125rem] leading-relaxed text-smoke">"{request.reason}"</p>
                <p className="mt-1 text-[0.6875rem] text-smoke/75">Asked {relativeTime(request.requestedAtUtc)}</p>
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <Button size="sm" loading={decide.isPending} onClick={() => answer(request.id, true)}>
                  Approve
                </Button>
                <Button size="sm" variant="ghost" onClick={() => setDeclining(request.id)}>
                  Decline
                </Button>
              </div>
            </div>

            {declining === request.id && (
              <div className="mt-4 space-y-3 border-t border-[var(--hairline)] pt-4">
                <TextField
                  label="Why"
                  hint="Shown to the member word for word. A decline with no reason reads as a system fault."
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                  placeholder="Your plan's freeze allowance is already used up this year."
                />
                <div className="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    loading={decide.isPending}
                    disabled={note.trim().length < 3}
                    onClick={() => answer(request.id, false, note.trim())}
                  >
                    Send the decline
                  </Button>
                  <Button size="sm" variant="ghost" onClick={() => setDeclining(null)}>
                    Cancel
                  </Button>
                </div>
              </div>
            )}
          </li>
        ))}
      </ul>
    </Panel>
  )
}

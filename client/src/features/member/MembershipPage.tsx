import { useState } from 'react'
import { Badge } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, Skeleton } from '@/components/ui/Skeleton'
import { cn, formatDate, formatInr, formatInrExact, todayIso } from '@/lib/utils'
import { describeErrorText } from '@/features/admin/lib/format'
import { useSiteSettings } from '@/lib/cms'
import {
  useInvoice,
  useMembership,
  usePayInvoice,
  usePlanOptions,
  useQuote,
  useRenew,
  useRequestFreeze,
  useVerifyPayment,
  useWithdrawFreeze,
  useMyCorporate,
  useEnrolCorporate,
} from './lib/portal-api'
import { CheckoutCancelled, openCheckout } from './lib/razorpay'
import { DrawnCheck, Field, InlineNote, Panel, PortalHeading, Sheet, StatTile } from './components/ui'
import type { GatewayOrder, PlanOption } from './lib/types'

/**
 * Membership self-serve (Module 3 — Membership): what the member holds, renewing
 * or upgrading it through Razorpay, asking for a freeze, and every invoice.
 *
 * A renewal starts the day the current plan ends, so renewing early never costs
 * the member the tail of what they already paid for. An upgrade starts today and
 * the unused days come back as a credit line on the invoice.
 */
export function MembershipPage() {
  const { data, isLoading } = useMembership()
  const [renewFor, setRenewFor] = useState<{ plan: PlanOption; upgrade: boolean } | null>(null)
  const [freezeOpen, setFreezeOpen] = useState(false)
  const [invoiceId, setInvoiceId] = useState<number | null>(null)

  if (isLoading || !data) {
    return (
      <div>
        <PortalHeading eyebrow="Your plan" title="Membership" />
        <div className="grid gap-5 lg:grid-cols-3">
          <Skeleton className="h-60 lg:col-span-2" />
          <Skeleton className="h-60" />
        </div>
      </div>
    )
  }

  const current = data.current
  const elapsed = current ? Math.max(0, current.totalDays - current.daysLeft) : 0
  const progress = current ? Math.min(100, Math.round((elapsed / Math.max(1, current.totalDays)) * 100)) : 0

  return (
    <div className="space-y-8">
      <PortalHeading
        eyebrow="Your plan"
        title="Membership"
        lead="Renew, upgrade, ask for a freeze, and read every invoice the gym has ever raised against your account."
        actions={
          current ? (
            <Button variant="outline" size="sm" icon="snowflake" onClick={() => setFreezeOpen(true)}>
              Request a freeze
            </Button>
          ) : undefined
        }
      />

      {!data.gateway.isLive && data.gateway.notice && (
        <InlineNote tone="warn" icon="lock">
          {data.gateway.notice}
        </InlineNote>
      )}

      {current ? (
        <div className="grid gap-5 lg:grid-cols-3">
          <Panel className="lg:col-span-2">
            <div className="flex flex-wrap items-start justify-between gap-5">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2.5">
                  <p className="caption">{current.cycleName}</p>
                  <Badge tone={current.statusName === 'Active' ? 'success' : 'accent'}>{current.statusName}</Badge>
                  {current.autoRenew && <Badge>Auto-renews</Badge>}
                </div>
                <h2 className="display-l mt-3 text-[clamp(1.75rem,3.5vw,2.5rem)] text-bone">{current.planName}</h2>
                <p className="measure mt-2 text-[0.9375rem] leading-relaxed text-smoke">{current.planTagline}</p>
              </div>
              <div className="text-right">
                <p className="numeric display-m text-[2rem] leading-none text-accent">{current.daysLeft}</p>
                <p className="caption mt-1.5 text-[0.5625rem]">days left</p>
              </div>
            </div>

            {/* Validity bar: paid time elapsed, not a countdown to a marketing deadline. */}
            <div className="mt-6">
              <div className="h-1.5 overflow-hidden rounded-full bg-steel">
                <div
                  className="h-full rounded-full bg-accent transition-[width] duration-[600ms] ease-out"
                  style={{ width: `${progress}%` }}
                />
              </div>
              <div className="mt-2 flex justify-between text-[0.75rem] text-smoke">
                <span>{formatDate(current.startsOn)}</span>
                <span>{formatDate(current.endsOn)}</span>
              </div>
            </div>

            <dl className="mt-6 grid grid-cols-2 gap-x-5 gap-y-4 border-t border-[var(--hairline)] pt-5 text-[0.875rem] sm:grid-cols-4">
              <Detail label="Branch access" value={current.accessScopeName} />
              <Detail label="Home branch" value={current.branchName.replace('FORGE ', '')} />
              <Detail
                label="Class credits"
                value={current.classCreditsRemaining > 0 ? String(current.classCreditsRemaining) : 'Unlimited'}
              />
              <Detail
                label="Freeze days"
                value={`${current.freezeDaysAllowed - current.freezeDaysUsed} of ${current.freezeDaysAllowed} left`}
              />
              {current.accessWindow && <Detail label="Access window" value={current.accessWindow} />}
              {current.ptCreditsRemaining > 0 && (
                <Detail label="PT sessions" value={String(current.ptCreditsRemaining)} />
              )}
            </dl>

            {current.features.length > 0 && (
              <ul className="mt-6 grid gap-2.5 border-t border-[var(--hairline)] pt-5 sm:grid-cols-2">
                {current.features.map((feature) => (
                  <li key={feature} className="flex items-start gap-2.5 text-[0.875rem] text-smoke">
                    <Icon name="check" size={15} className="mt-0.5 shrink-0 text-accent" />
                    {feature}
                  </li>
                ))}
              </ul>
            )}

            {current.pendingFreezeRequest && (
              <FreezePending
                request={current.pendingFreezeRequest}
              />
            )}

            {current.freezeStartsOn && current.statusName === 'Frozen' && (
              <InlineNote tone="warn" icon="snowflake" className="mt-5">
                Frozen {formatDate(current.freezeStartsOn)} – {formatDate(current.freezeEndsOn ?? current.endsOn)}. Those
                days were added to the end of your membership. The desk can resume it early if plans change.
              </InlineNote>
            )}
          </Panel>

          <div className="space-y-5">
            <StatTile
              label="Outstanding"
              value={data.duesOutstanding > 0 ? formatInr(data.duesOutstanding) : '₹0'}
              sub={data.duesOutstanding > 0 ? 'Tap an unpaid invoice below to clear it.' : 'Everything settled.'}
              icon="clock"
              tone={data.duesOutstanding > 0 ? 'warn' : 'success'}
            />
            <StatTile
              label="Paid for this term"
              value={formatInr(current.priceCharged)}
              sub={`${current.totalDays} days · ${current.cycleName.toLowerCase()}`}
              icon="medal"
            />
            {current.nextBillingOn && (
              <StatTile
                label="Next billing"
                value={formatDate(current.nextBillingOn)}
                sub="Auto-renew is on. Turn it off at the desk any time."
                icon="calendar"
              />
            )}
          </div>
        </div>
      ) : (
        <EmptyState
          icon="medal"
          headline="No active membership"
          body="Pick a plan below. Booking, QR check-in and your training programme all unlock the moment it goes live."
        />
      )}

      <PlanPicker
        currentPlanSlug={current?.planSlug}
        hasActive={Boolean(current)}
        onPick={(plan, upgrade) => setRenewFor({ plan, upgrade })}
      />

      <Panel
        title="Invoices"
        description="Every GST invoice raised against your account, oldest at the bottom."
        padded={false}
      >
        {data.invoices.length === 0 ? (
          <div className="p-5">
            <EmptyState icon="clock" headline="No invoices yet" body="They appear here the moment a plan is sold." />
          </div>
        ) : (
          <ul className="divide-y divide-[var(--hairline)]">
            {data.invoices.map((invoice) => (
              <li key={invoice.id}>
                <button
                  type="button"
                  onClick={() => setInvoiceId(invoice.id)}
                  className="flex w-full flex-wrap items-center justify-between gap-4 px-5 py-4 text-left transition-colors hover:bg-steel/40"
                >
                  <div className="min-w-0">
                    <p className="numeric text-[0.9375rem] text-bone">{invoice.invoiceNumber}</p>
                    <p className="mt-1 truncate text-[0.8125rem] text-smoke">
                      {invoice.description ?? 'Membership'} · issued {formatDate(invoice.issuedOn)}
                    </p>
                  </div>
                  <div className="flex items-center gap-4">
                    <div className="text-right">
                      <p className="numeric text-[0.9375rem] text-bone">{formatInr(invoice.grandTotal)}</p>
                      {invoice.amountDue > 0 && (
                        <p className="numeric mt-0.5 text-[0.75rem] text-accent-hot">
                          {formatInr(invoice.amountDue)} due
                        </p>
                      )}
                    </div>
                    <Badge
                      tone={
                        invoice.statusName === 'Paid'
                          ? 'success'
                          : invoice.statusName === 'Overdue'
                            ? 'hot'
                            : invoice.amountDue > 0
                              ? 'accent'
                              : 'neutral'
                      }
                    >
                      {invoice.statusName}
                    </Badge>
                    <Icon name="chevron-right" size={16} className="text-smoke" />
                  </div>
                </button>
              </li>
            ))}
          </ul>
        )}
      </Panel>

      <CorporateCard />

      {data.history.length > 1 && (
        <Panel title="Membership history" padded={false}>
          <ul className="divide-y divide-[var(--hairline)]">
            {data.history.map((row) => (
              <li key={row.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-3.5">
                <div className="min-w-0">
                  <p className="text-[0.875rem] text-bone">{row.planName}</p>
                  <p className="mt-0.5 text-[0.75rem] text-smoke">
                    {formatDate(row.startsOn)} – {formatDate(row.endsOn)} · {row.branchName.replace('FORGE ', '')}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  <span className="numeric text-[0.8125rem] text-smoke">{formatInr(row.priceCharged)}</span>
                  <Badge tone={row.statusName === 'Active' ? 'success' : 'neutral'}>{row.statusName}</Badge>
                </div>
              </li>
            ))}
          </ul>
        </Panel>
      )}

      {renewFor && (
        <CheckoutSheet
          plan={renewFor.plan}
          upgrade={renewFor.upgrade}
          onClose={() => setRenewFor(null)}
        />
      )}

      {freezeOpen && current && (
        <FreezeSheet
          subscriptionId={current.subscriptionId}
          daysLeft={current.freezeDaysAllowed - current.freezeDaysUsed}
          endsOn={current.endsOn}
          onClose={() => setFreezeOpen(false)}
        />
      )}

      {invoiceId !== null && <InvoiceSheet id={invoiceId} onClose={() => setInvoiceId(null)} />}
    </div>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <dt className="caption text-[0.5625rem]">{label}</dt>
      <dd className="mt-1 truncate text-bone">{value}</dd>
    </div>
  )
}

function FreezePending({ request }: { request: { id: number; requestedFrom: string; requestedTo: string; days: number } }) {
  const withdraw = useWithdrawFreeze()
  return (
    <div className="mt-5 flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-card)] border border-[var(--accent-line)] bg-[var(--accent-soft)] px-4 py-3.5">
      <p className="text-[0.8125rem] leading-relaxed text-accent">
        Freeze requested for {request.days} days from {formatDate(request.requestedFrom)}. The desk will confirm.
      </p>
      <Button variant="ghost" size="sm" loading={withdraw.isPending} onClick={() => withdraw.mutate(request.id)}>
        Withdraw
      </Button>
    </div>
  )
}

/* ---------------------------------------------------------------- plans */

function PlanPicker({
  currentPlanSlug,
  hasActive,
  onPick,
}: {
  currentPlanSlug?: string
  hasActive: boolean
  onPick: (plan: PlanOption, upgrade: boolean) => void
}) {
  const { data, isLoading } = usePlanOptions()

  if (isLoading) {
    return (
      <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
        {Array.from({ length: 3 }).map((_, index) => (
          <Skeleton key={index} className="h-72" />
        ))}
      </div>
    )
  }

  const plans = data ?? []
  if (plans.length === 0) return null

  return (
    <section>
      <h2 className="display-m mb-5 text-[1.375rem] text-bone">
        {hasActive ? 'Renew or move up' : 'Choose a plan'}
      </h2>
      <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
        {plans.map((plan) => {
          const isCurrent = plan.slug === currentPlanSlug
          return (
            <article
              key={plan.id}
              className={cn(
                'flex flex-col rounded-[var(--radius-card)] border p-5',
                plan.isMostPopular ? 'border-accent bg-[color-mix(in_srgb,var(--accent)_5%,var(--carbon))]' : 'border-[var(--hairline)] bg-carbon',
              )}
            >
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <h3 className="display-m text-[1.125rem] text-bone">{plan.name}</h3>
                  <p className="mt-1.5 text-[0.8125rem] leading-relaxed text-smoke">{plan.tagline}</p>
                </div>
                {plan.isMostPopular && <Badge tone="accent">Popular</Badge>}
              </div>

              <p className="numeric display-m mt-5 text-[1.75rem] text-bone">
                {formatInr(plan.price)}
                <span className="ml-1.5 text-[0.8125rem] font-normal text-smoke">/ {plan.cycleName.toLowerCase()}</span>
              </p>
              {plan.price < plan.listPrice && (
                <p className="numeric mt-1 text-[0.75rem] text-smoke line-through">{formatInr(plan.listPrice)}</p>
              )}
              {plan.trustMicrocopy && <p className="mt-2 text-[0.75rem] text-smoke/85">{plan.trustMicrocopy}</p>}

              <ul className="mt-5 flex-1 space-y-2 border-t border-[var(--hairline)] pt-5">
                {plan.features.slice(0, 5).map((feature) => (
                  <li key={feature} className="flex items-start gap-2 text-[0.8125rem] text-smoke">
                    <Icon name="check" size={14} className="mt-0.5 shrink-0 text-accent" />
                    {feature}
                  </li>
                ))}
              </ul>

              <div className="mt-5 flex flex-wrap gap-2">
                <Button size="sm" onClick={() => onPick(plan, false)} magnetic={plan.isMostPopular}>
                  {isCurrent ? 'Renew this' : hasActive ? 'Switch at renewal' : 'Buy this plan'}
                </Button>
                {hasActive && !isCurrent && (
                  <Button size="sm" variant="ghost" onClick={() => onPick(plan, true)}>
                    Upgrade today
                  </Button>
                )}
              </div>
            </article>
          )
        })}
      </div>
    </section>
  )
}

/* ---------------------------------------------------------------- checkout */

function CheckoutSheet({ plan, upgrade, onClose }: { plan: PlanOption; upgrade: boolean; onClose: () => void }) {
  const { data: settings } = useSiteSettings()
  const [coupon, setCoupon] = useState('')
  const [applied, setApplied] = useState<string | undefined>(undefined)
  const [stage, setStage] = useState<'review' | 'paying' | 'done'>('review')
  const [error, setError] = useState<string | null>(null)
  const [receipt, setReceipt] = useState<{ invoiceNumber: string; endsOn: string; simulated: boolean } | null>(null)

  const quote = useQuote({ planId: plan.id, couponCode: applied, upgradeNow: upgrade })
  const renew = useRenew()
  const verify = useVerifyPayment()

  const brand = settings?.values['brand.name'] ?? 'FORGE'

  async function pay() {
    setError(null)
    setStage('paying')
    try {
      const checkout = await renew.mutateAsync({
        planId: plan.id,
        couponCode: applied,
        upgradeNow: upgrade,
        autoRenew: false,
      })

      if (!checkout.order) {
        // Nothing to collect (a full-credit upgrade, or a gateway we could not reach).
        setReceipt({ invoiceNumber: checkout.invoiceNumber, endsOn: checkout.endsOn, simulated: false })
        setStage('done')
        return
      }

      const order: GatewayOrder = checkout.order
      const outcome = await openCheckout(order, { brandName: brand, description: plan.name })
      await verify.mutateAsync({
        orderId: outcome.orderId,
        paymentId: outcome.paymentId,
        signature: outcome.signature,
      })

      setReceipt({ invoiceNumber: checkout.invoiceNumber, endsOn: checkout.endsOn, simulated: outcome.simulated })
      setStage('done')
    } catch (failure) {
      if (failure instanceof CheckoutCancelled) {
        setError('Payment window closed. Your invoice is saved — pay it any time from the invoice list.')
      } else {
        setError(describeErrorText(failure, 'The payment did not go through.'))
      }
      setStage('review')
    }
  }

  return (
    <Sheet
      open
      onClose={onClose}
      title={stage === 'done' ? 'You are set' : upgrade ? `Upgrade to ${plan.name}` : plan.name}
      description={
        stage === 'done'
          ? undefined
          : upgrade
            ? 'Starts today. The unused days on your current plan come back as a credit on this invoice.'
            : 'Starts the day your current plan ends, so you keep every day you have already paid for.'
      }
      footer={
        stage === 'done' ? (
          <Button onClick={onClose}>Done</Button>
        ) : (
          <>
            <Button variant="ghost" onClick={onClose}>
              Not now
            </Button>
            <Button
              loading={stage === 'paying' || renew.isPending || verify.isPending}
              disabled={quote.isLoading || quote.isError}
              onClick={() => void pay()}
              magnetic
            >
              {quote.data ? `Pay ${formatInr(quote.data.payable)}` : 'Pay'}
            </Button>
          </>
        )
      }
    >
      {stage === 'done' && receipt ? (
        <div className="flex flex-col items-center py-6 text-center">
          <DrawnCheck size={64} />
          <h3 className="display-m mt-5 text-[1.375rem] text-bone">Membership active</h3>
          <p className="measure mt-2.5 text-[0.9375rem] leading-relaxed text-smoke">
            Invoice {receipt.invoiceNumber}. Your plan now runs to {formatDate(receipt.endsOn)}. Booking and your QR
            are live immediately.
          </p>
          {receipt.simulated && (
            <InlineNote tone="warn" icon="lock" className="mt-5 text-left">
              Recorded in sandbox simulation — no money moved. Every payment made this way is stamped as simulated on
              the invoice.
            </InlineNote>
          )}
        </div>
      ) : quote.isLoading ? (
        <Skeleton className="h-52" />
      ) : quote.isError ? (
        <InlineNote tone="danger" icon="x">
          {describeErrorText(quote.error, 'We could not price that plan right now.')}
        </InlineNote>
      ) : quote.data ? (
        <div className="space-y-5">
          <dl className="space-y-2.5 text-[0.9375rem]">
            <Line label={`${plan.name} · ${plan.durationDays} days`} value={formatInrExact(quote.data.listPrice)} />
            {quote.data.admissionFee > 0 && (
              <Line label="One-time admission fee" value={formatInrExact(quote.data.admissionFee)} />
            )}
            {quote.data.discountAmount > 0 && (
              <Line
                label={`Coupon ${quote.data.couponCode ?? ''}`}
                value={`− ${formatInrExact(quote.data.discountAmount)}`}
                tone="success"
              />
            )}
            {quote.data.prorationCredit > 0 && (
              <Line
                label="Credit for unused days"
                value={`− ${formatInrExact(quote.data.prorationCredit)}`}
                tone="success"
              />
            )}
            <div className="flex items-center justify-between border-t border-[var(--hairline)] pt-3">
              <dt className="text-[0.9375rem] font-medium text-bone">To pay</dt>
              <dd className="numeric display-m text-[1.5rem] text-accent">{formatInr(quote.data.payable)}</dd>
            </div>
            <p className="text-[0.75rem] text-smoke">
              Inclusive of {quote.data.gstRatePercent}% GST. Runs {formatDate(quote.data.startsOn)} –{' '}
              {formatDate(quote.data.endsOn)} at {quote.data.branchName}.
            </p>
          </dl>

          <div className="border-t border-[var(--hairline)] pt-5">
            <Field label="Coupon code" hint={quote.data.couponMessage ?? 'Optional. Applied before GST.'}>
              <div className="flex gap-2">
                <input
                  className="field-input"
                  value={coupon}
                  onChange={(event) => setCoupon(event.target.value.toUpperCase())}
                  placeholder="FORGE500"
                  aria-label="Coupon code"
                />
                <Button variant="outline" size="sm" onClick={() => setApplied(coupon.trim() || undefined)}>
                  Apply
                </Button>
              </div>
            </Field>
            {quote.data.couponMessage && (
              <InlineNote tone="warn" icon="x" className="mt-3">
                {quote.data.couponMessage}
              </InlineNote>
            )}
          </div>

          {error && (
            <InlineNote tone="danger" icon="x">
              {error}
            </InlineNote>
          )}
        </div>
      ) : null}
    </Sheet>
  )
}

function Line({ label, value, tone }: { label: string; value: string; tone?: 'success' }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="text-smoke">{label}</dt>
      <dd className={cn('numeric', tone === 'success' ? 'text-success' : 'text-bone')}>{value}</dd>
    </div>
  )
}

/* ---------------------------------------------------------------- freeze */

function FreezeSheet({
  subscriptionId,
  daysLeft,
  endsOn,
  onClose,
}: {
  subscriptionId: number
  daysLeft: number
  endsOn: string
  onClose: () => void
}) {
  const [from, setFrom] = useState(todayIso(1))
  const [to, setTo] = useState(todayIso(15))
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)
  const request = useRequestFreeze()

  const days = Math.max(0, Math.round((new Date(to).getTime() - new Date(from).getTime()) / 86_400_000))
  const overAllowance = days > daysLeft

  return (
    <Sheet
      open
      onClose={onClose}
      title="Request a freeze"
      description="The desk approves freezes. Every frozen day is added to the end of your membership, so you lose nothing."
      footer={
        done ? (
          <Button onClick={onClose}>Done</Button>
        ) : (
          <>
            <Button variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button
              loading={request.isPending}
              disabled={days < 1 || overAllowance || reason.trim().length < 3}
              onClick={() => {
                setError(null)
                request.mutate(
                  { subscriptionId, from, to, reason: reason.trim() },
                  {
                    onSuccess: () => setDone(true),
                    onError: (failure) => setError(describeErrorText(failure)),
                  },
                )
              }}
            >
              Send request
            </Button>
          </>
        )
      }
    >
      {done ? (
        <div className="flex flex-col items-center py-6 text-center">
          <DrawnCheck size={56} />
          <h3 className="display-m mt-5 text-[1.25rem] text-bone">Request sent</h3>
          <p className="measure mt-2 text-[0.9375rem] leading-relaxed text-smoke">
            The desk sees it now. You will get a notification the moment it is answered, and you can withdraw it until
            then.
          </p>
        </div>
      ) : (
        <div className="space-y-5">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="From">
              <input
                type="date"
                className="field-input"
                value={from}
                min={todayIso()}
                max={endsOn}
                onChange={(event) => setFrom(event.target.value)}
              />
            </Field>
            <Field label="To">
              <input
                type="date"
                className="field-input"
                value={to}
                min={from}
                onChange={(event) => setTo(event.target.value)}
              />
            </Field>
          </div>

          <InlineNote tone={overAllowance ? 'danger' : 'neutral'} icon="snowflake">
            {days} day{days === 1 ? '' : 's'} requested · {daysLeft} left on your plan's allowance.
            {overAllowance && ' Shorten the window to fit.'}
          </InlineNote>

          <Field label="Why" hint="Travel, injury, work — one line is enough, and it is what the desk acts on.">
            <textarea
              className="field-input"
              rows={3}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Away for work until the end of the month."
            />
          </Field>

          {error && (
            <InlineNote tone="danger" icon="x">
              {error}
            </InlineNote>
          )}
        </div>
      )}
    </Sheet>
  )
}

/* ---------------------------------------------------------------- invoice */

function InvoiceSheet({ id, onClose }: { id: number; onClose: () => void }) {
  const { data, isLoading } = useInvoice(id)
  const pay = usePayInvoice()
  const verify = useVerifyPayment()
  const { data: settings } = useSiteSettings()
  const [error, setError] = useState<string | null>(null)
  const [paid, setPaid] = useState(false)

  async function settle() {
    setError(null)
    try {
      const order = await pay.mutateAsync(id)
      const outcome = await openCheckout(order, {
        brandName: settings?.values['brand.name'] ?? 'FORGE',
        description: data?.invoiceNumber ?? 'Membership',
      })
      await verify.mutateAsync({
        orderId: outcome.orderId,
        paymentId: outcome.paymentId,
        signature: outcome.signature,
      })
      setPaid(true)
    } catch (failure) {
      if (failure instanceof CheckoutCancelled) setError('Payment window closed. Nothing was charged.')
      else setError(describeErrorText(failure))
    }
  }

  return (
    <Sheet
      open
      onClose={onClose}
      title={data?.invoiceNumber ?? 'Invoice'}
      description={data ? `${data.branchName} · issued ${formatDate(data.issuedOn)}` : undefined}
      width="lg"
      footer={
        data && data.amountDue > 0 && !paid ? (
          <>
            <Button variant="ghost" onClick={onClose}>
              Close
            </Button>
            <Button loading={pay.isPending || verify.isPending} onClick={() => void settle()}>
              Pay {formatInr(data.amountDue)}
            </Button>
          </>
        ) : (
          <Button variant="ghost" onClick={onClose}>
            Close
          </Button>
        )
      }
    >
      {isLoading || !data ? (
        <Skeleton className="h-64" />
      ) : (
        <div className="space-y-5">
          {paid && (
            <InlineNote tone="success" icon="check">
              Payment recorded. The receipt sits under this invoice.
            </InlineNote>
          )}
          {error && (
            <InlineNote tone="danger" icon="x">
              {error}
            </InlineNote>
          )}

          <div className="overflow-x-auto">
            <table className="w-full min-w-[34rem] text-left text-[0.875rem]">
              <thead>
                <tr className="border-b border-[var(--hairline)] text-smoke">
                  <th className="py-2 font-normal">Description</th>
                  <th className="py-2 text-right font-normal">Taxable</th>
                  <th className="py-2 text-right font-normal">GST</th>
                  <th className="py-2 text-right font-normal">Total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--hairline)]">
                {data.lines.map((line, index) => (
                  <tr key={index}>
                    <td className="py-3 pr-4 text-bone">
                      {line.description}
                      {line.sacOrHsnCode && (
                        <span className="numeric ml-2 text-[0.6875rem] text-smoke">SAC {line.sacOrHsnCode}</span>
                      )}
                    </td>
                    <td className="numeric py-3 text-right text-smoke">{formatInrExact(line.taxableValue)}</td>
                    <td className="numeric py-3 text-right text-smoke">{line.gstRatePercent}%</td>
                    <td className="numeric py-3 text-right text-bone">{formatInrExact(line.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <dl className="ml-auto max-w-xs space-y-2 border-t border-[var(--hairline)] pt-4 text-[0.875rem]">
            <Line label="Taxable value" value={formatInrExact(data.taxableValue)} />
            {data.cgstAmount > 0 && <Line label="CGST" value={formatInrExact(data.cgstAmount)} />}
            {data.sgstAmount > 0 && <Line label="SGST" value={formatInrExact(data.sgstAmount)} />}
            {data.igstAmount > 0 && <Line label="IGST" value={formatInrExact(data.igstAmount)} />}
            {data.roundOff !== 0 && <Line label="Round off" value={formatInrExact(data.roundOff)} />}
            <div className="flex items-baseline justify-between border-t border-[var(--hairline)] pt-2">
              <dt className="font-medium text-bone">Total</dt>
              <dd className="numeric display-m text-[1.25rem] text-bone">{formatInr(data.grandTotal)}</dd>
            </div>
            {data.amountDue > 0 && <Line label="Outstanding" value={formatInr(data.amountDue)} />}
          </dl>

          {data.payments.length > 0 && (
            <div className="border-t border-[var(--hairline)] pt-4">
              <h3 className="caption mb-3">Payments received</h3>
              <ul className="space-y-2 text-[0.8125rem]">
                {data.payments.map((payment) => (
                  <li key={payment.id} className="flex flex-wrap items-center justify-between gap-2">
                    <span className="text-smoke">
                      {payment.modeName} · {formatDate(payment.paidAtUtc)}
                      {payment.reference ? ` · ${payment.reference}` : ''}
                    </span>
                    <span className="numeric text-bone">{formatInrExact(payment.amount)}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {data.supplierGstin && (
            <p className="text-[0.75rem] text-smoke/80">
              GSTIN {data.supplierGstin} · place of supply {data.placeOfSupply}
            </p>
          )}
        </div>
      )}
    </Sheet>
  )
}

/**
 * Corporate self-enrolment (Module 4.6). An employee types the code their HR team gave them
 * and the benefit applies from their next renewal — the desk never has to key them in.
 */
function CorporateCard() {
  const { data } = useMyCorporate()
  const enrol = useEnrolCorporate()
  const [code, setCode] = useState('')
  const [workEmail, setWorkEmail] = useState('')
  const [employeeId, setEmployeeId] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)

  if (data?.enrolled) {
    return (
      <Panel title="Corporate membership">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="min-w-0">
            <p className="text-[0.9375rem] text-bone">{data.companyName}</p>
            <p className="mt-1 text-body-s text-smoke">
              {data.discountPercent}% off every renewal
              {data.waiveAdmissionFee && ' · no joining fee'} · runs to {formatDate(data.validTo)}
            </p>
          </div>
          <Badge tone="success">Enrolled</Badge>
        </div>
      </Panel>
    )
  }

  const submit = () => {
    setMessage(null)
    setFailed(false)
    enrol.mutate(
      { code: code.trim(), workEmail: workEmail.trim() || undefined, employeeId: employeeId.trim() || undefined },
      {
        onSuccess: (result) => setMessage(result.message ?? 'You are on the programme.'),
        onError: (error) => {
          setFailed(true)
          const detail = (error as { response?: { data?: { message?: string } } })?.response?.data?.message
          setMessage(detail ?? describeErrorText(error))
        },
      },
    )
  }

  return (
    <Panel
      title="Company code"
      description="If your employer has an agreement with us, enter the code your HR team gave you."
    >
      <div className="grid gap-4 sm:grid-cols-3">
        <Field label="Company code">
          <input
            className="auth-input"
            value={code}
            onChange={(event) => setCode(event.target.value.toUpperCase())}
            placeholder="ACME26"
            autoComplete="off"
          />
        </Field>
        <Field label="Work email" hint="Only if your company checks it">
          <input
            className="auth-input"
            type="email"
            value={workEmail}
            onChange={(event) => setWorkEmail(event.target.value)}
            placeholder="you@company.in"
          />
        </Field>
        <Field label="Employee id" hint="Optional">
          <input
            className="auth-input"
            value={employeeId}
            onChange={(event) => setEmployeeId(event.target.value)}
          />
        </Field>
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-3">
        <Button size="sm" onClick={submit} disabled={enrol.isPending || code.trim().length < 3}>
          {enrol.isPending ? 'Checking…' : 'Apply code'}
        </Button>
        {message && (
          <p
            className={cn('text-body-s', failed ? 'text-[var(--accent-hot)]' : 'text-[var(--success)]')}
            role="status"
          >
            {message}
          </p>
        )}
      </div>
    </Panel>
  )
}

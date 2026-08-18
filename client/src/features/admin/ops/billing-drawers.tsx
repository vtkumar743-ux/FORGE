import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { useSiteSettings } from '@/lib/cms'
import { Drawer, useToast } from '../components/overlays'
import { Hint, InlineError, SelectField, TextAreaField, TextField, Toggle } from '../components/ui'
import { useBillingActions, useGateway, usePlans, useQuote } from '../lib/admin-api'
import { describeErrorText, formatInr, istToday } from '../lib/format'
import { paymentModeNames } from '../lib/types'

/**
 * Selling a plan at the desk. The quote re-prices live against the branch override, the
 * coupon and any unused days on a plan being upgraded from, so what the owner reads out
 * loud is exactly what the invoice will say.
 */
export function SellPlanDrawer({
  open,
  onClose,
  memberId,
  memberName,
  defaultBranchId,
  upgradeFromSubscriptionId,
  onSold,
}: {
  open: boolean
  onClose: () => void
  memberId: number
  memberName: string
  defaultBranchId?: number
  upgradeFromSubscriptionId?: number
  onSold?: (result: { invoiceId: number; invoiceNumber: string }) => void
}) {
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const { data: plans } = usePlans()
  const actions = useBillingActions()

  const [planId, setPlanId] = useState<number | undefined>()
  const [branchId, setBranchId] = useState<number | undefined>(defaultBranchId)
  const [startsOn, setStartsOn] = useState(istToday())
  const [couponCode, setCouponCode] = useState('')
  const [autoRenew, setAutoRenew] = useState(false)
  const [dueInDays, setDueInDays] = useState(0)
  const [collectNow, setCollectNow] = useState(true)
  const [collectMode, setCollectMode] = useState('1')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data: quote, isFetching: quoting } = useQuote({
    memberId,
    planId,
    branchId,
    startsOn,
    couponCode: couponCode.trim() || undefined,
    upgradeFromSubscriptionId,
  })

  const sellable = (plans ?? []).filter((plan) => plan.isActive)

  async function submit() {
    if (!planId || !branchId) return setError('Pick a plan and a branch.')
    setError(null)

    try {
      const result = await actions.sell.mutateAsync({
        memberId,
        planId,
        branchId,
        startsOn,
        couponCode: couponCode.trim() || undefined,
        upgradeFromSubscriptionId,
        autoRenew,
        dueInDays,
        notes: notes || undefined,
        collectMode: collectNow ? Number(collectMode) : undefined,
      })
      toast.success('Membership sold', `${result.invoiceNumber} · ${formatInr(result.grandTotal)}`)
      onSold?.({ invoiceId: result.invoiceId, invoiceNumber: result.invoiceNumber })
      onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={upgradeFromSubscriptionId ? 'Upgrade membership' : 'Sell a membership'}
      description={`For ${memberName}. A GST invoice is raised the moment this is confirmed.`}
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button size="sm" icon="check" onClick={() => void submit()} loading={actions.sell.isPending}>
            {collectNow ? 'Sell and collect' : 'Raise invoice'}
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {error && <InlineError>{error}</InlineError>}

        <div className="grid gap-4 sm:grid-cols-2">
          <SelectField
            label="Plan"
            required
            value={planId ? String(planId) : ''}
            onChange={(event) => setPlanId(event.target.value ? Number(event.target.value) : undefined)}
          >
            <option value="">Choose a plan</option>
            {sellable.map((plan) => (
              <option key={plan.id} value={plan.id}>
                {plan.name} — {formatInr(plan.basePrice)} / {plan.durationDays}d
              </option>
            ))}
          </SelectField>

          <SelectField
            label="Branch"
            required
            hint="Branch overrides beat the list price."
            value={branchId ? String(branchId) : ''}
            onChange={(event) => setBranchId(event.target.value ? Number(event.target.value) : undefined)}
          >
            <option value="">Choose a branch</option>
            {(settings?.branches ?? []).map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </SelectField>

          <TextField label="Starts on" type="date" value={startsOn} onChange={(event) => setStartsOn(event.target.value)} />
          <TextField
            label="Coupon code"
            placeholder="Optional"
            value={couponCode}
            onChange={(event) => setCouponCode(event.target.value.toUpperCase())}
          />
        </div>

        {/* ---- live quote ---- */}
        <div className="rounded-[var(--radius-card)] border border-[var(--hairline)] p-5">
          <div className="mb-3 flex items-center justify-between">
            <p className="caption">What they pay</p>
            {quoting && <Icon name="loader" size={14} className="animate-spin text-smoke" />}
          </div>

          {!quote ? (
            <p className="text-[0.875rem] text-smoke">Pick a plan and a branch to see the price.</p>
          ) : (
            <dl className="space-y-2 text-[0.875rem]">
              <Row label="Plan price" value={formatInr(quote.listPrice)} />
              {quote.admissionFee > 0 && <Row label="Admission fee (first membership)" value={formatInr(quote.admissionFee)} />}
              {quote.discountAmount > 0 && (
                <Row label={`Coupon ${quote.couponCode ?? ''}`} value={`− ${formatInr(quote.discountAmount)}`} tone="success" />
              )}
              {quote.prorationCredit > 0 && (
                <Row label="Credit for unused days" value={`− ${formatInr(quote.prorationCredit)}`} tone="success" />
              )}
              <div className="my-2 border-t border-[var(--hairline)]" />
              <Row label={`Taxable value`} value={formatInr(quote.tax.taxableValue)} muted />
              <Row label={`CGST @ ${quote.tax.rate / 2}%`} value={formatInr(quote.tax.cgst)} muted />
              <Row label={`SGST @ ${quote.tax.rate / 2}%`} value={formatInr(quote.tax.sgst)} muted />
              <div className="my-2 border-t border-[var(--hairline)]" />
              <div className="flex items-baseline justify-between">
                <dt className="font-medium">Payable</dt>
                <dd className="numeric display-m text-[1.5rem]">{formatInr(quote.payable)}</dd>
              </div>
              <p className="text-[0.75rem] text-smoke">
                Valid {quote.startsOn} → {quote.endsOn}
              </p>
              {quote.couponMessage && (
                <p className="text-[0.75rem] text-accent-hot">Coupon not applied: {quote.couponMessage}</p>
              )}
            </dl>
          )}
        </div>

        <div className="space-y-3 rounded-[0.625rem] border border-[var(--hairline)] p-4">
          <Toggle
            label="Collect payment now"
            hint="Off raises the invoice with a due date and lets dunning chase it."
            checked={collectNow}
            onChange={setCollectNow}
          />
          {collectNow ? (
            <SelectField label="Payment mode" value={collectMode} onChange={(event) => setCollectMode(event.target.value)}>
              {paymentModeNames.map((name, index) => (
                <option key={name} value={index}>
                  {name === 'RazorpayLink' ? 'Razorpay link' : name === 'Upi' ? 'UPI' : name}
                </option>
              ))}
            </SelectField>
          ) : (
            <TextField
              label="Payment due in (days)"
              type="number"
              min={0}
              max={90}
              value={String(dueInDays)}
              onChange={(event) => setDueInDays(Number(event.target.value))}
            />
          )}
          <Toggle
            label="Auto-renew"
            hint="Flags the subscription for renewal; the mandate is set up separately."
            checked={autoRenew}
            onChange={setAutoRenew}
          />
        </div>

        <TextAreaField
          label="Invoice note"
          rows={2}
          placeholder="Anything that should print on the invoice."
          value={notes}
          onChange={(event) => setNotes(event.target.value)}
        />
      </div>
    </Drawer>
  )
}

function Row({
  label,
  value,
  tone,
  muted,
}: {
  label: string
  value: string
  tone?: 'success'
  muted?: boolean
}) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className={muted ? 'text-[0.8125rem] text-smoke' : 'text-smoke'}>{label}</dt>
      <dd
        className={
          tone === 'success'
            ? 'numeric text-success'
            : muted
              ? 'numeric text-[0.8125rem] text-smoke'
              : 'numeric'
        }
      >
        {value}
      </dd>
    </div>
  )
}

/* ---------------------------------------------------------------- payment */

/**
 * Recording money against an invoice. Cash and UPI at the desk are one field; Razorpay
 * creates a real order and settles it through the same verification path the webhook uses,
 * so a demo collection and a live one leave identical audit rows.
 */
export function RecordPaymentDrawer({
  open,
  onClose,
  invoiceId,
  invoiceNumber,
  amountDue,
  memberName,
}: {
  open: boolean
  onClose: () => void
  invoiceId: number
  invoiceNumber: string
  amountDue: number
  memberName: string
}) {
  const toast = useToast()
  const actions = useBillingActions()
  const { data: gateway } = useGateway()

  const [amount, setAmount] = useState(String(amountDue))
  const [mode, setMode] = useState('0')
  const [reference, setReference] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [order, setOrder] = useState<{ orderId: string; isSimulated: boolean; notice?: string | null } | null>(null)

  const isCheque = mode === '4'
  const isGateway = mode === '5'

  async function submitManual() {
    setError(null)
    try {
      await actions.recordPayment.mutateAsync({
        invoiceId,
        amount: Number(amount),
        mode: Number(mode),
        chequeNumber: isCheque ? reference : undefined,
        bankReference: !isCheque && reference ? reference : undefined,
        notes: notes || undefined,
        // Same invoice + same amount from a double-click must not credit twice.
        idempotencyKey: `desk-${invoiceId}-${amount}-${mode}`,
      })
      toast.success('Payment recorded', `${formatInr(Number(amount))} against ${invoiceNumber}`)
      onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  async function startOrder() {
    setError(null)
    try {
      const created = await actions.createOrder.mutateAsync({ invoiceId, amount: Number(amount) })
      setOrder({ orderId: created.orderId, isSimulated: created.isSimulated, notice: created.notice })
      toast.info('Razorpay order created', created.orderId)
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  async function settleOrder() {
    if (!order) return
    setError(null)
    try {
      await actions.verifyPayment.mutateAsync({
        invoiceId,
        razorpayOrderId: order.orderId,
        // In a live setup these come back from the checkout widget's handler.
        razorpayPaymentId: `pay_${order.orderId.slice(-12)}`,
        razorpaySignature: '',
      })
      toast.success('Payment captured', invoiceNumber)
      onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Record a payment"
      description={`${invoiceNumber} · ${memberName} · ${formatInr(amountDue)} outstanding`}
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          {isGateway ? (
            order ? (
              <Button size="sm" icon="check" onClick={() => void settleOrder()} loading={actions.verifyPayment.isPending}>
                Mark as captured
              </Button>
            ) : (
              <Button size="sm" icon="qr" onClick={() => void startOrder()} loading={actions.createOrder.isPending}>
                Create Razorpay order
              </Button>
            )
          ) : (
            <Button size="sm" icon="check" onClick={() => void submitManual()} loading={actions.recordPayment.isPending}>
              Record payment
            </Button>
          )}
        </>
      }
    >
      <div className="space-y-5">
        {error && <InlineError>{error}</InlineError>}

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label="Amount"
            type="number"
            step="0.01"
            required
            addon="₹"
            hint={`Partial payments are allowed; ${formatInr(amountDue)} is outstanding.`}
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
          />
          <SelectField label="Mode" value={mode} onChange={(event) => setMode(event.target.value)}>
            {paymentModeNames.map((name, index) => (
              <option key={name} value={index}>
                {name === 'RazorpayLink' ? 'Razorpay (online)' : name === 'Upi' ? 'UPI' : name}
              </option>
            ))}
          </SelectField>
        </div>

        {isGateway ? (
          <>
            {gateway && !gateway.isLive && (
              <Hint icon="lock">
                {gateway.notice ??
                  'Razorpay keys are not configured. The order is simulated and the payment will be stamped as such.'}
              </Hint>
            )}
            {order && (
              <div className="rounded-[0.625rem] border border-[var(--accent-line)] bg-[var(--accent-soft)] p-4">
                <p className="text-[0.8125rem] font-medium">Order {order.orderId}</p>
                <p className="mt-1 text-[0.75rem] leading-relaxed text-smoke">
                  {order.isSimulated
                    ? 'Simulated sandbox order. Marking it captured writes a payment row flagged as simulated.'
                    : 'Send the member to checkout, or wait for the webhook to settle it automatically.'}
                </p>
              </div>
            )}
          </>
        ) : (
          <TextField
            label={isCheque ? 'Cheque number' : 'Reference'}
            placeholder={isCheque ? '123456' : 'UTR / transaction id'}
            value={reference}
            onChange={(event) => setReference(event.target.value)}
          />
        )}

        <TextAreaField
          label="Notes"
          rows={2}
          value={notes}
          onChange={(event) => setNotes(event.target.value)}
        />
      </div>
    </Drawer>
  )
}

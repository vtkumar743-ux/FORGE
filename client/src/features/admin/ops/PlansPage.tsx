import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { useSiteSettings } from '@/lib/cms'
import {
  useCouponMutations,
  useCoupons,
  usePlanMutations,
  usePlans,
} from '../lib/admin-api'
import { describeErrorText, formatInr, formatIsoDate, istToday } from '../lib/format'
import { billingCycleNames, planKindNames, type CouponRow, type PlanRow } from '../lib/types'
import { ConfirmDialog, Drawer, useToast } from '../components/overlays'
import {
  DataTable,
  FilterChip,
  Hint,
  InlineError,
  PageHeader,
  Panel,
  Pill,
  SelectField,
  TextAreaField,
  TextField,
  Toggle,
} from '../components/ui'

/**
 * The plan catalogue and the coupon campaigns behind the public pricing page. Both write
 * straight through to what the website quotes, which is why the branch-override table sits
 * inside the plan editor rather than on a separate screen — a price the owner cannot see
 * next to the plan is a price they will forget to change.
 */
export function PlansPage() {
  const [tab, setTab] = useState<'plans' | 'coupons'>('plans')

  return (
    <>
      <PageHeader
        eyebrow="Money"
        title="Plans & coupons"
        lead="What the pricing page quotes, per branch, with the seasonal offers that override it."
      >
        <div className="flex gap-2">
          <FilterChip active={tab === 'plans'} onClick={() => setTab('plans')}>
            Plan catalogue
          </FilterChip>
          <FilterChip active={tab === 'coupons'} onClick={() => setTab('coupons')}>
            Coupons & offers
          </FilterChip>
        </div>
      </PageHeader>

      {tab === 'plans' ? <PlansTab /> : <CouponsTab />}
    </>
  )
}

/* ---------------------------------------------------------------- plans */

function PlansTab() {
  const { data: plans, isLoading } = usePlans()
  const [editing, setEditing] = useState<PlanRow | 'new' | null>(null)

  return (
    <>
      <Panel
        padded={false}
        title="Plan catalogue"
        description="Retired plans keep working for the members who hold them; only the website stops offering them."
        actions={
          <Button size="sm" icon="plus" onClick={() => setEditing('new')}>
            New plan
          </Button>
        }
      >
        <DataTable
          rows={plans ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          onRowClick={(row) => setEditing(row)}
          emptyHeadline="No plans yet"
          emptyBody="A plan is what the pricing page sells and what an invoice bills against."
          columns={[
            {
              key: 'name',
              header: 'Plan',
              cell: (row) => (
                <div>
                  <p className="flex items-center gap-2 font-medium">
                    {row.name}
                    {row.isMostPopular && <Pill tone="accent">popular</Pill>}
                  </p>
                  <p className="truncate text-[0.75rem] text-smoke">{row.tagline}</p>
                </div>
              ),
            },
            {
              key: 'kind',
              header: 'Type',
              cell: (row) => (
                <div className="flex flex-wrap gap-1.5">
                  <Pill tone="muted">{planKindNames[row.kind]}</Pill>
                  {row.cycle > 0 && <Pill tone="muted">{billingCycleNames[row.cycle]}</Pill>}
                </div>
              ),
            },
            {
              key: 'price',
              header: 'List price',
              align: 'right',
              cell: (row) => (
                <div>
                  <p className="numeric font-medium">{formatInr(row.basePrice)}</p>
                  <p className="numeric text-[0.75rem] text-smoke">{row.durationDays} days</p>
                </div>
              ),
            },
            {
              key: 'overrides',
              header: 'Branch prices',
              align: 'right',
              cell: (row) =>
                row.branchPrices.length === 0 ? (
                  <span className="text-smoke">list everywhere</span>
                ) : (
                  <span className="numeric text-[0.8125rem] text-smoke">
                    {row.branchPrices.map((price) => formatInr(price.price)).join(' · ')}
                  </span>
                ),
            },
            {
              key: 'subs',
              header: 'Live',
              align: 'right',
              cell: (row) => <span className="numeric">{row.activeSubscriptions}</span>,
            },
            {
              key: 'flags',
              header: '',
              align: 'right',
              cell: (row) => (
                <div className="flex justify-end gap-1.5">
                  {!row.isActive && <Pill tone="muted">retired</Pill>}
                  {row.isActive && !row.showOnWebsite && <Pill tone="muted">off-menu</Pill>}
                </div>
              ),
            },
          ]}
        />
      </Panel>

      <PlanDrawer value={editing} onClose={() => setEditing(null)} />
    </>
  )
}

function PlanDrawer({ value, onClose }: { value: PlanRow | 'new' | null; onClose: () => void }) {
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const mutations = usePlanMutations()
  const isNew = value === 'new'
  const plan = isNew ? null : value

  const [form, setForm] = useState<Record<string, unknown>>({})
  const [prices, setPrices] = useState<Record<number, { price: string; admissionFee: string; available: boolean }>>({})
  const [signature, setSignature] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const key = value === null ? null : isNew ? 'new' : `plan-${plan?.id}`
  if (key !== null && signature !== key) {
    setSignature(key)
    setError(null)
    setForm({
      name: plan?.name ?? '',
      slug: plan?.slug ?? '',
      tagline: plan?.tagline ?? '',
      description: plan?.description ?? '',
      kind: String(plan?.kind ?? 0),
      cycle: String(plan?.cycle ?? 1),
      accessScope: String(plan?.accessScope ?? 0),
      durationDays: String(plan?.durationDays ?? 30),
      basePrice: String(plan?.basePrice ?? 0),
      admissionFee: String(plan?.admissionFee ?? 0),
      gstRatePercent: String(plan?.gstRatePercent ?? 5),
      sacCode: plan?.sacCode ?? '999723',
      classCredits: plan?.classCredits ?? '',
      ptSessionCredits: plan?.ptSessionCredits ?? '',
      guestPasses: plan?.guestPasses ?? '',
      freezeDaysAllowed: String(plan?.freezeDaysAllowed ?? 0),
      freezeFee: String(plan?.freezeFee ?? 0),
      accessWindowStart: plan?.accessWindowStart ?? '',
      accessWindowEnd: plan?.accessWindowEnd ?? '',
      features: (plan?.features ?? []).join('\n'),
      trustMicrocopy: plan?.trustMicrocopy ?? '',
      isMostPopular: plan?.isMostPopular ?? false,
      showOnWebsite: plan?.showOnWebsite ?? true,
      isActive: plan?.isActive ?? true,
      displayOrder: String(plan?.displayOrder ?? 0),
    })
    setPrices(
      Object.fromEntries(
        (plan?.branchPrices ?? []).map((price) => [
          price.branchId,
          {
            price: String(price.price),
            admissionFee: price.admissionFee !== null && price.admissionFee !== undefined ? String(price.admissionFee) : '',
            available: price.isAvailable,
          },
        ]),
      ),
    )
  }

  function set(field: string, next: unknown) {
    setForm((current) => ({ ...current, [field]: next }))
  }

  async function submit() {
    setError(null)
    const body = {
      name: String(form.name),
      slug: String(form.slug || form.name).toLowerCase().replace(/[^a-z0-9]+/g, '-'),
      tagline: String(form.tagline),
      description: String(form.description),
      kind: Number(form.kind),
      cycle: Number(form.cycle),
      accessScope: Number(form.accessScope),
      durationDays: Number(form.durationDays),
      basePrice: Number(form.basePrice),
      admissionFee: Number(form.admissionFee),
      gstRatePercent: Number(form.gstRatePercent),
      sacCode: String(form.sacCode),
      classCredits: form.classCredits === '' ? undefined : Number(form.classCredits),
      ptSessionCredits: form.ptSessionCredits === '' ? undefined : Number(form.ptSessionCredits),
      guestPasses: form.guestPasses === '' ? undefined : Number(form.guestPasses),
      freezeDaysAllowed: Number(form.freezeDaysAllowed),
      freezeFee: Number(form.freezeFee),
      accessWindowStart: form.accessWindowStart || undefined,
      accessWindowEnd: form.accessWindowEnd || undefined,
      features: String(form.features ?? '')
        .split('\n')
        .map((line) => line.trim())
        .filter(Boolean),
      trustMicrocopy: form.trustMicrocopy || undefined,
      isMostPopular: Boolean(form.isMostPopular),
      showOnWebsite: Boolean(form.showOnWebsite),
      isActive: Boolean(form.isActive),
      displayOrder: Number(form.displayOrder),
    }

    try {
      const saved = isNew
        ? ((await mutations.create.mutateAsync(body)) as PlanRow)
        : ((await mutations.update.mutateAsync({ id: plan!.id, body })) as PlanRow)

      const overrides = Object.entries(prices)
        .filter(([, row]) => row.price !== '')
        .map(([branchId, row]) => ({
          branchId: Number(branchId),
          price: Number(row.price),
          admissionFee: row.admissionFee === '' ? undefined : Number(row.admissionFee),
          isAvailable: row.available,
        }))

      await mutations.setPrices.mutateAsync({ id: saved.id, prices: overrides })
      toast.success('Plan saved', 'The pricing page reflects this immediately.')
      onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <>
      <Drawer
        open={value !== null}
        onClose={onClose}
        title={isNew ? 'New plan' : `Edit ${plan?.name}`}
        description="Everything here is what the public pricing page reads. Branch overrides beat the list price."
        width="lg"
        footer={
          <>
            {!isNew && (
              <Button variant="ghost" size="sm" onClick={() => setConfirmDelete(true)}>
                Retire plan
              </Button>
            )}
            <div className="flex-1" />
            <Button variant="ghost" size="sm" onClick={onClose}>
              Cancel
            </Button>
            <Button
              size="sm"
              icon="check"
              onClick={() => void submit()}
              loading={mutations.create.isPending || mutations.update.isPending || mutations.setPrices.isPending}
            >
              Save plan
            </Button>
          </>
        }
      >
        <div className="space-y-6">
          {error && <InlineError>{error}</InlineError>}

          <div className="grid gap-4 sm:grid-cols-2">
            <TextField label="Name" required value={String(form.name ?? '')} onChange={(event) => set('name', event.target.value)} />
            <TextField label="Slug" hint="Used by deep links like /free-trial?plan=…" value={String(form.slug ?? '')} onChange={(event) => set('slug', event.target.value)} />
            <TextField label="Tagline" className="sm:col-span-2" value={String(form.tagline ?? '')} onChange={(event) => set('tagline', event.target.value)} />
            <SelectField label="Kind" value={String(form.kind ?? 0)} onChange={(event) => set('kind', event.target.value)}>
              {planKindNames.map((name, index) => (
                <option key={name} value={index}>
                  {name}
                </option>
              ))}
            </SelectField>
            <SelectField label="Billing cycle" value={String(form.cycle ?? 1)} onChange={(event) => set('cycle', event.target.value)}>
              {billingCycleNames.map((name, index) => (
                <option key={name} value={index}>
                  {name}
                </option>
              ))}
            </SelectField>
            <SelectField label="Access" value={String(form.accessScope ?? 0)} onChange={(event) => set('accessScope', event.target.value)}>
              <option value="0">Home branch only</option>
              <option value="1">All branches</option>
            </SelectField>
            <TextField label="Duration (days)" type="number" value={String(form.durationDays ?? '')} onChange={(event) => set('durationDays', event.target.value)} />
          </div>

          <div className="grid gap-4 rounded-[0.625rem] border border-[var(--hairline)] p-4 sm:grid-cols-2">
            <TextField label="List price" type="number" addon="₹" hint="GST inclusive — this is the figure on the pricing card." value={String(form.basePrice ?? '')} onChange={(event) => set('basePrice', event.target.value)} />
            <TextField label="Admission fee" type="number" addon="₹" hint="Charged once per member, on their first plan." value={String(form.admissionFee ?? '')} onChange={(event) => set('admissionFee', event.target.value)} />
            <TextField label="GST rate" type="number" addon="%" value={String(form.gstRatePercent ?? '')} onChange={(event) => set('gstRatePercent', event.target.value)} />
            <TextField label="SAC code" hint="999723 for health and fitness services." value={String(form.sacCode ?? '')} onChange={(event) => set('sacCode', event.target.value)} />
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <TextField label="Class credits" type="number" placeholder="Unlimited" value={String(form.classCredits ?? '')} onChange={(event) => set('classCredits', event.target.value)} />
            <TextField label="PT sessions" type="number" placeholder="None" value={String(form.ptSessionCredits ?? '')} onChange={(event) => set('ptSessionCredits', event.target.value)} />
            <TextField label="Guest passes" type="number" placeholder="None" value={String(form.guestPasses ?? '')} onChange={(event) => set('guestPasses', event.target.value)} />
            <TextField label="Freeze days allowed" type="number" value={String(form.freezeDaysAllowed ?? '')} onChange={(event) => set('freezeDaysAllowed', event.target.value)} />
            <TextField label="Freeze fee" type="number" addon="₹" value={String(form.freezeFee ?? '')} onChange={(event) => set('freezeFee', event.target.value)} />
            <div />
            <TextField label="Off-peak from" type="time" hint="Leave blank for all-hours access." value={String(form.accessWindowStart ?? '')} onChange={(event) => set('accessWindowStart', event.target.value)} />
            <TextField label="Off-peak until" type="time" value={String(form.accessWindowEnd ?? '')} onChange={(event) => set('accessWindowEnd', event.target.value)} />
          </div>

          <TextAreaField
            label="Features"
            hint="One per line. These are the ticks on the pricing card."
            rows={5}
            value={String(form.features ?? '')}
            onChange={(event) => set('features', event.target.value)}
          />
          <TextField
            label="Trust microcopy"
            hint="The reassurance line under the price — 'No joining fee · Pause anytime'."
            value={String(form.trustMicrocopy ?? '')}
            onChange={(event) => set('trustMicrocopy', event.target.value)}
          />

          {/* ---- branch overrides ---- */}
          <div>
            <h3 className="mb-1 text-[0.9375rem] font-semibold">Branch pricing</h3>
            <p className="mb-3 text-[0.8125rem] leading-relaxed text-smoke">
              Leave a branch blank to charge the list price there. Clearing a row removes the override.
            </p>
            <div className="space-y-2">
              {(settings?.branches ?? []).map((branch) => {
                const row = prices[branch.id] ?? { price: '', admissionFee: '', available: true }
                return (
                  <div
                    key={branch.id}
                    className="grid items-end gap-3 rounded-[0.625rem] border border-[var(--hairline)] p-3 sm:grid-cols-[1fr_7rem_7rem_auto]"
                  >
                    <div className="min-w-0">
                      <p className="truncate text-[0.875rem] font-medium">{branch.name}</p>
                      <p className="truncate text-[0.75rem] text-smoke">{branch.city}</p>
                    </div>
                    <TextField
                      label="Price"
                      type="number"
                      addon="₹"
                      value={row.price}
                      onChange={(event) =>
                        setPrices((current) => ({ ...current, [branch.id]: { ...row, price: event.target.value } }))
                      }
                    />
                    <TextField
                      label="Admission"
                      type="number"
                      addon="₹"
                      value={row.admissionFee}
                      onChange={(event) =>
                        setPrices((current) => ({ ...current, [branch.id]: { ...row, admissionFee: event.target.value } }))
                      }
                    />
                    <div className="pb-2.5">
                      <Toggle
                        label="Sold here"
                        checked={row.available}
                        onChange={(next) =>
                          setPrices((current) => ({ ...current, [branch.id]: { ...row, available: next } }))
                        }
                      />
                    </div>
                  </div>
                )
              })}
            </div>
          </div>

          <div className="space-y-3 rounded-[0.625rem] border border-[var(--hairline)] p-4">
            <Toggle label="Most popular" hint="Gold-bordered on the pricing page. Use it on one plan." checked={Boolean(form.isMostPopular)} onChange={(next) => set('isMostPopular', next)} />
            <Toggle label="Show on the website" checked={Boolean(form.showOnWebsite)} onChange={(next) => set('showOnWebsite', next)} />
            <Toggle label="Active" hint="Off retires the plan; existing members keep theirs." checked={Boolean(form.isActive)} onChange={(next) => set('isActive', next)} />
            <TextField label="Display order" type="number" value={String(form.displayOrder ?? 0)} onChange={(event) => set('displayOrder', event.target.value)} />
          </div>
        </div>
      </Drawer>

      <ConfirmDialog
        open={confirmDelete}
        onClose={() => setConfirmDelete(false)}
        title="Retire this plan?"
        body="If anyone holds it, the plan is switched off rather than deleted so their invoices keep resolving."
        confirmLabel="Retire plan"
        tone="danger"
        loading={mutations.remove.isPending}
        onConfirm={() => {
          if (!plan) return
          void mutations.remove.mutateAsync(plan.id).then(() => {
            toast.success('Plan retired')
            setConfirmDelete(false)
            onClose()
          })
        }}
      />
    </>
  )
}

/* ---------------------------------------------------------------- coupons */

function CouponsTab() {
  const { data: coupons, isLoading } = useCoupons()
  const [editing, setEditing] = useState<CouponRow | 'new' | null>(null)

  return (
    <>
      <Panel
        padded={false}
        title="Coupons & seasonal offers"
        description="Caps and windows are enforced at the point of sale, so a spent coupon simply stops applying."
        actions={
          <Button size="sm" icon="plus" onClick={() => setEditing('new')}>
            New coupon
          </Button>
        }
      >
        <DataTable
          rows={coupons ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          onRowClick={(row) => setEditing(row)}
          emptyHeadline="No coupons"
          emptyBody="Create one to drive a seasonal campaign or a referral reward."
          columns={[
            {
              key: 'code',
              header: 'Code',
              cell: (row) => (
                <div>
                  <p className="numeric font-semibold tracking-[0.04em]">{row.code}</p>
                  <p className="truncate text-[0.75rem] text-smoke">{row.name}</p>
                </div>
              ),
            },
            {
              key: 'discount',
              header: 'Discount',
              cell: (row) => (
                <span className="numeric">
                  {row.discountType === 0 ? `${row.discountValue}%` : formatInr(row.discountValue)}
                  {row.maxDiscountAmount ? ` (max ${formatInr(row.maxDiscountAmount)})` : ''}
                </span>
              ),
            },
            {
              key: 'window',
              header: 'Window',
              cell: (row) => (
                <span className="numeric text-[0.8125rem] text-smoke">
                  {formatIsoDate(row.validFrom)} → {formatIsoDate(row.validTo)}
                </span>
              ),
            },
            {
              key: 'usage',
              header: 'Redeemed',
              align: 'right',
              cell: (row) => (
                <span className="numeric">
                  {row.usageCount}
                  {row.usageCap ? <span className="text-smoke">/{row.usageCap}</span> : ''}
                </span>
              ),
            },
            {
              key: 'status',
              header: '',
              align: 'right',
              cell: (row) => (
                <div className="flex justify-end gap-1.5">
                  {row.showAsWebsiteBanner && <Pill tone="accent">on site</Pill>}
                  <Pill tone={row.isLive ? 'success' : 'muted'}>{row.isLive ? 'live' : 'off'}</Pill>
                </div>
              ),
            },
          ]}
        />
      </Panel>

      <CouponDrawer value={editing} onClose={() => setEditing(null)} />
    </>
  )
}

function CouponDrawer({ value, onClose }: { value: CouponRow | 'new' | null; onClose: () => void }) {
  const toast = useToast()
  const mutations = useCouponMutations()
  const isNew = value === 'new'
  const coupon = isNew ? null : value

  const [form, setForm] = useState<Record<string, unknown>>({})
  const [signature, setSignature] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const key = value === null ? null : isNew ? 'new' : `coupon-${coupon?.id}`
  if (key !== null && signature !== key) {
    setSignature(key)
    setError(null)
    setForm({
      code: coupon?.code ?? '',
      name: coupon?.name ?? '',
      description: coupon?.description ?? '',
      discountType: String(coupon?.discountType ?? 0),
      discountValue: String(coupon?.discountValue ?? 10),
      maxDiscountAmount: coupon?.maxDiscountAmount ?? '',
      minOrderAmount: String(coupon?.minOrderAmount ?? 0),
      validFrom: coupon?.validFrom ?? istToday(),
      validTo: coupon?.validTo ?? istToday(30),
      usageCap: coupon?.usageCap ?? '',
      perMemberCap: coupon?.perMemberCap ?? 1,
      isActive: coupon?.isActive ?? true,
      showAsWebsiteBanner: coupon?.showAsWebsiteBanner ?? false,
      bannerHeadline: coupon?.bannerHeadline ?? '',
    })
  }

  function set(field: string, next: unknown) {
    setForm((current) => ({ ...current, [field]: next }))
  }

  async function submit() {
    setError(null)
    const body = {
      code: String(form.code).toUpperCase(),
      name: String(form.name),
      description: form.description || undefined,
      discountType: Number(form.discountType),
      discountValue: Number(form.discountValue),
      maxDiscountAmount: form.maxDiscountAmount === '' ? undefined : Number(form.maxDiscountAmount),
      minOrderAmount: Number(form.minOrderAmount),
      validFrom: String(form.validFrom),
      validTo: String(form.validTo),
      usageCap: form.usageCap === '' ? undefined : Number(form.usageCap),
      perMemberCap: form.perMemberCap === '' ? undefined : Number(form.perMemberCap),
      isActive: Boolean(form.isActive),
      showAsWebsiteBanner: Boolean(form.showAsWebsiteBanner),
      bannerHeadline: form.bannerHeadline || undefined,
    }

    try {
      if (isNew) await mutations.create.mutateAsync(body)
      else await mutations.update.mutateAsync({ id: coupon!.id, body })
      toast.success('Coupon saved')
      onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <Drawer
      open={value !== null}
      onClose={onClose}
      title={isNew ? 'New coupon' : `Edit ${coupon?.code}`}
      description="Every cap is checked at the point of sale, and the desk is told why a code did not apply."
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button size="sm" icon="check" onClick={() => void submit()} loading={mutations.create.isPending || mutations.update.isPending}>
            Save coupon
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {error && <InlineError>{error}</InlineError>}

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label="Code"
            required
            value={String(form.code ?? '')}
            onChange={(event) => set('code', event.target.value.toUpperCase())}
          />
          <TextField label="Campaign name" required value={String(form.name ?? '')} onChange={(event) => set('name', event.target.value)} />
          <SelectField label="Discount type" value={String(form.discountType ?? 0)} onChange={(event) => set('discountType', event.target.value)}>
            <option value="0">Percentage</option>
            <option value="1">Flat amount</option>
          </SelectField>
          <TextField
            label="Value"
            type="number"
            addon={form.discountType === '0' ? '%' : '₹'}
            value={String(form.discountValue ?? '')}
            onChange={(event) => set('discountValue', event.target.value)}
          />
          <TextField
            label="Maximum discount"
            type="number"
            addon="₹"
            hint="Caps a percentage discount. Blank means no cap."
            value={String(form.maxDiscountAmount ?? '')}
            onChange={(event) => set('maxDiscountAmount', event.target.value)}
          />
          <TextField
            label="Minimum order"
            type="number"
            addon="₹"
            value={String(form.minOrderAmount ?? 0)}
            onChange={(event) => set('minOrderAmount', event.target.value)}
          />
          <TextField label="Valid from" type="date" value={String(form.validFrom ?? '')} onChange={(event) => set('validFrom', event.target.value)} />
          <TextField label="Valid to" type="date" value={String(form.validTo ?? '')} onChange={(event) => set('validTo', event.target.value)} />
          <TextField
            label="Total redemptions"
            type="number"
            hint="Blank for unlimited."
            value={String(form.usageCap ?? '')}
            onChange={(event) => set('usageCap', event.target.value)}
          />
          <TextField
            label="Per member"
            type="number"
            value={String(form.perMemberCap ?? '')}
            onChange={(event) => set('perMemberCap', event.target.value)}
          />
        </div>

        <TextAreaField label="Internal note" rows={2} value={String(form.description ?? '')} onChange={(event) => set('description', event.target.value)} />

        <div className="space-y-3 rounded-[0.625rem] border border-[var(--hairline)] p-4">
          <Toggle label="Active" checked={Boolean(form.isActive)} onChange={(next) => set('isActive', next)} />
          <Toggle
            label="Show as a website banner"
            hint="Drives the seasonal offer strip on the public site."
            checked={Boolean(form.showAsWebsiteBanner)}
            onChange={(next) => set('showAsWebsiteBanner', next)}
          />
          {Boolean(form.showAsWebsiteBanner) && (
            <TextField
              label="Banner headline"
              placeholder="Monsoon intake — 20% off annual"
              value={String(form.bannerHeadline ?? '')}
              onChange={(event) => set('bannerHeadline', event.target.value)}
            />
          )}
        </div>

        <Hint icon="sparkles">
          The offer banner section on the public site reads the coupon by code. Keep the code in the section
          content and the campaign here in step.
        </Hint>
      </div>
    </Drawer>
  )
}

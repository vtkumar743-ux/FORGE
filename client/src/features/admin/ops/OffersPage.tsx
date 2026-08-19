import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { DataTable, Hint, PageHeader, Panel, Pill, StatCard, TextField } from '../components/ui'
import { formatInr, istToday } from '../lib/format'
import {
  OFFER_STATUS,
  useOffPeak,
  useOfferCampaigns,
  useSetOfferBanner,
  useSetOfferState,
  type OfferCampaign,
} from '../lib/module4-api'

/**
 * The seasonal offer engine (Module 4.7).
 *
 * A campaign here is the same coupon row the point of sale validates, so the discount the
 * website advertises and the discount the desk can actually apply are one object. What this
 * screen adds is the campaign's life — when it starts, when it stops, whether it is on the
 * public banner, and what it earned.
 */
export function OffersPage() {
  const { data, isLoading } = useOfferCampaigns()
  const { data: offPeak } = useOffPeak()
  const setBanner = useSetOfferBanner()
  const setState = useSetOfferState()
  const [extendFor, setExtendFor] = useState<OfferCampaign | null>(null)
  const [extendTo, setExtendTo] = useState(istToday(30))

  return (
    <>
      <PageHeader
        eyebrow="Revenue"
        title="Campaigns & off-peak"
        lead="Seasonal offers, what each one earned, and the quiet-hours tier that fills the middle of the day."
      >
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <StatCard label="Live now" value={String(data?.live ?? 0)} sub="Inside their dates" tone="accent" />
          <StatCard label="Scheduled" value={String(data?.scheduled ?? 0)} sub="Start on a future date" />
          <StatCard
            label="Redemptions"
            value={String(data?.redemptionsAllTime ?? 0)}
            sub={`${formatInr(data?.discountGivenAllTime ?? 0)} given away`}
          />
          <StatCard
            label="Revenue booked"
            value={formatInr(data?.revenueBookedAllTime ?? 0)}
            sub="On subscriptions that used a code"
          />
        </div>
      </PageHeader>

      {data && data.onBanner === 0 && data.live > 0 && (
        <Hint icon="sparkles">
          {data.live} campaign{data.live === 1 ? ' is' : 's are'} live but none is on the public banner. Visitors
          will not see the offer unless the desk mentions it.
        </Hint>
      )}

      <Panel
        className="mt-5"
        padded={false}
        title="Campaigns"
        description="One banner at a time — two competing offers on the same hero is how a visitor picks neither."
      >
        <DataTable
          rows={data?.campaigns ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          emptyHeadline="No campaigns yet"
          emptyBody="Create a coupon under Billing → Plans, then bring it here to schedule and put it on the banner."
          columns={[
            {
              key: 'campaign',
              header: 'Campaign',
              cell: (row) => (
                <div className="min-w-0">
                  <p className="truncate font-medium">{row.name}</p>
                  <p className="truncate text-[0.75rem] text-smoke">
                    <code>{row.code}</code>
                    {row.branchNames.length > 0 && ` · ${row.branchNames.join(', ')}`}
                    {row.planNames.length > 0 && ` · ${row.planNames.join(', ')}`}
                  </p>
                </div>
              ),
            },
            {
              key: 'offer',
              header: 'Offer',
              width: '9rem',
              cell: (row) => (
                <span className="text-[0.8125rem]">
                  {row.discountType === 0 ? `${row.discountValue}% off` : `${formatInr(row.discountValue)} off`}
                  {row.maxDiscountAmount != null && (
                    <span className="text-smoke"> · cap {formatInr(row.maxDiscountAmount)}</span>
                  )}
                </span>
              ),
            },
            {
              key: 'window',
              header: 'Runs',
              width: '12rem',
              cell: (row) => (
                <div>
                  <p className="text-[0.8125rem] text-smoke">
                    {row.validFrom} → {row.validTo}
                  </p>
                  {row.status === 1 && (
                    <p className="text-[0.75rem] text-smoke">
                      {row.daysRemaining <= 0
                        ? 'ends today'
                        : `${row.daysRemaining} day${row.daysRemaining === 1 ? '' : 's'} left`}
                    </p>
                  )}
                </div>
              ),
            },
            {
              key: 'used',
              header: 'Used',
              width: '7rem',
              align: 'right',
              cell: (row) => (
                <span className="tabular-nums text-[0.8125rem]">
                  {row.usageCount}
                  {row.usageCap != null ? ` / ${row.usageCap}` : ''}
                </span>
              ),
            },
            {
              key: 'earned',
              header: 'Booked',
              width: '9rem',
              align: 'right',
              cell: (row) => (
                <div className="tabular-nums">
                  <p className="text-[0.8125rem]">{formatInr(row.revenueBooked)}</p>
                  {row.discountGiven > 0 && (
                    <p className="text-[0.75rem] text-smoke">−{formatInr(row.discountGiven)}</p>
                  )}
                </div>
              ),
            },
            {
              key: 'status',
              header: 'Status',
              width: '7rem',
              cell: (row) => (
                <Pill
                  tone={
                    row.status === 1 ? 'success' : row.status === 0 ? 'warn' : 'neutral'
                  }
                >
                  {OFFER_STATUS[row.status]}
                </Pill>
              ),
            },
            {
              key: 'actions',
              header: '',
              width: '15rem',
              cell: (row) => (
                <div className="flex flex-wrap justify-end gap-1.5">
                  <Button
                    size="sm"
                    variant={row.showAsWebsiteBanner ? 'primary' : 'ghost'}
                    onClick={() => setBanner.mutate({ id: row.id, show: !row.showAsWebsiteBanner })}
                    disabled={setBanner.isPending}
                  >
                    {row.showAsWebsiteBanner ? 'On banner' : 'Banner'}
                  </Button>
                  {row.status === 2 ? (
                    <Button size="sm" variant="ghost" onClick={() => { setExtendFor(row); setExtendTo(istToday(30)) }}>
                      Extend
                    </Button>
                  ) : (
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => setState.mutate({ id: row.id, active: !row.isActive })}
                      disabled={setState.isPending}
                    >
                      {row.isActive ? 'Pause' : 'Resume'}
                    </Button>
                  )}
                </div>
              ),
            },
          ]}
        />
      </Panel>

      {extendFor && (
        <Panel className="mt-5" title={`Extend ${extendFor.name}`}>
          <div className="flex flex-wrap items-end gap-4">
            <TextField
              label="New end date"
              type="date"
              value={extendTo}
              onChange={(event) => setExtendTo(event.target.value)}
            />
            <Button
              onClick={() =>
                setState.mutate(
                  { id: extendFor.id, active: true, extendToDate: extendTo },
                  { onSuccess: () => setExtendFor(null) },
                )
              }
            >
              Extend and resume
            </Button>
            <Button variant="ghost" onClick={() => setExtendFor(null)}>
              Cancel
            </Button>
          </div>
        </Panel>
      )}

      <Panel
        className="mt-5"
        title="Off-peak tier"
        description="The 10 AM–4 PM plan. It fills the quiet middle of the day, which is the cheapest capacity the gym owns."
      >
        {(offPeak?.plans ?? []).length === 0 ? (
          <p className="text-[0.875rem] text-smoke">
            No off-peak plan is configured. Add an access window to a plan under Billing → Plans and it appears here.
          </p>
        ) : (
          <div className="space-y-3">
            {(offPeak?.plans ?? []).map((plan) => (
              <div
                key={plan.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-input)] border border-[var(--hairline)] p-4"
              >
                <div className="min-w-0">
                  <p className="font-medium">{plan.name}</p>
                  <p className="text-[0.8125rem] text-smoke">
                    Access {plan.windowStart}–{plan.windowEnd} · {formatInr(plan.basePrice)} list
                  </p>
                </div>
                <div className="flex items-center gap-4">
                  <span className="text-[0.8125rem] tabular-nums">
                    {plan.activeSubscribers} on this plan
                  </span>
                  <Pill tone={plan.isActive ? 'success' : 'neutral'}>{plan.isActive ? 'Selling' : 'Retired'}</Pill>
                </div>
              </div>
            ))}

            {(offPeak?.offPeakRefusalsLast30Days ?? 0) > 0 && (
              <p className="flex items-start gap-2 text-[0.8125rem] text-smoke">
                <Icon name="clock" size={14} className="mt-0.5 shrink-0" aria-hidden="true" />
                {offPeak?.offPeakRefusalsLast30Days} check-ins were refused outside the window in the last 30 days.
                If that number keeps climbing, the window is in the wrong place — not the members.
              </p>
            )}
          </div>
        )}
      </Panel>
    </>
  )
}

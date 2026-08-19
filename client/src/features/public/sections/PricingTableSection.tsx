import { useState } from 'react'
import { Reveal } from '@/components/ui/Reveal'
import { Badge } from '@/components/ui/Card'
import { Button, ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, Skeleton, SkeletonText } from '@/components/ui/Skeleton'
import { useSiteSettings } from '@/lib/cms'
import { usePlans, type Plan } from '@/lib/public-api'
import { SectionHeader } from '../components/SectionHeader'
import { useBranchScope } from './context'
import { cn, formatInr } from '@/lib/utils'
import type { PricingTableContent } from './schemas'

/* ============================================================================
   Pricing (Module 1.5, 03 §7)

   Three tiers on screen, one elevated with a gold border, trust microcopy under
   every price (the Peloton pattern), and a full compare table underneath for the
   people who want the detail. The cycle toggle re-sorts which plans are on top
   rather than recomputing a price — each cycle is a real plan with its own rate,
   not a monthly figure multiplied by twelve, because that is what the member is
   actually charged.
   ============================================================================ */

const CYCLE_LABELS: Record<string, string> = {
  monthly: 'Monthly',
  quarterly: 'Quarterly',
  'half-yearly': 'Half-yearly',
  halfyearly: 'Half-yearly',
  annual: 'Annual',
}

/** Compare-table cells, derived from real plan fields rather than hand-maintained copy. */
const COMPARE_VALUES: Record<string, (plan: Plan) => string | boolean> = {
  floor: (plan) => (plan.accessWindow ? plan.accessWindow : 'Unlimited'),
  classes: (plan) => (plan.accessWindow ? 'Off-peak only' : true),
  branches: (plan) => (plan.accessScope === 1 ? 'All three' : 'Home branch'),
  freeze: (plan) => (plan.freezeDaysAllowed > 0 ? `${plan.freezeDaysAllowed} days` : false),
  guests: (plan) => (plan.guestPasses ? `${plan.guestPasses}` : false),
  scans: (plan) =>
    plan.durationDays >= 365 ? '4 a year' : plan.durationDays >= 180 ? 'Quarterly' : plan.durationDays >= 90 ? 'On joining' : 'On joining',
  program: (plan) => plan.durationDays >= 90,
  pt: (plan) => (plan.ptSessionCredits ? `${plan.ptSessionCredits} included` : false),
}

export function PricingTableSection({ content }: { content: PricingTableContent }) {
  const branchScope = useBranchScope()
  const { data: settings } = useSiteSettings()
  const branches = settings?.branches ?? []

  const [selectedBranch, setSelectedBranch] = useState<string | undefined>(branchScope)
  const [cycle, setCycle] = useState<string>(content.defaultCycle ?? content.cycleToggle[0] ?? 'annual')

  const { data: plans, isLoading, isError } = usePlans(selectedBranch)

  const bySlug = new Map((plans ?? []).map((plan) => [plan.slug, plan]))

  // The toggle promotes its cycle to the front of the trio; the other two stay for contrast.
  const highlighted = content.highlightedPlanSlugs
    .map((slug) => bySlug.get(slug))
    .filter((plan): plan is Plan => plan !== undefined)
    .sort((a, b) => Number(matchesCycle(b, cycle)) - Number(matchesCycle(a, cycle)))
    .slice(0, 3)

  const comparePlans = content.comparePlanSlugs
    .map((slug) => bySlug.get(slug))
    .filter((plan): plan is Plan => plan !== undefined)

  return (
    <section className="section-y bg-ink" id="plans">
      <div className="shell">
        <SectionHeader eyebrow={content.eyebrow} headline={content.headline} body={content.body} />

        {/* Cycle toggle + branch selector: the two things that change the number. */}
        <Reveal delay={0.12} className="mt-10 flex flex-wrap items-center gap-x-8 gap-y-5">
          {content.cycleToggle.length > 1 && (
            <div
              className="inline-flex rounded-full border border-[var(--hairline-strong)] p-1"
              role="group"
              aria-label="Billing cycle"
            >
              {content.cycleToggle.map((option) => (
                <button
                  key={option}
                  type="button"
                  onClick={() => setCycle(option)}
                  aria-pressed={cycle === option}
                  className={cn(
                    'rounded-full px-4 py-2 text-[0.8125rem] transition-colors duration-200 ease-out',
                    cycle === option ? 'bg-accent text-ink' : 'text-smoke hover:text-bone',
                  )}
                >
                  {CYCLE_LABELS[option] ?? option}
                </button>
              ))}
            </div>
          )}

          {content.showBranchSelector && branches.length > 0 && !branchScope && (
            <label className="flex items-center gap-3 text-[0.8125rem] text-smoke">
              <Icon name="map-pin" size={16} className="text-accent" />
              Prices at
              <select
                className="field-input h-10 w-auto min-w-[12rem] py-0 text-[0.8125rem]"
                value={selectedBranch ?? ''}
                onChange={(event) => setSelectedBranch(event.target.value || undefined)}
              >
                <option value="">every branch (list price)</option>
                {branches.map((branch) => (
                  <option key={branch.slug} value={branch.slug}>
                    {branch.name.replace('FORGE ', '')}
                  </option>
                ))}
              </select>
            </label>
          )}
        </Reveal>

        {isLoading && (
          <div className="mt-12 grid gap-5 lg:grid-cols-3" aria-busy="true">
            {Array.from({ length: 3 }).map((_, index) => (
              <div key={index} className="space-y-5 rounded-[var(--radius-card)] border border-[var(--hairline)] p-8">
                <Skeleton rounded="pill" className="h-4 w-24" />
                <Skeleton className="h-14 w-40" />
                <SkeletonText lines={5} />
              </div>
            ))}
          </div>
        )}

        {isError && (
          <EmptyState
            className="mt-12"
            icon="sparkles"
            headline="Prices are not loading"
            body="Call the desk on +91 80 4172 9500 and we will quote you directly — the figures are the same either way."
            actionLabel="Contact us"
            actionTo="/contact"
          />
        )}

        {highlighted.length > 0 && (
          <div className="mt-12 grid items-stretch gap-5 lg:grid-cols-3">
            {highlighted.map((plan, index) => (
              <Reveal key={plan.slug} delay={index * 0.08} className="h-full">
                <PlanCard plan={plan} />
              </Reveal>
            ))}
          </div>
        )}

        {content.trustMicrocopy && (
          <Reveal delay={0.2}>
            <p className="mt-8 flex flex-wrap items-center gap-x-2 text-center text-[0.8125rem] text-smoke sm:justify-center">
              <Icon name="lock" size={14} className="text-accent" />
              {content.trustMicrocopy}
            </p>
          </Reveal>
        )}

        {comparePlans.length > 0 && content.compareRows.length > 0 && (
          <CompareTable plans={comparePlans} rows={content.compareRows} />
        )}

        {content.footnote && (
          <Reveal>
            <p className="measure mt-10 text-[0.75rem] leading-relaxed text-smoke">{content.footnote}</p>
          </Reveal>
        )}
      </div>
    </section>
  )
}

function PlanCard({ plan }: { plan: Plan }) {
  const popular = plan.isMostPopular

  return (
    <article
      className={cn(
        'relative flex h-full flex-col rounded-[var(--radius-card)] border p-8',
        popular
          ? 'border-accent bg-carbon shadow-[0_30px_80px_-50px_color-mix(in_srgb,var(--accent)_70%,transparent)]'
          : 'border-[var(--hairline)] bg-carbon',
      )}
    >
      {popular && (
        <span className="absolute -top-3 left-8 rounded-full bg-accent px-3 py-1 text-[0.6875rem] font-semibold uppercase tracking-[0.08em] text-ink">
          Most popular
        </span>
      )}

      <div className="flex items-start justify-between gap-4">
        <div>
          <h3 className="display-m text-[1.375rem] text-bone">{plan.name}</h3>
          <p className="mt-2 text-[0.875rem] text-smoke">{plan.tagline}</p>
        </div>
        {plan.savingsPercent > 0 && <Badge tone="accent">Save {plan.savingsPercent}%</Badge>}
      </div>

      <p className="mt-7 flex items-baseline gap-2">
        <span className="numeric display-l text-[clamp(2.5rem,4vw,3.25rem)] text-bone">{formatInr(plan.price)}</span>
        <span className="text-[0.875rem] text-smoke">{cycleSuffix(plan)}</span>
      </p>

      <p className="mt-2 text-[0.8125rem] text-smoke">
        {plan.durationDays > 30 && (
          <>
            <span className="numeric text-bone/80">{formatInr(plan.effectiveMonthlyPrice)}</span> a month ·{' '}
          </>
        )}
        GST included
        {plan.admissionFee > 0 ? ` · ${formatInr(plan.admissionFee)} admission` : ' · no joining fee'}
      </p>

      <ul className="mt-7 flex-1 space-y-3">
        {plan.features.map((feature) => (
          <li key={feature} className="flex items-start gap-2.5 text-[0.9375rem] leading-snug text-bone/85">
            <Icon name="check" size={16} strokeWidth={2} className="mt-0.5 shrink-0 text-accent" />
            {feature}
          </li>
        ))}
      </ul>

      <div className="mt-8 space-y-3">
        <ButtonLink
          to={`/free-trial?intent=join&plan=${plan.slug}`}
          variant={popular ? 'primary' : 'outline'}
          fullWidth
          magnetic={popular}
        >
          {popular ? 'Join now' : 'Choose this plan'}
        </ButtonLink>
        <ButtonLink to="/free-trial" variant="ghost" size="sm" fullWidth>
          Or try a free day first
        </ButtonLink>
      </div>

      {plan.trustMicrocopy && (
        <p className="mt-5 border-t border-[var(--hairline)] pt-4 text-[0.75rem] leading-relaxed text-smoke/80">
          {plan.trustMicrocopy}
        </p>
      )}
    </article>
  )
}

function CompareTable({ plans, rows }: { plans: Plan[]; rows: Array<{ label: string; key: string }> }) {
  const [open, setOpen] = useState(false)

  return (
    <div className="mt-16">
      <Button
        variant="outline"
        onClick={() => setOpen((current) => !current)}
        icon={open ? 'minus' : 'plus'}
        aria-expanded={open}
        aria-controls="plan-compare"
      >
        {open ? 'Hide the full comparison' : 'Compare all plans side by side'}
      </Button>

      {open && (
        <div id="plan-compare" className="mt-8 overflow-x-auto">
          <table className="w-full min-w-[46rem] border-collapse text-left">
            <caption className="sr-only">Every membership plan compared feature by feature</caption>
            <thead>
              <tr className="border-b border-[var(--hairline-strong)]">
                <th scope="col" className="caption py-4 pr-4 font-medium">
                  What you get
                </th>
                {plans.map((plan) => (
                  <th key={plan.slug} scope="col" className="px-4 py-4 align-bottom">
                    <span className="block text-[0.9375rem] text-bone">{plan.name}</span>
                    <span className="numeric mt-1 block text-[0.8125rem] text-accent">{formatInr(plan.price)}</span>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.key} className="border-b border-[var(--hairline)]">
                  <th scope="row" className="py-4 pr-4 text-[0.9375rem] font-normal text-smoke">
                    {row.label}
                  </th>
                  {plans.map((plan) => {
                    const value = COMPARE_VALUES[row.key]?.(plan) ?? false
                    return (
                      <td key={plan.slug} className="px-4 py-4 text-[0.9375rem] text-bone/85">
                        {typeof value === 'boolean' ? (
                          value ? (
                            <Icon name="check" size={17} strokeWidth={2} className="text-accent" label="Included" />
                          ) : (
                            <Icon name="minus" size={17} className="text-steel" label="Not included" />
                          )
                        ) : (
                          value
                        )}
                      </td>
                    )
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

/** True when a plan belongs to the cycle the toggle is on. */
function matchesCycle(plan: Plan, cycle: string): boolean {
  return plan.cycleName.toLowerCase().replace(/\s/g, '-') === cycle.toLowerCase()
}

function cycleSuffix(plan: Plan): string {
  if (plan.durationDays <= 31) return '/ month'
  if (plan.durationDays <= 92) return '/ 3 months'
  if (plan.durationDays <= 185) return '/ 6 months'
  if (plan.durationDays <= 370) return '/ year'
  return 'total'
}

import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { useSiteSettings } from '@/lib/cms'
import { cn } from '@/lib/utils'
import { useLead, useLeadActions, useLeadBoard, usePlans } from '../lib/admin-api'
import { describeErrorText, formatInr, formatIstDateTime, istToday, relativeTime } from '../lib/format'
import { followUpChannelNames, leadSourceNames, leadStageNames, type LeadCard } from '../lib/types'
import { Drawer, useToast } from '../components/overlays'
import {
  Avatar,
  FilterChip,
  Hint,
  InlineError,
  PageHeader,
  Panel,
  Pill,
  SelectField,
  StatCard,
  StatusPill,
  TextAreaField,
  TextField,
  Toggle,
} from '../components/ui'

const stageTone: Record<number, string> = {
  0: 'var(--smoke)',
  1: 'var(--accent)',
  2: 'var(--accent)',
  3: 'var(--accent)',
  4: 'var(--success)',
  5: 'var(--accent-hot)',
}

/**
 * The pipeline board. Cards move between columns by drag or by keyboard, and every move
 * schedules the follow-up that stage implies — the queue and the board cannot drift apart
 * because there is no separate "add a task" step to forget.
 */
export function LeadsPage() {
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const [branchId, setBranchId] = useState<number | undefined>()
  const [source, setSource] = useState<number | undefined>()
  const { data, isLoading } = useLeadBoard({ branchId, source })
  const actions = useLeadActions()

  const [openLead, setOpenLead] = useState<number | null>(null)
  const [dragging, setDragging] = useState<number | null>(null)
  const [dropTarget, setDropTarget] = useState<number | null>(null)
  const [newOpen, setNewOpen] = useState(false)

  async function move(leadId: number, stage: number) {
    if (stage === 5) {
      const reason = window.prompt('Why was this lead lost?')
      if (reason === null) return
      await run(leadId, stage, reason)
      return
    }
    await run(leadId, stage)
  }

  async function run(leadId: number, stage: number, lostReason?: string) {
    try {
      await actions.move.mutateAsync({ id: leadId, stage, lostReason })
      toast.success(`Moved to ${leadStageNames[stage]}`)
    } catch (error) {
      toast.error('Could not move the lead', describeErrorText(error))
    }
  }

  const stats = data?.stats

  return (
    <>
      <PageHeader
        eyebrow="CRM"
        title="Leads pipeline"
        lead="Every enquiry from the website, the desk and referrals — with the follow-up clock running."
        actions={
          <Button size="sm" icon="plus" onClick={() => setNewOpen(true)}>
            Add lead
          </Button>
        }
      >
        <div className="flex flex-wrap gap-2">
          <FilterChip active={!branchId} onClick={() => setBranchId(undefined)}>
            All branches
          </FilterChip>
          {(settings?.branches ?? []).map((branch) => (
            <FilterChip key={branch.id} active={branchId === branch.id} onClick={() => setBranchId(branch.id)}>
              {branch.name.replace('FORGE ', '')}
            </FilterChip>
          ))}
          <span className="mx-1 w-px self-stretch bg-[var(--hairline)]" />
          <FilterChip active={source === undefined} onClick={() => setSource(undefined)}>
            All sources
          </FilterChip>
          {leadSourceNames.map((name, index) => (
            <FilterChip
              key={name}
              active={source === index}
              onClick={() => setSource(source === index ? undefined : index)}
            >
              {name === 'WalkIn' ? 'Walk-in' : name}
            </FilterChip>
          ))}
        </div>
      </PageHeader>

      {stats && (
        <div className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
          <StatCard label="New this week" value={String(stats.newThisWeek)} icon="flag" />
          <StatCard
            label="Awaiting first reply"
            value={String(stats.awaitingFirstResponse)}
            icon="clock"
            tone={stats.awaitingFirstResponse > 0 ? 'warn' : 'neutral'}
          />
          <StatCard
            label="Overdue follow-ups"
            value={String(stats.overdueFollowUps)}
            icon="phone"
            tone={stats.overdueFollowUps > 0 ? 'warn' : 'neutral'}
          />
          <StatCard label="Joined this month" value={String(stats.joinedThisMonth)} icon="trophy" tone="accent" />
          <StatCard
            label="Median first reply"
            value={
              stats.medianFirstResponseMinutes < 60
                ? `${Math.round(stats.medianFirstResponseMinutes)} min`
                : `${(stats.medianFirstResponseMinutes / 60).toFixed(1)} h`
            }
            sub={`${stats.conversionRate}% convert`}
            icon="trending-up"
          />
        </div>
      )}

      {isLoading || !data ? (
        <div className="grid gap-4 lg:grid-cols-3 xl:grid-cols-6">
          {Array.from({ length: 6 }).map((_, index) => (
            <Skeleton key={index} className="h-96 w-full" />
          ))}
        </div>
      ) : (
        <div className="grid gap-4 lg:grid-cols-3 xl:grid-cols-6">
          {data.columns.map((column) => (
            <section
              key={column.stage}
              onDragOver={(event) => {
                event.preventDefault()
                setDropTarget(column.stage)
              }}
              onDragLeave={() => setDropTarget((current) => (current === column.stage ? null : current))}
              onDrop={(event) => {
                event.preventDefault()
                setDropTarget(null)
                if (dragging !== null) void move(dragging, column.stage)
                setDragging(null)
              }}
              className={cn(
                'flex min-h-[18rem] flex-col rounded-[var(--radius-card)] border bg-carbon transition-colors',
                dropTarget === column.stage
                  ? 'border-accent bg-[var(--accent-soft)]'
                  : 'border-[var(--hairline)]',
              )}
            >
              <header className="flex items-center gap-2 border-b border-[var(--hairline)] px-3.5 py-3">
                <span
                  aria-hidden
                  className="size-2 rounded-full"
                  style={{ background: stageTone[column.stage] }}
                />
                <h2 className="text-[0.8125rem] font-semibold">{column.name}</h2>
                <span className="numeric ml-auto text-[0.75rem] text-smoke">{column.total}</span>
              </header>

              <div className="min-h-0 flex-1 space-y-2 overflow-y-auto p-2.5">
                {column.cards.length === 0 && (
                  <p className="px-2 py-6 text-center text-[0.75rem] text-smoke">Nothing here.</p>
                )}
                {column.cards.map((card) => (
                  <BoardCard
                    key={card.id}
                    card={card}
                    onOpen={() => setOpenLead(card.id)}
                    onDragStart={() => setDragging(card.id)}
                    onDragEnd={() => setDragging(null)}
                    onMove={(stage) => void move(card.id, stage)}
                  />
                ))}
              </div>
            </section>
          ))}
        </div>
      )}

      {stats && stats.bySource.length > 0 && (
        <Panel className="mt-6" title="Conversion by source" description="Where the members actually come from.">
          <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {stats.bySource.map((row) => (
              <li key={row.source}>
                <div className="flex items-baseline justify-between gap-2">
                  <span className="text-[0.875rem]">{row.name}</span>
                  <span className="numeric text-[0.8125rem] text-smoke">
                    {row.joined}/{row.total}
                  </span>
                </div>
                <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-[var(--steel)]">
                  <div
                    className="h-full rounded-full bg-accent transition-[width] duration-500"
                    style={{ width: `${Math.min(100, row.conversionRate)}%` }}
                  />
                </div>
                <p className="numeric mt-1 text-[0.75rem] text-smoke">{row.conversionRate}% convert</p>
              </li>
            ))}
          </ul>
        </Panel>
      )}

      <LeadDrawer leadId={openLead} onClose={() => setOpenLead(null)} />
      <NewLeadDrawer open={newOpen} onClose={() => setNewOpen(false)} />
    </>
  )
}

/* ---------------------------------------------------------------- card */

function BoardCard({
  card,
  onOpen,
  onDragStart,
  onDragEnd,
  onMove,
}: {
  card: LeadCard
  onOpen: () => void
  onDragStart: () => void
  onDragEnd: () => void
  onMove: (stage: number) => void
}) {
  return (
    <article
      draggable
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
      className={cn(
        'group cursor-grab rounded-[0.625rem] border border-[var(--hairline)] bg-[color-mix(in_srgb,var(--bone)_3%,transparent)]',
        'p-3 transition-[border-color,transform] duration-200 hover:-translate-y-0.5 hover:border-[var(--accent-line)]',
        'active:cursor-grabbing',
        card.isOverdue && 'border-accent-hot/40',
      )}
    >
      <button type="button" onClick={onOpen} className="w-full text-left">
        <div className="flex items-start gap-2.5">
          <Avatar name={card.fullName} size={28} />
          <div className="min-w-0 flex-1">
            <p className="truncate text-[0.8125rem] font-medium">{card.fullName}</p>
            <p className="numeric truncate text-[0.6875rem] text-smoke">
              +91 {card.phone}
            </p>
          </div>
        </div>

        {card.goal && <p className="mt-2 line-clamp-2 text-[0.75rem] leading-relaxed text-smoke">{card.goal}</p>}

        <div className="mt-2.5 flex flex-wrap items-center gap-1.5">
          {card.branchName && <Pill tone="muted">{card.branchName.replace('FORGE ', '')}</Pill>}
          {card.interestedPlanName && <Pill tone="accent">{card.interestedPlanName}</Pill>}
          {card.isOverdue && <Pill tone="danger">overdue</Pill>}
          {!card.firstResponseAtUtc && card.stage < 4 && <Pill tone="warn">unanswered</Pill>}
        </div>

        <p className="numeric mt-2 text-[0.6875rem] text-smoke">
          {card.reference} · {card.ageDays}d old
          {card.nextFollowUpAtUtc ? ` · next ${relativeTime(card.nextFollowUpAtUtc)}` : ''}
        </p>
      </button>

      {/* Keyboard and touch path — drag alone is not an accessible way to move a card. */}
      <div className="mt-2 flex items-center gap-1 opacity-0 transition-opacity focus-within:opacity-100 group-hover:opacity-100">
        <button
          type="button"
          onClick={() => onMove(Math.max(0, card.stage - 1))}
          disabled={card.stage === 0}
          className="rounded-full p-1 text-smoke transition-colors hover:text-bone disabled:opacity-30"
          aria-label="Move back a stage"
        >
          <Icon name="chevron-left" size={14} />
        </button>
        <button
          type="button"
          onClick={() => onMove(Math.min(5, card.stage + 1))}
          disabled={card.stage >= 4}
          className="rounded-full p-1 text-smoke transition-colors hover:text-bone disabled:opacity-30"
          aria-label="Move forward a stage"
        >
          <Icon name="chevron-right" size={14} />
        </button>
        <a
          href={`tel:+91${card.phone}`}
          className="ml-auto rounded-full p-1 text-smoke transition-colors hover:text-accent"
          aria-label={`Call ${card.fullName}`}
        >
          <Icon name="phone" size={13} />
        </a>
        <a
          href={`https://wa.me/91${card.phone}`}
          target="_blank"
          rel="noreferrer noopener"
          className="rounded-full p-1 text-smoke transition-colors hover:text-success"
          aria-label={`WhatsApp ${card.fullName}`}
        >
          <Icon name="share" size={13} />
        </a>
      </div>
    </article>
  )
}

/* ---------------------------------------------------------------- detail */

function LeadDrawer({ leadId, onClose }: { leadId: number | null; onClose: () => void }) {
  const toast = useToast()
  const { data } = useLead(leadId)
  const actions = useLeadActions()
  const [convertOpen, setConvertOpen] = useState(false)
  const [note, setNote] = useState('')
  const [channel, setChannel] = useState('0')
  const [dueAt, setDueAt] = useState('')

  if (!leadId) return null

  async function addFollowUp() {
    if (!leadId || !dueAt) return
    try {
      await actions.addFollowUp.mutateAsync({
        id: leadId,
        body: { channel: Number(channel), dueAtUtc: new Date(dueAt).toISOString(), notes: note || undefined },
      })
      toast.success('Follow-up scheduled')
      setNote('')
      setDueAt('')
    } catch (error) {
      toast.error('Could not schedule', describeErrorText(error))
    }
  }

  async function complete(followUpId: number, outcome: string) {
    if (!leadId) return
    try {
      await actions.completeFollowUp.mutateAsync({ followUpId, leadId, body: { outcome } })
      toast.success('Follow-up closed')
    } catch (error) {
      toast.error('Could not close', describeErrorText(error))
    }
  }

  return (
    <>
      <Drawer
        open={leadId !== null}
        onClose={onClose}
        title={data?.card.fullName ?? 'Lead'}
        description={data ? `${data.card.reference} · ${leadSourceNames[data.card.source]} · ${data.card.ageDays} days old` : undefined}
        footer={
          data && data.card.stage !== 4 ? (
            <>
              <Button variant="ghost" size="sm" onClick={() => void actions.move.mutateAsync({ id: data.card.id, stage: 5, lostReason: 'Not interested' })}>
                Mark lost
              </Button>
              <div className="flex-1" />
              <Button size="sm" icon="check" onClick={() => setConvertOpen(true)}>
                Convert to member
              </Button>
            </>
          ) : data?.card.convertedMemberId ? (
            <Link
              to={`/admin/members/${data.card.convertedMemberId}`}
              className="inline-flex h-9 items-center gap-2 rounded-full bg-accent px-4 text-[0.8125rem] font-medium text-ink"
            >
              Open member record
              <Icon name="arrow-right" size={15} />
            </Link>
          ) : null
        }
      >
        {!data ? (
          <Skeleton className="h-64 w-full" />
        ) : (
          <div className="space-y-6">
            <div className="flex flex-wrap gap-2">
              <StatusPill status={leadStageNames[data.card.stage] ?? '—'} />
              {data.card.branchName && <Pill>{data.card.branchName}</Pill>}
              {data.card.interestedPlanName && <Pill tone="accent">{data.card.interestedPlanName}</Pill>}
              {data.sequenceActive && <Pill tone="success">Sequence running</Pill>}
            </div>

            <dl className="grid gap-x-6 gap-y-3 text-[0.875rem] sm:grid-cols-2">
              <Field label="Mobile" value={`+91 ${data.card.phone}`} href={`tel:+91${data.card.phone}`} />
              <Field label="Email" value={data.card.email ?? '—'} />
              <Field label="Source" value={data.card.sourceDetail ?? leadSourceNames[data.card.source]} />
              <Field label="Goal" value={data.card.goal ?? '—'} />
              <Field label="Trial requested" value={data.card.trialRequestedFor ?? '—'} />
              <Field label="Preferred time" value={data.preferredTime ?? '—'} />
              <Field label="Assigned to" value={data.card.assignedTo ?? 'Unassigned'} />
              <Field
                label="First response"
                value={data.card.firstResponseAtUtc ? formatIstDateTime(data.card.firstResponseAtUtc) : 'not yet'}
              />
            </dl>

            {data.message && (
              <div className="rounded-[0.625rem] border border-[var(--hairline)] p-4">
                <p className="caption mb-1.5">What they said</p>
                <p className="text-[0.875rem] leading-relaxed text-smoke">{data.message}</p>
              </div>
            )}

            <div>
              <h3 className="mb-3 text-[0.9375rem] font-semibold">Follow-ups</h3>
              <ul className="space-y-2">
                {data.followUps.map((followUp) => (
                  <li
                    key={followUp.id}
                    className={cn(
                      'flex items-start gap-3 rounded-[0.625rem] border p-3',
                      followUp.completedAtUtc
                        ? 'border-[var(--hairline)] opacity-60'
                        : followUp.isOverdue
                          ? 'border-accent-hot/40'
                          : 'border-[var(--hairline-strong)]',
                    )}
                  >
                    <Icon
                      name={followUp.channel === 0 ? 'phone' : followUp.channel === 3 ? 'mail' : 'share'}
                      size={15}
                      className="mt-0.5 shrink-0 text-smoke"
                    />
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-baseline gap-2">
                        <span className="text-[0.8125rem] font-medium">
                          {followUpChannelNames[followUp.channel]}
                        </span>
                        <span className="numeric text-[0.75rem] text-smoke">
                          {formatIstDateTime(followUp.dueAtUtc)}
                        </span>
                        {followUp.isAutomated && <Pill tone="muted">auto</Pill>}
                        {followUp.isOverdue && !followUp.completedAtUtc && <Pill tone="danger">overdue</Pill>}
                      </div>
                      {followUp.notes && <p className="mt-1 text-[0.8125rem] leading-relaxed text-smoke">{followUp.notes}</p>}
                      {followUp.outcome && <p className="mt-1 text-[0.75rem] text-success">{followUp.outcome}</p>}
                    </div>
                    {!followUp.completedAtUtc && (
                      <Button variant="ghost" size="sm" onClick={() => void complete(followUp.id, 'Done')}>
                        Done
                      </Button>
                    )}
                  </li>
                ))}
                {data.followUps.length === 0 && (
                  <li className="py-4 text-center text-[0.8125rem] text-smoke">No follow-ups scheduled.</li>
                )}
              </ul>

              <div className="mt-4 grid gap-3 rounded-[0.625rem] border border-dashed border-[var(--hairline-strong)] p-4 sm:grid-cols-[8rem_1fr_auto]">
                <SelectField label="Channel" value={channel} onChange={(event) => setChannel(event.target.value)}>
                  {followUpChannelNames.map((name, index) => (
                    <option key={name} value={index}>
                      {name}
                    </option>
                  ))}
                </SelectField>
                <TextField
                  label="Due"
                  type="datetime-local"
                  value={dueAt}
                  onChange={(event) => setDueAt(event.target.value)}
                />
                <div className="flex items-end">
                  <Button size="sm" icon="plus" onClick={() => void addFollowUp()} loading={actions.addFollowUp.isPending}>
                    Add
                  </Button>
                </div>
                <TextAreaField
                  label="Note"
                  rows={2}
                  className="sm:col-span-3"
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                />
              </div>
            </div>
          </div>
        )}
      </Drawer>

      {data && (
        <ConvertDrawer
          open={convertOpen}
          onClose={() => setConvertOpen(false)}
          lead={data.card}
          onDone={() => {
            setConvertOpen(false)
            onClose()
          }}
        />
      )}
    </>
  )
}

function Field({ label, value, href }: { label: string; value: string; href?: string }) {
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

/* ---------------------------------------------------------------- convert */

function ConvertDrawer({
  open,
  onClose,
  lead,
  onDone,
}: {
  open: boolean
  onClose: () => void
  lead: LeadCard
  onDone: () => void
}) {
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const { data: plans } = usePlans()
  const actions = useLeadActions()

  const [branchId, setBranchId] = useState(String(lead.branchId ?? ''))
  const [planId, setPlanId] = useState('')
  const [startsOn, setStartsOn] = useState(istToday())
  const [couponCode, setCouponCode] = useState('')
  const [collect, setCollect] = useState(true)
  const [collectMode, setCollectMode] = useState('1')
  const [error, setError] = useState<string | null>(null)

  async function submit() {
    if (!branchId) return setError('Pick a branch.')
    setError(null)

    try {
      const result = await actions.convert.mutateAsync({
        id: lead.id,
        body: {
          branchId: Number(branchId),
          email: lead.email ?? undefined,
          planId: planId ? Number(planId) : undefined,
          startsOn: planId ? startsOn : undefined,
          couponCode: couponCode || undefined,
          collectMode: planId && collect ? Number(collectMode) : undefined,
        },
      })
      toast.success(
        result.reusedExistingMember ? 'Linked to the existing member' : `Member ${result.memberCode} created`,
        result.sale ? `${result.sale.invoiceNumber} · ${formatInr(result.sale.grandTotal)}` : undefined,
      )
      onDone()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Convert to member"
      description={`${lead.fullName} · +91 ${lead.phone}. Creates the member and a portal login, and optionally sells the plan in the same step.`}
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button size="sm" icon="check" onClick={() => void submit()} loading={actions.convert.isPending}>
            Convert
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {error && <InlineError>{error}</InlineError>}
        <Hint icon="users">
          If this number already belongs to a member, the lead is linked to them rather than creating a
          duplicate person.
        </Hint>

        <SelectField label="Branch" required value={branchId} onChange={(event) => setBranchId(event.target.value)}>
          <option value="">Choose a branch</option>
          {(settings?.branches ?? []).map((branch) => (
            <option key={branch.id} value={branch.id}>
              {branch.name}
            </option>
          ))}
        </SelectField>

        <SelectField
          label="Sell a plan now"
          hint="Leave blank to create the member as a trial and sell later."
          value={planId}
          onChange={(event) => setPlanId(event.target.value)}
        >
          <option value="">Do not sell yet</option>
          {(plans ?? [])
            .filter((plan) => plan.isActive)
            .map((plan) => (
              <option key={plan.id} value={plan.id}>
                {plan.name} — {formatInr(plan.basePrice)}
              </option>
            ))}
        </SelectField>

        {planId && (
          <>
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField label="Starts on" type="date" value={startsOn} onChange={(event) => setStartsOn(event.target.value)} />
              <TextField
                label="Coupon"
                value={couponCode}
                onChange={(event) => setCouponCode(event.target.value.toUpperCase())}
              />
            </div>
            <div className="space-y-3 rounded-[0.625rem] border border-[var(--hairline)] p-4">
              <Toggle label="Collect payment now" checked={collect} onChange={setCollect} />
              {collect && (
                <SelectField label="Mode" value={collectMode} onChange={(event) => setCollectMode(event.target.value)}>
                  <option value="0">Cash</option>
                  <option value="1">UPI</option>
                  <option value="2">Card</option>
                  <option value="5">Razorpay link</option>
                </SelectField>
              )}
            </div>
          </>
        )}
      </div>
    </Drawer>
  )
}

/* ---------------------------------------------------------------- new lead */

function NewLeadDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const actions = useLeadActions()
  const [form, setForm] = useState<Record<string, string>>({ source: '1' })
  const [error, setError] = useState<string | null>(null)

  function set(key: string, value: string) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  async function submit() {
    setError(null)
    try {
      await actions.create.mutateAsync({
        fullName: form.fullName,
        phone: form.phone,
        email: form.email || undefined,
        branchId: form.branchId ? Number(form.branchId) : undefined,
        source: Number(form.source ?? 1),
        goal: form.goal || undefined,
        message: form.message || undefined,
      })
      toast.success('Lead added', 'A first follow-up call is scheduled for tomorrow.')
      setForm({ source: '1' })
      onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Add a lead"
      description="Walk-ins and phone enquiries. Website submissions arrive here on their own."
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button size="sm" icon="check" onClick={() => void submit()} loading={actions.create.isPending}>
            Add lead
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {error && <InlineError>{error}</InlineError>}
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField label="Full name" required value={form.fullName ?? ''} onChange={(event) => set('fullName', event.target.value)} />
          <TextField label="Mobile" required addon="+91" value={form.phone ?? ''} onChange={(event) => set('phone', event.target.value)} />
          <TextField label="Email" type="email" value={form.email ?? ''} onChange={(event) => set('email', event.target.value)} />
          <SelectField label="Branch" value={form.branchId ?? ''} onChange={(event) => set('branchId', event.target.value)}>
            <option value="">Not decided</option>
            {(settings?.branches ?? []).map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </SelectField>
          <SelectField label="Source" value={form.source ?? '1'} onChange={(event) => set('source', event.target.value)}>
            {leadSourceNames.map((name, index) => (
              <option key={name} value={index}>
                {name === 'WalkIn' ? 'Walk-in' : name}
              </option>
            ))}
          </SelectField>
          <TextField label="Goal" placeholder="Fat loss, strength…" value={form.goal ?? ''} onChange={(event) => set('goal', event.target.value)} />
        </div>
        <TextAreaField label="Notes" rows={3} value={form.message ?? ''} onChange={(event) => set('message', event.target.value)} />
      </div>
    </Drawer>
  )
}

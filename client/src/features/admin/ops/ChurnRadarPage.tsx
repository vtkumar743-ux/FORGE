import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { useSiteSettings } from '@/lib/cms'
import { cn, formatPhone } from '@/lib/utils'
import {
  Avatar,
  DataTable,
  FilterChip,
  Hint,
  PageHeader,
  Panel,
  RiskPill,
  StatCard,
  TextAreaField,
  TextField,
  Toggle,
} from '../components/ui'
import { Drawer } from '../components/overlays'
import { formatInr, relativeTime } from '../lib/format'
import {
  useBulkWinBack,
  useChurnRadar,
  useRescoreChurn,
  useWinBack,
  type ChurnRadarRow,
} from '../lib/module4-api'

/**
 * The churn-risk radar (Module 4.3).
 *
 * The score is not the point — the reasons are. Every row says why it was flagged, because
 * the next thing that happens is a person picking up a phone, and "score 71" is not
 * something you can open a conversation with.
 */
export function ChurnRadarPage() {
  const { data: settings } = useSiteSettings()
  const [branchId, setBranchId] = useState<number | undefined>()
  const [band, setBand] = useState<number | undefined>()
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [single, setSingle] = useState<ChurnRadarRow | null>(null)
  const [bulkOpen, setBulkOpen] = useState(false)

  const { data, isLoading } = useChurnRadar(branchId, band)
  const rescore = useRescoreChurn()

  const rows = data?.rows ?? []
  const allSelected = rows.length > 0 && rows.every((row) => selected.has(row.memberId))

  const toggle = (memberId: number) =>
    setSelected((current) => {
      const next = new Set(current)
      if (next.has(memberId)) next.delete(memberId)
      else next.add(memberId)
      return next
    })

  const scoredLabel = useMemo(
    () => (data?.scoredAtUtc ? `scored ${relativeTime(data.scoredAtUtc)}` : 'never scored'),
    [data?.scoredAtUtc],
  )

  return (
    <>
      <PageHeader
        eyebrow="Retention"
        title="Churn radar"
        lead="Members the rules say are drifting, worst first — with the reason to open with and the money attached."
        actions={
          <>
            <Button
              variant="ghost"
              size="sm"
              icon="loader"
              onClick={() => rescore.mutate()}
              disabled={rescore.isPending}
            >
              {rescore.isPending ? 'Re-scoring…' : 'Re-score now'}
            </Button>
            <Button
              size="sm"
              icon="mail"
              disabled={selected.size === 0}
              onClick={() => setBulkOpen(true)}
            >
              Win back {selected.size > 0 ? `(${selected.size})` : ''}
            </Button>
          </>
        }
      >
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <StatCard label="At risk — red" value={String(data?.red ?? 0)} sub="Score 65 and above" tone="warn" />
          <StatCard label="At risk — amber" value={String(data?.amber ?? 0)} sub="Score 40–64" tone="warn" />
          <StatCard label="Watch" value={String(data?.watch ?? 0)} sub="Score 20–39" />
          <StatCard
            label="Revenue at risk"
            value={formatInr(data?.revenueAtRisk ?? 0)}
            sub="Plan value on the flagged rows" tone="accent"
          />
        </div>
      </PageHeader>

      <Panel
        className="mb-5"
        title="Filters"
        description={`Radar ${scoredLabel}. It re-scores itself with the operations sweep every two hours.`}
      >
        <div className="flex flex-wrap gap-2">
          <FilterChip active={branchId === undefined} onClick={() => setBranchId(undefined)}>
            All branches
          </FilterChip>
          {(settings?.branches ?? []).map((branch) => (
            <FilterChip
              key={branch.slug}
              active={branchId === branch.id}
              onClick={() => setBranchId(branch.id)}
            >
              {branch.name.replace('FORGE ', '')}
            </FilterChip>
          ))}
          <span className="mx-1 w-px self-stretch bg-[var(--hairline)]" aria-hidden="true" />
          <FilterChip active={band === undefined} onClick={() => setBand(undefined)}>
            Amber + red
          </FilterChip>
          <FilterChip active={band === 3} onClick={() => setBand(3)}>
            Red only
          </FilterChip>
          <FilterChip active={band === 1} onClick={() => setBand(1)}>
            Watch
          </FilterChip>
        </div>
      </Panel>

      <Panel padded={false}>
        <DataTable
          rows={rows}
          rowKey={(row) => row.memberId}
          loading={isLoading}
          emptyHeadline="Nobody is drifting"
          emptyBody="No member in this filter is scoring amber or red. That is the number you want."
          columns={[
            {
              key: 'select',
              header: (
                <input
                  type="checkbox"
                  checked={allSelected}
                  aria-label="Select every row"
                  onChange={() =>
                    setSelected(allSelected ? new Set() : new Set(rows.map((row) => row.memberId)))
                  }
                  className="size-4 accent-[var(--accent)]"
                />
              ),
              width: '2.5rem',
              cell: (row) => (
                <input
                  type="checkbox"
                  checked={selected.has(row.memberId)}
                  aria-label={`Select ${row.fullName}`}
                  onChange={() => toggle(row.memberId)}
                  onClick={(event) => event.stopPropagation()}
                  className="size-4 accent-[var(--accent)]"
                />
              ),
            },
            {
              key: 'member',
              header: 'Member',
              cell: (row) => (
                <div className="flex items-center gap-3">
                  <Avatar src={row.photoUrl} name={row.fullName} />
                  <div className="min-w-0">
                    <Link
                      to={`/admin/members/${row.memberId}`}
                      className="block truncate font-medium hover:text-accent"
                      onClick={(event) => event.stopPropagation()}
                    >
                      {row.fullName}
                    </Link>
                    <p className="truncate text-[0.75rem] text-smoke">
                      {row.memberCode} · {row.branchName.replace('FORGE ', '')}
                    </p>
                  </div>
                </div>
              ),
            },
            {
              key: 'score',
              header: 'Risk',
              width: '9rem',
              cell: (row) => (
                <div className="flex items-center gap-2">
                  <RiskPill band={row.band} />
                  <span className="text-[0.8125rem] tabular-nums text-smoke">{row.score}</span>
                </div>
              ),
            },
            {
              key: 'reasons',
              header: 'Why',
              cell: (row) =>
                row.reasons.length === 0 ? (
                  <span className="text-smoke">—</span>
                ) : (
                  <ul className="space-y-0.5">
                    {row.reasons.slice(0, 3).map((reason) => (
                      <li key={reason} className="text-[0.8125rem] leading-snug">
                        {reason}
                      </li>
                    ))}
                  </ul>
                ),
            },
            {
              key: 'plan',
              header: 'Plan',
              width: '11rem',
              cell: (row) => (
                <div>
                  <p className="truncate text-[0.8125rem]">{row.planName ?? 'No active plan'}</p>
                  {row.planEndsOn && <p className="text-[0.75rem] text-smoke">ends {row.planEndsOn}</p>}
                  {row.amountDue > 0 && (
                    <p className="text-[0.75rem] text-[var(--accent-hot)]">{formatInr(row.amountDue)} due</p>
                  )}
                </div>
              ),
            },
            {
              key: 'action',
              header: '',
              width: '9rem',
              cell: (row) => (
                <Button
                  size="sm"
                  variant={row.lastWinBackAtUtc ? 'ghost' : 'outline'}
                  onClick={(event) => {
                    event.stopPropagation()
                    setSingle(row)
                  }}
                >
                  {row.lastWinBackAtUtc ? 'Sent' : 'Win back'}
                </Button>
              ),
            },
          ]}
        />
      </Panel>

      <WinBackDrawer row={single} onClose={() => setSingle(null)} />
      <BulkWinBackDrawer
        open={bulkOpen}
        memberIds={[...selected]}
        onClose={() => setBulkOpen(false)}
        onSent={() => setSelected(new Set())}
      />
    </>
  )
}

/* ---------------------------------------------------------------- drawers */

function useWinBackForm() {
  const [discountPercent, setDiscountPercent] = useState(20)
  const [offerValidDays, setOfferValidDays] = useState(14)
  const [message, setMessage] = useState('')
  const [sendWhatsApp, setSendWhatsApp] = useState(true)
  const [sendEmail, setSendEmail] = useState(true)
  const [force, setForce] = useState(false)

  return {
    values: { discountPercent, offerValidDays, message: message.trim() || undefined, sendWhatsApp, sendEmail, force },
    fields: {
      discountPercent,
      setDiscountPercent,
      offerValidDays,
      setOfferValidDays,
      message,
      setMessage,
      sendWhatsApp,
      setSendWhatsApp,
      sendEmail,
      setSendEmail,
      force,
      setForce,
    },
  }
}

function WinBackFields({ fields }: { fields: ReturnType<typeof useWinBackForm>['fields'] }) {
  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <TextField
          label="Discount"
          type="number"
          min={0}
          max={60}
          value={fields.discountPercent}
          onChange={(event) => fields.setDiscountPercent(Number(event.target.value))}
          hint="0 sends a message with no offer attached."
        />
        <TextField
          label="Offer valid for"
          type="number"
          min={3}
          max={90}
          value={fields.offerValidDays}
          onChange={(event) => fields.setOfferValidDays(Number(event.target.value))}
          hint="Days. A coupon is minted for this member only."
        />
      </div>

      <TextAreaField
        label="Message"
        rows={4}
        value={fields.message}
        onChange={(event) => fields.setMessage(event.target.value)}
        placeholder="Leave blank to use the default, which names the member, their branch and the offer."
      />

      <div className="space-y-2.5">
        <Toggle
          label="Send on WhatsApp"
          checked={fields.sendWhatsApp}
          onChange={fields.setSendWhatsApp}
        />
        <Toggle label="Send by email" checked={fields.sendEmail} onChange={fields.setSendEmail} />
        <Toggle
          label="Override the fortnight cool-off"
          checked={fields.force}
          onChange={fields.setForce}
        />
      </div>

      <Hint icon="sparkles">
        A call-back task lands on the desk alongside the message. A win-back nobody follows up on is
        a discount the gym pays for twice.
      </Hint>
    </div>
  )
}

function WinBackDrawer({ row, onClose }: { row: ChurnRadarRow | null; onClose: () => void }) {
  const { values, fields } = useWinBackForm()
  const winBack = useWinBack()
  const [result, setResult] = useState<string | null>(null)

  const send = () => {
    if (!row) return
    winBack.mutate(
      { memberId: row.memberId, ...values },
      {
        onSuccess: (outcome) => {
          setResult(outcome.message)
          setTimeout(() => {
            setResult(null)
            onClose()
          }, 1800)
        },
        onError: (error) => {
          const detail = (error as { response?: { data?: { message?: string } } })?.response?.data?.message
          setResult(detail ?? 'That did not send. Try again.')
        },
      },
    )
  }

  return (
    <Drawer
      open={row != null}
      onClose={onClose}
      title={row ? `Win back ${row.fullName}` : 'Win back'}
      description={row?.reasons.join(' · ')}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button icon="mail" onClick={send} disabled={winBack.isPending}>
            {winBack.isPending ? 'Sending…' : 'Send win-back'}
          </Button>
        </>
      }
    >
      {row && (
        <div className="mb-5 rounded-[var(--radius-input)] border border-[var(--hairline)] p-4">
          <p className="text-[0.8125rem] text-smoke">
            {row.memberCode} · {formatPhone(row.phone)}
            {row.lastVisitOn ? ` · last visit ${row.lastVisitOn}` : ' · never visited'}
          </p>
          {row.lastWinBackAtUtc && (
            <p className="mt-2 flex items-center gap-2 text-[0.8125rem] text-[var(--accent)]">
              <Icon name="clock" size={14} aria-hidden="true" />
              A win-back already went out on {new Date(row.lastWinBackAtUtc).toLocaleDateString('en-IN')}.
            </p>
          )}
        </div>
      )}

      <WinBackFields fields={fields} />

      {result && (
        <p
          className={cn(
            'mt-5 rounded-[var(--radius-input)] border p-3 text-[0.8125rem]',
            winBack.isError
              ? 'border-[var(--accent-hot)]/40 text-[var(--accent-hot)]'
              : 'border-[var(--success)]/40 text-[var(--success)]',
          )}
          role="status"
        >
          {result}
        </p>
      )}
    </Drawer>
  )
}

function BulkWinBackDrawer({
  open,
  memberIds,
  onClose,
  onSent,
}: {
  open: boolean
  memberIds: number[]
  onClose: () => void
  onSent: () => void
}) {
  const { values, fields } = useWinBackForm()
  const bulk = useBulkWinBack()
  const [result, setResult] = useState<string | null>(null)

  const send = () => {
    bulk.mutate(
      { memberIds, ...values },
      {
        onSuccess: (outcome) => {
          setResult(
            outcome.skipped === 0
              ? `Sent to ${outcome.sent}.`
              : `Sent to ${outcome.sent}. ${outcome.skipped} skipped — they had a win-back inside the last fortnight.`,
          )
          onSent()
        },
      },
    )
  }

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={`Win back ${memberIds.length} ${memberIds.length === 1 ? 'member' : 'members'}`}
      description="Everyone selected gets the same offer and the same message."
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Close
          </Button>
          <Button icon="mail" onClick={send} disabled={bulk.isPending || memberIds.length === 0}>
            {bulk.isPending ? 'Sending…' : `Send to ${memberIds.length}`}
          </Button>
        </>
      }
    >
      <WinBackFields fields={fields} />
      {result && (
        <p className="mt-5 rounded-[var(--radius-input)] border border-[var(--success)]/40 p-3 text-[0.8125rem] text-[var(--success)]" role="status">
          {result}
        </p>
      )}
    </Drawer>
  )
}

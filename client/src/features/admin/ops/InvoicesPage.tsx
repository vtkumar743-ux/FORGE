import { useMemo, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { useSiteSettings } from '@/lib/cms'
import { useBillingActions, useCollections, useInvoice, useInvoices, type InvoiceQuery } from '../lib/admin-api'
import { describeErrorText, formatInr, formatInrCompact, formatInrExact, formatIsoDate, formatIstDateTime } from '../lib/format'
import { invoiceStatusNames, paymentModeNames, paymentStatusNames } from '../lib/types'
import { useToast } from '../components/overlays'
import {
  DataTable,
  FilterChip,
  Hint,
  PageHeader,
  Pagination,
  Panel,
  Pill,
  StatCard,
  StatusPill,
  TextField,
} from '../components/ui'
import { RecordPaymentDrawer } from './billing-drawers'

/** The invoice ledger. Filters live in the URL so "Whitefield, unpaid" is a bookmark. */
export function InvoicesPage() {
  const [params, setParams] = useSearchParams()
  const { data: settings } = useSiteSettings()
  const [payFor, setPayFor] = useState<{ id: number; number: string; due: number; member: string } | null>(null)

  const query = useMemo<InvoiceQuery>(
    () => ({
      q: params.get('q') ?? undefined,
      branchId: params.get('branchId') ? Number(params.get('branchId')) : undefined,
      status: params.get('status') ? Number(params.get('status')) : undefined,
      unpaidOnly: params.get('unpaid') === '1' || undefined,
      page: Number(params.get('page') ?? 1),
      pageSize: 25,
    }),
    [params],
  )

  const { data, isLoading } = useInvoices(query)

  function setParam(key: string, value: string | undefined) {
    const next = new URLSearchParams(params)
    if (!value) next.delete(key)
    else next.set(key, value)
    if (key !== 'page') next.delete('page')
    setParams(next, { replace: true })
  }

  return (
    <>
      <PageHeader
        eyebrow="Money"
        title="Invoices"
        lead="Every GST invoice raised across the network, with what is still owed against it."
      >
        <div className="space-y-3">
          <TextField
            placeholder="Invoice number, member name, code or number"
            defaultValue={query.q ?? ''}
            onChange={(event) => setParam('q', event.target.value || undefined)}
            aria-label="Search invoices"
          />
          <div className="flex flex-wrap gap-2">
            <FilterChip active={!query.branchId} onClick={() => setParam('branchId', undefined)}>
              All branches
            </FilterChip>
            {(settings?.branches ?? []).map((branch) => (
              <FilterChip
                key={branch.id}
                active={query.branchId === branch.id}
                onClick={() => setParam('branchId', String(branch.id))}
              >
                {branch.name.replace('FORGE ', '')}
              </FilterChip>
            ))}
            <span className="mx-1 w-px self-stretch bg-[var(--hairline)]" />
            <FilterChip active={query.unpaidOnly === true} onClick={() => setParam('unpaid', query.unpaidOnly ? undefined : '1')}>
              Unpaid only
            </FilterChip>
            {[3, 4, 1, 2].map((status) => (
              <FilterChip
                key={status}
                active={query.status === status}
                onClick={() => setParam('status', query.status === status ? undefined : String(status))}
              >
                {invoiceStatusNames[status].replace(/([a-z])([A-Z])/g, '$1 $2')}
              </FilterChip>
            ))}
          </div>
        </div>
      </PageHeader>

      <Panel padded={false}>
        <DataTable
          rows={data?.items ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          emptyHeadline="No invoices match"
          emptyBody="Clear a filter, or sell a membership to raise the first one."
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
            {
              key: 'member',
              header: 'Member',
              cell: (row) => (
                <Link to={`/admin/members/${row.memberId}`}>
                  <p className="truncate font-medium">{row.memberName}</p>
                  <p className="numeric truncate text-[0.75rem] text-smoke">{row.memberCode}</p>
                </Link>
              ),
            },
            { key: 'plan', header: 'For', cell: (row) => <span className="text-smoke">{row.planName ?? 'Ad hoc'}</span> },
            { key: 'branch', header: 'Branch', cell: (row) => <span className="text-smoke">{row.branchName.replace('FORGE ', '')}</span> },
            { key: 'issued', header: 'Issued', cell: (row) => <span className="numeric text-[0.8125rem] text-smoke">{formatIsoDate(row.issuedOn)}</span> },
            {
              key: 'due',
              header: 'Due',
              cell: (row) => (
                <div>
                  <span className="numeric text-[0.8125rem] text-smoke">{formatIsoDate(row.dueOn)}</span>
                  {row.daysOverdue > 0 && (
                    <p className="numeric text-[0.75rem] text-accent-hot">{row.daysOverdue}d overdue</p>
                  )}
                </div>
              ),
            },
            { key: 'status', header: 'Status', cell: (row) => <StatusPill status={invoiceStatusNames[row.status] ?? '—'} /> },
            { key: 'total', header: 'Total', align: 'right', cell: (row) => <span className="numeric">{formatInr(row.grandTotal)}</span> },
            {
              key: 'outstanding',
              header: 'Outstanding',
              align: 'right',
              cell: (row) =>
                row.amountDue > 0 ? (
                  <span className="numeric font-medium text-accent-hot">{formatInr(row.amountDue)}</span>
                ) : (
                  <span className="text-success">settled</span>
                ),
            },
            {
              key: 'action',
              header: '',
              align: 'right',
              cell: (row) =>
                row.amountDue > 0 && row.status !== 5 ? (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() =>
                      setPayFor({ id: row.id, number: row.invoiceNumber, due: row.amountDue, member: row.memberName })
                    }
                  >
                    Collect
                  </Button>
                ) : null,
            },
          ]}
        />
        {data && (
          <Pagination
            page={data.page}
            pageCount={data.pageCount}
            total={data.total}
            pageSize={data.pageSize}
            onPage={(page) => setParam('page', String(page))}
          />
        )}
      </Panel>

      {payFor && (
        <RecordPaymentDrawer
          open
          onClose={() => setPayFor(null)}
          invoiceId={payFor.id}
          invoiceNumber={payFor.number}
          amountDue={payFor.due}
          memberName={payFor.member}
        />
      )}
    </>
  )
}

/* ---------------------------------------------------------------- detail */

/**
 * A GST invoice rendered as the document it is — supplier GSTIN, place of supply, SAC codes,
 * the CGST/SGST split per line and the round-off. Printable straight from the browser, which
 * is what a desk actually needs at the counter.
 */
export function InvoiceDetailPage() {
  const { id } = useParams()
  const invoiceId = Number(id)
  const toast = useToast()
  const { data, isLoading } = useInvoice(Number.isFinite(invoiceId) ? invoiceId : null)
  const actions = useBillingActions()
  const { data: settings } = useSiteSettings()
  const [payOpen, setPayOpen] = useState(false)

  if (isLoading || !data) return <Skeleton className="h-[40rem] w-full" />

  const { header } = data
  const brand = settings?.values['brand.name'] ?? 'FORGE'

  return (
    <>
      <PageHeader
        eyebrow={
          <>
            <Link to="/admin/billing/invoices" className="hover:text-accent">
              Invoices
            </Link>{' '}
            / {header.invoiceNumber}
          </>
        }
        title={header.invoiceNumber}
        actions={
          <>
            <Button variant="ghost" size="sm" icon="share" onClick={() => window.print()}>
              Print
            </Button>
            {header.amountDue > 0 && (
              <>
                <Button
                  variant="outline"
                  size="sm"
                  icon="mail"
                  onClick={() =>
                    void actions.remind
                      .mutateAsync(header.id)
                      .then(() => toast.success('Reminder sent'))
                      .catch((error) => toast.error('Could not send', describeErrorText(error)))
                  }
                  loading={actions.remind.isPending}
                >
                  Send reminder
                </Button>
                <Button size="sm" icon="check" onClick={() => setPayOpen(true)}>
                  Record payment
                </Button>
              </>
            )}
          </>
        }
      />

      <div className="grid gap-5 xl:grid-cols-[1fr_20rem]">
        <Panel className="min-w-0">
          {/* ---- document header ---- */}
          <div className="flex flex-wrap items-start justify-between gap-6 border-b border-[var(--hairline)] pb-6">
            <div>
              <div className="flex items-center gap-2.5">
                <Icon name="barbell" size={22} />
                <span className="font-display text-[1.125rem] font-semibold uppercase tracking-[0.02em]">{brand}</span>
              </div>
              <p className="mt-3 max-w-xs text-[0.8125rem] leading-relaxed text-smoke">{data.branchAddress}</p>
              {data.supplierGstin && (
                <p className="numeric mt-1.5 text-[0.8125rem] text-smoke">GSTIN {data.supplierGstin}</p>
              )}
            </div>
            <div className="text-right">
              <p className="caption">Tax invoice</p>
              <p className="numeric display-m mt-1.5 text-[1.375rem]">{header.invoiceNumber}</p>
              <p className="numeric mt-2 text-[0.8125rem] text-smoke">Issued {formatIsoDate(header.issuedOn)}</p>
              <p className="numeric text-[0.8125rem] text-smoke">Due {formatIsoDate(header.dueOn)}</p>
              <div className="mt-3 flex justify-end">
                <StatusPill status={invoiceStatusNames[header.status] ?? '—'} />
              </div>
            </div>
          </div>

          {/* ---- parties ---- */}
          <div className="grid gap-6 border-b border-[var(--hairline)] py-6 sm:grid-cols-2">
            <div>
              <p className="caption mb-2">Billed to</p>
              <Link to={`/admin/members/${header.memberId}`} className="text-[1rem] font-medium hover:text-accent">
                {header.memberName}
              </Link>
              <p className="numeric mt-1 text-[0.8125rem] text-smoke">{header.memberCode}</p>
              <p className="numeric text-[0.8125rem] text-smoke">+91 {data.memberPhone}</p>
              {data.memberEmail && <p className="text-[0.8125rem] text-smoke">{data.memberEmail}</p>}
              {data.customerGstin && <p className="numeric mt-1 text-[0.8125rem]">GSTIN {data.customerGstin}</p>}
            </div>
            <div className="sm:text-right">
              <p className="caption mb-2">Place of supply</p>
              <p className="text-[0.9375rem]">{data.placeOfSupply ?? '—'}</p>
              <p className="mt-3 text-[0.8125rem] text-smoke">{header.branchName}</p>
            </div>
          </div>

          {/* ---- lines ---- */}
          <div className="overflow-x-auto py-2">
            <table className="w-full border-collapse text-[0.875rem]">
              <thead>
                <tr className="border-b border-[var(--hairline)]">
                  {['Description', 'SAC/HSN', 'Qty', 'Rate', 'Taxable', 'CGST', 'SGST', 'Total'].map((label, index) => (
                    <th
                      key={label}
                      className={`whitespace-nowrap py-3 text-[0.6875rem] font-semibold uppercase tracking-[0.08em] text-smoke ${
                        index === 0 ? 'text-left' : 'text-right'
                      }`}
                    >
                      {label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.lines.map((line) => (
                  <tr key={line.id} className="border-b border-[var(--hairline)]">
                    <td className="py-3 pr-4">{line.description}</td>
                    <td className="numeric py-3 text-right text-smoke">{line.sacOrHsnCode ?? '—'}</td>
                    <td className="numeric py-3 text-right">{line.quantity}</td>
                    <td className="numeric py-3 text-right">{formatInrExact(line.unitPrice)}</td>
                    <td className="numeric py-3 text-right">{formatInrExact(line.taxableValue)}</td>
                    <td className="numeric py-3 text-right text-smoke">
                      {formatInrExact(line.cgstAmount)}
                      <span className="ml-1 text-[0.6875rem]">@{line.gstRatePercent / 2}%</span>
                    </td>
                    <td className="numeric py-3 text-right text-smoke">
                      {formatInrExact(line.sgstAmount)}
                      <span className="ml-1 text-[0.6875rem]">@{line.gstRatePercent / 2}%</span>
                    </td>
                    <td className="numeric py-3 text-right font-medium">{formatInrExact(line.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* ---- totals ---- */}
          <div className="flex justify-end pt-5">
            <dl className="w-full max-w-xs space-y-2 text-[0.875rem]">
              <Line label="Taxable value" value={formatInrExact(data.taxableValue)} />
              {data.discountTotal > 0 && <Line label="Discount" value={`− ${formatInrExact(data.discountTotal)}`} />}
              {data.cgstAmount > 0 && <Line label="CGST" value={formatInrExact(data.cgstAmount)} />}
              {data.sgstAmount > 0 && <Line label="SGST" value={formatInrExact(data.sgstAmount)} />}
              {data.igstAmount > 0 && <Line label="IGST" value={formatInrExact(data.igstAmount)} />}
              {data.roundOff !== 0 && <Line label="Round off" value={formatInrExact(data.roundOff)} />}
              <div className="flex items-baseline justify-between border-t border-[var(--hairline)] pt-2">
                <dt className="font-medium">Grand total</dt>
                <dd className="numeric display-m text-[1.375rem]">{formatInr(header.grandTotal)}</dd>
              </div>
              <Line label="Paid" value={formatInrExact(header.amountPaid)} tone="success" />
              {header.amountDue > 0 && <Line label="Outstanding" value={formatInrExact(header.amountDue)} tone="danger" />}
            </dl>
          </div>

          {data.notes && (
            <p className="mt-6 border-t border-[var(--hairline)] pt-4 text-[0.8125rem] leading-relaxed text-smoke">
              {data.notes}
            </p>
          )}
        </Panel>

        {/* ---- sidebar ---- */}
        <div className="space-y-5">
          <div className="grid gap-4">
            <StatCard label="Grand total" value={formatInrCompact(header.grandTotal)} icon="medal" />
            <StatCard
              label="Outstanding"
              value={formatInrCompact(header.amountDue)}
              sub={header.daysOverdue > 0 ? `${header.daysOverdue} days overdue` : undefined}
              tone={header.amountDue > 0 ? 'warn' : 'neutral'}
              icon="clock"
            />
          </div>

          <Panel title="Payments" padded={false}>
            {data.payments.length === 0 ? (
              <p className="px-5 py-6 text-center text-[0.8125rem] text-smoke">Nothing received yet.</p>
            ) : (
              <ul className="divide-y divide-[var(--hairline)]">
                {data.payments.map((payment) => (
                  <li key={payment.id} className="px-5 py-3.5">
                    <div className="flex items-baseline justify-between gap-3">
                      <span className="numeric font-medium">{formatInr(payment.amount)}</span>
                      <Pill tone={payment.status === 1 ? 'success' : payment.status === 0 ? 'warn' : 'danger'}>
                        {paymentStatusNames[payment.status]}
                      </Pill>
                    </div>
                    <p className="mt-1 text-[0.75rem] text-smoke">
                      {paymentModeNames[payment.mode]} · {formatIstDateTime(payment.paidAtUtc)}
                    </p>
                    {payment.gatewayPaymentId && (
                      <p className="numeric mt-0.5 truncate text-[0.6875rem] text-smoke">{payment.gatewayPaymentId}</p>
                    )}
                    {payment.chequeNumber && (
                      <p className="numeric mt-0.5 text-[0.6875rem] text-smoke">Cheque {payment.chequeNumber}</p>
                    )}
                    {payment.receivedBy && <p className="mt-0.5 text-[0.6875rem] text-smoke">by {payment.receivedBy}</p>}
                    {payment.gatewayName?.includes('simulator') && <Pill tone="warn" className="mt-1.5">simulated</Pill>}
                  </li>
                ))}
              </ul>
            )}
          </Panel>

          {header.remindersSent > 0 && (
            <Hint icon="mail">
              {header.remindersSent} reminder{header.remindersSent === 1 ? '' : 's'} sent so far. The dunning sweep
              chases weekly once an invoice is overdue.
            </Hint>
          )}
        </div>
      </div>

      <RecordPaymentDrawer
        open={payOpen}
        onClose={() => setPayOpen(false)}
        invoiceId={header.id}
        invoiceNumber={header.invoiceNumber}
        amountDue={header.amountDue}
        memberName={header.memberName}
      />
    </>
  )
}

function Line({ label, value, tone }: { label: string; value: string; tone?: 'success' | 'danger' }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="text-smoke">{label}</dt>
      <dd
        className={
          tone === 'success' ? 'numeric text-success' : tone === 'danger' ? 'numeric text-accent-hot' : 'numeric'
        }
      >
        {value}
      </dd>
    </div>
  )
}

/* ---------------------------------------------------------------- collections */

/**
 * The dunning dashboard: what is owed, aged into the buckets a collections conversation
 * actually uses, with the ladder runnable on demand rather than only on its six-hourly sweep.
 */
export function CollectionsPage() {
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const [branchId, setBranchId] = useState<number | undefined>()
  const { data, isLoading } = useCollections(branchId)
  const actions = useBillingActions()
  const [payFor, setPayFor] = useState<{ id: number; number: string; due: number; member: string } | null>(null)

  return (
    <>
      <PageHeader
        eyebrow="Money"
        title="Collections"
        lead="Outstanding dues, aged. Reminders go out at D-7, D-3, on the due date and weekly after that."
        actions={
          <Button
            size="sm"
            icon="mail"
            loading={actions.runCollections.isPending}
            onClick={() =>
              void actions.runCollections
                .mutateAsync()
                .then((result) => toast.success(`${result.remindersSent} reminder(s) sent`))
                .catch((error) => toast.error('Could not run collections', describeErrorText(error)))
            }
          >
            Run the ladder now
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
        </div>
      </PageHeader>

      <div className="mb-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <StatCard
          label="Total outstanding"
          value={formatInrCompact(data?.totalOutstanding ?? 0)}
          sub={`${data?.invoiceCount ?? 0} invoices`}
          tone="warn"
          icon="clock"
        />
        {(data?.ageing ?? []).map((bucket) => (
          <StatCard
            key={bucket.bucket}
            label={bucket.bucket}
            value={formatInrCompact(bucket.amount)}
            sub={`${bucket.count} invoice${bucket.count === 1 ? '' : 's'}`}
            icon="trending-up"
          />
        ))}
      </div>

      <Panel padded={false}>
        <DataTable
          rows={data?.invoices ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          emptyHeadline="Nothing outstanding"
          emptyBody="Every invoice on the book has been settled."
          columns={[
            {
              key: 'invoice',
              header: 'Invoice',
              cell: (row) => (
                <Link to={`/admin/billing/invoices/${row.id}`} className="numeric font-medium hover:text-accent">
                  {row.invoiceNumber}
                </Link>
              ),
            },
            {
              key: 'member',
              header: 'Member',
              cell: (row) => (
                <Link to={`/admin/members/${row.memberId}`}>
                  <p className="truncate font-medium">{row.memberName}</p>
                  <p className="numeric truncate text-[0.75rem] text-smoke">
                    {row.memberCode} · {row.branchName.replace('FORGE ', '')}
                  </p>
                </Link>
              ),
            },
            { key: 'bucket', header: 'Age', cell: (row) => <Pill tone={row.daysOverdue > 30 ? 'danger' : row.daysOverdue > 0 ? 'warn' : 'muted'}>{row.bucket}</Pill> },
            { key: 'due', header: 'Due', cell: (row) => <span className="numeric text-[0.8125rem] text-smoke">{formatIsoDate(row.dueOn)}</span> },
            {
              key: 'amount',
              header: 'Outstanding',
              align: 'right',
              cell: (row) => <span className="numeric font-medium text-accent-hot">{formatInr(row.amountDue)}</span>,
            },
            {
              key: 'reminders',
              header: 'Chased',
              align: 'right',
              cell: (row) => (
                <span className="numeric text-[0.8125rem] text-smoke">
                  {row.remindersSent}×{row.lastReminderAtUtc ? ` · ${formatIstDateTime(row.lastReminderAtUtc)}` : ''}
                </span>
              ),
            },
            {
              key: 'actions',
              header: '',
              align: 'right',
              cell: (row) => (
                <div className="flex justify-end gap-1.5">
                  <a
                    href={`https://wa.me/91${row.phone}`}
                    target="_blank"
                    rel="noreferrer noopener"
                    className="inline-flex size-8 items-center justify-center rounded-full border border-[var(--hairline-strong)] text-smoke transition-colors hover:border-success/50 hover:text-success"
                    aria-label={`WhatsApp ${row.memberName}`}
                  >
                    <Icon name="share" size={14} />
                  </a>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() =>
                      setPayFor({ id: row.id, number: row.invoiceNumber, due: row.amountDue, member: row.memberName })
                    }
                  >
                    Collect
                  </Button>
                </div>
              ),
            },
          ]}
        />
      </Panel>

      {payFor && (
        <RecordPaymentDrawer
          open
          onClose={() => setPayFor(null)}
          invoiceId={payFor.id}
          invoiceNumber={payFor.number}
          amountDue={payFor.due}
          memberName={payFor.member}
        />
      )}
    </>
  )
}

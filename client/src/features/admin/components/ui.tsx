import {
  forwardRef,
  useId,
  type InputHTMLAttributes,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from 'react'
import { Icon, type IconName } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { cn } from '@/lib/utils'

/* ============================================================================
   Admin UI kit

   The admin panel runs the light surface (03 §2) on the same tokens as the
   public site, so everything here is built from `--carbon` / `--bone` /
   `--hairline` and inherits the theme swap from `.theme-light` on the shell.
   Owners spend hours in these views: density is higher, motion is shorter, and
   every list has a real empty state rather than a blank panel (03 §9.5).
   ============================================================================ */

export function PageHeader({
  eyebrow,
  title,
  lead,
  actions,
  children,
}: {
  eyebrow?: ReactNode
  title: string
  lead?: string
  actions?: ReactNode
  children?: ReactNode
}) {
  return (
    <div className="mb-8">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div className="min-w-0">
          {eyebrow && <p className="caption">{eyebrow}</p>}
          <h1 className="display-m mt-2 text-[1.75rem] leading-none">{title}</h1>
          {lead && <p className="measure mt-2.5 text-[0.9375rem] leading-relaxed text-smoke">{lead}</p>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2.5">{actions}</div>}
      </div>
      {children && <div className="mt-6">{children}</div>}
    </div>
  )
}

/* ---------------------------------------------------------------- surfaces */

export function Panel({
  title,
  description,
  actions,
  children,
  className,
  padded = true,
}: {
  title?: string
  description?: string
  actions?: ReactNode
  children: ReactNode
  className?: string
  padded?: boolean
}) {
  return (
    <section
      className={cn(
        'overflow-hidden rounded-[var(--radius-card)] border border-[var(--hairline)] bg-carbon',
        className,
      )}
    >
      {(title || actions) && (
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--hairline)] px-5 py-4">
          <div className="min-w-0">
            {title && <h2 className="text-[0.9375rem] font-semibold tracking-[-0.01em]">{title}</h2>}
            {description && <p className="mt-1 text-[0.8125rem] leading-relaxed text-smoke">{description}</p>}
          </div>
          {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
        </header>
      )}
      <div className={cn(padded && 'p-5')}>{children}</div>
    </section>
  )
}

export function StatCard({
  label,
  value,
  sub,
  delta,
  icon,
  tone = 'neutral',
  onClick,
}: {
  label: string
  value: string
  sub?: string
  /** Percent change against the comparison period; drives the arrow and colour. */
  delta?: number | null
  icon?: IconName
  tone?: 'neutral' | 'accent' | 'warn'
  onClick?: () => void
}) {
  const Tag = onClick ? 'button' : 'div'
  return (
    <Tag
      type={onClick ? 'button' : undefined}
      onClick={onClick}
      className={cn(
        'group relative w-full overflow-hidden rounded-[var(--radius-card)] border p-5 text-left',
        'transition-[border-color,transform] duration-200 ease-out',
        tone === 'accent'
          ? 'border-[var(--accent-line)] bg-[color-mix(in_srgb,var(--accent)_7%,var(--carbon))]'
          : tone === 'warn'
            ? 'border-accent-hot/35 bg-[color-mix(in_srgb,var(--accent-hot)_5%,var(--carbon))]'
            : 'border-[var(--hairline)] bg-carbon',
        onClick && 'hover:-translate-y-0.5 hover:border-[var(--accent-line)] motion-reduce:hover:translate-y-0',
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <p className="caption">{label}</p>
        {icon && <Icon name={icon} size={18} className="text-smoke" />}
      </div>
      <p className="numeric display-m mt-3 text-[1.75rem] leading-none">{value}</p>
      <div className="mt-2 flex items-center gap-2">
        {typeof delta === 'number' && Number.isFinite(delta) && (
          <span
            className={cn(
              'numeric inline-flex items-center gap-1 text-[0.75rem] font-medium',
              delta > 0 ? 'text-success' : delta < 0 ? 'text-accent-hot' : 'text-smoke',
            )}
          >
            <Icon
              name={delta >= 0 ? 'trending-up' : 'trending-up'}
              size={13}
              className={delta < 0 ? 'rotate-180' : undefined}
            />
            {Math.abs(delta).toFixed(delta % 1 === 0 ? 0 : 1)}%
          </span>
        )}
        {sub && <span className="text-[0.75rem] text-smoke">{sub}</span>}
      </div>
    </Tag>
  )
}

/* ---------------------------------------------------------------- status */

type Tone = 'neutral' | 'success' | 'warn' | 'danger' | 'accent' | 'muted'

const toneClasses: Record<Tone, string> = {
  neutral: 'border-[var(--hairline-strong)] text-smoke',
  success: 'border-success/45 text-success bg-[color-mix(in_srgb,var(--success)_10%,transparent)]',
  warn: 'border-[var(--accent-line)] text-[color-mix(in_srgb,var(--accent)_70%,var(--bone))] bg-[var(--accent-soft)]',
  danger: 'border-accent-hot/45 text-accent-hot bg-[color-mix(in_srgb,var(--accent-hot)_9%,transparent)]',
  accent: 'border-[var(--accent-line)] text-accent bg-[var(--accent-soft)]',
  muted: 'border-[var(--hairline)] text-smoke',
}

export function Pill({
  children,
  tone = 'neutral',
  icon,
  className,
}: {
  children: ReactNode
  tone?: Tone
  icon?: IconName
  className?: string
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border px-2.5 py-1',
        'text-[0.6875rem] font-medium uppercase tracking-[0.06em]',
        toneClasses[tone],
        className,
      )}
    >
      {icon && <Icon name={icon} size={11} strokeWidth={2} />}
      {children}
    </span>
  )
}

/** One vocabulary for every status word in the panel, so colour always means the same thing. */
export function StatusPill({ status }: { status: string }) {
  const map: Record<string, Tone> = {
    Active: 'success',
    Paid: 'success',
    Attended: 'success',
    Published: 'success',
    Joined: 'success',
    Captured: 'success',
    Completed: 'success',
    Trial: 'accent',
    Booked: 'accent',
    Scheduled: 'accent',
    Issued: 'accent',
    Inquiry: 'accent',
    Frozen: 'warn',
    Waitlisted: 'warn',
    PartiallyPaid: 'warn',
    Pending: 'warn',
    Draft: 'warn',
    Negotiation: 'warn',
    Tour: 'warn',
    Overdue: 'danger',
    Expired: 'danger',
    Cancelled: 'danger',
    NoShow: 'danger',
    Lost: 'danger',
    Failed: 'danger',
    Lead: 'muted',
    Refunded: 'muted',
  }

  const label = status.replace(/([a-z])([A-Z])/g, '$1 $2')
  return <Pill tone={map[status] ?? 'neutral'}>{label}</Pill>
}

export function RiskPill({ band }: { band: number | string }) {
  const name = typeof band === 'number' ? ['Healthy', 'Watch', 'Amber', 'Red'][band] ?? 'Healthy' : band
  const tone: Tone = name === 'Red' ? 'danger' : name === 'Amber' ? 'warn' : name === 'Watch' ? 'neutral' : 'success'
  return <Pill tone={tone}>{name}</Pill>
}

/* ---------------------------------------------------------------- fields */

function FieldShell({
  id,
  label,
  hint,
  error,
  required,
  children,
  className,
}: {
  id: string
  label?: string
  hint?: string
  error?: string
  required?: boolean
  children: ReactNode
  className?: string
}) {
  return (
    <div className={cn('min-w-0', className)}>
      {label && (
        <label htmlFor={id} className="mb-1.5 block text-[0.8125rem] font-medium text-bone">
          {label}
          {required && <span className="ml-1 text-accent-hot">*</span>}
        </label>
      )}
      {children}
      {error ? (
        <p id={`${id}-error`} className="mt-1.5 text-[0.75rem] text-accent-hot">
          {error}
        </p>
      ) : (
        hint && (
          <p id={`${id}-hint`} className="mt-1.5 text-[0.75rem] leading-relaxed text-smoke">
            {hint}
          </p>
        )
      )}
    </div>
  )
}

/** Admin inputs are shorter and squarer than the public pills — tables, not landing pages. */
const control =
  'w-full rounded-[0.625rem] border bg-[color-mix(in_srgb,var(--bone)_4%,var(--carbon))] px-3 text-[0.875rem] ' +
  'text-bone transition-[border-color,box-shadow] duration-200 ease-out placeholder:text-smoke ' +
  'focus:border-accent focus:outline-none focus:ring-[3px] focus:ring-[var(--accent-soft)] ' +
  'disabled:cursor-not-allowed disabled:opacity-50'

export interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'className'> {
  label?: string
  hint?: string
  error?: string
  className?: string
  addon?: ReactNode
}

export const TextField = forwardRef<HTMLInputElement, TextFieldProps>(function TextField(
  { label, hint, error, className, required, addon, ...rest },
  ref,
) {
  const generated = useId()
  const id = rest.id ?? generated
  return (
    <FieldShell id={id} label={label} hint={hint} error={error} required={required} className={className}>
      <div className="relative">
        <input
          ref={ref}
          id={id}
          required={required}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          className={cn(
            control,
            'h-10',
            addon ? 'pr-10' : undefined,
            error ? 'border-accent-hot' : 'border-[var(--field-line)]',
          )}
          {...rest}
        />
        {addon && (
          <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-[0.75rem] text-smoke">
            {addon}
          </span>
        )}
      </div>
    </FieldShell>
  )
})

export interface SelectFieldProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, 'className'> {
  label?: string
  hint?: string
  error?: string
  className?: string
  children: ReactNode
}

export const SelectField = forwardRef<HTMLSelectElement, SelectFieldProps>(function SelectField(
  { label, hint, error, className, required, children, ...rest },
  ref,
) {
  const generated = useId()
  const id = rest.id ?? generated
  return (
    <FieldShell id={id} label={label} hint={hint} error={error} required={required} className={className}>
      <select
        ref={ref}
        id={id}
        required={required}
        aria-invalid={error ? true : undefined}
        className={cn(
          control,
          'h-10 appearance-none bg-no-repeat pr-9',
          error ? 'border-accent-hot' : 'border-[var(--field-line)]',
        )}
        style={{
          // Inline SVG chevron — no icon font, and it picks up the smoke token.
          backgroundImage:
            "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='18' height='18' viewBox='0 0 24 24' fill='none' stroke='%239CA3AF' stroke-width='1.5' stroke-linecap='round'%3E%3Cpath d='m6 9 6 6 6-6'/%3E%3C/svg%3E\")",
          backgroundPosition: 'right 0.6rem center',
        }}
        {...rest}
      >
        {children}
      </select>
    </FieldShell>
  )
})

export interface TextAreaFieldProps extends Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'className'> {
  label?: string
  hint?: string
  error?: string
  className?: string
}

export const TextAreaField = forwardRef<HTMLTextAreaElement, TextAreaFieldProps>(function TextAreaField(
  { label, hint, error, className, required, rows = 4, ...rest },
  ref,
) {
  const generated = useId()
  const id = rest.id ?? generated
  return (
    <FieldShell id={id} label={label} hint={hint} error={error} required={required} className={className}>
      <textarea
        ref={ref}
        id={id}
        rows={rows}
        required={required}
        aria-invalid={error ? true : undefined}
        className={cn(
          control,
          'resize-y py-2.5 leading-relaxed',
          error ? 'border-accent-hot' : 'border-[var(--field-line)]',
        )}
        {...rest}
      />
    </FieldShell>
  )
})

export function Toggle({
  label,
  hint,
  checked,
  onChange,
  disabled,
}: {
  label: string
  hint?: string
  checked: boolean
  onChange: (value: boolean) => void
  disabled?: boolean
}) {
  const id = useId()
  return (
    <div className="flex items-start justify-between gap-4">
      <div className="min-w-0">
        <label htmlFor={id} className="block cursor-pointer text-[0.8125rem] font-medium text-bone">
          {label}
        </label>
        {hint && <p className="mt-1 text-[0.75rem] leading-relaxed text-smoke">{hint}</p>}
      </div>
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        aria-label={label}
        disabled={disabled}
        onClick={() => onChange(!checked)}
        className={cn(
          'relative mt-0.5 h-5 w-9 shrink-0 rounded-full border transition-colors duration-200 ease-out',
          'disabled:cursor-not-allowed disabled:opacity-50',
          checked ? 'border-accent bg-accent' : 'border-[var(--field-line)] bg-[var(--steel)]',
        )}
      >
        <span
          className={cn(
            'absolute top-1/2 size-3.5 -translate-y-1/2 rounded-full transition-[left] duration-200 ease-out',
            checked ? 'left-[1.15rem] bg-ink' : 'left-[0.15rem] bg-smoke',
          )}
        />
      </button>
    </div>
  )
}

/** Filter chips across the top of every list — one shape, used everywhere. */
export function FilterChip({
  active,
  onClick,
  children,
  count,
}: {
  active: boolean
  onClick: () => void
  children: ReactNode
  count?: number
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-[0.8125rem] font-medium',
        'transition-[background-color,border-color,color] duration-200 ease-out',
        active
          ? 'border-accent bg-accent text-ink'
          : 'border-[var(--hairline-strong)] text-smoke hover:border-[var(--accent-line)] hover:text-bone',
      )}
    >
      {children}
      {typeof count === 'number' && (
        <span className={cn('numeric text-[0.6875rem]', active ? 'text-ink/70' : 'text-smoke')}>{count}</span>
      )}
    </button>
  )
}

/* ---------------------------------------------------------------- table */

export interface Column<T> {
  key: string
  header: ReactNode
  /** Right-aligned for money and counts so digits line up down the column. */
  align?: 'left' | 'right' | 'center'
  width?: string
  cell: (row: T) => ReactNode
}

export function DataTable<T>({
  columns,
  rows,
  rowKey,
  onRowClick,
  loading,
  emptyHeadline = 'Nothing here yet',
  emptyBody,
  emptyAction,
  skeletonRows = 8,
  dense,
}: {
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string | number
  onRowClick?: (row: T) => void
  loading?: boolean
  emptyHeadline?: string
  emptyBody?: string
  emptyAction?: ReactNode
  skeletonRows?: number
  dense?: boolean
}) {
  if (loading) {
    return (
      <div className="space-y-px" role="status" aria-live="polite">
        <span className="sr-only">Loading</span>
        {Array.from({ length: skeletonRows }).map((_, index) => (
          <Skeleton key={index} className="h-12 w-full" rounded="none" />
        ))}
      </div>
    )
  }

  if (rows.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center px-8 py-16 text-center">
        <span className="mb-4 inline-flex size-11 items-center justify-center rounded-full border border-[var(--hairline-strong)] text-accent">
          <Icon name="sparkles" size={20} />
        </span>
        <p className="text-[0.9375rem] font-medium text-bone">{emptyHeadline}</p>
        {emptyBody && <p className="measure mt-1.5 text-[0.8125rem] leading-relaxed text-smoke">{emptyBody}</p>}
        {emptyAction && <div className="mt-5">{emptyAction}</div>}
      </div>
    )
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-[0.875rem]">
        <thead>
          <tr className="border-b border-[var(--hairline)]">
            {columns.map((column) => (
              <th
                key={column.key}
                scope="col"
                style={{ width: column.width }}
                className={cn(
                  'whitespace-nowrap px-4 py-3 text-[0.6875rem] font-semibold uppercase tracking-[0.08em] text-smoke',
                  column.align === 'right' && 'text-right',
                  column.align === 'center' && 'text-center',
                  (!column.align || column.align === 'left') && 'text-left',
                )}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr
              key={rowKey(row)}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              className={cn(
                'border-b border-[var(--hairline)] last:border-0',
                onRowClick && 'cursor-pointer transition-colors duration-150 hover:bg-[var(--accent-soft)]',
              )}
            >
              {columns.map((column) => (
                <td
                  key={column.key}
                  className={cn(
                    dense ? 'px-4 py-2' : 'px-4 py-3',
                    column.align === 'right' && 'text-right',
                    column.align === 'center' && 'text-center',
                  )}
                >
                  {column.cell(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function Pagination({
  page,
  pageCount,
  total,
  pageSize,
  onPage,
}: {
  page: number
  pageCount: number
  total: number
  pageSize: number
  onPage: (page: number) => void
}) {
  if (total === 0) return null
  const first = (page - 1) * pageSize + 1
  const last = Math.min(total, page * pageSize)

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--hairline)] px-5 py-3">
      <p className="numeric text-[0.8125rem] text-smoke">
        {first}–{last} of {total.toLocaleString('en-IN')}
      </p>
      <div className="flex items-center gap-1.5">
        <button
          type="button"
          onClick={() => onPage(page - 1)}
          disabled={page <= 1}
          className="inline-flex size-8 items-center justify-center rounded-full border border-[var(--hairline-strong)] text-smoke transition-colors hover:border-[var(--accent-line)] hover:text-bone disabled:pointer-events-none disabled:opacity-40"
          aria-label="Previous page"
        >
          <Icon name="chevron-left" size={16} />
        </button>
        <span className="numeric px-2 text-[0.8125rem] text-smoke">
          {page} / {Math.max(1, pageCount)}
        </span>
        <button
          type="button"
          onClick={() => onPage(page + 1)}
          disabled={page >= pageCount}
          className="inline-flex size-8 items-center justify-center rounded-full border border-[var(--hairline-strong)] text-smoke transition-colors hover:border-[var(--accent-line)] hover:text-bone disabled:pointer-events-none disabled:opacity-40"
          aria-label="Next page"
        >
          <Icon name="chevron-right" size={16} />
        </button>
      </div>
    </div>
  )
}

/* ---------------------------------------------------------------- misc */

export function Avatar({ src, name, size = 32 }: { src?: string | null; name: string; size?: number }) {
  const initials = name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')

  return src ? (
    <img
      src={src}
      alt=""
      width={size}
      height={size}
      loading="lazy"
      className="graded shrink-0 rounded-full object-cover"
      style={{ width: size, height: size }}
    />
  ) : (
    <span
      aria-hidden
      className="inline-flex shrink-0 items-center justify-center rounded-full border border-[var(--hairline-strong)] bg-[var(--steel)] font-medium text-smoke"
      style={{ width: size, height: size, fontSize: size * 0.36 }}
    >
      {initials || '—'}
    </span>
  )
}

export function InlineError({ children }: { children: ReactNode }) {
  if (!children) return null
  return (
    <div className="flex items-start gap-2.5 rounded-[var(--radius-card)] border border-accent-hot/40 bg-[color-mix(in_srgb,var(--accent-hot)_8%,transparent)] px-4 py-3">
      <Icon name="x" size={16} className="mt-0.5 shrink-0 text-accent-hot" />
      <p className="text-[0.8125rem] leading-relaxed text-bone">{children}</p>
    </div>
  )
}

export function Hint({ icon = 'sparkles', children }: { icon?: IconName; children: ReactNode }) {
  return (
    <div className="flex items-start gap-2.5 rounded-[var(--radius-card)] border border-[var(--accent-line)] bg-[var(--accent-soft)] px-4 py-3">
      <Icon name={icon} size={16} className="mt-0.5 shrink-0 text-accent" />
      <div className="text-[0.8125rem] leading-relaxed text-bone">{children}</div>
    </div>
  )
}

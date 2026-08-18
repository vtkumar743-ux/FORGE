import axios from 'axios'
import { describeError } from '@/lib/api'

export { cn, formatInr, formatInrExact, formatCount, formatDate, formatClock } from '@/lib/utils'

/**
 * Turns any failure into one line an owner can act on. ProblemDetails validation
 * errors are flattened first, because "Enter a 10-digit mobile number" is useful and
 * "Request failed with status code 400" is not.
 */
export function describeErrorText(error: unknown, fallback = 'That did not go through. Try again.'): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as
      | { errors?: Record<string, string[]>; detail?: string; title?: string; conflicts?: { message: string }[] }
      | undefined

    if (data?.conflicts?.length) return data.conflicts.map((c) => c.message).join(' ')
    if (data?.errors) {
      const messages = Object.values(data.errors).flat()
      if (messages.length > 0) return messages.join(' ')
    }
  }
  return describeError(error, fallback)
}

/** "₹1.24L" / "₹8.6Cr" — Indian short scale, for KPI tiles where the full figure will not fit. */
export function formatInrCompact(amount: number): string {
  const abs = Math.abs(amount)
  if (abs >= 1_00_00_000) return `₹${(amount / 1_00_00_000).toFixed(2)}Cr`
  if (abs >= 1_00_000) return `₹${(amount / 1_00_000).toFixed(2)}L`
  if (abs >= 1_000) return `₹${(amount / 1_000).toFixed(1)}K`
  return `₹${Math.round(amount).toLocaleString('en-IN')}`
}

/** Percentage change between two periods; null when there is no baseline to compare to. */
export function delta(current: number, previous: number): number | null {
  if (!previous) return null
  return ((current - previous) / Math.abs(previous)) * 100
}

/** "3 min ago" / "in 2 h" / "yesterday" — relative, because a desk reads recency, not clocks. */
export function relativeTime(value: string | Date | null | undefined): string {
  if (!value) return '—'
  const date = typeof value === 'string' ? new Date(value) : value
  const seconds = Math.round((date.getTime() - Date.now()) / 1000)
  const abs = Math.abs(seconds)

  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['second', 60],
    ['minute', 3600],
    ['hour', 86_400],
    ['day', 604_800],
    ['week', 2_629_800],
    ['month', 31_557_600],
  ]

  const formatter = new Intl.RelativeTimeFormat('en-IN', { numeric: 'auto' })
  let divisor = 1
  for (const [unit, limit] of units) {
    if (abs < limit) return formatter.format(Math.round(seconds / divisor), unit)
    divisor = limit
  }
  return formatter.format(Math.round(seconds / 31_557_600), 'year')
}

/** "17 Aug 2026" from a yyyy-MM-dd string, without dragging it through a timezone. */
export function formatIsoDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  const [year, month, day] = iso.split('-').map(Number)
  if (!year || !month || !day) return iso
  return new Intl.DateTimeFormat('en-IN', { day: 'numeric', month: 'short', year: 'numeric' }).format(
    new Date(year, month - 1, day),
  )
}

/** "17 Aug, 6:30 PM" in IST for a UTC instant off the API. */
export function formatIstDateTime(utc: string | Date | null | undefined): string {
  if (!utc) return '—'
  const date = typeof utc === 'string' ? new Date(utc) : utc
  return new Intl.DateTimeFormat('en-IN', {
    day: 'numeric',
    month: 'short',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
    timeZone: 'Asia/Kolkata',
  }).format(date)
}

export function formatIstTime(utc: string | Date | null | undefined): string {
  if (!utc) return '—'
  const date = typeof utc === 'string' ? new Date(utc) : utc
  return new Intl.DateTimeFormat('en-IN', {
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
    timeZone: 'Asia/Kolkata',
  }).format(date)
}

/** Today in IST as yyyy-MM-dd — the default for every date input in the panel. */
export function istToday(offsetDays = 0): string {
  const now = new Date(Date.now() + offsetDays * 86_400_000)
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Kolkata' }).format(now)
}

/** "6:30 PM" from an "18:30" wall clock. */
export function to12Hour(hhmm: string): string {
  const [hours, minutes] = hhmm.split(':')
  const hour = Number(hours)
  if (Number.isNaN(hour)) return hhmm
  const suffix = hour < 12 ? 'AM' : 'PM'
  return `${hour % 12 === 0 ? 12 : hour % 12}:${minutes} ${suffix}`
}

export function initials(name: string): string {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

/** Conditional class names without pulling in a dependency for it. */
export function cn(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(' ')
}

const inr = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  maximumFractionDigits: 0,
})

/** ₹19,900 — Indian digit grouping, no decimals on whole rupees. */
export function formatInr(amount: number): string {
  return inr.format(amount)
}

const inrWithPaise = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  minimumFractionDigits: 2,
})

/** Invoice lines and tax break-ups need the paise. */
export function formatInrExact(amount: number): string {
  return inrWithPaise.format(amount)
}

export function formatCount(value: number): string {
  return new Intl.NumberFormat('en-IN').format(value)
}

/** "17 Aug 2026" */
export function formatDate(value: string | Date): string {
  const date = typeof value === 'string' ? new Date(value) : value
  return new Intl.DateTimeFormat('en-IN', { day: 'numeric', month: 'short', year: 'numeric' }).format(date)
}

/** "6:30 PM" — class times read better in 12-hour form to Indian members. */
export function formatTime(value: string | Date): string {
  const date = typeof value === 'string' ? new Date(value) : value
  return new Intl.DateTimeFormat('en-IN', { hour: 'numeric', minute: '2-digit', hour12: true }).format(date)
}

/**
 * "18:30" → "6:30 PM". Class times come off the API as an IST wall clock rather than
 * an instant, so the timetable reads the same whether you browse it from Koramangala
 * or from Dubai. Formatting a string keeps that guarantee; `formatTime` would not.
 */
export function formatClock(hhmm: string): string {
  const [hoursRaw, minutes] = hhmm.split(':')
  const hours = Number(hoursRaw)
  if (Number.isNaN(hours)) return hhmm
  const suffix = hours < 12 ? 'AM' : 'PM'
  const display = hours % 12 === 0 ? 12 : hours % 12
  return `${display}:${minutes} ${suffix}`
}

/** "Mon 18" — the day-tab label on the timetable. */
export function formatDayTab(isoDate: string): { weekday: string; day: string; isToday: boolean } {
  const date = new Date(`${isoDate}T00:00:00`)
  const today = new Date()
  return {
    weekday: new Intl.DateTimeFormat('en-IN', { weekday: 'short' }).format(date),
    day: String(date.getDate()),
    isToday:
      date.getDate() === today.getDate() &&
      date.getMonth() === today.getMonth() &&
      date.getFullYear() === today.getFullYear(),
  }
}

/** "Thursday, 20 August" — the day heading above a group of sessions. */
export function formatDayHeading(isoDate: string): string {
  const date = new Date(`${isoDate}T00:00:00`)
  return new Intl.DateTimeFormat('en-IN', { weekday: 'long', day: 'numeric', month: 'long' }).format(date)
}

/** Today's date as yyyy-MM-dd in the visitor's own calendar — used to seed date inputs. */
export function todayIso(offsetDays = 0): string {
  const date = new Date()
  date.setDate(date.getDate() + offsetDays)
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

/** Renders a headline containing "\n" as explicit lines without dangerous HTML. */
export function toLines(value: string): string[] {
  return value.split('\n')
}

/** True when the visitor has asked the OS for less motion. */
export function prefersReducedMotion(): boolean {
  if (typeof window === 'undefined' || !window.matchMedia) return false
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

/** Builds a wa.me link with the pre-filled message from site settings. */
export function whatsappLink(number: string | undefined, message?: string): string {
  const text = message ? `?text=${encodeURIComponent(message)}` : ''
  return `https://wa.me/${e164Digits(number)}${text}`
}

/**
 * Phone numbers reach the client in E.164 (`+919876543210`), but members and leads can also
 * be keyed in as ten digits. These three helpers are the only places that decide what a
 * number looks like and where it points, because prefixing "+91" at the call site produced
 * `+91 +9198…` on screen and a `tel:` link that dialled nothing.
 */
function e164Digits(number: string | undefined): string {
  const digits = (number ?? '').replace(/\D/g, '')
  if (digits.length === 10) return `91${digits}`
  return digits
}

/** Display form: `+91 98765 43210`, grouped the way an Indian number is read aloud. */
export function formatPhone(number: string | undefined): string {
  const digits = e164Digits(number)
  if (digits.length !== 12) return number ?? ''
  const local = digits.slice(2)
  return `+91 ${local.slice(0, 5)} ${local.slice(5)}`
}

export function telLink(number: string | undefined): string {
  return `tel:+${e164Digits(number)}`
}

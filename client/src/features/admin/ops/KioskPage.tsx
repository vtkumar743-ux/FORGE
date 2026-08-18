import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { useSiteSettings } from '@/lib/cms'
import { cn } from '@/lib/utils'
import { useAttendanceActions } from '../lib/admin-api'
import { describeErrorText, formatInr, formatIsoDate } from '../lib/format'
import type { CheckInResult } from '../lib/types'
import { Avatar, Pill, SelectField } from '../components/ui'
import { ToastProvider } from '../components/overlays'

/**
 * The tablet at the front desk.
 *
 * Runs the dark theme full-bleed rather than the admin's light surface — it is read from a
 * metre away by someone walking past, not by an owner at a laptop. The scan field stays
 * focused at all times because a hardware QR reader types into whatever has focus and then
 * presses Enter; losing focus is losing the scanner.
 */
export function KioskPage() {
  return (
    <ToastProvider>
      <KioskScreen />
    </ToastProvider>
  )
}

function KioskScreen() {
  const { data: settings } = useSiteSettings()
  const actions = useAttendanceActions()
  const reduced = useReducedMotion()

  const [branchId, setBranchId] = useState<number | null>(null)
  const [scan, setScan] = useState('')
  const [result, setResult] = useState<CheckInResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [matches, setMatches] = useState<
    { id: number; memberCode: string; fullName: string; phone: string; photoUrl?: string | null; branchName: string; status: string }[]
  >([])

  const scanField = useRef<HTMLInputElement>(null)
  const clearTimer = useRef<number | null>(null)

  // Default to the first branch so the kiosk is usable the second it opens.
  useEffect(() => {
    if (branchId === null && settings?.branches.length) setBranchId(settings.branches[0].id)
  }, [settings, branchId])

  // The reader types and presses Enter into whatever is focused; keep that the scan field.
  useEffect(() => {
    const interval = window.setInterval(() => {
      if (document.activeElement?.tagName !== 'INPUT') scanField.current?.focus()
    }, 1200)
    return () => window.clearInterval(interval)
  }, [])

  useEffect(() => () => {
    if (clearTimer.current) window.clearTimeout(clearTimer.current)
  }, [])

  function scheduleClear() {
    if (clearTimer.current) window.clearTimeout(clearTimer.current)
    // Long enough to read, short enough that the next person is not looking at a stranger's card.
    clearTimer.current = window.setTimeout(() => setResult(null), 9000)
  }

  async function submit(token: string, memberId?: number) {
    if (!branchId) return setError('Pick a branch first.')
    setError(null)

    try {
      const outcome = await actions.checkIn.mutateAsync({
        qrToken: memberId ? undefined : token,
        memberId,
        branchId,
        source: memberId ? 1 : 0,
        deviceId: 'desk-kiosk',
      })
      setResult(outcome)
      setScan('')
      setSearch('')
      setMatches([])
      scheduleClear()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  async function lookup(term: string) {
    setSearch(term)
    if (term.trim().length < 2) return setMatches([])
    try {
      setMatches(await actions.lookup.mutateAsync(term))
    } catch {
      setMatches([])
    }
  }

  return (
    <div className="grain relative min-h-dvh bg-ink text-bone">
      <header className="shell flex items-center justify-between gap-4 py-6">
        <div className="flex items-center gap-3">
          <Icon name="barbell" size={26} className="text-accent" />
          <span className="font-display text-[1.25rem] font-semibold uppercase tracking-[0.02em]">
            {settings?.values['brand.name'] ?? 'FORGE'}
          </span>
          <Pill tone="accent">Desk kiosk</Pill>
        </div>
        <div className="flex items-center gap-3">
          <SelectField
            value={branchId ? String(branchId) : ''}
            onChange={(event) => setBranchId(Number(event.target.value))}
            aria-label="Branch"
            className="w-56"
          >
            {(settings?.branches ?? []).map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </SelectField>
          <Link
            to="/admin/attendance"
            className="rounded-full border border-[var(--hairline-strong)] p-2 text-smoke transition-colors hover:text-bone"
            aria-label="Leave kiosk mode"
          >
            <Icon name="x" size={18} />
          </Link>
        </div>
      </header>

      <main className="shell grid gap-8 pb-16 lg:grid-cols-[26rem_1fr]">
        {/* ---- scan panel ---- */}
        <div>
          <div className="rounded-[var(--radius-sheet)] border border-[var(--hairline)] bg-carbon p-7">
            <div className="mx-auto mb-6 flex size-16 items-center justify-center rounded-full border border-[var(--accent-line)] text-accent">
              <Icon name="qr" size={28} />
            </div>
            <h1 className="display-m text-center text-[1.5rem]">Scan your card</h1>
            <p className="mt-2 text-center text-[0.875rem] leading-relaxed text-smoke">
              Hold the QR from the app under the reader. The desk can also search by name.
            </p>

            <form
              className="mt-6"
              onSubmit={(event) => {
                event.preventDefault()
                if (scan.trim()) void submit(scan.trim())
              }}
            >
              <input
                ref={scanField}
                value={scan}
                onChange={(event) => setScan(event.target.value)}
                placeholder="Waiting for a scan…"
                aria-label="QR token"
                autoComplete="off"
                className="field-input text-center font-mono tracking-[0.08em]"
              />
              <Button type="submit" fullWidth className="mt-3" loading={actions.checkIn.isPending} icon="check">
                Check in
              </Button>
            </form>

            {error && <p className="mt-4 text-center text-[0.8125rem] text-accent-hot">{error}</p>}
          </div>

          {/* ---- desk search ---- */}
          <div className="mt-5 rounded-[var(--radius-card)] border border-[var(--hairline)] bg-carbon p-5">
            <p className="caption mb-3">Forgot the card?</p>
            <input
              value={search}
              onChange={(event) => void lookup(event.target.value)}
              placeholder="Name, member code or number"
              aria-label="Search for a member"
              className="field-input"
            />
            {matches.length > 0 && (
              <ul className="mt-3 space-y-1.5">
                {matches.map((match) => (
                  <li key={match.id}>
                    <button
                      type="button"
                      onClick={() => void submit('', match.id)}
                      className="flex w-full items-center gap-3 rounded-[0.625rem] border border-[var(--hairline)] p-2.5 text-left transition-colors hover:border-[var(--accent-line)]"
                    >
                      <Avatar src={match.photoUrl} name={match.fullName} size={34} />
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-[0.875rem] font-medium">{match.fullName}</p>
                        <p className="numeric truncate text-[0.75rem] text-smoke">
                          {match.memberCode} · {match.branchName.replace('FORGE ', '')}
                        </p>
                      </div>
                      <Icon name="arrow-right" size={16} className="shrink-0 text-smoke" />
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        {/* ---- result ---- */}
        <div className="min-h-[26rem]">
          <AnimatePresence mode="wait">
            {result ? (
              <motion.div
                key={`${result.memberId}-${result.admitted}`}
                initial={reduced ? { opacity: 0 } : { opacity: 0, y: 16, scale: 0.985 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0 }}
                transition={{ duration: 0.3, ease: [0.16, 1, 0.3, 1] }}
                className={cn(
                  'h-full rounded-[var(--radius-sheet)] border p-9',
                  result.admitted
                    ? 'border-success/45 bg-[color-mix(in_srgb,var(--success)_7%,var(--carbon))]'
                    : 'border-accent-hot/50 bg-[color-mix(in_srgb,var(--accent-hot)_7%,var(--carbon))]',
                )}
              >
                <div className="flex items-start gap-6">
                  <Avatar src={result.photoUrl} name={result.fullName ?? '—'} size={96} />
                  <div className="min-w-0 flex-1">
                    <div
                      className={cn(
                        'mb-3 inline-flex items-center gap-2 rounded-full border px-3 py-1 text-[0.75rem] font-medium uppercase tracking-[0.08em]',
                        result.admitted ? 'border-success/50 text-success' : 'border-accent-hot/50 text-accent-hot',
                      )}
                    >
                      <Icon name={result.admitted ? 'check' : 'lock'} size={13} />
                      {result.admitted ? 'Admitted' : 'See the desk'}
                    </div>
                    <h2 className="display-l text-[2.5rem]">{result.headline}</h2>
                    <p className="mt-3 text-body-l leading-relaxed text-smoke">{result.message}</p>

                    <dl className="mt-7 grid gap-x-8 gap-y-4 sm:grid-cols-2">
                      <Fact label="Member" value={result.memberCode ?? '—'} />
                      <Fact label="Plan" value={result.planName ?? 'No active plan'} />
                      <Fact
                        label="Valid to"
                        value={
                          result.membershipEndsOn
                            ? `${formatIsoDate(result.membershipEndsOn)}${
                                result.daysLeft !== null && result.daysLeft !== undefined
                                  ? ` · ${result.daysLeft}d left`
                                  : ''
                              }`
                            : '—'
                        }
                      />
                      <Fact
                        label="Streak"
                        value={result.currentStreakDays > 0 ? `${result.currentStreakDays} days` : '—'}
                      />
                    </dl>

                    {result.warnings.length > 0 && (
                      <ul className="mt-7 space-y-2">
                        {result.warnings.map((warning) => (
                          <li key={warning} className="flex items-start gap-2.5 text-[0.9375rem] text-bone">
                            <Icon name="sparkles" size={16} className="mt-0.5 shrink-0 text-accent" />
                            {warning}
                          </li>
                        ))}
                      </ul>
                    )}

                    {result.duesOutstanding > 0 && (
                      <p className="mt-5 inline-flex items-center gap-2 rounded-full border border-[var(--accent-line)] bg-[var(--accent-soft)] px-4 py-2 text-[0.875rem]">
                        <Icon name="clock" size={15} className="text-accent" />
                        {formatInr(result.duesOutstanding)} outstanding — settle at the desk
                      </p>
                    )}

                    {result.todaysClasses.length > 0 && (
                      <div className="mt-7 border-t border-[var(--hairline)] pt-5">
                        <p className="caption mb-3">Booked today</p>
                        <ul className="space-y-2">
                          {result.todaysClasses.map((session) => (
                            <li key={session.id} className="flex items-center gap-3 text-[0.9375rem]">
                              <span className="numeric w-20 shrink-0 text-accent">{session.startTime}</span>
                              <span className="font-medium">{session.formatName}</span>
                              <span className="text-smoke">
                                {session.trainerName}
                                {session.roomName ? ` · ${session.roomName}` : ''}
                              </span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}
                  </div>
                </div>
              </motion.div>
            ) : (
              <motion.div
                key="idle"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                className="flex h-full flex-col items-center justify-center rounded-[var(--radius-sheet)] border border-dashed border-[var(--hairline)] p-12 text-center"
              >
                <Icon name="barbell" size={44} className="mb-6 text-bone/15" />
                <p className="display-m text-[1.75rem] text-bone/70">Ready when you are</p>
                <p className="measure mt-3 text-[0.9375rem] leading-relaxed text-smoke">
                  Every scan is logged — including refusals, with the reason. That record is what makes the
                  peak-hours chart and the churn radar worth reading.
                </p>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </main>
    </div>
  )
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <dt className="caption">{label}</dt>
      <dd className="numeric mt-1 truncate text-[1.0625rem]">{value}</dd>
    </div>
  )
}

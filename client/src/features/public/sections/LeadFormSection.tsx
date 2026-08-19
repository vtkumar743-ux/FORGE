import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { motion, useReducedMotion } from 'motion/react'
import { Button, ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Reveal } from '@/components/ui/Reveal'
import { setting, useSiteSettings, type BranchSummary } from '@/lib/cms'
import { useCreateLead, type LeadSubmission } from '@/lib/public-api'
import { describeError } from '@/lib/api'
import { useBranchScope } from './context'
import { cn, todayIso, whatsappLink } from '@/lib/utils'
import type { LeadFormContent } from './schemas'

/* ============================================================================
   Free-trial / tour form (Module 1.6)

   Two steps, because a single wall of nine fields is where trial forms die. Step
   one is name and number — the only two things we actually need — so a visitor
   who abandons at step two has still given us a contactable lead on submit.

   The form is CMS-defined: fields, options, labels, consent wording and every
   line of the success and failure copy come from the section's content. Adding a
   question is an admin edit, not a deploy.
   ============================================================================ */

const NO_BRANCHES: BranchSummary[] = []

export function LeadFormSection({ content }: { content: LeadFormContent }) {
  const branchScope = useBranchScope()
  const [searchParams] = useSearchParams()
  const { data: settings } = useSiteSettings()
  const reduced = useReducedMotion()
  const mutation = useCreateLead()

  const [step, setStep] = useState(0)
  const [values, setValues] = useState<Record<string, string>>({})
  const [consent, setConsent] = useState(true)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [honeypot, setHoneypot] = useState('')

  // Stable empty reference: `?? []` would mint a new array each render and re-run the
  // summary memo (and every effect depending on it) on every keystroke.
  const branches = settings?.branches ?? NO_BRANCHES
  const whatsapp = setting(settings, 'contact.whatsapp')

  // Deep links carry intent: /free-trial?branch=whitefield&class=hiit-45&intent=pt.
  const intent = searchParams.get('intent') ?? content.intent
  const planSlug = searchParams.get('plan') ?? undefined

  useEffect(() => {
    const branch = branchScope ?? searchParams.get('branch') ?? undefined
    const date = searchParams.get('date') ?? undefined
    if (!branch && !date) return
    setValues((current) => ({
      ...current,
      ...(branch ? { branchSlug: current.branchSlug ?? branch } : {}),
      ...(date ? { trialDate: current.trialDate ?? date } : {}),
    }))
  }, [branchScope, searchParams])

  const steps = content.steps
  const isLastStep = step === steps.length - 1
  const currentFields = steps[step]?.fields ?? []

  const classHint = searchParams.get('class')

  const summary = useMemo(() => {
    const branch = branches.find((entry) => entry.slug === values.branchSlug)
    return [branch?.name.replace('FORGE ', ''), values.trialDate, values.preferredTime].filter(Boolean).join(' · ')
  }, [branches, values])

  function setField(name: string, value: string) {
    setValues((current) => ({ ...current, [name]: value }))
    setErrors((current) => {
      if (!current[name]) return current
      const next = { ...current }
      delete next[name]
      return next
    })
  }

  function validate(fields: typeof currentFields): boolean {
    const found: Record<string, string> = {}

    for (const field of fields) {
      const value = (values[field.name] ?? '').trim()

      if (field.required && !value) {
        found[field.name] = `${field.label} is needed.`
        continue
      }
      if (!value) continue

      if (field.type === 'tel' && value.replace(/\D/g, '').length < 10)
        found[field.name] = 'That does not look like a 10-digit mobile number.'
      if (field.type === 'email' && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value))
        found[field.name] = 'Check the email address.'
      if (field.type === 'date' && value < todayIso())
        found[field.name] = 'Pick today or a day after it.'
    }

    setErrors(found)
    return Object.keys(found).length === 0
  }

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    if (!validate(currentFields)) return

    if (!isLastStep) {
      setStep((current) => current + 1)
      return
    }

    const submission: LeadSubmission = {
      fullName: values.fullName ?? '',
      phone: values.phone ?? '',
      email: values.email || undefined,
      branchSlug: values.branchSlug || undefined,
      intent,
      goal: values.goal || undefined,
      preferredTime: values.preferredTime || undefined,
      trialDate: values.trialDate || undefined,
      message: values.message || undefined,
      consentMarketing: consent,
      planSlug,
      utmSource: searchParams.get('utm_source') ?? undefined,
      utmCampaign: searchParams.get('utm_campaign') ?? undefined,
      website: honeypot || undefined,
    }

    mutation.mutate(submission)
  }

  if (mutation.isSuccess) {
    return (
      <section className="section-y bg-ink" id="lead-form">
        <div className="shell">
          <motion.div
            className="mx-auto max-w-2xl rounded-[var(--radius-card)] border border-[var(--accent-line)] bg-carbon p-9 text-center sm:p-12"
            initial={reduced ? undefined : { opacity: 0, y: 20 }}
            animate={reduced ? undefined : { opacity: 1, y: 0 }}
            transition={{ duration: 0.5, ease: [0.16, 1, 0.3, 1] }}
          >
            <CheckDraw reduced={Boolean(reduced)} />
            <h2 className="display-l mt-7 text-bone">{content.successHeadline}</h2>
            {content.successBody && (
              <p className="mx-auto mt-5 max-w-lg text-[1.0625rem] leading-relaxed text-smoke">{content.successBody}</p>
            )}

            <dl className="mt-9 flex flex-wrap justify-center gap-x-10 gap-y-4 border-t border-[var(--hairline)] pt-8 text-left">
              <div>
                <dt className="caption text-[0.625rem]">Reference</dt>
                <dd className="numeric mt-1.5 text-[1.0625rem] text-accent">{mutation.data.reference}</dd>
              </div>
              {mutation.data.branchName && (
                <div>
                  <dt className="caption text-[0.625rem]">Branch</dt>
                  <dd className="mt-1.5 text-[1.0625rem] text-bone">{mutation.data.branchName.replace('FORGE ', '')}</dd>
                </div>
              )}
              {values.trialDate && (
                <div>
                  <dt className="caption text-[0.625rem]">Requested for</dt>
                  <dd className="mt-1.5 text-[1.0625rem] text-bone">{values.trialDate}</dd>
                </div>
              )}
            </dl>

            <div className="mt-9 flex flex-wrap justify-center gap-3">
              <ButtonLink
                href={whatsappLink(mutation.data.whatsAppNumber ?? whatsapp, `Hi FORGE — my trial reference is ${mutation.data.reference}.`)}
                target="_blank"
              >
                Message the desk
              </ButtonLink>
              <ButtonLink to="/classes" variant="outline">
                Browse the timetable
              </ButtonLink>
            </div>
          </motion.div>
        </div>
      </section>
    )
  }

  return (
    <section className="section-y bg-ink" id="lead-form">
      <div className="shell">
        <div className="mx-auto max-w-2xl">
          <Reveal>
            <div className="flex items-center gap-3">
              {steps.map((_, index) => (
                <span
                  key={index}
                  className={cn(
                    'h-0.5 flex-1 rounded-full transition-colors duration-300 ease-out',
                    index <= step ? 'bg-accent' : 'bg-steel',
                  )}
                />
              ))}
              <span className="numeric caption shrink-0 text-[0.625rem]">
                {step + 1} / {steps.length}
              </span>
            </div>

            {content.headline && <h2 className="display-l mt-8 text-bone">{content.headline}</h2>}
            <p className="mt-4 text-[0.9375rem] text-smoke">{steps[step]?.title}</p>
            {classHint && step === 0 && (
              <p className="mt-2 text-[0.875rem] text-accent">Booking towards {classHint.replace(/-/g, ' ')}.</p>
            )}
          </Reveal>

          <form onSubmit={handleSubmit} noValidate className="mt-9 space-y-6">
            {currentFields.map((field) => {
              const id = `lead-${field.name}`
              const error = errors[field.name]
              const describedBy = [error ? `${id}-error` : null, field.help ? `${id}-help` : null]
                .filter(Boolean)
                .join(' ')

              return (
                <div key={field.name}>
                  <label htmlFor={id} className="caption mb-2.5 block text-[0.625rem]">
                    {field.label}
                    {!field.required && <span className="ml-2 normal-case tracking-normal text-smoke">optional</span>}
                  </label>

                  {field.type === 'textarea' ? (
                    <textarea
                      id={id}
                      className="field-input"
                      value={values[field.name] ?? ''}
                      onChange={(event) => setField(field.name, event.target.value)}
                      aria-invalid={Boolean(error)}
                      aria-describedby={describedBy || undefined}
                    />
                  ) : field.type === 'select' || field.type === 'branch' ? (
                    <select
                      id={id}
                      className="field-input"
                      value={values[field.name] ?? ''}
                      onChange={(event) => setField(field.name, event.target.value)}
                      aria-invalid={Boolean(error)}
                      aria-describedby={describedBy || undefined}
                    >
                      <option value="">Choose one</option>
                      {field.type === 'branch'
                        ? branches.map((branch) => (
                            <option key={branch.slug} value={branch.slug}>
                              {branch.name.replace('FORGE ', '')} — {branch.addressLine1}
                            </option>
                          ))
                        : field.options?.map((option) => (
                            <option key={option} value={option}>
                              {option}
                            </option>
                          ))}
                    </select>
                  ) : (
                    <input
                      id={id}
                      type={field.type}
                      className="field-input"
                      value={values[field.name] ?? ''}
                      min={field.type === 'date' ? todayIso() : undefined}
                      max={field.type === 'date' ? todayIso(60) : undefined}
                      autoComplete={field.autoComplete}
                      inputMode={field.type === 'tel' ? 'tel' : undefined}
                      onChange={(event) => setField(field.name, event.target.value)}
                      aria-invalid={Boolean(error)}
                      aria-describedby={describedBy || undefined}
                    />
                  )}

                  {field.help && !error && (
                    <p id={`${id}-help`} className="mt-2 text-[0.75rem] text-smoke">
                      {field.help}
                    </p>
                  )}
                  {error && (
                    <p id={`${id}-error`} className="mt-2 flex items-center gap-1.5 text-[0.75rem] text-accent-hot">
                      <Icon name="x" size={13} />
                      {error}
                    </p>
                  )}
                </div>
              )
            })}

            {/* Honeypot: off-screen, not display:none, so bots that check visibility still fill it. */}
            <div aria-hidden className="absolute left-[-9999px] h-0 w-0 overflow-hidden">
              <label htmlFor="lead-website">Website</label>
              <input
                id="lead-website"
                name="website"
                tabIndex={-1}
                autoComplete="off"
                value={honeypot}
                onChange={(event) => setHoneypot(event.target.value)}
              />
            </div>

            {isLastStep && content.consentLabel && (
              <label className="flex cursor-pointer items-start gap-3 text-[0.8125rem] leading-relaxed text-smoke">
                <input
                  type="checkbox"
                  checked={consent}
                  onChange={(event) => setConsent(event.target.checked)}
                  className="mt-0.5 size-4 shrink-0 accent-[var(--accent)]"
                />
                {content.consentLabel}
              </label>
            )}

            {mutation.isError && (
              <p className="rounded-[var(--radius-card)] border border-accent-hot/40 bg-accent-hot/5 p-4 text-[0.875rem] leading-relaxed text-bone">
                {content.failureBody ?? describeError(mutation.error)}
              </p>
            )}

            <div className="flex flex-wrap items-center gap-3 pt-2">
              {step > 0 && (
                <Button type="button" variant="outline" icon="chevron-left" onClick={() => setStep((c) => c - 1)}>
                  Back
                </Button>
              )}
              <Button type="submit" size="lg" magnetic={isLastStep} loading={mutation.isPending}>
                {isLastStep ? content.submitLabel : 'Continue'}
              </Button>
              {isLastStep && summary && <span className="text-[0.8125rem] text-smoke">{summary}</span>}
            </div>
          </form>
        </div>
      </div>
    </section>
  )
}

/** The checkmark draws itself — the same confirmation gesture booking uses (03 §6). */
function CheckDraw({ reduced }: { reduced: boolean }) {
  return (
    <span className="mx-auto flex size-16 items-center justify-center rounded-full border border-accent text-accent">
      <svg width="30" height="30" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
        <motion.path
          d="m4.5 12.5 5 5 10-11"
          initial={reduced ? undefined : { pathLength: 0 }}
          animate={reduced ? undefined : { pathLength: 1 }}
          transition={{ duration: 0.5, delay: 0.15, ease: 'easeOut' }}
        />
      </svg>
    </span>
  )
}

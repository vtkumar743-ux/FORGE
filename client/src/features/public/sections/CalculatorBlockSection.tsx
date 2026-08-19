import { useMemo, useState } from 'react'
import { Reveal } from '@/components/ui/Reveal'
import { ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { cn } from '@/lib/utils'
import type { CalculatorBlockContent } from './schemas'

/* ============================================================================
   Calculators (Module 1.10)

   BMI on the WHO Asian-Indian cut-offs — the international bands overstate a
   healthy range for this population, and using them would mislead most of the
   people who read this page. BMR via Mifflin-St Jeor, which outperforms
   Harris-Benedict on modern populations, then an activity multiplier.

   Both compute on every keystroke with no network call: they are engagement
   widgets, and a spinner between a slider and a number would kill the point.
   ============================================================================ */

export function CalculatorBlockSection({ content }: { content: CalculatorBlockContent }) {
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      content.inputs.map((input) => [
        input.name,
        input.type === 'select' ? (input.options?.[0] ?? '') : String(input.default ?? input.min ?? 0),
      ]),
    ),
  )

  const result = useMemo(
    () => (content.kind === 'bmi' ? computeBmi(values, content) : computeBmr(values)),
    [values, content],
  )

  return (
    <section className="section-y bg-ink">
      <div className="shell">
        <div className="grid gap-10 lg:grid-cols-12 lg:gap-16">
          <Reveal className="lg:col-span-5">
            <h2 className="display-l text-bone">{content.headline}</h2>
            {content.body && <p className="measure mt-6 text-body-l leading-relaxed text-smoke">{content.body}</p>}
            {content.footnote && (
              <p className="measure mt-6 text-[0.75rem] leading-relaxed text-smoke">{content.footnote}</p>
            )}
            {content.cta && (
              <ButtonLink to={content.cta.href} variant="outline" className="mt-8" icon="arrow-right" iconAfter>
                {content.cta.label}
              </ButtonLink>
            )}
          </Reveal>

          <Reveal delay={0.1} className="lg:col-span-7">
            <div className="rounded-[var(--radius-card)] border border-[var(--hairline)] bg-carbon p-7 sm:p-9">
              <div className="grid gap-6 sm:grid-cols-2">
                {content.inputs.map((input) => (
                  <div key={input.name} className={cn(input.type === 'select' && 'sm:col-span-1')}>
                    <label
                      htmlFor={`${content.kind}-${input.name}`}
                      className="caption mb-2.5 flex items-baseline justify-between gap-3 text-[0.625rem]"
                    >
                      {input.label}
                      {input.unit && input.type !== 'select' && (
                        <span className="numeric text-[0.875rem] normal-case tracking-normal text-accent">
                          {values[input.name]} {input.unit}
                        </span>
                      )}
                    </label>

                    {input.type === 'select' ? (
                      <select
                        id={`${content.kind}-${input.name}`}
                        className="field-input"
                        value={values[input.name]}
                        onChange={(event) => setValues((current) => ({ ...current, [input.name]: event.target.value }))}
                      >
                        {input.options?.map((option) => (
                          <option key={option} value={option}>
                            {option}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <input
                        id={`${content.kind}-${input.name}`}
                        type="range"
                        min={input.min}
                        max={input.max}
                        value={values[input.name]}
                        onChange={(event) => setValues((current) => ({ ...current, [input.name]: event.target.value }))}
                        className="range-accent w-full"
                      />
                    )}
                  </div>
                ))}
              </div>

              <div className="mt-9 border-t border-[var(--hairline)] pt-8">
                {content.kind === 'bmi' ? (
                  <BmiResult result={result as BmiResultShape} />
                ) : (
                  <BmrResult result={result as BmrResultShape} outputs={content.outputs ?? []} />
                )}
              </div>
            </div>
          </Reveal>
        </div>
      </div>
    </section>
  )
}

/* ---------------------------------------------------------------- BMI */

interface BmiResultShape {
  value: number
  label: string
  tone: 'success' | 'warn' | 'hot'
}

function computeBmi(values: Record<string, string>, content: CalculatorBlockContent): BmiResultShape {
  const heightM = Number(values.heightCm ?? 170) / 100
  const weight = Number(values.weightKg ?? 72)
  const value = heightM > 0 ? weight / (heightM * heightM) : 0
  const band = content.bands?.find((entry) => value < entry.max) ?? content.bands?.at(-1)
  return { value, label: band?.label ?? '', tone: band?.tone ?? 'warn' }
}

const TONES = {
  success: 'var(--success)',
  warn: 'var(--accent)',
  hot: 'var(--accent-hot)',
} as const

function BmiResult({ result }: { result: BmiResultShape }) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-6">
      <div>
        <p className="caption text-[0.625rem]">Your BMI</p>
        <p className="numeric display-l mt-2 text-[clamp(3rem,6vw,4.5rem)]" style={{ color: TONES[result.tone] }}>
          {result.value.toFixed(1)}
        </p>
      </div>
      <p
        className="rounded-full border px-4 py-2 text-[0.875rem]"
        style={{ borderColor: `color-mix(in srgb, ${TONES[result.tone]} 45%, transparent)`, color: TONES[result.tone] }}
      >
        {result.label}
      </p>
    </div>
  )
}

/* ---------------------------------------------------------------- BMR */

interface BmrResultShape {
  bmr: number
  maintenance: number
  target: number
  proteinGrams: number
  goal: string
}

/** Mifflin-St Jeor, then an activity multiplier, then a ±15% goal adjustment. */
function computeBmr(values: Record<string, string>): BmrResultShape {
  const weight = Number(values.weightKg ?? 72)
  const height = Number(values.heightCm ?? 170)
  const age = Number(values.age ?? 30)
  const isMale = (values.sex ?? 'Male') === 'Male'

  const bmr = 10 * weight + 6.25 * height - 5 * age + (isMale ? 5 : -161)

  const multiplier =
    values.activity === '6–7' ? 1.725 : values.activity === '4–5' ? 1.55 : values.activity === '2–3' ? 1.375 : 1.2

  const maintenance = bmr * multiplier
  const goal = values.goal ?? 'Maintain'
  const target = goal === 'Lose fat' ? maintenance * 0.85 : goal === 'Build muscle' ? maintenance * 1.1 : maintenance

  return {
    bmr: Math.round(bmr),
    maintenance: Math.round(maintenance),
    target: Math.round(target),
    // 1.8 g/kg — where our members do best, and comfortably above the RDA floor.
    proteinGrams: Math.round(weight * 1.8),
    goal,
  }
}

const OUTPUT_LABELS: Record<string, { label: string; suffix: string; hint: string }> = {
  bmr: { label: 'Resting burn', suffix: 'kcal', hint: 'What you burn doing nothing at all' },
  maintenance: { label: 'Maintenance', suffix: 'kcal', hint: 'Eat this and your weight holds' },
  target: { label: 'Your target', suffix: 'kcal', hint: 'Adjusted for the goal you picked' },
  proteinGrams: { label: 'Protein', suffix: 'g', hint: '1.8 g per kg of bodyweight' },
}

function BmrResult({ result, outputs }: { result: BmrResultShape; outputs: string[] }) {
  const keys = outputs.length > 0 ? outputs : ['bmr', 'maintenance', 'target', 'proteinGrams']

  return (
    <div className="grid gap-6 sm:grid-cols-2">
      {keys.map((key) => {
        const meta = OUTPUT_LABELS[key]
        if (!meta) return null
        const value = result[key as keyof BmrResultShape]
        const isTarget = key === 'target'

        return (
          <div key={key}>
            <p className="caption text-[0.625rem]">{meta.label}</p>
            <p className={cn('numeric display-m mt-2 text-[1.875rem]', isTarget ? 'text-accent' : 'text-bone')}>
              {typeof value === 'number' ? value.toLocaleString('en-IN') : value}
              <span className="ml-1.5 text-[0.875rem] text-smoke">{meta.suffix}</span>
            </p>
            <p className="mt-1.5 text-[0.75rem] leading-snug text-smoke">{meta.hint}</p>
          </div>
        )
      })}

      <p className="flex items-start gap-2.5 text-[0.75rem] leading-relaxed text-smoke sm:col-span-2">
        <Icon name="sparkles" size={14} className="mt-0.5 shrink-0 text-accent" />
        Estimates, not verdicts. Eat at maintenance for two weeks and weigh yourself before you trust the number — or
        get an InBody scan and have a coach read it back to you.
      </p>
    </div>
  )
}

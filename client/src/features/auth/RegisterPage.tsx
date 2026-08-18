import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { Button } from '@/components/ui/Button'
import { describeError } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import { useSiteSettings } from '@/lib/cms'
import { AuthShell, Field, FormError } from './AuthShell'

/**
 * Mobile-first member registration. Phone is required and stored in E.164 so switching
 * to OTP login later needs no data migration — v1 verifies with a password (04 §4).
 * Password rules mirror the Identity policy exactly so the server never rejects
 * something the client accepted.
 */
const registerSchema = z.object({
  fullName: z.string().trim().min(2, 'Tell us your name.').max(160),
  email: z.string().trim().toLowerCase().email('That email address does not look right.'),
  phone: z
    .string()
    .trim()
    .regex(/^(\+91)?[6-9]\d{9}$/, 'Enter a 10-digit Indian mobile number.'),
  password: z
    .string()
    .min(10, 'At least 10 characters.')
    .regex(/[A-Z]/, 'Include an uppercase letter.')
    .regex(/[a-z]/, 'Include a lowercase letter.')
    .regex(/\d/, 'Include a number.')
    .regex(/[^A-Za-z0-9]/, 'Include a symbol.'),
  homeBranchId: z.coerce.number().int().positive('Pick the branch you will train at.'),
  primaryGoal: z.string().optional(),
  consentMarketing: z.boolean().default(true),
})

type RegisterValues = z.input<typeof registerSchema>

const goals = [
  'Fat loss',
  'Build muscle',
  'Get stronger',
  'General fitness',
  'Return from injury',
  'Event or race prep',
]

export function RegisterPage() {
  const { register: createAccount } = useAuth()
  const { data: settings } = useSiteSettings()
  const navigate = useNavigate()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterValues>({
    defaultValues: { consentMarketing: true, primaryGoal: '' },
  })

  const branches = settings?.branches ?? []

  async function onSubmit(values: RegisterValues) {
    setFormError(null)
    const parsed = registerSchema.safeParse(values)
    if (!parsed.success) {
      setFormError(parsed.error.issues[0]?.message ?? 'Check the form and try again.')
      return
    }

    try {
      await createAccount({
        fullName: parsed.data.fullName,
        email: parsed.data.email,
        phone: parsed.data.phone,
        password: parsed.data.password,
        homeBranchId: parsed.data.homeBranchId,
        primaryGoal: parsed.data.primaryGoal || undefined,
        consentMarketing: parsed.data.consentMarketing,
      })
      navigate('/portal', { replace: true })
    } catch (error) {
      setFormError(describeError(error, 'Could not create your account. Try again.'))
    }
  }

  return (
    <AuthShell
      eyebrow="Create an account"
      headline="Start here."
      subhead="An account lets you book classes, hold a free trial slot and track your training. Buying a membership comes later — nothing is charged now."
      posterUrl="/media/facility/chalk-hands.jpg"
      posterAlt="Chalked hands gripping a barbell at the start of a set"
      wide
    >
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-5">
        <FormError message={formError} />

        <Field label="Full name" htmlFor="fullName" error={errors.fullName?.message}>
          <input
            id="fullName"
            type="text"
            autoComplete="name"
            className="auth-input"
            {...register('fullName', { required: 'Tell us your name.' })}
          />
        </Field>

        <div className="grid gap-5 sm:grid-cols-2">
          <Field label="Mobile number" htmlFor="phone" error={errors.phone?.message} help="We confirm bookings on WhatsApp.">
            <input
              id="phone"
              type="tel"
              inputMode="numeric"
              autoComplete="tel"
              placeholder="98765 43210"
              className="auth-input"
              {...register('phone', { required: 'Enter your mobile number.' })}
            />
          </Field>

          <Field label="Email" htmlFor="email" error={errors.email?.message}>
            <input
              id="email"
              type="email"
              autoComplete="email"
              autoCapitalize="none"
              spellCheck={false}
              className="auth-input"
              {...register('email', { required: 'Enter your email address.' })}
            />
          </Field>
        </div>

        <Field
          label="Password"
          htmlFor="password"
          error={errors.password?.message}
          help="At least 10 characters, with an uppercase letter, a number and a symbol."
        >
          <input
            id="password"
            type="password"
            autoComplete="new-password"
            className="auth-input"
            {...register('password', { required: 'Choose a password.' })}
          />
        </Field>

        <div className="grid gap-5 sm:grid-cols-2">
          <Field label="Home branch" htmlFor="homeBranchId" error={errors.homeBranchId?.message}>
            <select
              id="homeBranchId"
              className="auth-input"
              defaultValue=""
              {...register('homeBranchId', { required: 'Pick the branch you will train at.' })}
            >
              <option value="" disabled>
                Choose a branch
              </option>
              {branches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name} — {branch.city}
                </option>
              ))}
            </select>
          </Field>

          <Field label="What are you training for?" htmlFor="primaryGoal" optional>
            <select id="primaryGoal" className="auth-input" {...register('primaryGoal')}>
              <option value="">Not sure yet</option>
              {goals.map((goal) => (
                <option key={goal} value={goal}>
                  {goal}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <label className="flex cursor-pointer items-start gap-3 text-[0.8125rem] leading-relaxed text-smoke">
          <input
            type="checkbox"
            className="mt-0.5 size-4 shrink-0 rounded-sm accent-[var(--accent)]"
            {...register('consentMarketing')}
          />
          <span>
            Send me class updates and offers on WhatsApp. You can stop this any time by replying
            STOP.
          </span>
        </label>

        <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
          {isSubmitting ? 'Creating your account' : 'Create account'}
        </Button>

        <p className="text-center text-[0.875rem] text-smoke">
          Already a member?{' '}
          <Link to="/login" className="text-accent underline-offset-4 hover:underline">
            Sign in
          </Link>
        </p>
      </form>
    </AuthShell>
  )
}

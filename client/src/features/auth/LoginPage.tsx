import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { describeError } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import { AuthShell, Field, FormError } from './AuthShell'

const loginSchema = z.object({
  identifier: z.string().min(3, 'Enter your email address or mobile number.'),
  password: z.string().min(1, 'Enter your password.'),
})

type LoginValues = z.infer<typeof loginSchema>

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [formError, setFormError] = useState<string | null>(null)
  const [showPassword, setShowPassword] = useState(false)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({ defaultValues: { identifier: '', password: '' } })

  async function onSubmit(values: LoginValues) {
    setFormError(null)
    const parsed = loginSchema.safeParse(values)
    if (!parsed.success) {
      setFormError(parsed.error.issues[0]?.message ?? 'Check the form and try again.')
      return
    }

    try {
      const user = await login(parsed.data.identifier, parsed.data.password)
      const intended = (location.state as { from?: string } | null)?.from
      // Admins land in the panel, members in the portal, unless they were sent here from a page.
      navigate(intended ?? (user.roles.includes('Admin') ? '/admin' : '/portal'), { replace: true })
    } catch (error) {
      setFormError(describeError(error, 'Could not sign you in. Try again.'))
    }
  }

  return (
    <AuthShell
      eyebrow="Members"
      headline="Sign in."
      subhead="Your bookings, QR code, program and invoices. Same login on the app."
      posterUrl="/media/facility/locker-corridor.jpg"
      posterAlt="A dimly lit locker corridor with warm brass fittings"
    >
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-5">
        <FormError message={formError} />

        <Field
          label="Email or mobile number"
          error={errors.identifier?.message}
          htmlFor="identifier"
        >
          <input
            id="identifier"
            type="text"
            autoComplete="username"
            autoCapitalize="none"
            spellCheck={false}
            placeholder="you@example.com"
            className="auth-input"
            {...register('identifier', { required: 'Enter your email address or mobile number.' })}
          />
        </Field>

        <Field label="Password" error={errors.password?.message} htmlFor="password">
          <div className="relative">
            <input
              id="password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              placeholder="••••••••••"
              className="auth-input pr-12"
              {...register('password', { required: 'Enter your password.' })}
            />
            <button
              type="button"
              onClick={() => setShowPassword((value) => !value)}
              aria-label={showPassword ? 'Hide password' : 'Show password'}
              className="absolute right-1.5 top-1/2 inline-flex size-9 -translate-y-1/2 items-center justify-center rounded-full text-smoke transition-colors hover:text-accent"
            >
              <Icon name={showPassword ? 'x' : 'lock'} size={16} />
            </button>
          </div>
        </Field>

        <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
          {isSubmitting ? 'Signing in' : 'Sign in'}
        </Button>

        <p className="text-center text-[0.875rem] text-smoke">
          No account yet?{' '}
          <Link to="/register" className="text-accent underline-offset-4 hover:underline">
            Create one
          </Link>
        </p>
      </form>
    </AuthShell>
  )
}

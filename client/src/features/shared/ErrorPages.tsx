import { Link } from 'react-router-dom'
import { ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { useAuth } from '@/lib/auth'

function ErrorShell({
  code,
  headline,
  body,
  children,
}: {
  code: string
  headline: string
  body: string
  children?: React.ReactNode
}) {
  return (
    <div className="grain flex min-h-dvh flex-col items-center justify-center bg-ink px-6 py-20 text-center">
      <div className="relative z-10 max-w-lg">
        <Link to="/" className="mb-10 inline-flex items-center gap-2.5 text-bone">
          <Icon name="barbell" size={26} className="text-accent" />
          <span className="font-display text-[1.25rem] font-semibold uppercase tracking-[0.02em]">
            FORGE
          </span>
        </Link>
        <p className="numeric display-xl text-outline leading-none">{code}</p>
        <h1 className="display-l mt-6 text-bone">{headline}</h1>
        <p className="mt-5 text-[0.9375rem] leading-relaxed text-smoke">{body}</p>
        <div className="mt-9 flex flex-wrap justify-center gap-3">{children}</div>
      </div>
    </div>
  )
}

export function NotFoundPage() {
  return (
    <ErrorShell
      code="404"
      headline="Nothing here."
      body="That page does not exist, or it was renamed in the CMS. The timetable and plans are both one tap away."
    >
      <ButtonLink to="/">Back to home</ButtonLink>
      <ButtonLink to="/classes" variant="outline">
        See the timetable
      </ButtonLink>
    </ErrorShell>
  )
}

export function ForbiddenPage() {
  const { isAdmin, isAuthenticated } = useAuth()

  return (
    <ErrorShell
      code="403"
      headline="Not your door."
      body={
        isAuthenticated
          ? 'Your account does not have access to that area. If you should have it, ask the owner to change your role.'
          : 'Sign in first, then try again.'
      }
    >
      {isAuthenticated ? (
        <ButtonLink to={isAdmin ? '/admin' : '/portal'}>Go to my account</ButtonLink>
      ) : (
        <ButtonLink to="/login">Sign in</ButtonLink>
      )}
      <ButtonLink to="/" variant="outline">
        Back to home
      </ButtonLink>
    </ErrorShell>
  )
}

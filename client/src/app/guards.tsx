import { Navigate, Outlet, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from '@/lib/auth'
import { Skeleton } from '@/components/ui/Skeleton'

/**
 * Client-side route guards. These are a UX affordance only — every endpoint is
 * independently protected with [Authorize(Roles=…)] and own-data filtering by the
 * userId claim server-side, per 04 §4. Never trust this layer for authorisation.
 */

function GuardShell({ children }: { children: ReactNode }) {
  return <div className="shell section-y">{children}</div>
}

/** Shown while the silent refresh settles, so a reload does not flash the login page. */
function ResolvingSession() {
  return (
    <GuardShell>
      <div className="mx-auto max-w-md space-y-4" role="status" aria-live="polite">
        <span className="sr-only">Restoring your session</span>
        <Skeleton className="h-10 w-2/3" rounded="pill" />
        <Skeleton className="h-4 w-full" rounded="pill" />
        <Skeleton className="h-4 w-4/5" rounded="pill" />
      </div>
    </GuardShell>
  )
}

export function RequireAuth({ children }: { children?: ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) return <ResolvingSession />
  if (!isAuthenticated) {
    // Preserve the intended destination so login can return the member to it.
    return <Navigate to="/login" state={{ from: location.pathname + location.search }} replace />
  }
  return <>{children ?? <Outlet />}</>
}

export function RequireRole({ role, children }: { role: string; children?: ReactNode }) {
  const { user, isAuthenticated, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) return <ResolvingSession />
  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location.pathname + location.search }} replace />
  }
  if (!user?.roles.includes(role)) {
    return <Navigate to="/forbidden" replace />
  }
  return <>{children ?? <Outlet />}</>
}

/** Keeps a signed-in user off /login and /register. */
export function RedirectIfAuthenticated({ children }: { children: ReactNode }) {
  const { isAuthenticated, isAdmin, isLoading } = useAuth()

  if (isLoading) return <ResolvingSession />
  if (isAuthenticated) return <Navigate to={isAdmin ? '/admin' : '/portal'} replace />
  return <>{children}</>
}

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { z } from 'zod'
import { api, setAccessToken, setUnauthorizedHandler } from './api'

/** Mirrors CurrentUserResponse on the API — parsed, so a contract drift fails loudly. */
export const currentUserSchema = z.object({
  id: z.number(),
  fullName: z.string(),
  email: z.string().nullable().optional(),
  phone: z.string().nullable().optional(),
  roles: z.array(z.string()),
  memberId: z.number().nullable().optional(),
  memberCode: z.string().nullable().optional(),
  homeBranchId: z.number().nullable().optional(),
  homeBranchName: z.string().nullable().optional(),
  avatarUrl: z.string().nullable().optional(),
  mustChangePassword: z.boolean(),
})

export type CurrentUser = z.infer<typeof currentUserSchema>

const authResponseSchema = z.object({
  accessToken: z.string(),
  accessTokenExpiresAtUtc: z.string(),
  user: currentUserSchema,
})

export interface RegisterInput {
  fullName: string
  email: string
  phone: string
  password: string
  homeBranchId?: number
  primaryGoal?: string
  consentMarketing?: boolean
}

interface AuthContextValue {
  user: CurrentUser | null
  /** True until the initial silent-refresh attempt settles. */
  isLoading: boolean
  isAuthenticated: boolean
  isAdmin: boolean
  isMember: boolean
  login: (identifier: string, password: string) => Promise<CurrentUser>
  register: (input: RegisterInput) => Promise<CurrentUser>
  logout: () => Promise<void>
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>
  refreshUser: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const clear = useCallback(() => {
    setAccessToken(null)
    setUser(null)
  }, [])

  // Silent restore: the refresh cookie survives a page reload, the access token does not.
  useEffect(() => {
    setUnauthorizedHandler(clear)
    let cancelled = false

    void (async () => {
      try {
        const response = await api.post('/auth/refresh')
        const parsed = authResponseSchema.parse(response.data)
        if (cancelled) return
        setAccessToken(parsed.accessToken)
        setUser(parsed.user)
      } catch {
        if (!cancelled) clear()
      } finally {
        if (!cancelled) setIsLoading(false)
      }
    })()

    return () => {
      cancelled = true
      setUnauthorizedHandler(null)
    }
  }, [clear])

  const login = useCallback(async (identifier: string, password: string) => {
    const response = await api.post('/auth/login', { identifier, password })
    const parsed = authResponseSchema.parse(response.data)
    setAccessToken(parsed.accessToken)
    setUser(parsed.user)
    return parsed.user
  }, [])

  const register = useCallback(async (input: RegisterInput) => {
    const response = await api.post('/auth/register', input)
    const parsed = authResponseSchema.parse(response.data)
    setAccessToken(parsed.accessToken)
    setUser(parsed.user)
    return parsed.user
  }, [])

  const logout = useCallback(async () => {
    try {
      await api.post('/auth/logout')
    } finally {
      clear()
    }
  }, [clear])

  const changePassword = useCallback(
    async (currentPassword: string, newPassword: string) => {
      await api.post('/auth/change-password', { currentPassword, newPassword })
      // Every session is revoked server-side, so sign back in with the new password.
      clear()
    },
    [clear],
  )

  const refreshUser = useCallback(async () => {
    const response = await api.get('/auth/me')
    setUser(currentUserSchema.parse(response.data))
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isLoading,
      isAuthenticated: user !== null,
      isAdmin: user?.roles.includes('Admin') ?? false,
      isMember: user?.roles.includes('Member') ?? false,
      login,
      register,
      logout,
      changePassword,
      refreshUser,
    }),
    [user, isLoading, login, register, logout, changePassword, refreshUser],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>.')
  return context
}

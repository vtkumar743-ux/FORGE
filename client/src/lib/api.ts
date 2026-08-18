import axios, {
  AxiosError,
  type AxiosInstance,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios'

/**
 * Access token lives in memory only. The refresh token is an httpOnly cookie the
 * browser sends to /api/auth/refresh, so a stolen XSS payload cannot read either
 * one out of localStorage.
 */
let accessToken: string | null = null
let onUnauthorized: (() => void) | null = null

export function setAccessToken(token: string | null): void {
  accessToken = token
}

export function getAccessToken(): string | null {
  return accessToken
}

/** Called when refreshing fails, so the auth context can clear its state. */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler
}

export const api: AxiosInstance = axios.create({
  baseURL: '/api',
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
  timeout: 30_000,
})

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`
  return config
})

/** One in-flight refresh at a time; parallel 401s queue behind it. */
let refreshInFlight: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  refreshInFlight ??= axios
    .post<{ accessToken: string }>('/api/auth/refresh', null, { withCredentials: true })
    .then((response) => {
      accessToken = response.data.accessToken
      return accessToken
    })
    .catch(() => {
      accessToken = null
      onUnauthorized?.()
      return null
    })
    .finally(() => {
      refreshInFlight = null
    })

  return refreshInFlight
}

interface RetriableConfig extends AxiosRequestConfig {
  _retried?: boolean
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetriableConfig | undefined
    const status = error.response?.status

    // A 15-minute access token will expire mid-session; refresh once and replay.
    const isRefreshCall = config?.url?.includes('/auth/refresh')
    if (status === 401 && config && !config._retried && !isRefreshCall) {
      config._retried = true
      const token = await refreshAccessToken()
      if (token) {
        config.headers = { ...config.headers, Authorization: `Bearer ${token}` }
        return api.request(config)
      }
    }

    return Promise.reject(error)
  },
)

/** ProblemDetails shape the API returns for every handled failure. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

/** Turns an axios error into copy we are willing to show a member. */
export function describeError(error: unknown, fallback = 'Something went wrong. Try again.'): string {
  if (!axios.isAxiosError(error)) return fallback

  const problem = error.response?.data as ProblemDetails | undefined
  if (problem?.errors) {
    const first = Object.values(problem.errors).flat()[0]
    if (first) return first
  }
  return problem?.detail ?? problem?.title ?? error.message ?? fallback
}

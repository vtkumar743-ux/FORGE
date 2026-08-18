import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useEffect, type ReactNode } from 'react'
import { AuthProvider } from '@/lib/auth'
import { useSiteSettings } from '@/lib/cms'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Public content is CMS-driven and changes on the owner's schedule, not per tab focus.
      refetchOnWindowFocus: false,
      staleTime: 60_000,
      retry: (failureCount, error) => {
        const status = (error as { response?: { status?: number } })?.response?.status
        // Never retry an auth or validation failure — only genuine transport trouble.
        if (status && status >= 400 && status < 500) return false
        return failureCount < 2
      },
    },
  },
})

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <ThemeFromCms>{children}</ThemeFromCms>
      </AuthProvider>
    </QueryClientProvider>
  )
}

/**
 * Applies the CMS theme tokens to :root. Because every Tailwind colour resolves
 * through a custom property, changing theme.accent in the admin panel repaints the
 * whole site with no rebuild — the "no developer needed" requirement, applied to colour.
 */
function ThemeFromCms({ children }: { children: ReactNode }) {
  const { data: settings } = useSiteSettings()

  useEffect(() => {
    if (!settings) return
    const map: Record<string, string> = {
      'theme.ink': '--ink',
      'theme.carbon': '--carbon',
      'theme.steel': '--steel',
      'theme.smoke': '--smoke',
      'theme.bone': '--bone',
      'theme.accent': '--accent',
      'theme.accentHot': '--accent-hot',
      'theme.success': '--success',
    }

    for (const [key, property] of Object.entries(map)) {
      const value = settings.values[key]
      if (value) document.documentElement.style.setProperty(property, value)
    }
  }, [settings])

  return <>{children}</>
}

export { queryClient }

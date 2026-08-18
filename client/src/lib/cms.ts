import { useQuery, type UseQueryResult } from '@tanstack/react-query'
import { z } from 'zod'
import { api } from './api'

/* ============================================================================
   CMS contracts

   Every public section is a row in CmsSections with a contentJson payload typed
   per section type. Zod validates it on arrival so a bad edit in the admin panel
   surfaces as a caught parse error against one section, not a blank page.
   ============================================================================ */

export const ctaSchema = z.object({
  label: z.string(),
  href: z.string(),
})

export type Cta = z.infer<typeof ctaSchema>

export const cmsSectionSchema = z.object({
  id: z.number(),
  key: z.string(),
  type: z.number(),
  typeName: z.string(),
  orderIndex: z.number(),
  isVisible: z.boolean(),
  branchId: z.number().nullable().optional(),
  content: z.record(z.string(), z.unknown()),
  draft: z.record(z.string(), z.unknown()).nullable().optional(),
  state: z.number(),
  publishedAtUtc: z.string().nullable().optional(),
  adminLabel: z.string().nullable().optional(),
})

export type CmsSection = z.infer<typeof cmsSectionSchema>

export const cmsSeoSchema = z.object({
  title: z.string(),
  description: z.string(),
  keywords: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  canonicalUrl: z.string().nullable().optional(),
  noIndex: z.boolean(),
  structuredData: z.unknown().nullable().optional(),
})

export type CmsSeo = z.infer<typeof cmsSeoSchema>

export const cmsPageSchema = z.object({
  id: z.number(),
  slug: z.string(),
  title: z.string(),
  seo: cmsSeoSchema,
  sections: z.array(cmsSectionSchema),
})

export type CmsPage = z.infer<typeof cmsPageSchema>

export const branchSummarySchema = z.object({
  id: z.number(),
  name: z.string(),
  slug: z.string(),
  city: z.string(),
  addressLine1: z.string(),
  addressLine2: z.string().nullable().optional(),
  pincode: z.string(),
  phone: z.string(),
  whatsAppNumber: z.string().nullable().optional(),
  email: z.string(),
  latitude: z.number(),
  longitude: z.number(),
  googleMapsUrl: z.string().nullable().optional(),
  heroImageUrl: z.string().nullable().optional(),
  shortPitch: z.string().nullable().optional(),
  weekdayHours: z.string(),
  weekendHours: z.string(),
  occupancyCapacity: z.number(),
  displayOrder: z.number(),
})

export type BranchSummary = z.infer<typeof branchSummarySchema>

export const siteSettingsSchema = z.object({
  values: z.record(z.string(), z.string()),
  branches: z.array(branchSummarySchema),
  announcement: z
    .object({
      text: z.string(),
      linkLabel: z.string().nullable().optional(),
      linkUrl: z.string().nullable().optional(),
    })
    .nullable()
    .optional(),
})

export type SiteSettings = z.infer<typeof siteSettingsSchema>

export const occupancySchema = z.object({
  branchId: z.number(),
  branchName: z.string(),
  branchSlug: z.string(),
  currentCount: z.number(),
  capacity: z.number(),
  percentFull: z.number(),
  /** 0 Comfortable · 1 Busy · 2 Peak */
  band: z.number(),
  asOfUtc: z.string(),
})

export type BranchOccupancy = z.infer<typeof occupancySchema>

/* ---------------------------------------------------------------- queries */

export const cmsKeys = {
  settings: ['cms', 'settings'] as const,
  page: (slug: string, branchSlug?: string) => ['cms', 'page', slug, branchSlug ?? null] as const,
  occupancy: ['branches', 'occupancy'] as const,
}

export function useSiteSettings(): UseQueryResult<SiteSettings> {
  return useQuery({
    queryKey: cmsKeys.settings,
    queryFn: async () => siteSettingsSchema.parse((await api.get('/cms/settings')).data),
    // Brand, theme and branch details change rarely; do not refetch on every mount.
    staleTime: 10 * 60 * 1000,
  })
}

export function useCmsPage(slug: string, branchSlug?: string): UseQueryResult<CmsPage> {
  return useQuery({
    queryKey: cmsKeys.page(slug, branchSlug),
    queryFn: async () => {
      const response = await api.get(`/cms/pages/${slug}`, {
        params: branchSlug ? { branchSlug } : undefined,
      })
      return cmsPageSchema.parse(response.data)
    },
    staleTime: 5 * 60 * 1000,
  })
}

export function useOccupancy(enabled = true): UseQueryResult<BranchOccupancy[]> {
  return useQuery({
    queryKey: cmsKeys.occupancy,
    queryFn: async () => z.array(occupancySchema).parse((await api.get('/branches/occupancy')).data),
    // Polling is the Phase-1 fallback; Phase 4 replaces it with the SignalR push.
    refetchInterval: 60_000,
    enabled,
  })
}

/* ---------------------------------------------------------------- helpers */

/** Finds a section by key, returning undefined rather than throwing on a missing one. */
export function sectionByKey(page: CmsPage | undefined, key: string): CmsSection | undefined {
  return page?.sections.find((section) => section.key === key)
}

/**
 * Validates one section's content against its Zod schema. A section that fails
 * validation is skipped by the renderer instead of taking the page down.
 */
export function parseSection<T>(section: CmsSection | undefined, schema: z.ZodType<T>): T | null {
  if (!section) return null
  const result = schema.safeParse(section.content)
  if (result.success) return result.data
  if (import.meta.env.DEV) {
    console.warn(`[cms] section "${section.key}" failed validation`, result.error.issues)
  }
  return null
}

/** Reads a site setting with a fallback, so a deleted key never renders "undefined". */
export function setting(settings: SiteSettings | undefined, key: string, fallback = ''): string {
  return settings?.values[key] ?? fallback
}

export function settingFlag(settings: SiteSettings | undefined, key: string, fallback = false): boolean {
  const raw = settings?.values[key]
  if (raw === undefined) return fallback
  return raw === 'true' || raw === '1'
}

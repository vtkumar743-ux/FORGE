import { useMutation, useQuery, type UseMutationResult, type UseQueryResult } from '@tanstack/react-query'
import { z } from 'zod'
import { api } from './api'

/* ============================================================================
   Public-site read models.

   Section components never fetch by hand — they call one of these hooks, so a
   class card on the home rail and a row in the full timetable are guaranteed to
   be the same shape, cached under the same key and refetched together.

   Times arrive twice from the API: `startsAtUtc` for ordering and countdowns,
   and `startTime` as an IST wall clock. The visible time always comes from the
   wall clock so a Bengaluru timetable reads correctly from any timezone.
   ============================================================================ */

export const classSessionSchema = z.object({
  id: z.number(),
  formatName: z.string(),
  formatSlug: z.string(),
  iconKey: z.string().nullable().optional(),
  coverImageUrl: z.string().nullable().optional(),
  level: z.number(),
  levelName: z.string(),
  intensity: z.number(),
  branchId: z.number(),
  branchName: z.string(),
  branchSlug: z.string(),
  trainerName: z.string(),
  trainerSlug: z.string(),
  trainerPortraitUrl: z.string().nullable().optional(),
  isSubstitute: z.boolean(),
  roomName: z.string().nullable().optional(),
  date: z.string(),
  startTime: z.string(),
  endTime: z.string(),
  startsAtUtc: z.string(),
  durationMinutes: z.number(),
  capacity: z.number(),
  bookedCount: z.number(),
  spotsLeft: z.number(),
  waitlistCount: z.number(),
  status: z.number(),
  isBookable: z.boolean(),
  timeOfDay: z.string(),
})

export type ClassSession = z.infer<typeof classSessionSchema>

export const classFormatSchema = z.object({
  id: z.number(),
  name: z.string(),
  slug: z.string(),
  shortDescription: z.string(),
  description: z.string(),
  durationMinutes: z.number(),
  capacity: z.number(),
  level: z.number(),
  levelName: z.string(),
  intensity: z.number(),
  intensityName: z.string(),
  estimatedCalories: z.number(),
  coverImageUrl: z.string().nullable().optional(),
  iconKey: z.string().nullable().optional(),
  tags: z.array(z.string()),
  displayOrder: z.number(),
  upcomingSessionCount: z.number(),
  branchSlugs: z.array(z.string()),
  nextSession: classSessionSchema.nullable().optional(),
})

export type ClassFormat = z.infer<typeof classFormatSchema>

const filterOptionSchema = z.object({ slug: z.string(), name: z.string(), count: z.number() })

export const timetableSchema = z.object({
  fromDate: z.string(),
  toDate: z.string(),
  sessions: z.array(classSessionSchema),
  formats: z.array(filterOptionSchema),
  trainers: z.array(filterOptionSchema),
})

export type Timetable = z.infer<typeof timetableSchema>
export type TimetableFilterOption = z.infer<typeof filterOptionSchema>

export const trainerSchema = z.object({
  id: z.number(),
  fullName: z.string(),
  slug: z.string(),
  headline: z.string(),
  bio: z.string(),
  portraitUrl: z.string().nullable().optional(),
  demoVideoUrl: z.string().nullable().optional(),
  specialties: z.array(z.string()),
  certifications: z.array(z.string()),
  yearsExperience: z.number(),
  instagramUrl: z.string().nullable().optional(),
  branchId: z.number(),
  branchName: z.string(),
  branchSlug: z.string(),
  ptSessionPrice: z.number(),
  acceptsPtClients: z.boolean(),
  averageRating: z.number(),
  ratingCount: z.number(),
  displayOrder: z.number(),
  teachesFormats: z.array(z.string()),
  weeklyClassCount: z.number(),
})

export type Trainer = z.infer<typeof trainerSchema>

export const planSchema = z.object({
  id: z.number(),
  name: z.string(),
  slug: z.string(),
  tagline: z.string(),
  description: z.string(),
  kind: z.number(),
  cycle: z.number(),
  cycleName: z.string(),
  accessScope: z.number(),
  durationDays: z.number(),
  price: z.number(),
  basePrice: z.number(),
  admissionFee: z.number(),
  effectiveMonthlyPrice: z.number(),
  savingsPercent: z.number(),
  gstRatePercent: z.number(),
  sacCode: z.string().nullable().optional(),
  classCredits: z.number().nullable().optional(),
  ptSessionCredits: z.number().nullable().optional(),
  guestPasses: z.number().nullable().optional(),
  freezeDaysAllowed: z.number(),
  accessWindow: z.string().nullable().optional(),
  features: z.array(z.string()),
  trustMicrocopy: z.string().nullable().optional(),
  isMostPopular: z.boolean(),
  isAvailableAtBranch: z.boolean(),
  displayOrder: z.number(),
})

export type Plan = z.infer<typeof planSchema>

export const offerSchema = z.object({
  code: z.string(),
  name: z.string(),
  description: z.string().nullable().optional(),
  discountType: z.number(),
  discountValue: z.number(),
  maxDiscountAmount: z.number().nullable().optional(),
  validTo: z.string(),
  validToUtc: z.string(),
  bannerHeadline: z.string().nullable().optional(),
})

export type Offer = z.infer<typeof offerSchema>

export const testimonialSchema = z.object({
  id: z.number(),
  authorName: z.string(),
  authorRole: z.string().nullable().optional(),
  authorPhotoUrl: z.string().nullable().optional(),
  quote: z.string(),
  rating: z.number(),
  program: z.string().nullable().optional(),
  branchName: z.string().nullable().optional(),
  branchSlug: z.string().nullable().optional(),
  isFeatured: z.boolean(),
})

export type Testimonial = z.infer<typeof testimonialSchema>

export const transformationSchema = z.object({
  id: z.number(),
  memberDisplayName: z.string(),
  beforeImageUrl: z.string(),
  afterImageUrl: z.string(),
  durationWeeks: z.number(),
  program: z.string(),
  trainerName: z.string().nullable().optional(),
  weightBeforeKg: z.number().nullable().optional(),
  weightAfterKg: z.number().nullable().optional(),
  story: z.string().nullable().optional(),
  branchName: z.string().nullable().optional(),
  branchSlug: z.string().nullable().optional(),
})

export type Transformation = z.infer<typeof transformationSchema>

export const faqSchema = z.object({
  id: z.number(),
  category: z.string(),
  question: z.string(),
  answer: z.string(),
  displayOrder: z.number(),
})

export type Faq = z.infer<typeof faqSchema>

export const blogSummarySchema = z.object({
  id: z.number(),
  slug: z.string(),
  title: z.string(),
  excerpt: z.string(),
  coverImageUrl: z.string().nullable().optional(),
  authorName: z.string(),
  authorRole: z.string().nullable().optional(),
  tags: z.array(z.string()),
  readMinutes: z.number(),
  publishedAtUtc: z.string().nullable().optional(),
  isFeatured: z.boolean(),
})

export type BlogSummary = z.infer<typeof blogSummarySchema>

export const blogPostSchema = blogSummarySchema.extend({
  body: z.array(
    z.object({
      type: z.string(),
      text: z.string().nullable().optional(),
      url: z.string().nullable().optional(),
      alt: z.string().nullable().optional(),
    }),
  ),
  seoTitle: z.string(),
  seoDescription: z.string(),
  ogImageUrl: z.string().nullable().optional(),
  related: z.array(blogSummarySchema),
})

export type BlogPost = z.infer<typeof blogPostSchema>

/* ---------------------------------------------------------------- query keys */

export const publicKeys = {
  formats: (branchSlug?: string) => ['public', 'formats', branchSlug ?? null] as const,
  timetable: (params: TimetableParams) => ['public', 'timetable', params] as const,
  trainers: (branchSlug?: string) => ['public', 'trainers', branchSlug ?? null] as const,
  trainer: (slug: string) => ['public', 'trainer', slug] as const,
  plans: (branchSlug?: string) => ['public', 'plans', branchSlug ?? null] as const,
  offer: (branchSlug?: string) => ['public', 'offer', branchSlug ?? null] as const,
  testimonials: (featuredOnly: boolean, branchSlug?: string) =>
    ['public', 'testimonials', featuredOnly, branchSlug ?? null] as const,
  transformations: (branchSlug?: string) => ['public', 'transformations', branchSlug ?? null] as const,
  faqs: (category?: string) => ['public', 'faqs', category ?? null] as const,
  journal: (tag?: string) => ['public', 'journal', tag ?? null] as const,
  journalPost: (slug: string) => ['public', 'journal', 'post', slug] as const,
}

/** Marketing content changes on the order of days, so it caches hard. */
const CONTENT_STALE = 10 * 60 * 1000
/** Spot counts move as members book, so the timetable is refreshed far more often. */
const TIMETABLE_STALE = 60 * 1000

/* ---------------------------------------------------------------- hooks */

export function useClassFormats(branchSlug?: string): UseQueryResult<ClassFormat[]> {
  return useQuery({
    queryKey: publicKeys.formats(branchSlug),
    queryFn: async () =>
      z.array(classFormatSchema).parse(
        (await api.get('/classes/formats', { params: branchSlug ? { branchSlug } : undefined })).data,
      ),
    staleTime: TIMETABLE_STALE,
  })
}

export interface TimetableParams {
  branchSlug?: string
  formatSlug?: string
  trainerSlug?: string
  level?: string
  timeOfDay?: string
  from?: string
  days?: number
}

export function useTimetable(params: TimetableParams, enabled = true): UseQueryResult<Timetable> {
  return useQuery({
    queryKey: publicKeys.timetable(params),
    queryFn: async () =>
      timetableSchema.parse((await api.get('/classes/timetable', { params: clean(params) })).data),
    staleTime: TIMETABLE_STALE,
    // Keep the previous days on screen while a filter change loads — the sheet
    // must never blink back to a skeleton once it has content.
    placeholderData: (previous) => previous,
    enabled,
  })
}

export function useTrainers(branchSlug?: string): UseQueryResult<Trainer[]> {
  return useQuery({
    queryKey: publicKeys.trainers(branchSlug),
    queryFn: async () =>
      z.array(trainerSchema).parse(
        (await api.get('/trainers', { params: branchSlug ? { branchSlug } : undefined })).data,
      ),
    staleTime: CONTENT_STALE,
  })
}

export function useTrainer(slug: string): UseQueryResult<Trainer> {
  return useQuery({
    queryKey: publicKeys.trainer(slug),
    queryFn: async () => trainerSchema.parse((await api.get(`/trainers/${slug}`)).data),
    staleTime: CONTENT_STALE,
  })
}

export function usePlans(branchSlug?: string): UseQueryResult<Plan[]> {
  return useQuery({
    queryKey: publicKeys.plans(branchSlug),
    queryFn: async () =>
      z.array(planSchema).parse((await api.get('/plans', { params: branchSlug ? { branchSlug } : undefined })).data),
    staleTime: CONTENT_STALE,
  })
}

export function useOffer(branchSlug?: string): UseQueryResult<Offer | null> {
  return useQuery({
    queryKey: publicKeys.offer(branchSlug),
    queryFn: async () => {
      const response = await api.get('/plans/offer', { params: branchSlug ? { branchSlug } : undefined })
      // 204 means no offer is running — a valid answer, not an error.
      return response.status === 204 || !response.data ? null : offerSchema.parse(response.data)
    },
    staleTime: CONTENT_STALE,
  })
}

export function useTestimonials(
  options: { featuredOnly?: boolean; branchSlug?: string; limit?: number } = {},
): UseQueryResult<Testimonial[]> {
  const { featuredOnly = false, branchSlug, limit } = options
  return useQuery({
    queryKey: publicKeys.testimonials(featuredOnly, branchSlug),
    queryFn: async () =>
      z.array(testimonialSchema).parse(
        (await api.get('/content/testimonials', { params: clean({ featuredOnly, branchSlug, limit }) })).data,
      ),
    staleTime: CONTENT_STALE,
  })
}

export function useTransformations(branchSlug?: string): UseQueryResult<Transformation[]> {
  return useQuery({
    queryKey: publicKeys.transformations(branchSlug),
    queryFn: async () =>
      z.array(transformationSchema).parse(
        (await api.get('/content/transformations', { params: branchSlug ? { branchSlug } : undefined })).data,
      ),
    staleTime: CONTENT_STALE,
  })
}

export function useFaqs(category?: string): UseQueryResult<Faq[]> {
  return useQuery({
    queryKey: publicKeys.faqs(category),
    queryFn: async () =>
      z.array(faqSchema).parse((await api.get('/content/faqs', { params: category ? { category } : undefined })).data),
    staleTime: CONTENT_STALE,
  })
}

export function useJournal(tag?: string): UseQueryResult<BlogSummary[]> {
  return useQuery({
    queryKey: publicKeys.journal(tag),
    queryFn: async () =>
      z.array(blogSummarySchema).parse((await api.get('/content/journal', { params: tag ? { tag } : undefined })).data),
    staleTime: CONTENT_STALE,
  })
}

export function useJournalPost(slug: string): UseQueryResult<BlogPost> {
  return useQuery({
    queryKey: publicKeys.journalPost(slug),
    queryFn: async () => blogPostSchema.parse((await api.get(`/content/journal/${slug}`)).data),
    staleTime: CONTENT_STALE,
  })
}

/* ---------------------------------------------------------------- lead capture */

export interface LeadSubmission {
  fullName: string
  phone: string
  email?: string
  branchSlug?: string
  intent?: string
  goal?: string
  preferredTime?: string
  trialDate?: string
  message?: string
  consentMarketing?: boolean
  planSlug?: string
  utmSource?: string
  utmCampaign?: string
  /** Honeypot — hidden from real visitors, filled by bots. */
  website?: string
}

export const leadResponseSchema = z.object({
  id: z.number(),
  reference: z.string(),
  branchName: z.string().nullable().optional(),
  whatsAppNumber: z.string().nullable().optional(),
  firstFollowUpAtUtc: z.string(),
})

export type LeadResponse = z.infer<typeof leadResponseSchema>

export function useCreateLead(): UseMutationResult<LeadResponse, unknown, LeadSubmission> {
  return useMutation({
    mutationFn: async (submission) =>
      leadResponseSchema.parse((await api.post('/leads', clean(submission))).data),
  })
}

/** Drops undefined/empty params so they never reach the query string as "undefined". */
function clean(params: object): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(params).filter(([, value]) => value !== undefined && value !== null && value !== ''),
  )
}

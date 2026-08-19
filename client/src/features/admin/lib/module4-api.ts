import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { api } from '@/lib/api'

/* ============================================================================
   Module 4 — churn radar, plan studio, corporate accounts, campaigns, feed.

   Kept in its own file rather than folded into admin-api.ts: these are the
   differentiator screens, and a reader looking for "how does the AI generator
   talk to the server" should find it in one place.
   ============================================================================ */

async function get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  const clean = params
    ? Object.fromEntries(
        Object.entries(params).filter(([, value]) => value !== undefined && value !== null && value !== ''),
      )
    : undefined
  return (await api.get<T>(url, { params: clean })).data
}

export const moduleFourKeys = {
  churn: (branchId?: number, band?: number) => ['admin', 'churn', branchId ?? null, band ?? null] as const,
  planQueue: (memberId?: number) => ['admin', 'plan-studio', 'queue', memberId ?? null] as const,
  planEngine: ['admin', 'plan-studio', 'engine'] as const,
  workoutDraft: (id: number) => ['admin', 'plan-studio', 'workout', id] as const,
  dietDraft: (id: number) => ['admin', 'plan-studio', 'diet', id] as const,
  corporate: ['admin', 'corporate'] as const,
  corporateUsage: (id: number, from?: string, to?: string) =>
    ['admin', 'corporate', 'usage', id, from ?? null, to ?? null] as const,
  offers: ['admin', 'offers'] as const,
  offPeak: ['admin', 'offers', 'off-peak'] as const,
  feed: (branchId?: number) => ['admin', 'feed', branchId ?? null] as const,
}

/* ---------------------------------------------------------------- churn radar */

export type ChurnRadarRow = {
  memberId: number
  memberCode: string
  fullName: string
  phone: string
  email?: string | null
  photoUrl?: string | null
  branchId: number
  branchName: string
  band: number
  score: number
  reasons: string[]
  lastVisitOn?: string | null
  daysSinceVisit?: number | null
  currentStreakDays: number
  status: number
  planName?: string | null
  planEndsOn?: string | null
  planValue: number
  amountDue: number
  lastWinBackAtUtc?: string | null
}

export type ChurnRadar = {
  scoredAtUtc?: string | null
  healthy: number
  watch: number
  amber: number
  red: number
  revenueAtRisk: number
  rows: ChurnRadarRow[]
}

export function useChurnRadar(branchId?: number, band?: number): UseQueryResult<ChurnRadar> {
  return useQuery({
    queryKey: moduleFourKeys.churn(branchId, band),
    queryFn: () => get<ChurnRadar>('/admin/churn/radar', { branchId, band }),
    staleTime: 60_000,
  })
}

export function useRescoreChurn() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async () => (await api.post('/admin/churn/rescore')).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'churn'] }),
  })
}

export type WinBackPayload = {
  discountPercent: number
  maxDiscountAmount?: number
  offerValidDays: number
  message?: string
  sendWhatsApp: boolean
  sendEmail: boolean
  force?: boolean
}

export type WinBackOutcome = {
  sent: boolean
  message: string
  couponCode?: string | null
  channelRowsWritten?: number
}

export function useWinBack() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ memberId, ...payload }: WinBackPayload & { memberId: number }) =>
      (await api.post<WinBackOutcome>(`/admin/churn/winback/${memberId}`, payload)).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'churn'] }),
  })
}

export function useBulkWinBack() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (payload: WinBackPayload & { memberIds: number[] }) =>
      (await api.post<{ sent: number; skipped: number }>('/admin/churn/winback/bulk', payload)).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'churn'] }),
  })
}

/* ---------------------------------------------------------------- plan studio */

export type PlanEngine = { engine: string; aiAvailable: boolean; description: string }

export type WorkoutQueueItem = {
  id: number
  name: string
  status: number
  author: number
  goal?: string | null
  daysPerWeek: number
  durationWeeks: number
  memberId: number
  memberName: string
  memberCode: string
  createdAtUtc: string
  days: number
}

export type DietQueueItem = {
  id: number
  name: string
  status: number
  author: number
  targetCalories: number
  proteinGrams: number
  memberId: number
  memberName: string
  memberCode: string
  createdAtUtc: string
  meals: number
}

export type PlanQueue = { workouts: WorkoutQueueItem[]; diets: DietQueueItem[] }

export type WorkoutDraftExercise = {
  id: number
  exerciseId: number
  name: string
  primaryMuscle: string
  equipment: string
  sets: number
  repScheme: string
  restSeconds: number
  targetWeightKg?: number | null
  supersetGroup?: string | null
  notes?: string | null
}

export type WorkoutDraftDay = {
  id: number
  dayIndex: number
  title: string
  focus?: string | null
  isRestDay: boolean
  notes?: string | null
  exercises: WorkoutDraftExercise[]
}

export type WorkoutDraft = {
  id: number
  name: string
  description?: string | null
  status: number
  author: number
  authorLabel: string
  engine?: string | null
  fallbackReason?: string | null
  goal?: string | null
  daysPerWeek: number
  durationWeeks: number
  memberId?: number | null
  memberName?: string | null
  memberCode?: string | null
  approvedBy?: string | null
  approvedAtUtc?: string | null
  createdAtUtc: string
  days: WorkoutDraftDay[]
}

export type DietDraftMeal = {
  id: number
  slot: number
  slotLabel: string
  title: string
  items: string
  calories: number
  proteinGrams: number
  carbGrams: number
  fatGrams: number
  timingHint?: string | null
}

export type DietDraft = {
  id: number
  name: string
  status: number
  author: number
  authorLabel: string
  engine?: string | null
  fallbackReason?: string | null
  memberId?: number | null
  memberName?: string | null
  memberCode?: string | null
  targetCalories: number
  proteinGrams: number
  carbGrams: number
  fatGrams: number
  isVegetarian: boolean
  notes?: string | null
  approvedBy?: string | null
  approvedAtUtc?: string | null
  createdAtUtc: string
  meals: DietDraftMeal[]
}

export function usePlanEngine(): UseQueryResult<PlanEngine> {
  return useQuery({
    queryKey: moduleFourKeys.planEngine,
    queryFn: () => get<PlanEngine>('/admin/plan-studio/engine'),
    staleTime: 10 * 60 * 1000,
  })
}

export function usePlanQueue(memberId?: number): UseQueryResult<PlanQueue> {
  return useQuery({
    queryKey: moduleFourKeys.planQueue(memberId),
    queryFn: () => get<PlanQueue>('/admin/plan-studio/queue', { memberId }),
    staleTime: 30_000,
  })
}

export function useWorkoutDraft(id: number | null): UseQueryResult<WorkoutDraft> {
  return useQuery({
    queryKey: moduleFourKeys.workoutDraft(id ?? 0),
    queryFn: () => get<WorkoutDraft>(`/admin/plan-studio/workout/${id}`),
    enabled: id != null,
  })
}

export function useDietDraft(id: number | null): UseQueryResult<DietDraft> {
  return useQuery({
    queryKey: moduleFourKeys.dietDraft(id ?? 0),
    queryFn: () => get<DietDraft>(`/admin/plan-studio/diet/${id}`),
    enabled: id != null,
  })
}

export type GeneratePayload = {
  goal?: string
  level?: number
  daysPerWeek?: number
  durationWeeks?: number
  isVegetarian?: boolean
  trainerNote?: string
}

export function useGenerateWorkout() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ memberId, ...payload }: GeneratePayload & { memberId: number }) =>
      (await api.post<WorkoutDraft>(`/admin/plan-studio/workout/${memberId}`, payload)).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'plan-studio'] }),
  })
}

export function useGenerateDiet() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ memberId, ...payload }: GeneratePayload & { memberId: number }) =>
      (await api.post<DietDraft>(`/admin/plan-studio/diet/${memberId}`, payload)).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'plan-studio'] }),
  })
}

export function usePublishPlan() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ kind, id }: { kind: 'workout' | 'diet'; id: number }) =>
      (await api.post(`/admin/plan-studio/${kind}/${id}/publish`)).data,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['admin', 'plan-studio'] })
      void client.invalidateQueries({ queryKey: ['admin', 'members'] })
    },
  })
}

export function useDiscardPlan() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ kind, id }: { kind: 'workout' | 'diet'; id: number }) =>
      api.delete(`/admin/plan-studio/${kind}/${id}`),
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'plan-studio'] }),
  })
}

export function useUpdateWorkoutDraft() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...payload }: { id: number } & Record<string, unknown>) =>
      (await api.put<WorkoutDraft>(`/admin/plan-studio/workout/${id}`, payload)).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'plan-studio'] }),
  })
}

export function useUpdateDietDraft() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...payload }: { id: number } & Record<string, unknown>) =>
      (await api.put<DietDraft>(`/admin/plan-studio/diet/${id}`, payload)).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'plan-studio'] }),
  })
}

/* ---------------------------------------------------------------- corporate */

export type CorporateAccountRow = {
  id: number
  companyName: string
  code: string
  domain?: string | null
  hrContactName: string
  hrContactEmail: string
  hrContactPhone?: string | null
  discountPercent: number
  waiveAdmissionFee: boolean
  seatCap?: number | null
  seatsUsed: number
  seatsLeft?: number | null
  branchScope?: string | null
  validFrom: string
  validTo: string
  isActive: boolean
  notes?: string | null
  status: string
}

export type CorporateUsageRow = {
  memberId: number
  memberCode: string
  name: string
  employeeId?: string | null
  workEmail?: string | null
  branch: string
  enrolledOn: string
  endedOn?: string | null
  isActive: boolean
  plan?: string | null
  planEndsOn?: string | null
  visits: number
  lastVisitOn?: string | null
  classesAttended: number
  amountInvoiced: number
}

export type CorporateUsage = {
  accountId: number
  companyName: string
  code: string
  from: string
  to: string
  seatCap?: number | null
  seatsUsed: number
  discountPercent: number
  totalVisits: number
  activeUsers: number
  neverVisited: number
  totalInvoiced: number
  rows: CorporateUsageRow[]
}

export function useCorporateAccounts(): UseQueryResult<CorporateAccountRow[]> {
  return useQuery({
    queryKey: moduleFourKeys.corporate,
    queryFn: () => get<CorporateAccountRow[]>('/admin/corporate'),
    staleTime: 60_000,
  })
}

export function useCorporateUsage(
  id: number | null,
  from?: string,
  to?: string,
): UseQueryResult<CorporateUsage> {
  return useQuery({
    queryKey: moduleFourKeys.corporateUsage(id ?? 0, from, to),
    queryFn: () => get<CorporateUsage>(`/admin/corporate/${id}/usage`, { from, to }),
    enabled: id != null,
  })
}

export function useSaveCorporate() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, ...payload }: Record<string, unknown> & { id?: number }) =>
      id
        ? (await api.put(`/admin/corporate/${id}`, payload)).data
        : (await api.post('/admin/corporate', payload)).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'corporate'] }),
  })
}

export function useRetireCorporate() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api.post(`/admin/corporate/${id}/retire`),
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'corporate'] }),
  })
}

/* ---------------------------------------------------------------- offers */

export type OfferCampaign = {
  id: number
  code: string
  name: string
  description?: string | null
  bannerHeadline?: string | null
  discountType: number
  discountValue: number
  maxDiscountAmount?: number | null
  minOrderAmount: number
  validFrom: string
  validTo: string
  usageCap?: number | null
  usageCount: number
  perMemberCap?: number | null
  isActive: boolean
  showAsWebsiteBanner: boolean
  status: number
  branchNames: string[]
  planNames: string[]
  redemptions: number
  discountGiven: number
  revenueBooked: number
  daysRemaining: number
}

export type OffersResponse = {
  today: string
  live: number
  scheduled: number
  onBanner: number
  redemptionsAllTime: number
  discountGivenAllTime: number
  revenueBookedAllTime: number
  campaigns: OfferCampaign[]
}

export type OffPeakResponse = {
  plans: {
    id: number
    name: string
    slug: string
    basePrice: number
    isActive: boolean
    showOnWebsite: boolean
    windowStart: string
    windowEnd: string
    activeSubscribers: number
  }[]
  offPeakRefusalsLast30Days: number
}

/** 0 Scheduled · 1 Live · 2 Expired · 3 Sold out · 4 Paused */
export const OFFER_STATUS = ['Scheduled', 'Live', 'Expired', 'Sold out', 'Paused'] as const

export function useOfferCampaigns(): UseQueryResult<OffersResponse> {
  return useQuery({
    queryKey: moduleFourKeys.offers,
    queryFn: () => get<OffersResponse>('/admin/offers/campaigns'),
    staleTime: 60_000,
  })
}

export function useOffPeak(): UseQueryResult<OffPeakResponse> {
  return useQuery({
    queryKey: moduleFourKeys.offPeak,
    queryFn: () => get<OffPeakResponse>('/admin/offers/off-peak'),
    staleTime: 5 * 60 * 1000,
  })
}

export function useSetOfferBanner() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, show, bannerHeadline }: { id: number; show: boolean; bannerHeadline?: string }) =>
      api.post(`/admin/offers/campaigns/${id}/banner`, { show, bannerHeadline }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['admin', 'offers'] })
      // The public banner reads the same row, so its cache has to drop too.
      void client.invalidateQueries({ queryKey: ['public', 'offer'] })
    },
  })
}

export function useSetOfferState() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...payload }: { id: number; active: boolean; endToday?: boolean; extendToDate?: string }) =>
      api.post(`/admin/offers/campaigns/${id}/state`, payload),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['admin', 'offers'] })
      void client.invalidateQueries({ queryKey: ['public', 'offer'] })
    },
  })
}

/* ---------------------------------------------------------------- feed */

export type AdminFeedPost = {
  id: number
  kind: string
  title: string
  body?: string | null
  imageUrl?: string | null
  likeCount: number
  isPinned: boolean
  isVisible: boolean
  postedAtUtc: string
  memberId?: number | null
  memberName?: string | null
  memberCode?: string | null
  branchName?: string | null
  consentGiven?: boolean | null
}

export type AdminFeed = { prsThisWeek: number; hidden: number; posts: AdminFeedPost[] }

export function useAdminFeed(branchId?: number): UseQueryResult<AdminFeed> {
  return useQuery({
    queryKey: moduleFourKeys.feed(branchId),
    queryFn: () => get<AdminFeed>('/admin/feed', { branchId }),
    staleTime: 30_000,
  })
}

export function useAnnounce() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (payload: { title: string; body?: string; branchId?: number; pin: boolean }) =>
      api.post('/admin/feed/announce', payload),
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'feed'] }),
  })
}

export function useSetPostVisibility() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, visible }: { id: number; visible: boolean }) =>
      api.post(`/admin/feed/${id}/visibility`, { visible }),
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'feed'] }),
  })
}

export function usePinPost() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, pinned }: { id: number; pinned: boolean }) =>
      api.post(`/admin/feed/${id}/pin`, { pinned }),
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'feed'] }),
  })
}

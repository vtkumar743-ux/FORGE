import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type {
  BadgeRow,
  BookingResult,
  CheckoutResult,
  ExerciseOption,
  FreezeRequestRow,
  GatewayOrder,
  InvoiceDetail,
  MembershipPage,
  NotificationsPage,
  PlanOption,
  PortalBooking,
  PortalCard,
  PortalHome,
  PortalMember,
  PortalSession,
  PortalTimetable,
  Program,
  ProgressPage,
  Quote,
  ReferralOverview,
  WorkoutHistoryRow,
} from './types'

/* ============================================================================
   One hook per portal resource.

   The booking mutations are optimistic (03 §6, and the NFR that says so): the
   card fills its own spot the instant it is tapped, and the server's real counts
   replace the guess on the same round trip. A rollback restores the exact cache
   snapshot rather than refetching, so a failed book never blanks the timetable.
   ============================================================================ */

export const portalKeys = {
  home: ['portal', 'home'] as const,
  card: ['portal', 'card'] as const,
  me: ['portal', 'me'] as const,
  timetable: (params: unknown) => ['portal', 'timetable', params] as const,
  bookings: (scope: string) => ['portal', 'bookings', scope] as const,
  membership: ['portal', 'membership'] as const,
  plans: (branchId?: number) => ['portal', 'plans', branchId ?? null] as const,
  quote: (params: unknown) => ['portal', 'quote', params] as const,
  invoice: (id: number) => ['portal', 'invoice', id] as const,
  program: ['portal', 'program'] as const,
  workoutHistory: ['portal', 'workouts', 'history'] as const,
  exercises: (q: string) => ['portal', 'exercises', q] as const,
  progress: ['portal', 'progress'] as const,
  referrals: ['portal', 'referrals'] as const,
  notifications: (unreadOnly: boolean) => ['portal', 'notifications', unreadOnly] as const,
}

async function get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  return (await api.get<T>(url, { params })).data
}

/* ---------------------------------------------------------------- home */

export function usePortalHome(): UseQueryResult<PortalHome> {
  return useQuery({
    queryKey: portalKeys.home,
    queryFn: () => get<PortalHome>('/portal/home'),
    // Occupancy and spots-left age fast; the rest of the payload is cheap to bring along.
    staleTime: 30_000,
    refetchInterval: 120_000,
  })
}

export function usePortalCard(): UseQueryResult<PortalCard> {
  return useQuery({ queryKey: portalKeys.card, queryFn: () => get<PortalCard>('/portal/card'), staleTime: 300_000 })
}

export function usePortalMe(): UseQueryResult<PortalMember> {
  return useQuery({ queryKey: portalKeys.me, queryFn: () => get<PortalMember>('/portal/me'), staleTime: 300_000 })
}

export function useUpdateProfile() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => api.put('/portal/me', body).then((r) => r.data as PortalMember),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: portalKeys.me })
      void client.invalidateQueries({ queryKey: portalKeys.home })
    },
  })
}

/* ---------------------------------------------------------------- booking */

export interface TimetableParams {
  branchId?: number
  formatSlug?: string
  trainerSlug?: string
  timeOfDay?: string
  from?: string
  days?: number
}

export function useTimetable(params: TimetableParams): UseQueryResult<PortalTimetable> {
  return useQuery({
    queryKey: portalKeys.timetable(params),
    queryFn: () => get<PortalTimetable>('/portal/timetable', params as Record<string, unknown>),
    // Keeps the previous sheet on screen through a filter change instead of blinking to skeletons.
    placeholderData: (previous) => previous,
    staleTime: 20_000,
  })
}

export function useMyBookings(scope: 'upcoming' | 'past'): UseQueryResult<PortalBooking[]> {
  return useQuery({
    queryKey: portalKeys.bookings(scope),
    queryFn: () => get<PortalBooking[]>('/portal/bookings', { scope }),
    staleTime: 20_000,
  })
}

/** Applies the optimistic edit to every cached timetable page at once. */
function patchSession(
  client: ReturnType<typeof useQueryClient>,
  sessionId: number,
  patch: (session: PortalSession) => PortalSession,
) {
  client.setQueriesData<PortalTimetable>({ queryKey: ['portal', 'timetable'] }, (previous) => {
    if (!previous) return previous
    return {
      ...previous,
      sessions: previous.sessions.map((session) => (session.id === sessionId ? patch(session) : session)),
    }
  })
}

export function useBookClass() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (input: { sessionId: number; allowWaitlist?: boolean }) =>
      api
        .post('/portal/bookings', { sessionId: input.sessionId, allowWaitlist: input.allowWaitlist ?? true })
        .then((r) => r.data as BookingResult),

    onMutate: async ({ sessionId }) => {
      await client.cancelQueries({ queryKey: ['portal', 'timetable'] })
      const snapshot = client.getQueriesData<PortalTimetable>({ queryKey: ['portal', 'timetable'] })

      patchSession(client, sessionId, (session) => {
        const goingOnWaitlist = session.spotsLeft === 0
        return {
          ...session,
          // -1 is the placeholder id: truthy enough to flip the card into "booked", and
          // replaced by the real one the moment the server answers.
          myBookingId: -1,
          myBookingStatus: goingOnWaitlist ? 1 : 0,
          myWaitlistPosition: goingOnWaitlist ? session.waitlistCount + 1 : null,
          bookedCount: goingOnWaitlist ? session.bookedCount : session.bookedCount + 1,
          spotsLeft: goingOnWaitlist ? 0 : Math.max(0, session.spotsLeft - 1),
          waitlistCount: goingOnWaitlist ? session.waitlistCount + 1 : session.waitlistCount,
          canBook: false,
          canJoinWaitlist: false,
          canCancel: true,
        }
      })

      return { snapshot }
    },

    onError: (_error, _input, context) => {
      // Restore exactly what was on screen — refetching would blank the sheet mid-tap.
      context?.snapshot.forEach(([key, data]) => client.setQueryData(key, data))
    },

    onSuccess: (result, { sessionId }) => {
      patchSession(client, sessionId, (session) => ({
        ...session,
        myBookingId: result.bookingId,
        myBookingStatus: result.status,
        myWaitlistPosition: result.waitlistPosition,
        bookedCount: result.bookedCount,
        spotsLeft: result.spotsLeft,
        waitlistCount: result.waitlistCount,
      }))
      void client.invalidateQueries({ queryKey: ['portal', 'bookings'] })
      void client.invalidateQueries({ queryKey: portalKeys.home })
    },
  })
}

export function useCancelBooking() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (input: { bookingId: number; sessionId?: number; reason?: string }) =>
      api
        .delete(`/portal/bookings/${input.bookingId}`, { params: { reason: input.reason } })
        .then((r) => r.data as BookingResult),

    onMutate: async ({ sessionId }) => {
      if (!sessionId) return { snapshot: [] as ReturnType<typeof client.getQueriesData<PortalTimetable>> }
      await client.cancelQueries({ queryKey: ['portal', 'timetable'] })
      const snapshot = client.getQueriesData<PortalTimetable>({ queryKey: ['portal', 'timetable'] })

      patchSession(client, sessionId, (session) => ({
        ...session,
        myBookingId: null,
        myBookingStatus: null,
        myWaitlistPosition: null,
        bookedCount: session.myBookingStatus === 0 ? Math.max(0, session.bookedCount - 1) : session.bookedCount,
        spotsLeft: session.myBookingStatus === 0 ? session.spotsLeft + 1 : session.spotsLeft,
        waitlistCount: session.myBookingStatus === 1 ? Math.max(0, session.waitlistCount - 1) : session.waitlistCount,
        canBook: true,
        canCancel: false,
      }))

      return { snapshot }
    },

    onError: (_error, _input, context) => {
      context?.snapshot.forEach(([key, data]) => client.setQueryData(key, data))
    },

    onSettled: () => {
      void client.invalidateQueries({ queryKey: ['portal', 'timetable'] })
      void client.invalidateQueries({ queryKey: ['portal', 'bookings'] })
      void client.invalidateQueries({ queryKey: portalKeys.home })
    },
  })
}

export function useRateClass() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { bookingId: number; score: number; comment?: string }) =>
      api.post(`/portal/bookings/${input.bookingId}/rate`, { score: input.score, comment: input.comment }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['portal', 'bookings'] })
      void client.invalidateQueries({ queryKey: portalKeys.home })
    },
  })
}

/* ---------------------------------------------------------------- membership */

export function useMembership(): UseQueryResult<MembershipPage> {
  return useQuery({
    queryKey: portalKeys.membership,
    queryFn: () => get<MembershipPage>('/portal/membership'),
    staleTime: 60_000,
  })
}

export function usePlanOptions(branchId?: number): UseQueryResult<PlanOption[]> {
  return useQuery({
    queryKey: portalKeys.plans(branchId),
    queryFn: () => get<PlanOption[]>('/portal/membership/plans', { branchId }),
    staleTime: 300_000,
  })
}

export interface QuoteParams {
  planId: number
  branchId?: number
  couponCode?: string
  upgradeNow?: boolean
}

export function useQuote(params: QuoteParams | null): UseQueryResult<Quote> {
  return useQuery({
    queryKey: portalKeys.quote(params),
    queryFn: () => get<Quote>('/portal/membership/quote', params as unknown as Record<string, unknown>),
    enabled: params !== null,
    staleTime: 30_000,
    retry: false,
  })
}

export function useRenew() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { planId: number; branchId?: number; couponCode?: string; upgradeNow?: boolean; autoRenew?: boolean }) =>
      api.post('/portal/membership/renew', body).then((r) => r.data as CheckoutResult),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: portalKeys.membership })
      void client.invalidateQueries({ queryKey: portalKeys.home })
      void client.invalidateQueries({ queryKey: portalKeys.card })
    },
  })
}

export function usePayInvoice() {
  return useMutation({
    mutationFn: (invoiceId: number) =>
      api.post(`/portal/invoices/${invoiceId}/pay`).then((r) => r.data as GatewayOrder),
  })
}

/**
 * Settles a gateway order. With real keys the browser has already run Razorpay's
 * checkout and holds a signature; in sandbox simulation there is nothing to run,
 * so the ids stand in and the API stamps the payment as simulated.
 */
export function useVerifyPayment() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { orderId: string; paymentId: string; signature?: string }) =>
      api.post('/payments/razorpay/verify', {
        razorpayOrderId: input.orderId,
        razorpayPaymentId: input.paymentId,
        razorpaySignature: input.signature ?? '',
      }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: portalKeys.membership })
      void client.invalidateQueries({ queryKey: portalKeys.home })
      void client.invalidateQueries({ queryKey: portalKeys.card })
    },
  })
}

export function useInvoice(id: number | null): UseQueryResult<InvoiceDetail> {
  return useQuery({
    queryKey: portalKeys.invoice(id ?? 0),
    queryFn: () => get<InvoiceDetail>(`/portal/invoices/${id}`),
    enabled: id !== null,
  })
}

export function useRequestFreeze() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { subscriptionId: number; from: string; to: string; reason: string }) =>
      api.post('/portal/membership/freeze', body).then((r) => r.data as FreezeRequestRow),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: portalKeys.membership })
      void client.invalidateQueries({ queryKey: portalKeys.home })
    },
  })
}

export function useWithdrawFreeze() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api.delete(`/portal/membership/freeze/${id}`),
    onSuccess: () => void client.invalidateQueries({ queryKey: portalKeys.membership }),
  })
}

/* ---------------------------------------------------------------- workouts */

export function useProgram(): UseQueryResult<Program> {
  return useQuery({
    queryKey: portalKeys.program,
    queryFn: () => get<Program>('/portal/program'),
    // A member with no programme yet is a 404 by design; retrying it is pure noise.
    retry: false,
    staleTime: 60_000,
  })
}

export function useWorkoutHistory(): UseQueryResult<WorkoutHistoryRow[]> {
  return useQuery({
    queryKey: portalKeys.workoutHistory,
    queryFn: () => get<WorkoutHistoryRow[]>('/portal/workouts/history'),
    staleTime: 60_000,
  })
}

export function useExerciseLibrary(q: string): UseQueryResult<ExerciseOption[]> {
  return useQuery({
    queryKey: portalKeys.exercises(q),
    queryFn: () => get<ExerciseOption[]>('/portal/exercises', q ? { q } : undefined),
    staleTime: 600_000,
  })
}

export function useLogSet() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: {
      exerciseId: number
      programExerciseId?: number
      setNumber: number
      reps: number
      weightKg: number
      rpe?: number
      notes?: string
      performedOn?: string
    }) => api.post('/portal/workouts/sets', body).then((r) => r.data as LogSetResponse),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: portalKeys.program })
      void client.invalidateQueries({ queryKey: portalKeys.workoutHistory })
      void client.invalidateQueries({ queryKey: portalKeys.progress })
      void client.invalidateQueries({ queryKey: portalKeys.home })
    },
  })
}

export type LogSetResponse = import('./types').LogSetResult

export function useDeleteSet() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api.delete(`/portal/workouts/sets/${id}`),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: portalKeys.program })
      void client.invalidateQueries({ queryKey: portalKeys.workoutHistory })
    },
  })
}

export function useDismissCelebration() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (logId: number) => api.post(`/portal/workouts/celebrations/${logId}/seen`),
    onSuccess: () => void client.invalidateQueries({ queryKey: portalKeys.home }),
  })
}

/* ---------------------------------------------------------------- progress */

export function useProgress(): UseQueryResult<ProgressPage> {
  return useQuery({
    queryKey: portalKeys.progress,
    queryFn: () => get<ProgressPage>('/portal/progress'),
    staleTime: 60_000,
  })
}

export function useAddScan() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => api.post('/portal/progress/scans', body),
    onSuccess: () => void client.invalidateQueries({ queryKey: portalKeys.progress }),
  })
}

export function useDeleteScan() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api.delete(`/portal/progress/scans/${id}`),
    onSuccess: () => void client.invalidateQueries({ queryKey: portalKeys.progress }),
  })
}

export function useUploadPhoto() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { file: File; pose: string; takenOn: string; weightKg?: number; notes?: string }) => {
      const form = new FormData()
      form.append('file', input.file)
      form.append('pose', input.pose)
      form.append('takenOn', input.takenOn)
      if (input.weightKg != null) form.append('weightKg', String(input.weightKg))
      if (input.notes) form.append('notes', input.notes)
      // Let the browser set the multipart boundary; the JSON default header would break it.
      return api.post('/portal/progress/photos', form, { headers: { 'Content-Type': undefined } })
    },
    onSuccess: () => void client.invalidateQueries({ queryKey: portalKeys.progress }),
  })
}

export function useDeletePhoto() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api.delete(`/portal/progress/photos/${id}`),
    onSuccess: () => void client.invalidateQueries({ queryKey: portalKeys.progress }),
  })
}

export function useMarkBadgesSeen() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => api.post('/portal/progress/badges/seen'),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: portalKeys.progress })
      void client.invalidateQueries({ queryKey: portalKeys.home })
    },
  })
}

export function useRefreshBadges() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => api.post('/portal/progress/badges/refresh').then((r) => r.data as BadgeRow[]),
    onSuccess: () => void client.invalidateQueries({ queryKey: portalKeys.progress }),
  })
}

/* ---------------------------------------------------------------- engagement */

export function useReferrals(): UseQueryResult<ReferralOverview> {
  return useQuery({
    queryKey: portalKeys.referrals,
    queryFn: () => get<ReferralOverview>('/portal/referrals'),
    staleTime: 120_000,
  })
}

export function useInvite() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { name: string; phone: string; branchId?: number }) => api.post('/portal/referrals', body),
    onSuccess: () => void client.invalidateQueries({ queryKey: portalKeys.referrals }),
  })
}

export function useNotifications(unreadOnly = false): UseQueryResult<NotificationsPage> {
  return useQuery({
    queryKey: portalKeys.notifications(unreadOnly),
    queryFn: () => get<NotificationsPage>('/portal/notifications', { unreadOnly }),
    staleTime: 30_000,
  })
}

export function useMarkNotificationRead() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api.post(`/portal/notifications/${id}/read`),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['portal', 'notifications'] })
      void client.invalidateQueries({ queryKey: portalKeys.home })
    },
  })
}

export function useMarkAllRead() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => api.post('/portal/notifications/read-all'),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['portal', 'notifications'] })
      void client.invalidateQueries({ queryKey: portalKeys.home })
    },
  })
}

/* ---------------------------------------------------------------- community (Module 4.5) */

export type FeedPostRow = {
  id: number
  kind: string
  title: string
  body?: string | null
  imageUrl?: string | null
  likeCount: number
  isPinned: boolean
  postedAtUtc: string
  isMine: boolean
  authorName?: string | null
  authorPhotoUrl?: string | null
  branchName?: string | null
  meta?: Record<string, unknown> | null
  ago: string
}

export type CommunityFeed = {
  consentGiven: boolean
  consentPrompt?: string | null
  posts: FeedPostRow[]
}

export function useCommunityFeed(scope: 'branch' | 'network' = 'branch'): UseQueryResult<CommunityFeed> {
  return useQuery({
    queryKey: ['portal', 'feed', scope],
    queryFn: () => get<CommunityFeed>('/portal/community/feed', { scope }),
    staleTime: 30_000,
  })
}

export function useLikePost() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api.post(`/portal/community/feed/${id}/like`),
    // Optimistic: a like that waits for the network reads as a broken button.
    onMutate: async (id: number) => {
      await client.cancelQueries({ queryKey: ['portal', 'feed'] })
      const snapshots = client.getQueriesData<CommunityFeed>({ queryKey: ['portal', 'feed'] })
      snapshots.forEach(([key, value]) => {
        if (!value) return
        client.setQueryData<CommunityFeed>(key, {
          ...value,
          posts: value.posts.map((post) =>
            post.id === id ? { ...post, likeCount: post.likeCount + 1 } : post,
          ),
        })
      })
      return { snapshots }
    },
    onError: (_error, _id, context) => {
      context?.snapshots.forEach(([key, value]) => client.setQueryData(key, value))
    },
  })
}

export type PrShareCard = {
  memberName: string
  exercise: string
  weightKg: number
  reps: number
  estimatedOneRepMax: number
  previousBest?: number | null
  gainKg?: number | null
  performedOn: string
  branchName: string
  shareText: string
  whatsAppUrl: string
}

export function usePrShareCard(workoutLogId: number | null): UseQueryResult<PrShareCard> {
  return useQuery({
    queryKey: ['portal', 'share', workoutLogId],
    queryFn: () => get<PrShareCard>(`/portal/community/share/pr/${workoutLogId}`),
    enabled: workoutLogId != null,
    staleTime: 5 * 60 * 1000,
  })
}

export type CorporateStanding =
  | { enrolled: false }
  | {
      enrolled: true
      id: number
      companyName: string
      code: string
      discountPercent: number
      waiveAdmissionFee: boolean
      enrolledOn: string
      validTo: string
      employeeId?: string | null
    }

export type CorporatePreview = {
  accepted: boolean
  message?: string | null
  accountId: number
  companyName: string
  discountPercent: number
  waiveAdmissionFee: boolean
  seatSummary: string
  validTo: string
}

export function useMyCorporate(): UseQueryResult<CorporateStanding> {
  return useQuery({
    queryKey: ['portal', 'corporate'],
    queryFn: () => get<CorporateStanding>('/portal/community/corporate/mine'),
    staleTime: 5 * 60 * 1000,
  })
}

export function useEnrolCorporate() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (payload: { code: string; employeeId?: string; workEmail?: string }) =>
      (await api.post<CorporatePreview>('/portal/community/corporate/enrol', payload)).data,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['portal', 'corporate'] })
      void client.invalidateQueries({ queryKey: portalKeys.membership })
    },
  })
}

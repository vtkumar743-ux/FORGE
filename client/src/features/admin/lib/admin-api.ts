import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type {
  AbsenteeRow,
  AdminPage,
  AdminPageListItem,
  AdminSection,
  AdminSessionRow,
  AdminSetting,
  AttendanceToday,
  BlogPostRow,
  CheckInResult,
  ClassFormatRow,
  CollectionsResponse,
  ConflictRow,
  CouponRow,
  DashboardResponse,
  FaqItem,
  HeatmapResponse,
  InvoiceDetail,
  InvoiceRow,
  LeadBoard,
  LeadDetail,
  MediaAsset,
  MemberDetail,
  MemberListRow,
  Paged,
  PlanRow,
  Quote,
  RoomRow,
  RosterEntry,
  ScheduleRow,
  SubscriptionRow,
  Testimonial,
  Transformation,
} from './types'

/* ============================================================================
   One hook per admin resource. Everything mutating invalidates the query keys it
   touches, so a sale updates the dashboard, the member and the invoice list
   without any screen having to know about the others.
   ============================================================================ */

export const adminKeys = {
  dashboard: (branchId?: number, days?: number) => ['admin', 'dashboard', branchId ?? null, days ?? 30] as const,

  cmsPages: ['admin', 'cms', 'pages'] as const,
  cmsPage: (id: number) => ['admin', 'cms', 'page', id] as const,
  settings: ['admin', 'cms', 'settings'] as const,
  media: (params: unknown) => ['admin', 'media', params] as const,
  mediaFolders: ['admin', 'media', 'folders'] as const,
  testimonials: ['admin', 'content', 'testimonials'] as const,
  transformations: ['admin', 'content', 'transformations'] as const,
  faqs: ['admin', 'content', 'faqs'] as const,
  posts: ['admin', 'content', 'posts'] as const,

  members: (params: unknown) => ['admin', 'members', params] as const,
  member: (id: number) => ['admin', 'member', id] as const,
  birthdays: (days: number) => ['admin', 'members', 'birthdays', days] as const,

  plans: ['admin', 'billing', 'plans'] as const,
  coupons: ['admin', 'billing', 'coupons'] as const,
  subscriptions: (params: unknown) => ['admin', 'billing', 'subscriptions', params] as const,
  invoices: (params: unknown) => ['admin', 'billing', 'invoices', params] as const,
  invoice: (id: number) => ['admin', 'billing', 'invoice', id] as const,
  collections: (branchId?: number) => ['admin', 'billing', 'collections', branchId ?? null] as const,
  quote: (params: unknown) => ['admin', 'billing', 'quote', params] as const,

  formats: ['admin', 'scheduling', 'formats'] as const,
  rooms: (branchId?: number) => ['admin', 'scheduling', 'rooms', branchId ?? null] as const,
  schedules: (params: unknown) => ['admin', 'scheduling', 'schedules', params] as const,
  sessions: (params: unknown) => ['admin', 'scheduling', 'sessions', params] as const,
  roster: (id: number) => ['admin', 'scheduling', 'roster', id] as const,

  attendanceToday: (params: unknown) => ['admin', 'attendance', 'today', params] as const,
  heatmap: (params: unknown) => ['admin', 'attendance', 'heatmap', params] as const,
  absentees: (params: unknown) => ['admin', 'attendance', 'absentees', params] as const,

  leadBoard: (params: unknown) => ['admin', 'leads', 'board', params] as const,
  lead: (id: number) => ['admin', 'leads', id] as const,
  leadList: (params: unknown) => ['admin', 'leads', 'list', params] as const,

  trainers: ['admin', 'trainers'] as const,
  gateway: ['admin', 'payments', 'gateway'] as const,
}

async function get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  const clean = params
    ? Object.fromEntries(Object.entries(params).filter(([, value]) => value !== undefined && value !== null && value !== ''))
    : undefined
  return (await api.get<T>(url, { params: clean })).data
}

/** Invalidates every admin query — used after actions whose blast radius is wide (a sale). */
function useInvalidateAll() {
  const client = useQueryClient()
  return () => void client.invalidateQueries({ queryKey: ['admin'] })
}

/* ---------------------------------------------------------------- dashboard */

export function useDashboard(branchId?: number, days = 30): UseQueryResult<DashboardResponse> {
  return useQuery({
    queryKey: adminKeys.dashboard(branchId, days),
    queryFn: () => get<DashboardResponse>('/admin/dashboard', { branchId, days }),
    staleTime: 60_000,
    refetchInterval: 120_000,
  })
}

/* ---------------------------------------------------------------- cms */

export function useCmsPages(): UseQueryResult<AdminPageListItem[]> {
  return useQuery({ queryKey: adminKeys.cmsPages, queryFn: () => get<AdminPageListItem[]>('/admin/cms/pages') })
}

export function useCmsPage(id: number | null): UseQueryResult<AdminPage> {
  return useQuery({
    queryKey: adminKeys.cmsPage(id ?? 0),
    queryFn: () => get<AdminPage>(`/admin/cms/pages/${id}`),
    enabled: id !== null,
  })
}

export function useSaveSection() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (input: {
      pageId: number
      sectionId: number
      content: Record<string, unknown>
      publish: boolean
      isVisible?: boolean
    }) =>
      (await api.put<AdminSection>(`/cms/sections/${input.sectionId}`, {
        content: input.content,
        publish: input.publish,
        isVisible: input.isVisible,
      })).data,
    onSuccess: (_data, input) => {
      void client.invalidateQueries({ queryKey: adminKeys.cmsPage(input.pageId) })
      // The public site reads the same rows; drop its cache so preview matches live.
      void client.invalidateQueries({ queryKey: ['cms'] })
    },
  })
}

export function useSectionAction() {
  const client = useQueryClient()
  const refresh = (pageId: number) => {
    void client.invalidateQueries({ queryKey: adminKeys.cmsPage(pageId) })
    void client.invalidateQueries({ queryKey: adminKeys.cmsPages })
    void client.invalidateQueries({ queryKey: ['cms'] })
  }

  return {
    publish: useMutation({
      mutationFn: async (input: { pageId: number; sectionId: number }) =>
        (await api.post(`/cms/sections/${input.sectionId}/publish`)).data,
      onSuccess: (_d, input) => refresh(input.pageId),
    }),
    discardDraft: useMutation({
      mutationFn: async (input: { pageId: number; sectionId: number }) =>
        (await api.post(`/admin/cms/sections/${input.sectionId}/discard-draft`)).data,
      onSuccess: (_d, input) => refresh(input.pageId),
    }),
    setVisibility: useMutation({
      mutationFn: async (input: { pageId: number; sectionId: number; visible: boolean }) =>
        (await api.post(`/cms/sections/${input.sectionId}/visibility?visible=${input.visible}`)).data,
      onSuccess: (_d, input) => refresh(input.pageId),
    }),
    reorder: useMutation({
      mutationFn: async (input: { pageId: number; sectionIds: number[] }) =>
        (await api.post(`/cms/pages/${input.pageId}/reorder`, { sectionIds: input.sectionIds })).data,
      onSuccess: (_d, input) => refresh(input.pageId),
    }),
    create: useMutation({
      mutationFn: async (input: {
        pageId: number
        sectionType: number
        key: string
        adminLabel: string
        content: Record<string, unknown>
        branchId?: number | null
      }) => (await api.post(`/admin/cms/pages/${input.pageId}/sections`, input)).data,
      onSuccess: (_d, input) => refresh(input.pageId),
    }),
    duplicate: useMutation({
      mutationFn: async (input: { pageId: number; sectionId: number }) =>
        (await api.post(`/admin/cms/sections/${input.sectionId}/duplicate`)).data,
      onSuccess: (_d, input) => refresh(input.pageId),
    }),
    remove: useMutation({
      mutationFn: async (input: { pageId: number; sectionId: number }) =>
        (await api.delete(`/admin/cms/sections/${input.sectionId}`)).data,
      onSuccess: (_d, input) => refresh(input.pageId),
    }),
    updateMeta: useMutation({
      mutationFn: async (input: {
        pageId: number
        sectionId: number
        adminLabel?: string
        branchId?: number | null
      }) => (await api.patch(`/admin/cms/sections/${input.sectionId}/meta`, input)).data,
      onSuccess: (_d, input) => refresh(input.pageId),
    }),
  }
}

export function useSavePageSeo() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
      (await api.put(`/admin/cms/pages/${input.id}`, input.body)).data,
    onSuccess: (_d, input) => {
      void client.invalidateQueries({ queryKey: adminKeys.cmsPage(input.id) })
      void client.invalidateQueries({ queryKey: adminKeys.cmsPages })
      void client.invalidateQueries({ queryKey: ['cms'] })
    },
  })
}

export function usePublishPage() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (id: number) => (await api.post(`/admin/cms/pages/${id}/publish`)).data,
    onSuccess: (_d, id) => {
      void client.invalidateQueries({ queryKey: adminKeys.cmsPage(id) })
      void client.invalidateQueries({ queryKey: adminKeys.cmsPages })
      void client.invalidateQueries({ queryKey: ['cms'] })
    },
  })
}

export function useSettings(): UseQueryResult<AdminSetting[]> {
  return useQuery({ queryKey: adminKeys.settings, queryFn: () => get<AdminSetting[]>('/admin/cms/settings') })
}

export function useSaveSettings() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (values: Record<string, string>) =>
      (await api.put<AdminSetting[]>('/admin/cms/settings', { values })).data,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: adminKeys.settings })
      void client.invalidateQueries({ queryKey: ['cms'] })
    },
  })
}

/* ---------------------------------------------------------------- media */

export interface MediaQuery {
  q?: string
  folder?: string
  kind?: number
  page?: number
  pageSize?: number
}

export function useMedia(params: MediaQuery): UseQueryResult<Paged<MediaAsset>> {
  return useQuery({
    queryKey: adminKeys.media(params),
    queryFn: () => get<Paged<MediaAsset>>('/media', params as Record<string, unknown>),
    placeholderData: (previous) => previous,
  })
}

export function useMediaFolders(): UseQueryResult<string[]> {
  return useQuery({ queryKey: adminKeys.mediaFolders, queryFn: () => get<string[]>('/media/folders') })
}

export function useUploadMedia() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (input: { file: File; altText: string; folder?: string; caption?: string; credit?: string; tags?: string }) => {
      const form = new FormData()
      form.append('file', input.file)
      form.append('altText', input.altText)
      if (input.folder) form.append('folder', input.folder)
      if (input.caption) form.append('caption', input.caption)
      if (input.credit) form.append('credit', input.credit)
      if (input.tags) form.append('tags', input.tags)
      // Let the browser set the multipart boundary; a hardcoded JSON header breaks the upload.
      const response = await api.post<MediaAsset>('/media', form, { headers: { 'Content-Type': undefined } })
      return response.data
    },
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['admin', 'media'] })
    },
  })
}

export function useUpdateMedia() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
      (await api.put<MediaAsset>(`/media/${input.id}`, input.body)).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'media'] }),
  })
}

export function useDeleteMedia() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (input: { id: number; force?: boolean }) =>
      (await api.delete(`/media/${input.id}`, { params: { force: input.force } })).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: ['admin', 'media'] }),
  })
}

/* ---------------------------------------------------------------- collections */

function collection<T>(key: readonly unknown[], path: string) {
  return {
    useList: (): UseQueryResult<T[]> => useQuery({ queryKey: key, queryFn: () => get<T[]>(path) }),
    path,
    key,
  }
}

export const testimonialsResource = collection<Testimonial>(adminKeys.testimonials, '/admin/content/testimonials')
export const transformationsResource = collection<Transformation>(adminKeys.transformations, '/admin/content/transformations')
export const faqsResource = collection<FaqItem>(adminKeys.faqs, '/admin/content/faqs')
export const postsResource = collection<BlogPostRow>(adminKeys.posts, '/admin/content/posts')

export function useCollectionMutation(path: string, key: readonly unknown[]) {
  const client = useQueryClient()
  const invalidate = () => {
    void client.invalidateQueries({ queryKey: key })
    void client.invalidateQueries({ queryKey: ['public'] })
    void client.invalidateQueries({ queryKey: ['cms'] })
  }

  return {
    create: useMutation({
      mutationFn: async (body: Record<string, unknown>) => (await api.post(path, body)).data,
      onSuccess: invalidate,
    }),
    update: useMutation({
      mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
        (await api.put(`${path}/${input.id}`, input.body)).data,
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: async (id: number) => (await api.delete(`${path}/${id}`)).data,
      onSuccess: invalidate,
    }),
  }
}

/* ---------------------------------------------------------------- members */

export interface MemberQuery {
  q?: string
  branchId?: number
  status?: number
  tag?: string
  churn?: number
  expiringSoon?: boolean
  hasDues?: boolean
  birthdayMonth?: number
  sort?: string
  page?: number
  pageSize?: number
}

export function useMembers(params: MemberQuery): UseQueryResult<Paged<MemberListRow>> {
  return useQuery({
    queryKey: adminKeys.members(params),
    queryFn: () => get<Paged<MemberListRow>>('/admin/members', params as Record<string, unknown>),
    // Keeps the table on screen through a filter change instead of blinking to skeletons.
    placeholderData: (previous) => previous,
  })
}

export function useMember(id: number | null): UseQueryResult<MemberDetail> {
  return useQuery({
    queryKey: adminKeys.member(id ?? 0),
    queryFn: () => get<MemberDetail>(`/admin/members/${id}`),
    enabled: id !== null,
  })
}

export function useMemberMutations() {
  const client = useQueryClient()
  const invalidate = (id?: number) => {
    void client.invalidateQueries({ queryKey: ['admin', 'members'] })
    if (id) void client.invalidateQueries({ queryKey: adminKeys.member(id) })
    void client.invalidateQueries({ queryKey: ['admin', 'dashboard'] })
  }

  return {
    create: useMutation({
      mutationFn: async (body: Record<string, unknown>) =>
        (await api.post<MemberListRow>('/admin/members', body)).data,
      onSuccess: () => invalidate(),
    }),
    update: useMutation({
      mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
        (await api.put(`/admin/members/${input.id}`, input.body)).data,
      onSuccess: (_d, input) => invalidate(input.id),
    }),
    setStatus: useMutation({
      mutationFn: async (input: { id: number; status: number }) =>
        (await api.post(`/admin/members/${input.id}/status?status=${input.status}`)).data,
      onSuccess: (_d, input) => invalidate(input.id),
    }),
    bulk: useMutation({
      mutationFn: async (body: Record<string, unknown>) => (await api.post('/admin/members/bulk', body)).data,
      onSuccess: () => invalidate(),
    }),
    importCsv: useMutation({
      mutationFn: async (input: { file: File; defaultBranchId: number }) => {
        const form = new FormData()
        form.append('file', input.file)
        form.append('defaultBranchId', String(input.defaultBranchId))
        const response = await api.post<{ imported: number; skipped: string[]; skippedCount: number }>(
          '/admin/members/import',
          form,
          { headers: { 'Content-Type': undefined } },
        )
        return response.data
      },
      onSuccess: () => invalidate(),
    }),
  }
}

export function memberExportUrl(params: { branchId?: number; status?: number }): string {
  const search = new URLSearchParams()
  if (params.branchId) search.set('branchId', String(params.branchId))
  if (params.status !== undefined) search.set('status', String(params.status))
  return `/api/admin/members/export?${search.toString()}`
}

/* ---------------------------------------------------------------- billing */

export function usePlans(): UseQueryResult<PlanRow[]> {
  return useQuery({ queryKey: adminKeys.plans, queryFn: () => get<PlanRow[]>('/admin/billing/plans') })
}

export function useCoupons(): UseQueryResult<CouponRow[]> {
  return useQuery({ queryKey: adminKeys.coupons, queryFn: () => get<CouponRow[]>('/admin/billing/coupons') })
}

export function usePlanMutations() {
  const client = useQueryClient()
  const invalidate = () => {
    void client.invalidateQueries({ queryKey: adminKeys.plans })
    void client.invalidateQueries({ queryKey: ['public'] })
  }

  return {
    create: useMutation({
      mutationFn: async (body: Record<string, unknown>) => (await api.post('/admin/billing/plans', body)).data,
      onSuccess: invalidate,
    }),
    update: useMutation({
      mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
        (await api.put(`/admin/billing/plans/${input.id}`, input.body)).data,
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: async (id: number) => (await api.delete(`/admin/billing/plans/${id}`)).data,
      onSuccess: invalidate,
    }),
    setPrices: useMutation({
      mutationFn: async (input: { id: number; prices: unknown[] }) =>
        (await api.put(`/admin/billing/plans/${input.id}/prices`, { prices: input.prices })).data,
      onSuccess: invalidate,
    }),
  }
}

export function useCouponMutations() {
  const client = useQueryClient()
  const invalidate = () => {
    void client.invalidateQueries({ queryKey: adminKeys.coupons })
    void client.invalidateQueries({ queryKey: ['public'] })
  }

  return {
    create: useMutation({
      mutationFn: async (body: Record<string, unknown>) => (await api.post('/admin/billing/coupons', body)).data,
      onSuccess: invalidate,
    }),
    update: useMutation({
      mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
        (await api.put(`/admin/billing/coupons/${input.id}`, input.body)).data,
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: async (id: number) => (await api.delete(`/admin/billing/coupons/${id}`)).data,
      onSuccess: invalidate,
    }),
  }
}

export interface InvoiceQuery {
  q?: string
  branchId?: number
  status?: number
  unpaidOnly?: boolean
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

export function useInvoices(params: InvoiceQuery): UseQueryResult<Paged<InvoiceRow>> {
  return useQuery({
    queryKey: adminKeys.invoices(params),
    queryFn: () => get<Paged<InvoiceRow>>('/admin/billing/invoices', params as Record<string, unknown>),
    placeholderData: (previous) => previous,
  })
}

export function useInvoice(id: number | null): UseQueryResult<InvoiceDetail> {
  return useQuery({
    queryKey: adminKeys.invoice(id ?? 0),
    queryFn: () => get<InvoiceDetail>(`/admin/billing/invoices/${id}`),
    enabled: id !== null,
  })
}

export function useSubscriptions(params: {
  branchId?: number
  status?: number
  expiringSoon?: boolean
  q?: string
  page?: number
  pageSize?: number
}): UseQueryResult<Paged<SubscriptionRow>> {
  return useQuery({
    queryKey: adminKeys.subscriptions(params),
    queryFn: () => get<Paged<SubscriptionRow>>('/admin/billing/subscriptions', params),
    placeholderData: (previous) => previous,
  })
}

export function useCollections(branchId?: number): UseQueryResult<CollectionsResponse> {
  return useQuery({
    queryKey: adminKeys.collections(branchId),
    queryFn: () => get<CollectionsResponse>('/admin/billing/collections', { branchId }),
  })
}

export function useQuote(params: {
  memberId?: number
  planId?: number
  branchId?: number
  startsOn?: string
  couponCode?: string
  upgradeFromSubscriptionId?: number
}): UseQueryResult<Quote> {
  const ready = Boolean(params.memberId && params.planId && params.branchId)
  return useQuery({
    queryKey: adminKeys.quote(params),
    queryFn: () => get<Quote>('/admin/billing/quote', params),
    enabled: ready,
  })
}

export function useBillingActions() {
  const invalidateAll = useInvalidateAll()

  return {
    sell: useMutation({
      mutationFn: async (body: Record<string, unknown>) =>
        (await api.post<{ subscriptionId: number; invoiceId: number; invoiceNumber: string; grandTotal: number; amountDue: number; endsOn: string }>(
          '/admin/billing/subscriptions',
          body,
        )).data,
      onSuccess: invalidateAll,
    }),
    recordPayment: useMutation({
      mutationFn: async (body: Record<string, unknown>) => (await api.post('/admin/billing/payments', body)).data,
      onSuccess: invalidateAll,
    }),
    freeze: useMutation({
      mutationFn: async (input: { id: number; from: string; to: string; reason?: string }) =>
        (await api.post(`/admin/billing/subscriptions/${input.id}/freeze`, input)).data,
      onSuccess: invalidateAll,
    }),
    resume: useMutation({
      mutationFn: async (id: number) => (await api.post(`/admin/billing/subscriptions/${id}/resume`)).data,
      onSuccess: invalidateAll,
    }),
    cancel: useMutation({
      mutationFn: async (input: { id: number; reason: string }) =>
        (await api.post(`/admin/billing/subscriptions/${input.id}/cancel`, { reason: input.reason })).data,
      onSuccess: invalidateAll,
    }),
    remind: useMutation({
      mutationFn: async (id: number) => (await api.post(`/admin/billing/invoices/${id}/remind`)).data,
      onSuccess: invalidateAll,
    }),
    runCollections: useMutation({
      mutationFn: async () =>
        (await api.post<{ remindersSent: number }>('/admin/billing/collections/run')).data,
      onSuccess: invalidateAll,
    }),
    cancelInvoice: useMutation({
      mutationFn: async (input: { id: number; reason: string }) =>
        (await api.post(`/admin/billing/invoices/${input.id}/cancel`, { reason: input.reason })).data,
      onSuccess: invalidateAll,
    }),
    createOrder: useMutation({
      mutationFn: async (input: { invoiceId: number; amount?: number }) =>
        (await api.post<{
          orderId: string
          keyId?: string | null
          amountInr: number
          currency: string
          receipt: string
          isSimulated: boolean
          invoiceNumber: string
          memberName: string
          memberEmail?: string | null
          memberPhone: string
          notice?: string | null
        }>('/payments/razorpay/order', input)).data,
    }),
    verifyPayment: useMutation({
      mutationFn: async (body: Record<string, unknown>) => (await api.post('/payments/razorpay/verify', body)).data,
      onSuccess: invalidateAll,
    }),
  }
}

export function useGateway(): UseQueryResult<{ provider: string; isLive: boolean; keyId?: string | null; notice?: string | null }> {
  return useQuery({
    queryKey: adminKeys.gateway,
    queryFn: () => get<{ provider: string; isLive: boolean; keyId?: string | null; notice?: string | null }>('/payments/gateway'),
    staleTime: 10 * 60 * 1000,
  })
}

/* ---------------------------------------------------------------- scheduling */

export function useClassFormats(): UseQueryResult<ClassFormatRow[]> {
  return useQuery({ queryKey: adminKeys.formats, queryFn: () => get<ClassFormatRow[]>('/admin/scheduling/formats') })
}

export function useRooms(branchId?: number): UseQueryResult<RoomRow[]> {
  return useQuery({
    queryKey: adminKeys.rooms(branchId),
    queryFn: () => get<RoomRow[]>('/admin/scheduling/rooms', { branchId }),
  })
}

export function useSchedules(params: { branchId?: number; trainerId?: number; includeInactive?: boolean }): UseQueryResult<ScheduleRow[]> {
  return useQuery({
    queryKey: adminKeys.schedules(params),
    queryFn: () => get<ScheduleRow[]>('/admin/scheduling/schedules', params),
    placeholderData: (previous) => previous,
  })
}

export function useSessions(params: {
  branchId?: number
  from?: string
  days?: number
  trainerId?: number
  formatId?: number
}): UseQueryResult<AdminSessionRow[]> {
  return useQuery({
    queryKey: adminKeys.sessions(params),
    queryFn: () => get<AdminSessionRow[]>('/admin/scheduling/sessions', params),
    placeholderData: (previous) => previous,
  })
}

export function useRoster(sessionId: number | null): UseQueryResult<{ session: AdminSessionRow; roster: RosterEntry[] }> {
  return useQuery({
    queryKey: adminKeys.roster(sessionId ?? 0),
    queryFn: () => get<{ session: AdminSessionRow; roster: RosterEntry[] }>(`/admin/scheduling/sessions/${sessionId}/roster`),
    enabled: sessionId !== null,
  })
}

export function useSchedulingActions() {
  const client = useQueryClient()
  const invalidate = () => {
    void client.invalidateQueries({ queryKey: ['admin', 'scheduling'] })
    void client.invalidateQueries({ queryKey: ['public'] })
  }

  return {
    checkConflicts: useMutation({
      mutationFn: async (body: Record<string, unknown>) =>
        (await api.post<ConflictRow[]>('/admin/scheduling/schedules/check-conflicts', body)).data,
    }),
    createSchedule: useMutation({
      mutationFn: async (body: Record<string, unknown>) => (await api.post('/admin/scheduling/schedules', body)).data,
      onSuccess: invalidate,
    }),
    updateSchedule: useMutation({
      mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
        (await api.put(`/admin/scheduling/schedules/${input.id}`, input.body)).data,
      onSuccess: invalidate,
    }),
    removeSchedule: useMutation({
      mutationFn: async (id: number) => (await api.delete(`/admin/scheduling/schedules/${id}`)).data,
      onSuccess: invalidate,
    }),
    materialise: useMutation({
      mutationFn: async (input: { id: number; weeks: number }) =>
        (await api.post(`/admin/scheduling/schedules/${input.id}/materialise?weeks=${input.weeks}`)).data,
      onSuccess: invalidate,
    }),
    createFormat: useMutation({
      mutationFn: async (body: Record<string, unknown>) => (await api.post('/admin/scheduling/formats', body)).data,
      onSuccess: invalidate,
    }),
    updateFormat: useMutation({
      mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
        (await api.put(`/admin/scheduling/formats/${input.id}`, input.body)).data,
      onSuccess: invalidate,
    }),
    createRoom: useMutation({
      mutationFn: async (body: Record<string, unknown>) => (await api.post('/admin/scheduling/rooms', body)).data,
      onSuccess: invalidate,
    }),
    updateRoom: useMutation({
      mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
        (await api.put(`/admin/scheduling/rooms/${input.id}`, input.body)).data,
      onSuccess: invalidate,
    }),
    cancelSession: useMutation({
      mutationFn: async (input: { id: number; reason: string; notifyMembers: boolean }) =>
        (await api.post(`/admin/scheduling/sessions/${input.id}/cancel`, input)).data,
      onSuccess: invalidate,
    }),
    substitute: useMutation({
      mutationFn: async (input: { id: number; trainerId?: number | null }) =>
        (await api.post(
          `/admin/scheduling/sessions/${input.id}/substitute${input.trainerId ? `?trainerId=${input.trainerId}` : ''}`,
        )).data,
      onSuccess: invalidate,
    }),
    book: useMutation({
      mutationFn: async (body: { sessionId: number; memberId: number; allowWaitlist: boolean }) =>
        (await api.post('/admin/scheduling/bookings', body)).data,
      onSuccess: invalidate,
    }),
    cancelBooking: useMutation({
      mutationFn: async (input: { id: number; reason?: string }) =>
        (await api.delete(`/admin/scheduling/bookings/${input.id}`, { params: { reason: input.reason } })).data,
      onSuccess: invalidate,
    }),
    mark: useMutation({
      mutationFn: async (input: { id: number; status: number }) =>
        (await api.post(`/admin/scheduling/bookings/${input.id}/mark?status=${input.status}`)).data,
      onSuccess: invalidate,
    }),
  }
}

/* ---------------------------------------------------------------- attendance */

export function useAttendanceToday(params: { branchId?: number; date?: string }): UseQueryResult<AttendanceToday> {
  return useQuery({
    queryKey: adminKeys.attendanceToday(params),
    queryFn: () => get<AttendanceToday>('/admin/attendance/today', params),
    refetchInterval: 45_000,
  })
}

export function useHeatmap(params: { branchId?: number; days?: number }): UseQueryResult<HeatmapResponse> {
  return useQuery({
    queryKey: adminKeys.heatmap(params),
    queryFn: () => get<HeatmapResponse>('/admin/attendance/heatmap', params),
  })
}

export function useAbsentees(params: { branchId?: number; days?: number }): UseQueryResult<AbsenteeRow[]> {
  return useQuery({
    queryKey: adminKeys.absentees(params),
    queryFn: () => get<AbsenteeRow[]>('/admin/attendance/absentees', params),
  })
}

export function useAttendanceActions() {
  const client = useQueryClient()
  const invalidate = () => {
    void client.invalidateQueries({ queryKey: ['admin', 'attendance'] })
    void client.invalidateQueries({ queryKey: ['admin', 'dashboard'] })
    void client.invalidateQueries({ queryKey: ['branches', 'occupancy'] })
  }

  return {
    checkIn: useMutation({
      mutationFn: async (body: Record<string, unknown>) =>
        (await api.post<CheckInResult>('/admin/attendance/checkin', body)).data,
      onSuccess: invalidate,
    }),
    checkOut: useMutation({
      mutationFn: async (id: number) => (await api.post(`/admin/attendance/checkout/${id}`)).data,
      onSuccess: invalidate,
    }),
    checkOutAll: useMutation({
      mutationFn: async (branchId: number) =>
        (await api.post(`/admin/attendance/checkout-all?branchId=${branchId}`)).data,
      onSuccess: invalidate,
    }),
    winBack: useMutation({
      mutationFn: async (body: { memberIds: number[]; message?: string }) =>
        (await api.post<{ members: number; notifications: number }>('/admin/attendance/winback', body)).data,
      onSuccess: invalidate,
    }),
    lookup: useMutation({
      mutationFn: async (q: string) =>
        (await api.get<{ id: number; memberCode: string; fullName: string; phone: string; photoUrl?: string | null; branchName: string; status: string }[]>(
          '/admin/attendance/lookup',
          { params: { q } },
        )).data,
    }),
  }
}

/* ---------------------------------------------------------------- leads */

export function useLeadBoard(params: { branchId?: number; assignedTo?: string; source?: number }): UseQueryResult<LeadBoard> {
  return useQuery({
    queryKey: adminKeys.leadBoard(params),
    queryFn: () => get<LeadBoard>('/admin/leads/board', params),
    placeholderData: (previous) => previous,
    refetchInterval: 90_000,
  })
}

export function useLead(id: number | null): UseQueryResult<LeadDetail> {
  return useQuery({
    queryKey: adminKeys.lead(id ?? 0),
    queryFn: () => get<LeadDetail>(`/admin/leads/${id}`),
    enabled: id !== null,
  })
}

export function useLeadActions() {
  const client = useQueryClient()
  const invalidateAll = useInvalidateAll()
  const invalidate = (id?: number) => {
    void client.invalidateQueries({ queryKey: ['admin', 'leads'] })
    if (id) void client.invalidateQueries({ queryKey: adminKeys.lead(id) })
    void client.invalidateQueries({ queryKey: ['admin', 'dashboard'] })
  }

  return {
    create: useMutation({
      mutationFn: async (body: Record<string, unknown>) => (await api.post('/admin/leads', body)).data,
      onSuccess: () => invalidate(),
    }),
    update: useMutation({
      mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
        (await api.put(`/admin/leads/${input.id}`, input.body)).data,
      onSuccess: (_d, input) => invalidate(input.id),
    }),
    move: useMutation({
      mutationFn: async (input: { id: number; stage: number; lostReason?: string; note?: string }) =>
        (await api.post(`/admin/leads/${input.id}/stage`, input)).data,
      onSuccess: (_d, input) => invalidate(input.id),
    }),
    addFollowUp: useMutation({
      mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
        (await api.post(`/admin/leads/${input.id}/followups`, input.body)).data,
      onSuccess: (_d, input) => invalidate(input.id),
    }),
    completeFollowUp: useMutation({
      mutationFn: async (input: { followUpId: number; leadId: number; body: Record<string, unknown> }) =>
        (await api.post(`/admin/leads/followups/${input.followUpId}/complete`, input.body)).data,
      onSuccess: (_d, input) => invalidate(input.leadId),
    }),
    convert: useMutation({
      mutationFn: async (input: { id: number; body: Record<string, unknown> }) =>
        (await api.post<{ memberId: number; memberCode: string; reusedExistingMember: boolean; sale?: { invoiceId: number; invoiceNumber: string; grandTotal: number } | null }>(
          `/admin/leads/${input.id}/convert`,
          input.body,
        )).data,
      onSuccess: invalidateAll,
    }),
  }
}

/* ---------------------------------------------------------------- shared */

export interface TrainerOption {
  id: number
  fullName: string
  slug: string
  primaryBranchId: number
  branchName: string
  portraitUrl?: string | null
  showOnWebsite: boolean
}

/**
 * The coach picker on the timetable builder and the substitution dialog. Reads the admin
 * list, not the public one, so a coach hidden from the website can still be rostered.
 */
export function useTrainerOptions(branchId?: number): UseQueryResult<TrainerOption[]> {
  return useQuery({
    queryKey: [...adminKeys.trainers, branchId ?? null],
    queryFn: () => get<TrainerOption[]>('/admin/scheduling/trainers', { branchId }),
    staleTime: 10 * 60 * 1000,
  })
}

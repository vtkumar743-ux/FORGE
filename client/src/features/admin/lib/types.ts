/* ============================================================================
   Admin response contracts.

   These mirror the records in Gym.Api/Contracts/Admin*.cs one for one. The public
   site validates CMS *content* with Zod because an owner edit can produce anything;
   these are server-shaped read models on an authenticated, admin-only surface, so
   they are typed rather than re-parsed. The one place Zod still runs in the admin
   panel is the section editor, which validates against the exact shapes the public
   renderer uses (`features/public/sections/schemas.ts`).
   ============================================================================ */

export interface Paged<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  pageCount: number
}

/* ---------------------------------------------------------------- cms */

export interface AdminPageListItem {
  id: number
  slug: string
  title: string
  state: number
  isSystemPage: boolean
  displayOrder: number
  sectionCount: number
  hiddenSectionCount: number
  draftSectionCount: number
  publishedAtUtc?: string | null
  updatedAtUtc?: string | null
}

export interface AdminSection {
  id: number
  key: string
  type: number
  typeName: string
  adminLabel: string
  orderIndex: number
  isVisible: boolean
  branchId?: number | null
  branchName?: string | null
  content: Record<string, unknown>
  draft?: Record<string, unknown> | null
  hasDraft: boolean
  state: number
  publishedAtUtc?: string | null
  publishedBy?: string | null
  updatedAtUtc?: string | null
}

export interface AdminPage {
  id: number
  slug: string
  title: string
  description?: string | null
  seo: {
    title: string
    description: string
    keywords?: string | null
    ogImageUrl?: string | null
    canonicalUrl?: string | null
    noIndex: boolean
    structuredData?: unknown
  }
  state: number
  isSystemPage: boolean
  displayOrder: number
  sections: AdminSection[]
}

export interface AdminSetting {
  id: number
  key: string
  value: string
  group: string
  label: string
  helpText?: string | null
  valueType: string
  isPublic: boolean
  displayOrder: number
}

export interface MediaAsset {
  id: number
  fileName: string
  url: string
  kind: number
  contentType: string
  sizeBytes: number
  width?: number | null
  height?: number | null
  variants: Record<string, string>
  blurDataUrl?: string | null
  altText: string
  caption?: string | null
  credit?: string | null
  folder?: string | null
  tags: string[]
  uploadedBy?: string | null
  createdAtUtc: string
}

export interface Testimonial {
  id: number
  authorName: string
  authorRole?: string | null
  authorPhotoUrl?: string | null
  quote: string
  rating: number
  branchId?: number | null
  branchName?: string | null
  program?: string | null
  googleReviewUrl?: string | null
  isFeatured: boolean
  isVisible: boolean
  displayOrder: number
}

export interface Transformation {
  id: number
  memberDisplayName: string
  memberId?: number | null
  beforeImageUrl: string
  afterImageUrl: string
  durationWeeks: number
  program: string
  trainerName?: string | null
  weightBeforeKg?: number | null
  weightAfterKg?: number | null
  story?: string | null
  branchId?: number | null
  branchName?: string | null
  consentGiven: boolean
  consentAtUtc?: string | null
  isVisible: boolean
  displayOrder: number
}

export interface FaqItem {
  id: number
  question: string
  answer: string
  category: string
  branchId?: number | null
  branchName?: string | null
  isVisible: boolean
  displayOrder: number
}

export interface BlogPostRow {
  id: number
  slug: string
  title: string
  excerpt: string
  coverImageUrl?: string | null
  authorName: string
  authorRole?: string | null
  tags?: string | null
  readMinutes: number
  state: number
  publishedAtUtc?: string | null
  isFeatured: boolean
  viewCount: number
}

/* ---------------------------------------------------------------- dashboard */

export interface TimeSeriesPoint {
  label: string
  date: string
  value: number
  secondary?: number | null
}

export interface DashboardResponse {
  kpis: {
    activeMembers: number
    activeMembersLastMonth: number
    mrr: number
    mrrLastMonth: number
    checkInsToday: number
    checkInsYesterday: number
    duesOutstanding: number
    duesInvoiceCount: number
    expiringInSevenDays: number
    newLeadsThisWeek: number
    leadsAwaitingFirstResponse: number
    onFloorNow: number
    revenueThisMonth: number
    revenueLastMonth: number
    classesThisWeek: number
    atRiskMembers: number
  }
  branches: {
    branchId: number
    name: string
    slug: string
    activeMembers: number
    mrr: number
    checkInsToday: number
    onFloorNow: number
    capacity: number
    duesOutstanding: number
    revenueThisMonth: number
    classFillPercent: number
  }[]
  revenue: TimeSeriesPoint[]
  footfall: TimeSeriesPoint[]
  joins: TimeSeriesPoint[]
  churnRisk: {
    memberId: number
    memberCode: string
    fullName: string
    branchName: string
    band: number
    score: number
    lastVisitOn?: string | null
    daysSinceVisit: number
    planName?: string | null
    endsOn?: string | null
    duesOutstanding: number
    phone: string
  }[]
  expiring: {
    subscriptionId: number
    memberId: number
    memberCode: string
    fullName: string
    phone: string
    branchName: string
    planName: string
    endsOn: string
    daysLeft: number
    priceCharged: number
    autoRenew: boolean
  }[]
  recentLeads: LeadCard[]
  planMix: { planName: string; subscriptions: number; mrr: number }[]
  generatedAtIst: string
}

/* ---------------------------------------------------------------- members */

export interface MemberListRow {
  id: number
  memberCode: string
  fullName: string
  phone: string
  email?: string | null
  photoUrl?: string | null
  branchId: number
  branchName: string
  status: number
  joinedOn: string
  planName?: string | null
  membershipEndsOn?: string | null
  daysLeft?: number | null
  duesOutstanding: number
  lastVisitOn?: string | null
  currentStreakDays: number
  churnRisk: number
  tags: string[]
  dateOfBirth?: string | null
}

export interface MemberDetail {
  summary: MemberListRow
  profile: {
    gender: number
    dateOfBirth?: string | null
    addressLine?: string | null
    city?: string | null
    pincode?: string | null
    primaryGoal?: string | null
    medicalNotes?: string | null
    injuryNotes?: string | null
    heightCm?: number | null
    startWeightKg?: number | null
    emergencyContactName?: string | null
    emergencyContactPhone?: string | null
    waiverSigned: boolean
    waiverDocumentUrl?: string | null
    waiverSignedAtUtc?: string | null
    qrToken: string
    referralCode?: string | null
    corporateCode?: string | null
    consentMarketing: boolean
    consentLeaderboard: boolean
    consentTransformationShowcase: boolean
  }
  subscriptions: SubscriptionRow[]
  invoices: InvoiceRow[]
  upcomingBookings: {
    id: number
    sessionId: number
    formatName: string
    branchName: string
    trainerName: string
    date: string
    startTime: string
    status: number
    waitlistPosition?: number | null
  }[]
  timeline: { atUtc: string; kind: string; title: string; detail?: string | null; amount?: string | null }[]
  stats: {
    totalVisits: number
    visitsLast30Days: number
    classesAttended: number
    noShows: number
    longestStreakDays: number
    lifetimeValue: number
    churnScore: number
  }
}

/* ---------------------------------------------------------------- billing */

export interface PlanRow {
  id: number
  name: string
  slug: string
  tagline: string
  description?: string | null
  kind: number
  cycle: number
  accessScope: number
  durationDays: number
  basePrice: number
  admissionFee: number
  gstRatePercent: number
  sacCode: string
  classCredits?: number | null
  ptSessionCredits?: number | null
  guestPasses?: number | null
  freezeDaysAllowed: number
  freezeFee: number
  accessWindowStart?: string | null
  accessWindowEnd?: string | null
  features: string[]
  trustMicrocopy?: string | null
  isMostPopular: boolean
  showOnWebsite: boolean
  isActive: boolean
  displayOrder: number
  activeSubscriptions: number
  branchPrices: {
    branchId: number
    branchName: string
    price: number
    admissionFee?: number | null
    isAvailable: boolean
  }[]
}

export interface CouponRow {
  id: number
  code: string
  name: string
  description?: string | null
  discountType: number
  discountValue: number
  maxDiscountAmount?: number | null
  minOrderAmount: number
  validFrom: string
  validTo: string
  usageCap?: number | null
  usageCount: number
  perMemberCap?: number | null
  branchScope?: string | null
  planScope?: string | null
  isActive: boolean
  showAsWebsiteBanner: boolean
  bannerHeadline?: string | null
  isLive: boolean
}

export interface SubscriptionRow {
  id: number
  memberId: number
  memberName: string
  memberCode: string
  planId: number
  planName: string
  branchName: string
  status: number
  startsOn: string
  endsOn: string
  daysLeft: number
  priceCharged: number
  discountAmount: number
  classCreditsRemaining: number
  ptCreditsRemaining: number
  freezeStartsOn?: string | null
  freezeEndsOn?: string | null
  freezeDaysUsed: number
  freezeDaysAllowed: number
  autoRenew: boolean
  cancellationReason?: string | null
}

export interface InvoiceRow {
  id: number
  invoiceNumber: string
  memberId: number
  memberName: string
  memberCode: string
  branchName: string
  issuedOn: string
  dueOn: string
  status: number
  grandTotal: number
  amountPaid: number
  amountDue: number
  remindersSent: number
  daysOverdue: number
  planName?: string | null
}

export interface InvoiceDetail {
  header: InvoiceRow
  lines: {
    id: number
    description: string
    sacOrHsnCode?: string | null
    quantity: number
    unitPrice: number
    discountAmount: number
    taxableValue: number
    gstRatePercent: number
    cgstAmount: number
    sgstAmount: number
    igstAmount: number
    lineTotal: number
  }[]
  payments: {
    id: number
    amount: number
    mode: number
    status: number
    paidAtUtc: string
    gatewayName?: string | null
    gatewayPaymentId?: string | null
    chequeNumber?: string | null
    bankReference?: string | null
    receivedBy?: string | null
    notes?: string | null
  }[]
  subTotal: number
  discountTotal: number
  taxableValue: number
  cgstAmount: number
  sgstAmount: number
  igstAmount: number
  roundOff: number
  supplierGstin?: string | null
  placeOfSupply?: string | null
  customerGstin?: string | null
  notes?: string | null
  branchAddress: string
  memberPhone: string
  memberEmail?: string | null
}

export interface Quote {
  listPrice: number
  admissionFee: number
  discountAmount: number
  prorationCredit: number
  payable: number
  couponId?: number | null
  couponCode?: string | null
  couponMessage?: string | null
  startsOn: string
  endsOn: string
  tax: { taxableValue: number; cgst: number; sgst: number; igst: number; rate: number }
}

export interface CollectionsResponse {
  totalOutstanding: number
  invoiceCount: number
  ageing: { bucket: string; amount: number; count: number }[]
  invoices: {
    id: number
    invoiceNumber: string
    memberId: number
    memberName: string
    memberCode: string
    phone: string
    branchName: string
    dueOn: string
    amountDue: number
    grandTotal: number
    remindersSent: number
    lastReminderAtUtc?: string | null
    status: number
    daysOverdue: number
    bucket: string
  }[]
}

/* ---------------------------------------------------------------- scheduling */

export interface ClassFormatRow {
  id: number
  name: string
  slug: string
  shortDescription: string
  description?: string | null
  defaultDurationMinutes: number
  defaultCapacity: number
  level: number
  intensity: number
  estimatedCalories: number
  coverImageUrl?: string | null
  iconKey?: string | null
  tags?: string | null
  showOnWebsite: boolean
  isActive: boolean
  displayOrder: number
  weeklySlots: number
}

export interface RoomRow {
  id: number
  branchId: number
  branchName: string
  name: string
  capacity: number
  notes?: string | null
  isActive: boolean
}

export interface ScheduleRow {
  id: number
  branchId: number
  branchName: string
  classFormatId: number
  formatName: string
  iconKey?: string | null
  roomId?: number | null
  roomName?: string | null
  trainerId: number
  trainerName: string
  dayOfWeek: number
  startTime: string
  endTime: string
  durationMinutes: number
  capacity: number
  effectiveFrom: string
  effectiveTo?: string | null
  bookingOpensHoursBefore: number
  cancelCutoffHoursBefore: number
  waitlistEnabled: boolean
  waitlistCapacity: number
  isActive: boolean
  upcomingSessions: number
  averageFillPercent: number
}

export interface AdminSessionRow {
  id: number
  date: string
  startTime: string
  endTime: string
  formatName: string
  branchName: string
  branchId: number
  trainerName: string
  isSubstitute: boolean
  roomName?: string | null
  capacity: number
  bookedCount: number
  waitlistCount: number
  attendedCount: number
  status: number
  cancellationReason?: string | null
  fillPercent: number
}

export interface RosterEntry {
  bookingId: number
  memberId: number
  memberCode: string
  fullName: string
  photoUrl?: string | null
  phone: string
  status: number
  waitlistPosition?: number | null
  checkedInAtUtc?: string | null
  bookedAtUtc: string
  wasPromoted: boolean
  noShowsLast90Days: number
}

export interface ConflictRow {
  kind: string
  message: string
  conflictingScheduleId: number
  conflictingLabel: string
}

/* ---------------------------------------------------------------- attendance */

export interface CheckInResult {
  admitted: boolean
  headline: string
  message: string
  checkInId?: number | null
  memberId?: number | null
  memberCode?: string | null
  fullName?: string | null
  photoUrl?: string | null
  planName?: string | null
  membershipEndsOn?: string | null
  daysLeft?: number | null
  duesOutstanding: number
  currentStreakDays: number
  warnings: string[]
  todaysClasses: AdminSessionRow[]
  blockReason?: string | null
}

export interface AttendanceRow {
  id: number
  memberId: number
  memberCode: string
  fullName: string
  photoUrl?: string | null
  branchName: string
  checkInAtUtc: string
  checkOutAtUtc?: string | null
  durationMinutes?: number | null
  source: number
  wasBlocked: boolean
  blockReason?: string | null
  className?: string | null
}

export interface AttendanceToday {
  date: string
  total: number
  onFloor: number
  refused: number
  rows: AttendanceRow[]
}

export interface HeatmapResponse {
  cells: { dayOfWeek: number; hour: number; count: number }[]
  peakCount: number
  peakLabel: string
  totalVisits: number
  daysCovered: number
  daily: TimeSeriesPoint[]
}

export interface AbsenteeRow {
  memberId: number
  memberCode: string
  fullName: string
  phone: string
  branchName: string
  lastVisitOn?: string | null
  daysSinceVisit: number
  planName?: string | null
  membershipEndsOn?: string | null
  winBackSent: boolean
  winBackSentAtUtc?: string | null
}

/* ---------------------------------------------------------------- leads */

export interface LeadCard {
  id: number
  reference: string
  fullName: string
  phone: string
  email?: string | null
  branchId?: number | null
  branchName?: string | null
  stage: number
  source: number
  sourceDetail?: string | null
  goal?: string | null
  interestedPlanName?: string | null
  trialRequestedFor?: string | null
  assignedTo?: string | null
  createdAtUtc: string
  firstResponseAtUtc?: string | null
  nextFollowUpAtUtc?: string | null
  isOverdue: boolean
  ageDays: number
  openFollowUps: number
  convertedMemberId?: number | null
  lostReason?: string | null
}

export interface LeadStats {
  newThisWeek: number
  awaitingFirstResponse: number
  overdueFollowUps: number
  joinedThisMonth: number
  conversionRate: number
  medianFirstResponseMinutes: number
  bySource: { source: number; name: string; total: number; joined: number; conversionRate: number }[]
}

export interface LeadBoard {
  columns: { stage: number; name: string; total: number; cards: LeadCard[] }[]
  stats: LeadStats
}

export interface LeadDetail {
  card: LeadCard
  message?: string | null
  preferredTime?: string | null
  utmSource?: string | null
  utmCampaign?: string | null
  sequenceActive: boolean
  sequenceStep: number
  followUps: {
    id: number
    channel: number
    dueAtUtc: string
    completedAtUtc?: string | null
    outcome?: string | null
    notes?: string | null
    owner?: string | null
    isAutomated: boolean
    isOverdue: boolean
  }[]
}

/* ---------------------------------------------------------------- enums */

export const memberStatusNames = ['Lead', 'Trial', 'Active', 'Frozen', 'Expired', 'Cancelled'] as const
export const subscriptionStatusNames = ['Pending', 'Active', 'Frozen', 'Expired', 'Cancelled'] as const
export const invoiceStatusNames = [
  'Draft', 'Issued', 'PartiallyPaid', 'Paid', 'Overdue', 'Cancelled', 'Refunded',
] as const
export const paymentModeNames = [
  'Cash', 'Upi', 'Card', 'NetBanking', 'Cheque', 'RazorpayLink', 'BankTransfer', 'Credit',
] as const
export const paymentStatusNames = ['Pending', 'Captured', 'Failed', 'Refunded'] as const
export const bookingStatusNames = ['Booked', 'Waitlisted', 'Attended', 'NoShow', 'Cancelled'] as const
export const sessionStatusNames = ['Scheduled', 'InProgress', 'Completed', 'Cancelled'] as const
export const leadStageNames = ['Inquiry', 'Tour', 'Trial', 'Negotiation', 'Joined', 'Lost'] as const
export const leadSourceNames = [
  'Website', 'WalkIn', 'Referral', 'Ads', 'Phone', 'Instagram', 'Corporate',
] as const
export const followUpChannelNames = ['Call', 'WhatsApp', 'Sms', 'Email', 'InPerson'] as const
export const checkInSourceNames = ['Qr', 'Manual', 'Biometric', 'Kiosk'] as const
export const planKindNames = ['Recurring', 'FixedTerm', 'ClassPack', 'PtPack', 'DayPass', 'Trial'] as const
export const billingCycleNames = ['None', 'Monthly', 'Quarterly', 'HalfYearly', 'Annual'] as const
export const classLevelNames = ['AllLevels', 'Beginner', 'Intermediate', 'Advanced'] as const
export const classIntensityNames = ['Low', 'Moderate', 'High'] as const
export const genderNames = ['Unspecified', 'Male', 'Female', 'Other'] as const
export const churnBandNames = ['Healthy', 'Watch', 'Amber', 'Red'] as const
export const weekdayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const

/** Enum ordinals come off the wire as numbers; this is the one place they get a name. */
export function enumName(names: readonly string[], value: number | null | undefined): string {
  return typeof value === 'number' ? names[value] ?? String(value) : '—'
}

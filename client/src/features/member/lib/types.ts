/* ============================================================================
   Member portal read models — a one-for-one mirror of Contracts/PortalContracts.cs.

   Types rather than Zod schemas, for the same reason the admin panel uses types
   (PROGRESS deviation 23): Zod earns its place where an arbitrary human edit can
   produce anything — CMS section content. These are server-shaped reads on an
   authenticated surface, and re-parsing them would double the contract with no
   safety gained.
   ============================================================================ */

export type MemberStatusName = 'Lead' | 'Trial' | 'Active' | 'Frozen' | 'Expired' | 'Cancelled'
export type BookingStatusCode = 0 | 1 | 2 | 3 | 4 // Booked · Waitlisted · Attended · NoShow · Cancelled

export interface PortalMember {
  id: number
  memberCode: string
  fullName: string
  firstName: string | null
  photoUrl: string | null
  email: string | null
  phone: string
  homeBranchId: number
  homeBranchName: string
  homeBranchSlug: string
  joinedOn: string
  primaryGoal: string | null
  status: number
  statusName: MemberStatusName
  heightCm: number | null
  startWeightKg: number | null
  dateOfBirth: string | null
  consentMarketing: boolean
  consentLeaderboard: boolean
  waiverSigned: boolean
}

export interface PortalCard {
  memberCode: string
  fullName: string
  photoUrl: string | null
  qrToken: string
  homeBranchName: string
  planName: string | null
  validUntil: string | null
  daysLeft: number | null
  statusName: string
  isUsable: boolean
  blockReason: string | null
}

export interface CalendarDay {
  date: string
  visited: boolean
  classCount: number
  isToday: boolean
}

export interface PortalStreak {
  currentStreakDays: number
  longestStreakDays: number
  lastVisitOn: string | null
  visitsThisWeek: number
  visitsThisMonth: number
  calendar: CalendarDay[]
}

export interface BranchOccupancy {
  branchId: number
  branchName: string
  branchSlug: string
  currentCount: number
  capacity: number
  percentFull: number
  band: number
  asOfUtc: string
}

export interface PortalSession {
  id: number
  date: string
  startTime: string
  endTime: string
  startsAtUtc: string
  durationMinutes: number
  formatName: string
  formatSlug: string
  iconKey: string | null
  coverImageUrl: string
  levelName: string
  estimatedCalories: number
  branchId: number
  branchName: string
  branchSlug: string
  trainerName: string
  trainerSlug: string
  trainerPortraitUrl: string | null
  isSubstitute: boolean
  roomName: string | null
  capacity: number
  bookedCount: number
  spotsLeft: number
  waitlistCount: number
  status: number
  timeOfDay: string
  myBookingId: number | null
  myBookingStatus: BookingStatusCode | null
  myWaitlistPosition: number | null
  canBook: boolean
  canJoinWaitlist: boolean
  canCancel: boolean
  blockedReason: string | null
  bookingOpensAtUtc: string | null
  cancelCutoffAtUtc: string | null
  isLateCancelWindow: boolean
  myRating: number | null
}

export interface FilterOption {
  slug: string
  name: string
  count: number
}

export interface BranchOption {
  id: number
  name: string
  slug: string
  count: number
  isHome: boolean
}

export interface PortalTimetable {
  fromDate: string
  toDate: string
  sessions: PortalSession[]
  formats: FilterOption[]
  trainers: FilterOption[]
  branches: BranchOption[]
  bookingBlockedReason: string | null
  classCreditsRemaining: number | null
}

export interface BookingResult {
  bookingId: number
  status: BookingStatusCode
  statusName: string
  waitlistPosition: number | null
  bookedCount: number
  capacity: number
  spotsLeft: number
  waitlistCount: number
  headline: string
  message: string
  classCreditsRemaining: number | null
}

export interface PortalBooking {
  id: number
  sessionId: number
  status: BookingStatusCode
  statusName: string
  waitlistPosition: number | null
  date: string
  startTime: string
  startsAtUtc: string
  durationMinutes: number
  formatName: string
  formatSlug: string
  coverImageUrl: string
  trainerName: string
  trainerSlug: string
  trainerPortraitUrl: string | null
  branchName: string
  roomName: string | null
  canCancel: boolean
  isLateCancelWindow: boolean
  canRate: boolean
  ratingScore: number | null
  ratingComment: string | null
  checkedInAtUtc: string | null
}

export interface RatingPrompt {
  bookingId: number
  sessionId: number
  formatName: string
  trainerId: number
  trainerName: string
  trainerPortraitUrl: string | null
  date: string
  startTime: string
}

export interface FreezeRequestRow {
  id: number
  subscriptionId: number
  planName: string
  requestedFrom: string
  requestedTo: string
  days: number
  reason: string
  status: number
  statusName: 'Pending' | 'Approved' | 'Declined' | 'Withdrawn'
  requestedAtUtc: string
  decidedAtUtc: string | null
  decisionNote: string | null
  memberName: string | null
  memberCode: string | null
}

export interface PortalMembership {
  subscriptionId: number
  planName: string
  planSlug: string
  planTagline: string
  kind: number
  cycleName: string
  status: number
  statusName: string
  branchName: string
  accessScopeName: string
  startsOn: string
  endsOn: string
  daysLeft: number
  totalDays: number
  classCreditsRemaining: number
  ptCreditsRemaining: number
  priceCharged: number
  autoRenew: boolean
  nextBillingOn: string | null
  freezeDaysAllowed: number
  freezeDaysUsed: number
  freezeFee: number
  freezeStartsOn: string | null
  freezeEndsOn: string | null
  accessWindow: string | null
  pendingFreezeRequest: FreezeRequestRow | null
  features: string[]
}

export interface InvoiceRow {
  id: number
  invoiceNumber: string
  issuedOn: string
  dueOn: string
  status: number
  statusName: string
  grandTotal: number
  amountPaid: number
  amountDue: number
  description: string | null
}

export interface InvoiceLine {
  description: string
  sacOrHsnCode: string | null
  quantity: number
  unitPrice: number
  discountAmount: number
  taxableValue: number
  gstRatePercent: number
  lineTotal: number
}

export interface PaymentRow {
  id: number
  amount: number
  modeName: string
  statusName: string
  paidAtUtc: string
  reference: string | null
}

export interface InvoiceDetail extends InvoiceRow {
  branchName: string
  supplierGstin: string | null
  placeOfSupply: string | null
  subTotal: number
  discountTotal: number
  taxableValue: number
  cgstAmount: number
  sgstAmount: number
  igstAmount: number
  roundOff: number
  lines: InvoiceLine[]
  payments: PaymentRow[]
}

export interface MembershipHistoryRow {
  id: number
  planName: string
  startsOn: string
  endsOn: string
  statusName: string
  priceCharged: number
  branchName: string
}

export interface GatewayInfo {
  provider: string
  isLive: boolean
  keyId: string | null
  notice: string | null
}

export interface MembershipPage {
  current: PortalMembership | null
  history: MembershipHistoryRow[]
  invoices: InvoiceRow[]
  duesOutstanding: number
  freezeRequests: FreezeRequestRow[]
  gateway: GatewayInfo
}

export interface PlanOption {
  id: number
  name: string
  slug: string
  tagline: string
  kind: number
  cycleName: string
  durationDays: number
  price: number
  listPrice: number
  admissionFee: number
  accessScopeName: string
  isMostPopular: boolean
  isCurrentPlan: boolean
  classCredits: number | null
  ptSessionCredits: number | null
  freezeDaysAllowed: number
  trustMicrocopy: string | null
  accessWindow: string | null
  features: string[]
}

export interface Quote {
  planId: number
  planName: string
  branchId: number
  branchName: string
  listPrice: number
  admissionFee: number
  discountAmount: number
  prorationCredit: number
  payable: number
  gstRatePercent: number
  couponCode: string | null
  couponMessage: string | null
  startsOn: string
  endsOn: string
  isRenewalOfCurrent: boolean
}

export interface GatewayOrder {
  orderId: string
  keyId: string | null
  amountInr: number
  currency: string
  isSimulated: boolean
  prefillName: string
  prefillEmail: string | null
  prefillContact: string
}

export interface CheckoutResult {
  invoiceId: number
  invoiceNumber: string
  amountDue: number
  subscriptionId: number
  startsOn: string
  endsOn: string
  order: GatewayOrder | null
  headline: string
  message: string
}

export interface ProgramSummary {
  id: number
  name: string
  goal: string | null
  weekNumber: number
  durationWeeks: number
  daysPerWeek: number
  trainerName: string | null
  nextDayId: number | null
  nextDayTitle: string | null
  sessionsLogged: number
}

export interface SetRow {
  id: number
  setNumber: number
  reps: number
  weightKg: number
  rpe: number | null
  volume: number
  estimatedOneRepMax: number
  isPersonalRecord: boolean
  performedOn: string
  notes: string | null
}

export interface ProgramExercise {
  id: number
  exerciseId: number
  name: string
  slug: string
  primaryMuscle: string
  equipment: string
  videoUrl: string | null
  thumbnailUrl: string | null
  cues: string | null
  isStrengthTracked: boolean
  orderIndex: number
  sets: number
  repScheme: string
  restSeconds: number
  targetWeightKg: number | null
  tempo: string | null
  supersetGroup: string | null
  notes: string | null
  lastSession: SetRow[]
  lastSessionOn: string | null
  bestE1Rm: number | null
  todaySets: SetRow[]
}

export interface ProgramDay {
  id: number
  dayIndex: number
  title: string
  focus: string | null
  isRestDay: boolean
  notes: string | null
  exerciseCount: number
  totalSets: number
  estimatedMinutes: number
  lastPerformedOn: string | null
  exercises: ProgramExercise[]
}

export interface Program {
  id: number
  name: string
  description: string | null
  goal: string | null
  statusName: string
  authorName: string
  durationWeeks: number
  daysPerWeek: number
  weekNumber: number
  startsOn: string | null
  endsOn: string | null
  trainerName: string | null
  trainerSlug: string | null
  trainerPortraitUrl: string | null
  days: ProgramDay[]
}

export interface PrCelebration {
  logId: number
  exerciseName: string
  weightKg: number
  reps: number
  estimatedOneRepMax: number
  previousBestE1Rm: number | null
  headline: string
  message: string
  shareText: string
  performedOn: string
}

export interface BadgeRow {
  id: number
  name: string
  slug: string
  description: string
  iconKey: string
  tier: string
  awardedAtUtc: string
  isSeen: boolean
}

export interface LogSetResult {
  set: SetRow
  isPersonalRecord: boolean
  previousBestE1Rm: number | null
  celebration: PrCelebration | null
  badgesAwarded: BadgeRow[]
}

export interface WorkoutHistoryRow {
  date: string
  sets: number
  volume: number
  personalRecords: number
  exercises: string[]
}

export interface ExerciseOption {
  id: number
  name: string
  slug: string
  primaryMuscle: string
  equipment: string
  isStrengthTracked: boolean
  cues: string | null
  videoUrl: string | null
  thumbnailUrl: string | null
}

export interface BodyScan {
  id: number
  scanDate: string
  weightKg: number
  bodyFatPercent: number | null
  skeletalMuscleMassKg: number | null
  fatMassKg: number | null
  visceralFatLevel: number | null
  bmi: number | null
  basalMetabolicRate: number | null
  totalBodyWaterL: number | null
  inBodyScore: number | null
  chestCm: number | null
  waistCm: number | null
  hipCm: number | null
  thighCm: number | null
  armCm: number | null
  measuredBy: string | null
  deviceName: string | null
  notes: string | null
  isSelfReported: boolean
}

export interface ProgressPhoto {
  id: number
  takenOn: string
  imageUrl: string
  pose: string
  weightKg: number | null
  notes: string | null
}

export interface StrengthPoint {
  date: string
  estimatedOneRepMax: number
  topSetWeightKg: number
  topSetReps: number
}

export interface StrengthSeries {
  exerciseId: number
  exerciseName: string
  slug: string
  bestE1Rm: number
  latestE1Rm: number
  points: StrengthPoint[]
}

export interface VolumePoint {
  weekStarting: string
  label: string
  volumeKg: number
  sets: number
}

export interface ProgressHeadline {
  currentWeightKg: number | null
  weightChangeKg: number | null
  currentBodyFatPercent: number | null
  bodyFatChange: number | null
  muscleMassChangeKg: number | null
  scanCount: number
  firstScanOn: string | null
  latestScanOn: string | null
  totalPersonalRecords: number
  totalVolumeLiftedKg: number
}

export interface ProgressPage {
  scans: BodyScan[]
  photos: ProgressPhoto[]
  strength: StrengthSeries[]
  weeklyVolume: VolumePoint[]
  streak: PortalStreak
  badges: BadgeRow[]
  headline: ProgressHeadline
}

export interface ReferralRow {
  id: number
  inviteeName: string | null
  inviteePhone: string | null
  status: number
  statusName: string
  rewardAmount: number
  referrerRewarded: boolean
  invitedAtUtc: string
  convertedAtUtc: string | null
  expiresOn: string | null
}

export interface ReferralOverview {
  code: string
  shareUrl: string
  shareMessage: string
  rewardAmount: number
  invited: number
  joined: number
  rewarded: number
  creditEarned: number
  creditPending: number
  rows: ReferralRow[]
}

export interface NotificationRow {
  id: number
  kind: number
  kindName: string
  title: string
  body: string
  actionUrl: string | null
  isRead: boolean
  createdAtUtc: string
}

export interface NotificationsPage {
  rows: NotificationRow[]
  unread: number
  total: number
}

export interface PortalHome {
  member: PortalMember
  membership: PortalMembership | null
  streak: PortalStreak
  homeBranchOccupancy: BranchOccupancy | null
  todaysClasses: PortalSession[]
  nextClass: PortalSession | null
  duesOutstanding: number
  nextPayment: InvoiceRow | null
  unreadNotifications: number
  announcements: NotificationRow[]
  ratingPrompts: RatingPrompt[]
  pendingCelebration: PrCelebration | null
  newBadges: BadgeRow[]
  program: ProgramSummary | null
  referralCredits: number
}

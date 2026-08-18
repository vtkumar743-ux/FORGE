namespace Gym.Core.Enums;

public enum MemberStatus { Lead = 0, Trial = 1, Active = 2, Frozen = 3, Expired = 4, Cancelled = 5 }

public enum Gender { Unspecified = 0, Male = 1, Female = 2, Other = 3 }

public enum PlanKind { Recurring = 0, FixedTerm = 1, ClassPack = 2, PtPack = 3, DayPass = 4, Trial = 5 }

public enum BillingCycle { None = 0, Monthly = 1, Quarterly = 2, HalfYearly = 3, Annual = 4 }

public enum AccessScope { HomeBranch = 0, AllBranches = 1 }

public enum SubscriptionStatus { Pending = 0, Active = 1, Frozen = 2, Expired = 3, Cancelled = 4 }

public enum InvoiceStatus { Draft = 0, Issued = 1, PartiallyPaid = 2, Paid = 3, Overdue = 4, Cancelled = 5, Refunded = 6 }

public enum PaymentMode { Cash = 0, Upi = 1, Card = 2, NetBanking = 3, Cheque = 4, RazorpayLink = 5, BankTransfer = 6, Credit = 7 }

public enum PaymentStatus { Pending = 0, Captured = 1, Failed = 2, Refunded = 3 }

public enum DiscountType { Percentage = 0, FlatAmount = 1 }

public enum ClassLevel { AllLevels = 0, Beginner = 1, Intermediate = 2, Advanced = 3 }

public enum ClassIntensity { Low = 0, Moderate = 1, High = 2 }

public enum SessionStatus { Scheduled = 0, InProgress = 1, Completed = 2, Cancelled = 3 }

public enum BookingStatus { Booked = 0, Waitlisted = 1, Attended = 2, NoShow = 3, Cancelled = 4 }

public enum CheckInSource { Qr = 0, Manual = 1, Biometric = 2, Kiosk = 3 }

public enum LeadStage { Inquiry = 0, Tour = 1, Trial = 2, Negotiation = 3, Joined = 4, Lost = 5 }

public enum LeadSource { Website = 0, WalkIn = 1, Referral = 2, Ads = 3, Phone = 4, Instagram = 5, Corporate = 6 }

public enum FollowUpChannel { Call = 0, WhatsApp = 1, Sms = 2, Email = 3, InPerson = 4 }

public enum MuscleGroup { FullBody = 0, Chest = 1, Back = 2 , Shoulders = 3, Arms = 4, Legs = 5, Glutes = 6, Core = 7, Cardio = 8, Mobility = 9 }

public enum EquipmentKind { Bodyweight = 0, Barbell = 1, Dumbbell = 2, Machine = 3, Kettlebell = 4, Cable = 5, Bands = 6, Cardio = 7 }

public enum ProgramStatus { Draft = 0, PendingApproval = 1, Published = 2, Archived = 3 }

public enum PlanAuthor { Trainer = 0, Ai = 1, Admin = 2 }

public enum MealSlot { Breakfast = 0, MidMorning = 1, Lunch = 2, PreWorkout = 3, PostWorkout = 4, Snack = 5, Dinner = 6 }

public enum ProductCategory { Supplement = 0, Merchandise = 1, Beverage = 2, Accessory = 3, DayPass = 4, Service = 5 }

public enum StockTransferStatus { Requested = 0, InTransit = 1, Received = 2, Cancelled = 3 }

public enum OrderStatus { Open = 0, Paid = 1, Cancelled = 2, Refunded = 3 }

public enum ReferralStatus { Sent = 0, Registered = 1, Converted = 2, Rewarded = 3, Expired = 4 }

public enum ChallengeMetric { CheckIns = 0, ClassesAttended = 1, VolumeLifted = 2, StreakDays = 3, DistanceKm = 4 }

public enum FeedPostKind { PersonalRecord = 0, Milestone = 1, Announcement = 2, Transformation = 3, ChallengeResult = 4 }

public enum NotificationChannel { InApp = 0, Email = 1, WhatsApp = 2, Sms = 3, Push = 4 }

public enum NotificationKind { General = 0, BookingConfirmed = 1, WaitlistPromoted = 2, PaymentDue = 3, PaymentReceived = 4, ClassCancelled = 5, PersonalRecord = 6, StreakMilestone = 7, WinBack = 8, Birthday = 9 }

public enum CmsSectionType
{
    Hero = 0,
    Manifesto = 1,
    StatBand = 2,
    AmenityBento = 3,
    ClassRail = 4,
    TrainerHighlight = 5,
    TransformationSlider = 6,
    TestimonialWall = 7,
    PricingTable = 8,
    MarqueeTicker = 9,
    FaqAccordion = 10,
    CtaBanner = 11,
    BranchLocator = 12,
    AppQr = 13,
    RichText = 14,
    ImageFeature = 15,
    BlogRail = 16,
    CalculatorBlock = 17,
    ContactBlock = 18,
    OfferBanner = 19,
    TimetableEmbed = 20,
    AnnotatedFacility = 21,
    LeadForm = 22,
    SignatureScroll = 23
}

public enum PublishState { Draft = 0, Published = 1 }

public enum MediaKind { Image = 0, Video = 1, Document = 2 }

public enum OccupancyBand { Comfortable = 0, Busy = 1, Peak = 2 }

public enum ChurnRiskBand { Healthy = 0, Watch = 1, Amber = 2, Red = 3 }

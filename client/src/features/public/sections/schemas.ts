import { z } from 'zod'
import { ctaSchema } from '@/lib/cms'

/* ============================================================================
   Zod shapes for CmsSections.contentJson, one per section type.

   These are the client half of the CMS contract: the admin panel edits structured
   forms validated against the same shapes, so the owner cannot produce content the
   renderer does not understand. Sections that fail validation are skipped, never
   rendered half-built.
   ============================================================================ */

export const heroSchema = z.object({
  variant: z.enum(['full', 'compact', 'split', 'text', 'branch']).default('full'),
  eyebrow: z.string().optional(),
  headline: z.string(),
  kineticWords: z.array(z.string()).optional(),
  subhead: z.string().optional(),
  videoUrl: z.string().optional(),
  videoWebmUrl: z.string().optional(),
  posterUrl: z.string().optional(),
  posterAlt: z.string().optional(),
  overlayOpacity: z.number().min(0).max(1).default(0.65),
  primaryCta: ctaSchema.optional(),
  secondaryCta: ctaSchema.optional(),
  tertiaryCta: ctaSchema.optional(),
  scrollHint: z.string().optional(),
  bullets: z.array(z.string()).optional(),
})

export type HeroContent = z.infer<typeof heroSchema>

export const marqueeSchema = z.object({
  items: z.array(z.string()).min(1),
  separator: z.string().default('—'),
  speedSeconds: z.number().positive().default(42),
  style: z.enum(['outline', 'solid']).default('outline'),
  direction: z.enum(['left', 'right']).default('left'),
})

export type MarqueeContent = z.infer<typeof marqueeSchema>

export const statBandSchema = z.object({
  eyebrow: z.string().optional(),
  stats: z
    .array(
      z.object({
        value: z.number(),
        suffix: z.string().default(''),
        prefix: z.string().optional(),
        label: z.string(),
        format: z.enum(['integer', 'decimal', 'currency']).default('integer'),
      }),
    )
    .min(1),
  footnote: z.string().optional(),
})

export type StatBandContent = z.infer<typeof statBandSchema>

export const manifestoSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string(),
  body: z.string(),
  signatureLine: z.string().optional(),
  imageUrl: z.string().optional(),
  imageAlt: z.string().optional(),
  imagePosition: z.enum(['left', 'right']).default('right'),
})

export type ManifestoContent = z.infer<typeof manifestoSchema>

export const ctaBannerSchema = z.object({
  headline: z.string(),
  body: z.string().optional(),
  primaryCta: ctaSchema.optional(),
  secondaryCta: ctaSchema.optional(),
  imageUrl: z.string().optional(),
  imageAlt: z.string().optional(),
  microcopy: z.string().optional(),
})

export type CtaBannerContent = z.infer<typeof ctaBannerSchema>

/* ---------------------------------------------------------------- bento & imagery */

export const amenityBentoSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  body: z.string().optional(),
  tiles: z
    .array(
      z.object({
        /** Grid span, mixed on purpose (03 §5) — never a row of equal cards. */
        size: z.enum(['1x1', '2x1', '1x2', '2x2']).default('1x1'),
        title: z.string(),
        body: z.string().optional(),
        imageUrl: z.string().optional(),
        imageAlt: z.string().optional(),
        iconKey: z.string().optional(),
      }),
    )
    .min(1),
})

export type AmenityBentoContent = z.infer<typeof amenityBentoSchema>

export const annotatedFacilitySchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  imageUrl: z.string(),
  imageAlt: z.string().optional(),
  leaderLineColor: z.enum(['accent', 'bone']).default('accent'),
  callouts: z
    .array(
      z.object({
        /** Percentage coordinates over the photograph, so it scales with any crop. */
        x: z.number().min(0).max(100),
        y: z.number().min(0).max(100),
        label: z.string(),
        detail: z.string().optional(),
      }),
    )
    .min(1),
})

export type AnnotatedFacilityContent = z.infer<typeof annotatedFacilitySchema>

export const imageFeatureSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string(),
  body: z.string().optional(),
  imageUrl: z.string(),
  imageAlt: z.string().optional(),
  imagePosition: z.enum(['left', 'right', 'full']).default('left'),
  bullets: z.array(z.string()).optional(),
  cta: ctaSchema.optional(),
})

export type ImageFeatureContent = z.infer<typeof imageFeatureSchema>

export const signatureScrollSchema = z.object({
  headline: z.string(),
  subline: z.string().optional(),
  imageUrl: z.string(),
  imageAlt: z.string().optional(),
  startScale: z.number().min(0.4).max(1).default(0.8),
  endScale: z.number().min(0.8).max(1.2).default(1),
  letterTightening: z.boolean().default(true),
})

export type SignatureScrollContent = z.infer<typeof signatureScrollSchema>

export const richTextSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  blocks: z
    .array(
      z.object({
        type: z.enum(['paragraph', 'heading', 'quote', 'list']).default('paragraph'),
        text: z.string().optional(),
        items: z.array(z.string()).optional(),
      }),
    )
    .min(1),
  align: z.enum(['left', 'centre']).default('left'),
})

export type RichTextContent = z.infer<typeof richTextSchema>

/* ---------------------------------------------------------------- data-backed rails */

export const classRailSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  body: z.string().optional(),
  /** Empty plus showAll renders the whole library; otherwise this is the curated order. */
  formatSlugs: z.array(z.string()).default([]),
  showAll: z.boolean().default(false),
  showCapacityRing: z.boolean().default(true),
  showSpotsLeft: z.boolean().default(true),
  layout: z.enum(['rail', 'grid']).default('rail'),
  cta: ctaSchema.optional(),
})

export type ClassRailContent = z.infer<typeof classRailSchema>

export const trainerHighlightSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  body: z.string().optional(),
  trainerSlugs: z.array(z.string()).default([]),
  showAll: z.boolean().default(false),
  duotoneOnHover: z.boolean().default(true),
  showRatings: z.boolean().default(false),
  showPtPrice: z.boolean().default(false),
  groupByBranch: z.boolean().default(false),
  cta: ctaSchema.optional(),
})

export type TrainerHighlightContent = z.infer<typeof trainerHighlightSchema>

export const transformationSliderSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  body: z.string().optional(),
  limit: z.number().int().positive().optional(),
  showAll: z.boolean().default(false),
  layout: z.enum(['rail', 'grid']).default('rail'),
  showWeights: z.boolean().default(true),
  showStory: z.boolean().default(false),
  sliderHandleLabel: z.string().default('Drag to compare'),
  cta: ctaSchema.optional(),
})

export type TransformationSliderContent = z.infer<typeof transformationSliderSchema>

export const testimonialWallSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  featuredOnly: z.boolean().default(false),
  limit: z.number().int().positive().default(6),
  layout: z.enum(['row', 'masonry']).default('row'),
  showGoogleRating: z.boolean().default(false),
  googleRating: z.number().min(0).max(5).optional(),
  googleReviewCount: z.number().int().nonnegative().optional(),
  googleReviewUrl: z.string().optional(),
})

export type TestimonialWallContent = z.infer<typeof testimonialWallSchema>

export const blogRailSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  layout: z.enum(['featured-plus-grid', 'grid', 'rail']).default('grid'),
  featuredCount: z.number().int().nonnegative().default(1),
  pageSize: z.number().int().positive().default(9),
  showTags: z.boolean().default(true),
  showReadTime: z.boolean().default(true),
  showAuthor: z.boolean().default(true),
  emptyState: z.string().default('Nothing published yet.'),
  cta: ctaSchema.optional(),
})

export type BlogRailContent = z.infer<typeof blogRailSchema>

/* ---------------------------------------------------------------- pricing & offers */

export const pricingTableSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  body: z.string().optional(),
  /** Max three on screen at once (03 §7); the rest live in the compare table. */
  highlightedPlanSlugs: z.array(z.string()).default([]),
  comparePlanSlugs: z.array(z.string()).default([]),
  cycleToggle: z.array(z.string()).default([]),
  defaultCycle: z.string().optional(),
  showBranchSelector: z.boolean().default(true),
  trustMicrocopy: z.string().optional(),
  compareRows: z.array(z.object({ label: z.string(), key: z.string() })).default([]),
  footnote: z.string().optional(),
})

export type PricingTableContent = z.infer<typeof pricingTableSchema>

export const offerBannerSchema = z.object({
  enabled: z.boolean().default(true),
  couponCode: z.string().optional(),
  headline: z.string(),
  body: z.string().optional(),
  cta: ctaSchema.optional(),
  urgencyStyle: z.enum(['countdown', 'date', 'none']).default('date'),
})

export type OfferBannerContent = z.infer<typeof offerBannerSchema>

/* ---------------------------------------------------------------- branches & contact */

export const branchLocatorSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  body: z.string().optional(),
  showLiveOccupancy: z.boolean().default(true),
  showMap: z.boolean().default(true),
  showTimings: z.boolean().default(true),
  showAmenityChips: z.boolean().default(false),
  showPhone: z.boolean().default(true),
  showWhatsApp: z.boolean().default(true),
  showEmail: z.boolean().default(false),
  sortBy: z.enum(['displayOrder', 'name']).default('displayOrder'),
  nearestFirstPrompt: z.string().optional(),
  layout: z.enum(['cards', 'list']).default('cards'),
})

export type BranchLocatorContent = z.infer<typeof branchLocatorSchema>

/**
 * Two shapes share this type: the per-branch "getting here" block (address, map,
 * timings) and the generic enquiry-routing block (labelled rows). Both are optional
 * so one renderer covers each without the CMS needing two section types.
 */
export const contactBlockSchema = z.object({
  headline: z.string().optional(),
  body: z.string().optional(),
  addressLines: z.array(z.string()).optional(),
  phone: z.string().optional(),
  whatsapp: z.string().optional(),
  email: z.string().optional(),
  mapUrl: z.string().optional(),
  latitude: z.number().optional(),
  longitude: z.number().optional(),
  timings: z.array(z.object({ label: z.string(), value: z.string() })).optional(),
  showLiveOccupancy: z.boolean().default(false),
  parkingNote: z.string().optional(),
  rows: z
    .array(
      z.object({
        label: z.string(),
        value: z.string(),
        type: z.enum(['email', 'phone', 'whatsapp', 'text']).default('text'),
        note: z.string().optional(),
      }),
    )
    .optional(),
  responseNote: z.string().optional(),
})

export type ContactBlockContent = z.infer<typeof contactBlockSchema>

/* ---------------------------------------------------------------- interactive */

export const timetableEmbedSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  defaultBranchSlug: z.string().optional(),
  /** Branch pages pin their own branch — the filter is hidden rather than pre-set. */
  lockBranch: z.boolean().default(false),
  daysVisible: z.number().int().min(1).max(21).default(7),
  filters: z.array(z.enum(['branch', 'format', 'trainer', 'timeOfDay', 'level'])).default([]),
  showSpotsLeft: z.boolean().default(true),
  showCapacityRing: z.boolean().default(true),
  trialCtaLabel: z.string().default('Book free trial'),
  emptyState: z.string().default('No classes match those filters.'),
})

export type TimetableEmbedContent = z.infer<typeof timetableEmbedSchema>

export const appQrSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string(),
  body: z.string().optional(),
  bullets: z.array(z.string()).default([]),
  qrCaption: z.string().default('Scan to install'),
  qrTarget: z.string().optional(),
  screenshotUrl: z.string().optional(),
  screenshotAlt: z.string().optional(),
})

export type AppQrContent = z.infer<typeof appQrSchema>

export const calculatorBlockSchema = z.object({
  kind: z.enum(['bmi', 'bmr']),
  headline: z.string(),
  body: z.string().optional(),
  inputs: z
    .array(
      z.object({
        name: z.string(),
        label: z.string(),
        type: z.enum(['number', 'select']).default('number'),
        unit: z.string().optional(),
        min: z.number().optional(),
        max: z.number().optional(),
        default: z.number().optional(),
        options: z.array(z.string()).optional(),
      }),
    )
    .min(1),
  bands: z
    .array(z.object({ max: z.number(), label: z.string(), tone: z.enum(['success', 'warn', 'hot']) }))
    .optional(),
  outputs: z.array(z.string()).optional(),
  footnote: z.string().optional(),
  cta: ctaSchema.optional(),
})

export type CalculatorBlockContent = z.infer<typeof calculatorBlockSchema>

export const faqAccordionSchema = z.object({
  eyebrow: z.string().optional(),
  headline: z.string().optional(),
  categories: z.array(z.string()).optional(),
  limit: z.number().int().positive().optional(),
  showAll: z.boolean().default(false),
  groupByCategory: z.boolean().default(false),
  categoryOrder: z.array(z.string()).optional(),
  defaultOpenFirst: z.boolean().default(false),
})

export type FaqAccordionContent = z.infer<typeof faqAccordionSchema>

export const leadFormSchema = z.object({
  headline: z.string().optional(),
  body: z.string().optional(),
  steps: z
    .array(
      z.object({
        title: z.string(),
        fields: z
          .array(
            z.object({
              name: z.string(),
              label: z.string(),
              type: z.enum(['text', 'tel', 'email', 'date', 'select', 'textarea', 'branch']).default('text'),
              required: z.boolean().default(false),
              autoComplete: z.string().optional(),
              help: z.string().optional(),
              options: z.array(z.string()).optional(),
            }),
          )
          .min(1),
      }),
    )
    .min(1),
  consentLabel: z.string().optional(),
  submitLabel: z.string().default('Send'),
  successHeadline: z.string().default('Booked.'),
  successBody: z.string().optional(),
  failureBody: z.string().optional(),
  intent: z.string().default('trial'),
})

export type LeadFormContent = z.infer<typeof leadFormSchema>

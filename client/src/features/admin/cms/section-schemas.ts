import type { z } from 'zod'
import {
  amenityBentoSchema,
  annotatedFacilitySchema,
  appQrSchema,
  blogRailSchema,
  branchLocatorSchema,
  calculatorBlockSchema,
  classRailSchema,
  contactBlockSchema,
  ctaBannerSchema,
  faqAccordionSchema,
  heroSchema,
  imageFeatureSchema,
  leadFormSchema,
  manifestoSchema,
  marqueeSchema,
  offerBannerSchema,
  pricingTableSchema,
  richTextSchema,
  signatureScrollSchema,
  statBandSchema,
  testimonialWallSchema,
  timetableEmbedSchema,
  trainerHighlightSchema,
  transformationSliderSchema,
} from '@/features/public/sections/schemas'

/**
 * Section type → the shape the public renderer validates against.
 *
 * This is the same object graph `sections/registry.tsx` uses, imported rather than
 * copied: the CMS form, the save-time validation and the renderer all read one
 * definition, so a field can never exist in the editor that the site cannot draw.
 */
export const sectionSchemas: Record<string, z.ZodTypeAny> = {
  Hero: heroSchema,
  Manifesto: manifestoSchema,
  StatBand: statBandSchema,
  AmenityBento: amenityBentoSchema,
  ClassRail: classRailSchema,
  TrainerHighlight: trainerHighlightSchema,
  TransformationSlider: transformationSliderSchema,
  TestimonialWall: testimonialWallSchema,
  PricingTable: pricingTableSchema,
  MarqueeTicker: marqueeSchema,
  FaqAccordion: faqAccordionSchema,
  CtaBanner: ctaBannerSchema,
  BranchLocator: branchLocatorSchema,
  AppQr: appQrSchema,
  RichText: richTextSchema,
  ImageFeature: imageFeatureSchema,
  BlogRail: blogRailSchema,
  CalculatorBlock: calculatorBlockSchema,
  ContactBlock: contactBlockSchema,
  OfferBanner: offerBannerSchema,
  TimetableEmbed: timetableEmbedSchema,
  AnnotatedFacility: annotatedFacilitySchema,
  LeadForm: leadFormSchema,
  SignatureScroll: signatureScrollSchema,
}

/** CmsSectionType ordinals, matching Gym.Core.Enums.CmsSectionType. */
export const sectionTypeOrdinals: Record<string, number> = {
  Hero: 0,
  Manifesto: 1,
  StatBand: 2,
  AmenityBento: 3,
  ClassRail: 4,
  TrainerHighlight: 5,
  TransformationSlider: 6,
  TestimonialWall: 7,
  PricingTable: 8,
  MarqueeTicker: 9,
  FaqAccordion: 10,
  CtaBanner: 11,
  BranchLocator: 12,
  AppQr: 13,
  RichText: 14,
  ImageFeature: 15,
  BlogRail: 16,
  CalculatorBlock: 17,
  ContactBlock: 18,
  OfferBanner: 19,
  TimetableEmbed: 20,
  AnnotatedFacility: 21,
  LeadForm: 22,
  SignatureScroll: 23,
}

/** What each type is for, shown in the "add section" picker so the choice is obvious. */
export const sectionDescriptions: Record<string, string> = {
  Hero: 'Full-bleed opener with video or poster, kinetic headline and up to three CTAs.',
  Manifesto: 'Constrained statement block with an optional portrait beside it.',
  StatBand: 'A row of counting numbers — members, branches, classes a week.',
  AmenityBento: 'Mixed-size tile grid for facilities and amenities.',
  ClassRail: 'Horizontal rail of class formats with capacity rings.',
  TrainerHighlight: 'Coach cards, 3:4 portraits going duotone on hover.',
  TransformationSlider: 'Before/after drag comparisons with name, duration and programme.',
  TestimonialWall: 'Member quotes, optionally with the Google rating.',
  PricingTable: 'Up to three highlighted tiers plus a compare table and cycle toggle.',
  MarqueeTicker: 'Scrolling outlined type between two major sections.',
  FaqAccordion: 'Questions from the FAQ library, filtered by category.',
  CtaBanner: 'A single conversion band with one or two actions.',
  BranchLocator: 'Branch cards with map, timings and live occupancy.',
  AppQr: 'App pitch with a scannable QR and a device frame.',
  RichText: 'Paragraphs, headings, quotes and lists — structured, never raw HTML.',
  ImageFeature: 'One photograph beside a headline, body and bullets.',
  BlogRail: 'Journal posts as a featured piece plus a grid.',
  CalculatorBlock: 'BMI or BMR calculator with bands and a CTA.',
  ContactBlock: 'Address, map and timings, or labelled enquiry-routing rows.',
  OfferBanner: 'Seasonal offer strip tied to a live coupon code.',
  TimetableEmbed: 'The filterable class timetable, embeddable on any page.',
  AnnotatedFacility: 'A facility photo with gold leader lines pointing at equipment zones.',
  LeadForm: 'The two-step trial form that feeds the leads pipeline.',
  SignatureScroll: 'The pinned scroll moment — image scales while the headline tightens.',
}

export const sectionTypeNames = Object.keys(sectionSchemas)

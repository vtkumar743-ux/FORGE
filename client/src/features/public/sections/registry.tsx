import type { ReactNode } from 'react'
import { parseSection, type CmsSection } from '@/lib/cms'
import { HeroSection } from './HeroSection'
import { MarqueeSection } from './MarqueeSection'
import { StatBandSection } from './StatBandSection'
import { ManifestoSection } from './ManifestoSection'
import { CtaBannerSection } from './CtaBannerSection'
import { AmenityBentoSection } from './AmenityBentoSection'
import { AnnotatedFacilitySection } from './AnnotatedFacilitySection'
import { AppQrSection } from './AppQrSection'
import { BlogRailSection } from './BlogRailSection'
import { BranchLocatorSection } from './BranchLocatorSection'
import { CalculatorBlockSection } from './CalculatorBlockSection'
import { ClassRailSection } from './ClassRailSection'
import { ContactBlockSection } from './ContactBlockSection'
import { FaqAccordionSection } from './FaqAccordionSection'
import { ImageFeatureSection } from './ImageFeatureSection'
import { LeadFormSection } from './LeadFormSection'
import { OfferBannerSection } from './OfferBannerSection'
import { PricingTableSection } from './PricingTableSection'
import { RichTextSection } from './RichTextSection'
import { SignatureScrollSection } from './SignatureScrollSection'
import { TestimonialWallSection } from './TestimonialWallSection'
import { TimetableEmbedSection } from './TimetableEmbedSection'
import { TrainerHighlightSection } from './TrainerHighlightSection'
import { TransformationSliderSection } from './TransformationSliderSection'
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
} from './schemas'

/**
 * Section registry — the single place a CmsSectionType is bound to a React component.
 *
 * The public site renders whatever the CMS returns, in the order the CMS returns it, so
 * adding a section type is a registry entry plus a Zod shape and nothing else. All 24
 * seeded types now have a renderer; content that fails its shape is skipped rather than
 * rendered half-built, which keeps one bad admin edit from taking a page down.
 */
type SectionRenderer = (section: CmsSection) => ReactNode

const renderers: Record<string, SectionRenderer> = {
  Hero: (section) => {
    const content = parseSection(section, heroSchema)
    return content ? <HeroSection content={content} /> : null
  },
  MarqueeTicker: (section) => {
    const content = parseSection(section, marqueeSchema)
    return content ? <MarqueeSection content={content} /> : null
  },
  StatBand: (section) => {
    const content = parseSection(section, statBandSchema)
    return content ? <StatBandSection content={content} /> : null
  },
  Manifesto: (section) => {
    const content = parseSection(section, manifestoSchema)
    return content ? <ManifestoSection content={content} /> : null
  },
  CtaBanner: (section) => {
    const content = parseSection(section, ctaBannerSchema)
    return content ? <CtaBannerSection content={content} /> : null
  },
  AmenityBento: (section) => {
    const content = parseSection(section, amenityBentoSchema)
    return content ? <AmenityBentoSection content={content} /> : null
  },
  ClassRail: (section) => {
    const content = parseSection(section, classRailSchema)
    return content ? <ClassRailSection content={content} /> : null
  },
  TrainerHighlight: (section) => {
    const content = parseSection(section, trainerHighlightSchema)
    return content ? <TrainerHighlightSection content={content} /> : null
  },
  TransformationSlider: (section) => {
    const content = parseSection(section, transformationSliderSchema)
    return content ? <TransformationSliderSection content={content} /> : null
  },
  TestimonialWall: (section) => {
    const content = parseSection(section, testimonialWallSchema)
    return content ? <TestimonialWallSection content={content} /> : null
  },
  PricingTable: (section) => {
    const content = parseSection(section, pricingTableSchema)
    return content ? <PricingTableSection content={content} /> : null
  },
  FaqAccordion: (section) => {
    const content = parseSection(section, faqAccordionSchema)
    return content ? <FaqAccordionSection content={content} /> : null
  },
  BranchLocator: (section) => {
    const content = parseSection(section, branchLocatorSchema)
    return content ? <BranchLocatorSection content={content} /> : null
  },
  AppQr: (section) => {
    const content = parseSection(section, appQrSchema)
    return content ? <AppQrSection content={content} /> : null
  },
  RichText: (section) => {
    const content = parseSection(section, richTextSchema)
    return content ? <RichTextSection content={content} /> : null
  },
  ImageFeature: (section) => {
    const content = parseSection(section, imageFeatureSchema)
    return content ? <ImageFeatureSection content={content} /> : null
  },
  BlogRail: (section) => {
    const content = parseSection(section, blogRailSchema)
    return content ? <BlogRailSection content={content} /> : null
  },
  CalculatorBlock: (section) => {
    const content = parseSection(section, calculatorBlockSchema)
    return content ? <CalculatorBlockSection content={content} /> : null
  },
  ContactBlock: (section) => {
    const content = parseSection(section, contactBlockSchema)
    return content ? <ContactBlockSection content={content} /> : null
  },
  OfferBanner: (section) => {
    const content = parseSection(section, offerBannerSchema)
    return content ? <OfferBannerSection content={content} /> : null
  },
  TimetableEmbed: (section) => {
    const content = parseSection(section, timetableEmbedSchema)
    return content ? <TimetableEmbedSection content={content} /> : null
  },
  AnnotatedFacility: (section) => {
    const content = parseSection(section, annotatedFacilitySchema)
    return content ? <AnnotatedFacilitySection content={content} /> : null
  },
  LeadForm: (section) => {
    const content = parseSection(section, leadFormSchema)
    return content ? <LeadFormSection content={content} /> : null
  },
  SignatureScroll: (section) => {
    const content = parseSection(section, signatureScrollSchema)
    return content ? <SignatureScrollSection content={content} /> : null
  },
}

export function renderSection(section: CmsSection): ReactNode {
  const renderer = renderers[section.typeName]

  if (!renderer) {
    if (import.meta.env.DEV) {
      console.warn(
        `[cms] section type "${section.typeName}" (key "${section.key}") has no renderer. ` +
          'Register it in sections/registry.tsx with a Zod shape.',
      )
    }
    return null
  }

  return renderer(section)
}

export function hasRenderer(typeName: string): boolean {
  return typeName in renderers
}

/** Every type the renderer can draw — the admin panel's section picker reads this. */
export const registeredSectionTypes = Object.keys(renderers)

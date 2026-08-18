import { z } from 'zod'

/* ============================================================================
   Zod → form fields

   The admin section editor is generated from the *same* Zod shapes the public
   renderer validates against (`features/public/sections/schemas.ts`). That is the
   whole point: a field cannot drift out of sync with what the site can draw,
   because there is only one definition of the shape. Adding a property to a
   section schema makes an input appear in the CMS with no further work.

   Labels and control hints are inferred from the property name and refined by a
   small override table, so the generated form still reads like it was written by
   hand rather than like a JSON dump.
   ============================================================================ */

export type FieldKind =
  | 'text'
  | 'textarea'
  | 'media'
  | 'number'
  | 'boolean'
  | 'select'
  | 'stringList'
  | 'objectList'
  | 'object'
  | 'unknown'

export interface FieldSpec {
  name: string
  label: string
  kind: FieldKind
  optional: boolean
  defaultValue: unknown
  options?: string[]
  /** Present for `object` and `objectList`. */
  children?: FieldSpec[]
  hint?: string
  /** Rows for a textarea; also used to decide how much space the field takes. */
  rows?: number
}

/* ---------------------------------------------------------------- naming */

const acronyms: Record<string, string> = {
  cta: 'CTA',
  url: 'URL',
  seo: 'SEO',
  faq: 'FAQ',
  qr: 'QR',
  bmi: 'BMI',
  bmr: 'BMR',
  id: 'ID',
}

/** "primaryCta" → "Primary CTA"; "posterUrl" → "Poster URL". */
export function humanise(name: string): string {
  const words = name
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .trim()
    .split(' ')
    .filter(Boolean)

  return words
    .map((word, index) => {
      const lower = word.toLowerCase()
      if (acronyms[lower]) return acronyms[lower]
      return index === 0 ? word[0].toUpperCase() + word.slice(1) : lower
    })
    .join(' ')
}

/* ---------------------------------------------------------------- hints */

/**
 * Copy the generated form cannot infer. Keyed by the leaf property name, so it
 * applies wherever that property appears — a headline is a headline on any section.
 */
const hints: Record<string, string> = {
  headline: 'The one big line. Keep it under about eight words.',
  eyebrow: 'Small uppercase label above the headline.',
  kineticWords: 'Words that animate in turn inside the hero headline.',
  overlayOpacity: '0 shows the footage plainly, 1 hides it. 0.65 keeps text legible.',
  videoUrl: 'MP4 loop. Leave blank to show the poster, which is a valid finished state.',
  posterUrl: 'Shown before the video loads and whenever motion is reduced.',
  formatSlugs: 'Leave empty and switch "Show all" on to render the whole library.',
  trainerSlugs: 'Leave empty and switch "Show all" on to render every coach.',
  highlightedPlanSlugs: 'Three at most — the rest belong in the compare table.',
  showAll: 'Ignores the hand-picked list above and renders everything available.',
  lockBranch: 'On a branch page this pins the branch and hides the filter.',
  size: '2x2 for a hero tile, 1x1 for a stat. Mixed sizes are what makes a bento grid.',
  consentLabel: 'The exact wording the visitor agrees to. This is a legal record.',
  x: 'Horizontal position over the photo, 0–100%.',
  y: 'Vertical position over the photo, 0–100%.',
  couponCode: 'Must match a live coupon code for the banner to price correctly.',
  speedSeconds: 'Seconds for one full loop. Higher is slower.',
}

const mediaNames = /(image|poster|screenshot|cover|photo|avatar|logo|video|webm)/i

/** Long-form copy gets a textarea; everything else a single line. */
const proseNames = /^(body|description|story|quote|answer|bio|subhead|lead|note|notes|footnote|microcopy|trustMicrocopy|successBody|failureBody|emptyState|responseNote|parkingNote|nearestFirstPrompt|detail|caption|text)$/

/* ---------------------------------------------------------------- unwrap */

interface Unwrapped {
  schema: z.ZodTypeAny
  optional: boolean
  defaultValue: unknown
}

/**
 * Peels `.optional()`, `.nullable()` and `.default()` off a schema so the inner
 * type — and the default the CMS should seed a new section with — are both known.
 */
function unwrap(schema: z.ZodTypeAny): Unwrapped {
  let current = schema
  let optional = false
  let defaultValue: unknown

  // Zod 4 exposes the internals as `.def`; wrappers nest, so this loops.
  for (let depth = 0; depth < 8; depth++) {
    const def = (current as unknown as { def?: Record<string, unknown> }).def
    const type = def?.type as string | undefined

    if (type === 'optional' || type === 'nullable') {
      optional = true
      current = def!.innerType as z.ZodTypeAny
      continue
    }
    if (type === 'default' || type === 'prefault') {
      const factory = def!.defaultValue
      defaultValue = typeof factory === 'function' ? (factory as () => unknown)() : factory
      current = def!.innerType as z.ZodTypeAny
      continue
    }
    break
  }

  return { schema: current, optional, defaultValue }
}

function typeOf(schema: z.ZodTypeAny): string {
  return ((schema as unknown as { def?: { type?: string } }).def?.type as string) ?? 'unknown'
}

function shapeOf(schema: z.ZodTypeAny): Record<string, z.ZodTypeAny> | null {
  const shape = (schema as unknown as { shape?: Record<string, z.ZodTypeAny> }).shape
  return shape && typeof shape === 'object' ? shape : null
}

function enumValues(schema: z.ZodTypeAny): string[] {
  const entries = (schema as unknown as { def?: { entries?: Record<string, string> } }).def?.entries
  return entries ? Object.values(entries) : []
}

/* ---------------------------------------------------------------- describe */

export function describeField(name: string, schema: z.ZodTypeAny, depth = 0): FieldSpec {
  const { schema: inner, optional, defaultValue } = unwrap(schema)
  const type = typeOf(inner)
  const label = humanise(name)
  const hint = hints[name]

  const base = { name, label, optional, defaultValue, hint }

  switch (type) {
    case 'string':
      if (mediaNames.test(name)) return { ...base, kind: 'media' }
      if (proseNames.test(name)) return { ...base, kind: 'textarea', rows: 4 }
      return { ...base, kind: 'text' }

    case 'number':
    case 'int':
      return { ...base, kind: 'number' }

    case 'boolean':
      return { ...base, kind: 'boolean', defaultValue: defaultValue ?? false }

    case 'enum':
      return { ...base, kind: 'select', options: enumValues(inner) }

    case 'literal': {
      const values = (inner as unknown as { def?: { values?: unknown[] } }).def?.values ?? []
      return { ...base, kind: 'select', options: values.map(String) }
    }

    case 'array': {
      const element = (inner as unknown as { def: { element: z.ZodTypeAny } }).def.element
      const { schema: elementInner } = unwrap(element)
      const elementType = typeOf(elementInner)

      if (elementType === 'object' && depth < 2) {
        const shape = shapeOf(elementInner)
        return {
          ...base,
          kind: 'objectList',
          defaultValue: defaultValue ?? [],
          children: shape
            ? Object.entries(shape).map(([key, value]) => describeField(key, value, depth + 1))
            : [],
        }
      }
      if (elementType === 'string' || elementType === 'number') {
        return { ...base, kind: 'stringList', defaultValue: defaultValue ?? [] }
      }
      return { ...base, kind: 'unknown', defaultValue: defaultValue ?? [] }
    }

    case 'object': {
      const shape = shapeOf(inner)
      if (!shape || depth >= 2) return { ...base, kind: 'unknown' }
      return {
        ...base,
        kind: 'object',
        children: Object.entries(shape).map(([key, value]) => describeField(key, value, depth + 1)),
      }
    }

    default:
      return { ...base, kind: 'unknown' }
  }
}

/** The ordered field list for one section type. */
export function describeSchema(schema: z.ZodTypeAny): FieldSpec[] {
  const { schema: inner } = unwrap(schema)
  const shape = shapeOf(inner)
  if (!shape) return []
  return Object.entries(shape).map(([key, value]) => describeField(key, value))
}

/* ---------------------------------------------------------------- defaults */

/** A blank value of the right shape, used when the owner adds a repeater row. */
export function emptyValue(field: FieldSpec): unknown {
  if (field.defaultValue !== undefined) return structuredClone(field.defaultValue)

  switch (field.kind) {
    case 'number':
      return 0
    case 'boolean':
      return false
    case 'select':
      return field.options?.[0] ?? ''
    case 'stringList':
    case 'objectList':
      return []
    case 'object':
      return Object.fromEntries((field.children ?? []).map((child) => [child.name, emptyValue(child)]))
    case 'unknown':
      return null
    default:
      return ''
  }
}

/** Seed content for a brand-new section of a given type. */
export function blankContent(fields: FieldSpec[]): Record<string, unknown> {
  const result: Record<string, unknown> = {}
  for (const field of fields) {
    // Optional fields with no default stay absent so the renderer's own fallbacks apply.
    if (field.optional && field.defaultValue === undefined) continue
    result[field.name] = emptyValue(field)
  }
  return result
}

/* ---------------------------------------------------------------- validation */

export interface ValidationIssue {
  path: string
  message: string
}

/**
 * Validates edited content against the section's real schema before it is saved.
 * The API stores whatever JSON it is given, so this is what actually stops an edit
 * that the public renderer would silently skip.
 */
export function validateContent(
  schema: z.ZodTypeAny,
  value: unknown,
): { ok: true; data: unknown } | { ok: false; issues: ValidationIssue[] } {
  const result = schema.safeParse(value)
  if (result.success) return { ok: true, data: result.data }

  return {
    ok: false,
    issues: result.error.issues.map((issue) => ({
      path: issue.path.join('.') || '(section)',
      message: issue.message,
    })),
  }
}

/**
 * Strips empty optional values so a blank input does not persist as "" and beat the
 * renderer's fallback — an empty string is content, an absent key is not.
 */
export function clean(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(clean)
  if (value && typeof value === 'object') {
    const entries = Object.entries(value as Record<string, unknown>)
      .map(([key, item]) => [key, clean(item)] as const)
      .filter(([, item]) => item !== '' && item !== undefined && item !== null)
    return Object.fromEntries(entries)
  }
  return value
}

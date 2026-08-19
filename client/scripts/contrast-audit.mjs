#!/usr/bin/env node
/**
 * WCAG AA contrast audit for the palette (03 §2, and the NFR that names AA on the dark theme).
 *
 * Checks every text and boundary pairing the product actually paints, on both surfaces, with
 * the values read out of the stylesheets rather than restated here — so a palette edit either
 * still passes or breaks this. Exits non-zero on a failure so it can gate a build.
 *
 *   node scripts/contrast-audit.mjs
 */
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { contrast, over, readToken } from './lib/contrast.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const tokensCss = readFileSync(join(here, '..', 'src', 'styles', 'tokens.css'), 'utf8')
const indexCss = readFileSync(join(here, '..', 'src', 'styles', 'index.css'), 'utf8')

const token = (name) => readToken(tokensCss, name)

const ink = token('ink')
const carbon = token('carbon')
const steel = token('steel')
const bone = token('bone')
const smoke = token('smoke')
const accent = token('accent')
const accentHot = token('accent-hot')
const success = token('success')
const paper = token('paper')
const paperRaised = token('paper-raised')
const paperInk = token('paper-ink')
const paperSmoke = token('paper-smoke')
const paperLine = token('paper-line')

/**
 * The admin surface runs darker instances of gold and signal red: both brand values are
 * chosen against near-black and fall under AA on paper. Read from the .theme-light block so
 * the audit tracks the real override rather than a copy of it.
 */
const lightBlock = indexCss.slice(indexCss.indexOf('.theme-light'))
const lightAccent = readToken(lightBlock, 'accent')
const lightAccentHot = readToken(lightBlock, 'accent-hot')
const lightSuccess = readToken(lightBlock, 'success')
const lightSmoke = readToken(lightBlock, 'smoke')

/**
 * `kind` sets the bar: AA is 4.5:1 for body text, and 3:1 for large text and for the
 * boundary of a form control (1.4.11). Decorative dividers are out of scope and not listed —
 * a hairline between two cards conveys nothing a reader needs.
 */
const checks = [
  // --- dark surface: the public site and the member portal
  ['Body text on page', bone, ink, 'body'],
  ['Body text on card', bone, carbon, 'body'],
  ['Body text on input', bone, steel, 'body'],
  ['Secondary text on page', smoke, ink, 'body'],
  ['Secondary text on card', smoke, carbon, 'body'],
  ['Secondary text on input', smoke, steel, 'body'],
  ['Gold on page', accent, ink, 'body'],
  ['Gold on card', accent, carbon, 'body'],
  ['Ink on gold button', ink, accent, 'body'],
  ['Signal red on page', accentHot, ink, 'body'],
  ['Signal red on card', accentHot, carbon, 'body'],
  ['Success on page', success, ink, 'body'],
  ['Success on card', success, carbon, 'body'],
  ['Dimmed body text (62%)', over(bone, ink, 0.62), ink, 'body'],
  ['Input border on page', over(bone, ink, 0.42), ink, 'ui'],
  ['Input border on card', over(bone, ink, 0.42), carbon, 'ui'],
  // Outlined display type is painted entirely by its stroke, so the stroke carries the text
  // contrast rather than a UI-component one. Large text, so the bar is 3:1.
  ['Outlined display type', over(bone, ink, 0.42), ink, 'large'],

  // --- light surface: the admin panel
  ['Admin body text', paperInk, paper, 'body'],
  ['Admin body text on card', paperInk, paperRaised, 'body'],
  ['Admin secondary text', lightSmoke, paper, 'body'],
  ['Admin secondary text on card', lightSmoke, paperRaised, 'body'],
  // Avatar initials and thumbnail captions sit on the --steel fill, not on the page.
  ['Admin secondary text on fill', lightSmoke, paperLine, 'body'],
  // The mint green is a dark-surface value; on paper it measured 1.9:1, so the light theme
  // runs its own. Status pills print it on a 10% tint of itself.
  ['Admin success text', lightSuccess, paper, 'body'],
  ['Admin success on card', lightSuccess, paperRaised, 'body'],
  ['Admin success on its pill tint', lightSuccess, over(lightSuccess, paperRaised, 0.1), 'body'],
  ['Admin danger on its pill tint', lightAccentHot, over(lightAccentHot, paperRaised, 0.09), 'body'],
  ['Admin gold on its pill tint', lightAccent, over(lightAccent, paperRaised, 0.14), 'body'],
  ['Admin gold accent', lightAccent, paper, 'body'],
  ['Admin gold on card', lightAccent, paperRaised, 'body'],
  // The primary button is `bg-accent text-ink`, and .theme-light remaps --ink to paper —
  // so the label on a gold button is near-white, not near-black.
  ['Button label on admin gold', paper, lightAccent, 'body'],
  ['Admin signal red', lightAccentHot, paper, 'body'],
  ['Admin signal red on card', lightAccentHot, paperRaised, 'body'],
  ['Admin input border', over(paperInk, paper, 0.5), paper, 'ui'],
]

const AA = { body: 4.5, large: 3, ui: 3 }

let failures = 0
console.log('\nWCAG AA contrast audit\n' + '='.repeat(60))

for (const [label, foreground, background, kind] of checks) {
  const ratio = contrast(foreground, background)
  const required = AA[kind]
  const pass = ratio >= required
  if (!pass) failures++
  console.log(`${pass ? 'PASS' : 'FAIL'}  ${ratio.toFixed(2).padStart(5)}:1  (needs ${required})  ${label}`)
}

console.log('='.repeat(60))
if (failures === 0) {
  console.log(`All ${checks.length} pairings meet AA.\n`)
} else {
  console.error(`${failures} of ${checks.length} pairings fall below AA.\n`)
  process.exitCode = 1
}

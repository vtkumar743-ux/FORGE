#!/usr/bin/env node
/**
 * Builds responsive WebP renditions of the brand photography.
 *
 * `fetch-media.mjs` downloads 4K originals, which is right for the source of truth and wrong
 * for what a phone on Indian mobile data should download. This generates a WebP at each width
 * the layouts actually request, next to the original, so `<Photo>` can hand the browser a
 * srcset and let it pick. Originals stay on disk untouched as the fallback and the master.
 *
 *   node scripts/optimise-media.mjs           # only what has changed
 *   node scripts/optimise-media.mjs --force   # rebuild everything
 */
import { readdir, stat, mkdir, writeFile } from 'node:fs/promises'
import { existsSync } from 'node:fs'
import { dirname, extname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const here = dirname(fileURLToPath(import.meta.url))
const mediaRoot = join(here, '..', 'public', 'media')
const force = process.argv.includes('--force')

/**
 * The widths the layouts ask for. A rendition wider than the source is never written — an
 * upscale costs bytes and adds nothing.
 */
const WIDTHS = [480, 960, 1440, 1920]
const QUALITY = 74

/** Directories whose images are never displayed at full width get a smaller ladder. */
const NARROW = new Set(['trainers', 'testimonials', 'classes', 'blog', 'transformations'])
const NARROW_WIDTHS = [320, 640, 960]

/** OG cards are fetched by crawlers at a fixed size and must stay JPEG — skip them. */
const SKIP = new Set(['og'])

async function* walk(dir) {
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name)
    if (entry.isDirectory()) {
      if (SKIP.has(entry.name)) continue
      yield* walk(full)
    } else {
      yield full
    }
  }
}

const manifest = {}
let written = 0
let skipped = 0
let savedBytes = 0

for await (const file of walk(mediaRoot)) {
  const ext = extname(file).toLowerCase()
  if (!['.jpg', '.jpeg', '.png'].includes(ext)) continue
  // Never re-process a rendition we produced on an earlier run.
  if (/-\d+w\.webp$/.test(file)) continue

  const source = sharp(file)
  const metadata = await source.metadata()
  if (!metadata.width) continue

  const folder = relative(mediaRoot, dirname(file)).split(/[\\/]/)[0]
  const ladder = NARROW.has(folder) ? NARROW_WIDTHS : WIDTHS
  const widths = ladder.filter((width) => width <= metadata.width)
  if (widths.length === 0) widths.push(metadata.width)

  const base = file.slice(0, -ext.length)
  const originalBytes = (await stat(file)).size
  const entries = []

  for (const width of widths) {
    const target = `${base}-${width}w.webp`
    if (!force && existsSync(target)) {
      entries.push({ width, path: target })
      skipped++
      continue
    }

    await mkdir(dirname(target), { recursive: true })
    const buffer = await sharp(file)
      .resize({ width, withoutEnlargement: true })
      .webp({ quality: QUALITY, effort: 5 })
      .toBuffer()
    await writeFile(target, buffer)
    entries.push({ width, path: target })
    written++
    savedBytes += Math.max(0, originalBytes / widths.length - buffer.length)
  }

  const publicPath = '/media/' + relative(mediaRoot, file).split(/[\\/]/).join('/')
  manifest[publicPath] = entries
    .map((entry) => entry.width)
    .sort((a, b) => a - b)
}

// The client reads this to know which widths exist for a given source, so it never points a
// srcset at a rendition that was never generated.
await writeFile(
  join(here, '..', 'src', 'lib', 'media-manifest.json'),
  JSON.stringify(manifest, null, 2) + '\n',
)

console.log(
  `${written} renditions written, ${skipped} already present, ` +
    `${Object.keys(manifest).length} sources · about ${(savedBytes / 1024 / 1024).toFixed(1)} MB lighter per view`,
)

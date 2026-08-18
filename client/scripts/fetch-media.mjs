#!/usr/bin/env node
/**
 * Downloads the brand imagery listed in media.manifest.json into public/media.
 *
 * Binaries stay out of the repository, but a fresh clone is one `npm run media` away from
 * a complete site — so the public pages never have to fall back to a grey placeholder.
 * Already-downloaded files are skipped; pass --force to re-fetch everything.
 */

import { createWriteStream } from 'node:fs'
import { mkdir, readFile, stat, unlink } from 'node:fs/promises'
import { dirname, join, resolve } from 'node:path'
import { pipeline } from 'node:stream/promises'
import { Readable } from 'node:stream'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..')
const force = process.argv.includes('--force')

const manifest = JSON.parse(await readFile(join(root, 'media.manifest.json'), 'utf8'))
const outputRoot = join(root, manifest.outputDir)

/** Unsplash serves derivatives from query params, so one source covers every size we need. */
function sourceUrl(asset) {
  const params = new URLSearchParams({ q: '82', fm: 'jpg', auto: 'format' })
  params.set('w', String(asset.width))
  if (asset.height) params.set('h', String(asset.height))
  params.set('fit', asset.crop ? 'crop' : 'max')
  if (asset.crop) params.set('crop', 'entropy')
  return `${manifest.baseUrl}/${asset.id}?${params}`
}

async function exists(path) {
  try {
    const info = await stat(path)
    return info.size > 1024
  } catch {
    return false
  }
}

async function download(asset) {
  const target = join(outputRoot, asset.path)

  if (!force && (await exists(target))) {
    return { path: asset.path, status: 'skipped' }
  }

  await mkdir(dirname(target), { recursive: true })
  const url = sourceUrl(asset)

  const response = await fetch(url, {
    headers: { 'User-Agent': 'forge-media-fetch/1.0 (+build script)' },
    redirect: 'follow',
  })

  if (!response.ok || !response.body) {
    throw new Error(`HTTP ${response.status} for ${asset.path}`)
  }

  const type = response.headers.get('content-type') ?? ''
  if (!type.startsWith('image/')) {
    throw new Error(`Expected an image for ${asset.path}, got "${type}"`)
  }

  try {
    await pipeline(Readable.fromWeb(response.body), createWriteStream(target))
  } catch (error) {
    // Never leave a truncated file behind — a half-written JPEG is worse than a missing one.
    await unlink(target).catch(() => {})
    throw error
  }

  const info = await stat(target)
  return { path: asset.path, status: 'downloaded', bytes: info.size }
}

const results = []
let failures = 0

// `$group` rows are section headings in the manifest, not assets — they carry no path.
const assets = manifest.assets.filter((asset) => asset.path && asset.id)

// Sequential on purpose: this is a one-off build step, not worth hammering the CDN in parallel.
for (const asset of assets) {
  try {
    results.push(await download(asset))
  } catch (error) {
    failures += 1
    results.push({ path: asset.path, status: 'failed', error: error.message })
  }
}

const downloaded = results.filter((r) => r.status === 'downloaded')
const skipped = results.filter((r) => r.status === 'skipped')

for (const result of results) {
  if (result.status === 'downloaded') {
    console.log(`  ↓ ${result.path}  ${(result.bytes / 1024).toFixed(0)} kB`)
  } else if (result.status === 'failed') {
    console.error(`  ✗ ${result.path}  ${result.error}`)
  }
}

console.log(
  `\nmedia: ${downloaded.length} downloaded, ${skipped.length} already present, ${failures} failed.`,
)

const pendingCount = Object.keys(manifest.pending ?? {}).filter((key) => key !== '$comment').length
if (pendingCount > 0) {
  console.log(`${pendingCount} asset group(s) still listed as pending in media.manifest.json.`)
}

process.exit(failures > 0 ? 1 : 0)

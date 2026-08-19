#!/usr/bin/env node
/**
 * Rasterises the FORGE barbell mark into the PNG icons a browser cannot take as SVG:
 * apple-touch-icon (iOS home screen) and the two manifest sizes.
 *
 * Hand-rolled rather than pulling in a rasteriser: the mark is five capsules and a rounded
 * rectangle, which is a few lines of distance-field maths, and it keeps the build free of a
 * native image dependency that would have to be installed on every machine that clones this.
 *
 *   node scripts/make-icons.mjs
 */
import { deflateSync } from 'node:zlib'
import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const publicDir = join(here, '..', 'public')

// The palette from 03 §2 — the icon must be the same black and gold as everything else.
const INK = [0x0a, 0x0a, 0x0a]
const GOLD = [0xec, 0xd0, 0x6f]
const BONE = [0xf5, 0xf3, 0xee]

/** Distance from a point to a line segment — a capsule is this, thresholded by its radius. */
function segmentDistance(px, py, x1, y1, x2, y2) {
  const dx = x2 - x1
  const dy = y2 - y1
  const lengthSquared = dx * dx + dy * dy
  const t = lengthSquared === 0 ? 0 : Math.max(0, Math.min(1, ((px - x1) * dx + (py - y1) * dy) / lengthSquared))
  const cx = x1 + t * dx
  const cy = y1 + t * dy
  return Math.hypot(px - cx, py - cy)
}

/** Signed distance to a rounded rectangle, negative inside. */
function roundedRectDistance(px, py, w, h, r) {
  const qx = Math.abs(px - w / 2) - (w / 2 - r)
  const qy = Math.abs(py - h / 2) - (h / 2 - r)
  const outside = Math.hypot(Math.max(qx, 0), Math.max(qy, 0))
  return outside + Math.min(Math.max(qx, qy), 0) - r
}

function mix(base, layer, alpha) {
  return [
    Math.round(base[0] * (1 - alpha) + layer[0] * alpha),
    Math.round(base[1] * (1 - alpha) + layer[1] * alpha),
    Math.round(base[2] * (1 - alpha) + layer[2] * alpha),
  ]
}

/**
 * The mark, in the same 32-unit space as favicon.svg so the two never drift apart.
 * `padding` insets the artwork for maskable icons, whose outer ring gets cropped on Android.
 */
function renderIcon(size, { padding = 0, transparent = false } = {}) {
  const scale = size / 32
  const inset = padding * size
  const artSize = size - inset * 2
  const artScale = artSize / 32

  // Straight from the SVG: four sleeve capsules in gold, one bar in bone.
  const capsules = [
    { x1: 6.5, y1: 11.5, x2: 6.5, y2: 20.5, width: 2.4, color: GOLD },
    { x1: 9.5, y1: 9.5, x2: 9.5, y2: 22.5, width: 2.4, color: GOLD },
    { x1: 22.5, y1: 9.5, x2: 22.5, y2: 22.5, width: 2.4, color: GOLD },
    { x1: 25.5, y1: 11.5, x2: 25.5, y2: 20.5, width: 2.4, color: GOLD },
    { x1: 9.5, y1: 16, x2: 22.5, y2: 16, width: 2.2, color: BONE },
  ]

  const pixels = Buffer.alloc(size * size * 4)

  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const px = x + 0.5
      const py = y + 0.5

      let rgb = INK
      let alpha = 1

      if (transparent) {
        alpha = 0
      } else {
        // Rounded-rect plate, antialiased across one pixel.
        const plate = roundedRectDistance(px, py, size, size, 7 * scale)
        alpha = Math.max(0, Math.min(1, 0.5 - plate))
      }

      for (const capsule of capsules) {
        const distance = segmentDistance(
          px,
          py,
          inset + capsule.x1 * artScale,
          inset + capsule.y1 * artScale,
          inset + capsule.x2 * artScale,
          inset + capsule.y2 * artScale,
        )
        const coverage = Math.max(0, Math.min(1, (capsule.width / 2) * artScale - distance + 0.5))
        if (coverage > 0) {
          rgb = mix(rgb, capsule.color, coverage)
          alpha = Math.max(alpha, coverage)
        }
      }

      const offset = (y * size + x) * 4
      pixels[offset] = rgb[0]
      pixels[offset + 1] = rgb[1]
      pixels[offset + 2] = rgb[2]
      pixels[offset + 3] = Math.round(alpha * 255)
    }
  }

  return encodePng(size, size, pixels)
}

/* ---------------------------------------------------------------- PNG encoding */

function crc32(buffer) {
  let crc = 0xffffffff
  for (const byte of buffer) {
    crc ^= byte
    for (let bit = 0; bit < 8; bit++) crc = crc & 1 ? (crc >>> 1) ^ 0xedb88320 : crc >>> 1
  }
  return (crc ^ 0xffffffff) >>> 0
}

function chunk(type, data) {
  const length = Buffer.alloc(4)
  length.writeUInt32BE(data.length)
  const typed = Buffer.concat([Buffer.from(type, 'ascii'), data])
  const crc = Buffer.alloc(4)
  crc.writeUInt32BE(crc32(typed))
  return Buffer.concat([length, typed, crc])
}

function encodePng(width, height, rgba) {
  // Each scanline is prefixed with its filter byte; 0 (none) keeps the encoder honest and small.
  const raw = Buffer.alloc(height * (width * 4 + 1))
  for (let y = 0; y < height; y++) {
    raw[y * (width * 4 + 1)] = 0
    rgba.copy(raw, y * (width * 4 + 1) + 1, y * width * 4, (y + 1) * width * 4)
  }

  const header = Buffer.alloc(13)
  header.writeUInt32BE(width, 0)
  header.writeUInt32BE(height, 4)
  header[8] = 8 // bit depth
  header[9] = 6 // colour type: RGBA
  header[10] = 0
  header[11] = 0
  header[12] = 0

  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ])
}

/* ---------------------------------------------------------------- write */

mkdirSync(publicDir, { recursive: true })

const outputs = [
  // iOS never renders the SVG favicon; without this the home-screen icon is a screenshot.
  ['apple-touch-icon.png', renderIcon(180)],
  ['icon-192.png', renderIcon(192)],
  ['icon-512.png', renderIcon(512)],
  // Android masks icons to a circle and crops ~10% off each edge, so the mark is inset.
  ['icon-maskable-512.png', renderIcon(512, { padding: 0.14 })],
]

for (const [name, buffer] of outputs) {
  writeFileSync(join(publicDir, name), buffer)
  console.log(`wrote public/${name} (${(buffer.length / 1024).toFixed(1)} kB)`)
}

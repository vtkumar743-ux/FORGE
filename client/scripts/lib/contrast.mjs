/**
 * Contrast maths, shared by the audit script. Kept separate so the ratio calculation can be
 * reasoned about (and corrected) without touching the list of pairings it is applied to.
 */

export function toRgb(hex) {
  const clean = hex.replace('#', '')
  const full = clean.length === 3 ? [...clean].map((c) => c + c).join('') : clean
  return [0, 2, 4].map((offset) => parseInt(full.slice(offset, offset + 2), 16))
}

/** Flattens a translucent layer over a known background — how the browser actually paints it. */
export function over(hex, backgroundHex, alpha) {
  const layer = toRgb(hex)
  const base = toRgb(backgroundHex)
  return layer.map((channel, index) => Math.round(channel * alpha + base[index] * (1 - alpha)))
}

export function relativeLuminance(rgb) {
  const [r, g, b] = rgb.map((channel) => {
    const value = channel / 255
    return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

export function contrast(foreground, background) {
  const a = relativeLuminance(Array.isArray(foreground) ? foreground : toRgb(foreground))
  const b = relativeLuminance(Array.isArray(background) ? background : toRgb(background))
  const [light, dark] = a > b ? [a, b] : [b, a]
  return (light + 0.05) / (dark + 0.05)
}

/** Pulls a literal hex custom property out of a CSS string. */
export function readToken(css, name) {
  const pattern = new RegExp('--' + name + ':\\s*(#[0-9a-fA-F]{3,8})')
  const match = css.match(pattern)
  if (!match) throw new Error(`token --${name} not found`)
  return match[1]
}

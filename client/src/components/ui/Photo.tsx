import { cn } from '@/lib/utils'
import manifest from '@/lib/media-manifest.json'

/**
 * One image element for the brand photography.
 *
 * `scripts/optimise-media.mjs` writes a WebP at each width the layouts actually ask for, and
 * records what it produced in a manifest. This reads that manifest and hands the browser a
 * srcset, so a phone downloads a 480px rendition instead of the 4K master. The original stays
 * as the `src` fallback, which also means an image the pipeline has not processed yet still
 * renders — it just renders heavy.
 *
 * The one-grade CSS treatment (03 §4) rides on the same element, so every photograph on the
 * site continues to look like it came from one shoot.
 */

const widthsBySource = manifest as Record<string, number[]>

export function buildSrcSet(src: string | undefined): string | undefined {
  if (!src) return undefined
  const widths = widthsBySource[src]
  if (!widths || widths.length === 0) return undefined

  const base = src.replace(/\.(jpe?g|png)$/i, '')
  return widths.map((width) => `${base}-${width}w.webp ${width}w`).join(', ')
}

/**
 * The widest rendition for a source, for the places that need a plain URL rather than an
 * `<img>` — a `<video poster>`, an OG tag, a CSS background.
 */
export function bestSrc(src: string | undefined): string | undefined {
  if (!src) return src
  const widths = widthsBySource[src]
  if (!widths || widths.length === 0) return src
  const widest = widths[widths.length - 1]
  return `${src.replace(/\.(jpe?g|png)$/i, '')}-${widest}w.webp`
}

export function Photo({
  src,
  alt,
  sizes = '100vw',
  className,
  graded = true,
  priority = false,
  width,
  height,
  ...rest
}: {
  src: string | undefined
  alt: string
  /** Tell the browser how wide this renders, or it assumes the full viewport and over-fetches. */
  sizes?: string
  className?: string
  graded?: boolean
  /** True only for the LCP image on a page — everything else stays lazy. */
  priority?: boolean
  width?: number
  height?: number
} & Omit<React.ImgHTMLAttributes<HTMLImageElement>, 'src' | 'alt' | 'sizes' | 'width' | 'height'>) {
  if (!src) return null

  const srcSet = buildSrcSet(src)

  return (
    <img
      // With a srcset present, `src` is only the fallback for a browser that cannot read one
      // — and setting it makes Chrome fetch that file *as well as* the candidate it chooses,
      // because the attribute lands before `srcset` does. Every browser this product targets
      // supports srcset, so the original is passed only when no renditions exist for it.
      src={srcSet ? undefined : src}
      srcSet={srcSet}
      sizes={srcSet ? sizes : undefined}
      alt={alt}
      width={width}
      height={height}
      loading={priority ? 'eager' : 'lazy'}
      fetchPriority={priority ? 'high' : 'auto'}
      decoding={priority ? 'sync' : 'async'}
      className={cn(graded && 'graded', className)}
      {...rest}
    />
  )
}

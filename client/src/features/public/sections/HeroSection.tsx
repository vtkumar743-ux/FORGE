import { useEffect, useRef, useState } from 'react'
import { motion, useReducedMotion } from 'motion/react'
import { ButtonLink } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { cn, toLines } from '@/lib/utils'
import type { HeroContent } from './schemas'
import { Photo } from '@/components/ui/Photo'

/**
 * Cinematic hero (03 §1, Barry's/Third Space pattern): full-bleed looping video,
 * one headline, one primary action. The poster paints first so LCP is an image, not
 * a video decode; the loop only attaches once it can play, and never under
 * prefers-reduced-motion.
 */
export function HeroSection({ content }: { content: HeroContent }) {
  const reduced = useReducedMotion()
  const videoRef = useRef<HTMLVideoElement>(null)
  const [videoReady, setVideoReady] = useState(false)

  const isFull = content.variant === 'full'
  const isTextOnly = content.variant === 'text'
  const hasVideo = Boolean(content.videoUrl) && !reduced

  useEffect(() => {
    const video = videoRef.current
    if (!video || !hasVideo) return

    const onPlaying = () => setVideoReady(true)
    video.addEventListener('playing', onPlaying)
    // Autoplay can be refused; the poster simply stays, which is a valid final state.
    void video.play().catch(() => setVideoReady(false))
    return () => video.removeEventListener('playing', onPlaying)
  }, [hasVideo])

  const lines = toLines(content.headline)

  return (
    <section
      className={cn(
        'relative isolate flex flex-col justify-end overflow-hidden',
        isTextOnly ? 'bg-ink pt-28 pb-16' : 'grain',
        isFull && 'min-h-[min(92svh,54rem)]',
        content.variant === 'compact' && 'min-h-[min(62svh,34rem)]',
        content.variant === 'branch' && 'min-h-[min(72svh,40rem)]',
        content.variant === 'split' && 'min-h-[min(70svh,38rem)]',
      )}
    >
      {!isTextOnly && (
        <div className="absolute inset-0 -z-10">
          {content.posterUrl && (
            <Photo
              src={content.posterUrl}
              alt={content.posterAlt ?? ''}
              // The hero is the LCP element: eager, high priority, and full-viewport wide.
              priority
              sizes="100vw"
              className={cn(
                'h-full w-full object-cover transition-opacity duration-700',
                videoReady && 'opacity-0',
              )}
            />
          )}

          {hasVideo && (
            <video
              ref={videoRef}
              muted
              loop
              playsInline
              preload="none"
              // Deliberately no `poster`: the <img> above is the poster, rendered from a
              // responsive srcset. Setting one here downloads a second copy of the frame at a
              // width nobody chose, for a video that starts transparent and preloads nothing.
              aria-hidden
              tabIndex={-1}
              className={cn(
                'graded absolute inset-0 h-full w-full object-cover transition-opacity duration-700',
                videoReady ? 'opacity-100' : 'opacity-0',
              )}
            >
              {content.videoWebmUrl && <source src={content.videoWebmUrl} type="video/webm" />}
              {content.videoUrl && <source src={content.videoUrl} type="video/mp4" />}
            </video>
          )}

          {/* Dark gradient so the headline clears AA contrast over any frame. */}
          <div
            aria-hidden
            className="absolute inset-0"
            style={{
              background: `linear-gradient(to top, rgb(10 10 10 / ${Math.min(1, content.overlayOpacity + 0.3)}) 0%, rgb(10 10 10 / ${content.overlayOpacity}) 45%, rgb(10 10 10 / ${Math.max(0, content.overlayOpacity - 0.35)}) 100%)`,
            }}
          />
        </div>
      )}

      <div className="shell relative z-10 pb-16 pt-24 sm:pb-20">
        <div className="max-w-4xl">
          {content.eyebrow && (
            <motion.p
              className="caption text-bone/70"
              initial={reduced ? undefined : { opacity: 0, y: 12 }}
              animate={reduced ? undefined : { opacity: 1, y: 0 }}
              transition={{ duration: 0.5, ease: [0.16, 1, 0.3, 1] }}
            >
              {content.eyebrow}
            </motion.p>
          )}

          <h1
            className={cn(
              'mt-5 text-bone',
              isFull ? 'display-xl' : 'display-l',
            )}
          >
            {lines.map((line, index) => (
              <KineticLine
                key={`${line}-${index}`}
                text={line}
                index={index}
                emphasise={content.kineticWords ?? []}
                reduced={Boolean(reduced)}
              />
            ))}
          </h1>

          {content.subhead && (
            <motion.p
              className="measure mt-7 text-[1.0625rem] leading-relaxed text-bone/80 sm:text-body-l"
              initial={reduced ? undefined : { opacity: 0, y: 14 }}
              animate={reduced ? undefined : { opacity: 1, y: 0 }}
              transition={{ duration: 0.55, delay: 0.25 + lines.length * 0.08, ease: [0.16, 1, 0.3, 1] }}
            >
              {content.subhead}
            </motion.p>
          )}

          {content.bullets && content.bullets.length > 0 && (
            <ul className="mt-7 grid gap-2.5 sm:grid-cols-2">
              {content.bullets.map((bullet) => (
                <li key={bullet} className="flex items-start gap-2.5 text-[0.9375rem] text-bone/80">
                  <Icon name="check" size={16} strokeWidth={2} className="mt-1 text-accent" />
                  {bullet}
                </li>
              ))}
            </ul>
          )}

          {/* Triple-intent CTAs: gold primary, outline secondary, text tertiary. */}
          {(content.primaryCta || content.secondaryCta || content.tertiaryCta) && (
            <motion.div
              className="mt-10 flex flex-wrap items-center gap-3"
              initial={reduced ? undefined : { opacity: 0, y: 14 }}
              animate={reduced ? undefined : { opacity: 1, y: 0 }}
              transition={{ duration: 0.55, delay: 0.35 + lines.length * 0.08, ease: [0.16, 1, 0.3, 1] }}
            >
              {content.primaryCta && (
                <ButtonLink to={content.primaryCta.href} size="lg" magnetic>
                  {content.primaryCta.label}
                </ButtonLink>
              )}
              {content.secondaryCta && (
                <ButtonLink to={content.secondaryCta.href} variant="outline" size="lg">
                  {content.secondaryCta.label}
                </ButtonLink>
              )}
              {content.tertiaryCta && (
                <ButtonLink
                  to={content.tertiaryCta.href}
                  variant="ghost"
                  size="lg"
                  icon="arrow-right"
                  iconAfter
                  className="ml-1"
                >
                  {content.tertiaryCta.label}
                </ButtonLink>
              )}
            </motion.div>
          )}
        </div>
      </div>

      {isFull && content.scrollHint && !reduced && (
        <motion.div
          aria-hidden
          className="absolute bottom-6 left-1/2 z-10 -translate-x-1/2"
          animate={{ y: [0, 7, 0] }}
          transition={{ duration: 2.4, repeat: Infinity, ease: 'easeInOut' }}
        >
          <span className="caption flex flex-col items-center gap-2 text-bone/45">
            {content.scrollHint}
            <Icon name="chevron-down" size={16} />
          </span>
        </motion.div>
      )}
    </section>
  )
}

/**
 * The kinetic-typography signature (03 §3): each line fades up, and the words the
 * CMS marks as emphasis animate their variable-font weight 400 → 700 as they land.
 */
function KineticLine({
  text,
  index,
  emphasise,
  reduced,
}: {
  text: string
  index: number
  emphasise: string[]
  reduced: boolean
}) {
  const shouldEmphasise = emphasise.some((word) => text.toLowerCase().includes(word.toLowerCase()))

  if (reduced) {
    return (
      <span className="block" style={{ fontWeight: shouldEmphasise ? 700 : 600 }}>
        {text}
      </span>
    )
  }

  return (
    <motion.span
      className="block"
      initial={{ opacity: 0, y: 26, fontWeight: 400 }}
      animate={{ opacity: 1, y: 0, fontWeight: shouldEmphasise ? 700 : 600 }}
      transition={{ duration: 0.85, delay: 0.12 + index * 0.09, ease: [0.16, 1, 0.3, 1] }}
    >
      {text}
    </motion.span>
  )
}

import { useRef } from 'react'
import { motion, useReducedMotion, useScroll, useTransform } from 'motion/react'
import type { SignatureScrollContent } from './schemas'
import { Photo } from '@/components/ui/Photo'

/**
 * The one signature scroll moment on the home page (03 §6): a facility photograph scales
 * from 80% to full bleed while it is pinned, and the headline's letter-spacing tightens as
 * it lands. Exactly one of these exists site-wide — a second would make it a gimmick.
 *
 * The whole effect short-circuits under prefers-reduced-motion to a static full-bleed
 * image with the headline over it, which is a valid final frame rather than a fallback.
 */
export function SignatureScrollSection({ content }: { content: SignatureScrollContent }) {
  const reduced = useReducedMotion()
  const trackRef = useRef<HTMLDivElement>(null)

  const { scrollYProgress } = useScroll({
    target: trackRef,
    offset: ['start end', 'end start'],
  })

  // Peaks at the midpoint — the image reaches full bleed exactly while it is centred.
  const scale = useTransform(scrollYProgress, [0, 0.5, 1], [content.startScale, content.endScale, content.endScale])
  const radius = useTransform(scrollYProgress, [0, 0.5], ['var(--radius-card)', '0px'])
  const tracking = useTransform(scrollYProgress, [0.15, 0.6], ['0.14em', '-0.02em'])
  const textOpacity = useTransform(scrollYProgress, [0.2, 0.45], [0, 1])

  if (reduced) {
    return (
      <section className="relative isolate grain overflow-hidden">
        <Photo
          src={content.imageUrl}
          alt={content.imageAlt ?? ''}
          sizes="100vw"
          className="h-[min(80svh,44rem)] w-full object-cover"
        />
        <div aria-hidden className="absolute inset-0 bg-ink/55" />
        <div className="absolute inset-0 flex flex-col items-center justify-center px-6 text-center">
          <h2 className="display-xl text-bone">{content.headline}</h2>
          {content.subline && <p className="mt-4 text-body-l text-bone/75">{content.subline}</p>}
        </div>
      </section>
    )
  }

  return (
    <section ref={trackRef} className="relative h-[220svh] bg-ink">
      <div className="sticky top-0 flex h-svh items-center justify-center overflow-hidden">
        <motion.div
          className="grain relative isolate h-[86svh] w-full overflow-hidden"
          style={{ scale, borderRadius: radius }}
        >
          <Photo
            src={content.imageUrl}
            alt={content.imageAlt ?? ''}
            sizes="100vw"
            className="absolute inset-0 h-full w-full object-cover"
          />
          <div aria-hidden className="absolute inset-0 bg-ink/50" />

          <motion.div
            className="absolute inset-0 flex flex-col items-center justify-center px-6 text-center"
            style={{ opacity: textOpacity }}
          >
            <motion.h2
              className="display-xl text-bone"
              style={content.letterTightening ? { letterSpacing: tracking } : undefined}
            >
              {content.headline}
            </motion.h2>
            {content.subline && (
              <p className="mt-5 text-body-l text-bone/75">{content.subline}</p>
            )}
          </motion.div>
        </motion.div>
      </div>
    </section>
  )
}

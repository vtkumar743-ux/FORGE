import { Reveal } from '@/components/ui/Reveal'
import { cn } from '@/lib/utils'
import type { RichTextContent } from './schemas'

/**
 * Structured prose. The CMS stores blocks, never HTML — so an owner cannot paste markup
 * that escapes the type scale, and the reading measure stays at 680px however long the
 * copy grows (03 §5).
 */
export function RichTextSection({ content }: { content: RichTextContent }) {
  const centred = content.align === 'centre'

  return (
    <section className="section-y bg-ink">
      <div className="shell">
        <div className={cn('measure', centred && 'mx-auto text-center')}>
          {content.eyebrow && (
            <Reveal>
              <p className="caption">{content.eyebrow}</p>
            </Reveal>
          )}

          {content.headline && (
            <Reveal delay={0.05}>
              <h2 className="display-l mt-5 text-bone">{content.headline}</h2>
            </Reveal>
          )}

          <div className="mt-8 space-y-6">
            {content.blocks.map((block, index) => (
              <Reveal key={index} delay={Math.min(0.3, index * 0.06)}>
                {block.type === 'heading' ? (
                  <h3 className="display-m mt-10 text-[1.375rem] text-bone">{block.text}</h3>
                ) : block.type === 'quote' ? (
                  <blockquote className="border-l-2 border-accent pl-6">
                    <p className="text-[1.25rem] leading-relaxed text-bone/90">{block.text}</p>
                  </blockquote>
                ) : block.type === 'list' ? (
                  <ul className={cn('space-y-2.5', centred && 'inline-block text-left')}>
                    {(block.items ?? []).map((item) => (
                      <li key={item} className="flex gap-3 text-[1.0625rem] leading-relaxed text-smoke">
                        <span aria-hidden className="mt-3 size-1 shrink-0 rounded-full bg-accent" />
                        {item}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-[1.0625rem] leading-relaxed text-smoke">{block.text}</p>
                )}
              </Reveal>
            ))}
          </div>
        </div>
      </div>
    </section>
  )
}

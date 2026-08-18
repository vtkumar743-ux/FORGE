import { useState } from 'react'
import { Reveal } from '@/components/ui/Reveal'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { useFaqs, type Faq } from '@/lib/public-api'
import { SectionHeader } from '../components/SectionHeader'
import { cn } from '@/lib/utils'
import type { FaqAccordionContent } from './schemas'

/**
 * FAQ accordion. Built on <details>/<summary> so it opens without JavaScript, is
 * keyboard-operable for free and gets announced correctly — an accordion rebuilt out of
 * divs and aria attributes is the classic place a design system quietly loses a11y.
 *
 * The FAQ page groups by category; every other page pulls a handful of the relevant ones,
 * which is why the CMS names categories rather than individual questions.
 */
export function FaqAccordionSection({ content }: { content: FaqAccordionContent }) {
  const { data, isLoading } = useFaqs()
  const all = data ?? []

  const filtered = content.categories?.length
    ? all.filter((faq) => content.categories!.includes(faq.category))
    : all

  const limited = content.showAll ? filtered : filtered.slice(0, content.limit ?? 6)

  const groups = content.groupByCategory ? groupByCategory(limited, content.categoryOrder) : [{ name: null, items: limited }]

  return (
    <section className="section-y bg-ink">
      <div className="shell">
        <SectionHeader eyebrow={content.eyebrow} headline={content.headline} />

        {isLoading && (
          <div className="mt-12 space-y-3" aria-busy="true">
            {Array.from({ length: 4 }).map((_, index) => (
              <Skeleton key={index} className="h-16 w-full" />
            ))}
          </div>
        )}

        <div className="mt-12 max-w-4xl">
          {groups.map((group) => (
            <div key={group.name ?? 'all'} className="mb-12 last:mb-0">
              {group.name && (
                <Reveal>
                  <h3 className="caption mb-5 flex items-center gap-4">
                    {group.name}
                    <span aria-hidden className="h-px flex-1 bg-[var(--hairline)]" />
                  </h3>
                </Reveal>
              )}

              <div className="divide-y divide-[var(--hairline)] border-y border-[var(--hairline)]">
                {group.items.map((faq, index) => (
                  <FaqRow key={faq.id} faq={faq} defaultOpen={content.defaultOpenFirst && index === 0} />
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}

function FaqRow({ faq, defaultOpen }: { faq: Faq; defaultOpen?: boolean }) {
  const [open, setOpen] = useState(Boolean(defaultOpen))

  return (
    <details
      open={open}
      onToggle={(event) => setOpen((event.currentTarget as HTMLDetailsElement).open)}
      className="group"
    >
      <summary className="flex cursor-pointer list-none items-start justify-between gap-6 py-6 [&::-webkit-details-marker]:hidden">
        <h4
          className={cn(
            'text-[1.0625rem] font-medium leading-snug transition-colors duration-200 ease-out',
            open ? 'text-accent' : 'text-bone group-hover:text-accent',
          )}
        >
          {faq.question}
        </h4>
        <span
          aria-hidden
          className={cn(
            'mt-0.5 inline-flex size-8 shrink-0 items-center justify-center rounded-full border',
            'border-[var(--hairline-strong)] transition-[transform,border-color] duration-200 ease-out',
            open ? 'rotate-45 border-accent text-accent' : 'text-smoke group-hover:border-accent',
          )}
        >
          <Icon name="plus" size={15} />
        </span>
      </summary>

      <p className="measure pb-7 text-[0.9375rem] leading-relaxed text-smoke">{faq.answer}</p>
    </details>
  )
}

function groupByCategory(faqs: Faq[], order?: string[]): Array<{ name: string; items: Faq[] }> {
  const groups = new Map<string, Faq[]>()
  for (const faq of faqs) {
    const existing = groups.get(faq.category)
    if (existing) existing.push(faq)
    else groups.set(faq.category, [faq])
  }

  const entries = [...groups.entries()].map(([name, items]) => ({ name, items }))
  if (!order?.length) return entries

  // The CMS's category order wins; anything it does not name falls to the end alphabetically.
  return entries.sort((a, b) => {
    const left = order.indexOf(a.name)
    const right = order.indexOf(b.name)
    if (left === -1 && right === -1) return a.name.localeCompare(b.name)
    if (left === -1) return 1
    if (right === -1) return -1
    return left - right
  })
}

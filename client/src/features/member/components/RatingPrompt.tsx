import { useState } from 'react'
import { Icon } from '@/components/ui/Icon'
import { Button } from '@/components/ui/Button'
import { cn, formatClock } from '@/lib/utils'
import { describeErrorText } from '@/features/admin/lib/format'
import { useRateClass } from '../lib/portal-api'
import { DrawnCheck, InlineNote } from './ui'
import type { RatingPrompt as Prompt } from '../lib/types'

/**
 * Post-class rating (Module 3 — Support). It appears on the home screen after a
 * class the member actually attended, and it is dismissible: a prompt you cannot
 * get rid of is a prompt people answer dishonestly to make it go away.
 *
 * A comment is optional above three stars and asked for below it, because that is
 * the score the desk needs to be able to act on.
 */
export function RatingPromptCard({ prompt, onDone }: { prompt: Prompt; onDone?: () => void }) {
  const [score, setScore] = useState(0)
  const [hover, setHover] = useState(0)
  const [comment, setComment] = useState('')
  const [done, setDone] = useState(false)
  const rate = useRateClass()

  const shown = hover || score
  const needsComment = score > 0 && score <= 2

  if (done) {
    return (
      <div className="flex items-center gap-4 rounded-[var(--radius-card)] border border-success/40 bg-[color-mix(in_srgb,var(--success)_7%,var(--carbon))] p-5">
        <DrawnCheck size={40} />
        <div>
          <p className="text-[0.9375rem] font-medium text-bone">Thanks — that goes straight to the coach.</p>
          <p className="mt-1 text-[0.8125rem] text-smoke">
            {score <= 2 ? 'The desk has been told too. Expect a call.' : 'Ratings shape who teaches what.'}
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--hairline)] bg-carbon p-5">
      <div className="flex items-start gap-4">
        <img
          src={prompt.trainerPortraitUrl ?? undefined}
          alt=""
          className="graded size-12 shrink-0 rounded-full object-cover"
          loading="lazy"
        />
        <div className="min-w-0 flex-1">
          <p className="caption">How was it?</p>
          <h3 className="display-m mt-2 text-[1.0625rem] text-bone">{prompt.formatName}</h3>
          <p className="mt-1 text-[0.8125rem] text-smoke">
            {prompt.trainerName} · {formatClock(prompt.startTime)}
          </p>

          <div className="mt-4 flex items-center gap-1" onMouseLeave={() => setHover(0)}>
            {[1, 2, 3, 4, 5].map((value) => (
              <button
                key={value}
                type="button"
                onClick={() => setScore(value)}
                onMouseEnter={() => setHover(value)}
                aria-label={`${value} star${value === 1 ? '' : 's'}`}
                aria-pressed={score === value}
                className="grid size-11 place-items-center rounded-full transition-transform duration-150 hover:scale-110 motion-reduce:hover:scale-100"
              >
                <Icon
                  name="star"
                  size={24}
                  strokeWidth={1.6}
                  className={cn(
                    'transition-colors duration-150',
                    value <= shown ? 'fill-accent text-accent' : 'text-bone/25',
                  )}
                />
              </button>
            ))}
          </div>

          {score > 0 && (
            <div className="mt-4 space-y-3">
              <textarea
                className="field-input"
                rows={2}
                placeholder={
                  needsComment
                    ? 'What went wrong? The desk reads this today.'
                    : 'Anything you want the coach to know? (optional)'
                }
                value={comment}
                onChange={(event) => setComment(event.target.value)}
                aria-label="Comment"
              />
              {rate.isError && (
                <InlineNote tone="danger" icon="x">
                  {describeErrorText(rate.error)}
                </InlineNote>
              )}
              <div className="flex flex-wrap items-center gap-2.5">
                <Button
                  size="sm"
                  loading={rate.isPending}
                  disabled={needsComment && comment.trim().length < 3}
                  onClick={() =>
                    rate.mutate(
                      { bookingId: prompt.bookingId, score, comment: comment.trim() || undefined },
                      { onSuccess: () => setDone(true) },
                    )
                  }
                >
                  Send rating
                </Button>
                {onDone && (
                  <Button variant="ghost" size="sm" onClick={onDone}>
                    Not now
                  </Button>
                )}
                {needsComment && comment.trim().length < 3 && (
                  <span className="text-[0.75rem] text-smoke">A line of context, then send.</span>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

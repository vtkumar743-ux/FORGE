import { useState } from 'react'
import { Badge } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { EmptyState, Skeleton } from '@/components/ui/Skeleton'
import { formatInr, whatsappLink } from '@/lib/utils'
import { describeErrorText, relativeTime } from '@/features/admin/lib/format'
import { useInvite, useReferrals } from './lib/portal-api'
import { DrawnCheck, Field, InlineNote, Panel, PortalHeading, StatTile } from './components/ui'

/**
 * Referrals (Module 3 — Referrals). ₹500 both sides, per the spec.
 *
 * An invitation does not just sit in a rewards table: it lands on the desk's
 * pipeline board with the referrer's name on it, and gets the same five-minute
 * follow-up every other lead gets. A referral nobody calls is a discount the gym
 * would end up paying for twice.
 */
export function ReferralsPage() {
  const { data, isLoading } = useReferrals()
  const [name, setName] = useState('')
  const [phone, setPhone] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [sent, setSent] = useState(false)
  const [copied, setCopied] = useState(false)
  const invite = useInvite()

  if (isLoading || !data) {
    return (
      <div>
        <PortalHeading eyebrow="Bring a friend" title="Referrals" />
        <Skeleton className="h-64" />
      </div>
    )
  }

  const shareUrl = `${window.location.origin}${data.shareUrl}`

  function copy() {
    void navigator.clipboard?.writeText(`${data!.shareMessage} ${shareUrl}`).then(() => {
      setCopied(true)
      window.setTimeout(() => setCopied(false), 2400)
    })
  }

  return (
    <div className="space-y-8">
      <PortalHeading
        eyebrow="Bring a friend"
        title="Referrals"
        lead={`₹${data.rewardAmount.toLocaleString('en-IN')} off for them, ₹${data.rewardAmount.toLocaleString('en-IN')} credit for you — paid when they join, not when they enquire.`}
      />

      <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
        <StatTile label="Invited" value={data.invited} icon="share" />
        <StatTile label="Joined" value={data.joined} icon="users" tone={data.joined > 0 ? 'success' : 'neutral'} />
        <StatTile
          label="Credit earned"
          value={formatInr(data.creditEarned)}
          sub="Applied at your next renewal"
          icon="medal"
          tone={data.creditEarned > 0 ? 'accent' : 'neutral'}
        />
        <StatTile
          label="Waiting on"
          value={formatInr(data.creditPending)}
          sub="Joined, reward not yet released"
          icon="clock"
        />
      </div>

      <div className="grid gap-5 lg:grid-cols-2">
        <Panel title="Your code">
          <p className="numeric display-l text-center text-[clamp(2.5rem,7vw,3.5rem)] tracking-[0.06em] text-accent">
            {data.code}
          </p>
          <p className="measure mx-auto mt-4 text-center text-[0.875rem] leading-relaxed text-smoke">
            They enter it on the free-trial form, or just give your name at the desk. Either works.
          </p>

          <div className="mt-6 flex flex-wrap justify-center gap-2.5">
            <Button
              icon="share"
              onClick={() => {
                if ('share' in navigator) {
                  void navigator.share({ text: data.shareMessage, url: shareUrl }).catch(() => copy())
                } else {
                  copy()
                }
              }}
            >
              Share the link
            </Button>
            <Button
              variant="outline"
              icon="phone"
              onClick={() =>
                window.open(whatsappLink('', `${data.shareMessage} ${shareUrl}`), '_blank', 'noopener')
              }
            >
              WhatsApp
            </Button>
            <Button variant="ghost" onClick={copy}>
              {copied ? 'Copied' : 'Copy'}
            </Button>
          </div>

          <p className="mt-5 break-all rounded-[var(--radius-card)] border border-[var(--hairline)] bg-ink/40 px-4 py-3 text-center text-[0.75rem] text-smoke">
            {shareUrl}
          </p>
        </Panel>

        <Panel title="Invite someone directly" description="We call them, not you.">
          {sent ? (
            <div className="flex flex-col items-center py-6 text-center">
              <DrawnCheck size={52} />
              <p className="mt-5 text-[0.9375rem] text-bone">Invitation sent</p>
              <p className="measure mt-2 text-[0.875rem] leading-relaxed text-smoke">
                They are on the desk's board with your name against them. Your credit lands the day they join.
              </p>
              <Button variant="ghost" size="sm" className="mt-5" onClick={() => setSent(false)}>
                Invite someone else
              </Button>
            </div>
          ) : (
            <div className="space-y-4">
              <Field label="Their name">
                <input
                  className="field-input"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  placeholder="Aditi Rao"
                  autoComplete="off"
                />
              </Field>
              <Field label="Their mobile" hint="Ten digits. We open with your name, not a cold pitch.">
                <input
                  className="field-input"
                  inputMode="tel"
                  value={phone}
                  onChange={(event) => setPhone(event.target.value)}
                  placeholder="98765 43210"
                  autoComplete="off"
                />
              </Field>

              {error && (
                <InlineNote tone="danger" icon="x">
                  {error}
                </InlineNote>
              )}

              <Button
                fullWidth
                loading={invite.isPending}
                disabled={name.trim().length < 2 || phone.replace(/\D/g, '').length < 10}
                onClick={() => {
                  setError(null)
                  invite.mutate(
                    { name: name.trim(), phone: phone.trim() },
                    {
                      onSuccess: () => {
                        setSent(true)
                        setName('')
                        setPhone('')
                      },
                      onError: (failure) => setError(describeErrorText(failure)),
                    },
                  )
                }}
              >
                Send the invitation
              </Button>
              <p className="text-[0.6875rem] leading-relaxed text-smoke/75">
                Only share a number you have permission to share. We contact them once, and stop if they ask.
              </p>
            </div>
          )}
        </Panel>
      </div>

      <Panel title="Who you have invited" padded={false}>
        {data.rows.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon="share"
              headline="Nobody yet"
              body="Most people join because someone they train with told them to. Your code is above."
            />
          </div>
        ) : (
          <ul className="divide-y divide-[var(--hairline)]">
            {data.rows.map((row) => (
              <li key={row.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
                <div className="min-w-0">
                  <p className="text-[0.9375rem] text-bone">{row.inviteeName ?? 'Invitation'}</p>
                  <p className="numeric mt-1 text-[0.75rem] text-smoke">
                    {row.inviteePhone ?? '—'} · sent {relativeTime(row.invitedAtUtc)}
                    {row.expiresOn ? ` · expires ${row.expiresOn}` : ''}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  {row.referrerRewarded && (
                    <span className="numeric inline-flex items-center gap-1.5 text-[0.8125rem] text-success">
                      <Icon name="check" size={13} strokeWidth={2.2} />
                      {formatInr(row.rewardAmount)}
                    </span>
                  )}
                  <Badge
                    tone={
                      row.statusName === 'Rewarded' || row.statusName === 'Converted'
                        ? 'success'
                        : row.statusName === 'Expired'
                          ? 'neutral'
                          : 'accent'
                    }
                  >
                    {row.statusName}
                  </Badge>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </div>
  )
}

import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Icon, type IconName } from '@/components/ui/Icon'
import { EmptyState, Skeleton } from '@/components/ui/Skeleton'
import { cn } from '@/lib/utils'
import { relativeTime } from '@/features/admin/lib/format'
import { useMarkAllRead, useMarkNotificationRead, useNotifications } from './lib/portal-api'
import { Panel, PillToggle, PortalHeading } from './components/ui'

/**
 * Notifications centre (Module 3 — Support).
 *
 * In-app rows only. Every message the system decides to send writes an in-app row
 * plus one row per external channel, so the WhatsApp and SMS copies of the same
 * booking confirmation are delivery records — showing them here would triple every
 * notification the member has ever had.
 */
const KIND_ICONS: Record<string, IconName> = {
  'Booking Confirmed': 'calendar-check',
  'Waitlist Promoted': 'users',
  'Payment Due': 'clock',
  'Payment Received': 'check',
  'Class Cancelled': 'x',
  'Personal Record': 'trophy',
  'Streak Milestone': 'flame',
  'Win Back': 'flame',
  Birthday: 'sparkles',
  General: 'mail',
}

export function NotificationsPage() {
  const [unreadOnly, setUnreadOnly] = useState(false)
  const { data, isLoading } = useNotifications(unreadOnly)
  const markRead = useMarkNotificationRead()
  const markAll = useMarkAllRead()

  return (
    <div>
      <PortalHeading
        eyebrow="Inbox"
        title="Notifications"
        lead="Booking confirmations, waitlist promotions, payment reminders and your own records."
        actions={
          (data?.unread ?? 0) > 0 ? (
            <Button size="sm" variant="outline" loading={markAll.isPending} onClick={() => markAll.mutate()}>
              Mark all read
            </Button>
          ) : undefined
        }
      />

      <div className="mb-5">
        <PillToggle
          ariaLabel="Filter notifications"
          value={unreadOnly ? 'unread' : 'all'}
          onChange={(value) => setUnreadOnly(value === 'unread')}
          options={[
            { value: 'all', label: 'Everything', count: data?.total },
            { value: 'unread', label: 'Unread', count: data?.unread },
          ]}
        />
      </div>

      {isLoading ? (
        <div className="space-y-3" aria-busy="true">
          {Array.from({ length: 5 }).map((_, index) => (
            <Skeleton key={index} className="h-20" />
          ))}
        </div>
      ) : (data?.rows.length ?? 0) === 0 ? (
        <EmptyState
          icon="mail"
          headline={unreadOnly ? 'Nothing unread' : 'Nothing yet'}
          body={
            unreadOnly
              ? 'You are caught up.'
              : 'Book a class or renew your plan and the confirmations will land here.'
          }
          actionLabel="Book a class"
          actionTo="/portal/book"
        />
      ) : (
        <Panel padded={false}>
          <ul className="divide-y divide-[var(--hairline)]">
            {data!.rows.map((note) => {
              const body = (
                <>
                  <span
                    className={cn(
                      'mt-0.5 grid size-9 shrink-0 place-items-center rounded-full border',
                      note.isRead
                        ? 'border-[var(--hairline)] text-smoke'
                        : 'border-[var(--accent-line)] bg-[var(--accent-soft)] text-accent',
                    )}
                  >
                    <Icon name={KIND_ICONS[note.kindName] ?? 'mail'} size={16} />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="flex flex-wrap items-center gap-2">
                      <span className={cn('text-[0.9375rem]', note.isRead ? 'text-smoke' : 'font-medium text-bone')}>
                        {note.title}
                      </span>
                      {!note.isRead && <span className="size-1.5 rounded-full bg-accent" aria-label="Unread" />}
                    </span>
                    <span className="mt-1 block text-[0.875rem] leading-relaxed text-smoke">{note.body}</span>
                    <span className="mt-1.5 block text-[0.6875rem] text-smoke">
                      {note.kindName} · {relativeTime(note.createdAtUtc)}
                    </span>
                  </span>
                  {note.actionUrl?.startsWith('/portal') && (
                    <Icon name="chevron-right" size={16} className="mt-2 shrink-0 text-smoke" />
                  )}
                </>
              )

              const className = cn(
                'flex w-full items-start gap-3.5 px-5 py-4 text-left transition-colors hover:bg-steel/35',
                !note.isRead && 'bg-[color-mix(in_srgb,var(--accent)_3%,transparent)]',
              )

              return (
                <li key={note.id}>
                  {note.actionUrl?.startsWith('/portal') ? (
                    <Link to={note.actionUrl} className={className} onClick={() => !note.isRead && markRead.mutate(note.id)}>
                      {body}
                    </Link>
                  ) : (
                    <button
                      type="button"
                      className={className}
                      onClick={() => !note.isRead && markRead.mutate(note.id)}
                    >
                      {body}
                    </button>
                  )}
                </li>
              )
            })}
          </ul>
        </Panel>
      )}
    </div>
  )
}

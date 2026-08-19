import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { useSiteSettings } from '@/lib/cms'
import { cn } from '@/lib/utils'
import { FilterChip, Hint, PageHeader, Panel, Pill, StatCard, TextAreaField, TextField, Toggle } from '../components/ui'
import { Drawer } from '../components/overlays'
import { relativeTime } from '../lib/format'
import {
  useAdminFeed,
  useAnnounce,
  usePinPost,
  useSetPostVisibility,
  type AdminFeedPost,
} from '../lib/module4-api'

/**
 * The community feed from the owner's side (Module 4.5).
 *
 * Records post themselves — this screen is the moderation and the megaphone. A member's
 * record can be hidden but never deleted: the achievement behind it is theirs, and taking
 * the post down is a display decision, not a rewrite of their history.
 */
export function FeedPage() {
  const { data: settings } = useSiteSettings()
  const [branchId, setBranchId] = useState<number | undefined>()
  const { data, isLoading } = useAdminFeed(branchId)
  const [announcing, setAnnouncing] = useState(false)

  const visibility = useSetPostVisibility()
  const pin = usePinPost()

  return (
    <>
      <PageHeader
        eyebrow="Community"
        title="Feed"
        lead="Personal records and streak milestones post themselves. Announcements are yours."
        actions={
          <Button size="sm" icon="plus" onClick={() => setAnnouncing(true)}>
            Announcement
          </Button>
        }
      >
        <div className="grid gap-3 sm:grid-cols-3">
          <StatCard label="Records this week" value={String(data?.prsThisWeek ?? 0)} sub="Posted automatically" tone="accent" />
          <StatCard label="Hidden" value={String(data?.hidden ?? 0)} sub="Taken off the feed" />
          <StatCard label="On the feed" value={String((data?.posts ?? []).filter((post) => post.isVisible).length)} sub="Visible to members" />
        </div>
      </PageHeader>

      <Panel className="mb-5">
        <div className="flex flex-wrap gap-2">
          <FilterChip active={branchId === undefined} onClick={() => setBranchId(undefined)}>
            All branches
          </FilterChip>
          {(settings?.branches ?? []).map((branch) => (
            <FilterChip key={branch.slug} active={branchId === branch.id} onClick={() => setBranchId(branch.id)}>
              {branch.name.replace('FORGE ', '')}
            </FilterChip>
          ))}
        </div>
      </Panel>

      {isLoading && (
        <div className="space-y-3" aria-busy="true">
          {Array.from({ length: 5 }).map((_, index) => (
            <Skeleton key={index} className="h-24 w-full" />
          ))}
        </div>
      )}

      {!isLoading && (data?.posts.length ?? 0) === 0 && (
        <Panel>
          <div className="py-10 text-center">
            <p className="text-[0.9375rem] font-medium">Nothing on the feed yet</p>
            <p className="measure mx-auto mt-1.5 text-[0.8125rem] text-smoke">
              Records appear here as members log them. Post an announcement to give the feed something to open with.
            </p>
            <Button size="sm" icon="plus" className="mt-5" onClick={() => setAnnouncing(true)}>
              Write an announcement
            </Button>
          </div>
        </Panel>
      )}

      <div className="space-y-3">
        {(data?.posts ?? []).map((post) => (
          <PostRow
            key={post.id}
            post={post}
            onToggleVisible={() => visibility.mutate({ id: post.id, visible: !post.isVisible })}
            onTogglePin={() => pin.mutate({ id: post.id, pinned: !post.isPinned })}
          />
        ))}
      </div>

      <AnnouncementDrawer open={announcing} onClose={() => setAnnouncing(false)} />
    </>
  )
}

function PostRow({
  post,
  onToggleVisible,
  onTogglePin,
}: {
  post: AdminFeedPost
  onToggleVisible: () => void
  onTogglePin: () => void
}) {
  return (
    <article
      className={cn(
        'rounded-[var(--radius-card)] border bg-carbon p-4 transition-opacity duration-200',
        post.isPinned ? 'border-[var(--accent)]/40' : 'border-[var(--hairline)]',
        !post.isVisible && 'opacity-60',
      )}
    >
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0 flex-1">
          <div className="mb-1.5 flex flex-wrap items-center gap-2">
            <Pill tone={post.kind === 'PersonalRecord' ? 'accent' : 'neutral'}>{spaced(post.kind)}</Pill>
            {post.isPinned && <Pill tone="warn">Pinned</Pill>}
            {!post.isVisible && <Pill tone="neutral">Hidden</Pill>}
            {post.consentGiven === false && <Pill tone="warn">Consent withdrawn</Pill>}
          </div>

          <p className="font-medium">{post.title}</p>
          {post.body && <p className="mt-1 text-[0.875rem] text-smoke">{post.body}</p>}

          <p className="mt-2 text-[0.75rem] text-smoke">
            {relativeTime(post.postedAtUtc)}
            {post.branchName && ` · ${post.branchName.replace('FORGE ', '')}`}
            {post.memberId && (
              <>
                {' · '}
                <Link to={`/admin/members/${post.memberId}`} className="hover:text-accent">
                  {post.memberName}
                </Link>
              </>
            )}
            {post.likeCount > 0 && ` · ${post.likeCount} cheers`}
          </p>
        </div>

        <div className="flex shrink-0 gap-1.5">
          <Button size="sm" variant="ghost" onClick={onTogglePin} disabled={!post.isVisible}>
            {post.isPinned ? 'Unpin' : 'Pin'}
          </Button>
          <Button size="sm" variant="ghost" onClick={onToggleVisible}>
            {post.isVisible ? 'Hide' : 'Show'}
          </Button>
        </div>
      </div>

      {/* Consent can be withdrawn after a post exists. The feed already filters those out
          for members; saying so here stops the owner wondering why it is not showing. */}
      {post.consentGiven === false && post.isVisible && (
        <p className="mt-3 flex items-start gap-2 text-[0.8125rem] text-[var(--accent)]">
          <Icon name="lock" size={14} className="mt-0.5 shrink-0" aria-hidden="true" />
          This member has turned leaderboard sharing off, so members do not see this post.
        </p>
      )}
    </article>
  )
}

function AnnouncementDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { data: settings } = useSiteSettings()
  const announce = useAnnounce()
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [branchId, setBranchId] = useState<number | undefined>()
  const [pin, setPin] = useState(true)

  const submit = () =>
    announce.mutate(
      { title: title.trim(), body: body.trim() || undefined, branchId, pin },
      {
        onSuccess: () => {
          setTitle('')
          setBody('')
          onClose()
        },
      },
    )

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Write an announcement"
      description="It appears at the top of the member feed."
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button icon="check" onClick={submit} disabled={announce.isPending || title.trim().length === 0}>
            {announce.isPending ? 'Posting…' : 'Post'}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <TextField
          label="Headline"
          required
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          placeholder="Whitefield closes at 6 PM on Saturday for the platform install"
          hint="Say the thing itself. Members read the headline and nothing else."
        />
        <TextAreaField
          label="Detail"
          rows={4}
          value={body}
          onChange={(event) => setBody(event.target.value)}
          placeholder="Optional. The floor reopens Sunday at the usual time; classes run as scheduled."
        />

        <div className="flex flex-wrap gap-2">
          <FilterChip active={branchId === undefined} onClick={() => setBranchId(undefined)}>
            Every branch
          </FilterChip>
          {(settings?.branches ?? []).map((branch) => (
            <FilterChip key={branch.slug} active={branchId === branch.id} onClick={() => setBranchId(branch.id)}>
              {branch.name.replace('FORGE ', '')}
            </FilterChip>
          ))}
        </div>

        <Toggle label="Pin to the top" checked={pin} onChange={setPin} />

        <Hint icon="sparkles">
          Pinning replaces whatever is currently pinned. A feed with four pinned posts has none.
        </Hint>
      </div>
    </Drawer>
  )
}

function spaced(pascal: string): string {
  return pascal.replace(/(?<!^)([A-Z])/g, ' $1')
}

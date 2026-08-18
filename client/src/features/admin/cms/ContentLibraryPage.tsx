import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { useSiteSettings } from '@/lib/cms'
import {
  faqsResource,
  postsResource,
  testimonialsResource,
  transformationsResource,
  useCollectionMutation,
} from '../lib/admin-api'
import { describeErrorText, formatIsoDate } from '../lib/format'
import type { BlogPostRow, FaqItem, Testimonial, Transformation } from '../lib/types'
import { ConfirmDialog, Drawer, useToast } from '../components/overlays'
import {
  Avatar,
  DataTable,
  FilterChip,
  Hint,
  InlineError,
  PageHeader,
  Panel,
  Pill,
  SelectField,
  StatusPill,
  TextAreaField,
  TextField,
  Toggle,
} from '../components/ui'
import { MediaPicker } from '../components/MediaPicker'

type Tab = 'testimonials' | 'transformations' | 'faqs' | 'posts'

/**
 * The shared content pools several sections draw from. These are rows rather than section
 * content because a testimonial edited once should update every wall that shows it.
 */
export function ContentLibraryPage() {
  const [tab, setTab] = useState<Tab>('testimonials')

  return (
    <>
      <PageHeader
        eyebrow="Website"
        title="Content library"
        lead="Testimonials, transformations, FAQs and the journal — edited once, reused by every section that reads them."
      >
        <div className="flex flex-wrap gap-2">
          {(
            [
              ['testimonials', 'Testimonials'],
              ['transformations', 'Transformations'],
              ['faqs', 'FAQs'],
              ['posts', 'Journal'],
            ] as [Tab, string][]
          ).map(([value, label]) => (
            <FilterChip key={value} active={tab === value} onClick={() => setTab(value)}>
              {label}
            </FilterChip>
          ))}
        </div>
      </PageHeader>

      {tab === 'testimonials' && <TestimonialsTab />}
      {tab === 'transformations' && <TransformationsTab />}
      {tab === 'faqs' && <FaqsTab />}
      {tab === 'posts' && <PostsTab />}
    </>
  )
}

/* ---------------------------------------------------------------- testimonials */

function TestimonialsTab() {
  const toast = useToast()
  const { data, isLoading } = testimonialsResource.useList()
  const mutations = useCollectionMutation(testimonialsResource.path, testimonialsResource.key)
  const { data: settings } = useSiteSettings()

  const [editing, setEditing] = useState<Testimonial | 'new' | null>(null)
  const [deleting, setDeleting] = useState<Testimonial | null>(null)
  const [form, setForm] = useState<Record<string, unknown>>({})
  const [signature, setSignature] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const current = editing === 'new' ? null : editing
  const key = editing === null ? null : editing === 'new' ? 'new' : `t-${current?.id}`
  if (key !== null && signature !== key) {
    setSignature(key)
    setError(null)
    setForm({
      authorName: current?.authorName ?? '',
      authorRole: current?.authorRole ?? '',
      authorPhotoUrl: current?.authorPhotoUrl ?? '',
      quote: current?.quote ?? '',
      rating: String(current?.rating ?? 5),
      branchId: current?.branchId ? String(current.branchId) : '',
      program: current?.program ?? '',
      googleReviewUrl: current?.googleReviewUrl ?? '',
      isFeatured: current?.isFeatured ?? false,
      isVisible: current?.isVisible ?? true,
      displayOrder: String(current?.displayOrder ?? 0),
    })
  }

  async function submit() {
    setError(null)
    const body = {
      ...form,
      rating: Number(form.rating),
      branchId: form.branchId ? Number(form.branchId) : undefined,
      displayOrder: Number(form.displayOrder),
    }
    try {
      if (editing === 'new') await mutations.create.mutateAsync(body)
      else if (current) await mutations.update.mutateAsync({ id: current.id, body })
      toast.success('Testimonial saved')
      setEditing(null)
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <>
      <Panel
        padded={false}
        actions={
          <Button size="sm" icon="plus" onClick={() => setEditing('new')}>
            New testimonial
          </Button>
        }
      >
        <DataTable
          rows={data ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          onRowClick={(row) => setEditing(row)}
          emptyHeadline="No testimonials"
          emptyBody="Add a member quote and it appears on every testimonial wall on the site."
          columns={[
            {
              key: 'author',
              header: 'Member',
              cell: (row) => (
                <div className="flex items-center gap-3">
                  <Avatar src={row.authorPhotoUrl} name={row.authorName} size={32} />
                  <div className="min-w-0">
                    <p className="truncate font-medium">{row.authorName}</p>
                    <p className="truncate text-[0.75rem] text-smoke">{row.authorRole ?? '—'}</p>
                  </div>
                </div>
              ),
            },
            {
              key: 'quote',
              header: 'Quote',
              cell: (row) => <p className="line-clamp-2 max-w-md text-[0.8125rem] text-smoke">{row.quote}</p>,
            },
            {
              key: 'rating',
              header: 'Rating',
              align: 'right',
              cell: (row) => <span className="numeric text-accent">{'★'.repeat(row.rating)}</span>,
            },
            { key: 'branch', header: 'Branch', cell: (row) => <span className="text-smoke">{row.branchName ?? 'All'}</span> },
            {
              key: 'flags',
              header: '',
              align: 'right',
              cell: (row) => (
                <div className="flex justify-end gap-1.5">
                  {row.isFeatured && <Pill tone="accent">featured</Pill>}
                  {!row.isVisible && <Pill tone="muted">hidden</Pill>}
                </div>
              ),
            },
          ]}
        />
      </Panel>

      <Drawer
        open={editing !== null}
        onClose={() => setEditing(null)}
        title={editing === 'new' ? 'New testimonial' : 'Edit testimonial'}
        footer={
          <>
            {current && (
              <Button variant="ghost" size="sm" onClick={() => setDeleting(current)}>
                Delete
              </Button>
            )}
            <div className="flex-1" />
            <Button variant="ghost" size="sm" onClick={() => setEditing(null)}>
              Cancel
            </Button>
            <Button size="sm" icon="check" onClick={() => void submit()} loading={mutations.create.isPending || mutations.update.isPending}>
              Save
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          {error && <InlineError>{error}</InlineError>}
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField label="Name" required value={String(form.authorName ?? '')} onChange={(event) => setForm((c) => ({ ...c, authorName: event.target.value }))} />
            <TextField label="Role or context" placeholder="Member since 2023" value={String(form.authorRole ?? '')} onChange={(event) => setForm((c) => ({ ...c, authorRole: event.target.value }))} />
            <SelectField label="Rating" value={String(form.rating ?? 5)} onChange={(event) => setForm((c) => ({ ...c, rating: event.target.value }))}>
              {[5, 4, 3, 2, 1].map((value) => (
                <option key={value} value={value}>
                  {value} star{value === 1 ? '' : 's'}
                </option>
              ))}
            </SelectField>
            <SelectField label="Branch" value={String(form.branchId ?? '')} onChange={(event) => setForm((c) => ({ ...c, branchId: event.target.value }))}>
              <option value="">All branches</option>
              {(settings?.branches ?? []).map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </SelectField>
          </div>
          <TextAreaField label="Quote" required rows={4} value={String(form.quote ?? '')} onChange={(event) => setForm((c) => ({ ...c, quote: event.target.value }))} />
          <MediaPicker label="Photo" value={String(form.authorPhotoUrl ?? '')} onChange={(next) => setForm((c) => ({ ...c, authorPhotoUrl: next }))} folder="testimonials" />
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField label="Programme" value={String(form.program ?? '')} onChange={(event) => setForm((c) => ({ ...c, program: event.target.value }))} />
            <TextField label="Google review URL" value={String(form.googleReviewUrl ?? '')} onChange={(event) => setForm((c) => ({ ...c, googleReviewUrl: event.target.value }))} />
          </div>
          <div className="space-y-3 rounded-[0.625rem] border border-[var(--hairline)] p-4">
            <Toggle label="Featured" hint="Featured-only walls show just these." checked={Boolean(form.isFeatured)} onChange={(next) => setForm((c) => ({ ...c, isFeatured: next }))} />
            <Toggle label="Visible" checked={Boolean(form.isVisible)} onChange={(next) => setForm((c) => ({ ...c, isVisible: next }))} />
            <TextField label="Display order" type="number" value={String(form.displayOrder ?? 0)} onChange={(event) => setForm((c) => ({ ...c, displayOrder: event.target.value }))} />
          </div>
        </div>
      </Drawer>

      <ConfirmDialog
        open={deleting !== null}
        onClose={() => setDeleting(null)}
        title="Delete this testimonial?"
        body="It disappears from every wall on the site."
        confirmLabel="Delete"
        tone="danger"
        loading={mutations.remove.isPending}
        onConfirm={() => {
          if (!deleting) return
          void mutations.remove.mutateAsync(deleting.id).then(() => {
            toast.success('Deleted')
            setDeleting(null)
            setEditing(null)
          })
        }}
      />
    </>
  )
}

/* ---------------------------------------------------------------- transformations */

function TransformationsTab() {
  const toast = useToast()
  const { data, isLoading } = transformationsResource.useList()
  const mutations = useCollectionMutation(transformationsResource.path, transformationsResource.key)
  const { data: settings } = useSiteSettings()

  const [editing, setEditing] = useState<Transformation | 'new' | null>(null)
  const [form, setForm] = useState<Record<string, unknown>>({})
  const [signature, setSignature] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const current = editing === 'new' ? null : editing
  const key = editing === null ? null : editing === 'new' ? 'new' : `x-${current?.id}`
  if (key !== null && signature !== key) {
    setSignature(key)
    setError(null)
    setForm({
      memberDisplayName: current?.memberDisplayName ?? '',
      beforeImageUrl: current?.beforeImageUrl ?? '',
      afterImageUrl: current?.afterImageUrl ?? '',
      durationWeeks: String(current?.durationWeeks ?? 12),
      program: current?.program ?? '',
      trainerName: current?.trainerName ?? '',
      weightBeforeKg: current?.weightBeforeKg ?? '',
      weightAfterKg: current?.weightAfterKg ?? '',
      story: current?.story ?? '',
      branchId: current?.branchId ? String(current.branchId) : '',
      consentGiven: current?.consentGiven ?? false,
      isVisible: current?.isVisible ?? true,
      displayOrder: String(current?.displayOrder ?? 0),
    })
  }

  async function submit() {
    setError(null)
    const body = {
      ...form,
      durationWeeks: Number(form.durationWeeks),
      weightBeforeKg: form.weightBeforeKg === '' ? undefined : Number(form.weightBeforeKg),
      weightAfterKg: form.weightAfterKg === '' ? undefined : Number(form.weightAfterKg),
      branchId: form.branchId ? Number(form.branchId) : undefined,
      displayOrder: Number(form.displayOrder),
    }
    try {
      if (editing === 'new') await mutations.create.mutateAsync(body)
      else if (current) await mutations.update.mutateAsync({ id: current.id, body })
      toast.success('Transformation saved')
      setEditing(null)
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <>
      <Panel
        padded={false}
        actions={
          <Button size="sm" icon="plus" onClick={() => setEditing('new')}>
            New transformation
          </Button>
        }
      >
        <DataTable
          rows={data ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          onRowClick={(row) => setEditing(row)}
          emptyHeadline="No transformations"
          emptyBody="Before/after pairs need written consent from the member before they can be published."
          columns={[
            {
              key: 'member',
              header: 'Member',
              cell: (row) => (
                <div className="flex items-center gap-3">
                  <div className="flex shrink-0 gap-0.5">
                    <img src={row.beforeImageUrl} alt="" className="graded size-9 rounded-l-[0.375rem] object-cover" loading="lazy" />
                    <img src={row.afterImageUrl} alt="" className="graded size-9 rounded-r-[0.375rem] object-cover" loading="lazy" />
                  </div>
                  <div className="min-w-0">
                    <p className="truncate font-medium">{row.memberDisplayName}</p>
                    <p className="truncate text-[0.75rem] text-smoke">{row.program}</p>
                  </div>
                </div>
              ),
            },
            { key: 'weeks', header: 'Duration', align: 'right', cell: (row) => <span className="numeric">{row.durationWeeks}w</span> },
            {
              key: 'weight',
              header: 'Change',
              align: 'right',
              cell: (row) =>
                row.weightBeforeKg && row.weightAfterKg ? (
                  <span className="numeric">
                    {row.weightBeforeKg} → {row.weightAfterKg} kg
                  </span>
                ) : (
                  <span className="text-smoke">—</span>
                ),
            },
            {
              key: 'consent',
              header: 'Consent',
              cell: (row) =>
                row.consentGiven ? (
                  <Pill tone="success">{row.consentAtUtc ? formatIsoDate(row.consentAtUtc.slice(0, 10)) : 'given'}</Pill>
                ) : (
                  <Pill tone="danger">missing</Pill>
                ),
            },
            {
              key: 'flags',
              header: '',
              align: 'right',
              cell: (row) => (!row.isVisible ? <Pill tone="muted">hidden</Pill> : null),
            },
          ]}
        />
      </Panel>

      <Drawer
        open={editing !== null}
        onClose={() => setEditing(null)}
        title={editing === 'new' ? 'New transformation' : 'Edit transformation'}
        description="This publishes a named person with their weight and timeline. Consent is a hard gate, and it is timestamped."
        width="lg"
        footer={
          <>
            <Button variant="ghost" size="sm" onClick={() => setEditing(null)}>
              Cancel
            </Button>
            <Button size="sm" icon="check" onClick={() => void submit()} loading={mutations.create.isPending || mutations.update.isPending}>
              Save
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          {error && <InlineError>{error}</InlineError>}
          {!form.consentGiven && (
            <Hint icon="lock">
              Without consent this row stays out of the public gallery no matter what the visibility toggle says.
            </Hint>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <TextField label="Display name" required value={String(form.memberDisplayName ?? '')} onChange={(event) => setForm((c) => ({ ...c, memberDisplayName: event.target.value }))} />
            <TextField label="Programme" required value={String(form.program ?? '')} onChange={(event) => setForm((c) => ({ ...c, program: event.target.value }))} />
            <TextField label="Duration (weeks)" type="number" value={String(form.durationWeeks ?? '')} onChange={(event) => setForm((c) => ({ ...c, durationWeeks: event.target.value }))} />
            <TextField label="Coach" value={String(form.trainerName ?? '')} onChange={(event) => setForm((c) => ({ ...c, trainerName: event.target.value }))} />
            <TextField label="Weight before (kg)" type="number" value={String(form.weightBeforeKg ?? '')} onChange={(event) => setForm((c) => ({ ...c, weightBeforeKg: event.target.value }))} />
            <TextField label="Weight after (kg)" type="number" value={String(form.weightAfterKg ?? '')} onChange={(event) => setForm((c) => ({ ...c, weightAfterKg: event.target.value }))} />
          </div>

          <div className="grid gap-5 sm:grid-cols-2">
            <MediaPicker label="Before photo" value={String(form.beforeImageUrl ?? '')} onChange={(next) => setForm((c) => ({ ...c, beforeImageUrl: next }))} folder="transformations" />
            <MediaPicker label="After photo" value={String(form.afterImageUrl ?? '')} onChange={(next) => setForm((c) => ({ ...c, afterImageUrl: next }))} folder="transformations" />
          </div>

          <TextAreaField label="Story" rows={4} value={String(form.story ?? '')} onChange={(event) => setForm((c) => ({ ...c, story: event.target.value }))} />

          <SelectField label="Branch" value={String(form.branchId ?? '')} onChange={(event) => setForm((c) => ({ ...c, branchId: event.target.value }))}>
            <option value="">All branches</option>
            {(settings?.branches ?? []).map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </SelectField>

          <div className="space-y-3 rounded-[0.625rem] border border-[var(--hairline)] p-4">
            <Toggle
              label="Written consent on file"
              hint="Switching this on records the date and time. Do not tick it without the signed form."
              checked={Boolean(form.consentGiven)}
              onChange={(next) => setForm((c) => ({ ...c, consentGiven: next }))}
            />
            <Toggle label="Visible" checked={Boolean(form.isVisible)} onChange={(next) => setForm((c) => ({ ...c, isVisible: next }))} />
            <TextField label="Display order" type="number" value={String(form.displayOrder ?? 0)} onChange={(event) => setForm((c) => ({ ...c, displayOrder: event.target.value }))} />
          </div>
        </div>
      </Drawer>
    </>
  )
}

/* ---------------------------------------------------------------- faqs */

function FaqsTab() {
  const toast = useToast()
  const { data, isLoading } = faqsResource.useList()
  const mutations = useCollectionMutation(faqsResource.path, faqsResource.key)

  const [editing, setEditing] = useState<FaqItem | 'new' | null>(null)
  const [form, setForm] = useState<Record<string, unknown>>({})
  const [signature, setSignature] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const current = editing === 'new' ? null : editing
  const key = editing === null ? null : editing === 'new' ? 'new' : `f-${current?.id}`
  if (key !== null && signature !== key) {
    setSignature(key)
    setError(null)
    setForm({
      question: current?.question ?? '',
      answer: current?.answer ?? '',
      category: current?.category ?? 'General',
      isVisible: current?.isVisible ?? true,
      displayOrder: String(current?.displayOrder ?? 0),
    })
  }

  async function submit() {
    setError(null)
    try {
      const body = { ...form, displayOrder: Number(form.displayOrder) }
      if (editing === 'new') await mutations.create.mutateAsync(body)
      else if (current) await mutations.update.mutateAsync({ id: current.id, body })
      toast.success('FAQ saved')
      setEditing(null)
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <>
      <Panel
        padded={false}
        actions={
          <Button size="sm" icon="plus" onClick={() => setEditing('new')}>
            New question
          </Button>
        }
      >
        <DataTable
          rows={data ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          onRowClick={(row) => setEditing(row)}
          emptyHeadline="No FAQs"
          columns={[
            { key: 'category', header: 'Category', cell: (row) => <Pill tone="muted">{row.category}</Pill> },
            {
              key: 'question',
              header: 'Question',
              cell: (row) => (
                <div className="min-w-0">
                  <p className="truncate font-medium">{row.question}</p>
                  <p className="line-clamp-1 text-[0.75rem] text-smoke">{row.answer}</p>
                </div>
              ),
            },
            { key: 'order', header: 'Order', align: 'right', cell: (row) => <span className="numeric text-smoke">{row.displayOrder}</span> },
            { key: 'flags', header: '', align: 'right', cell: (row) => (!row.isVisible ? <Pill tone="muted">hidden</Pill> : null) },
          ]}
        />
      </Panel>

      <Drawer
        open={editing !== null}
        onClose={() => setEditing(null)}
        title={editing === 'new' ? 'New question' : 'Edit question'}
        footer={
          <>
            <Button variant="ghost" size="sm" onClick={() => setEditing(null)}>
              Cancel
            </Button>
            <Button size="sm" icon="check" onClick={() => void submit()} loading={mutations.create.isPending || mutations.update.isPending}>
              Save
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          {error && <InlineError>{error}</InlineError>}
          <TextField label="Question" required value={String(form.question ?? '')} onChange={(event) => setForm((c) => ({ ...c, question: event.target.value }))} />
          <TextAreaField label="Answer" required rows={5} value={String(form.answer ?? '')} onChange={(event) => setForm((c) => ({ ...c, answer: event.target.value }))} />
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField label="Category" hint="Groups the accordion on the FAQ page." value={String(form.category ?? '')} onChange={(event) => setForm((c) => ({ ...c, category: event.target.value }))} />
            <TextField label="Display order" type="number" value={String(form.displayOrder ?? 0)} onChange={(event) => setForm((c) => ({ ...c, displayOrder: event.target.value }))} />
          </div>
          <Toggle label="Visible" checked={Boolean(form.isVisible)} onChange={(next) => setForm((c) => ({ ...c, isVisible: next }))} />
        </div>
      </Drawer>
    </>
  )
}

/* ---------------------------------------------------------------- journal */

function PostsTab() {
  const toast = useToast()
  const { data, isLoading } = postsResource.useList()
  const mutations = useCollectionMutation(postsResource.path, postsResource.key)

  const [editing, setEditing] = useState<BlogPostRow | 'new' | null>(null)
  const [form, setForm] = useState<Record<string, unknown>>({})
  const [signature, setSignature] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const current = editing === 'new' ? null : editing
  const key = editing === null ? null : editing === 'new' ? 'new' : `p-${current?.id}`
  if (key !== null && signature !== key) {
    setSignature(key)
    setError(null)
    setForm({
      slug: current?.slug ?? '',
      title: current?.title ?? '',
      excerpt: current?.excerpt ?? '',
      coverImageUrl: current?.coverImageUrl ?? '',
      authorName: current?.authorName ?? '',
      authorRole: current?.authorRole ?? '',
      tags: current?.tags ?? '',
      readMinutes: String(current?.readMinutes ?? 5),
      state: String(current?.state ?? 0),
      isFeatured: current?.isFeatured ?? false,
      bodyText: '',
    })
  }

  async function submit() {
    setError(null)
    // Body is edited as plain paragraphs and stored as structured blocks — never raw HTML,
    // so the article typography stays on-system whatever the owner pastes in.
    const paragraphs = String(form.bodyText ?? '')
      .split('\n\n')
      .map((block) => block.trim())
      .filter(Boolean)
      .map((text) => ({ type: 'paragraph', text }))

    const body = {
      slug: String(form.slug || form.title).toLowerCase().replace(/[^a-z0-9]+/g, '-'),
      title: String(form.title),
      excerpt: String(form.excerpt),
      body: paragraphs.length > 0 ? paragraphs : undefined,
      coverImageUrl: form.coverImageUrl || undefined,
      authorName: String(form.authorName),
      authorRole: form.authorRole || undefined,
      tags: form.tags || undefined,
      readMinutes: Number(form.readMinutes),
      state: Number(form.state),
      isFeatured: Boolean(form.isFeatured),
    }

    try {
      if (editing === 'new') await mutations.create.mutateAsync(body)
      else if (current) await mutations.update.mutateAsync({ id: current.id, body })
      toast.success('Post saved')
      setEditing(null)
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <>
      <Panel
        padded={false}
        actions={
          <Button size="sm" icon="plus" onClick={() => setEditing('new')}>
            New post
          </Button>
        }
      >
        <DataTable
          rows={data ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          onRowClick={(row) => setEditing(row)}
          emptyHeadline="Nothing in the journal"
          columns={[
            {
              key: 'title',
              header: 'Post',
              cell: (row) => (
                <div className="flex items-center gap-3">
                  {row.coverImageUrl && (
                    <img src={row.coverImageUrl} alt="" className="graded size-10 rounded-[0.5rem] object-cover" loading="lazy" />
                  )}
                  <div className="min-w-0">
                    <p className="truncate font-medium">{row.title}</p>
                    <p className="truncate text-[0.75rem] text-smoke">/journal/{row.slug}</p>
                  </div>
                </div>
              ),
            },
            { key: 'author', header: 'Author', cell: (row) => <span className="text-smoke">{row.authorName}</span> },
            { key: 'read', header: 'Read', align: 'right', cell: (row) => <span className="numeric text-smoke">{row.readMinutes} min</span> },
            { key: 'state', header: 'State', cell: (row) => <StatusPill status={row.state === 1 ? 'Published' : 'Draft'} /> },
            {
              key: 'published',
              header: 'Published',
              align: 'right',
              cell: (row) => (
                <span className="numeric text-[0.8125rem] text-smoke">
                  {row.publishedAtUtc ? formatIsoDate(row.publishedAtUtc.slice(0, 10)) : '—'}
                </span>
              ),
            },
          ]}
        />
      </Panel>

      <Drawer
        open={editing !== null}
        onClose={() => setEditing(null)}
        title={editing === 'new' ? 'New journal post' : 'Edit post'}
        description="Body copy is stored as structured blocks, not HTML, so the article typography stays on-system."
        width="lg"
        footer={
          <>
            <Button variant="ghost" size="sm" onClick={() => setEditing(null)}>
              Cancel
            </Button>
            <Button size="sm" icon="check" onClick={() => void submit()} loading={mutations.create.isPending || mutations.update.isPending}>
              Save
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          {error && <InlineError>{error}</InlineError>}
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField label="Title" required value={String(form.title ?? '')} onChange={(event) => setForm((c) => ({ ...c, title: event.target.value }))} />
            <TextField label="Slug" value={String(form.slug ?? '')} onChange={(event) => setForm((c) => ({ ...c, slug: event.target.value }))} />
            <TextField label="Author" required value={String(form.authorName ?? '')} onChange={(event) => setForm((c) => ({ ...c, authorName: event.target.value }))} />
            <TextField label="Author role" value={String(form.authorRole ?? '')} onChange={(event) => setForm((c) => ({ ...c, authorRole: event.target.value }))} />
            <TextField label="Tags" hint="Comma separated." value={String(form.tags ?? '')} onChange={(event) => setForm((c) => ({ ...c, tags: event.target.value }))} />
            <TextField label="Read time (minutes)" type="number" value={String(form.readMinutes ?? 5)} onChange={(event) => setForm((c) => ({ ...c, readMinutes: event.target.value }))} />
          </div>

          <TextAreaField label="Excerpt" required rows={3} value={String(form.excerpt ?? '')} onChange={(event) => setForm((c) => ({ ...c, excerpt: event.target.value }))} />
          <MediaPicker label="Cover image" value={String(form.coverImageUrl ?? '')} onChange={(next) => setForm((c) => ({ ...c, coverImageUrl: next }))} folder="journal" />
          <TextAreaField
            label="Body"
            rows={12}
            hint="One paragraph per blank line. Existing body copy is preserved unless you type here."
            value={String(form.bodyText ?? '')}
            onChange={(event) => setForm((c) => ({ ...c, bodyText: event.target.value }))}
          />

          <div className="space-y-3 rounded-[0.625rem] border border-[var(--hairline)] p-4">
            <SelectField label="State" value={String(form.state ?? 0)} onChange={(event) => setForm((c) => ({ ...c, state: event.target.value }))}>
              <option value="0">Draft</option>
              <option value="1">Published</option>
            </SelectField>
            <Toggle label="Featured" checked={Boolean(form.isFeatured)} onChange={(next) => setForm((c) => ({ ...c, isFeatured: next }))} />
          </div>
        </div>
      </Drawer>
    </>
  )
}

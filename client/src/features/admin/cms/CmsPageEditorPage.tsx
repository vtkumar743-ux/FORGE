import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { useSiteSettings } from '@/lib/cms'
import { cn } from '@/lib/utils'
import { useCmsPage, usePublishPage, useSavePageSeo, useSectionAction } from '../lib/admin-api'
import { describeErrorText, relativeTime } from '../lib/format'
import type { AdminSection } from '../lib/types'
import { ConfirmDialog, Drawer, useToast } from '../components/overlays'
import {
  Hint,
  InlineError,
  PageHeader,
  Panel,
  Pill,
  SelectField,
  TextAreaField,
  TextField,
  Toggle,
} from '../components/ui'
import { SectionEditor, defaultContentFor } from './SectionEditor'
import { sectionDescriptions, sectionTypeNames, sectionTypeOrdinals } from './section-schemas'
import { humanise } from './zod-fields'

/**
 * One page's section stack: reorder, hide, edit, publish.
 *
 * Reordering is drag-and-drop with keyboard arrows alongside it — a list an owner has to
 * mouse-drag is a list a keyboard user cannot reorder at all. The order is committed to the
 * API on drop, so what is on screen is always what the site renders.
 */
export function CmsPageEditorPage() {
  const { id } = useParams()
  const pageId = Number(id)
  const toast = useToast()
  const { data: page, isLoading } = useCmsPage(Number.isFinite(pageId) ? pageId : null)
  const sectionActions = useSectionAction()
  const publishPage = usePublishPage()

  const [order, setOrder] = useState<AdminSection[]>([])
  const [dragIndex, setDragIndex] = useState<number | null>(null)
  const [editing, setEditing] = useState<AdminSection | null>(null)
  const [addOpen, setAddOpen] = useState(false)
  const [seoOpen, setSeoOpen] = useState(false)
  const [deleting, setDeleting] = useState<AdminSection | null>(null)

  // Local order so a drag feels instant; the API call follows and re-syncs on success.
  useEffect(() => {
    if (page) setOrder([...page.sections].sort((a, b) => a.orderIndex - b.orderIndex))
  }, [page])

  if (isLoading || !page) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-9 w-72" />
        {Array.from({ length: 6 }).map((_, index) => (
          <Skeleton key={index} className="h-20 w-full" />
        ))}
      </div>
    )
  }

  const publicHref = `/${page.slug === 'home' ? '' : page.slug}`
  const pendingDrafts = order.filter((section) => section.hasDraft).length

  function commitOrder(next: AdminSection[]) {
    setOrder(next)
    void sectionActions.reorder
      .mutateAsync({ pageId, sectionIds: next.map((section) => section.id) })
      .catch((error) => toast.error('Could not save the order', describeErrorText(error)))
  }

  function move(index: number, direction: -1 | 1) {
    const target = index + direction
    if (target < 0 || target >= order.length) return
    const next = [...order]
    ;[next[index], next[target]] = [next[target], next[index]]
    commitOrder(next)
  }

  return (
    <>
      <PageHeader
        eyebrow={
          <>
            <Link to="/admin/cms" className="hover:text-accent">
              Pages
            </Link>{' '}
            / {page.slug}
          </>
        }
        title={page.title}
        lead={`${order.length} section${order.length === 1 ? '' : 's'} · rendered in this order at ${publicHref}`}
        actions={
          <>
            <a
              href={publicHref}
              target="_blank"
              rel="noreferrer noopener"
              className="inline-flex h-9 items-center gap-2 rounded-full border border-[var(--hairline-strong)] px-4 text-[0.8125rem] font-medium text-bone transition-colors hover:border-[var(--accent-line)] hover:text-accent"
            >
              <Icon name="arrow-up-right" size={15} />
              Preview
            </a>
            <Button variant="ghost" size="sm" onClick={() => setSeoOpen(true)}>
              SEO
            </Button>
            <Button variant="outline" size="sm" icon="plus" onClick={() => setAddOpen(true)}>
              Add section
            </Button>
            <Button
              size="sm"
              icon="check"
              loading={publishPage.isPending}
              onClick={() =>
                void publishPage
                  .mutateAsync(pageId)
                  .then(() => toast.success('Page published', 'Every pending draft on this page is now live.'))
                  .catch((error) => toast.error('Could not publish', describeErrorText(error)))
              }
            >
              Publish page
            </Button>
          </>
        }
      />

      {pendingDrafts > 0 && (
        <div className="mb-5">
          <Hint icon="clock">
            {pendingDrafts} section{pendingDrafts === 1 ? '' : 's'} on this page {pendingDrafts === 1 ? 'has' : 'have'}{' '}
            unpublished changes. Publish the page to push them all at once, or publish a section on its own from
            its editor.
          </Hint>
        </div>
      )}

      <ol className="space-y-2">
        {order.map((section, index) => (
          <li
            key={section.id}
            draggable
            onDragStart={() => setDragIndex(index)}
            onDragEnd={() => setDragIndex(null)}
            onDragOver={(event) => event.preventDefault()}
            onDrop={(event) => {
              event.preventDefault()
              if (dragIndex === null || dragIndex === index) return
              const next = [...order]
              const [moved] = next.splice(dragIndex, 1)
              next.splice(index, 0, moved)
              commitOrder(next)
              setDragIndex(null)
            }}
            className={cn(
              'group flex items-center gap-3 rounded-[var(--radius-card)] border bg-carbon px-4 py-3.5',
              'transition-[border-color,opacity] duration-200',
              dragIndex === index && 'opacity-40',
              section.isVisible ? 'border-[var(--hairline)]' : 'border-dashed border-[var(--hairline)]',
            )}
          >
            <span
              className="cursor-grab text-smoke transition-colors hover:text-bone active:cursor-grabbing"
              aria-hidden
            >
              <Icon name="menu" size={16} />
            </span>

            <span className="numeric w-6 shrink-0 text-[0.75rem] text-smoke">{index + 1}</span>

            <button
              type="button"
              onClick={() => setEditing(section)}
              className="min-w-0 flex-1 text-left"
            >
              <div className="flex flex-wrap items-center gap-2">
                <span className={cn('font-medium', !section.isVisible && 'text-smoke')}>{section.adminLabel}</span>
                <Pill tone="muted">{humanise(section.typeName)}</Pill>
                {section.hasDraft && <Pill tone="warn">draft</Pill>}
                {!section.isVisible && <Pill tone="muted">hidden</Pill>}
                {section.branchName && <Pill tone="accent">{section.branchName.replace('FORGE ', '')} only</Pill>}
              </div>
              <p className="mt-1 truncate text-[0.75rem] text-smoke">
                #{section.key}
                {section.publishedAtUtc ? ` · published ${relativeTime(section.publishedAtUtc)}` : ' · never published'}
              </p>
            </button>

            <div className="flex shrink-0 items-center gap-0.5">
              <button
                type="button"
                onClick={() => move(index, -1)}
                disabled={index === 0}
                className="rounded-full p-1.5 text-smoke transition-colors hover:text-bone disabled:opacity-30"
                aria-label={`Move ${section.adminLabel} up`}
              >
                <Icon name="chevron-left" size={15} className="rotate-90" />
              </button>
              <button
                type="button"
                onClick={() => move(index, 1)}
                disabled={index === order.length - 1}
                className="rounded-full p-1.5 text-smoke transition-colors hover:text-bone disabled:opacity-30"
                aria-label={`Move ${section.adminLabel} down`}
              >
                <Icon name="chevron-right" size={15} className="rotate-90" />
              </button>
              <button
                type="button"
                onClick={() =>
                  void sectionActions.setVisibility.mutateAsync({
                    pageId,
                    sectionId: section.id,
                    visible: !section.isVisible,
                  })
                }
                className={cn(
                  'rounded-full p-1.5 transition-colors',
                  section.isVisible ? 'text-accent hover:text-bone' : 'text-smoke hover:text-accent',
                )}
                aria-label={section.isVisible ? `Hide ${section.adminLabel}` : `Show ${section.adminLabel}`}
              >
                <Icon name={section.isVisible ? 'check' : 'minus'} size={15} />
              </button>
              <button
                type="button"
                onClick={() => void sectionActions.duplicate.mutateAsync({ pageId, sectionId: section.id })}
                className="rounded-full p-1.5 text-smoke transition-colors hover:text-bone"
                aria-label={`Duplicate ${section.adminLabel}`}
              >
                <Icon name="plus" size={15} />
              </button>
              <button
                type="button"
                onClick={() => setDeleting(section)}
                className="rounded-full p-1.5 text-smoke transition-colors hover:text-accent-hot"
                aria-label={`Delete ${section.adminLabel}`}
              >
                <Icon name="x" size={15} />
              </button>
            </div>
          </li>
        ))}
      </ol>

      {order.length === 0 && (
        <Panel>
          <div className="py-12 text-center">
            <p className="display-m text-[1.25rem]">This page is empty</p>
            <p className="measure mx-auto mt-2 text-[0.875rem] leading-relaxed text-smoke">
              Add a section and it renders on the public route immediately once you make it visible.
            </p>
            <Button size="sm" icon="plus" className="mt-5" onClick={() => setAddOpen(true)}>
              Add the first section
            </Button>
          </div>
        </Panel>
      )}

      <SectionEditor
        pageId={pageId}
        section={editing}
        open={editing !== null}
        onClose={() => setEditing(null)}
        previewHref={publicHref}
      />

      <AddSectionDrawer open={addOpen} onClose={() => setAddOpen(false)} pageId={pageId} nextIndex={order.length + 1} />

      <SeoDrawer open={seoOpen} onClose={() => setSeoOpen(false)} page={page} />

      <ConfirmDialog
        open={deleting !== null}
        onClose={() => setDeleting(null)}
        title={`Delete "${deleting?.adminLabel}"?`}
        body="The section and its content are removed from this page. This cannot be undone — hide it instead if you might want it back."
        confirmLabel="Delete section"
        tone="danger"
        loading={sectionActions.remove.isPending}
        onConfirm={() => {
          if (!deleting) return
          void sectionActions.remove.mutateAsync({ pageId, sectionId: deleting.id }).then(() => {
            toast.success('Section deleted')
            setDeleting(null)
          })
        }}
      />
    </>
  )
}

/* ---------------------------------------------------------------- add */

function AddSectionDrawer({
  open,
  onClose,
  pageId,
  nextIndex,
}: {
  open: boolean
  onClose: () => void
  pageId: number
  nextIndex: number
}) {
  const toast = useToast()
  const { data: settings } = useSiteSettings()
  const actions = useSectionAction()

  const [type, setType] = useState<string | null>(null)
  const [key, setKey] = useState('')
  const [label, setLabel] = useState('')
  const [branchId, setBranchId] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function submit() {
    if (!type) return setError('Pick a section type.')
    setError(null)

    try {
      await actions.create.mutateAsync({
        pageId,
        sectionType: sectionTypeOrdinals[type],
        key: key || `${type.toLowerCase()}-${nextIndex}`,
        adminLabel: label || humanise(type),
        content: defaultContentFor(type),
        branchId: branchId ? Number(branchId) : undefined,
      })
      toast.success('Section added', 'It starts hidden — fill it in, then make it visible.')
      setType(null)
      setKey('')
      setLabel('')
      onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Add a section"
      description="New sections start hidden and unpublished, so nothing half-written is ever live."
      width="lg"
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button size="sm" icon="plus" onClick={() => void submit()} loading={actions.create.isPending} disabled={!type}>
            Add section
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {error && <InlineError>{error}</InlineError>}

        <div className="grid gap-2 sm:grid-cols-2">
          {sectionTypeNames.map((name) => (
            <button
              key={name}
              type="button"
              onClick={() => {
                setType(name)
                setLabel(humanise(name))
              }}
              className={cn(
                'rounded-[0.625rem] border p-3.5 text-left transition-[border-color,transform] duration-200',
                'hover:-translate-y-0.5 hover:border-[var(--accent-line)]',
                type === name ? 'border-accent bg-[var(--accent-soft)]' : 'border-[var(--hairline)]',
              )}
            >
              <p className="text-[0.875rem] font-medium">{humanise(name)}</p>
              <p className="mt-1 text-[0.75rem] leading-relaxed text-smoke">{sectionDescriptions[name]}</p>
            </button>
          ))}
        </div>

        {type && (
          <div className="grid gap-4 border-t border-[var(--hairline)] pt-5 sm:grid-cols-2">
            <TextField
              label="Admin label"
              hint="What this section is called in the list. Visitors never see it."
              value={label}
              onChange={(event) => setLabel(event.target.value)}
            />
            <TextField
              label="Key"
              hint="Stable handle used by deep links, e.g. #amenities."
              placeholder={`${type.toLowerCase()}-${nextIndex}`}
              value={key}
              onChange={(event) => setKey(event.target.value)}
            />
            <SelectField
              label="Branch variant"
              hint="Shows only on that branch's page. Leave blank for every page this section is on."
              value={branchId}
              onChange={(event) => setBranchId(event.target.value)}
              className="sm:col-span-2"
            >
              <option value="">Shown everywhere</option>
              {(settings?.branches ?? []).map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name} only
                </option>
              ))}
            </SelectField>
          </div>
        )}
      </div>
    </Drawer>
  )
}

/* ---------------------------------------------------------------- seo */

function SeoDrawer({
  open,
  onClose,
  page,
}: {
  open: boolean
  onClose: () => void
  page: { id: number; slug: string; title: string; description?: string | null; seo: Record<string, unknown>; state: number; isSystemPage: boolean; displayOrder: number }
}) {
  const toast = useToast()
  const save = useSavePageSeo()
  const [form, setForm] = useState<Record<string, unknown>>({})
  const [signature, setSignature] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)

  if (signature !== page.id) {
    setSignature(page.id)
    setForm({
      slug: page.slug,
      title: page.title,
      description: page.description ?? '',
      seoTitle: page.seo.title ?? '',
      seoDescription: page.seo.description ?? '',
      seoKeywords: page.seo.keywords ?? '',
      ogImageUrl: page.seo.ogImageUrl ?? '',
      canonicalUrl: page.seo.canonicalUrl ?? '',
      noIndex: Boolean(page.seo.noIndex),
      state: page.state,
      displayOrder: page.displayOrder,
    })
  }

  function set(field: string, value: unknown) {
    setForm((current) => ({ ...current, [field]: value }))
  }

  async function submit() {
    setError(null)
    try {
      await save.mutateAsync({
        id: page.id,
        body: {
          slug: String(form.slug),
          title: String(form.title),
          description: form.description || undefined,
          seoTitle: String(form.seoTitle),
          seoDescription: String(form.seoDescription),
          seoKeywords: form.seoKeywords || undefined,
          ogImageUrl: form.ogImageUrl || undefined,
          canonicalUrl: form.canonicalUrl || undefined,
          noIndex: Boolean(form.noIndex),
          structuredData: page.seo.structuredData ?? undefined,
          state: Number(form.state),
          displayOrder: Number(form.displayOrder),
        },
      })
      toast.success('Page settings saved')
      onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  const titleLength = String(form.seoTitle ?? '').length
  const descriptionLength = String(form.seoDescription ?? '').length

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Page settings & SEO"
      description="The sitemap is generated from these rows, so a page added or unpublished here enters or leaves search without a deploy."
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button size="sm" icon="check" onClick={() => void submit()} loading={save.isPending}>
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {error && <InlineError>{error}</InlineError>}

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField label="Page title" value={String(form.title ?? '')} onChange={(event) => set('title', event.target.value)} />
          <TextField
            label="Slug"
            hint={page.isSystemPage ? 'System routes are wired into the router and cannot be renamed.' : 'The URL path.'}
            disabled={page.isSystemPage}
            value={String(form.slug ?? '')}
            onChange={(event) => set('slug', event.target.value)}
          />
        </div>

        <TextAreaField
          label="Internal description"
          rows={2}
          hint="A note for whoever edits this page next. Never rendered."
          value={String(form.description ?? '')}
          onChange={(event) => set('description', event.target.value)}
        />

        <div className="space-y-4 rounded-[0.625rem] border border-[var(--hairline)] p-4">
          <TextField
            label="SEO title"
            hint={`${titleLength} characters — search results cut off around 60.`}
            error={titleLength > 65 ? 'Too long; this will be truncated in results.' : undefined}
            value={String(form.seoTitle ?? '')}
            onChange={(event) => set('seoTitle', event.target.value)}
          />
          <TextAreaField
            label="Meta description"
            rows={3}
            hint={`${descriptionLength} characters — aim for 140 to 160.`}
            error={descriptionLength > 165 ? 'Too long; this will be truncated in results.' : undefined}
            value={String(form.seoDescription ?? '')}
            onChange={(event) => set('seoDescription', event.target.value)}
          />
          <TextField label="Keywords" value={String(form.seoKeywords ?? '')} onChange={(event) => set('seoKeywords', event.target.value)} />
          <TextField
            label="OG image URL"
            hint="1200×630 for link previews."
            value={String(form.ogImageUrl ?? '')}
            onChange={(event) => set('ogImageUrl', event.target.value)}
          />
          <TextField label="Canonical URL" value={String(form.canonicalUrl ?? '')} onChange={(event) => set('canonicalUrl', event.target.value)} />
          <Toggle
            label="Hide from search engines"
            hint="Adds noindex and drops the page from the sitemap."
            checked={Boolean(form.noIndex)}
            onChange={(next) => set('noIndex', next)}
          />
        </div>

        <SelectField label="State" value={String(form.state ?? 1)} onChange={(event) => set('state', event.target.value)}>
          <option value="1">Published</option>
          <option value="0">Draft — admins only</option>
        </SelectField>
      </div>
    </Drawer>
  )
}

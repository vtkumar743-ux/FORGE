import { Link } from 'react-router-dom'
import { Icon } from '@/components/ui/Icon'
import { useCmsPages } from '../lib/admin-api'
import { relativeTime } from '../lib/format'
import { DataTable, Hint, PageHeader, Panel, Pill, StatusPill } from '../components/ui'

/**
 * Every public route the owner can edit. Section counts and the draft badge are on the list
 * itself, because "which page has unpublished work on it" is the question this screen is
 * really being asked.
 */
export function CmsPagesPage() {
  const { data, isLoading } = useCmsPages()

  const withDrafts = (data ?? []).filter((page) => page.draftSectionCount > 0).length

  return (
    <>
      <PageHeader
        eyebrow="Website"
        title="Pages"
        lead="Every public route renders from these rows. Editing here changes the live site — there is no deploy step."
      />

      {withDrafts > 0 && (
        <div className="mb-5">
          <Hint icon="clock">
            {withDrafts} page{withDrafts === 1 ? ' has' : 's have'} unpublished changes. Visitors still see the last
            published version until you publish them.
          </Hint>
        </div>
      )}

      <Panel padded={false}>
        <DataTable
          rows={data ?? []}
          rowKey={(row) => row.id}
          loading={isLoading}
          emptyHeadline="No pages"
          columns={[
            {
              key: 'title',
              header: 'Page',
              cell: (row) => (
                <Link to={`/admin/cms/pages/${row.id}`} className="group flex items-center gap-3">
                  <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-[0.5rem] border border-[var(--hairline)] text-smoke transition-colors group-hover:border-[var(--accent-line)] group-hover:text-accent">
                    <Icon name="sparkles" size={15} />
                  </span>
                  <div className="min-w-0">
                    <p className="truncate font-medium">{row.title}</p>
                    <p className="truncate text-[0.75rem] text-smoke">/{row.slug === 'home' ? '' : row.slug}</p>
                  </div>
                </Link>
              ),
            },
            {
              key: 'sections',
              header: 'Sections',
              align: 'right',
              cell: (row) => (
                <span className="numeric">
                  {row.sectionCount}
                  {row.hiddenSectionCount > 0 && (
                    <span className="text-smoke"> · {row.hiddenSectionCount} hidden</span>
                  )}
                </span>
              ),
            },
            {
              key: 'drafts',
              header: 'Drafts',
              align: 'right',
              cell: (row) =>
                row.draftSectionCount > 0 ? (
                  <Pill tone="warn">{row.draftSectionCount} pending</Pill>
                ) : (
                  <span className="text-smoke">—</span>
                ),
            },
            { key: 'state', header: 'State', cell: (row) => <StatusPill status={row.state === 1 ? 'Published' : 'Draft'} /> },
            {
              key: 'system',
              header: '',
              cell: (row) => (row.isSystemPage ? <Pill tone="muted">system route</Pill> : null),
            },
            {
              key: 'updated',
              header: 'Updated',
              align: 'right',
              cell: (row) => (
                <span className="text-[0.8125rem] text-smoke">
                  {row.updatedAtUtc ? relativeTime(row.updatedAtUtc) : '—'}
                </span>
              ),
            },
            {
              key: 'view',
              header: '',
              align: 'right',
              cell: (row) => (
                <a
                  href={`/${row.slug === 'home' ? '' : row.slug}`}
                  target="_blank"
                  rel="noreferrer noopener"
                  onClick={(event) => event.stopPropagation()}
                  className="inline-flex size-8 items-center justify-center rounded-full border border-[var(--hairline-strong)] text-smoke transition-colors hover:border-[var(--accent-line)] hover:text-accent"
                  aria-label={`Open /${row.slug} in a new tab`}
                >
                  <Icon name="arrow-up-right" size={14} />
                </a>
              ),
            },
          ]}
        />
      </Panel>
    </>
  )
}

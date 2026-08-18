import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { useDeleteMedia, useMedia, useMediaFolders, useUpdateMedia, useUploadMedia } from '../lib/admin-api'
import { describeErrorText, formatIsoDate } from '../lib/format'
import type { MediaAsset } from '../lib/types'
import { ConfirmDialog, Drawer, useToast } from '../components/overlays'
import {
  FilterChip,
  Hint,
  InlineError,
  PageHeader,
  Pagination,
  Panel,
  Pill,
  TextField,
} from '../components/ui'

/**
 * The media library. Every upload is transcoded to WebP with a full ladder of width variants
 * and a blurred placeholder, so what the owner drops in here arrives on the public site
 * already inside the LCP budget.
 */
export function MediaLibraryPage() {
  const [search, setSearch] = useState('')
  const [folder, setFolder] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<MediaAsset | null>(null)
  const [uploadOpen, setUploadOpen] = useState(false)

  const { data, isLoading } = useMedia({ q: search || undefined, folder: folder || undefined, page, pageSize: 36 })
  const { data: folders } = useMediaFolders()

  return (
    <>
      <PageHeader
        eyebrow="Website"
        title="Media"
        lead={data ? `${data.total} file${data.total === 1 ? '' : 's'} in the library.` : undefined}
        actions={
          <Button size="sm" icon="plus" onClick={() => setUploadOpen(true)}>
            Upload
          </Button>
        }
      >
        <div className="flex flex-wrap items-center gap-2">
          <TextField
            placeholder="Search by name, alt text or tag"
            value={search}
            onChange={(event) => {
              setSearch(event.target.value)
              setPage(1)
            }}
            className="min-w-[16rem] flex-1"
            aria-label="Search media"
          />
          <FilterChip active={!folder} onClick={() => setFolder('')}>
            All folders
          </FilterChip>
          {(folders ?? []).map((name) => (
            <FilterChip key={name} active={folder === name} onClick={() => setFolder(name)}>
              {name}
            </FilterChip>
          ))}
        </div>
      </PageHeader>

      <Panel padded={false}>
        <div className="p-5">
          {isLoading ? (
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6">
              {Array.from({ length: 12 }).map((_, index) => (
                <Skeleton key={index} className="aspect-[4/3] w-full" />
              ))}
            </div>
          ) : (data?.items.length ?? 0) === 0 ? (
            <div className="py-16 text-center">
              <span className="mb-4 inline-flex size-11 items-center justify-center rounded-full border border-[var(--hairline-strong)] text-accent">
                <Icon name="studio" size={20} />
              </span>
              <p className="text-[0.9375rem] font-medium">Nothing in the library</p>
              <p className="measure mx-auto mt-1.5 text-[0.8125rem] leading-relaxed text-smoke">
                Upload a photograph and it becomes pickable from every section editor on the site.
              </p>
              <Button size="sm" icon="plus" className="mt-5" onClick={() => setUploadOpen(true)}>
                Upload the first file
              </Button>
            </div>
          ) : (
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6">
              {(data?.items ?? []).map((asset) => (
                <button
                  key={asset.id}
                  type="button"
                  onClick={() => setSelected(asset)}
                  className="group overflow-hidden rounded-[0.625rem] border border-[var(--hairline)] text-left transition-[border-color,transform] duration-200 hover:-translate-y-0.5 hover:border-[var(--accent-line)]"
                >
                  <div
                    className="aspect-[4/3] w-full bg-[var(--steel)]"
                    style={{
                      backgroundImage: asset.blurDataUrl ? `url(${asset.blurDataUrl})` : undefined,
                      backgroundSize: 'cover',
                    }}
                  >
                    <img src={asset.url} alt={asset.altText} loading="lazy" className="graded size-full object-cover" />
                  </div>
                  <div className="px-2.5 py-2">
                    <p className="truncate text-[0.75rem] font-medium">{asset.altText || asset.fileName}</p>
                    <p className="numeric mt-0.5 truncate text-[0.6875rem] text-smoke">
                      {asset.width}×{asset.height} · {Math.round(asset.sizeBytes / 1024)} KB ·{' '}
                      {Object.keys(asset.variants).length} variants
                    </p>
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>

        {data && (
          <Pagination page={data.page} pageCount={data.pageCount} total={data.total} pageSize={data.pageSize} onPage={setPage} />
        )}
      </Panel>

      <AssetDrawer asset={selected} onClose={() => setSelected(null)} />
      <UploadDrawer open={uploadOpen} onClose={() => setUploadOpen(false)} folders={folders ?? []} />
    </>
  )
}

function AssetDrawer({ asset, onClose }: { asset: MediaAsset | null; onClose: () => void }) {
  const toast = useToast()
  const update = useUpdateMedia()
  const remove = useDeleteMedia()

  const [form, setForm] = useState<Record<string, string>>({})
  const [signature, setSignature] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [conflict, setConflict] = useState<string | null>(null)

  if (asset && signature !== asset.id) {
    setSignature(asset.id)
    setError(null)
    setConflict(null)
    setForm({
      altText: asset.altText,
      caption: asset.caption ?? '',
      credit: asset.credit ?? '',
      folder: asset.folder ?? '',
      tags: asset.tags.join(', '),
    })
  }

  async function save() {
    if (!asset) return
    setError(null)
    try {
      await update.mutateAsync({ id: asset.id, body: form })
      toast.success('Media updated')
      onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  async function destroy(force: boolean) {
    if (!asset) return
    try {
      await remove.mutateAsync({ id: asset.id, force })
      toast.success('File deleted')
      setConfirmDelete(false)
      onClose()
    } catch (cause) {
      const response = (cause as { response?: { status?: number; data?: { detail?: string } } }).response
      if (response?.status === 409) {
        setConflict(response.data?.detail ?? 'This file is still in use.')
        setConfirmDelete(false)
      } else {
        setError(describeErrorText(cause))
      }
    }
  }

  return (
    <>
      <Drawer
        open={asset !== null}
        onClose={onClose}
        title={asset?.fileName ?? 'Media'}
        description="Alt text is what a screen reader announces and what search engines read. It is not optional."
        footer={
          <>
            <Button variant="ghost" size="sm" onClick={() => setConfirmDelete(true)}>
              Delete
            </Button>
            <div className="flex-1" />
            <Button variant="ghost" size="sm" onClick={onClose}>
              Cancel
            </Button>
            <Button size="sm" icon="check" onClick={() => void save()} loading={update.isPending}>
              Save
            </Button>
          </>
        }
      >
        {asset && (
          <div className="space-y-5">
            {error && <InlineError>{error}</InlineError>}
            {conflict && (
              <div className="rounded-[var(--radius-card)] border border-accent-hot/45 bg-[color-mix(in_srgb,var(--accent-hot)_7%,transparent)] p-4">
                <p className="text-[0.875rem] leading-relaxed">{conflict}</p>
                <Button variant="danger" size="sm" className="mt-3" onClick={() => void destroy(true)}>
                  Delete anyway
                </Button>
              </div>
            )}

            <img
              src={asset.url}
              alt={asset.altText}
              className="graded w-full rounded-[var(--radius-card)] border border-[var(--hairline)] object-cover"
            />

            <dl className="grid grid-cols-2 gap-3 text-[0.8125rem]">
              <div>
                <dt className="text-smoke">Dimensions</dt>
                <dd className="numeric">
                  {asset.width}×{asset.height}
                </dd>
              </div>
              <div>
                <dt className="text-smoke">Size</dt>
                <dd className="numeric">{Math.round(asset.sizeBytes / 1024)} KB</dd>
              </div>
              <div>
                <dt className="text-smoke">Uploaded</dt>
                <dd className="numeric">{formatIsoDate(asset.createdAtUtc.slice(0, 10))}</dd>
              </div>
              <div>
                <dt className="text-smoke">Variants</dt>
                <dd className="flex flex-wrap gap-1 pt-0.5">
                  {Object.keys(asset.variants).length === 0 ? (
                    <span className="text-smoke">original only</span>
                  ) : (
                    Object.keys(asset.variants).map((width) => (
                      <Pill key={width} tone="muted">
                        {width}px
                      </Pill>
                    ))
                  )}
                </dd>
              </div>
            </dl>

            <TextField
              label="URL"
              readOnly
              value={asset.url}
              onFocus={(event) => event.currentTarget.select()}
              hint="Paste this into any image field, or pick from the library instead."
            />

            <TextField label="Alt text" required value={form.altText ?? ''} onChange={(event) => setForm((c) => ({ ...c, altText: event.target.value }))} />
            <TextField label="Caption" value={form.caption ?? ''} onChange={(event) => setForm((c) => ({ ...c, caption: event.target.value }))} />
            <TextField label="Credit" value={form.credit ?? ''} onChange={(event) => setForm((c) => ({ ...c, credit: event.target.value }))} />
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField label="Folder" value={form.folder ?? ''} onChange={(event) => setForm((c) => ({ ...c, folder: event.target.value }))} />
              <TextField label="Tags" hint="Comma separated." value={form.tags ?? ''} onChange={(event) => setForm((c) => ({ ...c, tags: event.target.value }))} />
            </div>
          </div>
        )}
      </Drawer>

      <ConfirmDialog
        open={confirmDelete}
        onClose={() => setConfirmDelete(false)}
        title="Delete this file?"
        body="The original and every WebP variant are removed from disk. If a page still references it, you will be told before anything is deleted."
        confirmLabel="Delete"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => void destroy(false)}
      />
    </>
  )
}

function UploadDrawer({ open, onClose, folders }: { open: boolean; onClose: () => void; folders: string[] }) {
  const toast = useToast()
  const upload = useUploadMedia()
  const [files, setFiles] = useState<File[]>([])
  const [altText, setAltText] = useState('')
  const [folder, setFolder] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [progress, setProgress] = useState<string | null>(null)

  async function submit() {
    if (files.length === 0) return setError('Choose at least one file.')
    if (!altText.trim()) return setError('Describe the images for screen readers.')
    setError(null)

    let done = 0
    for (const file of files) {
      setProgress(`Uploading ${done + 1} of ${files.length}…`)
      try {
        await upload.mutateAsync({
          file,
          // Numbered when several go up together, so no two share identical alt text.
          altText: files.length > 1 ? `${altText.trim()} (${done + 1})` : altText.trim(),
          folder: folder || undefined,
        })
        done++
      } catch (cause) {
        setError(describeErrorText(cause))
        break
      }
    }

    setProgress(null)
    if (done > 0) {
      toast.success(`${done} file${done === 1 ? '' : 's'} uploaded`)
      setFiles([])
      setAltText('')
      if (done === files.length) onClose()
    }
  }

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Upload media"
      description="Images are converted to WebP at 480, 960, 1440, 1920 and 2560 wide, plus a blurred placeholder."
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button size="sm" icon="plus" onClick={() => void submit()} loading={upload.isPending}>
            Upload {files.length > 0 ? `${files.length} file${files.length === 1 ? '' : 's'}` : ''}
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {error && <InlineError>{error}</InlineError>}
        {progress && <Hint icon="loader">{progress}</Hint>}

        <div>
          <label htmlFor="upload-files" className="mb-1.5 block text-[0.8125rem] font-medium">
            Files
          </label>
          <input
            id="upload-files"
            type="file"
            accept="image/*"
            multiple
            onChange={(event) => setFiles([...(event.target.files ?? [])])}
            className="w-full text-[0.8125rem] text-smoke file:mr-3 file:rounded-full file:border file:border-[var(--hairline-strong)] file:bg-transparent file:px-3 file:py-1.5 file:text-[0.8125rem] file:text-bone"
          />
          <p className="mt-1.5 text-[0.75rem] text-smoke">Up to 25 MB each.</p>
        </div>

        <TextField
          label="Alt text"
          required
          hint="Describe what is in the photograph, not that it is a photograph."
          placeholder="Eleiko platform on the Whitefield strength floor"
          value={altText}
          onChange={(event) => setAltText(event.target.value)}
        />

        <div>
          <TextField
            label="Folder"
            hint="Groups the library. Existing folders are listed below."
            value={folder}
            onChange={(event) => setFolder(event.target.value)}
          />
          {folders.length > 0 && (
            <div className="mt-2 flex flex-wrap gap-1.5">
              {folders.map((name) => (
                <button
                  key={name}
                  type="button"
                  onClick={() => setFolder(name)}
                  className="rounded-full border border-[var(--hairline-strong)] px-2.5 py-1 text-[0.75rem] text-smoke transition-colors hover:border-[var(--accent-line)] hover:text-bone"
                >
                  {name}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>
    </Drawer>
  )
}

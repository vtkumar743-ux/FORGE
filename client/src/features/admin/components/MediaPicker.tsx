import { useRef, useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { Skeleton } from '@/components/ui/Skeleton'
import { cn, describeErrorText } from '../lib/format'
import { useMedia, useMediaFolders, useUploadMedia } from '../lib/admin-api'
import type { MediaAsset } from '../lib/types'
import { Drawer } from './overlays'
import { InlineError, TextField } from './ui'

/**
 * The media field used by every section editor and every image property in the panel.
 * Picking from the library is the default path; uploading is available inline so the
 * owner never has to leave the section they are editing to add one photograph.
 */
export function MediaPicker({
  label,
  hint,
  value,
  onChange,
  folder,
}: {
  label: string
  hint?: string
  value: string
  onChange: (url: string) => void
  folder?: string
}) {
  const [open, setOpen] = useState(false)
  const isImage = value && !/\.(mp4|webm|mov)$/i.test(value)

  return (
    <div>
      <p className="mb-1.5 text-[0.8125rem] font-medium text-bone">{label}</p>
      <div className="flex items-start gap-3">
        <div className="size-16 shrink-0 overflow-hidden rounded-[0.625rem] border border-[var(--hairline-strong)] bg-[var(--steel)]">
          {value && isImage ? (
            <img src={value} alt="" className="graded size-full object-cover" loading="lazy" />
          ) : (
            <div className="flex size-full items-center justify-center">
              <Icon name={value ? 'sparkles' : 'plus'} size={18} className="text-smoke" />
            </div>
          )}
        </div>

        <div className="min-w-0 flex-1">
          <input
            value={value}
            onChange={(event) => onChange(event.target.value)}
            placeholder="/media/uploads/…"
            aria-label={`${label} URL`}
            className={
              'h-10 w-full rounded-[0.625rem] border border-[var(--hairline-strong)] ' +
              'bg-[color-mix(in_srgb,var(--bone)_4%,var(--carbon))] px-3 text-[0.8125rem] text-bone ' +
              'transition-colors focus:border-accent focus:outline-none focus:ring-[3px] focus:ring-[var(--accent-soft)]'
            }
          />
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" icon="sparkles" onClick={() => setOpen(true)}>
              Library
            </Button>
            {value && (
              <Button variant="ghost" size="sm" onClick={() => onChange('')}>
                Clear
              </Button>
            )}
          </div>
          {hint && <p className="mt-2 text-[0.75rem] leading-relaxed text-smoke">{hint}</p>}
        </div>
      </div>

      <MediaLibraryDrawer
        open={open}
        onClose={() => setOpen(false)}
        defaultFolder={folder}
        onPick={(asset) => {
          onChange(asset.url)
          setOpen(false)
        }}
      />
    </div>
  )
}

/** The picker sheet: search, folder filter, upload and a grid of everything uploaded. */
export function MediaLibraryDrawer({
  open,
  onClose,
  onPick,
  defaultFolder,
}: {
  open: boolean
  onClose: () => void
  onPick: (asset: MediaAsset) => void
  defaultFolder?: string
}) {
  const [search, setSearch] = useState('')
  const [activeFolder, setActiveFolder] = useState(defaultFolder ?? '')
  const { data, isLoading } = useMedia({ q: search || undefined, folder: activeFolder || undefined, pageSize: 60 })
  const { data: folders } = useMediaFolders()

  return (
    <Drawer open={open} onClose={onClose} title="Media library" width="lg" description="Pick an image, or upload a new one. Every upload is converted to WebP with size variants.">
      <UploadRow defaultFolder={activeFolder || defaultFolder} />

      <div className="mt-6 flex flex-wrap items-center gap-2">
        <TextField
          placeholder="Search by name, alt text or tag"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          className="min-w-[14rem] flex-1"
          aria-label="Search media"
        />
        <select
          value={activeFolder}
          onChange={(event) => setActiveFolder(event.target.value)}
          aria-label="Folder"
          className="h-10 rounded-[0.625rem] border border-[var(--hairline-strong)] bg-[color-mix(in_srgb,var(--bone)_4%,var(--carbon))] px-3 text-[0.875rem] text-bone"
        >
          <option value="">All folders</option>
          {(folders ?? []).map((name) => (
            <option key={name} value={name}>
              {name}
            </option>
          ))}
        </select>
      </div>

      <div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
        {isLoading &&
          Array.from({ length: 8 }).map((_, index) => <Skeleton key={index} className="aspect-[4/3] w-full" />)}

        {!isLoading &&
          (data?.items ?? []).map((asset) => (
            <button
              key={asset.id}
              type="button"
              onClick={() => onPick(asset)}
              className="group overflow-hidden rounded-[0.625rem] border border-[var(--hairline)] text-left transition-[border-color,transform] duration-200 hover:-translate-y-0.5 hover:border-[var(--accent-line)]"
            >
              <div
                className="aspect-[4/3] w-full bg-[var(--steel)]"
                style={{
                  backgroundImage: asset.blurDataUrl ? `url(${asset.blurDataUrl})` : undefined,
                  backgroundSize: 'cover',
                }}
              >
                <img
                  src={asset.url}
                  alt={asset.altText}
                  loading="lazy"
                  className="graded size-full object-cover"
                />
              </div>
              <div className="px-2.5 py-2">
                <p className="truncate text-[0.75rem] font-medium text-bone">{asset.altText || asset.fileName}</p>
                <p className="numeric mt-0.5 text-[0.6875rem] text-smoke">
                  {asset.width}×{asset.height} · {Math.round(asset.sizeBytes / 1024)} KB
                </p>
              </div>
            </button>
          ))}
      </div>

      {!isLoading && (data?.items.length ?? 0) === 0 && (
        <p className="mt-8 text-center text-[0.875rem] text-smoke">
          Nothing in the library yet. Upload the first image above.
        </p>
      )}
    </Drawer>
  )
}

function UploadRow({ defaultFolder }: { defaultFolder?: string }) {
  const upload = useUploadMedia()
  const fileInput = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [altText, setAltText] = useState('')
  const [folder, setFolder] = useState(defaultFolder ?? '')
  const [error, setError] = useState<string | null>(null)

  async function submit() {
    if (!file) return setError('Choose a file first.')
    if (!altText.trim()) return setError('Describe the image for screen readers.')

    setError(null)
    try {
      await upload.mutateAsync({ file, altText: altText.trim(), folder: folder || undefined })
      setFile(null)
      setAltText('')
      if (fileInput.current) fileInput.current.value = ''
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  return (
    <div className="rounded-[var(--radius-card)] border border-dashed border-[var(--hairline-strong)] p-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="min-w-[12rem] flex-1">
          <label htmlFor="media-file" className="mb-1.5 block text-[0.8125rem] font-medium text-bone">
            New upload
          </label>
          <input
            id="media-file"
            ref={fileInput}
            type="file"
            accept="image/*"
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
            className={cn(
              'w-full text-[0.8125rem] text-smoke',
              'file:mr-3 file:rounded-full file:border file:border-[var(--hairline-strong)] file:bg-transparent',
              'file:px-3 file:py-1.5 file:text-[0.8125rem] file:text-bone hover:file:border-[var(--accent-line)]',
            )}
          />
        </div>
        <TextField
          label="Alt text"
          placeholder="Barbell rack on the Whitefield floor"
          value={altText}
          onChange={(event) => setAltText(event.target.value)}
          className="min-w-[12rem] flex-1"
        />
        <TextField
          label="Folder"
          placeholder="facility"
          value={folder}
          onChange={(event) => setFolder(event.target.value)}
          className="w-32"
        />
        <Button size="sm" onClick={() => void submit()} loading={upload.isPending} icon="plus">
          Upload
        </Button>
      </div>
      {error && (
        <div className="mt-3">
          <InlineError>{error}</InlineError>
        </div>
      )}
    </div>
  )
}

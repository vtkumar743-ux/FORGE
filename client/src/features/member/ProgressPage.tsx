import { useEffect, useMemo, useRef, useState } from 'react'
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { Badge } from '@/components/ui/Card'
import { getAccessToken } from '@/lib/api'
import { Button } from '@/components/ui/Button'
import { Icon, isIconName } from '@/components/ui/Icon'
import { EmptyState, Skeleton } from '@/components/ui/Skeleton'
import { cn, formatDate, todayIso } from '@/lib/utils'
import { describeErrorText } from '@/features/admin/lib/format'
import {
  useAddScan,
  useDeletePhoto,
  useDeleteScan,
  useMarkBadgesSeen,
  useProgress,
  useRefreshBadges,
  useUploadPhoto,
} from './lib/portal-api'
import {
  DrawnCheck,
  Field,
  InlineNote,
  Panel,
  PillToggle,
  PortalHeading,
  Sheet,
  StatTile,
  StreakCalendar,
  StreakFlame,
} from './components/ui'
import type { BodyScan, ProgressPhoto, StrengthSeries } from './lib/types'

/**
 * Progress (Module 3 — Progress): weight and composition trends, strength curves,
 * the attendance calendar, body-scan entries, side-by-side photo compare and badges.
 *
 * Charts are drawn from the tokens rather than Recharts' defaults, so the palette
 * survives a theme change in the CMS the same way everything else does.
 */
export function ProgressPage() {
  const { data, isLoading } = useProgress()
  const [scanOpen, setScanOpen] = useState(false)
  const markSeen = useMarkBadgesSeen()

  // Opening this screen is what "looking at your badges" means; stop the ring.
  const unseen = data?.badges.some((badge) => !badge.isSeen) ?? false
  useEffect(() => {
    if (unseen && !markSeen.isPending) markSeen.mutate()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [unseen])

  /**
   * The shareable body-scan report (Module 4.4). The access token lives in memory only, so a
   * plain link would 401 — fetch it with the header and hand the blob to the browser.
   */
  const [reportBusy, setReportBusy] = useState(false)
  const downloadReport = async () => {
    setReportBusy(true)
    try {
      const response = await fetch('/api/portal/community/progress-report.pdf', {
        headers: { Authorization: `Bearer ${getAccessToken() ?? ''}` },
        credentials: 'include',
      })
      if (!response.ok) return
      const blob = await response.blob()
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = 'FORGE-progress-report.pdf'
      anchor.click()
      URL.revokeObjectURL(url)
    } finally {
      setReportBusy(false)
    }
  }

  if (isLoading || !data) {
    return (
      <div>
        <PortalHeading eyebrow="Your numbers" title="Progress" />
        <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
          {Array.from({ length: 4 }).map((_, index) => (
            <Skeleton key={index} className="h-32" />
          ))}
        </div>
        <Skeleton className="mt-5 h-80" />
      </div>
    )
  }

  const { headline, scans, strength, weeklyVolume, streak, photos, badges } = data
  const weightSeries = scans
    .slice()
    .reverse()
    .map((scan) => ({
      date: scan.scanDate,
      label: formatDate(scan.scanDate),
      weight: Number(scan.weightKg),
      fat: scan.bodyFatPercent != null ? Number(scan.bodyFatPercent) : null,
      muscle: scan.skeletalMuscleMassKg != null ? Number(scan.skeletalMuscleMassKg) : null,
    }))

  return (
    <div className="space-y-8">
      <PortalHeading
        eyebrow="Your numbers"
        title="Progress"
        lead="Body composition, strength and attendance in one place. Scans the desk measured and scans you took at home sit on the same trend."
        actions={
          <>
            <Button size="sm" variant="outline" icon="arrow-up-right" onClick={downloadReport} disabled={reportBusy}>
              {reportBusy ? 'Building…' : 'Download report'}
            </Button>
            <Button size="sm" icon="body-scan" onClick={() => setScanOpen(true)}>
              Add a scan
            </Button>
          </>
        }
      />

      <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
        <StatTile
          label="Weight"
          value={headline.currentWeightKg != null ? `${headline.currentWeightKg} kg` : '—'}
          sub={
            headline.weightChangeKg != null
              ? `${headline.weightChangeKg > 0 ? '+' : ''}${headline.weightChangeKg} kg since your first scan`
              : 'Add a second scan to see the trend'
          }
          icon="body-scan"
        />
        <StatTile
          label="Body fat"
          value={headline.currentBodyFatPercent != null ? `${headline.currentBodyFatPercent}%` : '—'}
          sub={
            headline.bodyFatChange != null
              ? `${headline.bodyFatChange > 0 ? '+' : ''}${headline.bodyFatChange} points`
              : `${headline.scanCount} scan${headline.scanCount === 1 ? '' : 's'} on file`
          }
          icon="gauge"
          tone={headline.bodyFatChange != null && headline.bodyFatChange < 0 ? 'success' : 'neutral'}
        />
        <StatTile
          label="Personal records"
          value={headline.totalPersonalRecords}
          sub="Across every lift you have logged"
          icon="trophy"
          tone={headline.totalPersonalRecords > 0 ? 'accent' : 'neutral'}
        />
        <StatTile
          label="Total volume"
          value={`${Math.round(headline.totalVolumeLiftedKg).toLocaleString('en-IN')} kg`}
          sub="Weight × reps, every set you have logged here"
          icon="barbell"
        />
      </div>

      <div className="grid gap-5 lg:grid-cols-3">
        <Panel title="Body composition" className="lg:col-span-2">
          {weightSeries.length < 2 ? (
            <EmptyState
              icon="body-scan"
              headline="Two scans and this becomes a trend"
              body="Add what you weigh today, and again in a fortnight. The desk's InBody readings land here automatically."
            />
          ) : (
            <div className="h-72">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={weightSeries} margin={{ top: 8, right: 8, bottom: 0, left: -18 }}>
                  <defs>
                    <linearGradient id="weightFill" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor="var(--accent)" stopOpacity={0.35} />
                      <stop offset="100%" stopColor="var(--accent)" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid stroke="var(--hairline)" vertical={false} />
                  <XAxis dataKey="label" stroke="var(--smoke)" fontSize={11} tickLine={false} axisLine={false} />
                  <YAxis stroke="var(--smoke)" fontSize={11} tickLine={false} axisLine={false} domain={['dataMin - 2', 'dataMax + 2']} />
                  <Tooltip content={<ChartTooltip unit="kg" />} />
                  <Area
                    type="monotone"
                    dataKey="weight"
                    name="Weight"
                    stroke="var(--accent)"
                    strokeWidth={2}
                    fill="url(#weightFill)"
                  />
                  <Line type="monotone" dataKey="muscle" name="Muscle" stroke="var(--success)" strokeWidth={1.5} dot={false} />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}
        </Panel>

        <Panel title="Attendance">
          <StreakFlame streak={streak} size={96} />
          <div className="mt-5 border-t border-[var(--hairline)] pt-5">
            <StreakCalendar days={streak.calendar} />
          </div>
        </Panel>
      </div>

      {strength.length > 0 && <StrengthPanel series={strength} />}

      {weeklyVolume.length > 1 && (
        <Panel title="Weekly volume" description="Total kilograms moved, week by week.">
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={weeklyVolume} margin={{ top: 8, right: 8, bottom: 0, left: -18 }}>
                <CartesianGrid stroke="var(--hairline)" vertical={false} />
                <XAxis dataKey="label" stroke="var(--smoke)" fontSize={11} tickLine={false} axisLine={false} />
                <YAxis stroke="var(--smoke)" fontSize={11} tickLine={false} axisLine={false} />
                <Tooltip content={<ChartTooltip unit="kg" />} cursor={{ fill: 'var(--steel)', opacity: 0.4 }} />
                <Bar dataKey="volumeKg" name="Volume" fill="var(--accent)" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </Panel>
      )}

      <PhotoCompare photos={photos} />

      <ScanTable scans={scans} />

      <Panel title="Badges" description="Earned, not handed out.">
        {badges.length === 0 ? (
          <EmptyState
            icon="medal"
            headline="No badges yet"
            body="They unlock on visits, streaks, classes attended and personal records. The first one arrives on your first check-in."
          />
        ) : (
          <>
            <div className="flex flex-wrap gap-3">
              {badges.map((badge) => (
                <div
                  key={badge.id}
                  title={badge.description}
                  className={cn(
                    'flex items-center gap-3 rounded-[var(--radius-card)] border px-4 py-3',
                    badge.tier === 'gold'
                      ? 'border-[var(--accent-line)] bg-[var(--accent-soft)]'
                      : 'border-[var(--hairline)] bg-carbon',
                  )}
                >
                  <Icon
                    name={isIconName(badge.iconKey) ? badge.iconKey : 'medal'}
                    size={22}
                    className={badge.tier === 'gold' ? 'text-accent' : 'text-smoke'}
                  />
                  <div className="min-w-0">
                    <p className="text-[0.875rem] font-medium text-bone">{badge.name}</p>
                    <p className="text-[0.6875rem] capitalize text-smoke">
                      {badge.tier} · {formatDate(badge.awardedAtUtc)}
                    </p>
                  </div>
                </div>
              ))}
            </div>
            <RefreshBadges />
          </>
        )}
      </Panel>

      {scanOpen && <ScanSheet onClose={() => setScanOpen(false)} />}
    </div>
  )
}

function RefreshBadges() {
  const refresh = useRefreshBadges()
  const [awarded, setAwarded] = useState<number | null>(null)

  return (
    <div className="mt-5 flex flex-wrap items-center gap-3 border-t border-[var(--hairline)] pt-4">
      <Button
        variant="ghost"
        size="sm"
        loading={refresh.isPending}
        onClick={() => refresh.mutate(undefined, { onSuccess: (rows) => setAwarded(rows.length) })}
      >
        Check for new badges
      </Button>
      {awarded !== null && (
        <span className="text-[0.8125rem] text-smoke">
          {awarded === 0 ? 'Nothing new yet — keep going.' : `${awarded} unlocked.`}
        </span>
      )}
    </div>
  )
}

/* ---------------------------------------------------------------- strength */

function StrengthPanel({ series }: { series: StrengthSeries[] }) {
  const [slug, setSlug] = useState(series[0].slug)
  const active = series.find((entry) => entry.slug === slug) ?? series[0]
  const change = active.points.length > 1 ? active.latestE1Rm - active.points[0].estimatedOneRepMax : null

  return (
    <Panel
      title="Strength"
      description="Estimated one-rep max from your top set each session — the number that compares a heavy triple with a light ten."
      actions={
        <PillToggle
          ariaLabel="Which lift"
          value={slug}
          onChange={setSlug}
          options={series.map((entry) => ({ value: entry.slug, label: entry.exerciseName }))}
        />
      }
    >
      <div className="mb-4 flex flex-wrap items-baseline gap-x-6 gap-y-2">
        <div>
          <p className="caption text-[0.5625rem]">Best</p>
          <p className="numeric display-m mt-1 text-[1.5rem] text-accent">{active.bestE1Rm} kg</p>
        </div>
        <div>
          <p className="caption text-[0.5625rem]">Latest</p>
          <p className="numeric display-m mt-1 text-[1.5rem] text-bone">{active.latestE1Rm} kg</p>
        </div>
        {change != null && (
          <div>
            <p className="caption text-[0.5625rem]">Since you started logging</p>
            <p className={cn('numeric mt-1 text-[1.125rem]', change >= 0 ? 'text-success' : 'text-accent-hot')}>
              {change >= 0 ? '+' : ''}
              {change.toFixed(1)} kg
            </p>
          </div>
        )}
      </div>

      <div className="h-64">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart
            data={active.points.map((point) => ({ ...point, label: formatDate(point.date) }))}
            margin={{ top: 8, right: 8, bottom: 0, left: -18 }}
          >
            <CartesianGrid stroke="var(--hairline)" vertical={false} />
            <XAxis dataKey="label" stroke="var(--smoke)" fontSize={11} tickLine={false} axisLine={false} />
            <YAxis stroke="var(--smoke)" fontSize={11} tickLine={false} axisLine={false} domain={['dataMin - 5', 'dataMax + 5']} />
            <Tooltip content={<ChartTooltip unit="kg" />} />
            <Line
              type="monotone"
              dataKey="estimatedOneRepMax"
              name="Est. 1RM"
              stroke="var(--accent)"
              strokeWidth={2}
              dot={{ r: 3, fill: 'var(--accent)', strokeWidth: 0 }}
              activeDot={{ r: 5 }}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </Panel>
  )
}

interface TooltipPayload {
  name?: string
  value?: number | string | null
  color?: string
}

function ChartTooltip({
  active,
  payload,
  label,
  unit,
}: {
  active?: boolean
  payload?: TooltipPayload[]
  label?: string | number
  unit?: string
}) {
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-[10px] border border-[var(--hairline-strong)] bg-ink/95 px-3 py-2.5 text-[0.75rem] backdrop-blur">
      <p className="text-smoke">{label}</p>
      {payload
        .filter((entry) => entry.value != null)
        .map((entry) => (
          <p key={entry.name} className="numeric mt-1 text-bone">
            <span style={{ color: entry.color }}>{entry.name}</span> {entry.value}
            {unit ? ` ${unit}` : ''}
          </p>
        ))}
    </div>
  )
}

/* ---------------------------------------------------------------- photos */

function PhotoCompare({ photos }: { photos: ProgressPhoto[] }) {
  const [pose, setPose] = useState('front')
  const [uploadOpen, setUploadOpen] = useState(false)
  const remove = useDeletePhoto()

  const inPose = useMemo(
    () => photos.filter((photo) => photo.pose === pose).sort((a, b) => a.takenOn.localeCompare(b.takenOn)),
    [photos, pose],
  )

  const [leftId, setLeftId] = useState<number | null>(null)
  const [rightId, setRightId] = useState<number | null>(null)

  const left = inPose.find((photo) => photo.id === leftId) ?? inPose[0]
  const right = inPose.find((photo) => photo.id === rightId) ?? inPose[inPose.length - 1]

  return (
    <Panel
      title="Progress photos"
      description="Private to you. Stored outside the public media folder and served only to your own session."
      actions={
        <Button size="sm" variant="outline" icon="plus" onClick={() => setUploadOpen(true)}>
          Add a photo
        </Button>
      }
    >
      {photos.length === 0 ? (
        <EmptyState
          icon="body-scan"
          headline="No photos yet"
          body="Same pose, same light, same time of day — twelve weeks apart is where this stops being a guess and starts being evidence."
        />
      ) : (
        <div className="space-y-5">
          <PillToggle
            ariaLabel="Pose"
            value={pose}
            onChange={setPose}
            options={['front', 'side', 'back'].map((value) => ({
              value,
              label: value[0].toUpperCase() + value.slice(1),
              count: photos.filter((photo) => photo.pose === value).length,
            }))}
          />

          {inPose.length === 0 ? (
            <p className="text-[0.875rem] text-smoke">Nothing in that pose yet.</p>
          ) : (
            <>
              <div className="grid gap-4 sm:grid-cols-2">
                <ComparePane
                  photo={left}
                  options={inPose}
                  onChange={setLeftId}
                  label="Then"
                  onDelete={() => left && remove.mutate(left.id)}
                />
                <ComparePane
                  photo={right}
                  options={inPose}
                  onChange={setRightId}
                  label="Now"
                  onDelete={() => right && remove.mutate(right.id)}
                />
              </div>
              {left && right && left.id !== right.id && left.weightKg != null && right.weightKg != null && (
                <p className="numeric text-center text-[0.875rem] text-smoke">
                  {left.weightKg} kg → {right.weightKg} kg over{' '}
                  {Math.round(
                    (new Date(right.takenOn).getTime() - new Date(left.takenOn).getTime()) / 86_400_000 / 7,
                  )}{' '}
                  weeks
                </p>
              )}
            </>
          )}
        </div>
      )}

      {uploadOpen && <PhotoSheet onClose={() => setUploadOpen(false)} />}
    </Panel>
  )
}

function ComparePane({
  photo,
  options,
  onChange,
  label,
  onDelete,
}: {
  photo: ProgressPhoto | undefined
  options: ProgressPhoto[]
  onChange: (id: number) => void
  label: string
  onDelete: () => void
}) {
  if (!photo) return null
  return (
    <figure className="overflow-hidden rounded-[var(--radius-card)] border border-[var(--hairline)] bg-ink/40">
      <div className="relative aspect-[3/4] bg-steel">
        <img src={photo.imageUrl} alt={`Progress photo from ${photo.takenOn}`} className="graded h-full w-full object-cover" />
        <span className="absolute left-3 top-3 rounded-full bg-ink/80 px-2.5 py-1 text-[0.625rem] uppercase tracking-[0.08em] text-bone backdrop-blur">
          {label}
        </span>
      </div>
      <figcaption className="flex items-center justify-between gap-2 p-3">
        <select
          className="field-input h-9 text-[0.8125rem]"
          value={photo.id}
          onChange={(event) => onChange(Number(event.target.value))}
          aria-label={`${label} photo`}
        >
          {options.map((option) => (
            <option key={option.id} value={option.id}>
              {formatDate(option.takenOn)}
              {option.weightKg != null ? ` · ${option.weightKg} kg` : ''}
            </option>
          ))}
        </select>
        <button
          type="button"
          onClick={onDelete}
          aria-label="Delete this photo"
          className="grid size-9 shrink-0 place-items-center rounded-full text-smoke transition-colors hover:bg-steel hover:text-accent-hot"
        >
          <Icon name="x" size={15} />
        </button>
      </figcaption>
    </figure>
  )
}

function PhotoSheet({ onClose }: { onClose: () => void }) {
  const upload = useUploadPhoto()
  const inputRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<string | null>(null)
  const [pose, setPose] = useState('front')
  const [takenOn, setTakenOn] = useState(todayIso())
  const [weight, setWeight] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview) }, [preview])

  return (
    <Sheet
      open
      onClose={onClose}
      title="Add a progress photo"
      description="Private to your account. Same pose and light each time makes the comparison honest."
      footer={
        done ? (
          <Button onClick={onClose}>Done</Button>
        ) : (
          <>
            <Button variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button
              loading={upload.isPending}
              disabled={!file}
              onClick={() => {
                setError(null)
                upload.mutate(
                  {
                    file: file!,
                    pose,
                    takenOn,
                    weightKg: weight ? Number(weight) : undefined,
                  },
                  { onSuccess: () => setDone(true), onError: (failure) => setError(describeErrorText(failure)) },
                )
              }}
            >
              Upload
            </Button>
          </>
        )
      }
    >
      {done ? (
        <div className="flex flex-col items-center py-6 text-center">
          <DrawnCheck size={56} />
          <p className="mt-5 text-[0.9375rem] text-bone">Saved to your private gallery.</p>
        </div>
      ) : (
        <div className="space-y-5">
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            className="flex w-full flex-col items-center justify-center gap-3 rounded-[var(--radius-card)] border border-dashed border-[var(--hairline-strong)] px-6 py-10 text-center transition-colors hover:border-accent"
          >
            {preview ? (
              <img src={preview} alt="" className="max-h-56 rounded-[10px] object-contain" />
            ) : (
              <>
                <Icon name="plus" size={26} className="text-accent" />
                <span className="text-[0.9375rem] text-bone">Choose a photo</span>
                <span className="text-[0.75rem] text-smoke">JPEG, PNG or WebP · up to 8 MB</span>
              </>
            )}
          </button>
          <input
            ref={inputRef}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            className="sr-only"
            onChange={(event) => {
              const picked = event.target.files?.[0] ?? null
              setFile(picked)
              if (preview) URL.revokeObjectURL(preview)
              setPreview(picked ? URL.createObjectURL(picked) : null)
            }}
          />

          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Pose">
              <select className="field-input" value={pose} onChange={(event) => setPose(event.target.value)}>
                <option value="front">Front</option>
                <option value="side">Side</option>
                <option value="back">Back</option>
              </select>
            </Field>
            <Field label="Taken on">
              <input
                type="date"
                className="field-input"
                value={takenOn}
                max={todayIso()}
                onChange={(event) => setTakenOn(event.target.value)}
              />
            </Field>
            <Field label="Weight (kg)">
              <input
                className="field-input"
                inputMode="decimal"
                value={weight}
                onChange={(event) => setWeight(event.target.value)}
                placeholder="Optional"
              />
            </Field>
          </div>

          {error && (
            <InlineNote tone="danger" icon="x">
              {error}
            </InlineNote>
          )}
        </div>
      )}
    </Sheet>
  )
}

/* ---------------------------------------------------------------- scans */

function ScanTable({ scans }: { scans: BodyScan[] }) {
  const remove = useDeleteScan()

  if (scans.length === 0) return null

  return (
    <Panel title="Body scans" description="Newest first. Readings from the gym's InBody sit alongside your own." padded={false}>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[42rem] text-left text-[0.875rem]">
          <thead>
            <tr className="border-b border-[var(--hairline)] text-smoke">
              <th className="px-5 py-3 font-normal">Date</th>
              <th className="px-3 py-3 text-right font-normal">Weight</th>
              <th className="px-3 py-3 text-right font-normal">Body fat</th>
              <th className="px-3 py-3 text-right font-normal">Muscle</th>
              <th className="px-3 py-3 text-right font-normal">Waist</th>
              <th className="px-3 py-3 font-normal">Source</th>
              <th className="px-5 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--hairline)]">
            {scans.map((scan) => (
              <tr key={scan.id}>
                <td className="px-5 py-3 text-bone">{formatDate(scan.scanDate)}</td>
                <td className="numeric px-3 py-3 text-right text-bone">{scan.weightKg} kg</td>
                <td className="numeric px-3 py-3 text-right text-smoke">
                  {scan.bodyFatPercent != null ? `${scan.bodyFatPercent}%` : '—'}
                </td>
                <td className="numeric px-3 py-3 text-right text-smoke">
                  {scan.skeletalMuscleMassKg != null ? `${scan.skeletalMuscleMassKg} kg` : '—'}
                </td>
                <td className="numeric px-3 py-3 text-right text-smoke">
                  {scan.waistCm != null ? `${scan.waistCm} cm` : '—'}
                </td>
                <td className="px-3 py-3">
                  {scan.isSelfReported ? (
                    <Badge>Self</Badge>
                  ) : (
                    <Badge tone="accent">{scan.deviceName ?? 'Gym'}</Badge>
                  )}
                </td>
                <td className="px-5 py-3 text-right">
                  {scan.isSelfReported && (
                    <button
                      type="button"
                      onClick={() => remove.mutate(scan.id)}
                      aria-label={`Delete the scan from ${scan.scanDate}`}
                      className="grid size-8 place-items-center rounded-full text-smoke transition-colors hover:bg-steel hover:text-accent-hot"
                    >
                      <Icon name="x" size={14} />
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Panel>
  )
}

function ScanSheet({ onClose }: { onClose: () => void }) {
  const add = useAddScan()
  const [form, setForm] = useState({
    scanDate: todayIso(),
    weightKg: '',
    bodyFatPercent: '',
    skeletalMuscleMassKg: '',
    waistCm: '',
    chestCm: '',
    armCm: '',
    thighCm: '',
    notes: '',
  })
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  function set(key: keyof typeof form, value: string) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  return (
    <Sheet
      open
      onClose={onClose}
      title="Add a scan"
      description="Weight is all that is required. Everything else sharpens the trend if you have it."
      footer={
        done ? (
          <Button onClick={onClose}>Done</Button>
        ) : (
          <>
            <Button variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button
              loading={add.isPending}
              disabled={!form.weightKg}
              onClick={() => {
                setError(null)
                add.mutate(
                  {
                    scanDate: form.scanDate,
                    weightKg: Number(form.weightKg),
                    bodyFatPercent: form.bodyFatPercent ? Number(form.bodyFatPercent) : undefined,
                    skeletalMuscleMassKg: form.skeletalMuscleMassKg ? Number(form.skeletalMuscleMassKg) : undefined,
                    waistCm: form.waistCm ? Number(form.waistCm) : undefined,
                    chestCm: form.chestCm ? Number(form.chestCm) : undefined,
                    armCm: form.armCm ? Number(form.armCm) : undefined,
                    thighCm: form.thighCm ? Number(form.thighCm) : undefined,
                    notes: form.notes || undefined,
                  },
                  { onSuccess: () => setDone(true), onError: (failure) => setError(describeErrorText(failure)) },
                )
              }}
            >
              Save scan
            </Button>
          </>
        )
      }
    >
      {done ? (
        <div className="flex flex-col items-center py-6 text-center">
          <DrawnCheck size={56} />
          <p className="mt-5 text-[0.9375rem] text-bone">Added to your trend.</p>
        </div>
      ) : (
        <div className="space-y-5">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Date">
              <input
                type="date"
                className="field-input"
                value={form.scanDate}
                max={todayIso()}
                onChange={(event) => set('scanDate', event.target.value)}
              />
            </Field>
            <Field label="Weight (kg)" hint="Required.">
              <input
                className="field-input"
                inputMode="decimal"
                value={form.weightKg}
                onChange={(event) => set('weightKg', event.target.value)}
              />
            </Field>
            <Field label="Body fat (%)">
              <input
                className="field-input"
                inputMode="decimal"
                value={form.bodyFatPercent}
                onChange={(event) => set('bodyFatPercent', event.target.value)}
              />
            </Field>
            <Field label="Muscle mass (kg)">
              <input
                className="field-input"
                inputMode="decimal"
                value={form.skeletalMuscleMassKg}
                onChange={(event) => set('skeletalMuscleMassKg', event.target.value)}
              />
            </Field>
          </div>

          <div className="grid gap-4 sm:grid-cols-4">
            {(['waistCm', 'chestCm', 'armCm', 'thighCm'] as const).map((key) => (
              <Field key={key} label={`${key.replace('Cm', '')} (cm)`.replace(/^./, (c) => c.toUpperCase())}>
                <input
                  className="field-input"
                  inputMode="decimal"
                  value={form[key]}
                  onChange={(event) => set(key, event.target.value)}
                />
              </Field>
            ))}
          </div>

          <Field label="Notes" hint="Time of day, whether you had eaten — anything that explains an odd reading.">
            <textarea
              className="field-input"
              rows={2}
              value={form.notes}
              onChange={(event) => set('notes', event.target.value)}
            />
          </Field>

          {error && (
            <InlineNote tone="danger" icon="x">
              {error}
            </InlineNote>
          )}
        </div>
      )}
    </Sheet>
  )
}

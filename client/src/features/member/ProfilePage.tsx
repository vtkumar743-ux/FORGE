import { useEffect, useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Skeleton } from '@/components/ui/Skeleton'
import { formatDate } from '@/lib/utils'
import { describeErrorText } from '@/features/admin/lib/format'
import { useSiteSettings } from '@/lib/cms'
import { useAuth } from '@/lib/auth'
import { usePortalMe, useUpdateProfile } from './lib/portal-api'
import { DrawnCheck, Field, InlineNote, Panel, PortalHeading } from './components/ui'

/**
 * Profile — the onboarding fields a member can maintain themselves: goal, home
 * branch, height, emergency contact, medical and injury notes, and the two
 * consents. The injury note is editable by the member on purpose: they know about
 * the shoulder before the coach does, and a form only the desk can fill in is a
 * form that stays wrong.
 */
export function ProfilePage() {
  const { data, isLoading } = usePortalMe()
  const { data: settings } = useSiteSettings()
  const { logout } = useAuth()
  const update = useUpdateProfile()

  const [form, setForm] = useState({
    primaryGoal: '',
    homeBranchId: 0,
    heightCm: '',
    emergencyContactName: '',
    emergencyContactPhone: '',
    medicalNotes: '',
    injuryNotes: '',
    consentMarketing: true,
    consentLeaderboard: false,
  })
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!data) return
    setForm({
      primaryGoal: data.primaryGoal ?? '',
      homeBranchId: data.homeBranchId,
      heightCm: data.heightCm != null ? String(data.heightCm) : '',
      emergencyContactName: '',
      emergencyContactPhone: '',
      medicalNotes: '',
      injuryNotes: '',
      consentMarketing: data.consentMarketing,
      consentLeaderboard: data.consentLeaderboard,
    })
  }, [data])

  if (isLoading || !data) {
    return (
      <div>
        <PortalHeading eyebrow="Account" title="Profile" />
        <Skeleton className="h-96" />
      </div>
    )
  }

  function set<K extends keyof typeof form>(key: K, value: (typeof form)[K]) {
    setForm((current) => ({ ...current, [key]: value }))
    setSaved(false)
  }

  return (
    <div className="space-y-8">
      <PortalHeading
        eyebrow="Account"
        title="Profile"
        lead={`Member since ${formatDate(data.joinedOn)} · ${data.memberCode}`}
      />

      <div className="grid gap-5 lg:grid-cols-2">
        <Panel title="Training">
          <div className="space-y-4">
            <Field label="Your goal" hint="What the coach writes your programme against.">
              <input
                className="field-input"
                value={form.primaryGoal}
                onChange={(event) => set('primaryGoal', event.target.value)}
                placeholder="Get stronger without losing the run"
              />
            </Field>

            <Field label="Home branch" hint="Where your plan is anchored and whose occupancy you see on the home screen.">
              <select
                className="field-input"
                value={form.homeBranchId}
                onChange={(event) => set('homeBranchId', Number(event.target.value))}
              >
                {(settings?.branches ?? []).map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name} · {branch.city}
                  </option>
                ))}
              </select>
            </Field>

            <Field label="Height (cm)" hint="Used for BMI on your body scans. Nothing else reads it.">
              <input
                className="field-input"
                inputMode="decimal"
                value={form.heightCm}
                onChange={(event) => set('heightCm', event.target.value)}
                placeholder="172"
              />
            </Field>
          </div>
        </Panel>

        <Panel title="Safety">
          <div className="space-y-4">
            <Field label="Emergency contact" hint="Leave blank to keep what the desk already has on file.">
              <input
                className="field-input"
                value={form.emergencyContactName}
                onChange={(event) => set('emergencyContactName', event.target.value)}
                placeholder="Name"
              />
            </Field>
            <Field label="Their number">
              <input
                className="field-input"
                inputMode="tel"
                value={form.emergencyContactPhone}
                onChange={(event) => set('emergencyContactPhone', event.target.value)}
                placeholder="98765 43210"
              />
            </Field>
            <Field
              label="Injuries the floor should know about"
              hint="Coaches read this before they program a lift. Left blank, nothing already on file is changed."
            >
              <textarea
                className="field-input"
                rows={3}
                value={form.injuryNotes}
                onChange={(event) => set('injuryNotes', event.target.value)}
                placeholder="Left shoulder — no overhead pressing until January."
              />
            </Field>
            <Field label="Medical notes" hint="Only the desk and your coach can see this.">
              <textarea
                className="field-input"
                rows={2}
                value={form.medicalNotes}
                onChange={(event) => set('medicalNotes', event.target.value)}
              />
            </Field>
          </div>
        </Panel>
      </div>

      <Panel title="Privacy" description="Both are off unless you turn them on, and you can turn them back off here.">
        <div className="space-y-4">
          <Toggle
            checked={form.consentLeaderboard}
            onChange={(value) => set('consentLeaderboard', value)}
            label="Show me on the branch leaderboard and post my records to the community feed"
            hint="Off means your PRs stay between you and your coach. The record is still detected and still counted."
          />
          <Toggle
            checked={form.consentMarketing}
            onChange={(value) => set('consentMarketing', value)}
            label="Send me offers and gym news"
            hint="Booking confirmations, payment reminders and class cancellations are sent either way — those are not marketing."
          />
        </div>
      </Panel>

      {error && (
        <InlineNote tone="danger" icon="x">
          {error}
        </InlineNote>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <Button
          loading={update.isPending}
          onClick={() => {
            setError(null)
            update.mutate(
              {
                primaryGoal: form.primaryGoal || undefined,
                homeBranchId: form.homeBranchId,
                heightCm: form.heightCm ? Number(form.heightCm) : undefined,
                emergencyContactName: form.emergencyContactName || undefined,
                emergencyContactPhone: form.emergencyContactPhone || undefined,
                medicalNotes: form.medicalNotes || undefined,
                injuryNotes: form.injuryNotes || undefined,
                consentMarketing: form.consentMarketing,
                consentLeaderboard: form.consentLeaderboard,
              },
              { onSuccess: () => setSaved(true), onError: (failure) => setError(describeErrorText(failure)) },
            )
          }}
        >
          Save changes
        </Button>
        {saved && (
          <span className="flex items-center gap-2 text-[0.875rem] text-success">
            <DrawnCheck size={22} />
            Saved
          </span>
        )}
        <Button variant="ghost" icon="log-out" onClick={() => void logout()} className="ml-auto">
          Sign out
        </Button>
      </div>

      <Panel title="Your details" description="Changing a name, number or email is a desk job — it is on your invoices.">
        <dl className="grid gap-x-6 gap-y-4 text-[0.875rem] sm:grid-cols-2">
          <Row label="Name" value={data.fullName} />
          <Row label="Member number" value={data.memberCode} />
          <Row label="Mobile" value={data.phone} />
          <Row label="Email" value={data.email ?? '—'} />
          <Row label="Status" value={data.statusName} />
          <Row label="Waiver" value={data.waiverSigned ? 'Signed' : 'Not signed — ask at the desk'} />
        </dl>
      </Panel>
    </div>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <dt className="caption text-[0.5625rem]">{label}</dt>
      <dd className="mt-1 truncate text-bone">{value}</dd>
    </div>
  )
}

function Toggle({
  checked,
  onChange,
  label,
  hint,
}: {
  checked: boolean
  onChange: (value: boolean) => void
  label: string
  hint: string
}) {
  return (
    <label className="flex cursor-pointer items-start gap-3.5">
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className={`relative mt-0.5 h-6 w-11 shrink-0 rounded-full border transition-colors duration-200 ${
          checked ? 'border-accent bg-accent' : 'border-[var(--hairline-strong)] bg-steel'
        }`}
      >
        <span
          aria-hidden
          className={`absolute top-0.5 size-4 rounded-full transition-[left] duration-200 ${
            checked ? 'left-[1.5rem] bg-ink' : 'left-0.5 bg-bone/70'
          }`}
        />
      </button>
      <span className="min-w-0">
        <span className="block text-[0.9375rem] leading-snug text-bone">{label}</span>
        <span className="mt-1 block text-[0.8125rem] leading-relaxed text-smoke">{hint}</span>
      </span>
    </label>
  )
}

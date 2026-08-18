import { useMemo, useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Skeleton } from '@/components/ui/Skeleton'
import { useSaveSettings, useSettings } from '../lib/admin-api'
import { describeErrorText } from '../lib/format'
import { useToast } from '../components/overlays'
import { Hint, InlineError, PageHeader, Panel, Pill, TextAreaField, TextField, Toggle } from '../components/ui'
import { MediaPicker } from '../components/MediaPicker'
import { humanise } from './zod-fields'

/**
 * Site settings: brand, theme tokens, contact, socials, SEO defaults and the announcement
 * bar. The colour fields write straight to CSS custom properties on the public site, which
 * is why changing the accent here repaints every button without a rebuild.
 */
export function SiteSettingsPage() {
  const toast = useToast()
  const { data: settings, isLoading } = useSettings()
  const save = useSaveSettings()

  const [dirty, setDirty] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)

  const groups = useMemo(() => {
    const map = new Map<string, typeof settings>()
    for (const setting of settings ?? []) {
      const list = map.get(setting.group) ?? []
      list.push(setting)
      map.set(setting.group, list)
    }
    return [...map.entries()]
  }, [settings])

  function set(key: string, value: string) {
    setDirty((current) => ({ ...current, [key]: value }))
  }

  function valueOf(key: string, fallback: string) {
    return dirty[key] ?? fallback
  }

  async function submit() {
    if (Object.keys(dirty).length === 0) return
    setError(null)
    try {
      await save.mutateAsync(dirty)
      setDirty({})
      toast.success('Settings saved', 'The public site picks these up on its next load.')
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  if (isLoading) {
    return (
      <div className="space-y-5">
        <Skeleton className="h-9 w-64" />
        {Array.from({ length: 4 }).map((_, index) => (
          <Skeleton key={index} className="h-56 w-full" />
        ))}
      </div>
    )
  }

  const changeCount = Object.keys(dirty).length

  return (
    <>
      <PageHeader
        eyebrow="Website"
        title="Site settings"
        lead="Brand, theme, contact details and SEO defaults — everything the site reads that is not page content."
        actions={
          <Button size="sm" icon="check" onClick={() => void submit()} loading={save.isPending} disabled={changeCount === 0}>
            {changeCount > 0 ? `Save ${changeCount} change${changeCount === 1 ? '' : 's'}` : 'Saved'}
          </Button>
        }
      />

      {error && (
        <div className="mb-5">
          <InlineError>{error}</InlineError>
        </div>
      )}

      <div className="space-y-5">
        {groups.map(([group, rows]) => (
          <Panel
            key={group}
            title={group}
            description={
              group.toLowerCase() === 'theme'
                ? 'Every Tailwind colour resolves through these custom properties, so a change here repaints the whole site.'
                : undefined
            }
          >
            <div className="grid gap-5 sm:grid-cols-2">
              {(rows ?? []).map((setting) => {
                const value = valueOf(setting.key, setting.value)
                const changed = dirty[setting.key] !== undefined && dirty[setting.key] !== setting.value
                const label = (
                  <span className="flex items-center gap-2">
                    {setting.label || humanise(setting.key.split('.').pop() ?? setting.key)}
                    {changed && <Pill tone="warn">unsaved</Pill>}
                    {!setting.isPublic && <Pill tone="muted">private</Pill>}
                  </span>
                )

                if (setting.valueType === 'boolean') {
                  return (
                    <div key={setting.key} className="rounded-[0.625rem] border border-[var(--hairline)] px-4 py-3">
                      <Toggle
                        label={setting.label || setting.key}
                        hint={setting.helpText ?? undefined}
                        checked={value === 'true' || value === '1'}
                        onChange={(next) => set(setting.key, String(next))}
                      />
                    </div>
                  )
                }

                if (setting.valueType === 'color') {
                  return (
                    <div key={setting.key}>
                      <p className="mb-1.5 text-[0.8125rem] font-medium">{label}</p>
                      <div className="flex items-center gap-3">
                        <input
                          type="color"
                          value={/^#[0-9a-f]{6}$/i.test(value) ? value : '#000000'}
                          onChange={(event) => set(setting.key, event.target.value)}
                          aria-label={setting.label}
                          className="size-10 shrink-0 cursor-pointer rounded-[0.5rem] border border-[var(--hairline-strong)] bg-transparent p-1"
                        />
                        <input
                          value={value}
                          onChange={(event) => set(setting.key, event.target.value)}
                          aria-label={`${setting.label} hex`}
                          className="numeric h-10 w-full rounded-[0.625rem] border border-[var(--hairline-strong)] bg-[color-mix(in_srgb,var(--bone)_4%,var(--carbon))] px-3 text-[0.875rem] uppercase focus:border-accent focus:outline-none focus:ring-[3px] focus:ring-[var(--accent-soft)]"
                        />
                      </div>
                      {setting.helpText && <p className="mt-1.5 text-[0.75rem] text-smoke">{setting.helpText}</p>}
                    </div>
                  )
                }

                if (setting.valueType === 'image') {
                  return (
                    <div key={setting.key}>
                      <MediaPicker
                        label={setting.label || setting.key}
                        hint={setting.helpText ?? undefined}
                        value={value}
                        onChange={(next) => set(setting.key, next)}
                        folder="brand"
                      />
                    </div>
                  )
                }

                if (setting.valueType === 'textarea') {
                  return (
                    <TextAreaField
                      key={setting.key}
                      label={setting.label || setting.key}
                      hint={setting.helpText ?? undefined}
                      rows={3}
                      className="sm:col-span-2"
                      value={value}
                      onChange={(event) => set(setting.key, event.target.value)}
                    />
                  )
                }

                return (
                  <TextField
                    key={setting.key}
                    label={setting.label || setting.key}
                    hint={setting.helpText ?? undefined}
                    type={setting.valueType === 'number' ? 'number' : setting.valueType === 'url' ? 'url' : 'text'}
                    value={value}
                    onChange={(event) => set(setting.key, event.target.value)}
                  />
                )
              })}
            </div>
          </Panel>
        ))}
      </div>

      <div className="mt-6">
        <Hint icon="lock">
          Keys marked private are never sent to the public site — they exist for invoices, integrations and internal
          reference only.
        </Hint>
      </div>
    </>
  )
}

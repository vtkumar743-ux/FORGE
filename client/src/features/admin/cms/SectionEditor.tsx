import { useMemo, useState } from 'react'
import type { z } from 'zod'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { cn } from '@/lib/utils'
import { MediaPicker } from '../components/MediaPicker'
import { Drawer } from '../components/overlays'
import { InlineError, Hint, SelectField, TextAreaField, TextField, Toggle } from '../components/ui'
import { describeErrorText } from '../lib/format'
import { useSaveSection, useSectionAction } from '../lib/admin-api'
import type { AdminSection } from '../lib/types'
import { sectionSchemas } from './section-schemas'
import {
  blankContent,
  clean,
  describeSchema,
  emptyValue,
  humanise,
  validateContent,
  type FieldSpec,
  type ValidationIssue,
} from './zod-fields'

/**
 * The structured editor for one CMS section.
 *
 * Fields are generated from the section's own Zod shape, so the form and the public
 * renderer can never disagree about what a section contains. Nothing is saved until
 * the edited object passes that same shape — the API stores whatever JSON it is
 * handed, so this is the gate that keeps a bad edit off the site.
 */
export function SectionEditor({
  pageId,
  section,
  open,
  onClose,
  previewHref,
}: {
  pageId: number
  section: AdminSection | null
  open: boolean
  onClose: () => void
  previewHref?: string
}) {
  const save = useSaveSection()
  const { discardDraft } = useSectionAction()

  const schema = section ? sectionSchemas[section.typeName] : undefined
  const fields = useMemo(() => (schema ? describeSchema(schema) : []), [schema])

  // The draft wins in the editor: an unpublished edit is what the owner was last working on.
  const initial = useMemo(
    () => (section ? ((section.draft ?? section.content) as Record<string, unknown>) : {}),
    [section],
  )

  const [value, setValue] = useState<Record<string, unknown>>(initial)
  const [issues, setIssues] = useState<ValidationIssue[]>([])
  const [error, setError] = useState<string | null>(null)
  const [dirty, setDirty] = useState(false)
  const [signature, setSignature] = useState<number | null>(null)

  // Reset when a different section is opened, without an effect that fights the user's typing.
  if (section && signature !== section.id) {
    setSignature(section.id)
    setValue(initial)
    setIssues([])
    setError(null)
    setDirty(false)
  }

  function update(name: string, next: unknown) {
    setValue((current) => ({ ...current, [name]: next }))
    setDirty(true)
  }

  async function submit(publish: boolean) {
    if (!section || !schema) return
    setError(null)

    const candidate = clean(value)
    const result = validateContent(schema, candidate)
    if (!result.ok) {
      setIssues(result.issues)
      return
    }
    setIssues([])

    try {
      await save.mutateAsync({
        pageId,
        sectionId: section.id,
        content: result.data as Record<string, unknown>,
        publish,
      })
      setDirty(false)
      if (publish) onClose()
    } catch (cause) {
      setError(describeErrorText(cause))
    }
  }

  if (!section) return null

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={section.adminLabel}
      description={`${humanise(section.typeName)} · key "${section.key}"${
        section.branchName ? ` · ${section.branchName} only` : ''
      }`}
      width="lg"
      footer={
        <>
          {section.hasDraft && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                void discardDraft.mutateAsync({ pageId, sectionId: section.id }).then(onClose)
              }}
            >
              Discard draft
            </Button>
          )}
          <div className="flex-1" />
          <Button variant="outline" size="sm" onClick={() => void submit(false)} loading={save.isPending}>
            Save draft
          </Button>
          <Button size="sm" icon="check" onClick={() => void submit(true)} loading={save.isPending}>
            Publish
          </Button>
        </>
      }
    >
      {!schema ? (
        <Hint icon="sparkles">
          <p>
            <strong>{section.typeName}</strong> has no registered shape, so it cannot be edited safely here.
            Add it to <code>features/public/sections/schemas.ts</code> and it will appear as a form.
          </p>
        </Hint>
      ) : (
        <div className="space-y-6">
          {section.hasDraft && (
            <Hint icon="clock">
              This section has unpublished changes. The public site is still showing the last published
              version until you press Publish.
            </Hint>
          )}

          {issues.length > 0 && (
            <InlineError>
              <span className="font-medium">This section will not render yet.</span>
              <ul className="mt-1.5 space-y-0.5">
                {issues.map((issue) => (
                  <li key={`${issue.path}-${issue.message}`}>
                    <code className="text-accent-hot/90">{issue.path}</code> — {issue.message}
                  </li>
                ))}
              </ul>
            </InlineError>
          )}
          {error && <InlineError>{error}</InlineError>}

          <div className="space-y-5">
            {fields.map((field) => (
              <FieldControl
                key={field.name}
                field={field}
                value={(value as Record<string, unknown>)[field.name]}
                onChange={(next) => update(field.name, next)}
              />
            ))}
          </div>

          {previewHref && (
            <div className="border-t border-[var(--hairline)] pt-5">
              <a
                href={previewHref}
                target="_blank"
                rel="noreferrer noopener"
                className="inline-flex items-center gap-2 text-[0.8125rem] text-accent underline-offset-4 hover:underline"
              >
                <Icon name="arrow-up-right" size={15} />
                Open the live page in a new tab
              </a>
              {dirty && (
                <p className="mt-1.5 text-[0.75rem] text-smoke">
                  Save the draft first — preview reads published content.
                </p>
              )}
            </div>
          )}
        </div>
      )}
    </Drawer>
  )
}

/* ---------------------------------------------------------------- controls */

function FieldControl({
  field,
  value,
  onChange,
  compact,
}: {
  field: FieldSpec
  value: unknown
  onChange: (next: unknown) => void
  compact?: boolean
}) {
  switch (field.kind) {
    case 'media':
      return (
        <MediaPicker
          label={field.label}
          hint={field.hint}
          value={(value as string) ?? ''}
          onChange={onChange}
        />
      )

    case 'textarea':
      return (
        <TextAreaField
          label={field.label}
          hint={field.hint}
          rows={field.rows ?? 4}
          value={(value as string) ?? ''}
          onChange={(event) => onChange(event.target.value)}
        />
      )

    case 'number':
      return (
        <TextField
          label={field.label}
          hint={field.hint}
          type="number"
          step="any"
          value={value === undefined || value === null ? '' : String(value)}
          onChange={(event) => onChange(event.target.value === '' ? undefined : Number(event.target.value))}
        />
      )

    case 'boolean':
      return (
        <div className="rounded-[0.625rem] border border-[var(--hairline)] px-4 py-3">
          <Toggle
            label={field.label}
            hint={field.hint}
            checked={Boolean(value)}
            onChange={onChange}
          />
        </div>
      )

    case 'select':
      return (
        <SelectField
          label={field.label}
          hint={field.hint}
          value={(value as string) ?? field.options?.[0] ?? ''}
          onChange={(event) => onChange(event.target.value)}
        >
          {field.optional && <option value="">Not set</option>}
          {(field.options ?? []).map((option) => (
            <option key={option} value={option}>
              {humanise(option)}
            </option>
          ))}
        </SelectField>
      )

    case 'stringList':
      return <StringListControl field={field} value={(value as string[]) ?? []} onChange={onChange} />

    case 'object':
      return (
        <fieldset className="rounded-[0.625rem] border border-[var(--hairline)] p-4">
          <legend className="px-1 text-[0.8125rem] font-medium text-bone">{field.label}</legend>
          {field.hint && <p className="mb-3 text-[0.75rem] text-smoke">{field.hint}</p>}
          <div className="grid gap-4 sm:grid-cols-2">
            {(field.children ?? []).map((child) => (
              <FieldControl
                key={child.name}
                field={child}
                compact
                value={(value as Record<string, unknown> | undefined)?.[child.name]}
                onChange={(next) =>
                  onChange({ ...((value as Record<string, unknown>) ?? {}), [child.name]: next })
                }
              />
            ))}
          </div>
        </fieldset>
      )

    case 'objectList':
      return <RepeaterControl field={field} value={(value as Record<string, unknown>[]) ?? []} onChange={onChange} />

    default:
      return (
        <TextAreaField
          label={`${field.label} (raw JSON)`}
          hint="This property has no generated control, so it is edited as JSON."
          rows={compact ? 3 : 5}
          value={value === undefined ? '' : JSON.stringify(value, null, 2)}
          onChange={(event) => {
            try {
              onChange(event.target.value ? JSON.parse(event.target.value) : undefined)
            } catch {
              // Keep the keystroke; validation on save reports anything still malformed.
            }
          }}
        />
      )
  }
}

function StringListControl({
  field,
  value,
  onChange,
}: {
  field: FieldSpec
  value: string[]
  onChange: (next: string[]) => void
}) {
  return (
    <div>
      <p className="mb-1.5 text-[0.8125rem] font-medium text-bone">{field.label}</p>
      {field.hint && <p className="mb-2 text-[0.75rem] leading-relaxed text-smoke">{field.hint}</p>}

      <div className="space-y-2">
        {value.map((item, index) => (
          <div key={index} className="flex items-center gap-2">
            <input
              value={item}
              onChange={(event) => {
                const next = [...value]
                next[index] = event.target.value
                onChange(next)
              }}
              className="h-9 w-full rounded-[0.625rem] border border-[var(--hairline-strong)] bg-[color-mix(in_srgb,var(--bone)_4%,var(--carbon))] px-3 text-[0.875rem] text-bone focus:border-accent focus:outline-none focus:ring-[3px] focus:ring-[var(--accent-soft)]"
            />
            <button
              type="button"
              onClick={() => onChange(value.filter((_, i) => i !== index))}
              className="shrink-0 rounded-full p-1.5 text-smoke transition-colors hover:text-accent-hot"
              aria-label={`Remove item ${index + 1}`}
            >
              <Icon name="x" size={15} />
            </button>
          </div>
        ))}
      </div>

      <Button variant="ghost" size="sm" icon="plus" className="mt-2" onClick={() => onChange([...value, ''])}>
        Add
      </Button>
    </div>
  )
}

function RepeaterControl({
  field,
  value,
  onChange,
}: {
  field: FieldSpec
  value: Record<string, unknown>[]
  onChange: (next: Record<string, unknown>[]) => void
}) {
  const [openIndex, setOpenIndex] = useState<number | null>(0)
  const children = field.children ?? []

  function move(index: number, direction: -1 | 1) {
    const target = index + direction
    if (target < 0 || target >= value.length) return
    const next = [...value]
    ;[next[index], next[target]] = [next[target], next[index]]
    onChange(next)
    setOpenIndex(target)
  }

  return (
    <div>
      <div className="mb-2 flex items-center justify-between gap-3">
        <div>
          <p className="text-[0.8125rem] font-medium text-bone">{field.label}</p>
          {field.hint && <p className="mt-1 text-[0.75rem] leading-relaxed text-smoke">{field.hint}</p>}
        </div>
        <span className="numeric text-[0.75rem] text-smoke">{value.length}</span>
      </div>

      <div className="space-y-2">
        {value.map((item, index) => {
          // The first text-ish property doubles as the row's title in the collapsed list.
          const titleField = children.find((child) => child.kind === 'text' || child.kind === 'textarea')
          const title = titleField ? String(item[titleField.name] ?? '') : ''
          const isOpen = openIndex === index

          return (
            <div key={index} className="overflow-hidden rounded-[0.625rem] border border-[var(--hairline)]">
              <div className="flex items-center gap-1 bg-[color-mix(in_srgb,var(--bone)_3%,transparent)] px-3 py-2">
                <button
                  type="button"
                  onClick={() => setOpenIndex(isOpen ? null : index)}
                  className="flex min-w-0 flex-1 items-center gap-2 text-left"
                  aria-expanded={isOpen}
                >
                  <Icon
                    name="chevron-down"
                    size={15}
                    className={cn('shrink-0 text-smoke transition-transform', !isOpen && '-rotate-90')}
                  />
                  <span className="numeric text-[0.6875rem] text-smoke">{index + 1}</span>
                  <span className="truncate text-[0.8125rem] text-bone">{title || `Item ${index + 1}`}</span>
                </button>
                <button
                  type="button"
                  onClick={() => move(index, -1)}
                  disabled={index === 0}
                  className="rounded-full p-1 text-smoke transition-colors hover:text-bone disabled:opacity-30"
                  aria-label="Move up"
                >
                  <Icon name="chevron-left" size={14} className="rotate-90" />
                </button>
                <button
                  type="button"
                  onClick={() => move(index, 1)}
                  disabled={index === value.length - 1}
                  className="rounded-full p-1 text-smoke transition-colors hover:text-bone disabled:opacity-30"
                  aria-label="Move down"
                >
                  <Icon name="chevron-right" size={14} className="rotate-90" />
                </button>
                <button
                  type="button"
                  onClick={() => {
                    onChange(value.filter((_, i) => i !== index))
                    setOpenIndex(null)
                  }}
                  className="rounded-full p-1 text-smoke transition-colors hover:text-accent-hot"
                  aria-label="Remove"
                >
                  <Icon name="x" size={14} />
                </button>
              </div>

              {isOpen && (
                <div className="grid gap-4 border-t border-[var(--hairline)] p-4 sm:grid-cols-2">
                  {children.map((child) => (
                    <div key={child.name} className={child.kind === 'textarea' || child.kind === 'objectList' ? 'sm:col-span-2' : undefined}>
                      <FieldControl
                        field={child}
                        compact
                        value={item[child.name]}
                        onChange={(next) => {
                          const updated = [...value]
                          updated[index] = { ...item, [child.name]: next }
                          onChange(updated)
                        }}
                      />
                    </div>
                  ))}
                </div>
              )}
            </div>
          )
        })}
      </div>

      <Button
        variant="ghost"
        size="sm"
        icon="plus"
        className="mt-2"
        onClick={() => {
          onChange([...value, blankRow(children)])
          setOpenIndex(value.length)
        }}
      >
        Add {field.label.replace(/s$/, '').toLowerCase()}
      </Button>
    </div>
  )
}

function blankRow(children: FieldSpec[]): Record<string, unknown> {
  return Object.fromEntries(
    children.filter((child) => !(child.optional && child.defaultValue === undefined))
      .map((child) => [child.name, emptyValue(child)]),
  )
}

/** Exported for the "add section" flow, which needs a valid starting object per type. */
export function defaultContentFor(typeName: string): Record<string, unknown> {
  const schema = sectionSchemas[typeName] as z.ZodTypeAny | undefined
  return schema ? blankContent(describeSchema(schema)) : {}
}

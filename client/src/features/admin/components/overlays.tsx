import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { createPortal } from 'react-dom'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import { Button } from '@/components/ui/Button'
import { Icon } from '@/components/ui/Icon'
import { cn } from '@/lib/utils'

/* ============================================================================
   Overlays: toasts, the edit drawer and the confirm dialog.

   All three respect prefers-reduced-motion by collapsing to a cross-fade, and
   all three trap focus and close on Escape — an admin panel is a keyboard tool.
   ============================================================================ */

type ToastTone = 'success' | 'error' | 'info'

interface Toast {
  id: number
  tone: ToastTone
  title: string
  body?: string
}

interface ToastApi {
  push: (toast: Omit<Toast, 'id'>) => void
  success: (title: string, body?: string) => void
  error: (title: string, body?: string) => void
  info: (title: string, body?: string) => void
}

const ToastContext = createContext<ToastApi | null>(null)

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(1)

  const push = useCallback((toast: Omit<Toast, 'id'>) => {
    const id = nextId.current++
    setToasts((current) => [...current, { ...toast, id }])
    // Errors linger; confirmations get out of the way.
    const ttl = toast.tone === 'error' ? 8000 : 4000
    window.setTimeout(() => setToasts((current) => current.filter((t) => t.id !== id)), ttl)
  }, [])

  const api = useMemo<ToastApi>(
    () => ({
      push,
      success: (title, body) => push({ tone: 'success', title, body }),
      error: (title, body) => push({ tone: 'error', title, body }),
      info: (title, body) => push({ tone: 'info', title, body }),
    }),
    [push],
  )

  return (
    <ToastContext.Provider value={api}>
      {children}
      {createPortal(
        <div
          className="pointer-events-none fixed bottom-6 right-6 z-[var(--z-toast)] flex w-[min(24rem,calc(100vw-3rem))] flex-col gap-2.5"
          role="region"
          aria-label="Notifications"
        >
          <AnimatePresence initial={false}>
            {toasts.map((toast) => (
              <motion.div
                key={toast.id}
                layout
                initial={{ opacity: 0, y: 12, scale: 0.98 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0, y: 8, scale: 0.98 }}
                transition={{ duration: 0.22, ease: [0.16, 1, 0.3, 1] }}
                role="status"
                aria-live={toast.tone === 'error' ? 'assertive' : 'polite'}
                className={cn(
                  'pointer-events-auto flex items-start gap-3 rounded-[var(--radius-card)] border px-4 py-3.5',
                  'bg-carbon shadow-[0_18px_48px_-24px_rgb(0_0_0/0.45)]',
                  toast.tone === 'success' && 'border-success/45',
                  toast.tone === 'error' && 'border-accent-hot/50',
                  toast.tone === 'info' && 'border-[var(--accent-line)]',
                )}
              >
                <Icon
                  name={toast.tone === 'success' ? 'check' : toast.tone === 'error' ? 'x' : 'sparkles'}
                  size={16}
                  className={cn(
                    'mt-0.5 shrink-0',
                    toast.tone === 'success' && 'text-success',
                    toast.tone === 'error' && 'text-accent-hot',
                    toast.tone === 'info' && 'text-accent',
                  )}
                />
                <div className="min-w-0 flex-1">
                  <p className="text-[0.875rem] font-medium text-bone">{toast.title}</p>
                  {toast.body && <p className="mt-1 text-[0.8125rem] leading-relaxed text-smoke">{toast.body}</p>}
                </div>
                <button
                  type="button"
                  onClick={() => setToasts((current) => current.filter((t) => t.id !== toast.id))}
                  className="-mr-1 -mt-1 shrink-0 rounded-full p-1 text-smoke transition-colors hover:text-bone"
                  aria-label="Dismiss"
                >
                  <Icon name="x" size={14} />
                </button>
              </motion.div>
            ))}
          </AnimatePresence>
        </div>,
        document.body,
      )}
    </ToastContext.Provider>
  )
}

export function useToast(): ToastApi {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast must be used inside <ToastProvider>.')
  return context
}

/* ---------------------------------------------------------------- drawer */

/**
 * The edit surface for everything in the panel. A drawer rather than a route so the list
 * behind it keeps its scroll position, filters and selection while a row is edited.
 */
export function Drawer({
  open,
  onClose,
  title,
  description,
  footer,
  width = 'md',
  children,
}: {
  open: boolean
  onClose: () => void
  title: string
  description?: string
  footer?: ReactNode
  width?: 'md' | 'lg' | 'xl'
  children: ReactNode
}) {
  const reduced = useReducedMotion()
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const previous = document.activeElement as HTMLElement | null
    document.body.style.overflow = 'hidden'

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
      if (event.key !== 'Tab' || !panelRef.current) return

      // Focus stays inside the sheet: tabbing out of a modal edit form loses the edit.
      const focusable = panelRef.current.querySelectorAll<HTMLElement>(
        'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])',
      )
      if (focusable.length === 0) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', onKeyDown)
    window.setTimeout(() => panelRef.current?.querySelector<HTMLElement>('input,select,textarea,button')?.focus(), 60)

    return () => {
      document.removeEventListener('keydown', onKeyDown)
      document.body.style.overflow = ''
      previous?.focus()
    }
  }, [open, onClose])

  const widths = { md: 'max-w-xl', lg: 'max-w-3xl', xl: 'max-w-5xl' } as const

  return createPortal(
    <AnimatePresence>
      {open && (
        <div className="fixed inset-0 z-[var(--z-overlay)] flex justify-end" role="dialog" aria-modal aria-label={title}>
          <motion.button
            type="button"
            aria-label="Close"
            onClick={onClose}
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="absolute inset-0 cursor-default bg-black/45 backdrop-blur-[2px]"
          />
          <motion.div
            ref={panelRef}
            initial={reduced ? { opacity: 0 } : { x: '100%' }}
            animate={reduced ? { opacity: 1 } : { x: 0 }}
            exit={reduced ? { opacity: 0 } : { x: '100%' }}
            transition={{ duration: 0.3, ease: [0.16, 1, 0.3, 1] }}
            className={cn(
              'relative flex h-full w-full flex-col border-l border-[var(--hairline)] bg-[var(--ink)]',
              widths[width],
            )}
          >
            <header className="flex items-start justify-between gap-4 border-b border-[var(--hairline)] px-6 py-5">
              <div className="min-w-0">
                <h2 className="display-m text-[1.25rem] leading-none">{title}</h2>
                {description && (
                  <p className="mt-2 text-[0.8125rem] leading-relaxed text-smoke">{description}</p>
                )}
              </div>
              <button
                type="button"
                onClick={onClose}
                className="-mr-1 shrink-0 rounded-full p-1.5 text-smoke transition-colors hover:text-bone"
                aria-label="Close"
              >
                <Icon name="x" size={18} />
              </button>
            </header>

            <div className="min-h-0 flex-1 overflow-y-auto px-6 py-6">{children}</div>

            {footer && (
              <footer className="flex items-center justify-end gap-2.5 border-t border-[var(--hairline)] px-6 py-4">
                {footer}
              </footer>
            )}
          </motion.div>
        </div>
      )}
    </AnimatePresence>,
    document.body,
  )
}

/* ---------------------------------------------------------------- confirm */

export function ConfirmDialog({
  open,
  onClose,
  onConfirm,
  title,
  body,
  confirmLabel = 'Confirm',
  tone = 'default',
  loading,
}: {
  open: boolean
  onClose: () => void
  onConfirm: () => void
  title: string
  body: string
  confirmLabel?: string
  tone?: 'default' | 'danger'
  loading?: boolean
}) {
  useEffect(() => {
    if (!open) return
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [open, onClose])

  return createPortal(
    <AnimatePresence>
      {open && (
        <div
          className="fixed inset-0 z-[var(--z-overlay)] flex items-center justify-center p-6"
          role="alertdialog"
          aria-modal
          aria-label={title}
        >
          <motion.button
            type="button"
            aria-label="Cancel"
            onClick={onClose}
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="absolute inset-0 cursor-default bg-black/50 backdrop-blur-[2px]"
          />
          <motion.div
            initial={{ opacity: 0, scale: 0.97, y: 8 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.97, y: 8 }}
            transition={{ duration: 0.22, ease: [0.16, 1, 0.3, 1] }}
            className="relative w-full max-w-md rounded-[var(--radius-sheet)] border border-[var(--hairline)] bg-[var(--ink)] p-6"
          >
            <h2 className="display-m text-[1.25rem] leading-none">{title}</h2>
            <p className="mt-3 text-[0.875rem] leading-relaxed text-smoke">{body}</p>
            <div className="mt-7 flex justify-end gap-2.5">
              <Button variant="outline" size="sm" onClick={onClose} disabled={loading}>
                Cancel
              </Button>
              <Button
                variant={tone === 'danger' ? 'danger' : 'primary'}
                size="sm"
                onClick={onConfirm}
                loading={loading}
              >
                {confirmLabel}
              </Button>
            </div>
          </motion.div>
        </div>
      )}
    </AnimatePresence>,
    document.body,
  )
}

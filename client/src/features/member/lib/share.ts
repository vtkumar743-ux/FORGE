/**
 * Share a line of text: the native sheet where the browser has one, the clipboard
 * everywhere else. Never a button that does nothing — a dead share control is the
 * fastest way to make an app feel unfinished.
 */
export function shareText(text: string, url?: string): void {
  if (typeof navigator === 'undefined') return

  const canShare = typeof (navigator as Navigator).share === 'function'
  if (canShare) {
    void (navigator as Navigator).share({ text, url }).catch(() => copy(text, url))
    return
  }
  copy(text, url)
}

function copy(text: string, url?: string): void {
  const payload = url ? `${text} ${url}` : text
  void navigator.clipboard?.writeText(payload).catch(() => undefined)
}

/**
 * The WhatsApp share card (Module 4.5). WhatsApp is where this actually gets sent in India,
 * so it gets its own control rather than hiding behind the generic share sheet.
 */
export function shareToWhatsApp(text: string): void {
  window.open(`https://wa.me/?text=${encodeURIComponent(text)}`, '_blank', 'noopener,noreferrer')
}

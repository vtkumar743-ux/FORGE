import { useState } from 'react'
import { QRCodeSVG } from 'qrcode.react'
import { Reveal } from '@/components/ui/Reveal'
import { Icon } from '@/components/ui/Icon'
import { ButtonLink } from '@/components/ui/Button'
import { setting, useSiteSettings } from '@/lib/cms'
import type { AppQrContent } from './schemas'

/**
 * App and QR section (Module 1.1). The QR is generated as an inline SVG at render time —
 * no image request, no icon font, and it re-encodes itself the moment the owner changes
 * the store URL in site settings rather than needing a designer to re-export a PNG.
 */
export function AppQrSection({ content }: { content: AppQrContent }) {
  const { data: settings } = useSiteSettings()
  // The screenshot is the one asset that cannot be stock — until the client supplies a
  // real capture, the frame draws the screen from live tokens instead of showing a
  // broken image. It is sharper than a PNG would be and it restyles with the brand.
  const [screenshotFailed, setScreenshotFailed] = useState(false)
  const useDrawnScreen = !content.screenshotUrl || screenshotFailed

  // Point at whichever store the owner has configured; the member portal is the
  // honest fallback, since it is the thing that definitely exists today.
  const target =
    content.qrTarget ??
    setting(settings, 'app.playStoreUrl') ??
    (typeof window === 'undefined' ? '/portal' : `${window.location.origin}/portal`)

  const playStore = setting(settings, 'app.playStoreUrl')
  const appStore = setting(settings, 'app.appStoreUrl')

  return (
    <section className="section-y bg-ink">
      <div className="shell grid items-center gap-14 lg:grid-cols-12">
        <div className="lg:col-span-6">
          {content.eyebrow && (
            <Reveal>
              <p className="caption">{content.eyebrow}</p>
            </Reveal>
          )}

          <Reveal delay={0.05}>
            <h2 className="display-l mt-5 text-bone">{content.headline}</h2>
          </Reveal>

          {content.body && (
            <Reveal delay={0.1}>
              <p className="measure mt-6 text-body-l leading-relaxed text-smoke">{content.body}</p>
            </Reveal>
          )}

          {content.bullets.length > 0 && (
            <Reveal delay={0.15}>
              <ul className="mt-8 grid gap-3 sm:grid-cols-2">
                {content.bullets.map((bullet) => (
                  <li key={bullet} className="flex items-start gap-2.5 text-[0.9375rem] leading-snug text-bone/85">
                    <Icon name="check" size={16} strokeWidth={2} className="mt-0.5 shrink-0 text-accent" />
                    {bullet}
                  </li>
                ))}
              </ul>
            </Reveal>
          )}

          <Reveal delay={0.2}>
            <div className="mt-10 flex flex-wrap items-center gap-8">
              <figure className="flex flex-col items-center">
                <div className="rounded-[var(--radius-card)] border border-[var(--hairline-strong)] bg-bone p-3">
                  <QRCodeSVG
                    value={target}
                    size={124}
                    level="M"
                    bgColor="#F5F3EE"
                    fgColor="#0A0A0A"
                    title="Scan to install the FORGE member app"
                  />
                </div>
                <figcaption className="caption mt-3 text-[0.625rem]">{content.qrCaption}</figcaption>
              </figure>

              <div className="flex flex-col gap-3">
                {playStore && (
                  <ButtonLink href={playStore} target="_blank" variant="outline" size="sm" icon="arrow-up-right" iconAfter>
                    Google Play
                  </ButtonLink>
                )}
                {appStore && (
                  <ButtonLink href={appStore} target="_blank" variant="outline" size="sm" icon="arrow-up-right" iconAfter>
                    App Store
                  </ButtonLink>
                )}
                <ButtonLink to="/portal" variant="ghost" size="sm" icon="arrow-right" iconAfter>
                  Or use it in the browser
                </ButtonLink>
              </div>
            </div>
          </Reveal>
        </div>

        <Reveal delay={0.12} distance={36} className="lg:col-span-6">
          <div className="relative mx-auto max-w-[19rem]">
            {/* A drawn device frame rather than a mock-up image: it stays crisp at any
                density and costs one border instead of a 400 kB PNG. */}
            <div className="rounded-[2.25rem] border border-[var(--hairline-strong)] bg-carbon p-2.5 shadow-[0_60px_120px_-60px_rgba(0,0,0,0.9)]">
              <div className="relative overflow-hidden rounded-[1.75rem] bg-ink">
                <span
                  aria-hidden
                  className="absolute left-1/2 top-2.5 z-10 h-5 w-24 -translate-x-1/2 rounded-full bg-carbon"
                />
                {useDrawnScreen ? (
                  <AppHomeScreen />
                ) : (
                  <img
                    src={content.screenshotUrl}
                    alt={content.screenshotAlt ?? ''}
                    loading="lazy"
                    decoding="async"
                    onError={() => setScreenshotFailed(true)}
                    className="aspect-[9/19.5] w-full object-cover"
                  />
                )}
              </div>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  )
}

/**
 * The member app's home screen, drawn from the same tokens the real portal uses: today's
 * booked class, the streak counter and live branch occupancy — the three things the copy
 * beside it promises. Not a mock-up image, so it cannot drift out of date with the palette.
 */
function AppHomeScreen() {
  return (
    <div className="aspect-[9/19.5] w-full bg-ink px-5 pb-6 pt-12" aria-hidden>
      <p className="caption text-[0.5rem]">Thursday</p>
      <p className="display-m mt-1 text-[1.125rem] text-bone">Good morning, Nandini</p>

      <div className="mt-5 rounded-[var(--radius-card)] border border-[var(--accent-line)] bg-carbon p-3.5">
        <p className="caption text-[0.5rem] text-accent">Booked · 6:30 AM</p>
        <p className="mt-1.5 text-[0.8125rem] text-bone">Strength Foundations</p>
        <p className="mt-0.5 text-[0.625rem] text-smoke">Karthik Reddy · Koramangala</p>
      </div>

      <div className="mt-3 grid grid-cols-2 gap-3">
        <div className="rounded-[var(--radius-card)] border border-[var(--hairline)] bg-carbon p-3.5">
          <p className="caption text-[0.5rem]">Streak</p>
          <p className="numeric display-m mt-1 text-[1.375rem] text-accent">18</p>
          <p className="text-[0.5625rem] text-smoke">days</p>
        </div>
        <div className="rounded-[var(--radius-card)] border border-[var(--hairline)] bg-carbon p-3.5">
          <p className="caption text-[0.5rem]">Floor now</p>
          <p className="numeric display-m mt-1 text-[1.375rem] text-success">41%</p>
          <p className="text-[0.5625rem] text-smoke">comfortable</p>
        </div>
      </div>

      <div className="mt-3 rounded-[var(--radius-card)] border border-[var(--hairline)] bg-carbon p-3.5">
        <p className="caption text-[0.5rem]">This block</p>
        <div className="mt-2.5 space-y-2">
          {[
            ['Back squat', '4 × 5 · 62.5 kg'],
            ['Romanian deadlift', '3 × 8 · 50 kg'],
            ['Pull-up', '3 × max'],
          ].map(([move, prescription]) => (
            <div key={move} className="flex items-center justify-between gap-2">
              <span className="text-[0.6875rem] text-bone/85">{move}</span>
              <span className="numeric text-[0.625rem] text-smoke">{prescription}</span>
            </div>
          ))}
        </div>
      </div>

      <div className="mt-3 flex items-center gap-2 rounded-full bg-accent px-4 py-2.5">
        <span className="text-[0.6875rem] font-medium text-ink">Scan to enter</span>
      </div>
    </div>
  )
}

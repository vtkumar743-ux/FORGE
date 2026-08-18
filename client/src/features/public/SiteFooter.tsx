import { Link } from 'react-router-dom'
import { Icon, isIconName } from '@/components/ui/Icon'
import { setting, useSiteSettings } from '@/lib/cms'
import { whatsappLink } from '@/lib/utils'

const columns = [
  {
    heading: 'Train',
    links: [
      { label: 'Classes & timetable', to: '/classes' },
      { label: 'Our coaches', to: '/trainers' },
      { label: 'Personal training', to: '/trainers' },
      { label: 'Transformations', to: '/transformations' },
    ],
  },
  {
    heading: 'Join',
    links: [
      { label: 'Plans & pricing', to: '/plans' },
      { label: 'Book a free trial', to: '/free-trial' },
      { label: 'Corporate memberships', to: '/contact' },
      { label: 'Calculators', to: '/tools' },
    ],
  },
  {
    heading: 'About',
    links: [
      { label: 'Branches', to: '/branches' },
      { label: 'Journal', to: '/journal' },
      { label: 'Questions', to: '/faq' },
      { label: 'Contact', to: '/contact' },
    ],
  },
]

const socialKeys = [
  { key: 'social.instagram', icon: 'instagram', label: 'Instagram' },
  { key: 'social.youtube', icon: 'youtube', label: 'YouTube' },
  { key: 'social.linkedin', icon: 'linkedin', label: 'LinkedIn' },
] as const

export function SiteFooter() {
  const { data: settings } = useSiteSettings()
  const brand = setting(settings, 'brand.name', 'FORGE')
  const branches = settings?.branches ?? []
  const year = new Date().getFullYear()

  return (
    <footer className="grain relative border-t border-[var(--hairline)] bg-ink">
      <div className="shell relative z-10 pt-20 pb-10">
        {/* Oversized wordmark — one big type moment per screen (03 §9.3). */}
        <div className="hairline-b pb-12">
          <p className="display-l text-outline select-none text-[clamp(3rem,12vw,9rem)] leading-[0.85]">
            {brand}
          </p>
          <p className="measure mt-6 text-[0.9375rem] leading-relaxed text-smoke">
            {setting(
              settings,
              'brand.tagline',
              'Strength, built in Bengaluru.',
            )}{' '}
            Three floors, eight full-time coaches, forty coached classes a week.
          </p>
        </div>

        {/* Per-branch detail, straight from the Branches table (never hardcoded). */}
        <div className="hairline-b grid gap-10 py-12 md:grid-cols-3">
          {branches.map((branch) => (
            <div key={branch.id}>
              <h3 className="display-m text-[1.125rem] text-bone">
                {branch.name.replace(`${brand} `, '')}
              </h3>
              <address className="mt-3 space-y-1 text-[0.875rem] not-italic leading-relaxed text-smoke">
                <p>{branch.addressLine1}</p>
                {branch.addressLine2 && <p>{branch.addressLine2}</p>}
                <p>
                  {branch.city} {branch.pincode}
                </p>
              </address>
              <dl className="mt-4 space-y-1.5 text-[0.8125rem] text-smoke">
                <div className="flex gap-2">
                  <dt className="sr-only">Weekdays</dt>
                  <Icon name="clock" size={14} className="mt-0.5 text-bone/40" />
                  <dd className="numeric">
                    Mon–Fri {branch.weekdayHours} · Sat–Sun {branch.weekendHours}
                  </dd>
                </div>
                <div className="flex gap-2">
                  <dt className="sr-only">Phone</dt>
                  <Icon name="phone" size={14} className="mt-0.5 text-bone/40" />
                  <dd>
                    <a href={`tel:${branch.phone}`} className="transition-colors hover:text-accent">
                      {branch.phone}
                    </a>
                  </dd>
                </div>
              </dl>
              <Link
                to={`/branches/${branch.slug}`}
                className="group mt-4 inline-flex items-center gap-1.5 text-[0.8125rem] text-accent"
              >
                <span className="underline-slide">Visit this branch</span>
                <Icon
                  name="arrow-right"
                  size={14}
                  className="transition-transform duration-200 group-hover:translate-x-0.5"
                />
              </Link>
            </div>
          ))}
        </div>

        <div className="hairline-b grid gap-10 py-12 sm:grid-cols-2 lg:grid-cols-4">
          {columns.map((column) => (
            <nav key={column.heading} aria-label={column.heading}>
              <h3 className="caption mb-4">{column.heading}</h3>
              <ul className="space-y-2.5">
                {column.links.map((link) => (
                  <li key={link.label}>
                    <Link
                      to={link.to}
                      className="text-[0.9375rem] text-bone/75 transition-colors duration-200 hover:text-accent"
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </nav>
          ))}

          <div>
            <h3 className="caption mb-4">Reach us</h3>
            <ul className="space-y-2.5 text-[0.9375rem]">
              <li>
                <a
                  href={whatsappLink(
                    setting(settings, 'contact.whatsapp'),
                    setting(settings, 'contact.whatsappPrefill'),
                  )}
                  target="_blank"
                  rel="noreferrer noopener"
                  className="inline-flex items-center gap-2 text-bone/75 transition-colors hover:text-accent"
                >
                  <Icon name="phone" size={15} />
                  WhatsApp
                </a>
              </li>
              <li>
                <a
                  href={`mailto:${setting(settings, 'contact.email', 'hello@forgestrength.in')}`}
                  className="inline-flex items-center gap-2 text-bone/75 transition-colors hover:text-accent"
                >
                  <Icon name="mail" size={15} />
                  {setting(settings, 'contact.email', 'hello@forgestrength.in')}
                </a>
              </li>
            </ul>

            <div className="mt-6 flex gap-2">
              {socialKeys.map((social) => {
                const url = setting(settings, social.key)
                if (!url || !isIconName(social.icon)) return null
                return (
                  <a
                    key={social.key}
                    href={url}
                    target="_blank"
                    rel="noreferrer noopener"
                    aria-label={social.label}
                    className="inline-flex size-10 items-center justify-center rounded-full border border-[var(--hairline)] text-bone/70 transition-colors duration-200 hover:border-accent hover:text-accent"
                  >
                    <Icon name={social.icon} size={17} />
                  </a>
                )
              })}
            </div>
          </div>
        </div>

        <div className="flex flex-col gap-4 pt-8 text-[0.75rem] text-smoke sm:flex-row sm:items-center sm:justify-between">
          <p>
            © {year} {setting(settings, 'legal.registeredName', 'Forge Strength & Performance Pvt Ltd')}
            {setting(settings, 'legal.gstin') && (
              <>
                {' · '}
                <span className="numeric">GSTIN {setting(settings, 'legal.gstin')}</span>
              </>
            )}
          </p>
          <p>All prices include 5% GST on gym services. Photography is of our own floors.</p>
        </div>
      </div>
    </footer>
  )
}

import { useEffect } from 'react'
import type { CmsSeo } from '@/lib/cms'

/**
 * Per-page meta, driven entirely from CmsPages (Module 1.12) — title, description,
 * OG image, canonical, robots and the LocalBusiness/HealthClub JSON-LD.
 * Written imperatively into <head> so there is no dependency on a helmet library.
 */
export function Seo({
  seo,
  titleSuffix = ' · FORGE Bengaluru',
}: {
  seo: CmsSeo | undefined
  titleSuffix?: string
}) {
  useEffect(() => {
    if (!seo) return

    // The CMS title is already complete; only append the suffix when it is absent.
    const title = seo.title.includes('FORGE') ? seo.title : `${seo.title}${titleSuffix}`
    document.title = title

    setMeta('name', 'description', seo.description)
    if (seo.keywords) setMeta('name', 'keywords', seo.keywords)
    setMeta('name', 'robots', seo.noIndex ? 'noindex,nofollow' : 'index,follow')

    setMeta('property', 'og:title', title)
    setMeta('property', 'og:description', seo.description)
    setMeta('property', 'og:type', 'website')
    setMeta('property', 'og:url', window.location.href)
    if (seo.ogImageUrl) {
      setMeta('property', 'og:image', absolute(seo.ogImageUrl))
      setMeta('name', 'twitter:image', absolute(seo.ogImageUrl))
    }
    setMeta('name', 'twitter:card', 'summary_large_image')
    setMeta('name', 'twitter:title', title)
    setMeta('name', 'twitter:description', seo.description)

    setLink('canonical', seo.canonicalUrl ?? window.location.href.split('?')[0])
    setJsonLd(seo.structuredData)
  }, [seo, titleSuffix])

  return null
}

function setMeta(attribute: 'name' | 'property', key: string, content: string) {
  let tag = document.head.querySelector<HTMLMetaElement>(`meta[${attribute}="${key}"]`)
  if (!tag) {
    tag = document.createElement('meta')
    tag.setAttribute(attribute, key)
    document.head.appendChild(tag)
  }
  tag.content = content
}

function setLink(rel: string, href: string) {
  let tag = document.head.querySelector<HTMLLinkElement>(`link[rel="${rel}"]`)
  if (!tag) {
    tag = document.createElement('link')
    tag.rel = rel
    document.head.appendChild(tag)
  }
  tag.href = href
}

const JSON_LD_ID = 'forge-structured-data'

function setJsonLd(data: unknown) {
  const existing = document.getElementById(JSON_LD_ID)
  if (!data) {
    existing?.remove()
    return
  }

  const script = existing ?? document.createElement('script')
  script.id = JSON_LD_ID
  script.setAttribute('type', 'application/ld+json')
  script.textContent = JSON.stringify(data)
  if (!existing) document.head.appendChild(script)
}

function absolute(url: string): string {
  return url.startsWith('http') ? url : `${window.location.origin}${url}`
}

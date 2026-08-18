import { useParams } from 'react-router-dom'
import { Seo } from '@/components/Seo'
import { Skeleton, SkeletonText, EmptyState } from '@/components/ui/Skeleton'
import { setting, useCmsPage, useSiteSettings } from '@/lib/cms'
import { renderSection } from './sections/registry'
import { BranchScopeContext } from './sections/context'

/**
 * The one public page component. Every public route resolves to a CMS page slug and
 * renders whatever sections that page holds, in the order the CMS gives them. Nothing
 * about a page's content, order, or visibility lives in the client — which is what makes
 * "hardcoded marketing copy is a bug" true in practice rather than aspirationally.
 */
export function CmsRoutePage({ slug: fixedSlug }: { slug?: string }) {
  const params = useParams<{ branchSlug?: string }>()
  const slug = fixedSlug ?? 'home'
  // Branch pages ask for their own branch-scoped section variants.
  const resolvedSlug = params.branchSlug ? `branches/${params.branchSlug}` : slug

  const { data: settings } = useSiteSettings()
  const { data: page, isLoading, isError } = useCmsPage(resolvedSlug, params.branchSlug)

  if (isLoading) return <PageSkeleton />

  if (isError || !page) {
    return (
      <div className="shell section-y">
        <EmptyState
          icon="map-pin"
          headline="That page has moved"
          body="It may have been renamed in the CMS. Head back to the home page, or WhatsApp the desk and we will point you at the right one."
          actionLabel="Back to home"
          actionTo="/"
        />
      </div>
    )
  }

  return (
    <BranchScopeContext.Provider value={params.branchSlug}>
      <Seo seo={page.seo} titleSuffix={setting(settings, 'seo.titleSuffix', ' · FORGE Bengaluru')} />
      {page.sections.map((section) => (
        <div key={section.id} id={section.key}>
          {renderSection(section)}
        </div>
      ))}
    </BranchScopeContext.Provider>
  )
}

function PageSkeleton() {
  return (
    <div aria-busy="true">
      <div className="relative min-h-[min(72svh,42rem)] bg-carbon">
        <div className="shell flex h-full flex-col justify-end pb-20 pt-32">
          <Skeleton rounded="pill" className="h-3 w-52" />
          <Skeleton className="mt-6 h-[clamp(3rem,7vw,6rem)] w-full max-w-3xl" />
          <Skeleton className="mt-4 h-[clamp(3rem,7vw,6rem)] w-full max-w-xl" />
          <div className="measure mt-8">
            <SkeletonText lines={2} />
          </div>
          <div className="mt-10 flex gap-3">
            <Skeleton rounded="pill" className="h-14 w-44" />
            <Skeleton rounded="pill" className="h-14 w-36" />
          </div>
        </div>
      </div>
      <div className="shell section-y grid gap-8 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 3 }).map((_, index) => (
          <div key={index} className="space-y-4">
            <Skeleton className="aspect-[4/3] w-full" />
            <Skeleton rounded="pill" className="h-5 w-2/3" />
            <SkeletonText lines={2} />
          </div>
        ))}
      </div>
    </div>
  )
}

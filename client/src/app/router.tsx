import { lazy, Suspense } from 'react'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { PublicLayout } from '@/features/public/PublicLayout'
import { CmsRoutePage } from '@/features/public/CmsRoutePage'
import { RedirectIfAuthenticated, RequireRole } from './guards'
import { ForbiddenPage, NotFoundPage } from '@/features/shared/ErrorPages'
import { LoadingRegion, Skeleton } from '@/components/ui/Skeleton'

/**
 * Three route trees on one router (04 §2): public (no login), member portal and admin
 * panel. Auth and the two private trees are lazy so a first-time visitor downloads only
 * the public bundle.
 */
// Detail pages are lazy too: a visitor who never opens a coach profile or an article
// should not pay for either bundle on the home page's first paint.
const TrainerDetailPage = lazy(() =>
  import('@/features/public/TrainerDetailPage').then((m) => ({ default: m.TrainerDetailPage })),
)
const JournalPostPage = lazy(() =>
  import('@/features/public/JournalPostPage').then((m) => ({ default: m.JournalPostPage })),
)

const LoginPage = lazy(() => import('@/features/auth/LoginPage').then((m) => ({ default: m.LoginPage })))
const RegisterPage = lazy(() => import('@/features/auth/RegisterPage').then((m) => ({ default: m.RegisterPage })))
const PortalHome = lazy(() => import('@/features/member/PortalHome').then((m) => ({ default: m.PortalHome })))

/**
 * The admin panel is one lazy chunk per screen behind a shared shell. A visitor who never
 * signs in downloads none of it, and the owner pays for one screen at a time.
 */
const AdminLayout = lazy(() => import('@/features/admin/AdminLayout').then((m) => ({ default: m.AdminLayout })))
const DashboardPage = lazy(() => import('@/features/admin/ops/DashboardPage').then((m) => ({ default: m.DashboardPage })))
const MembersPage = lazy(() => import('@/features/admin/ops/MembersPage').then((m) => ({ default: m.MembersPage })))
const MemberDetailPage = lazy(() =>
  import('@/features/admin/ops/MemberDetailPage').then((m) => ({ default: m.MemberDetailPage })),
)
const LeadsPage = lazy(() => import('@/features/admin/ops/LeadsPage').then((m) => ({ default: m.LeadsPage })))
const AttendancePage = lazy(() =>
  import('@/features/admin/ops/AttendancePage').then((m) => ({ default: m.AttendancePage })),
)
const KioskPage = lazy(() => import('@/features/admin/ops/KioskPage').then((m) => ({ default: m.KioskPage })))
const SchedulingPage = lazy(() =>
  import('@/features/admin/ops/SchedulingPage').then((m) => ({ default: m.SchedulingPage })),
)
const PlansPage = lazy(() => import('@/features/admin/ops/PlansPage').then((m) => ({ default: m.PlansPage })))
const InvoicesPage = lazy(() => import('@/features/admin/ops/InvoicesPage').then((m) => ({ default: m.InvoicesPage })))
const InvoiceDetailPage = lazy(() =>
  import('@/features/admin/ops/InvoicesPage').then((m) => ({ default: m.InvoiceDetailPage })),
)
const CollectionsPage = lazy(() =>
  import('@/features/admin/ops/InvoicesPage').then((m) => ({ default: m.CollectionsPage })),
)
const CmsPagesPage = lazy(() => import('@/features/admin/cms/CmsPagesPage').then((m) => ({ default: m.CmsPagesPage })))
const CmsPageEditorPage = lazy(() =>
  import('@/features/admin/cms/CmsPageEditorPage').then((m) => ({ default: m.CmsPageEditorPage })),
)
const MediaLibraryPage = lazy(() =>
  import('@/features/admin/cms/MediaLibraryPage').then((m) => ({ default: m.MediaLibraryPage })),
)
const SiteSettingsPage = lazy(() =>
  import('@/features/admin/cms/SiteSettingsPage').then((m) => ({ default: m.SiteSettingsPage })),
)
const ContentLibraryPage = lazy(() =>
  import('@/features/admin/cms/ContentLibraryPage').then((m) => ({ default: m.ContentLibraryPage })),
)

function RouteFallback() {
  return (
    <div className="shell section-y space-y-4">
      <LoadingRegion label="Loading page" />
      <Skeleton className="h-12 w-2/3" />
      <Skeleton className="h-4 w-full" rounded="pill" />
      <Skeleton className="h-4 w-4/5" rounded="pill" />
    </div>
  )
}

function Lazy({ children }: { children: React.ReactNode }) {
  return <Suspense fallback={<RouteFallback />}>{children}</Suspense>
}

/**
 * Public routes map 1:1 onto CMS page slugs. Adding a marketing page is a CMS row plus
 * one line here — never a new component.
 */
const router = createBrowserRouter([
  {
    element: <PublicLayout />,
    children: [
      { path: '/', element: <CmsRoutePage slug="home" /> },
      { path: '/classes', element: <CmsRoutePage slug="classes" /> },
      { path: '/trainers', element: <CmsRoutePage slug="trainers" /> },
      {
        path: '/trainers/:slug',
        element: (
          <Lazy>
            <TrainerDetailPage />
          </Lazy>
        ),
      },
      { path: '/plans', element: <CmsRoutePage slug="plans" /> },
      { path: '/transformations', element: <CmsRoutePage slug="transformations" /> },
      { path: '/journal', element: <CmsRoutePage slug="journal" /> },
      {
        path: '/journal/:slug',
        element: (
          <Lazy>
            <JournalPostPage />
          </Lazy>
        ),
      },
      { path: '/branches', element: <CmsRoutePage slug="branches" /> },
      { path: '/branches/:branchSlug', element: <CmsRoutePage /> },
      { path: '/free-trial', element: <CmsRoutePage slug="free-trial" /> },
      { path: '/contact', element: <CmsRoutePage slug="contact" /> },
      { path: '/faq', element: <CmsRoutePage slug="faq" /> },
      { path: '/tools', element: <CmsRoutePage slug="tools" /> },
    ],
  },
  {
    path: '/login',
    element: (
      <RedirectIfAuthenticated>
        <Lazy>
          <LoginPage />
        </Lazy>
      </RedirectIfAuthenticated>
    ),
  },
  {
    path: '/register',
    element: (
      <RedirectIfAuthenticated>
        <Lazy>
          <RegisterPage />
        </Lazy>
      </RedirectIfAuthenticated>
    ),
  },
  {
    path: '/portal',
    element: (
      <RequireRole role="Member">
        <Lazy>
          <PortalHome />
        </Lazy>
      </RequireRole>
    ),
  },
  {
    path: '/admin',
    element: (
      <RequireRole role="Admin">
        <Lazy>
          <AdminLayout />
        </Lazy>
      </RequireRole>
    ),
    children: [
      { index: true, element: <Lazy><DashboardPage /></Lazy> },
      { path: 'members', element: <Lazy><MembersPage /></Lazy> },
      { path: 'members/:id', element: <Lazy><MemberDetailPage /></Lazy> },
      { path: 'leads', element: <Lazy><LeadsPage /></Lazy> },
      { path: 'attendance', element: <Lazy><AttendancePage /></Lazy> },
      { path: 'scheduling', element: <Lazy><SchedulingPage /></Lazy> },
      { path: 'billing/plans', element: <Lazy><PlansPage /></Lazy> },
      { path: 'billing/invoices', element: <Lazy><InvoicesPage /></Lazy> },
      { path: 'billing/invoices/:id', element: <Lazy><InvoiceDetailPage /></Lazy> },
      { path: 'billing/collections', element: <Lazy><CollectionsPage /></Lazy> },
      { path: 'cms', element: <Lazy><CmsPagesPage /></Lazy> },
      { path: 'cms/pages/:id', element: <Lazy><CmsPageEditorPage /></Lazy> },
      { path: 'cms/media', element: <Lazy><MediaLibraryPage /></Lazy> },
      { path: 'cms/settings', element: <Lazy><SiteSettingsPage /></Lazy> },
      { path: 'cms/content', element: <Lazy><ContentLibraryPage /></Lazy> },
    ],
  },
  {
    // The kiosk runs outside the admin shell: full-bleed dark on a tablet, no sidebar.
    path: '/admin/attendance/kiosk',
    element: (
      <RequireRole role="Admin">
        <Lazy>
          <KioskPage />
        </Lazy>
      </RequireRole>
    ),
  },
  { path: '/forbidden', element: <ForbiddenPage /> },
  { path: '*', element: <NotFoundPage /> },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}

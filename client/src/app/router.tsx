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
/**
 * The member portal is one lazy chunk per screen behind a shared dark shell, the
 * same shape as the admin tree — a member who only ever books a class never
 * downloads the workout logger or the charting library.
 */
const MemberLayout = lazy(() => import('@/features/member/MemberLayout').then((m) => ({ default: m.MemberLayout })))
const PortalHome = lazy(() => import('@/features/member/PortalHome').then((m) => ({ default: m.PortalHome })))
const BookingPage = lazy(() => import('@/features/member/BookingPage').then((m) => ({ default: m.BookingPage })))
const MyQrPage = lazy(() => import('@/features/member/MyQrPage').then((m) => ({ default: m.MyQrPage })))
const MembershipPage = lazy(() =>
  import('@/features/member/MembershipPage').then((m) => ({ default: m.MembershipPage })),
)
const WorkoutsPage = lazy(() => import('@/features/member/WorkoutsPage').then((m) => ({ default: m.WorkoutsPage })))
const ProgressPage = lazy(() => import('@/features/member/ProgressPage').then((m) => ({ default: m.ProgressPage })))
const ReferralsPage = lazy(() =>
  import('@/features/member/ReferralsPage').then((m) => ({ default: m.ReferralsPage })),
)
const NotificationsPage = lazy(() =>
  import('@/features/member/NotificationsPage').then((m) => ({ default: m.NotificationsPage })),
)
const ProfilePage = lazy(() => import('@/features/member/ProfilePage').then((m) => ({ default: m.ProfilePage })))
const CommunityPage = lazy(() =>
  import('@/features/member/CommunityPage').then((m) => ({ default: m.CommunityPage })),
)

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
// Module 4 screens: the radar, the plan studio, corporate accounts, campaigns and the feed.
const ChurnRadarPage = lazy(() =>
  import('@/features/admin/ops/ChurnRadarPage').then((m) => ({ default: m.ChurnRadarPage })),
)
const PlanStudioPage = lazy(() =>
  import('@/features/admin/ops/PlanStudioPage').then((m) => ({ default: m.PlanStudioPage })),
)
const CorporatePage = lazy(() =>
  import('@/features/admin/ops/CorporatePage').then((m) => ({ default: m.CorporatePage })),
)
const OffersPage = lazy(() => import('@/features/admin/ops/OffersPage').then((m) => ({ default: m.OffersPage })))
const FeedPage = lazy(() => import('@/features/admin/ops/FeedPage').then((m) => ({ default: m.FeedPage })))

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
          <MemberLayout />
        </Lazy>
      </RequireRole>
    ),
    children: [
      { index: true, element: <Lazy><PortalHome /></Lazy> },
      { path: 'book', element: <Lazy><BookingPage /></Lazy> },
      { path: 'qr', element: <Lazy><MyQrPage /></Lazy> },
      { path: 'membership', element: <Lazy><MembershipPage /></Lazy> },
      { path: 'workouts', element: <Lazy><WorkoutsPage /></Lazy> },
      { path: 'progress', element: <Lazy><ProgressPage /></Lazy> },
      { path: 'community', element: <Lazy><CommunityPage /></Lazy> },
      { path: 'referrals', element: <Lazy><ReferralsPage /></Lazy> },
      { path: 'notifications', element: <Lazy><NotificationsPage /></Lazy> },
      { path: 'profile', element: <Lazy><ProfilePage /></Lazy> },
      // Deep links written into notifications before this tree existed still land somewhere real.
      { path: 'booking', element: <Lazy><BookingPage /></Lazy> },
      { path: 'billing/:id', element: <Lazy><MembershipPage /></Lazy> },
    ],
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
      { path: 'churn', element: <Lazy><ChurnRadarPage /></Lazy> },
      { path: 'plan-studio', element: <Lazy><PlanStudioPage /></Lazy> },
      { path: 'plan-studio/:memberId', element: <Lazy><PlanStudioPage /></Lazy> },
      { path: 'corporate', element: <Lazy><CorporatePage /></Lazy> },
      { path: 'offers', element: <Lazy><OffersPage /></Lazy> },
      { path: 'feed', element: <Lazy><FeedPage /></Lazy> },
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

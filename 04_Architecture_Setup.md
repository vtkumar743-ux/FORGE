# Architecture & Environment Setup
### React + .NET + SQL Server, CMS-first

---

## 1. Stack (fixed by client requirement)

| Layer | Choice | Why / Notes |
|---|---|---|
| Frontend | **React 18+ with Vite + TypeScript** | Fast dev server, TS for safety |
| Styling | **Tailwind CSS v4** + CSS custom-property design tokens | v4 is the "more advanced" Tailwind: CSS-first config, faster engine |
| Motion | **Framer Motion (`motion`)** + CSS scroll-driven where cheap | Scroll reveals, kinetic type, magnetic CTAs |
| Icons/graphics | **Inline SVG only** — `lucide-react` (renders true SVGs) + custom brand SVGs (barbell, capacity ring, gauge) | Hard rule: no icon fonts, no emoji icons |
| Data fetching | **TanStack Query** + Axios | Caching, optimistic updates for booking |
| Routing | **React Router v7** | Public / member / admin route trees with role guards |
| Forms | React Hook Form + Zod | Validation shared shape with API DTOs |
| Charts (admin) | Recharts | Revenue, footfall, churn charts |
| Backend | **.NET 8 (LTS) ASP.NET Core Web API** | Controllers or minimal APIs; Swagger in dev |
| ORM | **Entity Framework Core 8** (code-first migrations) | SQL Server provider |
| Auth | **ASP.NET Core Identity + JWT (access) / refresh tokens** | Roles: `Admin`, `Member` (v1); claims ready for `BranchManager`, `Trainer` |
| Real-time | **SignalR** | Live occupancy meter, waitlist promotions |
| Background jobs | Hosted services (or Hangfire later) | Dunning reminders, absentee alerts, digest emails |
| Logging | Serilog (rolling file + console) | |
| Payments | Razorpay (order + webhook verification) — sandbox keys in v1 | UPI/card/netbanking |
| Media storage | Local `wwwroot/media` in v1 with WebP conversion (ImageSharp) | Swappable to S3/Azure Blob via interface |
| Database | **SQL Server 2022** (Developer/Express) managed via **SSMS** | |

## 2. Repository layout
```
GYM/
├─ docs (these .md files)
├─ client/                      # React app
│  ├─ src/
│  │  ├─ app/                   # router, providers, guards
│  │  ├─ features/              # public/ member/ admin/ feature folders
│  │  ├─ components/ui/         # design-system primitives (Button, Card, Reveal, Icon, …)
│  │  ├─ lib/                   # api client, hooks, utils
│  │  └─ styles/                # tailwind entry + tokens.css
└─ server/
   ├─ Gym.Api/                  # controllers, middleware, SignalR hubs
   ├─ Gym.Core/                 # entities, enums, interfaces
   ├─ Gym.Infrastructure/       # EF Core DbContext, migrations, services (payments, media, comms)
   └─ Gym.Api.sln
```

## 3. Database — core schema (ERD sketch)
**Identity:** Users, Roles, RefreshTokens
**Gym ops:** Branches · Members (1-1 User) · Plans · PlanBranchPrices · Subscriptions (member×plan, status lifecycle, freeze windows) · Invoices + InvoiceLines (GST fields: GSTIN, SAC/HSN, rate) · Payments (mode, gateway ref, partial allowed) · Coupons
**Scheduling:** ClassFormats · Rooms · ClassSchedules (recurring rule, branch, trainer, capacity) · ClassSessions (materialized occurrences) · Bookings (status: booked/waitlisted/attended/no-show/cancelled, waitlist position)
**Attendance:** CheckIns (member, branch, in/out, source: QR/manual/biometric) → occupancy derived
**People:** Trainers (profile, certs, payRates) · TrainerRatings · Leads (pipeline stage, source, branch, followUps[])
**Training:** Exercises (video URL, muscle group) · WorkoutPrograms → ProgramDays → ProgramExercises · WorkoutLogs (+PR detection) · DietPlans → Meals · BodyScans · ProgressPhotos
**Commerce:** Products · BranchStock · StockTransfers · Orders + OrderLines
**Engagement:** Referrals · Badges · MemberBadges · Challenges · FeedPosts · Notifications
**CMS:** CmsPages (slug, seo) · CmsSections (page, type, orderIndex, isVisible, **contentJson**) · MediaAssets (original + WebP variants) · Testimonials · Transformations · BlogPosts · FaqItems · SiteSettings (key-value: brand, colors, WhatsApp, socials)

> **CMS design principle:** every public-site section renders from `CmsSections.contentJson` (typed per section-type, validated with Zod on the client and a JSON schema on the API). Admin edits structured forms — not raw HTML — so the design system stays intact no matter what the owner types. Seed the full default site content via migration so the site is complete on first run.

## 4. Auth model (v1, per client decision)
- **Admin:** seeded credential (e.g., `admin@gym.local` / strong password printed once by seeder; force-change on first login). Role `Admin` → admin panel + all APIs.
- **Member (global login):** open registration (email/phone + password; OTP-ready field structure). Role `Member` → member portal APIs, own-data only (enforced server-side by userId claim, never by client filtering).
- JWT access (15 min) + refresh (7 d, rotating, httpOnly cookie). Route guards client-side + `[Authorize(Roles=…)]` + branch-scope policy handlers server-side.

## 5. Prerequisite check & installation (Windows)
Run each check; install only what's missing (winget = built-in on Win 11):

| Tool | Check | Install |
|---|---|---|
| Node.js 20+ LTS | `node -v` | `winget install OpenJS.NodeJS.LTS` |
| .NET 8 SDK | `dotnet --list-sdks` (need 8.x) | `winget install Microsoft.DotNet.SDK.8` |
| SQL Server 2022 Express | `sqlcmd -S localhost\SQLEXPRESS -Q "SELECT @@VERSION"` | `winget install Microsoft.SQLServer.2022.Express` |
| SSMS | Start-menu check | `winget install Microsoft.SQLServerManagementStudio` |
| Git | `git --version` | `winget install Git.Git` |

Then scaffold:
```powershell
# frontend
npm create vite@latest client -- --template react-ts
cd client; npm i; npm i tailwindcss @tailwindcss/vite motion lucide-react @tanstack/react-query axios react-router-dom react-hook-form zod recharts
# backend
dotnet new sln -n Gym; dotnet new webapi -n Gym.Api; dotnet new classlib -n Gym.Core; dotnet new classlib -n Gym.Infrastructure
dotnet add Gym.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer
dotnet add Gym.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add Gym.Api package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add Gym.Api package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add Gym.Api package Serilog.AspNetCore
dotnet tool install --global dotnet-ef
```
Connection string (SQL auth off, Windows auth on Express default):
`Server=localhost\SQLEXPRESS;Database=GymDb;Trusted_Connection=True;TrustServerCertificate=True`
Migrations: `dotnet ef migrations add Init -p Gym.Infrastructure -s Gym.Api` → `dotnet ef database update …`

## 6. API surface (v1 controllers)
`/api/auth` (register, login, refresh) · `/api/cms/*` (public read; admin write) · `/api/branches` · `/api/members` · `/api/plans` `/api/subscriptions` `/api/invoices` `/api/payments` (+ `/razorpay/webhook`) · `/api/schedule` `/api/bookings` (+waitlist) · `/api/checkins` (+`/occupancy` SignalR hub) · `/api/trainers` · `/api/leads` · `/api/workouts` `/api/progress` · `/api/products` `/api/orders` · `/api/notifications` · `/api/reports/*` · `/api/media`

## 7. Performance & quality gates
- Public pages: LCP < 2.5s — poster image before video, `loading="lazy"`, WebP variants, font `display=swap`, route-level code splitting
- Never block on animation: all reveals are content-visible-first, `prefers-reduced-motion` short-circuits
- Server: pagination everywhere, `AsNoTracking` reads, indexed FKs + covering indexes on hot queries (bookings by session, check-ins by branch+date)
- Zod ↔ DTO parity; ProblemDetails error contract; FluentValidation on API
- Seeder produces a fully-populated demo (3 branches, 200 members, realistic Indian data) so the client demo looks alive on first run

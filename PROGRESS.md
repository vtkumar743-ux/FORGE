# PROGRESS

Running log for the FORGE build. One line per completed chunk: `[phase] what was built · files touched · next step`.
Read this first in every new session.

---

## PHASE 0 — Environment & Skeleton ✅ COMPLETE (17 Aug 2026)

- `[0.1]` Prerequisite audit + installs · nothing touched in repo · **Installed via winget:** Node.js 24.19.0 LTS, .NET 8 SDK 8.0.424 (only the runtime was present), Git 2.55.0, SQL Server 2022 Express (new `SQLEXPRESS` instance), SSMS 20.2.1. Also installed `dotnet-ef` 8.0.11 global tool. An unrelated SQL Server **2008 R2** instance (`MSSQL$RELYONDB`, third-party payroll software) was found and deliberately left alone — too old for EF Core 8 and not ours.
- `[0.2]` Client scaffold · `client/**` · Vite 8 + React 19 + TS 6, Tailwind v4 via `@tailwindcss/vite`, motion, lucide-react, TanStack Query, axios, React Router 7, RHF, Zod, Recharts. Vite proxies `/api`, `/media/uploads`, `/hubs` to the API so the client is same-origin with it.
- `[0.3]` Design tokens · `client/src/styles/tokens.css`, `index.css` · Full 03 §2–3 palette, type scale, geometry, motion and layout tokens as CSS custom properties, bridged into Tailwind's `@theme` so every utility resolves through them. Utilities: `.graded` (the one-grade rule), `.grain` (inline SVG `feTurbulence` at 4.5%), `.marquee-track`, `.text-outline`, `.skeleton`, `.underline-slide`, `.auth-input`, `.theme-light` (admin surface), gold selection + custom scrollbar, global `prefers-reduced-motion` short-circuit.
- `[0.4]` UI primitives · `client/src/components/ui/**` · `<Icon>` (inline SVG only — Lucide plus 14 hand-authored brand marks: barbell, kettlebell, boxing glove, body-scan, shaker, stretch, lotus, core, run, studio, spa, and the three social glyphs Lucide v1 dropped), `<Reveal>`/`<RevealGroup>`/`<KineticHeading>`, `<Button>`/`<ButtonLink>` (triple-intent variants + magnetic hover), `<Card>`/`<CardMedia>`/`<Badge>`/`<CapacityRing>`, `<Skeleton>`/`<EmptyState>`. Fonts: Clash Display (Fontshare, real variable face) + Inter variable, both preconnected with `display=swap`.
- `[0.5]` Domain model · `server/Gym.Core/**` · 47 entities across the full 04 §3 schema (identity, gym ops, billing+GST, scheduling, attendance, CRM, training, commerce, engagement, CMS) plus 30 enums, `RoleNames`, and the `IClock`/`IMediaStorage` seams.
- `[0.6]` Persistence · `server/Gym.Infrastructure/Persistence/**` · `GymDbContext` (schemas `gym` + `auth`), 47 fluent configurations with unique constraints and covering indexes on the hot paths (bookings by session, check-ins by branch+date, subscriptions by status+end, invoices by status+due, PR lookup by member+exercise+e1RM), global decimal/string conventions, audit stamping. Migration `Init` applied to `localhost\SQLEXPRESS` / `GymDb`.
- `[0.7]` Seeder · `server/Gym.Infrastructure/Persistence/Seeding/**` · Deterministic and idempotent (fixed RNG seed; every step short-circuits if its table has rows). Produces **3 branches, 8 trainers, 10 class formats, 15 rooms, 6 plans with Indian pricing + per-branch overrides, 40 classes/week (verified 0 trainer and 0 room conflicts), 458 materialised sessions, 200 members with realistic Indian names, 200 subscriptions/invoices/payments with 5% GST split, 6,673 bookings, 6,320 check-ins, 35 exercises, 14 products with mixed GST slabs, 10 badges, 3 coupons, 51 leads with follow-up sequences, 14 CMS pages / 63 sections, 8 testimonials, 5 transformations, 5 journal posts, 14 FAQs, 44 site settings**. Attendance drives streaks, which drive churn scores — the numbers agree with each other rather than being independently random.
- `[0.8]` Auth end-to-end · `Gym.Infrastructure/Services/TokenService.cs`, `Gym.Api/Controllers/AuthController.cs`, `Gym.Api/Program.cs`, `client/src/lib/{api,auth}.tsx`, `client/src/app/guards.tsx` · Identity + JWT (15 min) + rotating refresh (7 d, httpOnly cookie, SHA-256 hashed at rest). Reuse of an already-rotated token revokes the whole token family. Register / login (by email **or** mobile) / refresh / logout / me / change-password. Client keeps the access token in memory only, with a single-flight silent refresh on 401. Route guards both sides; `[Authorize(Roles=…)]` server-side.
- `[0.9]` CMS read+write API · `Gym.Api/Controllers/CmsController.cs`, `BranchesController.cs` · Anonymous published reads, Admin-only writes to a draft slot with explicit publish, drag-reorder, per-section visibility, site settings, branch directory and derived occupancy. `OccupancyHub` SignalR contract established so Phases 1–4 build against a stable client-method surface.
- `[0.10]` Public shell, CMS-driven · `client/src/features/public/**`, `client/src/app/router.tsx` · Header (nav by experience, scroll-reactive, mobile sheet), announcement bar, footer with per-branch detail read from the API, and `CmsRoutePage` — one component that renders whatever sections the CMS returns, in CMS order, through a section registry. **5 of 24 section types are registered** (Hero with video+poster+kinetic headline, MarqueeTicker, StatBand with count-ups, Manifesto, CtaBanner); the other 19 are seeded and wired but render nothing until Phase 1 adds them. Per-page SEO, OG tags and per-branch JSON-LD all come from `CmsPages`.
- `[0.11]` Auth + portal + admin shells · `client/src/features/{auth,member,admin,shared}/**` · Split-panel login/register on the same tokens, member portal home (dark, live occupancy from the API), admin home (light surface, same tokens), 403/404 pages.
- `[0.12]` Media pipeline · `client/media.manifest.json`, `client/scripts/fetch-media.mjs` · `npm run media` downloads 16 verified 4K/2K Unsplash assets into `client/public/media` so no binaries are committed and no page falls back to a grey box. Owner-uploaded media is separate: `LocalMediaStorage` writes WebP renditions + a blur placeholder to the API's `wwwroot/media/uploads`, served at `/media/uploads`.

### Phase 0 verification — all gates pass

| Gate | Result |
|---|---|
| `dotnet build Gym.sln` | 0 errors, 0 warnings |
| `npm run build` | passes; vendor chunks split (react / motion / data) |
| API runs, Swagger lists auth + cms | 18 endpoints: 6 × `/api/auth`, 8 × `/api/cms`, 3 × `/api/branches`, `/health` |
| Migration applied to `localhost\SQLEXPRESS` | `GymDb`, 55 tables (47 `gym` + 8 `auth`) |
| Seeded admin login | 200, role `Admin`, `mustChangePassword: true` |
| Self-registered member | 200, role `Member`, code continues the branch series (`FRG-WHF-00056`) |
| Seeded member login (email and mobile) | 200 both ways |
| Refresh rotation | new cookie issued; replayed old token → 401; post-logout refresh → 401 |
| Role enforcement | member → CMS write 403; admin → 204 |
| Client shell | renders at `:5173`, CMS content and media both resolve through the proxy |

### Credentials (dev)

- **Admin** — `admin@gym.local` / `Forge@Admin2026!` (set in `appsettings.Development.json`; forced change on first login)
- **Members** — any seeded member email (e.g. `rakesh.chopra@example.com`) / `Member@12345`

### Deviations from the docs — each with its reason

1. **Kinetic headline animates weight 400→700, not 400→800** (03 §3). Clash Display's real variable axis is 200–700; 800 does not exist in the font.
2. **Solution is `Gym.sln`, not `Gym.Api.sln`.** 04 §2's tree and §5's `dotnet new sln -n Gym` command disagree; followed the command.
3. **Build-time brand imagery lives in `client/public/media`; owner uploads live at `/media/uploads`.** The standing rule puts stock imagery in `client/public/media` while 04 §1 puts uploads in `wwwroot/media` — both would occupy `/media/*`. Splitting the upload namespace keeps them from shadowing each other.
4. **Public route slugs are `/plans`, `/journal`, `/tools`** (not `/pricing`, `/blog`, `/calculators`). Nav-by-experience per 03 §1; the CMS page slugs match, so Phase 1 should use these.
5. **ImageSharp pinned to 3.1.12, not 4.x.** v4 refuses to build without a paid Six Labors licence key; 3.1.12 is the latest patched release under the Split Licence. **Flag for the client:** even 3.1.x requires a commercial licence above a revenue threshold — worth confirming before launch, or swapping `IMediaStorage` for a different encoder.
6. **Invoices and Payments do not cascade-delete with a Member** (`NoAction`, not `Cascade`). The NFRs demand a non-repudiable payment audit trail, so financial rows must outlive the member record. Members are retired by `Status`, not deleted.
7. **Refresh cookie drops its `Secure` flag in Development only** (`!IsDevelopment() || Request.IsHttps`). Local dev runs over `http://localhost` through the Vite proxy, where a `Secure` cookie is silently discarded and silent refresh breaks. Always `Secure` outside Development.
8. **Day-pass and trial are sold as POS products, not as two of the 6 plans.** The spec asks for exactly 6 plans *and* lists DayPass/Trial plan kinds; the day pass and 5-visit guest pass are seeded in `Products` with SAC 999723 so the 6 headline plans stay the ones on the pricing page. Both `PlanKind` values exist for later use.
9. **`Branch.MemberCodePrefix` added to the schema.** Deriving the prefix from the slug gave Whitefield two series (`WHF` in the seeder, `WHI` at registration) with independent counters. Storing it makes one series per branch, including branches the owner adds later.
10. **Hero video not yet sourced.** `forge-hero.mp4/.webm` are referenced by the CMS but pending — the hero renders its poster, which is the designed fallback, so nothing shows as empty. Phase 1's imagery pass fills it.

### What Phase 1 inherits (nothing here needs redoing)

- Every public page's content already exists in `CmsSections` with real, specific copy — including the 19 section types with no renderer yet. Phase 1 registers components against seeded content rather than inventing it.
- `media.manifest.json` lists exactly which asset groups are still unsourced.
- `CapacityRing`, `Badge`, `CardMedia` (hover image swap), `Reveal`, `KineticHeading` and the occupancy endpoint + SignalR contract are all in place for the class rail, timetable and occupancy meter.

**Next step — PROMPT 1:** build the complete public website (all 12 features of Module 1) by registering the remaining 19 section renderers against the already-seeded CMS content, and complete the imagery pass listed under `pending` in `media.manifest.json`.

---

## PHASE 1 — Public Website ✅ COMPLETE (17 Aug 2026)

- `[1.1]` Public read API · `Gym.Api/Contracts/PublicContracts.cs`, `Controllers/{Classes,Trainers,Plans,Content}Controller.cs` · The data the marketing sections render from, all anonymous: class formats (each with its live session count, the branches that run it and the next bookable slot), the filterable timetable with facets, coach profiles, per-branch plan pricing, the seasonal offer, testimonials, transformations, FAQs and the journal. Times are returned twice — the UTC instant for ordering, and the IST wall clock for anything a visitor reads — so a Bengaluru timetable says "6:30 PM" whichever timezone it is browsed from.
- `[1.2]` Lead capture · `Controllers/LeadsController.cs` · `POST /api/leads` feeds the same pipeline the Phase 2 board reads, with the automated follow-up sequence already scheduled (5-minute WhatsApp, 24-hour call, evening-before reminder and post-trial call when a date is given). Phone normalisation to ten digits, a 24-hour dedupe so a double-tap is one lead, a honeypot that answers 201 without storing, and `+91` handling.
- `[1.3]` SEO endpoints · `Controllers/SeoController.cs` · `/sitemap.xml` generated from the CMS (27 URLs: 14 pages, 8 coaches, 5 posts) with per-route priorities, and `/robots.txt` disallowing the authenticated trees. A page the owner adds or unpublishes enters or leaves the index without a deploy.
- `[1.4]` Client data layer · `client/src/lib/public-api.ts` · One Zod-validated hook per resource. Marketing content caches for 10 minutes; the timetable for 60 seconds and keeps previous results on screen through a filter change, so the sheet never blinks back to a skeleton once it has content.
- `[1.5]` All 19 remaining section renderers · `client/src/features/public/sections/**` · AmenityBento (mixed 1×1/2×1/1×2/2×2 tiles, photo tiles scrimmed and text tiles inverted), ClassRail, TrainerHighlight, TransformationSlider, TestimonialWall, PricingTable, FaqAccordion, BranchLocator, AppQr, RichText, ImageFeature, BlogRail, CalculatorBlock, ContactBlock, OfferBanner, TimetableEmbed, AnnotatedFacility, LeadForm, SignatureScroll. **All 24 section types now render**; the registry is the only place a type binds to a component.
- `[1.6]` Shared public components · `client/src/features/public/components/**` · `SectionHeader` (one eyebrow/headline/lead stack so section rhythm is identical down the page), `OccupancyMeter` (240° SVG gauge, Comfortable/Busy/Peak, plus a compact chip), `ClassFormatCard` + `ClassSessionCard` (cult.fit's density at 2–3× the image size, capacity ring, spots-left urgency colouring), `TrainerCard` (3:4 portrait going duotone on hover via a CSS filter chain, not a second asset), `BeforeAfterSlider` (both frames clipped from one box so the comparison stays pixel-aligned; a real range input underneath carries the keyboard and screen-reader semantics).
- `[1.7]` Signature scroll moment · `SignatureScrollSection.tsx` · The one on the site (03 §6): the Whitefield floor scales 80%→100vw pinned while the headline's tracking tightens 0.14em→-0.02em. Collapses to a static full-bleed frame under `prefers-reduced-motion` — a valid final frame, not a degraded one.
- `[1.8]` Timetable · `TimetableEmbedSection.tsx` · Day tabs with per-day counts, filter pills for branch/format/coach/level/time-of-day, sessions grouped by day, skeletons not spinners. Facets come back with the results so a filter never offers a combination that returns nothing; branch pages pin their own branch and hide the control rather than pre-setting it.
- `[1.9]` Pricing · `PricingTableSection.tsx` · Three tiers on screen with the popular one gold-bordered, trust microcopy under every price, a branch selector that re-quotes what you would actually be charged, and a collapsible compare table whose cells derive from real plan fields rather than hand-maintained copy. The cycle toggle promotes a cycle rather than multiplying a monthly figure — each cycle is its own plan with its own rate.
- `[1.10]` Free-trial form · `LeadFormSection.tsx` · Two CMS-defined steps (fields, options, consent wording and every line of success and failure copy come from the section). Step one is name and number so an abandon at step two still leaves a contactable lead. Deep links carry intent: `/free-trial?branch=whitefield&class=hiit-45&intent=pt&plan=annual-all-access`. Per-field validation with `aria-invalid`/`aria-describedby`, off-screen honeypot, and a checkmark that draws itself on success.
- `[1.11]` Calculators · `CalculatorBlockSection.tsx` · BMI on the **WHO Asian-Indian cut-offs** (the international bands overstate a healthy range for this population and would mislead most readers of this page), and BMR via Mifflin-St Jeor with an activity multiplier and a ±15% goal adjustment. Both recompute on every keystroke, no network call.
- `[1.12]` Detail pages · `TrainerDetailPage.tsx`, `JournalPostPage.tsx` · Coach profiles with this-week's live classes, Person JSON-LD and a PT CTA; journal articles rendered from structured blocks (never HTML) with Article JSON-LD and a related rail. Both lazy-loaded so the home page's first paint pays for neither.
- `[1.13]` Imagery pass · `client/media.manifest.json`, `scripts/fetch-media.mjs` · **85 assets, 0 failures** — 8 trainer portraits, 14 facility shots, 10 class covers, 8 testimonial avatars, 5 journal covers, 5 transformation pairs and 14 per-page OG crops, on top of Phase 0's 16. Every one was sourced by searching Unsplash and reading the returned descriptions, then the portraits were opened and **visually reviewed**; five were rejected and re-sourced for carrying a competitor's wordmark (a rival yoga studio, "KINGS GYM", an NBA jersey) or an off-palette backdrop. The manifest gained `$group` heading rows and the fetcher skips them.

### Phase 1 verification — all gates pass

| Gate | Result |
|---|---|
| `dotnet build Gym.sln` | 0 errors, 0 warnings |
| `npm run build` | passes; vendor chunks split, both detail pages code-split |
| 15 new public endpoints | all 200 — formats, timetable (+filters), trainers, trainer/{slug}, plans, plans@branch, offer, testimonials, transformations, faqs, journal, journal/{slug}, sitemap.xml, robots.txt |
| Every section traceable to a `CmsSection` row | **14 pages, 63 sections, 22 types — every one has a renderer and every required field is present** (scripted check against the live API) |
| Media referenced by the CMS exists on disk | 31 referenced, 28 present; the 3 absent are the hero video pair and the app screenshot, both with working designed fallbacks |
| Per-branch pricing resolves | Whitefield quotes ₹2,900/₹7,700/₹13,700/₹18,300 against list ₹3,200/₹8,400/₹14,900/₹19,900; annual shows 48% saving vs monthly |
| Timetable has live data | 40 sessions over 7 days, 10 formats, 8 coaches, spots-left counts from real bookings |
| Lead capture end-to-end | create → `FRG-L00052` + 4 scheduled follow-ups; same number again folds into the same lead; honeypot answers 201 without storing; 3-digit phone → 400 |
| Performance basics | all below-fold media `loading="lazy"`, hero poster `eager`/`fetchPriority=high`, route-level code splitting, skeletons everywhere |

### Deviations from the docs — each with its reason

11. **Hero video still not sourced.** Trimming a Pexels clip to the specified 6–10s, muted, 2–4 MB WebM+MP4 pair needs ffmpeg, which is not installed here; shipping an untrimmed 4K clip with audio would breach 03 §4 and I could not view its content to check it was appropriate. The hero renders its poster, which is the designed fallback and looks finished. Listed in `media.manifest.json` with the exact command needed.
12. **Trainer portraits and transformation pairs are stock stand-ins.** 03 §4 wants one commissioned shoot and 02 §1.7 wants consented member photos. Both are flagged `REPLACE BEFORE LAUNCH` in the manifest — the transformation gallery in particular publishes named people with weights and timelines, so it must not ship with stock.
13. **Maps are OpenStreetMap, not Google.** No API key to leak, no consent banner, and the light tiles are inverted and hue-rotated back into the palette rather than punching a white rectangle through the page. A "Directions" link still opens Google Maps, which is what people actually want from a map on a gym page.
14. **`BranchLocator.nearestFirstPrompt` does not geolocate.** The seeded copy offers "Use my location to sort by distance"; the section rewrites it to state the branches are within 14 km of each other instead of prompting for a permission it would not act on. Advertising a control that does nothing is worse than not offering it.
15. **Online joining (1.11) stops at the checkout hand-off.** Plan cards route to `/free-trial?intent=join&plan={slug}`, which captures the lead with the plan attached. Razorpay order creation and the webhook are explicitly PROMPT 2's gate, so the payment leg lands there rather than being half-built twice.
16. **The app screenshot is drawn, not photographed.** `AppQrSection` renders the member-app home screen from live tokens inside the device frame — sharper than a PNG, and it cannot drift out of date with the palette. A real capture dropped at `/media/app/forge-app-home.png` takes over automatically.
17. **`ImageFeature` and `RichText` have renderers but no seeded content.** Both are registered and validated so the owner can add them to any page from the Phase 2 CMS without a deploy.

### What Phase 2 inherits

- The section registry and the Zod shapes in `sections/schemas.ts` are the exact contract the admin editors must produce — one structured form per shape and the public site cannot be broken by an edit.
- `registeredSectionTypes` is exported for the admin section picker.
- Leads arriving from the website already carry a scheduled follow-up sequence, so the pipeline board has real work in it on day one.
- `client/src/lib/public-api.ts` holds every public read model; the admin panel's write endpoints return the same shapes.

**Next step — PROMPT 2:** the admin panel — CMS editors against these shapes, then core ops (dashboard, members, billing with Razorpay, scheduling, attendance, leads board).

---

## PHASE 2 — Admin Panel: CMS + Core Ops ✅ COMPLETE (18 Aug 2026)

### 2A — CMS in full

- `[2.1]` Admin CMS API · `Gym.Api/Controllers/Admin/AdminCmsController.cs`, `Contracts/AdminCmsContracts.cs` · Pages (list with draft counts, detail, create, update, publish, unpublish, delete), sections (create, duplicate, delete, discard-draft, rename/rescope) and site settings (read every key including private ones, bulk update, create). Publishing a page promotes every pending section draft with it — leaving drafts behind is how stale copy ships. System pages can be edited but never renamed or deleted, because their slugs are wired into the router.
- `[2.2]` Media library · `Controllers/Admin/MediaController.cs` · Upload → WebP original plus a rendition at every width narrower than the source, plus a ~200-byte blurred data URI. Paged list with search/folder/kind filters, alt-text editing, folder taxonomy. **Alt text is required on an image upload** — a gallery of unlabelled photographs fails WCAG the moment the owner drops one into a section. Delete refuses while the URL is still referenced by a section, a coach portrait, a class cover, a journal post or a branch hero, and names what is using it.
- `[2.3]` Content library · `Controllers/Admin/AdminCollectionsController.cs` · CRUD for the four shared pools several sections read from: testimonials, transformations, FAQs and the journal. Transformation consent is a hard gate and is **timestamped on the transition**, because that gallery publishes a named person with their weight.
- `[2.4]` **Zod-driven section editors** · `client/src/features/admin/cms/{zod-fields.ts,SectionEditor.tsx,section-schemas.ts}` · The editor form for all 24 section types is *generated from the same Zod shapes the public renderer validates against* — imported, not copied. Adding a property to a section schema makes an input appear in the CMS with no further work, and a field cannot exist in the editor that the site cannot draw. Controls are inferred per type (string → text or textarea, media names → picker, enum → select, array-of-object → collapsible repeater with reorder, nested object → inline fieldset) and refined by a small label/hint table so the generated form still reads hand-written. **Nothing saves until the edited object passes that same shape**; the API stores whatever JSON it is handed, so this is the gate that keeps a bad edit off the site.
- `[2.5]` Page editor · `cms/CmsPageEditorPage.tsx` · Drag-reorder committed on drop **with keyboard arrows alongside it** — a list only reorderable by mouse-drag is a list a keyboard user cannot reorder at all. Per-section visibility, duplicate, delete, branch-scoping, a draft badge on every row, "add section" with a described picker for all 24 types, and a page SEO/state sheet with live length counters against the 60/160-character search limits.
- `[2.6]` Site settings & media screens · `cms/{SiteSettingsPage,MediaLibraryPage,ContentLibraryPage}.tsx` · Settings render by `valueType` (colour swatch + hex, media picker, toggle, textarea, url, number) grouped as the seeder grouped them, with unsaved-change badges and one save. Theme colours write straight to the CSS custom properties the whole site resolves through, so changing the accent repaints every button with no rebuild.

### 2B — Operations

- `[2.7]` Dashboard · `Controllers/Admin/DashboardController.cs`, `ops/DashboardPage.tsx` · Active members, MRR (every term normalised to a month so an annual and a monthly plan compare), today's check-ins, dues outstanding, expiring-in-7, new leads, collected-this-month, at-risk count — each against its comparison period. Plus 30/7/90-day revenue, footfall and joins series, per-branch comparison with class fill, plan mix, the churn radar and the renewals list. All live queries; there is no nightly rollup to go stale.
- `[2.8]` Members · `Controllers/Admin/AdminMembersController.cs`, `ops/{MembersPage,MemberDetailPage}.tsx` · Paged list with filters in the URL (so "Whitefield, expiring soon" is a bookmark), bulk tag/status/branch on the selection, CSV export with a BOM so Excel opens Indian names intact, and a **row-by-row CSV import** where one bad line is reported and skipped rather than failing the file. Detail carries the full profile, memberships, invoices, upcoming bookings, the QR card, and **one merged activity timeline** — joins, payments, visits, bookings and freezes in one reverse-chronological feed, which is the screen the desk actually reads before picking up the phone.
- `[2.9]` Billing · `Gym.Infrastructure/Services/{GstCalculator,InvoiceService,SubscriptionService}.cs`, `Controllers/Admin/AdminBillingController.cs` · Plan catalogue with the per-branch override table **inside** the plan editor, coupons with every cap enforced at the point of sale (and the reason surfaced when one does not apply), live quoting (branch override → admission fee once per member → coupon → proration credit → GST split), selling, freezing, resuming, cancelling and upgrading. GST is computed in one place: intra-state CGST+SGST halves, IGST when the customer GSTIN is out of state, statutory round-off carried as its own line. Invoice numbers are **gap-free within the Indian financial year** and account for rows added in the same unit of work. Invoice status is always *derived* from payments received and the due date, never set by hand.
- `[2.10]` Razorpay · `Services/RazorpayGateway.cs`, `Controllers/PaymentsController.cs` · Real order creation and constant-time HMAC verification for both the checkout handler and the `X-Razorpay-Signature` webhook. Creating an order writes a **Pending payment row keyed by the gateway order id**, which both the browser callback and the webhook settle — so a capture can arrive twice, out of order, or only over the webhook, and the invoice is credited exactly once.
- `[2.11]` Dunning & operations workers · `Gym.Infrastructure/BackgroundJobs/{DunningWorker,OperationsWorker}.cs` · The collections ladder at D-7, D-3, due date, then weekly, with state on the invoice so a restart never double-chases and never silently drops. The operations sweep keeps four weeks of sessions materialised, closes finished classes and marks no-shows, expires lapsed memberships, and raises the "no visit in 10 days" absentee alert (one per member per fortnight — chasing daily is how a win-back becomes spam). Both are runnable on demand from the UI.
- `[2.12]` Scheduling · `Services/SchedulingService.cs`, `Controllers/Admin/AdminSchedulingController.cs`, `ops/SchedulingPage.tsx` · Shared format library, per-branch rooms, and a weekly-grid recurring builder with **conflict detection as interval overlap on trainer and room, honouring both effective windows**. Editing a rule updates its future occurrences in place rather than deleting them, because members hold bookings against those exact rows; retiring a slot ends it today and removes only the unbooked occurrences. Rosters with attended/no-show marking, substitutions (checked for a clash), class cancellation that releases bookings, returns credits and notifies, and **waitlist auto-promotion on every cancel** with the queue renumbered so position 1 always means next.
- `[2.13]` Attendance & QR kiosk · `Controllers/Admin/AttendanceController.cs`, `ops/{AttendancePage,KioskPage}.tsx` · The kiosk answers in one round trip with everything the desk needs to act on — plan, expiry, dues, streak and today's booked classes — because a turnstile that only says "no" is one someone works around. Dues are a warning, not a locked door; expiry, freeze, wrong-branch and off-peak windows are refusals, and **a refusal is recorded with its reason** rather than discarded. Streaks advance on the first visit of a day. Plus the peak-hours heatmap on IST buckets, live occupancy, and the absentee list with one-click win-back. The kiosk runs the dark theme full-bleed and keeps focus in the scan field, because a hardware reader types into whatever has focus.
- `[2.14]` Leads pipeline · `Controllers/Admin/AdminLeadsController.cs`, `ops/LeadsPage.tsx` · Six-column board, drag *and* keyboard to move a card, and **every stage move schedules the follow-up that stage implies** — so the queue cannot drift from the board. Follow-up queue with completion and next-touch in one action, conversion analytics on the **median** first response (one lead answered three days late should not move the number the desk is judged on), and one-step conversion into a member with the plan sold and money collected — reusing an existing member if the number already belongs to one, rather than creating a duplicate person.

### Phase 2 verification — all gates pass

| Gate | Result |
|---|---|
| `dotnet build Gym.sln` | 0 errors, 0 warnings |
| `npm run build` | passes; charts split into their own chunk so the dashboard chunk is 12 kB, not 387 kB |
| `npx oxlint src` | no errors (only the pre-existing fast-refresh style warnings) |
| Swagger | **115 paths, 145 operations** (was 33 after Phase 1) |
| Scripted end-to-end suite | **53 checks, 53 pass** — see below |
| CMS proof | edited the hero headline and the brand tagline from the admin API and read the change back off the **public** endpoints; draft edits stayed invisible to the public while the admin saw them |
| Demo flow | lead `FRG-L00060` → board → trial stage → converted to `FRG-WHF-00063` → invoice `FRG/26-27/000208` → half by UPI → replayed key did **not** double-credit → settled by cash → booked a class → QR check-in admitted with plan/expiry/streak → visit on the attendance list |
| GST arithmetic | taxable 4190.47 + CGST 104.77 + SGST 104.76 + round-off = 4400.00 exactly; POS `29-Karnataka`, supplier GSTIN on the invoice |
| Waitlist | filled a class, cancelled one booking, **1 member promoted** off a 5-deep waitlist |
| Conflict detection | re-posting an existing slot reports 2 clashes naming the coach and the room; a 23:30 slot reports none |
| Freeze policy | 7-day freeze moved the end date 17 Sep → 24 Sep; resuming early handed the unused days back |
| Media pipeline | 2400×1350 PNG → WebP with 480/960/1440/1920 renditions + a 215-char blur URI, served at `/media/uploads/…`, deleted cleanly |
| Authorization | member → 403 on `/api/admin/members`, `/api/media` and CMS settings write; anonymous → 401 |

### Deviations from the docs — each with its reason

18. **Admin CMS writes are split across two routes.** The five write endpoints Phase 0 shipped stay at `/api/cms/*`; everything new is at `/api/admin/cms/*`. Moving the originals would have broken a working contract for no gain.
19. **Added a lenient `TimeOnly` JSON converter** (`Contracts/TimeOnlyJsonConverter.cs`). The API renders wall-clock times as `"06:00"` and an `<input type="time">` produces the same, but System.Text.Json only reads `"HH:mm:ss"` — so without it the timetable builder could not post back a slot it had just been shown. Found by the verification pass, not by inspection.
20. **`IFormFile` parameters carry no `[FromForm]`.** They bind from the multipart body regardless, and Swashbuckle throws generating the document when the attribute is present — which took `/swagger` down until it was removed. Also found by verification.
21. **Razorpay falls back to a sandbox simulator when no keys are configured — in Development only.** With keys it calls the real API. Without them, and only when `IsDevelopment()`, it issues a deterministic `order_SIM…` so the whole demo runs before the client's keys arrive; every such payment is stamped `razorpay-sandbox-simulator` on the row. In any other environment an unconfigured gateway *refuses to transact*, because a fake "paid" in production would corrupt the audit trail. **Same rule for the webhook:** with no `Razorpay:WebhookSecret` it accepts unsigned callbacks in Development and rejects them everywhere else — an unsigned webhook is an open endpoint for crediting invoices. Set `Razorpay:KeyId`, `KeySecret` and `WebhookSecret` in `appsettings.Development.json` to exercise the real sandbox.
22. **The QR kiosk is an admin-authenticated route** (`/admin/attendance/kiosk`), not a public one. The tablet signs in once; an anonymous check-in endpoint would let anyone on the network write attendance rows.
23. **Admin read models are TypeScript types, not Zod-parsed.** Zod runs where an arbitrary human edit can produce anything — CMS section content, validated against the exact shape the renderer uses. Admin list responses are server-shaped reads on an authenticated surface; re-parsing them would double the contract surface for no safety gained. `features/admin/lib/types.ts` mirrors `Contracts/Admin*.cs` one for one.
24. **`GET /api/admin/scheduling/trainers` added.** The public `/api/trainers` deliberately excludes coaches hidden from the website; the roster and substitution pickers must still be able to schedule them.
25. **Plans, coupons and class formats are retired rather than deleted when in use.** An invoice that references a deleted plan stops resolving, and history has to stay readable years later. The API says which it did and why.
26. **No migration was needed.** Phase 0's 47-entity schema covered Module 2 in full — including `Invoice.RemindersSent`/`LastReminderAtUtc` for dunning, `Payment.IdempotencyKey` for webhook retries and `Booking.WaitlistPosition` for promotion. `GymDb` is unchanged at 55 tables.
27. **The section editor is generated, not hand-written.** Twenty-four bespoke forms would drift from the schemas the day someone adds a field. Deriving them from the Zod shapes means the editor and the renderer cannot disagree — at the cost that a genuinely unusual field falls back to a raw-JSON textarea (nothing in the current 24 types does).
28. **Notifications are written but not delivered.** `Whatsapp:Provider` is `"none"`, so every dunning reminder, win-back, booking confirmation and cancellation writes its in-app row and queues the WhatsApp/SMS/email rows with an explicit "no provider connected" note. Nothing the system decided to say is invisible; connecting a provider is a single `INotificationDispatcher` change.

### What PROMPT 2 did **not** cover

02 §MODULE 2B also tables **Trainers & Staff payroll, POS & Inventory, Communications broadcasts, the Reports suite and Feedback/NPS**. PROMPT 2 scoped 2B to Dashboard, Members, Memberships & Billing, Classes & Scheduling, Attendance and Leads, and 02's own Build Phases put the rest in Phase 2/3. They are not built. The tables (`Products`, `BranchStocks`, `Orders`, `TrainerRatings`, `Notifications`, `FeedPosts`) are seeded and waiting.

**Next step — PROMPT 3:** the member portal — home with streak and occupancy, booking with optimistic UI and waitlist, My QR, membership self-serve via Razorpay, workout logging with PR detection, progress charts, referrals and notifications.

---

## How to run

```powershell
# API — http://localhost:5080 (Swagger at /swagger). Migrates and seeds on startup, idempotently.
cd server\Gym.Api; dotnet run --urls "http://localhost:5080"

# Client — http://localhost:5173 (proxies /api, /media/uploads, /hubs to the API)
cd client; npm run media   # once, to download brand imagery
cd client; npm run dev
```

To rebuild the demo database from scratch: drop `GymDb`, then `dotnet ef database update -p Gym.Infrastructure -s Gym.Api` and start the API. A design-time `GymDbContextFactory` keeps `dotnet ef` from booting the app's startup migrate-and-seed block as a side effect.

### Where things are

| Surface | URL | Login |
|---|---|---|
| Public site | `http://localhost:5173/` | none |
| Member portal | `/portal` | any seeded member email · `Member@12345` |
| Admin panel | `/admin` | `admin@gym.local` · `Forge@Admin2026!` |
| Desk kiosk | `/admin/attendance/kiosk` | the admin session |
| Swagger | `http://localhost:5080/swagger` | paste an access token |

Two background workers start with the API: the dunning sweep every six hours and the operations sweep every two. Both can be run on demand from **Collections → Run the ladder now** and `POST /api/admin/attendance/run-sweep`.

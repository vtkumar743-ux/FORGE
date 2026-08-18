# Feature Specification
### End-to-end scope for the multi-branch gym management + showcase application

**Client:** owner of multiple gym branches. **Users (v1):** 2 credential types —
1. **ADMIN** — the owner. Manages everything: branches, members, plans, billing, classes, trainers, leads, POS, reports, and **all public-website content (CMS)** without touching code.
2. **MEMBER (global customer login)** — any customer can register/login to browse, book, track progress, and manage their membership.

> Architecture must be **role-ready**: Branch Manager, Trainer, and Front-Desk roles are Phase-3 additions on the same auth system (role + branch-scope claims), not rewrites.

The app has three faces sharing one backend:
- **Public website** (no login) — the cinematic showcase, 100% CMS-driven
- **Member portal** (member login)
- **Admin panel** (admin login)

---

## MODULE 1 — Public Website (CMS-driven showcase)

| # | Feature | Notes |
|---|---|---|
| 1.1 | Cinematic home page | Video hero, manifesto headline, stat counters, bento amenity grid, class-format rail, trainer highlights, transformation slider, testimonials, app/QR section, footer with per-branch info |
| 1.2 | Branch pages | Photos, amenities bento, Google map, timings, per-branch timetable, contact + WhatsApp click-to-chat, **live occupancy meter** |
| 1.3 | Classes & timetable | Public, filterable (branch/type/trainer/time), no login required to view; "Book free trial" on every class |
| 1.4 | Trainer profiles | 3:4 portraits, bio, certifications, specialties, demo video, "Book PT" |
| 1.5 | Pricing page | Max 3 highlighted tiers + compare table; monthly/quarterly/half-yearly/annual toggle; trust microcopy; seasonal-offer banner (CMS-controlled) |
| 1.6 | Free trial / tour booking | Two-step form → feeds Leads pipeline; auto WhatsApp/email confirmation |
| 1.7 | Transformations gallery | Before/after drag-slider cards, member name + duration + program (consent-flagged) |
| 1.8 | Testimonials + Google reviews | CMS-managed + embed |
| 1.9 | Blog / content hub | SEO; CMS editor with cover image, tags |
| 1.10 | Calculators | BMI, BMR/calorie — engagement widgets |
| 1.11 | Online joining | Buy membership end-to-end: pick plan → pay (Razorpay UPI/card) → member account auto-created |
| 1.12 | SEO & meta | Per-page meta from CMS, local business schema per branch, OG images, sitemap |

## MODULE 2 — Admin Panel

### 2A. CMS (the "no developer needed" requirement)
- Edit every public-site text, image, section order, and visibility: hero video/headline, stats, amenities, pricing figures, offers, trainers, transformations, testimonials, blog, FAQs, branch details, contact info, social links
- **Media library**: upload/crop images, automatic WebP conversion + size variants; pick from library anywhere
- Section toggle (show/hide) + drag-to-reorder per page; draft → publish workflow with preview
- Site settings: brand name, logo, colors (theme tokens), announcement bar, WhatsApp number, SEO defaults

### 2B. Operations
| Area | Features |
|---|---|
| **Dashboard** | Network-wide KPIs: active members, MRR, today's check-ins, dues outstanding, expiring-in-7-days, new leads, branch comparison cards, revenue & footfall charts, churn-risk list |
| **Members** | Full CRUD, photo, e-sign waiver upload, medical notes, tags; lifecycle (trial/active/frozen/expired/cancelled); activity timeline; bulk actions + CSV import/export; birthday list |
| **Memberships & Billing** | Plan catalog (recurring, fixed-term, class packs, PT packs, day pass, trial) with branch price overrides; admission fee; proration & upgrades; freeze with policy limits & fees; coupons/seasonal offers; **GST-compliant invoices (5% service slab, mixed-rate POS)**; payment recording: Razorpay link/QR, UPI, card, cash, cheque, partial/EMI; dunning: auto reminders D-7/D-3/D-day/overdue via WhatsApp+SMS+email with payment link; renewals & collections dashboards |
| **Classes & Scheduling** | Class-format library (shared across branches); per-branch recurring timetable builder (room, trainer, capacity); conflict detection; substitutions; booking/cancel windows; **waitlist auto-promotion**; no-show tracking |
| **Attendance** | QR kiosk mode (tablet at desk scans member QR); manual check-in; class rosters; peak-hours heatmap; absentee alerts ("no visit in 10 days" auto-task + win-back message); biometric-device sync hook (Phase 3, eSSL/ZKTeco) with expiry auto-block |
| **Trainers & Staff** | Profiles, certifications, specialties; shift scheduling; payroll: per-class rate, PT session rate, commission %, monthly payout report; performance: classes taught, fill rate, ratings, PT revenue, client retention |
| **Leads / CRM** | Pipeline board (Inquiry → Tour → Trial → Negotiation → Joined/Lost); sources (website, walk-in, referral, ads); auto-assignment to branch; follow-up task queue with due dates; automated sequences (5-min first response, trial reminder, no-show next-day nudge); conversion analytics |
| **POS & Inventory** | Supplements/merch/day-pass sales; barcode SKUs; per-branch stock + transfer; low-stock alerts; member tab (charge to account); mixed-GST invoices; sales reports |
| **Communications** | Broadcast composer with audience segments (branch, plan, inactive 14+ days, expiring); channels: WhatsApp template, SMS (DLT), email, in-app push; automation triggers: birthdays, milestones, PR shout-outs, absentee win-back |
| **Reports** | Revenue (MRR, by plan/branch/category, projections), members (joins, renewals, churn, LTV), attendance, funnel, staff, outstanding dues; export CSV/PDF; scheduled email digest to owner |
| **Feedback** | Post-class ratings feed, NPS at 30/90 days, complaint tickets, low-rating instant alert, Google-review nudge for promoters |

## MODULE 3 — Member Portal

| Area | Features |
|---|---|
| **Onboarding** | Mobile-first registration (OTP-ready; v1 = email/phone + password), goal & branch selection, health questionnaire |
| **Home** | Today's booked classes, streak flame, next payment, branch occupancy meter, announcements |
| **Booking** | Timetable with filters; one-tap book (cult.fit density: duration/trainer/level/spots-left on card); capacity ring; cancel/reschedule; waitlist join with auto-promote notification |
| **My QR** | Personal QR + digital membership card for check-in |
| **Membership** | Plan, validity, sessions remaining; self-serve: renew (Razorpay), upgrade, buy packs, request freeze; invoice history |
| **Workouts** | Assigned program viewer (day splits, sets/reps/rest, exercise videos); log sets/weights; rest timer; **PR auto-detection with celebration banner** |
| **Progress** | Weight & measurements charts, body-scan history (InBody-style entries), progress photos side-by-side compare, attendance streak calendar, strength charts |
| **Diet** | Assigned meal plan with macros (Indian food entries), daily adherence check |
| **Gamification** | Streaks, badges, monthly challenges, branch leaderboard (opt-in) |
| **Community** | Feed: milestones, PR shout-outs, gym announcements; likes/comments (Phase 2) |
| **PT Marketplace** | Browse trainers, ratings, availability; book & pay for sessions/packs |
| **Referrals** | Personal code/link, reward tracking (e.g., ₹500 credit both sides) |
| **VOD Library** | At-home workout videos filterable by goal/equipment/duration (Phase 2) |
| **Support** | Feedback/rating prompts post-class, help tickets, FAQs |

## MODULE 4 — Signature Differentiators (features "no gym around has")
1. **Live branch occupancy meter** — public + in-app SVG gauge (Comfortable/Busy/Peak) from check-in data, with typical-busy-hours chart. *(SignalR real-time)*
2. **AI workout & diet plan generator** — admin/trainer clicks "Generate" on a member profile (age, goal, injuries, equipment, history) → editable plan draft. *(Claude API; admin approves before publish)*
3. **Churn-risk radar** — rules-based scoring (visit-frequency drop, failed payment, low ratings, no bookings) → red/amber list on dashboard with one-click win-back sequence.
4. **Digitized body-scan tracking** — manual/CSV entry of InBody-style metrics, 8-scan trend charts, shareable progress report PDF.
5. **PR celebrations & streak engine** — auto-detected personal records posted (opt-in) to community feed + WhatsApp share card.
6. **Corporate memberships** — company code self-enrollment, HR usage report export.
7. **Off-peak plans & seasonal offer engine** — 10 AM–4 PM tier, coupon campaigns with expiry & usage caps.
8. **Form-check uploads** — member uploads set video, trainer replies with timestamped feedback (Phase 3, premium PT tier).

## Build Phases
- **PHASE 1 (MVP):** Auth (2 roles) · CMS + full public website · Members · Plans/Billing+Razorpay+GST invoices · Classes/Booking/Waitlist · QR attendance · Dashboard basics · Leads capture · WhatsApp/email notifications (template-based)
- **PHASE 2:** Workouts/Diet builders + member logging · Progress & body-scan · Gamification + community feed · POS/Inventory · Reports suite · Referrals · Occupancy meter · Churn radar · AI plan generator
- **PHASE 3:** Trainer/Branch-manager roles · Payroll · Biometric device sync · VOD library · Form-check · Corporate · Multi-language (Hindi) · PWA/mobile wrapper

## Non-functional requirements
Non-repudiable payment audit trail; role+branch-scoped authorization on every endpoint; <2.5s LCP on public pages (image CDN-style optimization, lazy video); WCAG AA contrast on dark theme; `prefers-reduced-motion` respected; all lists paginated/virtualized; optimistic UI on booking; seed data: 3 branches, 8 trainers, 40 classes/week, 200 members, 6 plans, realistic Indian names & pricing.

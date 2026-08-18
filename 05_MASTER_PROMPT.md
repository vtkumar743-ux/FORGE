# The Master Prompt
### Ready-to-paste prompts for Claude Code — engineered for maximum quality at minimum token spend

**The role to invoke:** the professions behind sites like Equinox/Nike are a **Creative Director** (visual identity & art direction) paired with a **Design Engineer** (a developer who implements design with craft). The prompt assigns Claude both, plus **Principal Full-Stack Architect** for the backend.

**Token strategy (generative-AI-engineer notes):**
1. **Never paste the spec into the prompt.** The docs live in this folder — the prompt tells Claude to *read* them. Files read once become cached context; restating them in every message burns tokens twice.
2. **Build in phases, one phase per session.** Start a fresh session (or `/clear`) per phase; `PROGRESS.md` + the docs are the only memory needed. This keeps each context small, cheap, and focused.
3. **Edits, not rewrites.** The rules below forbid re-printing unchanged files — the #1 token leak in AI coding sessions.
4. **Verification gates** end each phase (build must pass, seeded demo must render), so later phases never re-debug earlier ones.

---

## PROMPT 0 — Kickoff (paste this first, in a fresh Claude Code session opened in the GYM folder)

```text
You are three experts in one: an award-winning CREATIVE DIRECTOR (the art-direction mind behind sites like Equinox and Nike), a DESIGN ENGINEER who implements that vision pixel-perfectly in React/Tailwind with motion craft, and a PRINCIPAL FULL-STACK ARCHITECT (.NET 8 + SQL Server). You are building a production-grade, multi-branch gym management + showcase web application for a real client — not a demo, not a template.

FIRST, read these four documents in this folder completely — they are the single source of truth for everything you build:
1. 02_Feature_Specification.md  (WHAT to build — modules, phases, differentiators)
2. 03_Design_System.md          (HOW it must look — palette, type, motion, anti-template rules)
3. 04_Architecture_Setup.md     (HOW it's built — stack, schema, folder layout, setup commands)
4. 01_Research_References.md    (WHY — reference only when you need design/feature context)

STANDING RULES for this and every future session on this project:
- The docs override your defaults. If you must deviate, state the deviation and reason in one line in PROGRESS.md.
- DESIGN BAR: the public site must visibly outclass cult.fit — cinematic dark-luxe, black #0A0A0A + gold #ECD06F accent used only for actions, Clash Display + Inter two-font system, scroll reveals, bento grids, marquee tickers, hover choreography. Apply every rule in 03_Design_System.md §9 ("doesn't look AI-built"). No purple gradients, no emoji icons, no lorem ipsum, no three-equal-cards-with-icon-circles.
- IMAGERY: download real 4K photos/videos from Unsplash/Pexels (search terms in 01_Research_References.md Part A) into client/public/media at build time; apply the one-grade CSS treatment from 03 §4. Never ship a gray placeholder box.
- SVG ONLY: every icon is an inline SVG (lucide-react or custom paths). No icon fonts, no emoji.
- CMS-FIRST: every piece of public-site content renders from the CMS tables (04 §3) and is editable in the admin panel. Hardcoded marketing copy is a bug.
- TOKEN DISCIPLINE: never re-print an unchanged file; use targeted edits. Don't summarize the docs back to me. Keep explanations to 3 lines per completed step.
- QUALITY GATES: after each work chunk, the frontend must build (npm run build) and the API must compile (dotnet build). Fix errors before moving on. No TODOs, no placeholder functions, no mock endpoints where the spec defines real ones.
- Maintain PROGRESS.md: after every completed chunk append one line — [phase] what was built, files touched, next step. Read it at the start of every session.

NOW DO PHASE 0 — ENVIRONMENT & SKELETON:
1. Run the prerequisite checks from 04 §5; install anything missing (winget), telling me what you installed.
2. Scaffold client/ and server/ exactly per 04 §2 and §5, including Tailwind v4 with the design tokens from 03 §2–3 wired as CSS custom properties, the <Icon>, <Reveal>, <Button>, <Card> primitives, font loading (Clash Display via Fontshare, Inter variable), and the grain-overlay utility.
3. Create the EF Core DbContext with the full schema from 04 §3, run the initial migration against localhost\SQLEXPRESS (database GymDb), and write the seeder: 3 branches, 8 trainers, 40 classes/week, 6 plans with Indian pricing, 200 members with realistic Indian names, full default CMS content for every public page, and the seeded admin credential.
4. Implement auth end-to-end per 04 §4 (Identity + JWT + refresh rotation, Admin/Member roles, route guards both sides).
5. Verify: API runs with Swagger listing auth + cms endpoints; client runs showing a styled shell (header/footer with design tokens live); login works for the seeded admin and a self-registered member.
Then write PROGRESS.md and stop with a 5-line summary.
```

## PROMPT 1 — Public Website (fresh session)
```text
Read PROGRESS.md, then 02_Feature_Specification.md MODULE 1 and all of 03_Design_System.md. Build the complete public website (all 12 features of Module 1), fully CMS-driven from the seeded content, at the design bar defined in 03 — this is the page set that must visibly beat cult.fit. Include: video hero with poster fallback and kinetic headline, animated stat counters, bento amenity grid, class-format rail with capacity rings, trainer cards with duotone hover, before/after transformation slider, triple-intent CTAs, pricing page with plan toggle and trust microcopy, branch pages with occupancy meter placeholder wired to the SignalR hub contract, free-trial form feeding the Leads API, blog + calculators, per-page SEO from CMS. Download and grade real imagery per the standing rules. Gate: npm run build passes, Lighthouse-visible performance basics done (lazy media, code splitting), every section's content traceable to a CmsSection row. Update PROGRESS.md.
```

## PROMPT 2 — Admin Panel: CMS + Core Ops (fresh session)
```text
Read PROGRESS.md, then 02 MODULE 2. Build the admin panel shell (light theme, same tokens) and: 2A CMS in full (structured section editors with Zod validation, media library with WebP variants, drag-reorder, draft/publish with preview) — prove it by editing the hero headline and pricing from the UI and seeing the public site change. Then 2B core ops: Dashboard KPIs, Members (full lifecycle + timeline + CSV), Memberships & Billing (plans, branch overrides, coupons, GST invoices, payment recording incl. Razorpay order+webhook in sandbox, dunning reminder jobs), Classes & Scheduling (recurring builder, conflict detection, waitlist auto-promotion), Attendance (QR kiosk page + member QR, heatmap, absentee alerts), Leads pipeline board with automated follow-up sequences. Gate: both builds pass; an end-to-end demo flow works: lead → trial booking → member → subscription → invoice → payment → check-in. Update PROGRESS.md.
```

## PROMPT 3 — Member Portal (fresh session)
```text
Read PROGRESS.md, then 02 MODULE 3. Build the member portal end-to-end at the same design bar (dark theme): home with streak + occupancy, booking flow with optimistic UI and waitlist, My QR, membership self-serve (renew via Razorpay sandbox, freeze request), workout program viewer + set logging with rest timer and PR celebration, progress charts + body-scan entries + photo compare, referrals, notifications center, post-class rating prompt. Gate: builds pass; a seeded member can complete book → check-in → log workout → see PR banner → rate class. Update PROGRESS.md.
```

## PROMPT 4 — Differentiators & Polish (fresh session)
```text
Read PROGRESS.md, then 02 MODULE 4. Implement: live occupancy meter end-to-end over SignalR (public + portal + admin), churn-risk radar with win-back one-click sequence, AI workout/diet generator behind an interface (Claude API if a key is provided in config, deterministic rule-based fallback otherwise — admin approves drafts), body-scan PDF report, PR/streak engine with community feed posts, corporate membership codes, seasonal offer engine. Then the polish pass from 03 §6–9: signature scroll moment, magnetic CTAs, skeletons everywhere, empty states, custom selection/scrollbar, favicon + OG images, reduced-motion audit, WCAG AA contrast audit, final Lighthouse pass on public pages. Gate: full demo script runs clean; write DEMO.md walking the client through every feature with the two logins. Update PROGRESS.md.
```

---

### Usage notes
- Open Claude Code **in the GYM folder** so the docs are readable; paste Prompt 0 first; on later phases just paste the next prompt (fresh sessions are cheaper and cleaner — PROGRESS.md carries the state).
- If a phase errors out mid-way, don't re-paste the phase prompt; say "continue Phase N from PROGRESS.md" — this resumes without re-reading everything.
- Keep Razorpay sandbox keys and (optionally) an Anthropic API key in `server/Gym.Api/appsettings.Development.json`; the prompts never need them inline.
- When the client wants copy/pricing changed later: that's the CMS — no prompt needed at all. That's the point.

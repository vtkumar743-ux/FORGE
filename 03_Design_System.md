# FORGE — Design System & UI Direction
### The visual language that beats cult.fit

> Working codename for the app: **FORGE** (rename to the client's gym brand).
> Verdict from research: cult.fit is a conversion-optimized utility site, not a design showpiece — dense, carousel-heavy, promo-driven. It sells hard but never feels *luxurious*. That is the gap we exploit.

---

## 1. Brand Direction — "Dark Luxe Performance"

One sentence: **the confidence of Nike, the luxury restraint of Equinox, the environmental drama of Barry's, and cult.fit's one-tap booking density — on a pure-black canvas where photography carries all the color.**

| Cult.fit does | We do instead |
|---|---|
| Promo banner hero (an ad slot) | Cinematic full-bleed looping video hero, one headline, one CTA |
| 6 membership SKUs in the nav | Navigation by experience (Train, Classes, Trainers, Plans) — David Lloyd pattern |
| Generic sans-serif, no personality | Two-font system: condensed display + neutral workhorse |
| Teal accent on everything | One accent, used ONLY for actions — chrome stays monochrome |
| Small thumbnail imagery | 4K editorial photography, uniformly graded, full-bleed |
| Zero motion design | Scroll-triggered reveals, kinetic headline moments, micro-interactions |

## 2. Color System

```
--ink:        #0A0A0A   /* page base — near-black, OLED-first. Dark IS the brand */
--carbon:     #121212   /* raised surfaces, cards */
--steel:      #1E1E1E   /* borders, dividers, input backgrounds */
--smoke:      #9CA3AF   /* secondary text */
--bone:       #F5F3EE   /* primary text — warm off-white, never pure #FFF */
--accent:     #ECD06F   /* GOLD — the premium gym signature (Awwwards "ReShape" pattern).
                           Used ONLY for: primary CTAs, active states, key numbers, accent underlines. */
--accent-hot: #E8442E   /* signal red — errors, live indicators, "almost full" urgency ONLY */
--success:    #3ECF8E   /* confirmations, attendance streaks */
```

**Rules (non-negotiable):**
- The accent is never decorative. If it isn't clickable or a key metric, it isn't gold.
- Photography carries all chromatic energy (Nike rule). No decorative gradients on chrome.
- Admin panel may run a light theme (`#FAFAF8` base) for long data sessions, sharing the same tokens.

## 3. Typography

Two fonts, both free (Google Fonts / Fontshare):

- **Display: "Clash Display"** (Fontshare, variable) or fallback **Archivo Expanded** — uppercase, weight 600–700, line-height **0.9–1.0**, tight tracking (-0.02em). Used for: hero headlines, section titles, big stat numbers, pricing figures.
- **Workhorse: "Inter"** (variable) — UI, body, forms, tables, admin panel. Line-height 1.5–1.6 for body.

Scale (fluid with `clamp()`):
```
Display XL  clamp(3.5rem, 8vw, 7.5rem)    hero headlines
Display L   clamp(2.5rem, 5vw, 4.5rem)    section titles
Display M   clamp(1.75rem, 3vw, 2.75rem)  card group titles, stats
Body L      1.125rem                       lead paragraphs
Body        1rem                           default
Caption     0.8125rem                      labels, meta, badges (uppercase, +0.08em tracking)
```

Signature move: hero headline weight animates 400 → 800 as it enters the viewport (variable-font kinetic typography). Use on hero + max 2 section breaks — polish, not gimmick.

## 4. Photography & Imagery

- **Source:** Unsplash / Pexels 4K originals (≥3840px heroes, 3:4 portraits for trainer cards). Search terms and contributors listed in `01_Research_References.md`. Pexels Videos for the hero loop (6–10s, muted, WebM+MP4, ~2–4MB).
- **The one-grade rule:** every image gets the identical treatment so mixed stock reads as one commissioned shoot:
  ```css
  filter: contrast(1.08) saturate(0.85) brightness(0.92);
  /* plus a dark gradient overlay on text-bearing images: */
  background: linear-gradient(rgba(0,0,0,.2), rgba(0,0,0,.75));
  ```
- Prefer mid-effort authenticity: sweat, chalk dust, motion blur — never posed catalog stock.
- **Annotated facility photos** (Gymshark technique): callout labels with thin gold leader lines on equipment-zone photography ("Olympic lifting platform", "Recovery zone — ice bath").
- Grain overlay on dark sections: SVG `feTurbulence` noise at 3–6% opacity — kills flat-black sterility.

## 5. Layout Grammar

- **Rhythm (Third Space):** full-bleed 100vw image section → constrained text block (max-width 680px) → bento grid → full-bleed. Never stack two carousels adjacent.
- **Bento grids** for amenities/facilities/stats: mixed 1×1, 2×1, 2×2 tiles — pool photo (2×2), classes-per-week stat (1×1), trainer portrait (1×2), hours tile (1×1).
- **Marquee ticker strips** between major sections: auto-scrolling outlined display type — `STRENGTH • CARDIO • MOBILITY • RECOVERY •`.
- **Geometry token:** ONE radius for all interactive elements — pills (`rounded-full`) for buttons/badges/inputs, `rounded-2xl` (16px) for cards. Never mix radii randomly (fastest tell of a template site).
- 12-column grid, `max-w-[1440px]` shell, 24px gutters, generous section padding (`py-24` to `py-36`).

## 6. Motion Design

Framer Motion (`motion` package) + CSS. Every animation ≤ 400ms, `ease-out`, and honors `prefers-reduced-motion`.

- **Scroll reveals:** 24–40px translate-up + fade, 60–100ms stagger across card groups. Applied via one reusable `<Reveal>` component.
- **Stat count-ups:** animated big numbers (members, branches, classes/week, kg lifted this month) triggered on viewport entry.
- **Card hover:** secondary image swap (Gymshark) + scale 1.03 + gold underline slide-in on the title.
- **Magnetic primary CTAs:** button translates ≤6px toward cursor within proximity radius.
- **One signature scroll moment** on the homepage: facility image scales 80% → 100vw pinned while headline letters tighten.
- **Booking feedback:** optimistic UI on "Book" — instant slot fill + checkmark draw animation; skeleton loaders everywhere, never spinners.

## 7. Components — the "not-a-template" checklist

- **Class cards:** cult.fit's info density (duration, trainer, level badge, live spots left, one-tap Book) at 2–3× the image size, hover choreography, capacity ring (SVG) showing fill.
- **Pricing:** max 3 tiers on screen, one "Most Popular" elevated with gold border, trust microcopy under price ("No joining fee · Pause anytime · 7-day money-back") — Peloton pattern. Triple-intent CTAs site-wide (David Lloyd): `Book Free Trial` (gold, primary) · `Book a Tour` (outline) · `View Plans` (text link).
- **Trainer cards:** 3:4 duotone-on-hover portraits, specialty badges, years/certs, "Book PT session".
- **Timetable:** full-height sheet/drawer with day tabs, filter pills (branch, class type, trainer), skeleton loading.
- **Live occupancy meter:** SVG gauge per branch ("Comfortable / Busy / Peak") — a rare feature made into a visual signature.
- **Transformation gallery:** before/after slider (drag handle), member name + duration + program.

## 8. SVG Rules (hard requirement — NO icon fonts, NO emoji icons)

- All icons are inline SVGs: 24×24 viewBox, `stroke="currentColor"`, `stroke-width="1.5"`, one shared `<Icon>` component wrapping a local library (Lucide React is acceptable as it renders true inline SVGs; custom paths for brand-specific icons: barbell, kettlebell, heart-rate, body-scan, shaker).
- Decorative SVGs: grain filter, capacity rings, gauge meters, route-map of branches, animated logo mark. All theme-aware via `currentColor`.

## 9. "Doesn't look AI-built" checklist

1. No purple-blue gradient on white. No `Inter`-only typography. No emoji as icons. No three-equal-cards-with-icon-circle sections.
2. Real, specific copy everywhere — never "Lorem ipsum" or "Unlock your potential today!" Write like Equinox: understated, specific, amenity name-drops ("Eleiko platforms", "ice-bath recovery suite").
3. Asymmetry on purpose: alternate image left/right, vary section darkness, one oversized number per screen.
4. Photography treated as one shoot (the one-grade rule) — mixed-source stock is the #1 AI-site tell.
5. Micro-details: custom selection color (gold on black), custom scrollbar on dark sections, favicon + OG images, hover states on EVERY interactive element, real empty states in the admin panel.

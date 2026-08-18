# Research & References
### What we learned from cult.fit, the platforms that beat it, and the gym-software market

---

## PART A — DESIGN RESEARCH

### Cult.fit — honest assessment
Cult.fit is a **conversion-optimized utility site, not a design showpiece** — dense, carousel-heavy, promo-driven. It sells hard but never feels luxurious. That's the gap we exploit.

**What it does:** dark theme with teal accent on every CTA; static promo-banner hero (an ad slot, not a brand moment); six competing membership SKUs in the nav (ELITE, PRO, Home, Transform, Bootcamp, Transform Plus) causing choice paralysis; generic sans-serif with no typographic personality; small thumbnail-grade class cards; virtually zero motion design.

**Its genuine strength (steal this):** one-tap "JOIN" directly on class cards — duration, trainer, level badge visible without a detail page; "START A FREE TRIAL" everywhere, low friction. **Steal the flow, not the visuals.**

### Sites that beat it — technique per brand
| Brand | What to steal |
|---|---|
| **Equinox** | Photography carries ALL color; restrained neutrals; editorial fashion-grade image treatment; luxury amenity name-drops in copy ("Le Labo products, eucalyptus towels") |
| **Barry's** | One signature environmental color (Red Room red) owned across video, photo, and UI; full-screen video hero of the actual room; huge confident headlines |
| **Third Space London** | Full-bleed image ↔ constrained-text alternation rhythm; two-word manifesto headlines ("Training for life"); enquiry-modal for premium tiers |
| **Gymshark** | Hover secondary-image swap on cards; annotated photography callouts; activity-based mega-menu |
| **Peloton** | Trust microcopy under price ("30-day trial · 0% financing"); elegant large-card horizontal rails; verb-driven aspirational headlines |
| **David Lloyd** | Triple-intent CTAs everywhere (Enquire / Book a tour / Join online); nav by life-experience, not by SKU — the direct fix for cult.fit's nav |
| **Nike/NTC** | Two-font system (condensed display + neutral workhorse); line-height 0.9 uppercase display type; one pill radius token everywhere; monochrome chrome |
| **F45** | QR-to-app section; geo-personalized studio finder near hero |

**Awwwards-tier proof points:** Kinective Fitness Club (pure black/white + one coral accent — juror-cited header), ReShape (**black + gold #ECD06F — the canonical premium-gym palette**), Phive (parallax storytelling, SVG animation, sound design). Browse: awwwards.com/inspiration_search/fitness/

### 2025–26 trends worth using (validated, not gimmicks)
1. Dark-luxe as identity (pure-black OLED-first, one metallic/neon accent)
2. Kinetic typography — variable-font weight animating on scroll (hero + 2 section breaks max)
3. Scroll-triggered storytelling (GSAP ScrollTrigger / Framer Motion `useScroll`)
4. Video-first heroes: 5–10s muted loop + dark gradient overlay
5. Bento grids replacing uniform card rows
6. Film-grain overlays (SVG `feTurbulence`, 3–6% opacity) on dark sections
7. Big-number animated stat counters
8. Micro-interactions on conversion elements (magnetic buttons, hover swaps) — these convert; ambient 3D scenes don't
9. Marquee ticker strips as section dividers
10. Soft-brutalist badges/stickers layered over polished photography

### 4K imagery sources (free, commercial use)
- **Unsplash:** topic `unsplash.com/t/health-fitness`; searches: `gym dark`, `weightlifting black and white`, `barbell close up`, `boxing gym moody`, `athlete portrait dark background`, `spin class red light`, `yoga studio minimal`. Contributors: Anastase Maragos, Victor Freitas, John Arano.
- **Pexels:** `pexels.com/search/gym` (80k+); **Pexels Videos** for the hero loop: `weightlifting slow motion`, `boxing training` — trim to 6–10s, strip audio, WebM+MP4 at 2–4MB.
- Rules: heroes ≥3840px landscape; trainer cards 3:4 portrait; favor dark thirds for text placement; unify everything with one CSS grade (`contrast(1.08) saturate(0.85) brightness(0.92)`).

---

## PART B — FEATURE RESEARCH

*Compiled from Mindbody, ABC Glofox, Wodify, PushPress, Zen Planner, GymMaster, Exercise.com, Virtuagym, Hevy/TrueCoach/Trainerize, cult.fit ecosystem, and Indian platforms (Fitxzo, YDL, GymForce).*

### Standard expectations (absence = defect)
- Member lifecycle management (trial → active → frozen → expired → won-back), e-sign waivers, activity timeline
- Plan types: recurring, fixed-term, class packs, PT packs, day passes, trials; dunning + auto-suspension on arrears
- Class scheduling with capacity, booking/cancel windows, **waitlist auto-promotion** (now standard), trainer conflict detection
- QR self check-in + kiosk mode; attendance analytics with absentee alerts
- Staff roles/permissions, trainer payroll (per-class/per-session/commission), performance metrics
- Lead pipeline (inquiry → tour → trial → joined) with automated follow-ups — benchmark: text within 5 min of inquiry; auto follow-ups convert 40–60% more trials (PushPress)
- POS for supplements/merch with inventory and low-stock alerts; member "tab"
- Reports: MRR, churn, LTV, peak-hours heatmap, class fill rates, outstanding dues, lead conversion
- Workout/diet builders with exercise video library; bulk-assign programs
- Post-class ratings, NPS at 30/90 days, low-rating instant manager alerts (same-day contact recovers 60%+ of at-risk members)

### Rare differentiators (ranked by build-value for this project)
| Feature | Rarity | Notes |
|---|---|---|
| **Live gym occupancy / crowd meter** | RARE | Planet Fitness pattern; trivially derived from check-in data; perfect for multi-branch ("which branch is quieter right now") |
| **AI workout & diet plan generation** | EMERGING | Virtuagym MAX AI, cult.fit; almost no independent Indian gym has it; cheap now via LLM APIs |
| **Churn prediction + win-back automation** | RARE | Glofox AI is nearly alone; a rules-based version (visit-frequency drop alerts) captures 80% of the value |
| **Gamification: streaks, badges, leaderboards, challenges** | EMERGING | Wodify/Virtuagym; strong retention lever; rare in Indian gym software |
| **Body-composition scan tracking with charts** | EMERGING | Most Indian gyms with InBody machines record on paper — digitizing is huge perceived value |
| **Trainer marketplace: browse, book & pay PT in-app with commission split** | EMERGING | Booking is common; ratings+revenue-split marketplace is rare; monetization feature |
| **Community feed + auto PR shout-outs** | EMERGING | PushPress/Wodify/Hevy; belonging drives retention |
| **Hybrid home+gym programs / VOD library** | EMERGING | cult.fit's tiering model (ELITE all-access / PRO gym-focused) is the proven reference |
| **Off-peak membership tiers / seasonal offer engine** | RARE | The practical version of dynamic pricing (full dynamic pricing only exists inside Mindbody's marketplace) |
| **Corporate memberships** | EMERGING | Company code self-enrollment + HR usage reports; major revenue channel in Indian metros |
| **Form-check video uploads with trainer feedback** | RARE | TrueCoach core feature, absent from gym apps; premium PT differentiator |
| **Access-linked billing enforcement** | EMERGING | Expired/overdue member blocked at biometric device/turnstile (GymMaster); loved by Indian owners |

### Multi-branch essentials
Single member identity across branches; home-branch vs all-branch plan tiers; owner network-wide dashboard vs branch-manager scoped views; branch P&L side-by-side comparison + branch leaderboard; central plan catalog with branch price overrides (metro vs tier-2); per-branch timetables/rooms/inventory with inter-branch stock transfer; trainers across branches without double-booking; website leads auto-routed to nearest branch; per-branch occupancy meter.

### India-context essentials
- **Payments:** Razorpay primary — UPI dominant, cards, netbanking; UPI Autopay/e-mandates for recurring (RBI rules: pre-debit notification; design renewals payment-link-first); WhatsApp payment links (reduce overdue up to 40% — YDL); dynamic UPI QR at desk; cash/cheque/partial payments/EMI must be recordable.
- **GST:** gym services at **5% without ITC (effective 22 Sep 2025)**; retail items at their own rates → POS handles mixed-rate invoices; GSTIN, SAC codes, GSTR-ready reports.
- **Comms:** WhatsApp Business API is the primary channel; SMS via DLT-registered templates (TRAI); email secondary.
- **Identity:** mobile-number-first + OTP login; email optional; Android-first, low-end device performance matters.
- **Check-in:** biometric (eSSL/ZKTeco-class) is the entrenched expectation alongside QR; auto-block expired members at device.
- **Pricing norms:** monthly/quarterly/half-yearly/annual with steep long-term discounts (annual ≈ 50% of 12× monthly); admission fee common; heavy seasonal-offer culture (New Year, monsoon) → coupon engine matters; freeze requests frequent → self-serve freeze with policy limits. Market bands: budget ₹700–1,500/mo, mid ₹1,500–3,000, premium ₹3,000–6,000+.

---

*Full source links preserved in research transcripts; key ones: glofox.com (AI churn, multi-location), wodify.com, pushpress.com, zenplanner.com, gymmaster.com (access control), exercise.com, business.virtuagym.com (MAX AI, gamification), cult.fit (cultpass ELITE), mindbodyonline.com (dynamic pricing), inbody.in, hevyapp.com, truecoach.co, gymforce.in (UPI & GST, India pricing), charteredhelp.com (GST 2025-26), equinox.com, barrys.com, thirdspace.london, gymshark.com, onepeloton.com, davidlloyd.co.uk, awwwards.com/inspiration_search/fitness.*

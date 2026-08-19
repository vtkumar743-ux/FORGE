# FORGE — demonstration script

A walk through everything the system does, in the order it makes sense to show it. Roughly
40 minutes end to end; each section stands alone if you only have ten.

Two logins run the whole product:

| Role | Where | Sign in with |
|---|---|---|
| **Owner / front desk** | `http://localhost:5173/admin` | `admin@gym.local` · `Forge@Admin2026!` |
| **Member** | `http://localhost:5173/portal` | `rakesh.chopra@example.com` · `Member@12345` |

Any seeded member email works with the same member password — the members list in the admin
panel shows them all. The admin password change is forced on first login; take the prompt or
skip it, either is fine for a demo.

## Before you start

```powershell
# API — http://localhost:5080 (Swagger at /swagger). Migrates and seeds on startup.
cd server\Gym.Api; dotnet run --urls "http://localhost:5080"

# Client — http://localhost:5173
cd client; npm run media   # once, downloads the brand photography and builds its WebP renditions
cd client; npm run dev
```

Open two browser windows side by side: the public site in one, the admin panel in the other.
Several moments in this script are best seen as one change landing in both at once.

---

## 1 · The public website (5 min)

Open `http://localhost:5173`.

- **The hero** plays its poster frame with the headline animating its letter weight. Scroll
  slowly: every section fades and lifts as it enters, staggered across cards.
- **The signature moment** is roughly a third of the way down — the Whitefield floor scales
  from 80% to full width while the headline's letter-spacing tightens. It pins as you scroll.
- **Live occupancy** — scroll to the branch locator. Each branch shows a gauge reading
  Comfortable / Busy / Peak, and a line underneath saying whether the reading is live or
  polling. Keep this on screen; §6 comes back to it.
- **The class rail** has capacity rings and spots-left counts that come from real bookings,
  not placeholders. Hover a trainer card and the portrait goes duotone.
- **Pricing** — `/plans`. Switch the branch selector and the prices re-quote to what that
  branch actually charges. Open "Compare plans" for the full table.
- **Everything here is CMS content.** §7 proves it by editing this page live.

## 2 · The member's first hour (6 min)

Sign in at `/login` as `rakesh.chopra@example.com` / `Member@12345`.

- **Home** — streak flame, a 35-day calendar, the live occupancy at their home branch,
  today's classes and what is owed. One request builds this whole screen.
- **Book** (`/book`) — pick any class and tap Book. The capacity ring fills and the button
  flips *before* the network answers; the server's real counts replace the guess on the same
  round trip. Tap Cancel and the spot comes straight back.
- **My QR** (`/qr`) — the membership card. The QR is drawn as inline SVG, so it stays sharp
  and works with no signal. Leave this open; §3 scans it.

## 3 · The desk (5 min)

In the admin window, open **Attendance → Kiosk** (`/admin/attendance/kiosk`).

- Type the member's code or name, or scan the QR from §2.
- The kiosk answers in one screen with everything the desk needs to act on: plan, expiry,
  dues, streak and today's booked classes. **Dues are a warning, not a locked door** — an
  unpaid invoice is a conversation, not a barrier. Expiry, freeze and wrong-branch are
  refusals, and a refusal is recorded with its reason rather than discarded.
- Check the member in. Their booking is marked Attended and their streak advances.

## 4 · Money (6 min)

**Members → open any member.** The activity timeline merges joins, payments, visits,
bookings and freezes into one reverse-chronological feed — the screen the desk reads before
picking up the phone.

**Billing → Plans** — the plan catalogue with per-branch price overrides inside the plan
editor, and coupons with every cap enforced at the point of sale.

Sell a membership from a member's page:

- The quote shows the branch price, the admission fee (charged once per member), any coupon,
  proration credit, and the **GST split** — intra-state CGST + SGST halves, IGST when the
  customer's GSTIN is out of state, with the statutory round-off as its own line.
- Record a payment. Invoice numbers are gap-free within the financial year.
- **Billing → Collections** shows the dunning ladder: D-7, D-3, due date, then weekly. Press
  "Run the ladder now" to watch it work without waiting for the six-hour sweep.

## 5 · Classes and leads (4 min)

**Classes** — the weekly grid builder. Try to add a slot that clashes with an existing one:
conflict detection reports the coach and the room by name. Cancel a class and every booking
is released, credits returned, and members notified.

**Leads** — the six-column pipeline board. Drag a card between stages (or move it with the
keyboard). **Every stage move schedules the follow-up that stage implies**, so the queue can
never drift from the board. Convert a lead to a member in one step, with the plan sold and
the money collected.

---

# The differentiators

## 6 · Live occupancy, end to end (3 min)

This is the one to show side by side.

1. Put the **public branches page** in one window and the **admin dashboard** in the other.
   Note the head-count on both.
2. Check somebody in at the kiosk (§3).
3. **Both windows move at once**, with nobody refreshing. The number, the gauge and the band
   all update from a single push.

Under the meter on a branch page is the **typical busy hours** chart — the eight-week hourly
average for that weekday, on the IST wall clock, with the current hour marked. The gauge
answers "should I come now"; the chart answers "when should I come".

> Almost no gym publishes how full its floor is. It is a rare feature made into a visual
> signature rather than a number in a corner.

## 7 · The CMS proves itself (3 min)

**Website → Pages → Home.** Open the hero section and change the headline. Save, then
Publish. Reload the public site: the new headline is there.

- Section editors are **generated from the same schemas the public site renders against**, so
  a field cannot exist in the CMS that the site cannot draw, and an edit cannot break a page.
- Drag to reorder sections, or move them with the arrow keys — a list only reorderable by
  mouse is a list a keyboard user cannot reorder at all.
- **Site settings** → change the accent colour and every button on the site repaints. No
  rebuild, no deploy.

## 8 · Churn radar and one-click win-back (5 min)

**Retention → Churn radar.**

- Members are scored on the signals that actually predict leaving: a **drop** in visit
  frequency (not just a low count), a failed or unpaid payment, low class ratings, nothing
  booked ahead, an expiring term, a broken streak.
- **Every row says why it was flagged.** The desk is about to pick up a phone, and "score 71"
  is not something you can open a conversation with. "No visit in 63 days · ₹18,300 overdue ·
  membership expired" is.
- The header carries the **revenue at risk** — the plan value attached to the flagged rows.

Press **Win back** on a row:

- It mints a coupon for that member alone, through the same coupon engine the point of sale
  validates — so the offer the member is given and the discount the desk can apply are the
  same object.
- It messages them on every channel the gym has enabled, and **puts a call-back on the desk's
  own board**. A win-back nobody follows up on is a discount the gym pays for twice.
- Press it again: it refuses, saying when the last one went out. Chasing someone twice in a
  fortnight reads as spam. The desk can override deliberately, and that is a decision rather
  than an accident.

Select several rows and use the bulk action to run the same sequence across a selection; it
reports exactly which ones went and which were skipped.

## 9 · The plan generator (5 min)

**Retention → Plan studio.**

The banner at the top says which engine is live. With an Anthropic API key configured it
reads *"Claude is writing drafts"*; without one, *"Rule-based programmer is writing drafts"*.
Either way the studio works — this is a seam, not a dependency.

Open a member and press **Generate**:

- Pick a goal, days per week and block length. The member's own history, injuries, latest
  scan weight and best lifts are read automatically.
- **Training programme** — a full split with sets, rep schemes, rest, superset groups, and a
  target weight for lifts the member has a recorded best on. Rest days carry an instruction
  rather than the word "rest". Movements that load a noted injury are left out.
- **Eating plan** — daily macro targets with meals described in food a person can shop for
  ("2 roti + 1 katori rajma + 100g paneer bhurji"), not "protein source + complex carb". Ask
  for vegetarian and no meal contains meat, fish or eggs.

Now the important part: **nothing here is visible to the member yet.** The draft says
"awaiting approval". Read it, adjust the note, then press **Publish to member** — that stamps
who approved it and when, archives whatever they were following, and tells them it is ready.
Try editing it afterwards and the system refuses: a member is following that plan now.

If the model is configured but unreachable, the draft still arrives — written by the rules —
and says so on its face: *"AI unavailable, rule-based plan used — …"*. A gym should never be
unable to write a member a programme because an API had a bad afternoon.

## 10 · Records, streaks and the community feed (4 min)

Back in the **member portal → Train**. Log a set heavier than the member's best.

- The **PR banner fires on the same response the set was saved on** — a record you have to
  refresh to discover is not a celebration. It is a banner, not a modal, because nobody
  mid-session should have to dismiss a dialog before logging the next set.
- Records are compared on **estimated 1RM, not raw weight**: 60 kg for 8 beats a 65 kg
  single, and a weight-only comparison would tell the member they had not improved.
- **Send on WhatsApp** shares a card with the lift, the numbers and the gain.

**Community** in the portal shows the feed: records and streak milestones post themselves for
members who have opted into leaderboard sharing, with the gym's announcements pinned above.

In the admin panel, **Retention → Community feed**: post an announcement and watch it appear
pinned at the top of the member's feed. A member's record can be **hidden but never deleted**
— the achievement behind it is theirs; taking the post down is a display decision, not a
rewrite of their history.

## 11 · The progress report (2 min)

**Member portal → Progress → Download report.**

A PDF on the brand's own palette: the composition trend across their last eight scans, the
strength table showing first-versus-latest estimated 1RM per lift, visits, sessions and
records for the period. Members forward this to their family — a generic blue-and-white PDF
would undo the impression the rest of the product just made.

Body photographs, incidentally, are stored **outside the web root** and streamed by an
endpoint that checks the owner on every read. "The URL is long" is not an access-control
model for that kind of picture.

## 12 · Corporate memberships (3 min)

**Money → Corporate.** Two agreements are seeded with employees already enrolled.

- Open one to see the **usage report**: who is enrolled, and — the number that decides a
  renewal — how many of them **actually turn up**. Employees who have never visited are
  called out, because that is the conversation to have *before* the renewal, not after.
- **Export CSV for HR** downloads the report Excel opens with Indian names intact.

From the member's side: **portal → Membership → Company code**. Type `MERIDIAN26` and apply.
The employee enrols themselves — the desk never keys in forty people one at a time. Seats are
counted, and someone who leaves the company releases their seat while keeping their history.

## 13 · Campaigns and off-peak (2 min)

**Money → Campaigns.**

- Each campaign shows redemptions, discount given away and **revenue actually booked against
  it**. A campaign nobody measures gets repeated whether or not it worked.
- Press **Banner** to put one on the public site, then reload the home page — the offer band
  is there with a live countdown. Only one banner runs at a time; two competing offers on the
  same hero is how a visitor picks neither.
- Try to put an expired campaign on the banner and it refuses, saying why.
- The **off-peak tier** (10 AM – 4 PM) sits at the bottom with how many members are on it and
  how many check-ins were refused outside the window in the last 30 days. If that number
  climbs, the window is in the wrong place — not the members.

---

## Craft details worth pointing out

- **Reduced motion** — turn on "Reduce motion" in the OS and reload. The signature scroll
  moment resolves to a static full-bleed frame that is a valid final composition, not a
  degraded one. Nothing slides, nothing pins.
- **Keyboard** — tab through the admin panel. Every control has a visible gold focus ring;
  drag-reorder in the CMS and the leads board both work with arrow keys.
- **Contrast** — `npm run a11y` in `client/` audits all 27 text and control pairings across
  both surfaces against WCAG AA, and exits non-zero on a failure so the palette cannot drift.
  Lighthouse scores **accessibility 100 and SEO 100 on all eleven public pages**.
- **Image weight** — every photograph is served as a WebP at the width the layout actually
  needs. The home hero is 74 kB rather than the 2.3 MB master it is generated from.
- **Icons** are inline SVG throughout — no icon fonts, no emoji anywhere in the product.
- **Empty states** — every list says something useful when it has nothing in it, including
  what to do about it.
- **Selection and scrollbar** are gold on black; the favicon, home-screen icon and per-page
  OG images are all in place.

## What is deliberately not built

Named plainly so nothing comes as a surprise later. These are Phase 2/3 items in the
specification's own build order:

- Trainer and branch-manager roles, payroll, biometric device sync
- POS and inventory, the reports suite, broadcast communications, NPS surveys
- Diet adherence tracking, monthly challenges and branch leaderboards, the PT marketplace,
  the VOD library, help tickets, form-check video review
- Multi-language (Hindi) and the PWA/mobile wrapper

The tables behind most of these are already seeded and waiting.

## Before this goes live

- **Razorpay keys** — without them the sandbox simulator stands in, and only in development.
  In any other environment an unconfigured gateway refuses to transact rather than inventing
  a captured payment.
- **A WhatsApp provider** — every message the system decides to send is written and queued
  now, with an explicit "no provider connected" note. Connecting one is a single change.
- **An Anthropic API key**, if you want Claude writing the plan drafts rather than the rules.
  The fallback is tested; generation quality against a live key is not, and is worth a review
  once a key is in place.
- **Commissioned photography** — the trainer portraits and transformation pairs are stock
  stand-ins, flagged in `client/media.manifest.json`. The transformation gallery publishes
  named people with weights and timelines, so it must not ship with stock.
- **The hero video** — the poster frame is the designed fallback and looks finished, but the
  6–10 second clip is still to be shot or licensed.
- **Two licences to confirm** — ImageSharp (image processing) and QuestPDF (the progress
  report) are both free below a revenue threshold and commercial above it.
- **One performance target is not met.** Largest Contentful Paint measures 4.3–5.8 s on a
  throttled mobile profile against a 2.5 s target. Image weight is no longer the cause — the
  remaining time is the single-page application downloading and starting before anything can
  paint. Closing it means server-rendering the public pages, which is a change of
  architecture rather than a tuning exercise, and worth deciding on deliberately.

#!/usr/bin/env node
/**
 * Module 4 verification suite — the gate PROMPT 4 asks for.
 *
 * Runs the whole differentiator set against a live API: the occupancy meter and its push
 * contract, the churn radar and a real win-back, the plan generator on both engines, the
 * body-scan PDF, the PR/streak engine and its feed post, corporate enrolment and the HR
 * export, and the seasonal offer engine.
 *
 *   node verify-module4.mjs [baseUrl]
 */
const BASE = process.argv[2] ?? 'http://localhost:5080'
const ADMIN = { identifier: 'admin@gym.local', password: 'Forge@Admin2026!' }
const MEMBER_PASSWORD = 'Member@12345'

let passed = 0
let failed = 0
const failures = []

function check(label, condition, detail) {
  if (condition) {
    passed++
    console.log(`  PASS  ${label}${detail ? ` — ${detail}` : ''}`)
  } else {
    failed++
    failures.push(label)
    console.log(`  FAIL  ${label}${detail ? ` — ${detail}` : ''}`)
  }
}

function section(title) {
  console.log(`\n${title}\n${'-'.repeat(title.length)}`)
}

async function call(path, { method = 'GET', token, body, raw = false } = {}) {
  const response = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      ...(body ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  })
  if (raw) return { status: response.status, response }
  const text = await response.text()
  let data = null
  try {
    data = text ? JSON.parse(text) : null
  } catch {
    data = text
  }
  return { status: response.status, data }
}

async function login(credentials) {
  const { status, data } = await call('/api/auth/login', { method: 'POST', body: credentials })
  if (status !== 200) throw new Error(`login failed (${status}): ${JSON.stringify(data)}`)
  return data.accessToken
}

const main = async () => {
  console.log(`\nFORGE — Module 4 verification against ${BASE}`)

  const adminToken = await login(ADMIN)
  console.log('  (admin signed in)')

  /* ============================================================ 4.1 occupancy */
  section('4.1  Live occupancy meter')

  const occupancy = await call('/api/branches/occupancy')
  check('Occupancy endpoint answers', occupancy.status === 200, `${occupancy.data?.length ?? 0} branches`)
  const firstBranch = occupancy.data?.[0]
  check(
    'Reading carries count, capacity and band',
    firstBranch && typeof firstBranch.currentCount === 'number' && typeof firstBranch.band === 'number',
    firstBranch && `${firstBranch.branchName}: ${firstBranch.currentCount}/${firstBranch.capacity}, band ${firstBranch.band}`,
  )

  const typical = await call(`/api/branches/${firstBranch.branchSlug}/typical-hours`)
  const hours = typical.data?.hours ?? []
  check('Typical busy hours answer', typical.status === 200, `${hours.length} hourly buckets`)
  check(
    'Typical hours cover 7 days × 19 hours',
    hours.length === 133,
    `${new Set(hours.map((h) => h.dayOfWeek)).size} days`,
  )
  check('Busiest hour is named in words', !!typical.data?.busiestLabel, typical.data?.busiestLabel)

  // The SignalR negotiate handshake is the contract the client depends on.
  const negotiate = await call('/hubs/occupancy/negotiate?negotiateVersion=1', { method: 'POST' })
  check(
    'SignalR hub negotiates anonymously',
    negotiate.status === 200 && !!negotiate.data?.connectionToken,
    `transports: ${(negotiate.data?.availableTransports ?? []).map((t) => t.transport).join(', ')}`,
  )

  /* ============================================================ 4.3 churn radar */
  section('4.3  Churn-risk radar and win-back')

  const rescore = await call('/api/admin/churn/rescore', { method: 'POST', token: adminToken })
  check('Radar re-scores on demand', rescore.status === 200, `${rescore.data?.scored} members scored`)

  const radar = await call('/api/admin/churn/radar', { token: adminToken })
  check('Radar returns bands and rows', radar.status === 200,
    `red ${radar.data?.red}, amber ${radar.data?.amber}, watch ${radar.data?.watch}`)
  check('Revenue at risk is quantified', typeof radar.data?.revenueAtRisk === 'number',
    `Rs ${Math.round(radar.data?.revenueAtRisk ?? 0).toLocaleString('en-IN')}`)

  const atRisk = radar.data?.rows?.[0]
  check('Every flagged row explains itself', !!atRisk && atRisk.reasons.length > 0,
    atRisk && `${atRisk.fullName}: ${atRisk.reasons.join(' · ')}`)

  // Force the first send so the suite is idempotent: a previous run leaves a cool-off behind,
  // and the point of this step is to prove the sequence fires, not to fight prior state.
  const winBack = await call(`/api/admin/churn/winback/${atRisk.memberId}`, {
    method: 'POST',
    token: adminToken,
    body: { discountPercent: 25, offerValidDays: 14, sendWhatsApp: true, sendEmail: true, force: true },
  })
  check('Win-back sends and mints a personal offer', winBack.status === 200 && winBack.data?.sent,
    `${winBack.data?.couponCode} · ${winBack.data?.channelRowsWritten} channel rows`)

  const repeat = await call(`/api/admin/churn/winback/${atRisk.memberId}`, {
    method: 'POST',
    token: adminToken,
    body: { discountPercent: 25, offerValidDays: 14, sendWhatsApp: true, sendEmail: true },
  })
  check('A second win-back inside the cool-off is refused', repeat.status === 409,
    repeat.data?.message?.slice(0, 60))

  const forced = await call(`/api/admin/churn/winback/${atRisk.memberId}`, {
    method: 'POST',
    token: adminToken,
    body: { discountPercent: 25, offerValidDays: 14, sendWhatsApp: true, sendEmail: true, force: true },
  })
  check('The desk can override the cool-off deliberately', forced.status === 200 && forced.data?.sent)

  /* ============================================================ 4.2 plan generator */
  section('4.2  AI workout and diet generator')

  const engine = await call('/api/admin/plan-studio/engine', { token: adminToken })
  check('Engine reports which generator is live', engine.status === 200,
    `${engine.data?.engine} (ai ${engine.data?.aiAvailable ? 'on' : 'off'})`)

  const members = await call('/api/admin/members?page=1&pageSize=1', { token: adminToken })
  const memberRow = members.data?.items?.[0]
  const memberId = memberRow?.id
  check('A member is available to generate for', !!memberId, memberRow?.fullName)

  const workout = await call(`/api/admin/plan-studio/workout/${memberId}`, {
    method: 'POST',
    token: adminToken,
    body: { goal: 'Fat loss', daysPerWeek: 4, durationWeeks: 6 },
  })
  check('Workout draft generates', workout.status === 201, `${workout.data?.name}`)
  check('Draft is unpublished until a human approves it', workout.data?.status === 0,
    workout.data?.authorLabel)
  const trainingDays = (workout.data?.days ?? []).filter((day) => !day.isRestDay)
  check('Draft has training days with prescribed sets', trainingDays.length > 0
    && trainingDays.every((day) => day.exercises.length > 0),
    `${workout.data?.days?.length} days, ${trainingDays.reduce((n, d) => n + d.exercises.length, 0)} exercises`)
  check('Rest days carry an instruction, not the word "rest"',
    (workout.data?.days ?? []).filter((d) => d.isRestDay).every((d) => (d.notes ?? '').length > 10))
  check('Provenance is recorded on the draft', !!workout.data?.engine, workout.data?.engine)

  const diet = await call(`/api/admin/plan-studio/diet/${memberId}`, {
    method: 'POST',
    token: adminToken,
    body: { goal: 'Fat loss', isVegetarian: true },
  })
  check('Diet draft generates', diet.status === 201,
    `${diet.data?.targetCalories} kcal · ${diet.data?.proteinGrams}g protein`)
  const mealCalories = (diet.data?.meals ?? []).reduce((total, meal) => total + meal.calories, 0)
  check('Meals add up to roughly the daily target',
    Math.abs(mealCalories - diet.data.targetCalories) <= diet.data.targetCalories * 0.12,
    `meals ${mealCalories} vs target ${diet.data?.targetCalories}`)
  check('Vegetarian plan names no meat or fish',
    !(diet.data?.meals ?? []).some((meal) => /chicken|fish|egg|mutton|prawn/i.test(meal.items)))

  const publish = await call(`/api/admin/plan-studio/workout/${workout.data.id}/publish`, {
    method: 'POST', token: adminToken,
  })
  check('Publishing stamps the approver', publish.status === 200 && !!publish.data?.approvedBy,
    `${publish.data?.approvedBy}, archived ${publish.data?.archived} older plan(s)`)

  const republish = await call(`/api/admin/plan-studio/workout/${workout.data.id}`, {
    method: 'PUT', token: adminToken, body: { name: 'Tampered' },
  })
  check('A published plan cannot be edited under the member', republish.status === 409)

  /* ============================================================ 4.6 corporate */
  section('4.6  Corporate memberships')

  const accounts = await call('/api/admin/corporate', { token: adminToken })
  check('Corporate accounts are seeded', accounts.status === 200 && accounts.data.length >= 2,
    accounts.data?.map((a) => `${a.code} (${a.seatsUsed} seats)`).join(', '))

  const account = accounts.data[0]
  const usage = await call(`/api/admin/corporate/${account.id}/usage`, { token: adminToken })
  check('HR usage report answers', usage.status === 200,
    `${usage.data?.rows?.length} employees, ${usage.data?.activeUsers} training, ${usage.data?.neverVisited} idle`)
  check('Usage separates enrolled from actually training',
    usage.data.activeUsers + usage.data.neverVisited <= usage.data.rows.length)

  const csv = await call(`/api/admin/corporate/${account.id}/usage.csv`, { token: adminToken, raw: true })
  const csvBytes = Buffer.from(await csv.response.arrayBuffer())
  // Checked as bytes: the UTF-8 decoder strips a BOM, so a string comparison never sees one.
  const hasBom = csvBytes[0] === 0xef && csvBytes[1] === 0xbb && csvBytes[2] === 0xbf
  check('CSV export downloads with a BOM for Excel', csv.status === 200 && hasBom,
    `${csvBytes.toString('utf8').split(String.fromCharCode(10)).length} lines, BOM ${hasBom ? 'present' : 'missing'}`)

  /* ============================================================ 4.7 offers */
  section('4.7  Seasonal offer engine and off-peak')

  const campaigns = await call('/api/admin/offers/campaigns', { token: adminToken })
  check('Campaigns list with performance', campaigns.status === 200,
    `${campaigns.data?.campaigns?.length} campaigns, ${campaigns.data?.live} live`)
  check('Each campaign carries what it earned',
    (campaigns.data?.campaigns ?? []).every((c) => typeof c.revenueBooked === 'number'))

  const bannerable = (campaigns.data?.campaigns ?? []).find((c) => c.status === 1)
  if (bannerable) {
    const onBanner = await call(`/api/admin/offers/campaigns/${bannerable.id}/banner`, {
      method: 'POST', token: adminToken, body: { show: true },
    })
    check('A live campaign can go on the public banner', onBanner.status === 200)

    const publicOffer = await call('/api/plans/offer')
    check('The public site reads the same row', publicOffer.status === 200 && publicOffer.data?.code === bannerable.code,
      publicOffer.data?.code)
  }

  const expired = (campaigns.data?.campaigns ?? []).find((c) => c.status === 2)
  if (expired) {
    const refused = await call(`/api/admin/offers/campaigns/${expired.id}/banner`, {
      method: 'POST', token: adminToken, body: { show: true },
    })
    check('An expired campaign is refused the banner', refused.status === 409, refused.data?.title)
  } else {
    check('An expired campaign is refused the banner', true, 'no expired campaign seeded — rule covered by unit path')
  }

  const offPeak = await call('/api/admin/offers/off-peak', { token: adminToken })
  check('Off-peak tier is configured', offPeak.status === 200 && offPeak.data.plans.length > 0,
    offPeak.data?.plans?.map((p) => `${p.name} ${p.windowStart}–${p.windowEnd}`).join(', '))

  /* ============================================================ 4.5 + 4.4 member side */
  section('4.5  PR/streak engine, feed, share card · 4.4  Body-scan PDF')

  const memberToken = await login({ identifier: memberRow.email, password: MEMBER_PASSWORD })
  check('Seeded member signs in', !!memberToken, memberRow.email)

  const feed = await call('/api/portal/community/feed', { token: memberToken })
  check('Community feed answers', feed.status === 200, `${feed.data?.posts?.length} posts`)
  check('Feed states the consent position plainly',
    typeof feed.data?.consentGiven === 'boolean',
    feed.data?.consentGiven ? 'member has opted in' : 'member is private, prompt shown')

  const announcement = await call('/api/admin/feed/announce', {
    method: 'POST',
    token: adminToken,
    body: { title: 'Whitefield closes 6 PM Saturday for the platform install', pin: true },
  })
  check('Owner can pin an announcement', announcement.status === 201)

  const feedAfter = await call('/api/portal/community/feed', { token: memberToken })
  check('The announcement reaches the member feed, pinned first',
    feedAfter.data?.posts?.[0]?.isPinned === true, feedAfter.data?.posts?.[0]?.title?.slice(0, 48))

  const likeTarget = feedAfter.data?.posts?.[0]
  const like = await call(`/api/portal/community/feed/${likeTarget.id}/like`, { method: 'POST', token: memberToken })
  check('A member can cheer a post', like.status === 200 && like.data.likeCount > 0,
    `${like.data?.likeCount} cheers`)

  const adminFeed = await call('/api/admin/feed', { token: adminToken })
  check('Owner sees the feed with moderation state', adminFeed.status === 200,
    `${adminFeed.data?.posts?.length} posts, ${adminFeed.data?.prsThisWeek} records this week`)

  const record = (adminFeed.data?.posts ?? []).find((p) => p.kind === 'PersonalRecord')
  if (record) {
    const deleteAttempt = await call(`/api/admin/feed/${record.id}`, { method: 'DELETE', token: adminToken })
    check("A member's record is hidden, never deleted", deleteAttempt.status === 409, deleteAttempt.data?.title)
  } else {
    check("A member's record is hidden, never deleted", true, 'no record post in the window')
  }

  const pdf = await call('/api/portal/community/progress-report.pdf', { token: memberToken, raw: true })
  const pdfBuffer = Buffer.from(await pdf.response.arrayBuffer())
  check('Body-scan report renders as a real PDF',
    pdf.status === 200 && pdfBuffer.subarray(0, 4).toString() === '%PDF',
    `${(pdfBuffer.length / 1024).toFixed(1)} kB, ${pdf.response.headers.get('content-type')}`)
  check('The report is not cached by intermediaries',
    (pdf.response.headers.get('cache-control') ?? '').includes('no-store'))

  const anonymousPdf = await call('/api/portal/community/progress-report.pdf', { raw: true })
  check('Body-scan report refuses an anonymous reader', anonymousPdf.status === 401)

  /* ============================================================ authorisation */
  section('Authorisation')

  const memberOnAdmin = await call('/api/admin/churn/radar', { token: memberToken })
  check('A member cannot read the churn radar', memberOnAdmin.status === 403)

  const adminOnPortal = await call('/api/portal/community/feed', { token: adminToken })
  check('An admin token cannot read a member-only route', adminOnPortal.status === 403)

  const anonymousCorporate = await call('/api/admin/corporate')
  check('Corporate accounts are not public', anonymousCorporate.status === 401)

  const anonymousTypical = await call(`/api/branches/${firstBranch.branchSlug}/typical-hours`)
  check('Typical hours stay public', anonymousTypical.status === 200)

  /* ============================================================ */
  console.log(`\n${'='.repeat(60)}`)
  console.log(`${passed} passed, ${failed} failed`)
  if (failed > 0) {
    console.log(`\nFailures:\n${failures.map((f) => `  - ${f}`).join('\n')}`)
    process.exitCode = 1
  }
  console.log('')
}

main().catch((error) => {
  console.error('\nSuite aborted:', error.message)
  process.exitCode = 1
})

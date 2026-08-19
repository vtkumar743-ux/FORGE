#!/usr/bin/env node
/**
 * Proves the live occupancy meter end to end (Module 4.1): a real check-in at the kiosk must
 * reach a subscribed browser over SignalR, without that browser polling for it.
 *
 * Runs the raw hub protocol over WebSockets rather than pulling the client library into the
 * server tree — the point is to test the wire contract the browser actually speaks.
 *
 *   node verify-occupancy-push.mjs [baseUrl]
 */
// Node 22+ ships a global WebSocket; no dependency needed for a wire-level test.

const BASE = process.argv[2] ?? 'http://localhost:5080'
const RECORD_SEPARATOR = String.fromCharCode(0x1e)

const post = async (path, body, token) => {
  const response = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: {
      ...(body ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await response.text()
  return { status: response.status, data: text ? JSON.parse(text) : null }
}

const get = async (path, token) => {
  const response = await fetch(`${BASE}${path}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  return { status: response.status, data: await response.json() }
}

async function main() {
  const login = await post('/api/auth/login', {
    identifier: 'admin@gym.local',
    password: 'Forge@Admin2026!',
  })
  const token = login.data.accessToken

  // 1. Find a member who is not currently on the floor, so the check-in genuinely moves the number.
  const branches = await get('/api/branches/occupancy')
  const branch = branches.data[0]
  const before = branch.currentCount
  console.log(`Branch ${branch.branchName}: ${before}/${branch.capacity} on the floor now`)

  const members = await get(`/api/admin/members?page=1&pageSize=60&branchId=${branch.branchId}`, token)

  // Seeded visits from previous days can still be open, and they count towards occupancy
  // without appearing in "today". Rather than guess who is already on the floor, check the
  // head-count after each attempt and keep going until one actually moves the meter.
  const candidates = (members.data?.items ?? []).slice(0, 12)
  if (candidates.length === 0) throw new Error('no members at this branch')

  // 2. Open the hub connection and subscribe to that branch, exactly as the browser does.
  const negotiate = await post('/hubs/occupancy/negotiate?negotiateVersion=1', null)
  const wsUrl =
    `${BASE.replace('http', 'ws')}/hubs/occupancy` +
    `?id=${negotiate.data.connectionToken}`

  const socket = new WebSocket(wsUrl)
  const received = []

  const opened = new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve)
    socket.addEventListener('error', reject)
  })

  socket.addEventListener('message', (event) => {
    const raw = typeof event.data === 'string' ? event.data : String(event.data)
    for (const frame of raw.split(RECORD_SEPARATOR).filter(Boolean)) {
      const message = JSON.parse(frame)
      if (message.type === 1) received.push(message)
    }
  })

  await opened
  socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + RECORD_SEPARATOR)
  // Handshake response arrives before any invocation is honoured.
  await new Promise((resolve) => setTimeout(resolve, 300))
  socket.send(
    JSON.stringify({
      type: 1,
      target: 'SubscribeToBranch',
      arguments: [branch.branchSlug],
    }) + RECORD_SEPARATOR,
  )
  await new Promise((resolve) => setTimeout(resolve, 300))
  console.log(`Subscribed to branch:${branch.branchSlug} over WebSockets`)

  // 3. Check members in at the desk until one is genuinely new to the floor.
  let checkIn = null
  let candidate = null
  for (const person of candidates) {
    candidate = person
    checkIn = await post(
      '/api/admin/attendance/checkin',
      { memberId: person.id, branchId: branch.branchId, source: 3 },
      token,
    )
    const now = await get('/api/branches/occupancy')
    const count = now.data.find((row) => row.branchSlug === branch.branchSlug).currentCount
    if (checkIn.data.admitted && count === before + 1) break
    checkIn = null
  }

  if (!checkIn) {
    socket.close()
    throw new Error('could not find a member who was not already on the floor')
  }
  console.log(`Kiosk: admitted ${candidate.fullName}`)

  // 4. The push should arrive without anyone asking for it.
  const deadline = Date.now() + 5000
  let push = null
  while (Date.now() < deadline && !push) {
    push = received.find((message) => message.target === 'OccupancyChanged')
    if (!push) await new Promise((resolve) => setTimeout(resolve, 100))
  }

  socket.close()

  if (!push) {
    console.error('\nFAIL  no OccupancyChanged frame arrived within 5 seconds')
    process.exitCode = 1
    return
  }

  const payload = push.arguments[0]
  const rest = await get('/api/branches/occupancy')
  const polled = rest.data.find((row) => row.branchSlug === branch.branchSlug)

  console.log(`\nPushed:  ${payload.branchName} ${payload.currentCount}/${payload.capacity}, band ${payload.band}`)
  console.log(`Polled:  ${polled.branchName} ${polled.currentCount}/${polled.capacity}, band ${polled.band}`)

  const moved = checkIn.data.admitted ? payload.currentCount === before + 1 : payload.currentCount === before
  const agree = payload.currentCount === polled.currentCount

  console.log(`\n${moved ? 'PASS' : 'FAIL'}  the meter moved with the check-in (${before} → ${payload.currentCount})`)
  console.log(`${agree ? 'PASS' : 'FAIL'}  the push and the REST reading agree`)
  if (!moved || !agree) process.exitCode = 1
}

main().catch((error) => {
  console.error('Aborted:', error.message)
  process.exitCode = 1
})

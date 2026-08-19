import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { getAccessToken } from './api'
import type { BranchOccupancy } from './cms'

/**
 * The client half of the live occupancy meter (Module 4.1).
 *
 * One connection per tab, shared by every component that asks — the public branch page, the
 * portal home and the admin dashboard can all be open at once, and none of them should open
 * a second socket. Subscriptions are reference-counted so the last component to unmount
 * releases the group, and the connection itself is torn down when nothing is listening.
 */

export type SessionCapacityUpdate = {
  classSessionId: number
  bookedCount: number
  capacity: number
  waitlistCount: number
  spotsLeft: number
}

export type WaitlistPromotion = {
  bookingId: number
  classSessionId: number
  memberId: number
  className: string
  startsAtUtc: string
}

type OccupancyHandler = (occupancy: BranchOccupancy) => void
type CapacityHandler = (update: SessionCapacityUpdate) => void
type PromotionHandler = (promotion: WaitlistPromotion) => void

const occupancyHandlers = new Set<OccupancyHandler>()
const capacityHandlers = new Set<CapacityHandler>()
const promotionHandlers = new Set<PromotionHandler>()

/** branch slug (or the literal "network") → how many components want it. */
const groupCounts = new Map<string, number>()

let connection: HubConnection | null = null
let starting: Promise<void> | null = null

const statusListeners = new Set<(status: RealtimeStatus) => void>()
export type RealtimeStatus = 'idle' | 'connecting' | 'live' | 'reconnecting' | 'offline'
let status: RealtimeStatus = 'idle'

function setStatus(next: RealtimeStatus) {
  if (status === next) return
  status = next
  statusListeners.forEach((listener) => listener(next))
}

function ensureConnection(): HubConnection {
  if (connection) return connection

  connection = new HubConnectionBuilder()
    .withUrl('/hubs/occupancy', {
      // The hub is anonymous for public pages; an admin subscribing to the whole network
      // needs the token, and SignalR cannot set a header on the websocket handshake.
      accessTokenFactory: () => getAccessToken() ?? '',
    })
    // Backing off rather than hammering: a gym network with flaky wifi should not turn
    // every phone in the building into a reconnect loop.
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
    .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
    .build()

  connection.on('OccupancyChanged', (payload: BranchOccupancy) => {
    occupancyHandlers.forEach((handler) => handler(payload))
  })
  connection.on('SessionCapacityChanged', (payload: SessionCapacityUpdate) => {
    capacityHandlers.forEach((handler) => handler(payload))
  })
  connection.on('WaitlistPromoted', (payload: WaitlistPromotion) => {
    promotionHandlers.forEach((handler) => handler(payload))
  })

  connection.onreconnecting(() => setStatus('reconnecting'))
  connection.onreconnected(async () => {
    setStatus('live')
    // Group membership does not survive a reconnect — re-join everything still wanted.
    await Promise.all([...groupCounts.keys()].map((group) => joinGroup(group)))
  })
  connection.onclose(() => {
    setStatus('offline')
    starting = null
  })

  return connection
}

async function start(): Promise<void> {
  const hub = ensureConnection()
  if (hub.state === HubConnectionState.Connected) return
  setStatus('connecting')
  starting ??= hub
    .start()
    .then(() => setStatus('live'))
    .catch((error) => {
      setStatus('offline')
      starting = null
      // Polling stays in place underneath, so a failed socket degrades to a slower meter
      // rather than an empty one. Nothing here should reach the user as an error.
      if (import.meta.env.DEV) console.warn('[realtime] hub connection failed', error)
    })
  return starting
}

async function joinGroup(group: string): Promise<void> {
  const hub = ensureConnection()
  if (hub.state !== HubConnectionState.Connected) return
  try {
    if (group === 'network') await hub.invoke('SubscribeToNetwork')
    else await hub.invoke('SubscribeToBranch', group)
  } catch (error) {
    if (import.meta.env.DEV) console.warn(`[realtime] could not join ${group}`, error)
  }
}

async function leaveGroup(group: string): Promise<void> {
  const hub = connection
  if (!hub || hub.state !== HubConnectionState.Connected) return
  if (group === 'network') return // the network group is released when the connection closes
  try {
    await hub.invoke('UnsubscribeFromBranch', group)
  } catch {
    /* a group we cannot leave is released when the socket closes */
  }
}

async function retain(group: string): Promise<void> {
  const count = groupCounts.get(group) ?? 0
  groupCounts.set(group, count + 1)
  await start()
  if (count === 0) await joinGroup(group)
}

async function release(group: string): Promise<void> {
  const count = groupCounts.get(group) ?? 0
  if (count <= 1) {
    groupCounts.delete(group)
    await leaveGroup(group)
  } else {
    groupCounts.set(group, count - 1)
  }

  if (groupCounts.size === 0 && connection) {
    const hub = connection
    connection = null
    starting = null
    setStatus('idle')
    await hub.stop().catch(() => undefined)
  }
}

/**
 * Subscribes to live occupancy for the given branch slugs (or the whole network for admin).
 * Returns the pushed readings keyed by slug plus the connection status, so a surface can say
 * "live" honestly and fall back to its polled figures when it cannot.
 */
export function useLiveOccupancy(
  slugs: string[] | 'network',
  options: { enabled?: boolean } = {},
): { updates: Record<string, BranchOccupancy>; status: RealtimeStatus } {
  const enabled = options.enabled ?? true
  const [updates, setUpdates] = useState<Record<string, BranchOccupancy>>({})
  const [connectionStatus, setConnectionStatus] = useState<RealtimeStatus>(status)

  // A fresh array literal every render would otherwise re-subscribe on every render.
  const key = slugs === 'network' ? 'network' : [...slugs].sort().join(',')
  const wanted = useRef<Set<string>>(new Set())
  wanted.current = new Set(slugs === 'network' ? [] : slugs)

  useEffect(() => {
    if (!enabled) return
    const groups = key === 'network' ? ['network'] : key.split(',').filter(Boolean)
    if (groups.length === 0) return

    const handler: OccupancyHandler = (payload) => {
      // The network group carries every branch; a branch page keeps only its own.
      if (key !== 'network' && !wanted.current.has(payload.branchSlug)) return
      setUpdates((current) => ({ ...current, [payload.branchSlug]: payload }))
    }
    occupancyHandlers.add(handler)
    statusListeners.add(setConnectionStatus)
    setConnectionStatus(status)

    groups.forEach((group) => void retain(group))

    return () => {
      occupancyHandlers.delete(handler)
      statusListeners.delete(setConnectionStatus)
      groups.forEach((group) => void release(group))
    }
  }, [key, enabled])

  return { updates, status: connectionStatus }
}

/** Live capacity pushes for the booking sheet — the counts the optimistic UI reconciles against. */
export function useLiveSessionCapacity(
  branchSlugs: string[],
  onUpdate: (update: SessionCapacityUpdate) => void,
  options: { enabled?: boolean } = {},
): void {
  const enabled = options.enabled ?? true
  const key = [...branchSlugs].sort().join(',')
  const callback = useRef(onUpdate)
  callback.current = onUpdate

  useEffect(() => {
    if (!enabled) return
    const groups = key.split(',').filter(Boolean)
    if (groups.length === 0) return

    const handler: CapacityHandler = (update) => callback.current(update)
    capacityHandlers.add(handler)
    groups.forEach((group) => void retain(group))

    return () => {
      capacityHandlers.delete(handler)
      groups.forEach((group) => void release(group))
    }
  }, [key, enabled])
}

/** Waitlist promotions, so a member watching the sheet sees their own promotion land. */
export function useWaitlistPromotions(
  branchSlugs: string[],
  onPromoted: (promotion: WaitlistPromotion) => void,
  options: { enabled?: boolean } = {},
): void {
  const enabled = options.enabled ?? true
  const key = [...branchSlugs].sort().join(',')
  const callback = useRef(onPromoted)
  callback.current = onPromoted

  useEffect(() => {
    if (!enabled) return
    const groups = key.split(',').filter(Boolean)
    if (groups.length === 0) return

    const handler: PromotionHandler = (promotion) => callback.current(promotion)
    promotionHandlers.add(handler)
    groups.forEach((group) => void retain(group))

    return () => {
      promotionHandlers.delete(handler)
      groups.forEach((group) => void release(group))
    }
  }, [key, enabled])
}

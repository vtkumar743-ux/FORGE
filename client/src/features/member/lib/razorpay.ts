import type { GatewayOrder } from './types'

/* ============================================================================
   Razorpay checkout

   Two paths, one caller. With real keys the browser loads Razorpay's widget and
   comes back with a signature the API verifies. Without keys — the sandbox
   simulator the API stands in during development — there is no widget to open,
   so we settle straight away with placeholder ids and the API stamps the payment
   as simulated. The member sees the same flow either way; the receipt does not lie.
   ============================================================================ */

const SCRIPT_SRC = 'https://checkout.razorpay.com/v1/checkout.js'

interface RazorpayHandlerResponse {
  razorpay_order_id: string
  razorpay_payment_id: string
  razorpay_signature: string
}

interface RazorpayInstance {
  open: () => void
  on: (event: string, handler: (payload: unknown) => void) => void
}

declare global {
  interface Window {
    Razorpay?: new (options: Record<string, unknown>) => RazorpayInstance
  }
}

let scriptPromise: Promise<boolean> | null = null

function loadScript(): Promise<boolean> {
  if (typeof window === 'undefined') return Promise.resolve(false)
  if (window.Razorpay) return Promise.resolve(true)

  scriptPromise ??= new Promise<boolean>((resolve) => {
    const existing = document.querySelector<HTMLScriptElement>(`script[src="${SCRIPT_SRC}"]`)
    if (existing) {
      existing.addEventListener('load', () => resolve(true))
      existing.addEventListener('error', () => resolve(false))
      return
    }

    const script = document.createElement('script')
    script.src = SCRIPT_SRC
    script.async = true
    script.onload = () => resolve(true)
    script.onerror = () => resolve(false)
    document.body.appendChild(script)
  })

  return scriptPromise
}

export interface CheckoutOutcome {
  orderId: string
  paymentId: string
  signature?: string
  simulated: boolean
}

export class CheckoutCancelled extends Error {
  constructor() {
    super('Payment window closed before it finished.')
    this.name = 'CheckoutCancelled'
  }
}

/**
 * Resolves once the member has paid, rejects with <see cref="CheckoutCancelled"/>
 * if they dismissed the widget. Never resolves on a half-finished payment: the
 * webhook is the backstop, and pretending otherwise would credit an invoice twice.
 */
export async function openCheckout(
  order: GatewayOrder,
  meta: { brandName: string; description: string },
): Promise<CheckoutOutcome> {
  if (order.isSimulated || !order.keyId) {
    // No keys configured: nothing to open. The ids are placeholders and the API
    // marks the payment row as simulated, so the audit trail says what happened.
    return {
      orderId: order.orderId,
      paymentId: `pay_SIM${order.orderId.slice(-10)}`,
      simulated: true,
    }
  }

  const ready = await loadScript()
  if (!ready || !window.Razorpay) {
    throw new Error('Could not reach the payment provider. Check your connection and try again.')
  }

  return new Promise<CheckoutOutcome>((resolve, reject) => {
    const checkout = new window.Razorpay!({
      key: order.keyId,
      order_id: order.orderId,
      amount: Math.round(order.amountInr * 100),
      currency: order.currency,
      name: meta.brandName,
      description: meta.description,
      prefill: {
        name: order.prefillName,
        email: order.prefillEmail ?? undefined,
        contact: order.prefillContact,
      },
      theme: { color: '#ECD06F' },
      handler: (response: RazorpayHandlerResponse) =>
        resolve({
          orderId: response.razorpay_order_id,
          paymentId: response.razorpay_payment_id,
          signature: response.razorpay_signature,
          simulated: false,
        }),
      modal: { ondismiss: () => reject(new CheckoutCancelled()) },
    })

    checkout.on('payment.failed', (payload: unknown) => {
      const description =
        (payload as { error?: { description?: string } })?.error?.description ?? 'The payment did not go through.'
      reject(new Error(description))
    })

    checkout.open()
  })
}

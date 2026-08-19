#!/usr/bin/env node
/**
 * Signs into the admin panel and audits what is actually rendered.
 *
 * The palette audit reasons about tokens; this reasons about pixels. It walks every visible
 * text node on each admin screen, reads the *computed* colour and the colour actually behind
 * it, and reports anything below WCAG AA. That is the only way to catch a colour that only
 * goes wrong once a class, a theme override and an opacity have been combined by the browser.
 *
 * It also checks that the shell scrolls its content rather than the page, and screenshots
 * each screen for eyeballing.
 *
 *   node scripts/audit-admin.mjs [baseUrl]
 */
import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import puppeteer from 'puppeteer-core'

const BASE = process.argv[2] ?? 'http://localhost:4173'
const CHROME =
  process.env.CHROME_PATH ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe'
const here = dirname(fileURLToPath(import.meta.url))
const shotDir = join(here, '..', '..', '.audit-shots')

const SCREENS = [
  ['dashboard', '/admin'],
  ['members', '/admin/members'],
  ['leads', '/admin/leads'],
  ['attendance', '/admin/attendance'],
  ['churn', '/admin/churn'],
  ['plan-studio', '/admin/plan-studio'],
  ['corporate', '/admin/corporate'],
  ['offers', '/admin/offers'],
  ['feed', '/admin/feed'],
  ['invoices', '/admin/billing/invoices'],
  ['plans', '/admin/billing/plans'],
  ['cms', '/admin/cms'],
]

/** Runs in the page: every visible text node, its colour, and the colour painted behind it. */
const CONTRAST_SWEEP = `(() => {
  const parse = (value) => {
    const m = value.match(/rgba?\\(([^)]+)\\)/)
    if (!m) return null
    const parts = m[1].split(',').map((p) => parseFloat(p))
    return { r: parts[0], g: parts[1], b: parts[2], a: parts.length > 3 ? parts[3] : 1 }
  }
  const lum = ({ r, g, b }) => {
    const f = (c) => { c /= 255; return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4) }
    return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b)
  }
  const ratio = (fg, bg) => {
    const a = lum(fg), b = lum(bg)
    const [hi, lo] = a > b ? [a, b] : [b, a]
    return (hi + 0.05) / (lo + 0.05)
  }
  const over = (fg, bg) => ({
    r: fg.r * fg.a + bg.r * (1 - fg.a),
    g: fg.g * fg.a + bg.g * (1 - fg.a),
    b: fg.b * fg.a + bg.b * (1 - fg.a),
    a: 1,
  })
  // Walk up until something actually paints, compositing translucent layers on the way.
  const backgroundOf = (el) => {
    let stack = []
    let node = el
    while (node && node !== document.documentElement) {
      const bg = parse(getComputedStyle(node).backgroundColor)
      if (bg && bg.a > 0) { stack.push(bg); if (bg.a === 1) break }
      node = node.parentElement
    }
    let base = { r: 255, g: 255, b: 255, a: 1 }
    for (let i = stack.length - 1; i >= 0; i--) base = over(stack[i], base)
    return base
  }

  const results = []
  const seen = new Set()
  document.querySelectorAll('body *').forEach((el) => {
    const text = Array.from(el.childNodes)
      .filter((n) => n.nodeType === 3)
      .map((n) => n.textContent.trim())
      .join(' ')
      .trim()
    if (!text) return
    const rect = el.getBoundingClientRect()
    if (rect.width === 0 || rect.height === 0) return
    const style = getComputedStyle(el)
    if (style.visibility === 'hidden' || style.display === 'none') return
    if (parseFloat(style.opacity) < 0.15) return

    const fg = parse(style.color)
    if (!fg) return
    const bg = backgroundOf(el)
    const composited = fg.a < 1 ? over(fg, bg) : fg
    const r = ratio(composited, bg)

    const size = parseFloat(style.fontSize)
    const weight = parseInt(style.fontWeight, 10) || 400
    const large = size >= 24 || (size >= 18.66 && weight >= 700)
    const required = large ? 3 : 4.5
    if (r >= required) return

    const key = text.slice(0, 40) + '|' + style.color
    if (seen.has(key)) return
    seen.add(key)
    results.push({
      text: text.slice(0, 60),
      colour: style.color,
      background: 'rgb(' + Math.round(bg.r) + ',' + Math.round(bg.g) + ',' + Math.round(bg.b) + ')',
      ratio: Math.round(r * 100) / 100,
      required,
      size: Math.round(size),
      selector: el.tagName.toLowerCase() + (el.className && typeof el.className === 'string' ? '.' + el.className.split(' ').slice(0, 2).join('.') : ''),
    })
  })
  return results
})()`

const main = async () => {
  await mkdir(shotDir, { recursive: true })

  const browser = await puppeteer.launch({
    executablePath: CHROME,
    headless: 'new',
    args: ['--no-sandbox', '--window-size=1440,900'],
    defaultViewport: { width: 1440, height: 900 },
  })

  const page = await browser.newPage()

  // ---- sign in
  await page.goto(`${BASE}/login`, { waitUntil: 'networkidle2' })
  await page.type('input[type="text"], input[name="identifier"], input[type="email"]', 'admin@gym.local')
  await page.type('input[type="password"]', 'Forge@Admin2026!')
  await Promise.all([
    page.click('button[type="submit"]'),
    page.waitForNavigation({ waitUntil: 'networkidle2' }).catch(() => undefined),
  ])
  await new Promise((r) => setTimeout(r, 1500))

  if (!page.url().includes('/admin')) {
    await page.goto(`${BASE}/admin`, { waitUntil: 'networkidle2' })
    await new Promise((r) => setTimeout(r, 1200))
  }
  console.log(`signed in — at ${page.url()}\n`)

  let totalFindings = 0

  for (const [name, path] of SCREENS) {
    await page.goto(`${BASE}${path}`, { waitUntil: 'networkidle2' })
    // Give the data queries a moment; an empty skeleton audits nothing useful.
    await new Promise((r) => setTimeout(r, 1800))

    const findings = await page.evaluate(CONTRAST_SWEEP)
    const scroll = await page.evaluate(`(() => {
      const main = document.querySelector('main')
      const shell = main ? main.parentElement.parentElement : null
      return {
        pageScrolls: document.documentElement.scrollHeight > window.innerHeight + 2,
        mainScrolls: main ? main.scrollHeight > main.clientHeight + 2 : false,
        shellHeight: shell ? Math.round(shell.getBoundingClientRect().height) : 0,
        viewport: window.innerHeight,
      }
    })()`)

    await page.screenshot({ path: join(shotDir, `${name}.png`) })

    const flag = scroll.pageScrolls ? '  PAGE SCROLLS (sidebar will drag)' : ''
    console.log(
      `${name.padEnd(13)} contrast findings: ${String(findings.length).padStart(2)}` +
        `  | main scrolls: ${scroll.mainScrolls ? 'yes' : 'no '}${flag}`,
    )
    findings.slice(0, 6).forEach((f) =>
      console.log(
        `    ${String(f.ratio).padStart(5)}:1 (needs ${f.required}) ${f.size}px  "${f.text}"  ${f.colour} on ${f.background}`,
      ),
    )
    totalFindings += findings.length
  }

  await writeFile(join(shotDir, 'README.txt'), 'Screenshots from scripts/audit-admin.mjs\n')
  await browser.close()

  console.log(`\ntotal contrast findings across ${SCREENS.length} screens: ${totalFindings}`)
  console.log(`screenshots: ${shotDir}`)
  if (totalFindings > 0) process.exitCode = 1
}

main().catch((error) => {
  console.error('audit failed:', error.message)
  process.exitCode = 1
})

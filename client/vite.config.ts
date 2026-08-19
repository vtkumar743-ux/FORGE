import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'

/** Dev API. Override with VITE_API_TARGET when the API runs on another port. */
const apiTarget = process.env.VITE_API_TARGET ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  // The same proxy for `vite preview`, so a production-build smoke test (and a Lighthouse
  // run against it) sees real API content rather than an empty shell.
  preview: {
    port: 4173,
    strictPort: true,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
      '/media/uploads': { target: apiTarget, changeOrigin: true },
      '/hubs': { target: apiTarget, changeOrigin: true, ws: true },
      // Generated from the CMS by the API, so a page the owner adds enters the index
      // without a deploy. Proxied here too, or a preview build serves the SPA shell for them.
      '/robots.txt': { target: apiTarget, changeOrigin: true },
      '/sitemap.xml': { target: apiTarget, changeOrigin: true },
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    // Proxying keeps the client same-origin with the API, so the httpOnly refresh
    // cookie works with SameSite=Lax instead of needing None+Secure cross-site.
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
      // Only owner-uploaded media comes from the API. Build-time brand imagery lives in
      // client/public/media and must keep being served locally, so the proxy is scoped to
      // /media/uploads rather than all of /media.
      '/media/uploads': { target: apiTarget, changeOrigin: true },
      '/hubs': { target: apiTarget, changeOrigin: true, ws: true },
      '/robots.txt': { target: apiTarget, changeOrigin: true },
      '/sitemap.xml': { target: apiTarget, changeOrigin: true },
    },
  },
  build: {
    target: 'es2022',
    sourcemap: true,
    rollupOptions: {
      output: {
        // Keep the heavy, rarely-changing libraries in their own long-cached chunks.
        // Vite 8 bundles with Rolldown, which expresses this as codeSplitting groups
        // rather than the manualChunks record Rollup used.
        codeSplitting: {
          groups: [
            { name: 'react', test: /node_modules[\\/](react|react-dom|react-router|react-router-dom|scheduler)[\\/]/ },
            { name: 'motion', test: /node_modules[\\/](motion|framer-motion)[\\/]/ },
            { name: 'data', test: /node_modules[\\/](@tanstack|axios|zod)[\\/]/ },
            // Recharts and d3 are admin-only and heavy; keeping them out of the dashboard's
            // own chunk means a second admin screen does not re-download them.
            { name: 'charts', test: /node_modules[\\/](recharts|d3-.*|victory-.*|internmap|decimal\.js-light|eventemitter3)[\\/]/ },
          ],
        },
      },
    },
  },
})

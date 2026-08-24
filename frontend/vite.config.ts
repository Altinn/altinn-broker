import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), basicSsl()],
  server: {
    // HTTPS so Secure session cookies and ID-Porten redirect URIs work locally.
    // Open https://localhost:5173 (not http://) — accept the self-signed cert once.
    https: true,
    proxy: {
      // Same-origin proxy so the session cookie is first-party in local dev.
      '/authentication': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'https://localhost:7241',
        changeOrigin: true,
        secure: false,
        xfwd: true,
        configure: (proxy) => {
          proxy.on('proxyReq', (proxyReq, req) => {
            const host = req.headers.host
            if (host) {
              proxyReq.setHeader('X-Forwarded-Host', host)
            }
          })
        },
      },
      '/broker': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'https://localhost:7241',
        changeOrigin: true,
        secure: false,
        xfwd: true,
        configure: (proxy) => {
          proxy.on('proxyReq', (proxyReq, req) => {
            const host = req.headers.host
            if (host) {
              proxyReq.setHeader('X-Forwarded-Host', host)
            }
          })
        },
      },
    },
  },
})

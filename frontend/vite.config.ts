import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), basicSsl()],
  server: {
    // HTTPS for local dev comes from @vitejs/plugin-basic-ssl (Vite 8 no longer
    // accepts server.https: true). Open https://localhost:5173 — accept the cert once.
    proxy: {
      // Same-origin proxy so the session cookie is first-party in local dev.
      // Covers API + auth under /broker/api/v1/...
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

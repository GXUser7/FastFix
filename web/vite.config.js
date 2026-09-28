import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// В режиме разработки запросы /api и /hubs проксируются на сервер ServiceDesk.Api
const api = process.env.API_URL || 'http://localhost:5080'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': api,
      '/hubs': { target: api, ws: true },
    },
  },
})

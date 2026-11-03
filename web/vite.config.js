import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    host: true,
    proxy: {
      '/api': 'http://localhost:8080',
      '/photos': 'http://localhost:8080'
    }
  },
  test: {
    environment: 'jsdom',
    globals: false
  }
})

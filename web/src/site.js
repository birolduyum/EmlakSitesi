import { api } from './api.js'

// Danışman bilgisi bir kez alınır ve modül düzeyinde saklanır; hata olursa sonraki istek yeniden dener.
let cached = null

export function getSiteInfo() {
  if (!cached) {
    cached = api.get('/api/site').catch((e) => {
      cached = null
      throw e
    })
  }
  return cached
}

export function resetSiteInfo() {
  cached = null
}

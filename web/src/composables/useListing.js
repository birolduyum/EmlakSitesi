import { ref, watch, onBeforeUnmount, toValue } from 'vue'
import { api } from '../api.js'

// Tek ilanın yüklenmesi: Detay ve Galeri aynı durum makinesini kullanır.
// state: 'loading' | 'ready' | 'notfound' | 'error'
export function useListing(idSource) {
  const listing = ref(null)
  const state = ref('loading')
  let seq = 0
  let alive = true

  async function load() {
    const mine = ++seq
    state.value = 'loading'
    try {
      const data = await api.get(`/api/listings/${toValue(idSource)}`)
      if (!alive || mine !== seq) return
      listing.value = data
      state.value = 'ready'
    } catch (e) {
      if (!alive || mine !== seq) return
      listing.value = null
      state.value = e?.status === 404 ? 'notfound' : 'error'
    }
  }

  watch(() => toValue(idSource), load, { immediate: true })
  onBeforeUnmount(() => {
    alive = false
  })

  return { listing, state, reload: load }
}

export function addressOf(l) {
  return [l?.neighborhood, l?.district, l?.city].filter(Boolean).join(', ')
}

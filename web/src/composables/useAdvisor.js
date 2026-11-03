import { ref, computed, onBeforeUnmount } from 'vue'
import { getSiteInfo } from '../site.js'

// Danışman bilgisi (Ara / Mesaj Gönder bağlantıları) — Lightbox, Detay ve Galeri paylaşır.
export function useAdvisor() {
  const info = ref(null)
  let alive = true

  getSiteInfo()
    .then((data) => {
      if (alive && data) info.value = data
    })
    .catch(() => {
      // Danışman kutusu bir kolaylıktır; hata olursa düğmeler görünmez.
    })
  onBeforeUnmount(() => {
    alive = false
  })

  const telHref = computed(() => {
    const phone = info.value?.advisorPhone
    const digits = typeof phone === 'string' ? phone.replace(/[^\d+]/g, '') : ''
    return digits ? `tel:${digits}` : null
  })
  // Yalnızca http(s) bağlantısı kabul edilir (javascript:, data: vb. düşer).
const whatsappUrl = computed(() => {
    const url = info.value?.whatsappUrl
    return typeof url === 'string' && /^https?:\/\//i.test(url.trim()) ? url.trim() : null
  })

  return { info, telHref, whatsappUrl }
}

import { ref, computed, onBeforeUnmount } from 'vue'
import { getSiteInfo } from '../site.js'

// WhatsApp'ta doğrudan sesli arama başlatan resmi bir bağlantı yoktur; wa.me yalnızca sohbeti açar.
// Yalnızca http(s) bağlantısı kabul edilir (javascript:, data: vb. düşer).
export function safeWhatsappUrl(url) {
  return typeof url === 'string' && /^https?:\/\//i.test(url.trim()) ? url.trim() : null
}

// Aynı numaraya, ilana özel hazır mesajlı bağlantı. Mevcut sorgu dizgesi korunur, önceki `text` değiştirilir.
export function whatsappMessageUrl(base, listing) {
  const safe = safeWhatsappUrl(base)
  if (!safe || !listing) return null
  let url
  try {
    url = new URL(safe)
  } catch {
    return null
  }
  const message = `Merhaba, ${listing.title} (İlan No: ${listing.id}) hakkında bilgi almak istiyorum.`
  url.searchParams.delete('text')
  const rest = url.searchParams.toString()
  url.search = `${rest ? `${rest}&` : ''}text=${encodeURIComponent(message)}`
  return url.toString()
}

// Danışman bilgisi (arama / mesaj bağlantıları) — Lightbox, Detay, Galeri ve liste satırı paylaşır.
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

  // Yedek: WhatsApp bağlantısı yoksa eski davranış (tel:).
  const telHref = computed(() => {
    const phone = info.value?.advisorPhone
    const digits = typeof phone === 'string' ? phone.replace(/[^\d+]/g, '') : ''
    return digits ? `tel:${digits}` : null
  })
  const whatsappUrl = computed(() => safeWhatsappUrl(info.value?.whatsappUrl))

  return { info, telHref, whatsappUrl }
}

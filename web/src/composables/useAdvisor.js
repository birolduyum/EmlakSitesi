import { ref, computed, watch, toValue, onBeforeUnmount } from 'vue'
import { getSiteInfo } from '../site.js'

// WhatsApp'ta doğrudan sesli arama başlatan resmi bir bağlantı yoktur; wa.me yalnızca sohbeti açar.
// Yalnızca http(s) bağlantısı kabul edilir (javascript:, data: vb. düşer).
export function safeWhatsappUrl(url) {
  return typeof url === 'string' && /^https?:\/\//i.test(url.trim()) ? url.trim() : null
}

// Aynı numaraya, ilana özel hazır mesajlı bağlantı. Mevcut sorgu dizgesi korunur, önceki `text` değiştirilir.
// Eksik başlık/numara mesajdan çıkarılır ("undefined" yazılmaz); kodlanamayan metinde (yalnız vekil karakter) düz bağlantı döner.
export function whatsappMessageUrl(base, listing) {
  const safe = safeWhatsappUrl(base)
  if (!safe || !listing) return null
  let url
  try {
    url = new URL(safe)
  } catch {
    return null
  }
  url.searchParams.delete('text')
  const plain = url.toString()
  const title = typeof listing.title === 'string' ? listing.title.trim() : ''
  const hasId = listing.id !== null && listing.id !== undefined && listing.id !== ''
  const subject = [title, hasId ? `(İlan No: ${listing.id})` : ''].filter(Boolean).join(' ')
  const message = subject ? `Merhaba, ${subject} hakkında bilgi almak istiyorum.` : 'Merhaba, bilgi almak istiyorum.'
  try {
    const rest = url.searchParams.toString()
    url.search = `${rest ? `${rest}&` : ''}text=${encodeURIComponent(message)}`
    return url.toString()
  } catch {
    return plain
  }
}

// "Ara" düğmesinin etiketi (WhatsApp varsa "WhatsApp'tan Ara", yoksa tel: yedeği "Ara") ve erişilebilir adı; tek yerde.
export function callLabels(hasWhatsapp, advisorName) {
  const label = hasWhatsapp ? "WhatsApp'tan Ara" : 'Ara'
  return { label, aria: `${label}: ${advisorName ?? ''}`.trim() }
}

// Danışman bilgisi (arama / mesaj bağlantıları) — Lightbox, Detay, Galeri ve liste satırı paylaşır.
// `advisorSource` (ref/getter/nesne) ilanın kendi danışmanıdır ({ name, phone, whatsappUrl }); varsa o kullanılır, yoksa /api/site
// (varsayılan danışman). `info` her iki durumda da { advisorName, advisorPhone, whatsappUrl } biçimindedir.
export function useAdvisor(advisorSource = null) {
  const site = ref(null)
  let alive = true
  let requested = false

  const own = computed(() => {
    const a = toValue(advisorSource)
    return a && (a.name || a.phone || a.whatsappUrl) ? a : null
  })

  // Site bilgisi yalnızca ilanın danışmanı yokken istenir (satır başına ek istek yok; zaten tek sefer önbelleklenir).
  watch(
    own,
    (a) => {
      if (a || requested) return
      requested = true
      getSiteInfo()
        .then((data) => {
          if (alive && data) site.value = data
        })
        .catch(() => {
          // Danışman kutusu bir kolaylıktır; hata olursa düğmeler görünmez.
        })
    },
    { immediate: true }
  )
  onBeforeUnmount(() => {
    alive = false
  })

  const info = computed(() => {
    const a = own.value
    if (a) return { advisorName: a.name ?? '', advisorPhone: a.phone ?? '', whatsappUrl: a.whatsappUrl ?? null }
    return site.value
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

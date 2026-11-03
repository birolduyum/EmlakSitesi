import { ref, onBeforeUnmount } from 'vue'

// API hatasını kullanıcıya gösterilecek Türkçe iletiye çevirir: önce duruma özel ileti, yoksa genel.
export function errorMessage(error, byStatus = {}, fallback = 'İşlem tamamlanamadı. Lütfen tekrar deneyin.') {
  const status = error?.status
  if (status === 0 || status === undefined) return byStatus[0] ?? 'Bağlantı kurulamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.'
  return byStatus[status] ?? fallback
}

// Tek bir asenkron eylem: bekleme bayrağı, çift gönderim kilidi ve hata iletisi.
// `run` bekleme sürerken tekrar çağrılırsa hiçbir şey yapmaz (null döner).
export function useAsyncAction(action, { byStatus = {}, fallback } = {}) {
  const pending = ref(false)
  const error = ref('')
  let alive = true
  onBeforeUnmount(() => {
    alive = false
  })

  async function run(...args) {
    if (pending.value) return null
    pending.value = true
    error.value = ''
    try {
      const data = await action(...args)
      return { ok: true, data }
    } catch (e) {
      if (alive) error.value = errorMessage(e, byStatus, fallback)
      return { ok: false, error: e }
    } finally {
      if (alive) pending.value = false
    }
  }

  return { pending, error, run }
}

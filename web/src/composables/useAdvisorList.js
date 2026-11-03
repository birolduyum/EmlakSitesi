import { ref, onBeforeUnmount } from 'vue'
import { api } from '../api.js'

// Danışmanları addan, Türkçe sıralamayla dizer (sunucu sırası yalnızca varsayılandır).
export function sortAdvisors(list) {
  return (Array.isArray(list) ? [...list] : []).sort((a, b) => String(a.name).localeCompare(String(b.name), 'tr') || a.id - b.id)
}

// İlan formu ve yönetim listesindeki "Danışman" seçicileri için danışman listesi (yalnızca Yönetici; Saha bu ucu çağırmaz).
// Yüklenemezse liste boş kalır ve `failed` true olur; seçici varsayılan seçenekle çalışmaya devam eder.
export function useAdvisorList() {
  const advisors = ref([])
  const failed = ref(false)
  let alive = true
  onBeforeUnmount(() => {
    alive = false
  })

  async function load() {
    try {
      const data = await api.get('/api/admin/advisors')
      if (!alive) return
      advisors.value = sortAdvisors(data)
      failed.value = false
    } catch {
      if (!alive) return
      failed.value = true
    }
  }

  return { advisors, failed, load }
}

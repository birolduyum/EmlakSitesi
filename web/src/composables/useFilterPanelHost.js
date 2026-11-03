import { ref, computed } from 'vue'

const NARROW = '(max-width: 899.98px)'
const NON_FILTER_KEYS = new Set(['page', 'pageSize', 'sort', 'text'])

// Süzgeç panelini barındıran liste sayfaları (ziyaretçi ve yönetici) için ortak mantık:
// dar ekranda panelin açılıp kapanması, "uygula" sonrası kapanma + sonuçlara kayma, uygulanan süzgeç sayısı.
export function useFilterPanelHost(params) {
  const filtersOpen = ref(false)
  const resultsEl = ref(null)

  function onApplied() {
    filtersOpen.value = false
    if (window.matchMedia?.(NARROW).matches) {
      resultsEl.value?.scrollIntoView?.({ behavior: 'smooth', block: 'start' })
    }
  }

  // Yalnızca UYGULANAN süzgeçleri sayar (adres çubuğundaki durum); sayfa, sıralama ve arama metni süzgeç sayılmaz.
  const activeFilterCount = computed(() => Object.keys(params.value).filter((k) => !NON_FILTER_KEYS.has(k)).length)

  return { filtersOpen, resultsEl, onApplied, activeFilterCount }
}

import { describe, it, expect, vi, afterEach } from 'vitest'
import { ref } from 'vue'
import { useFilterPanelHost } from '../composables/useFilterPanelHost.js'

afterEach(() => {
  delete window.matchMedia
})

describe('useFilterPanelHost', () => {
  it('counts only applied filters (type counts; page, pageSize, sort, text do not)', () => {
    const params = ref({ page: 2, pageSize: 20, sort: 'PriceAsc', text: 'x' })
    const { activeFilterCount } = useFilterPanelHost(params)
    expect(activeFilterCount.value).toBe(0)
    params.value = { ...params.value, type: 'Kiralik', balcony: true, rooms: ['2+1'] }
    expect(activeFilterCount.value).toBe(3)
  })
  it('onApplied closes the panel and scrolls to the results on narrow screens only', () => {
    const { filtersOpen, resultsEl, onApplied } = useFilterPanelHost(ref({}))
    const scrollIntoView = vi.fn()
    resultsEl.value = { scrollIntoView }
    filtersOpen.value = true
    window.matchMedia = vi.fn(() => ({ matches: false }))
    onApplied()
    expect(filtersOpen.value).toBe(false)
    expect(scrollIntoView).not.toHaveBeenCalled()
    filtersOpen.value = true
    window.matchMedia = vi.fn(() => ({ matches: true }))
    onApplied()
    expect(filtersOpen.value).toBe(false)
    expect(scrollIntoView).toHaveBeenCalledTimes(1)
  })
  it('works without matchMedia or a results element', () => {
    const { filtersOpen, onApplied } = useFilterPanelHost(ref({}))
    filtersOpen.value = true
    expect(() => onApplied()).not.toThrow()
    expect(filtersOpen.value).toBe(false)
  })
})

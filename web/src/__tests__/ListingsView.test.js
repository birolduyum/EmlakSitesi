import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import ListingsView from '../views/ListingsView.vue'
import { api } from '../api.js'
import { resetSiteInfo } from '../site.js'

vi.mock('../api.js', () => ({ api: { get: vi.fn() } }))

const row = (id, over = {}) => ({
  id, title: `İlan ${id}`, badge: null, type: 'Satilik', price: 1000000, currency: 'TRY', city: 'Ankara',
  district: 'Çankaya', rooms: '2+1', grossM2: 90, floor: 1, createdAt: new Date(2026, 7, 23, 12).toISOString(), photoUrls: [],
  ...over
})
let filtersBody
const defaultFilters = () => ({ cities: ['Ankara'], districtsByCity: { Ankara: ['Çankaya'] }, roomCounts: { '2+1': 3 } })

let listingsImpl
beforeEach(() => {
  resetSiteInfo()
  filtersBody = defaultFilters()
  api.get.mockReset()
  listingsImpl = async () => ({ items: [row(1), row(2)], total: 1234, page: 1, pageSize: 20 })
  api.get.mockImplementation((path, params) => {
    if (path === '/api/listings/filters') return Promise.resolve(filtersBody)
    if (path === '/api/site') return Promise.resolve({ advisorName: 'Ayşe', advisorPhone: '0532 000 00 00', whatsappUrl: null })
    return listingsImpl(params)
  })
})

async function make(url = '/') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/', component: ListingsView }, { path: '/ilan/:id', component: { template: '<i/>' } }]
  })
  router.push(url)
  await router.isReady()
  const w = mount(ListingsView, { global: { plugins: [router] } })
  await flushPromises()
  return { w, router }
}
const listingCalls = () => api.get.mock.calls.filter((c) => c[0] === '/api/listings')

describe('ListingsView', () => {
  it('initial load sends type=Satilik, page=1, pageSize=20', async () => {
    await make()
    expect(listingCalls()).toHaveLength(1)
    expect(listingCalls()[0][1]).toEqual({ type: 'Satilik', page: 1, pageSize: 20 })
    expect(api.get).toHaveBeenCalledWith('/api/listings/filters', { type: 'Satilik' })
  })
  it('shows the title, localized count and rows linking to the detail page', async () => {
    const { w } = await make()
    expect(w.find('h1').text()).toBe('Satılık Daire')
    expect(w.find('[aria-live="polite"]').text()).toBe('1.234 ilan bulundu')
    expect(w.findAll('article')).toHaveLength(2)
    expect(w.find('article h2 a').attributes('href')).toBe('/ilan/1')
    expect(w.find('nav[aria-label="Sayfa yolu"]').text()).toContain('Satılık Daire')
  })
  it('sorts_cities_and_districts_in_Turkish_order', async () => {
    filtersBody.cities = ['Ankara', 'Zonguldak', 'Çanakkale', 'İzmir', 'Şanlıurfa', 'Isparta']
    filtersBody.districtsByCity = { Ankara: ['Yenimahalle', 'Çankaya', 'Altındağ', 'Şereflikoçhisar', 'Keçiören'] }
    const { w } = await make('/?city=Ankara')
    const options = (name) => w.findAll(`select[name="${name}"] option`).map((o) => o.text()).slice(1)
    expect(options('city')).toEqual(['Ankara', 'Çanakkale', 'Isparta', 'İzmir', 'Şanlıurfa', 'Zonguldak'])
    expect(options('district')).toEqual(['Altındağ', 'Çankaya', 'Keçiören', 'Şereflikoçhisar', 'Yenimahalle'])
    // oda sayıları değişmeden gelir
    expect(w.findAll('[data-testid="room"]').map((r) => r.text())).toEqual(['2+1 (3)'])
  })
  it('reads the query from the URL (kiralik, page 3, filters)', async () => {
    const { w } = await make('/?type=Kiralik&page=3&rooms=2%2B1&balcony=true&minPriceTry=abc')
    expect(listingCalls()[0][1]).toEqual({ type: 'Kiralik', page: 3, pageSize: 20, rooms: ['2+1'], balcony: true })
    expect(w.find('h1').text()).toBe('Kiralık Daire')
  })
  it('changing a filter resets page to 1 and updates the URL', async () => {
    const { w, router } = await make('/?page=3')
    await w.find('input[name="balcony"]').setValue(true)
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ balcony: 'true' })
    expect(listingCalls().at(-1)[1]).toEqual({ type: 'Satilik', page: 1, pageSize: 20, balcony: true })
  })
  it('changing the sort writes the URL', async () => {
    const { w, router } = await make()
    await w.find('select[name="sort"]').setValue('PriceDesc')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ sort: 'PriceDesc' })
  })
  it('stale response is ignored', async () => {
    const pending = []
    listingsImpl = (params) => new Promise((resolve) => pending.push({ params, resolve }))
    const { w, router } = await make()
    await router.replace({ query: { balcony: 'true' } })
    await flushPromises()
    expect(pending).toHaveLength(2)
    pending[1].resolve({ items: [row(22)], total: 1, page: 1, pageSize: 20 })
    await flushPromises()
    pending[0].resolve({ items: [row(11), row(12), row(13)], total: 3, page: 1, pageSize: 20 })
    await flushPromises()
    expect(w.findAll('article')).toHaveLength(1)
    expect(w.find('article h2').text()).toBe('İlan 22')
    expect(w.find('[aria-live="polite"]').text()).toBe('1 ilan bulundu')
  })
  it('empty state offers to clear the filters', async () => {
    listingsImpl = async () => ({ items: [], total: 0, page: 1, pageSize: 20 })
    const { w, router } = await make('/?balcony=true&city=Ankara')
    expect(w.text()).toContain('Aramanıza uygun ilan bulunamadı')
    await w.find('[data-testid="clear-filters"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({})
  })
  it('error state shows a retry that reloads', async () => {
    listingsImpl = async () => { throw new Error('boom') }
    const { w } = await make()
    expect(w.text()).toContain('İlanlar yüklenemedi')
    listingsImpl = async () => ({ items: [row(5)], total: 1, page: 1, pageSize: 20 })
    await w.find('[data-testid="retry"]').trigger('click')
    await flushPromises()
    expect(w.findAll('article')).toHaveLength(1)
    expect(w.text()).not.toContain('İlanlar yüklenemedi')
  })
  it('pagination moves to the next page and keeps filters', async () => {
    listingsImpl = async () => ({ items: [row(1)], total: 45, page: 1, pageSize: 20 })
    window.scrollTo = vi.fn()
    const { w, router } = await make('/?balcony=true')
    await w.find('[data-testid="next-page"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ balcony: 'true', page: '2' })
  })
  it('typing in search is debounced and resets the page', async () => {
    vi.useFakeTimers()
    try {
      const { w, router } = await make('/?page=2')
      await w.find('input[type="search"]').setValue('deniz')
      expect(router.currentRoute.value.query.text).toBeUndefined()
      vi.advanceTimersByTime(300)
      await flushPromises()
      expect(router.currentRoute.value.query).toEqual({ text: 'deniz' })
    } finally {
      vi.useRealTimers()
    }
  })
})

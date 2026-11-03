import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
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

const realScrollTo = window.scrollTo
afterEach(() => {
  window.scrollTo = realScrollTo
})

let listingsImpl
beforeEach(() => {
  resetSiteInfo()
  filtersBody = defaultFilters()
  api.get.mockReset()
  listingsImpl = async () => ({ items: [row(1), row(2, { type: 'Kiralik' })], total: 1234, page: 1, pageSize: 20 })
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
const filterCalls = () => api.get.mock.calls.filter((c) => c[0] === '/api/listings/filters')
const submit = async (w) => {
  await w.find('#filter-panel form').trigger('submit')
  await flushPromises()
}

describe('ListingsView', () => {
  it('default_shows_both_types_without_type_param', async () => {
    const { w } = await make()
    expect(listingCalls()).toHaveLength(1)
    expect(listingCalls()[0][1]).toEqual({ page: 1, pageSize: 20 })
    expect(filterCalls()[0][1]).toEqual({})
    expect(w.find('input[name="type"][value=""]').element.checked).toBe(true)
  })
  it('shows the title, localized count and rows linking to the detail page', async () => {
    const { w } = await make()
    expect(w.find('h1').text()).toBe('Tüm İlanlar')
    expect(w.find('p.total').text()).toBe('1.234 ilan bulundu')
    expect(w.findAll('article')).toHaveLength(2)
    expect(w.find('article h2 a').attributes('href')).toBe('/ilan/1')
    expect(w.find('nav[aria-label="Sayfa yolu"]').text()).toContain('Tüm İlanlar')
  })
  it('title_follows_applied_type (not the draft)', async () => {
    const { w } = await make('/?type=Satilik')
    expect(w.find('h1').text()).toBe('Satılık Daire')
    expect(w.find('nav[aria-label="Sayfa yolu"]').text()).toContain('Satılık Daire')
    await w.find('input[name="type"][value="Kiralik"]').setValue()
    expect(w.find('h1').text()).toBe('Satılık Daire') // taslak: henüz uygulanmadı
    await submit(w)
    expect(w.find('h1').text()).toBe('Kiralık Daire')
    expect(w.find('nav[aria-label="Sayfa yolu"]').text()).toContain('Kiralık Daire')
  })
  it('row_shows_type_label_in_tumu_mode_only', async () => {
    const { w } = await make()
    expect(w.findAll('[data-testid="type-tag"]').map((t) => t.text())).toEqual(['Satılık', 'Kiralık'])
    const sale = await make('/?type=Satilik')
    expect(sale.w.findAll('[data-testid="type-tag"]')).toHaveLength(0)
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
  it('no_request_until_apply, then exactly one request with all params and page 1', async () => {
    const { w, router } = await make('/?page=3')
    const before = api.get.mock.calls.length
    await w.find('input[name="balcony"]').setValue(true)
    await w.find('select[name="city"]').setValue('Ankara')
    await w.find('input[name="minPriceTry"]').setValue('1000000')
    await w.findAll('[data-testid="room"] input')[0].setValue(true)
    await w.find('input[name="type"][value="Kiralik"]').setValue()
    await flushPromises()
    expect(api.get.mock.calls.length).toBe(before)
    expect(router.currentRoute.value.query).toEqual({ page: '3' })
    await submit(w)
    expect(listingCalls()).toHaveLength(2)
    expect(listingCalls().at(-1)[1]).toEqual({
      type: 'Kiralik', page: 1, pageSize: 20, city: 'Ankara', minPriceTry: 1000000, rooms: ['2+1'], balcony: true
    })
    expect(router.currentRoute.value.query).toEqual({
      type: 'Kiralik', city: 'Ankara', minPriceTry: '1000000', rooms: ['2+1'], balcony: 'true'
    })
  })
  it('type_change_keeps_other_filters; option lists reload for the applied type only', async () => {
    const { w } = await make('/?balcony=true&city=Ankara')
    await w.find('input[name="type"][value="Kiralik"]').setValue()
    expect(filterCalls()).toHaveLength(1) // taslak: seçenek listeleri henüz yeniden istenmez
    await submit(w)
    expect(listingCalls().at(-1)[1]).toEqual({ type: 'Kiralik', page: 1, pageSize: 20, city: 'Ankara', balcony: true })
    expect(filterCalls().at(-1)[1]).toEqual({ type: 'Kiralik' })
  })
  it('apply_without_changes_sends_no_request', async () => {
    const { w } = await make('/?balcony=true')
    await submit(w)
    expect(listingCalls()).toHaveLength(1)
  })
  it('enter in a field applies (form submit)', async () => {
    const { w, router } = await make()
    await w.find('input[name="neighborhood"]').setValue('Moda')
    await submit(w)
    expect(router.currentRoute.value.query).toEqual({ neighborhood: 'Moda' })
  })
  it('clear applies immediately and resets everything including type', async () => {
    const { w, router } = await make('/?type=Kiralik&balcony=true&city=Ankara')
    await w.find('[data-testid="clear"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({})
    expect(listingCalls().at(-1)[1]).toEqual({ page: 1, pageSize: 20 })
  })
  it('draft_resyncs_when_url_changes (back/forward)', async () => {
    const { w, router } = await make('/?balcony=true')
    await w.find('input[name="elevator"]').setValue(true)
    await router.replace({ query: { parking: 'true' } })
    await flushPromises()
    expect(w.find('input[name="elevator"]').element.checked).toBe(false)
    expect(w.find('input[name="balcony"]').element.checked).toBe(false)
    expect(w.find('input[name="parking"]').element.checked).toBe(true)
  })
  it('changing the sort writes the URL and keeps the applied filters', async () => {
    const { w, router } = await make('/?type=Kiralik&balcony=true')
    await w.find('select[name="sort"]').setValue('PriceDesc')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ type: 'Kiralik', balcony: 'true', sort: 'PriceDesc' })
  })
  it('tumu_hides_price_sorts_and_falls_back to Newest', async () => {
    const { w } = await make('/?sort=PriceAsc')
    expect(listingCalls()[0][1]).toEqual({ page: 1, pageSize: 20 })
    expect(w.findAll('select[name="sort"] option').map((o) => o.text())).toEqual(['En yeni'])
    const sale = await make('/?type=Satilik')
    expect(sale.w.findAll('select[name="sort"] option').map((o) => o.text())).toEqual(['En yeni', 'Fiyat: artan', 'Fiyat: azalan'])
  })
  it('pagination_and_sort_use_applied_state: unapplied drafts are not applied silently', async () => {
    listingsImpl = async () => ({ items: [row(1)], total: 45, page: 1, pageSize: 20 })
    window.scrollTo = vi.fn()
    const { w, router } = await make('/?type=Satilik&balcony=true')
    await w.find('select[name="city"]').setValue('Ankara')
    await w.find('[data-testid="next-page"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ type: 'Satilik', balcony: 'true', page: '2' })
    expect(listingCalls().at(-1)[1]).not.toHaveProperty('city')
    await w.find('select[name="city"]').setValue('Ankara')
    await w.find('select[name="sort"]').setValue('PriceAsc')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ type: 'Satilik', balcony: 'true', sort: 'PriceAsc' })
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
    expect(w.find('article h2').text()).toContain('İlan 22')
    expect(w.find('p.total').text()).toBe('1 ilan bulundu')
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
  it('mobile_apply_closes_panel and scrolls to the results', async () => {
    const scrollIntoView = vi.fn()
    Element.prototype.scrollIntoView = scrollIntoView
    window.matchMedia = vi.fn(() => ({ matches: true }))
    try {
      const { w } = await make()
      const toggle = w.find('button[aria-controls="filter-panel"]')
      expect(toggle.attributes('aria-expanded')).toBe('false')
      await toggle.trigger('click')
      expect(toggle.attributes('aria-expanded')).toBe('true')
      expect(w.find('#filter-panel').classes()).toContain('open')
      await w.find('input[name="balcony"]').setValue(true)
      expect(toggle.text()).toBe('Filtreler') // sayaç yalnızca UYGULANAN süzgeçleri sayar
      await submit(w)
      expect(toggle.attributes('aria-expanded')).toBe('false')
      expect(w.find('#filter-panel').classes()).not.toContain('open')
      expect(scrollIntoView).toHaveBeenCalledTimes(1)
      expect(toggle.text()).toContain('(1)')
    } finally {
      delete window.matchMedia
      delete Element.prototype.scrollIntoView
    }
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

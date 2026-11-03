import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import ListingRow from '../components/ListingRow.vue'
import { resetSiteInfo } from '../site.js'
import { api } from '../api.js'

vi.mock('../api.js', () => ({ api: { get: vi.fn() } }))

const listing = {
  id: 7, title: 'Deniz manzaralı 3+1', badge: 'Fırsat', type: 'Satilik', price: 22000000, currency: 'TRY',
  city: 'İzmir', district: 'Karşıyaka', rooms: '3+1', grossM2: 145, floor: 2,
  createdAt: new Date(2026, 7, 23, 12).toISOString(), photoUrls: ['/photos/a.jpg', '/photos/b.jpg']
}

async function make(l = listing) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/', component: { template: '<i/>' } }, { path: '/ilan/:id', component: { template: '<i/>' } }]
  })
  router.push('/')
  await router.isReady()
  const w = mount(ListingRow, { props: { listing: l }, global: { plugins: [router] } })
  await flushPromises()
  return { w, router }
}

beforeEach(() => {
  resetSiteInfo()
  api.get.mockReset()
  api.get.mockResolvedValue({ advisorName: 'Ayşe Yılmaz', advisorPhone: '+90 532 111 22 33', whatsappUrl: null })
})

describe('ListingRow', () => {
  it('shows badge, location, spec line and price', async () => {
    const { w } = await make()
    expect(w.find('[data-testid="badge"]').text()).toBe('Fırsat')
    expect(w.text()).toContain('İzmir, Karşıyaka')
    expect(w.text()).toContain('3+1 · 145 m² · 2. Kat · 23.08.2026')
    expect(w.text()).toContain('22.000.000 ₺')
  })
  it('shows no badge when absent', async () => {
    const { w } = await make({ ...listing, badge: null })
    expect(w.find('[data-testid="badge"]').exists()).toBe(false)
  })
  it('shows $ for a USD listing and / ay for rentals', async () => {
    const usd = await make({ ...listing, currency: 'USD', price: 450000 })
    expect(usd.w.text()).toContain('450.000 $')
    const rent = await make({ ...listing, type: 'Kiralik', price: 15000 })
    expect(rent.w.text()).toContain('15.000 ₺ / ay')
  })
  it('links to the detail page from title and photo', async () => {
    const { w } = await make()
    expect(w.findAll('a[href="/ilan/7"]').length).toBeGreaterThanOrEqual(2)
    expect(w.find('h2 a').attributes('href')).toBe('/ilan/7')
  })
  it('Ara is a tel: link and its click does not bubble to the row', async () => {
    const { w, router } = await make()
    const ara = w.find('a[href^="tel:"]')
    expect(ara.exists()).toBe(true)
    expect(ara.attributes('href')).toBe('tel:+905321112233')
    expect(ara.text()).toBe('Ara')
    ara.element.addEventListener('click', (e) => e.preventDefault()) // jsdom tel: gezinmesini desteklemez
    const spy = vi.fn()
    w.element.addEventListener('click', spy)
    await ara.trigger('click')
    expect(spy).not.toHaveBeenCalled()
    expect(router.currentRoute.value.path).toBe('/')
  })
  it('renders nothing breaking when the site call fails', async () => {
    api.get.mockRejectedValue(new Error('down'))
    const { w } = await make()
    expect(w.find('a[href^="tel:"]').exists()).toBe(false)
    expect(w.text()).toContain('Deniz manzaralı 3+1')
  })
})

describe('ListingRow type label', () => {
  it('shows Satılık/Kiralık next to the title only when showType is set', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:p(.*)*', component: { template: '<i/>' } }] })
    const on = mount(ListingRow, { props: { listing: { ...listing, type: 'Kiralik' }, showType: true }, global: { plugins: [router] } })
    expect(on.find('[data-testid="type-tag"]').text()).toBe('Kiralık')
    const off = mount(ListingRow, { props: { listing }, global: { plugins: [router] } })
    expect(off.find('[data-testid="type-tag"]').exists()).toBe(false)
  })
})

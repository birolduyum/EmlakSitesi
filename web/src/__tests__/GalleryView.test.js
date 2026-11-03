import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import GalleryView from '../views/GalleryView.vue'
import { api } from '../api.js'
import { resetSiteInfo } from '../site.js'

vi.mock('../api.js', () => ({ api: { get: vi.fn() } }))

const photos = (n) => Array.from({ length: n }, (_, i) => `/photos/${i + 1}.jpg`)
const detail = (over = {}) => ({
  id: 7, title: 'Deniz manzaralı 3+1', badge: null, type: 'Satilik', price: 22000000, currency: 'TRY',
  city: 'İzmir', district: 'Karşıyaka', rooms: '3+1', grossM2: 145, floor: 2, createdAt: new Date(2026, 7, 23, 12).toISOString(),
  photoUrls: photos(11), neighborhood: 'Bostanlı', tags: [], description: '', showingCount: 0,
  ...over
})
const site = { advisorName: 'Ayşe', advisorPhone: '0532 111 22 33', whatsappUrl: null }

let listingImpl
let wrapper
beforeEach(() => {
  resetSiteInfo()
  api.get.mockReset()
  listingImpl = async () => detail()
  api.get.mockImplementation((p) => (p === '/api/site' ? Promise.resolve(site) : listingImpl(p)))
})
afterEach(() => {
  wrapper?.unmount()
  wrapper = null
  document.body.innerHTML = ''
})

async function make() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<i/>' } },
      { path: '/ilan/:id', component: { template: '<i/>' } },
      { path: '/ilan/:id/fotograflar', component: GalleryView }
    ]
  })
  router.push('/ilan/7/fotograflar')
  await router.isReady()
  wrapper = mount(GalleryView, { global: { plugins: [router] }, attachTo: document.body })
  await flushPromises()
  return wrapper
}

describe('GalleryView', () => {
  it('renders every photo and the back link', async () => {
    const w = await make()
    expect(w.findAll('[data-testid="gallery-photo"] img')).toHaveLength(11)
    const back = w.find('a[href="/ilan/7"]')
    expect(back.text()).toContain('İlana Dön')
  })
  it('every 5th photo spans the full width', async () => {
    const w = await make()
    const items = w.findAll('[data-testid="gallery-photo"]')
    const wide = items.map((it, i) => (it.classes().includes('wide') ? i : -1)).filter((i) => i >= 0)
    expect(wide).toEqual([4, 9])
  })
  it('summary shows price, title, rooms and m2, address and Ara', async () => {
    const w = await make()
    const s = w.find('[data-testid="summary"]')
    expect(s.text()).toContain('22.000.000 ₺')
    expect(s.text()).toContain('Deniz manzaralı 3+1')
    expect(s.text()).toContain('3+1 · 145 m²')
    expect(s.text()).toContain('Bostanlı, Karşıyaka, İzmir')
    expect(s.find('a[href="tel:05321112233"]').exists()).toBe(true)
  })
  it('summary button is WhatsApp when a valid whatsappUrl exists', async () => {
    api.get.mockImplementation((p) => (p === '/api/site' ? Promise.resolve({ ...site, whatsappUrl: 'https://wa.me/905320000001' }) : listingImpl(p)))
    const w = await make()
    const a = w.find('[data-testid="summary"] a[href="https://wa.me/905320000001"]')
    expect(a.text()).toBe("WhatsApp'tan Ara")
    expect(a.attributes('target')).toBe('_blank')
  })
  it('clicking a photo opens the Lightbox at its index', async () => {
    const w = await make()
    await w.findAll('[data-testid="gallery-photo"] button')[6].trigger('click')
    await flushPromises()
    expect(document.body.querySelector('[data-testid="lightbox-image"]').getAttribute('src')).toBe('/photos/7.jpg')
  })
  it('404 shows İlan bulunamadı', async () => {
    listingImpl = async () => {
      throw Object.assign(new Error('HTTP 404'), { status: 404 })
    }
    const w = await make()
    expect(w.text()).toContain('İlan bulunamadı')
  })
  it('0 photos shows the placeholder and a message', async () => {
    listingImpl = async () => detail({ photoUrls: [] })
    const w = await make()
    expect(w.text()).toContain('Bu ilanda fotoğraf yok')
    expect(w.find('[data-testid="empty"] img').attributes('src')).toContain('placeholder')
  })
  it('summary_and_lightbox_use_the_listing_advisor', async () => {
    listingImpl = async () => detail({ advisor: { name: 'Mehmet Demir', phone: '0 532 444 55 66', whatsappUrl: 'https://wa.me/905324445566' } })
    const w = await make()
    const s = w.find('[data-testid="summary"]')
    expect(s.find('a[href="https://wa.me/905324445566"]').text()).toBe("WhatsApp'tan Ara")
    expect(s.findAll('a').some((a) => a.attributes('aria-label') === "WhatsApp'tan Ara: Mehmet Demir")).toBe(true)
    expect(s.find('a[href="tel:05321112233"]').exists()).toBe(false)
    await w.findAll('[data-testid="gallery-photo"] button')[0].trigger('click')
    await flushPromises()
    const bar = document.body.querySelector('[role="dialog"] .bar')
    expect(bar.querySelector('a[href="https://wa.me/905324445566"]')).not.toBeNull()
    expect(api.get).not.toHaveBeenCalledWith('/api/site')
  })
})

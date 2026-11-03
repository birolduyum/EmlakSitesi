import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import DetailView from '../views/DetailView.vue'
import { api } from '../api.js'
import { resetSiteInfo } from '../site.js'

vi.mock('../api.js', () => ({ api: { get: vi.fn() } }))

const detail = (over = {}) => ({
  id: 7, title: 'Deniz manzaralı 3+1', badge: null, type: 'Satilik', price: 22000000, currency: 'TRY',
  city: 'İzmir', district: 'Karşıyaka', rooms: '3+1', grossM2: 145, floor: 2,
  createdAt: new Date(2026, 7, 23, 12).toISOString(),
  photoUrls: ['/photos/1.jpg', '/photos/2.jpg', '/photos/3.jpg', '/photos/4.jpg', '/photos/5.jpg', '/photos/6.jpg'],
  deposit: 30000, aidat: 1500, neighborhood: 'Bostanlı', bathrooms: 2, netM2: 120, totalFloors: 8, buildingAge: 5,
  heating: 'Kombi', furnishing: 'Esyasiz', usage: 'Bos', deed: 'KatMulkiyeti', kitchen: 'Acik',
  balcony: true, elevator: true, parking: false, inSite: true, loanEligible: true, swap: false,
  tags: [
    { id: 1, name: 'Klima', group: 'Ic' },
    { id: 2, name: 'Çelik Kapı', group: 'Ic' },
    { id: 3, name: 'Havuz', group: 'Dis' }
  ],
  description: 'Güzel bir daire.\nİkinci satır.', showingCount: 0,
  ...over
})
const site = { advisorName: 'Ayşe Yılmaz', advisorPhone: '+90 532 111 22 33', whatsappUrl: 'https://wa.me/905321112233' }

let listingImpl
let wrapper
beforeEach(() => {
  resetSiteInfo()
  api.get.mockReset()
  listingImpl = async () => detail()
  api.get.mockImplementation((path) => (path === '/api/site' ? Promise.resolve(site) : listingImpl(path)))
})
afterEach(() => {
  wrapper?.unmount()
  wrapper = null
  document.body.innerHTML = ''
})

async function make(id = 7) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: { template: '<i/>' } },
      { path: '/ilan/:id', component: DetailView },
      { path: '/ilan/:id/fotograflar', component: { template: '<i/>' } }
    ]
  })
  router.push(`/ilan/${id}`)
  await router.isReady()
  wrapper = mount(DetailView, { global: { plugins: [router] }, attachTo: document.body })
  await flushPromises()
  return { w: wrapper, router }
}
const rowLabels = (w) => w.findAll('dt').map((d) => d.text())
const rowValue = (w, label) => {
  const dt = w.findAll('dt').find((d) => d.text() === label)
  return dt?.element.nextElementSibling?.textContent.trim()
}

describe('DetailView', () => {
  it('renders title, price and address', async () => {
    const { w } = await make()
    expect(w.find('h1').text()).toBe('Deniz manzaralı 3+1')
    expect(w.text()).toContain('22.000.000 ₺')
    expect(w.text()).toContain('Bostanlı, Karşıyaka, İzmir')
    expect(api.get).toHaveBeenCalledWith('/api/listings/7')
  })
  it('breadcrumb links back to the list with filters', async () => {
    const { w } = await make()
    const nav = w.find('nav[aria-label="Sayfa yolu"]')
    const hrefs = nav.findAll('a').map((a) => a.attributes('href'))
    expect(hrefs).toEqual([
      '/?type=Satilik',
      '/?type=Satilik',
      '/?type=Satilik&city=%C4%B0zmir',
      '/?type=Satilik&city=%C4%B0zmir&district=Kar%C5%9F%C4%B1yaka'
    ])
    expect(nav.text()).toContain('Satılık Daire')
  })
  it('summary boxes and kunye rows', async () => {
    const { w } = await make()
    const summary = w.find('[data-testid="spec-grid"]').text()
    for (const t of ['Oda Sayısı', '3+1', 'Banyo Sayısı', 'Brüt / Net m²', '145 m² / 120 m²', 'Bulunduğu Kat', '2. Kat', 'Kat Sayısı', 'Bina Yaşı']) {
      expect(summary).toContain(t)
    }
    expect(rowValue(w, 'İlan Numarası')).toBe('7')
    expect(rowValue(w, 'İlan Güncelleme Tarihi')).toBe('23.08.2026')
    expect(rowValue(w, 'Kategori')).toBe('Satılık Daire')
    expect(rowValue(w, 'Isıtma Tipi')).toBe('Kombi')
    expect(rowValue(w, 'Otopark')).toBe('Yok')
    expect(rowValue(w, 'Krediye Uygunluk')).toBe('Uygun')
    expect(rowValue(w, 'Takas')).toBe('Hayır')
    expect(rowValue(w, 'Site İçerisinde')).toBe('Evet')
    expect(rowValue(w, 'Asansör')).toBe('Var')
    expect(rowValue(w, 'Balkon')).toBe('Var')
    expect(rowValue(w, 'Mutfak')).toBe('Açık')
    expect(rowValue(w, 'Eşya Durumu')).toBe('Eşyasız')
    expect(rowValue(w, 'Tapu Durumu')).toBe('Kat Mülkiyeti')
    expect(rowValue(w, 'Kullanım Durumu')).toBe('Boş')
  })
  it('shows Depozito and Aidat only for Kiralik', async () => {
    listingImpl = async () => detail({ type: 'Kiralik', price: 15000, currency: 'USD' })
    const { w } = await make()
    expect(rowValue(w, 'Depozito')).toBe('30.000 $')
    expect(rowValue(w, 'Aidat')).toBe('1.500 $ / ay')
    expect(rowValue(w, 'Kategori')).toBe('Kiralık Daire')
    expect(w.text()).toContain('15.000 $ / ay')
  })
  it('hides Depozito and Aidat for Satilik', async () => {
    const { w } = await make()
    expect(rowLabels(w)).not.toContain('Depozito')
    expect(rowLabels(w)).not.toContain('Aidat')
  })
  it('hides the showing count when 0 and shows it when 3', async () => {
    let { w } = await make()
    expect(w.text()).not.toContain('kez gösterildi')
    w.unmount()
    listingImpl = async () => detail({ showingCount: 3 })
    ;({ w } = await make())
    expect(w.text()).toContain('Bu ilan 3 kez gösterildi')
  })
  it('404 shows İlan bulunamadı with a link home', async () => {
    listingImpl = async () => {
      throw Object.assign(new Error('HTTP 404'), { status: 404 })
    }
    const { w } = await make(99)
    expect(w.text()).toContain('İlan bulunamadı')
    expect(w.find('a[href="/"]').exists()).toBe(true)
  })
  it('other errors show İlan yüklenemedi and retry works', async () => {
    let fail = true
    listingImpl = async () => {
      if (fail) throw Object.assign(new Error('HTTP 500'), { status: 500 })
      return detail()
    }
    const { w } = await make()
    expect(w.text()).toContain('İlan yüklenemedi')
    fail = false
    await w.find('[data-testid="retry"]').trigger('click')
    await flushPromises()
    expect(w.find('h1').text()).toBe('Deniz manzaralı 3+1')
  })
  it('tabs show only groups with tags and switch with arrow keys', async () => {
    const { w } = await make()
    const tabs = w.findAll('[role="tab"]')
    expect(tabs.map((t) => t.text())).toEqual(['İç Özellikler', 'Dış Özellikler'])
    expect(tabs[0].attributes('aria-selected')).toBe('true')
    expect(w.find('[role="tabpanel"]').text()).toContain('Klima')
    expect(w.find('[role="tabpanel"]').text()).toContain('Çelik Kapı')
    await tabs[0].trigger('keydown', { key: 'ArrowRight' })
    const after = w.findAll('[role="tab"]')
    expect(after[1].attributes('aria-selected')).toBe('true')
    expect(w.find('[role="tabpanel"]').text()).toContain('Havuz')
    await after[1].trigger('keydown', { key: 'ArrowRight' })
    expect(w.findAll('[role="tab"]')[0].attributes('aria-selected')).toBe('true')
    await w.findAll('[role="tab"]')[0].trigger('keydown', { key: 'ArrowLeft' })
    expect(w.findAll('[role="tab"]')[1].attributes('aria-selected')).toBe('true')
  })
  it('hides the tab block with no tags', async () => {
    listingImpl = async () => detail({ tags: [] })
    const { w } = await make()
    expect(w.find('[role="tablist"]').exists()).toBe(false)
  })
  it('fallback_to_tel_when_whatsapp_missing', async () => {
    api.get.mockImplementation((p) => (p === '/api/site' ? Promise.resolve({ ...site, whatsappUrl: null }) : listingImpl(p)))
    const { w } = await make()
    expect(w.text()).not.toContain('Mesaj Gönder')
    const ara = w.findAll('a').find((x) => x.text() === 'Ara')
    expect(ara.attributes('href')).toBe('tel:+905321112233')
    expect(ara.attributes('target')).toBeUndefined()
    expect(w.text()).toContain('+90 532 111 22 33')
  })
  it('detail_card_buttons_use_whatsapp_with_prefilled_text', async () => {
    api.get.mockImplementation((p) => (p === '/api/site' ? Promise.resolve({ ...site, whatsappUrl: 'https://wa.me/905320000001' }) : listingImpl(p)))
    const { w } = await make()
    const call = w.findAll('a').find((x) => x.text() === "WhatsApp'tan Ara")
    const msg = w.findAll('a').find((x) => x.text() === "WhatsApp'tan Mesaj Gönder")
    expect(call.attributes('href')).toBe('https://wa.me/905320000001')
    expect(call.attributes('aria-label')).toBe("WhatsApp'tan Ara: Ayşe Yılmaz")
    const url = new URL(msg.attributes('href'))
    expect(url.origin + url.pathname).toBe('https://wa.me/905320000001')
    expect(url.searchParams.get('text')).toBe('Merhaba, Deniz manzaralı 3+1 (İlan No: 7) hakkında bilgi almak istiyorum.')
    expect(msg.attributes('href')).not.toBe(call.attributes('href'))
    for (const a of [call, msg]) {
      expect(a.attributes('target')).toBe('_blank')
      expect(a.attributes('rel')).toBe('noopener noreferrer')
    }
    expect(w.text()).toContain('+90 532 111 22 33')
  })
  it.each(['javascript:alert(1)', 'data:text/html,<script>alert(1)</script>', 'ftp://x'])('javascript_url_still_rejected (%s)', async (bad) => {
    api.get.mockImplementation((p) => (p === '/api/site' ? Promise.resolve({ ...site, whatsappUrl: bad }) : listingImpl(p)))
    const { w } = await make()
    expect(w.text()).not.toContain('Mesaj Gönder')
    expect(w.text()).not.toContain("WhatsApp'tan")
    expect(w.find(`a[href="${bad}"]`).exists()).toBe(false)
    expect(w.findAll('a').find((x) => x.text() === 'Ara').attributes('href')).toBe('tel:+905321112233')
  })
  it('uses updatedAt for İlan Güncelleme Tarihi when present', async () => {
    listingImpl = async () => detail({ updatedAt: new Date(2026, 9, 5, 12).toISOString() })
    const { w } = await make()
    expect(rowValue(w, 'İlan Güncelleme Tarihi')).toBe('05.10.2026')
  })
  it('tabs expose aria-controls, aria-selected and roving tabindex; Konum tab works', async () => {
    listingImpl = async () => detail({ tags: [{ id: 1, name: 'Klima', group: 'Ic' }, { id: 9, name: 'Metroya yakın', group: 'Konum' }] })
    const { w } = await make()
    const tabs = w.findAll('[role="tab"]')
    expect(tabs.map((t) => t.text())).toEqual(['İç Özellikler', 'Konum Özellikleri'])
    expect(tabs.map((t) => t.attributes('tabindex'))).toEqual(['0', '-1'])
    const panelId = w.find('[role="tabpanel"]').attributes('id')
    expect(tabs[0].attributes('aria-controls')).toBe(panelId)
    await tabs[1].trigger('click')
    const after = w.findAll('[role="tab"]')
    expect(after.map((t) => t.attributes('aria-selected'))).toEqual(['false', 'true'])
    expect(after.map((t) => t.attributes('tabindex'))).toEqual(['-1', '0'])
    expect(w.find('[role="tabpanel"]').text()).toContain('Metroya yakın')
    expect(w.find('[role="tabpanel"]').attributes('aria-labelledby')).toBe(after[1].attributes('id'))
  })
  it('clicking a photo opens the Lightbox at that index', async () => {
    const { w } = await make()
    const photoButtons = w.findAll('[data-testid="photo-button"]')
    expect(photoButtons).toHaveLength(5)
    await photoButtons[2].trigger('click')
    await flushPromises()
    const img = document.body.querySelector('[data-testid="lightbox-image"]')
    expect(img.getAttribute('src')).toBe('/photos/3.jpg')
    expect(document.body.querySelector('[data-testid="counter"]').textContent.trim()).toBe('3 / 6')
  })
  it('shows the see-all link to the gallery', async () => {
    const { w } = await make()
    const a = w.find('a[href="/ilan/7/fotograflar"]')
    expect(a.text()).toBe('6 fotoğrafın tümünü gör')
  })
  it('renders the description as text only', async () => {
    listingImpl = async () => detail({ description: 'Merhaba <script>alert(1)</script> <b>kalın</b>' })
    const { w } = await make()
    const card = w.find('[data-testid="description"]')
    expect(card.text()).toContain('<script>alert(1)</script>')
    expect(card.find('script').exists()).toBe(false)
    expect(card.find('b').exists()).toBe(false)
    expect(card.find('h2').text()).toBe('Deniz manzaralı 3+1 Açıklaması')
  })
  it('0 photos shows the placeholder and no gallery link', async () => {
    listingImpl = async () => detail({ photoUrls: [] })
    const { w } = await make()
    const imgs = w.findAll('[data-testid="photo-block"] img')
    expect(imgs).toHaveLength(1)
    expect(imgs[0].attributes('src')).toContain('placeholder')
    expect(w.find('a[href="/ilan/7/fotograflar"]').exists()).toBe(false)
    expect(w.findAll('[data-testid="photo-button"]')).toHaveLength(0)
  })
  it('1 photo shows one large photo, 3 photos large plus two', async () => {
    listingImpl = async () => detail({ photoUrls: ['/photos/1.jpg'] })
    let { w } = await make()
    expect(w.findAll('[data-testid="photo-button"]')).toHaveLength(1)
    w.unmount()
    listingImpl = async () => detail({ photoUrls: ['/photos/1.jpg', '/photos/2.jpg', '/photos/3.jpg'] })
    ;({ w } = await make())
    expect(w.findAll('[data-testid="photo-button"]')).toHaveLength(3)
  })
  it('sets document.title while shown and restores it on leave', async () => {
    document.title = 'Emlak'
    const { w } = await make()
    expect(document.title).toBe('Deniz manzaralı 3+1')
    w.unmount()
    wrapper = null
    expect(document.title).toBe('Emlak')
  })
  it('detail_card_uses_the_listing_advisor_phone_and_whatsapp_links', async () => {
    listingImpl = async () => detail({ advisor: { name: 'Mehmet Demir', phone: '0 532 444 55 66', whatsappUrl: 'https://wa.me/905324445566' } })
    const { w } = await make()
    const card = w.find('aside.advisor-card')
    expect(card.text()).toContain('Mehmet Demir')
    expect(card.text()).toContain('0 532 444 55 66')
    expect(card.text()).not.toContain('Ayşe Yılmaz')
    const call = card.findAll('a').find((x) => x.text() === "WhatsApp'tan Ara")
    const msg = card.findAll('a').find((x) => x.text() === "WhatsApp'tan Mesaj Gönder")
    expect(call.attributes('href')).toBe('https://wa.me/905324445566')
    expect(call.attributes('aria-label')).toBe("WhatsApp'tan Ara: Mehmet Demir")
    const url = new URL(msg.attributes('href'))
    expect(url.origin + url.pathname).toBe('https://wa.me/905324445566')
    expect(url.searchParams.get('text')).toContain('Deniz manzaralı 3+1 (İlan No: 7)')
    expect(api.get).not.toHaveBeenCalledWith('/api/site')
  })
  it('detail_lightbox_uses_the_listing_advisor', async () => {
    listingImpl = async () => detail({ advisor: { name: 'Mehmet Demir', phone: '0 532 444 55 66', whatsappUrl: 'https://wa.me/905324445566' } })
    const { w } = await make()
    await w.find('button[aria-label^="Fotoğrafı büyüt"]').trigger('click')
    await flushPromises()
    const bar = document.body.querySelector('[role="dialog"] .bar')
    expect(bar.querySelector('a[href="https://wa.me/905324445566"]').textContent.trim()).toBe("WhatsApp'tan Ara")
  })
  it('listing_without_advisor_falls_back_to_site_info', async () => {
    listingImpl = async () => detail({ advisor: null })
    const { w } = await make()
    expect(w.find('aside.advisor-card').text()).toContain('Ayşe Yılmaz')
    expect(api.get).toHaveBeenCalledWith('/api/site')
  })
})

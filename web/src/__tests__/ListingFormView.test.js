import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import ListingFormView from '../views/admin/ListingFormView.vue'
import { api } from '../api.js'
import { setSession, clearSession } from '../session.js'

vi.mock('../api.js', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn(), upload: vi.fn() } }))

const tags = [
  { id: 1, name: 'Klima', group: 'Ic' },
  { id: 2, name: 'Bahçe', group: 'Dis' },
  { id: 3, name: 'Metroya yakın', group: 'Konum' }
]
const detail = (over = {}) => ({
  id: 5, title: 'Deniz manzaralı daire', badge: null, type: 'Satilik', price: 2500000, currency: 'TRY', city: 'İzmir',
  district: 'Karşıyaka', rooms: '3+1', grossM2: 120, floor: 3, createdAt: '2026-08-01T10:00:00Z', photoUrls: ['/photos/a.jpg'],
  deposit: null, aidat: null, neighborhood: 'Bostanlı', bathrooms: 2, netM2: 110, totalFloors: 8, buildingAge: 4, heating: 'Kombi',
  furnishing: 'Esyasiz', usage: 'Bos', deed: 'KatMulkiyeti', kitchen: 'Acik', balcony: true, elevator: true, parking: false,
  inSite: false, loanEligible: true, swap: false, tags: [tags[0]], description: 'Güzel', showingCount: 0,
  owner: { name: 'Mehmet Kaya', phone: '0532 111 22 33', note: 'Pazarlık var' },
  createdBy: 'ayse', updatedAt: null, updatedBy: null, isDeleted: false, deletedAt: null, deletedBy: null, restoredAt: null, restoredBy: null,
  photos: [{ id: 11, url: '/photos/a.jpg', sortOrder: 0 }],
  ...over
})

let getDetail
const mounted = []
afterEach(() => {
  // window dinleyicileri (beforeunload) testler arasında sızmasın
  while (mounted.length) mounted.pop().unmount()
})
beforeEach(() => {
  clearSession()
  localStorage.clear()
  for (const f of Object.values(api)) f.mockReset()
  getDetail = async () => detail()
  api.get.mockImplementation((path) => {
    if (path === '/api/tags') return Promise.resolve(tags)
    if (/^\/api\/admin\/listings\/\d+$/.test(path)) return getDetail(path)
    return Promise.reject({ status: 404 })
  })
})

async function make(url, role = 'Admin') {
  setSession('t', { username: 'ayse', role })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin', component: { template: '<p id="list">liste</p>' } },
      { path: '/admin/ilan/yeni', component: ListingFormView },
      { path: '/admin/ilan/:id', component: ListingFormView }
    ]
  })
  router.push(url)
  await router.isReady()
  const w = mount({ template: '<router-view />' }, { global: { plugins: [router] }, attachTo: document.body })
  mounted.push(w)
  await flushPromises()
  return { w, router }
}

async function fillBasics(w) {
  await w.find('input[name="title"]').setValue('Yeni ilan')
  await w.find('input[name="price"]').setValue('1500000')
  await w.find('input[name="city"]').setValue('Ankara')
  await w.find('input[name="district"]').setValue('Çankaya')
  await w.find('input[name="rooms"]').setValue('2+1')
  await w.find('input[name="ownerName"]').setValue('Ali')
  await w.find('input[name="ownerPhone"]').setValue('0555')
}

describe('ListingFormView (new)', () => {
  it('shows all sections, kiralik-only fields hidden for Satilik', async () => {
    const { w } = await make('/admin/ilan/yeni')
    for (const h of ['Temel', 'Konum', 'Özellikler', 'Etiketler', 'Açıklama', 'Ev sahibi']) {
      expect(w.findAll('h2').some((x) => x.text().startsWith(h))).toBe(true)
    }
    expect(w.text()).toContain('Yalnızca yönetici görür')
    expect(w.find('input[name="deposit"]').exists()).toBe(false)
    await w.find('input[type="radio"][name="type"][value="Kiralik"]').setValue()
    expect(w.find('input[name="deposit"]').exists()).toBe(true)
    expect(w.find('input[name="aidat"]').exists()).toBe(true)
    expect(w.find('[data-testid="photo-manager"]').exists()).toBe(false)
  })
  it('client-side checks stop an incomplete form before any request', async () => {
    const { w } = await make('/admin/ilan/yeni')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(api.post).not.toHaveBeenCalled()
    expect(w.find('[role="alert"]').text()).toContain('Başlık')
    expect(w.find('#f-title-err').exists()).toBe(true)
    expect(w.find('input[name="title"]').attributes('aria-invalid')).toBe('true')
  })
  it('create_then_navigates_to_edit', async () => {
    api.post.mockResolvedValue(detail({ id: 77 }))
    const { w, router } = await make('/admin/ilan/yeni')
    await fillBasics(w)
    await w.find('input[type="radio"][name="type"][value="Kiralik"]').setValue()
    await w.find('input[name="deposit"]').setValue('30000')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(api.post).toHaveBeenCalledTimes(1)
    const [path, body] = api.post.mock.calls[0]
    expect(path).toBe('/api/admin/listings')
    expect(body).toMatchObject({ title: 'Yeni ilan', price: 1500000, type: 'Kiralik', deposit: 30000, aidat: null, ownerName: 'Ali' })
    expect(router.currentRoute.value.path).toBe('/admin/ilan/77')
  })
  it('double submit sends one request', async () => {
    let resolve
    api.post.mockReturnValue(new Promise((r) => { resolve = r }))
    const { w } = await make('/admin/ilan/yeni')
    await fillBasics(w)
    await w.find('form').trigger('submit')
    await w.find('form').trigger('submit')
    expect(api.post).toHaveBeenCalledTimes(1)
    expect(w.find('button[type="submit"]').attributes('disabled')).toBeDefined()
    resolve(detail({ id: 78 }))
    await flushPromises()
  })
  it('server_errors_are_shown_in_alert', async () => {
    api.post.mockRejectedValue({ status: 400, body: { errors: ['Fiyat sıfırdan büyük olmalıdır.', 'Bilinmeyen etiket seçildi.'] } })
    const { w, router } = await make('/admin/ilan/yeni')
    await fillBasics(w)
    await w.find('form').trigger('submit')
    await flushPromises()
    const alert = w.find('[role="alert"]')
    expect(alert.text()).toContain('Fiyat sıfırdan büyük olmalıdır.')
    expect(alert.text()).toContain('Bilinmeyen etiket seçildi.')
    // alan hatası alanın yanında da görünür
    expect(w.find('#f-price-err').text()).toContain('Fiyat sıfırdan büyük')
    expect(router.currentRoute.value.path).toBe('/admin/ilan/yeni')
    expect(w.find('button[type="submit"]').attributes('disabled')).toBeUndefined()
  })
  it('network failure shows a generic message', async () => {
    api.post.mockRejectedValue({ status: 0 })
    const { w } = await make('/admin/ilan/yeni')
    await fillBasics(w)
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(w.find('[role="alert"]').text()).toContain('Sunucuya ulaşılamadı')
  })
  it('new_tag_409_shows_message', async () => {
    api.post.mockRejectedValue({ status: 409, body: { error: 'x' } })
    const { w } = await make('/admin/ilan/yeni')
    await w.find('input[name="newTagName"]').setValue('Klima')
    await w.find('select[name="newTagGroup"]').setValue('Ic')
    await w.find('[data-testid="add-tag"]').trigger('click')
    await flushPromises()
    expect(api.post).toHaveBeenCalledWith('/api/admin/tags', { name: 'Klima', group: 'Ic' })
    expect(w.find('[data-testid="tag-error"]').text()).toBe('Bu etiket zaten var')
  })
  it('new tag is added and selected', async () => {
    api.post.mockResolvedValue({ id: 9, name: 'Teras', group: 'Dis' })
    const { w } = await make('/admin/ilan/yeni')
    await w.find('input[name="newTagName"]').setValue('Teras')
    await w.find('select[name="newTagGroup"]').setValue('Dis')
    await w.find('[data-testid="add-tag"]').trigger('click')
    await flushPromises()
    const box = w.find('input[type="checkbox"][value="9"]')
    expect(box.exists()).toBe(true)
    expect(box.element.checked).toBe(true)
  })
  it('tags are grouped İç / Dış / Konum', async () => {
    const { w } = await make('/admin/ilan/yeni')
    const text = w.find('[data-testid="tags"]').text()
    expect(text.indexOf('İç Özellikler')).toBeLessThan(text.indexOf('Dış Özellikler'))
    expect(text.indexOf('Dış Özellikler')).toBeLessThan(text.indexOf('Konum Özellikleri'))
    expect(w.findAll('input[type="checkbox"][name="tagIds"]')).toHaveLength(3)
  })
})

describe('ListingFormView (edit)', () => {
  it('loads the listing, PUTs on save and stays on the page', async () => {
    api.put.mockResolvedValue(detail({ title: 'Güncel', updatedBy: 'veli', updatedAt: '2026-09-01T10:00:00Z' }))
    const { w, router } = await make('/admin/ilan/5')
    expect(w.find('input[name="title"]').element.value).toBe('Deniz manzaralı daire')
    expect(w.find('input[name="ownerName"]').element.value).toBe('Mehmet Kaya')
    await w.find('input[name="title"]').setValue('Güncel')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(api.put).toHaveBeenCalledTimes(1)
    const [path, body] = api.put.mock.calls[0]
    expect(path).toBe('/api/admin/listings/5')
    expect(body.title).toBe('Güncel')
    expect(body.tagIds).toEqual([1])
    expect(body).not.toHaveProperty('photos')
    expect(body).not.toHaveProperty('id')
    expect(router.currentRoute.value.path).toBe('/admin/ilan/5')
    expect(w.find('[data-testid="status"]').text()).toContain('Kaydedildi')
  })
  it('audit_box_shows_created_by', async () => {
    getDetail = async () => detail({ updatedBy: 'veli', updatedAt: '2026-09-01T10:00:00Z' })
    const { w } = await make('/admin/ilan/5')
    const box = w.find('[data-testid="audit"]')
    expect(box.text()).toContain('Oluşturan ayse, 01.08.2026')
    expect(box.text()).toContain('Son güncelleyen veli, 01.09.2026')
    expect(box.text()).not.toContain('Silen')
  })
  it('deleted_listing_is_readonly_with_restore', async () => {
    getDetail = async () => detail({
      isDeleted: true, deletedBy: 'veli', deletedAt: '2026-09-02T10:00:00Z', restoredBy: 'ayse', restoredAt: '2026-08-20T10:00:00Z'
    })
    api.post.mockResolvedValue(null)
    const { w } = await make('/admin/ilan/5')
    expect(w.find('[data-testid="audit"]').text()).toContain('Silen veli, 02.09.2026')
    expect(w.find('[data-testid="audit"]').text()).toContain('Geri alan ayse, 20.08.2026')
    expect(w.find('button[type="submit"]').exists()).toBe(false)
    expect(w.find('fieldset[disabled]').exists()).toBe(true)
    expect(w.find('[data-testid="photo-manager"]').exists()).toBe(false)
    expect(w.find('[data-testid="delete"]').exists()).toBe(false)
    getDetail = async () => detail()
    await w.find('[data-testid="restore"]').trigger('click')
    await flushPromises()
    expect(api.post).toHaveBeenCalledWith('/api/admin/listings/5/restore')
    expect(w.find('fieldset[disabled]').exists()).toBe(false)
    expect(w.find('button[type="submit"]').exists()).toBe(true)
  })
  it('delete asks for confirmation, then soft-deletes and shows read-only state', async () => {
    api.delete.mockResolvedValue(null)
    const { w } = await make('/admin/ilan/5')
    await w.find('[data-testid="delete"]').trigger('click')
    expect(w.find('[role="dialog"]').text()).toContain('Deniz manzaralı daire')
    expect(api.delete).not.toHaveBeenCalled()
    getDetail = async () => detail({ isDeleted: true, deletedBy: 'ayse', deletedAt: '2026-09-02T10:00:00Z' })
    await w.find('[data-testid="confirm"]').trigger('click')
    await flushPromises()
    expect(api.delete).toHaveBeenCalledWith('/api/admin/listings/5')
    expect(w.find('fieldset[disabled]').exists()).toBe(true)
  })
  it('shows the photo manager with the listing photos', async () => {
    const { w } = await make('/admin/ilan/5')
    expect(w.find('[data-testid="photo-manager"]').exists()).toBe(true)
    expect(w.text()).toContain('Kapak')
  })
  it('404 shows a not-found message', async () => {
    getDetail = async () => {
      throw { status: 404 }
    }
    const { w } = await make('/admin/ilan/999')
    expect(w.find('[role="alert"]').text()).toContain('İlan bulunamadı')
    expect(w.find('form').exists()).toBe(false)
  })
  it('save of a listing deleted meanwhile (404) is explained', async () => {
    api.put.mockRejectedValue({ status: 404 })
    const { w } = await make('/admin/ilan/5')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(w.find('[role="alert"]').text()).toContain('silinmiş')
  })
  it('unsaved changes ask before leaving; cancel stays, confirm leaves', async () => {
    const { w, router } = await make('/admin/ilan/5')
    await w.find('input[name="title"]').setValue('Değişti')
    const nav = router.push('/admin')
    await flushPromises()
    expect(w.find('[role="dialog"]').exists()).toBe(true)
    await w.find('[data-testid="cancel"]').trigger('click')
    await nav
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/admin/ilan/5')
    const nav2 = router.push('/admin')
    await flushPromises()
    await w.find('[data-testid="confirm"]').trigger('click')
    await nav2
    expect(router.currentRoute.value.path).toBe('/admin')
  })
  it('a clean form leaves without asking', async () => {
    const { w, router } = await make('/admin/ilan/5')
    await router.push('/admin')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/admin')
    expect(w.find('[role="dialog"]').exists()).toBe(false)
  })
  it('registers a beforeunload warning only while dirty', async () => {
    const { w } = await make('/admin/ilan/5')
    const ev = () => {
      const e = new Event('beforeunload', { cancelable: true })
      window.dispatchEvent(e)
      return e.defaultPrevented
    }
    expect(ev()).toBe(false)
    await w.find('input[name="title"]').setValue('Değişti')
    expect(ev()).toBe(true)
  })
})

describe('ListingFormView (roles)', () => {
  it('a Field user is sent back to /admin and nothing is loaded', async () => {
    const { router } = await make('/admin/ilan/5', 'Field')
    expect(router.currentRoute.value.path).toBe('/admin')
    expect(api.get).not.toHaveBeenCalledWith('/api/admin/listings/5')
  })
})

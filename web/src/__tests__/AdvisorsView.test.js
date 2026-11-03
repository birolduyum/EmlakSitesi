import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import AdvisorsView from '../views/admin/AdvisorsView.vue'
import { api, ApiError } from '../api.js'
import { setSession, clearSession } from '../session.js'

vi.mock('../api.js', async (orig) => {
  const real = await orig()
  return { ...real, api: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }
})

const created = new Date(2026, 10, 2, 14, 30).toISOString()
const adv = (id, over = {}) => ({
  id, name: `Danışman ${id}`, phone: '+90 532 111 22 33', whatsApp: null, isActive: true, listingCount: 0,
  createdAt: created, createdBy: 'admin1', updatedAt: null, updatedBy: null, ...over
})

let list
beforeEach(() => {
  clearSession()
  setSession('t', { username: 'admin1', role: 'Admin' })
  for (const f of Object.values(api)) f.mockReset()
  list = [adv(2, { name: 'Zeynep Yalçın', listingCount: 3 }), adv(1, { name: 'Ayşe Kaya', whatsApp: 'https://wa.me/905321112233' })]
  api.get.mockImplementation(async () => list)
})

async function make() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/admin', component: { template: '<i/>' } }, { path: '/admin/danismanlar', component: AdvisorsView }]
  })
  router.push('/admin/danismanlar')
  await router.isReady()
  const w = mount({ template: '<router-view />' }, { global: { plugins: [router] }, attachTo: document.body })
  await flushPromises()
  return { w, router }
}
const rows = (w) => w.findAll('[data-testid="advisor"]')
const fill = async (w, { name = '', phone = '', wa = '' } = {}) => {
  await w.find('input[name="name"]').setValue(name)
  await w.find('input[name="phone"]').setValue(phone)
  await w.find('input[name="whatsApp"]').setValue(wa)
}
const submit = async (w) => {
  await w.find('form').trigger('submit')
  await flushPromises()
}

describe('AdvisorsView', () => {
  it('lists advisors in Turkish name order with phone, whatsapp text, status, count and audit line', async () => {
    const { w } = await make()
    expect(api.get).toHaveBeenCalledWith('/api/admin/advisors')
    const r = rows(w)
    expect(r).toHaveLength(2)
    expect(r[0].text()).toContain('Ayşe Kaya')
    expect(r[0].text()).toContain('WhatsApp: https://wa.me/905321112233')
    expect(r[1].text()).toContain('Zeynep Yalçın')
    expect(r[1].text()).toContain('WhatsApp: telefondan türetilir')
    expect(r[1].find('[data-testid="count"]').text()).toBe('3 ilan')
    expect(r[1].find('[data-testid="state"]').text()).toBe('Aktif')
    expect(r[1].text()).toContain('Ekleyen admin1, 02.11.2026 14:30')
  })

  it('mutes inactive advisors and shows the Pasif badge and the Etkinleştir button', async () => {
    list = [adv(1, { name: 'Eski', isActive: false })]
    const { w } = await make()
    const r = rows(w)[0]
    expect(r.classes()).toContain('inactive')
    expect(r.find('[data-testid="state"]').text()).toBe('Pasif')
    expect(r.find('[data-testid="activate"]').exists()).toBe(true)
    expect(r.find('[data-testid="deactivate"]').exists()).toBe(false)
  })

  it('shows loading, empty and error states (with retry)', async () => {
    list = []
    const empty = await make()
    expect(empty.w.find('[data-testid="empty"]').text()).toContain('Henüz danışman eklenmedi')
    empty.w.unmount()
    list = [adv(2, { name: 'Zeynep Yalçın', listingCount: 3 }), adv(1, { name: 'Ayşe Kaya' })]

    api.get.mockRejectedValueOnce(new ApiError(500, null))
    const { w } = await make()
    expect(w.find('[role="alert"]').text()).toContain('Danışmanlar yüklenemedi')
    await w.find('[data-testid="retry"]').trigger('click')
    await flushPromises()
    expect(rows(w)).toHaveLength(2)
  })

  it('create validates before calling the API (no numbers in the messages)', async () => {
    const { w } = await make()
    await fill(w, { name: ' ', phone: 'abc', wa: 'xyz' })
    await submit(w)
    expect(api.post).not.toHaveBeenCalled()
    const summary = w.find('[data-testid="summary"]')
    expect(summary.attributes('role')).toBe('alert')
    expect(summary.text()).toContain('Ad boş olamaz.')
    expect(summary.text()).toContain('Telefon geçerli bir numara olmalı')
    expect(summary.text()).toContain('WhatsApp, geçerli bir numara ya da http(s) bağlantısı olmalı.')
    expect(summary.text()).not.toMatch(/\d/)
    expect(w.find('input[name="name"]').attributes('aria-invalid')).toBe('true')
  })

  it('create sends the trimmed payload (empty whatsapp becomes null), reloads and announces', async () => {
    api.post.mockResolvedValue(adv(3))
    const { w } = await make()
    await fill(w, { name: '  Yeni Kişi ', phone: ' 0 532 444 55 66 ', wa: '  ' })
    await submit(w)
    expect(api.post).toHaveBeenCalledWith('/api/admin/advisors', { name: 'Yeni Kişi', phone: '0 532 444 55 66', whatsApp: null })
    expect(w.find('[data-testid="status"]').text()).toBe('"Yeni Kişi" eklendi.')
    expect(w.find('[data-testid="status"]').attributes('aria-live')).toBe('polite')
    expect(api.get).toHaveBeenCalledTimes(2)
    expect(w.find('input[name="name"]').element.value).toBe('')
  })

  it('accepts an explicit whatsapp url or number', async () => {
    api.post.mockResolvedValue(adv(3))
    const { w } = await make()
    await fill(w, { name: 'A', phone: '+90 532 111 22 33', wa: 'https://wa.me/905321112233' })
    await submit(w)
    await fill(w, { name: 'B', phone: '+90 532 111 22 33', wa: '905321112233' })
    await submit(w)
    expect(api.post).toHaveBeenCalledTimes(2)
    expect(api.post.mock.calls[0][1].whatsApp).toBe('https://wa.me/905321112233')
  })

  it('edit fills the form, focuses the name, sends PUT and can be cancelled', async () => {
    api.put.mockResolvedValue(adv(1))
    const { w } = await make()
    await rows(w)[0].find('[data-testid="edit"]').trigger('click')
    await flushPromises()
    expect(w.find('h2#advisor-form-title').text()).toBe('Danışmanı düzenle')
    expect(w.find('input[name="name"]').element.value).toBe('Ayşe Kaya')
    expect(w.find('input[name="whatsApp"]').element.value).toBe('https://wa.me/905321112233')
    expect(document.activeElement).toBe(w.find('input[name="name"]').element)

    await w.find('input[name="name"]').setValue('Ayşe Kaya Yeni')
    await submit(w)
    expect(api.put).toHaveBeenCalledWith('/api/admin/advisors/1', {
      name: 'Ayşe Kaya Yeni', phone: '+90 532 111 22 33', whatsApp: 'https://wa.me/905321112233'
    })
    expect(w.find('[data-testid="status"]').text()).toBe('"Ayşe Kaya Yeni" güncellendi.')
    expect(w.find('h2#advisor-form-title').text()).toBe('Yeni danışman')

    await rows(w)[1].find('[data-testid="edit"]').trigger('click')
    await w.find('[data-testid="cancel-edit"]').trigger('click')
    expect(w.find('h2#advisor-form-title').text()).toBe('Yeni danışman')
    expect(w.find('input[name="name"]').element.value).toBe('')
  })

  it('deactivate asks for confirmation with the listing count, then posts', async () => {
    api.post.mockResolvedValue(null)
    const { w } = await make()
    await rows(w)[1].find('[data-testid="deactivate"]').trigger('click')
    const dialog = w.find('[role="dialog"]')
    expect(dialog.text()).toContain('Bu danışmanın 3 ilanı var; ilanlarda görünmeye devam eder, yeni ilana atanamaz.')
    expect(api.post).not.toHaveBeenCalled()

    await dialog.find('[data-testid="confirm"]').trigger('click')
    await flushPromises()
    expect(api.post).toHaveBeenCalledWith('/api/admin/advisors/2/deactivate')
    expect(w.find('[data-testid="status"]').text()).toBe('"Zeynep Yalçın" pasifleştirildi.')
    expect(w.find('[role="dialog"]').exists()).toBe(false)
  })

  it('cancelling the confirmation does nothing', async () => {
    const { w } = await make()
    await rows(w)[1].find('[data-testid="deactivate"]').trigger('click')
    await w.find('[role="dialog"] [data-testid="cancel"]').trigger('click')
    expect(api.post).not.toHaveBeenCalled()
    expect(w.find('[role="dialog"]').exists()).toBe(false)
  })

  it('activate posts directly without a dialog', async () => {
    list = [adv(1, { name: 'Eski', isActive: false })]
    api.post.mockResolvedValue(null)
    const { w } = await make()
    await rows(w)[0].find('[data-testid="activate"]').trigger('click')
    await flushPromises()
    expect(api.post).toHaveBeenCalledWith('/api/admin/advisors/1/activate')
    expect(w.find('[data-testid="status"]').text()).toBe('"Eski" etkinleştirildi.')
  })

  it('links each advisor to their listings in the admin list', async () => {
    const { w } = await make()
    const link = rows(w)[1].find('a')
    expect(link.text()).toBe('İlanlarını gör')
    expect(link.attributes('href')).toBe('/admin?advisorId=2')
  })

  it('shows server 400 errors in the alert summary and keeps the form', async () => {
    api.post.mockRejectedValue(new ApiError(400, { errors: ['Telefon geçersiz.', 'Ad çok uzun.'] }))
    const { w } = await make()
    await fill(w, { name: 'Ad', phone: '+90 532 111 22 33' })
    await submit(w)
    const summary = w.find('[data-testid="summary"]')
    expect(summary.text()).toContain('Telefon geçersiz.')
    expect(summary.text()).toContain('Ad çok uzun.')
    expect(w.find('input[name="name"]').element.value).toBe('Ad')
  })

  it('shows a generic message for network failures', async () => {
    api.post.mockRejectedValue(new ApiError(0, null))
    const { w } = await make()
    await fill(w, { name: 'Ad', phone: '+90 532 111 22 33' })
    await submit(w)
    expect(w.find('[data-testid="summary"]').text()).toContain('Bağlantı kurulamadı')
  })

  it('double submit is guarded: the second submit while pending is ignored', async () => {
    let release
    api.post.mockImplementation(() => new Promise((r) => (release = r)))
    const { w } = await make()
    await fill(w, { name: 'Ad', phone: '+90 532 111 22 33' })
    // Aynı tıkta iki gönderim: bekleme bayrağı eşzamanlı kurulduğu için yalnızca biri API'ye gider.
    w.find('form').trigger('submit')
    w.find('form').trigger('submit')
    await flushPromises()
    expect(api.post).toHaveBeenCalledTimes(1)
    expect(w.find('[data-testid="save"]').attributes('disabled')).toBeDefined()
    release(adv(3))
    await flushPromises()
    expect(w.find('[data-testid="save"]').attributes('disabled')).toBeUndefined()
  })

  it('ignores a stale list response that arrives after a newer one', async () => {
    let first
    let n = 0
    api.get.mockImplementation(() => {
      n++
      if (n === 1) return new Promise((r) => (first = r))
      return Promise.resolve([adv(9, { name: 'Yeni liste' })])
    })
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: AdvisorsView }, { path: '/admin', component: { template: '<i/>' } }] })
    router.push('/')
    await router.isReady()
    const w = mount({ template: '<router-view />' }, { global: { plugins: [router] }, attachTo: document.body })
    api.post.mockResolvedValue(null)
    await w.findComponent(AdvisorsView).vm.$nextTick()
    await w.find('input[name="name"]').setValue('X')
    await w.find('input[name="phone"]').setValue('+90 532 111 22 33')
    await w.find('form').trigger('submit') // başarılı kayıt → ikinci (yeni) yükleme
    await flushPromises()
    first([adv(8, { name: 'Eski liste' })]) // ilk yükleme geç döner
    await flushPromises()
    expect(w.text()).toContain('Yeni liste')
    expect(w.text()).not.toContain('Eski liste')
  })

  it('renders nothing and does not call the API for a non-admin', async () => {
    clearSession()
    setSession('t', { username: 'saha1', role: 'Field' })
    const w = mount(AdvisorsView, { global: { stubs: { RouterLink: true } } })
    await flushPromises()
    expect(api.get).not.toHaveBeenCalled()
    expect(w.find('h1').exists()).toBe(false)
  })
})

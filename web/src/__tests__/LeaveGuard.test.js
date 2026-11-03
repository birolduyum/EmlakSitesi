import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import AdminLayout from '../components/admin/AdminLayout.vue'
import ListingFormView from '../views/admin/ListingFormView.vue'
import { api } from '../api.js'
import { setSession, clearSession, getToken } from '../session.js'

vi.mock('../api.js', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn(), upload: vi.fn() } }))

const detail = {
  id: 5, title: 'Daire', badge: null, type: 'Satilik', price: 1000, currency: 'TRY', city: 'İzmir', district: 'Karşıyaka', rooms: '3+1',
  grossM2: 1, floor: 1, createdAt: '2026-08-01T10:00:00Z', photoUrls: [], deposit: null, aidat: null, neighborhood: '', bathrooms: 1,
  netM2: 1, totalFloors: 1, buildingAge: 1, heating: 'Kombi', furnishing: 'Esyasiz', usage: 'Bos', deed: 'KatMulkiyeti', kitchen: 'Acik',
  balcony: false, elevator: false, parking: false, inSite: false, loanEligible: false, swap: false, tags: [], description: '',
  owner: { name: 'A', phone: '1', note: null }, createdBy: 'ayse', isDeleted: false, photos: []
}
const mounted = []

beforeEach(() => {
  clearSession()
  localStorage.clear()
  for (const f of Object.values(api)) f.mockReset()
  api.get.mockImplementation((path) => (path === '/api/tags' ? Promise.resolve([]) : Promise.resolve(detail)))
})
afterEach(() => {
  while (mounted.length) mounted.pop().unmount()
})

async function make() {
  setSession('t', { username: 'ayse', role: 'Admin' })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin/giris', component: { template: '<p id="login">giris</p>' } },
      {
        path: '/admin',
        component: AdminLayout,
        children: [
          { path: '', component: { template: '<p>liste</p>' } },
          { path: 'ilan/:id', component: ListingFormView },
          { path: 'gosterim', component: { template: '<p>g</p>' } },
          { path: 'rapor', component: { template: '<p>r</p>' } },
          { path: 'danismanlar', component: { template: '<p>d</p>' } },
          { path: 'kur', component: { template: '<p>k</p>' } }
        ]
      }
    ]
  })
  router.push('/admin/ilan/5')
  await router.isReady()
  const w = mount({ template: '<router-view />' }, { global: { plugins: [router] }, attachTo: document.body })
  mounted.push(w)
  await flushPromises()
  await w.find('input[name="title"]').setValue('Değişti')
  return { w, router }
}

describe('leave guard', () => {
  it('logout_with_dirty_form_bypasses_the_prompt_and_ends_at_login', async () => {
    const { w, router } = await make()
    await w.find('[data-testid="logout"]').trigger('click')
    await flushPromises()
    expect(w.find('[role="dialog"]').exists()).toBe(false)
    expect(getToken()).toBeNull()
    expect(router.currentRoute.value.path).toBe('/admin/giris')
  })
  it('normal navigation with a dirty form still asks', async () => {
    const { w, router } = await make()
    router.push('/admin/gosterim')
    await flushPromises()
    expect(w.find('[role="dialog"]').exists()).toBe(true)
    expect(getToken()).toBe('t')
    await w.find('[data-testid="cancel"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/admin/ilan/5')
  })
  it('a second navigation cancels the first pending prompt instead of orphaning it', async () => {
    const { w, router } = await make()
    let firstDone = false
    const first = router.push('/admin/gosterim').then(() => {
      firstDone = true
    })
    await flushPromises()
    expect(firstDone).toBe(false)
    const second = router.push('/admin/rapor')
    await flushPromises()
    expect(firstDone).toBe(true)
    await first
    // ikinci gezinme için soru açık kalır; "Ayrıl" ikinciyi tamamlar
    expect(w.findAll('[role="dialog"]')).toHaveLength(1)
    await w.find('[data-testid="confirm"]').trigger('click')
    await second
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/admin/rapor')
  })
})

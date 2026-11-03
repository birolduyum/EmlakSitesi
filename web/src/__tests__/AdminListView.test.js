import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import AdminListView from '../views/admin/AdminListView.vue'
import { api } from '../api.js'
import { setSession, clearSession } from '../session.js'

vi.mock('../api.js', () => ({ api: { get: vi.fn(), post: vi.fn(), delete: vi.fn() } }))

const row = (id, over = {}) => ({
  id, title: `İlan ${id}`, badge: null, type: 'Satilik', price: 1000000, currency: 'TRY', city: 'Ankara',
  district: 'Çankaya', rooms: '2+1', grossM2: 90, floor: 1, createdAt: new Date(2026, 7, 23, 12).toISOString(), photoUrls: [],
  createdBy: 'ayse', isDeleted: false, deletedAt: null, deletedBy: null, owner: { name: 'Mehmet Kaya', phone: '0532 111 22 33', note: null },
  ...over
})
const fieldRow = (id) => {
  const r = row(id)
  delete r.owner
  delete r.createdBy
  delete r.isDeleted
  delete r.deletedAt
  delete r.deletedBy
  return r
}
const filtersBody = { cities: ['Ankara'], districtsByCity: { Ankara: ['Çankaya'] }, roomCounts: { '2+1': 3 } }

let listImpl
beforeEach(() => {
  clearSession()
  localStorage.clear()
  vi.mocked(api.get).mockReset()
  api.post.mockReset()
  api.delete.mockReset()
  listImpl = async () => ({ items: [row(1), row(2)], total: 2, page: 1, pageSize: 20 })
  api.get.mockImplementation((path, params) => {
    if (path === '/api/listings/filters') return Promise.resolve(filtersBody)
    if (path === '/api/admin/listings') return listImpl(params)
    return Promise.reject({ status: 404 })
  })
})

async function make(role = 'Admin', url = '/admin') {
  setSession('t', { username: 'kullanici', role })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin', component: AdminListView },
      { path: '/admin/ilan/yeni', component: { template: '<i/>' } },
      { path: '/admin/ilan/:id', component: { template: '<i/>' } },
      { path: '/ilan/:id', component: { template: '<i/>' } }
    ]
  })
  router.push(url)
  await router.isReady()
  const w = mount(AdminListView, { global: { plugins: [router] }, attachTo: document.body })
  await flushPromises()
  return { w, router }
}
const listCalls = () => api.get.mock.calls.filter((c) => c[0] === '/api/admin/listings')

describe('AdminListView', () => {
  it('initial load sends no type and no status (all types, active)', async () => {
    await make()
    expect(listCalls()[0][1]).toEqual({ page: 1, pageSize: 20 })
  })
  it('field_role_sees_no_edit_delete_buttons_and_no_owner_search', async () => {
    listImpl = async () => ({ items: [fieldRow(1), fieldRow(2)], total: 2, page: 1, pageSize: 20 })
    const { w } = await make('Field')
    const text = w.text()
    expect(w.findAll('article')).toHaveLength(2)
    for (const word of ['Düzenle', 'Sil', 'Geri al', 'Yeni ilan']) expect(text).not.toContain(word)
    expect(w.find('input[name="ownerText"]').exists()).toBe(false)
    expect(text).not.toContain('Mehmet Kaya')
    // satır Field için herkese açık ayrıntıya gider
    expect(w.find('article h2 a').attributes('href')).toBe('/ilan/1')
    // Field her zaman yalnızca aktifleri görür: durum süzgeci de yok
    expect(w.find('select[name="status"]').exists()).toBe(false)
    expect(listCalls()[0][1]).not.toHaveProperty('status')
    expect(listCalls()[0][1]).not.toHaveProperty('ownerText')
  })
  it('admin_sees_edit_delete_and_new_buttons', async () => {
    const { w } = await make()
    const article = w.findAll('article')[0]
    expect(article.text()).toContain('Düzenle')
    expect(article.text()).toContain('Sil')
    expect(article.text()).not.toContain('Geri al')
    expect(article.text()).toContain('Mehmet Kaya')
    expect(w.text()).toContain('Yeni ilan')
    expect(w.find('a[href="/admin/ilan/yeni"]').exists()).toBe(true)
    expect(article.find('a[href="/admin/ilan/1"]').exists()).toBe(true)
    expect(w.find('input[name="ownerText"]').exists()).toBe(true)
  })
  it('admin_sees_edit_delete_and_restore_on_deleted_row', async () => {
    listImpl = async () => ({
      items: [row(1), row(2, { isDeleted: true, deletedBy: 'veli', deletedAt: new Date(2026, 8, 5, 12).toISOString() })],
      total: 2, page: 1, pageSize: 20
    })
    const { w } = await make('Admin', '/admin?status=All')
    const [active, deleted] = w.findAll('article')
    expect(active.text()).not.toContain('Silindi')
    expect(active.text()).not.toContain('Geri al')
    expect(deleted.text()).toContain('Silindi — veli, 05.09.2026')
    expect(deleted.classes()).toContain('deleted')
    expect(deleted.text()).toContain('Geri al')
    expect(deleted.findAll('button').map((b) => b.text())).toEqual(['Geri al'])
  })
  it('status_filter_sends_status_param', async () => {
    const { w, router } = await make()
    await w.find('select[name="status"]').setValue('Deleted')
    await flushPromises()
    expect(listCalls().at(-1)[1]).toMatchObject({ status: 'Deleted', page: 1 })
    expect(router.currentRoute.value.query.status).toBe('Deleted')
  })
  it('owner_search_sends_ownerText_after_debounce', async () => {
    const { w } = await make()
    vi.useFakeTimers()
    try {
      await w.find('input[name="ownerText"]').setValue('Kaya')
      await vi.advanceTimersByTimeAsync(400)
    } finally {
      vi.useRealTimers()
    }
    await flushPromises()
    expect(listCalls().at(-1)[1]).toMatchObject({ ownerText: 'Kaya', page: 1 })
  })
  it('type_hepsi_omits_type_param', async () => {
    const { w } = await make('Admin', '/admin?type=Kiralik')
    expect(listCalls().at(-1)[1].type).toBe('Kiralik')
    await w.find('input[type="radio"][name="type"][value=""]').setValue()
    await flushPromises()
    expect(listCalls().at(-1)[1]).not.toHaveProperty('type')
  })
  it('delete_asks_confirmation_then_calls_api_then_reloads', async () => {
    api.delete.mockResolvedValue(null)
    const { w } = await make()
    const before = listCalls().length
    const sil = w.findAll('article')[0].findAll('button').find((b) => b.text() === 'Sil')
    await sil.trigger('click')
    const dialog = w.find('[role="dialog"]')
    expect(dialog.exists()).toBe(true)
    expect(dialog.text()).toContain('İlan 1')
    expect(api.delete).not.toHaveBeenCalled()
    await w.find('[data-testid="confirm"]').trigger('click')
    await flushPromises()
    expect(api.delete).toHaveBeenCalledWith('/api/admin/listings/1')
    expect(listCalls().length).toBe(before + 1)
    expect(w.find('[role="dialog"]').exists()).toBe(false)
    expect(w.find('[aria-live="polite"][data-testid="toast"]').text()).toContain('silindi')
  })
  it('cancel_in_confirm_does_not_delete', async () => {
    const { w } = await make()
    const before = listCalls().length
    const sil = w.findAll('article')[0].findAll('button').find((b) => b.text() === 'Sil')
    await sil.trigger('click')
    await w.find('[data-testid="cancel"]').trigger('click')
    await flushPromises()
    expect(api.delete).not.toHaveBeenCalled()
    expect(listCalls().length).toBe(before)
    expect(w.find('[role="dialog"]').exists()).toBe(false)
  })
  it('restore_calls_api_and_reloads', async () => {
    api.post.mockResolvedValue(null)
    listImpl = async () => ({ items: [row(3, { isDeleted: true, deletedBy: 'veli' })], total: 1, page: 1, pageSize: 20 })
    const { w } = await make('Admin', '/admin?status=Deleted')
    const before = listCalls().length
    await w.findAll('button').find((b) => b.text() === 'Geri al').trigger('click')
    await flushPromises()
    expect(api.post).toHaveBeenCalledWith('/api/admin/listings/3/restore')
    expect(listCalls().length).toBe(before + 1)
    expect(w.find('[data-testid="toast"]').text()).toContain('geri alındı')
  })
  it('failed_delete_shows_error_and_keeps_list', async () => {
    api.delete.mockRejectedValue({ status: 500 })
    const { w } = await make()
    await w.findAll('article')[0].findAll('button').find((b) => b.text() === 'Sil').trigger('click')
    await w.find('[data-testid="confirm"]').trigger('click')
    await flushPromises()
    expect(w.find('[data-testid="toast"]').text()).toContain('silinemedi')
    expect(w.findAll('article')).toHaveLength(2)
  })
  it('shows error state with retry and empty state', async () => {
    listImpl = async () => {
      throw { status: 500 }
    }
    const { w } = await make()
    expect(w.find('[role="alert"]').text()).toContain('İlanlar yüklenemedi')
    listImpl = async () => ({ items: [], total: 0, page: 1, pageSize: 20 })
    await w.find('[data-testid="retry"]').trigger('click')
    await flushPromises()
    expect(w.text()).toContain('uygun ilan bulunamadı')
  })
  it('stale response is ignored', async () => {
    const pending = []
    listImpl = (params) => new Promise((resolve) => pending.push({ params, resolve }))
    const { w, router } = await make()
    await router.replace({ query: { status: 'All' } })
    await flushPromises()
    expect(pending).toHaveLength(2)
    pending[1].resolve({ items: [row(9, { title: 'Yeni sonuç' })], total: 1 })
    await flushPromises()
    pending[0].resolve({ items: [row(8, { title: 'Eski sonuç' })], total: 1 })
    await flushPromises()
    expect(w.text()).toContain('Yeni sonuç')
    expect(w.text()).not.toContain('Eski sonuç')
  })
})

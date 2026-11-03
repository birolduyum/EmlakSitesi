import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import ShowingView from '../views/admin/ShowingView.vue'
import { api, ApiError } from '../api.js'
import { setSession, clearSession } from '../session.js'

vi.mock('../api.js', async (orig) => {
  const real = await orig()
  return { ...real, api: { get: vi.fn(), post: vi.fn() } }
})

const taken = new Date(2026, 10, 2, 14, 30).toISOString()
const record = (id, over = {}) => ({ id, listingId: 10 + id, listingTitle: `Ev ${id}`, username: 'saha1', takenAt: taken, returnedAt: null, note: null, ...over })
const listing = (id) => ({ id, title: `Ev ${id}`, city: 'Ankara', district: 'Çankaya', rooms: '2+1' })

let mine
beforeEach(() => {
  clearSession()
  localStorage.clear()
  api.get.mockReset()
  api.post.mockReset()
  mine = []
  api.get.mockImplementation((path) => {
    if (path === '/api/showings/mine') return Promise.resolve(mine)
    if (path === '/api/admin/listings') return Promise.resolve({ items: [listing(5), listing(6)], total: 2, page: 1, pageSize: 8 })
    return Promise.reject(new ApiError(404, null))
  })
})

async function make(role = 'Field') {
  setSession('t', { username: 'saha1', role })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin/gosterim', component: ShowingView },
      { path: '/ilan/:id', component: { template: '<i/>' } },
      { path: '/admin/ilan/:id', component: { template: '<i/>' } }
    ]
  })
  router.push('/admin/gosterim')
  await router.isReady()
  const w = mount(ShowingView, { global: { plugins: [router] }, attachTo: document.body })
  await flushPromises()
  return w
}

async function pick(w, id = 5) {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
  try {
    await w.find('input[role="combobox"]').setValue('ev')
    await vi.advanceTimersByTimeAsync(300)
  } finally {
    vi.useRealTimers()
  }
  await flushPromises()
  const opt = w.findAll('[role="option"]').find((o) => o.text().includes(`Ev ${id}`))
  await opt.trigger('click')
}
const takeButton = (w) => w.findAll('button').find((b) => b.text().includes('Anahtarı aldım'))
const status = (w) => w.find('[data-testid="status"]')
const openItems = (w) => w.findAll('[data-testid="open-record"]')

describe('ShowingView', () => {
  it('take button disabled until a listing is selected', async () => {
    const w = await make()
    expect(takeButton(w).attributes('disabled')).toBeDefined()
    await pick(w)
    expect(takeButton(w).attributes('disabled')).toBeUndefined()
  })

  it('take_posts_listing_id_and_disables_button_while_pending', async () => {
    let resolve
    api.post.mockImplementation(() => new Promise((r) => (resolve = r)))
    const w = await make()
    await pick(w, 5)
    await takeButton(w).trigger('click')
    expect(api.post).toHaveBeenCalledWith('/api/showings', { listingId: 5 })
    expect(takeButton(w).attributes('disabled')).toBeDefined()
    await takeButton(w).trigger('click')
    expect(api.post).toHaveBeenCalledTimes(1)
    resolve(record(1, { listingId: 5, listingTitle: 'Ev 5' }))
    await flushPromises()
    expect(status(w).text()).toContain('Ev 5 için kayıt açıldı')
    expect(w.find('input[role="combobox"]').exists()).toBe(true)
    expect(takeButton(w).attributes('disabled')).toBeDefined()
    expect(openItems(w)).toHaveLength(1)
  })

  it('take_409_shows_message', async () => {
    api.post.mockRejectedValue(new ApiError(409, null))
    const w = await make()
    await pick(w)
    await takeButton(w).trigger('click')
    await flushPromises()
    expect(w.find('[role="alert"]').text()).toContain('Bu ilan için zaten açık bir kaydınız var')
    expect(takeButton(w).attributes('disabled')).toBeUndefined()
  })

  it('take_404_shows_message', async () => {
    api.post.mockRejectedValue(new ApiError(404, null))
    const w = await make()
    await pick(w)
    await takeButton(w).trigger('click')
    await flushPromises()
    expect(w.find('[role="alert"]').text()).toContain('İlan bulunamadı veya silinmiş')
  })

  it('take network error shows generic message', async () => {
    api.post.mockRejectedValue(new ApiError(0, null))
    const w = await make()
    await pick(w)
    await takeButton(w).trigger('click')
    await flushPromises()
    expect(w.find('[role="alert"]').text()).toContain('Bağlantı kurulamadı')
  })

  it('open_records_listed_from_mine', async () => {
    mine = [record(1), record(2)]
    const w = await make('Field')
    const items = openItems(w)
    expect(items).toHaveLength(2)
    expect(items[0].text()).toContain('Ev 1')
    expect(items[0].text()).toContain('02.11.2026 14:30')
    expect(items[0].find('a').attributes('href')).toBe('/ilan/11')
    expect(w.text()).not.toContain('Açık kaydınız yok')
  })

  it('admin links to the admin edit page', async () => {
    mine = [record(1)]
    const w = await make('Admin')
    expect(openItems(w)[0].find('a').attributes('href')).toBe('/admin/ilan/11')
  })

  it('empty_state', async () => {
    const w = await make()
    expect(w.text()).toContain('Açık kaydınız yok')
  })

  it('mine load error shows alert', async () => {
    api.get.mockImplementation((path) => (path === '/api/showings/mine' ? Promise.reject(new ApiError(500, null)) : Promise.resolve({ items: [] })))
    const w = await make()
    expect(w.text()).toContain('Açık kayıtlar yüklenemedi')
  })

  it('close_sends_trimmed_note_and_removes_item', async () => {
    mine = [record(1), record(2)]
    api.post.mockResolvedValue(null)
    const w = await make()
    const first = openItems(w)[0]
    await first.find('textarea').setValue('  Müşteri beğendi  ')
    await first.find('button').trigger('click')
    expect(api.post).toHaveBeenCalledWith('/api/showings/1/close', { note: 'Müşteri beğendi' })
    await flushPromises()
    const items = openItems(w)
    expect(items).toHaveLength(1)
    expect(items[0].text()).toContain('Ev 2')
    expect(status(w).text()).toContain('Ev 1')
  })

  it('close with empty note sends null note and is pending-disabled', async () => {
    mine = [record(1)]
    let resolve
    api.post.mockImplementation(() => new Promise((r) => (resolve = r)))
    const w = await make()
    const btn = openItems(w)[0].find('button')
    await btn.trigger('click')
    expect(api.post).toHaveBeenCalledWith('/api/showings/1/close', { note: null })
    expect(btn.attributes('disabled')).toBeDefined()
    await btn.trigger('click')
    expect(api.post).toHaveBeenCalledTimes(1)
    resolve(null)
    await flushPromises()
  })

  it('close_403_shows_message', async () => {
    mine = [record(1)]
    api.post.mockRejectedValue(new ApiError(403, null))
    const w = await make()
    await openItems(w)[0].find('button').trigger('click')
    await flushPromises()
    expect(openItems(w)[0].find('[role="alert"]').text()).toContain('Bu kaydı kapatma yetkiniz yok')
    expect(openItems(w)).toHaveLength(1)
  })

  it('close 409 shows already closed message', async () => {
    mine = [record(1)]
    api.post.mockRejectedValue(new ApiError(409, null))
    const w = await make()
    await openItems(w)[0].find('button').trigger('click')
    await flushPromises()
    expect(openItems(w)[0].find('[role="alert"]').text()).toContain('Bu kayıt zaten kapatılmış')
  })

  it('note_counter_and_limit_500', async () => {
    mine = [record(1)]
    const w = await make()
    const ta = openItems(w)[0].find('textarea')
    expect(ta.attributes('maxlength')).toBe('500')
    expect(openItems(w)[0].text()).toContain('0 / 500')
    await ta.setValue('abc')
    expect(openItems(w)[0].text()).toContain('3 / 500')
  })

  it('late initial /mine response does not overwrite a freshly taken record', async () => {
    let resolveMine
    api.get.mockImplementation((path) => {
      if (path === '/api/showings/mine') return new Promise((r) => (resolveMine = r))
      return Promise.resolve({ items: [listing(5)], total: 1, page: 1, pageSize: 8 })
    })
    api.post.mockResolvedValue(record(1, { listingId: 5, listingTitle: 'Ev 5' }))
    const w = await make()
    await pick(w, 5)
    await takeButton(w).trigger('click')
    await flushPromises()
    resolveMine([])
    await flushPromises()
    expect(openItems(w)).toHaveLength(1)
    expect(openItems(w)[0].text()).toContain('Ev 5')
  })

  it('close 409 reloads /mine', async () => {
    mine = [record(1), record(2)]
    api.post.mockRejectedValue(new ApiError(409, null))
    const w = await make()
    mine = [record(2)]
    await openItems(w)[0].find('button').trigger('click')
    await flushPromises()
    expect(api.get.mock.calls.filter((c) => c[0] === '/api/showings/mine')).toHaveLength(2)
    expect(openItems(w)).toHaveLength(1)
    expect(openItems(w)[0].text()).toContain('Ev 2')
    expect(status(w).text()).toContain('zaten kapatılmıştı')
  })
})

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import ReportView from '../views/admin/ReportView.vue'
import { api, ApiError } from '../api.js'
import { setSession, clearSession } from '../session.js'

vi.mock('../api.js', async (orig) => {
  const real = await orig()
  return { ...real, api: { get: vi.fn() } }
})

const taken = new Date(2026, 10, 2, 14, 30).toISOString()
const returned = new Date(2026, 10, 2, 16, 5).toISOString()
const row = (id, over = {}) => ({ id, listingId: 40 + id, listingTitle: `Ev ${id}`, username: 'saha1', takenAt: taken, returnedAt: returned, note: null, ...over })

let rows
beforeEach(() => {
  clearSession()
  localStorage.clear()
  api.get.mockReset()
  rows = [row(1), row(2, { username: 'admin1', returnedAt: null, note: 'Anahtar bende' }), row(3)]
  api.get.mockImplementation(() => Promise.resolve(rows))
})

async function make(url = '/admin/rapor') {
  setSession('t', { username: 'admin1', role: 'Admin' })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin/rapor', component: ReportView },
      { path: '/admin/ilan/:id', component: { template: '<i/>' } }
    ]
  })
  router.push(url)
  await router.isReady()
  const w = mount(ReportView, { global: { plugins: [router] }, attachTo: document.body })
  await flushPromises()
  return { w, router }
}
// Beklenen anlar test makinesinin saat diliminden kurulur (sabit ISO dizesi yok): yerel gün başı ve yerel gün sonu.
const dayStart = (y, m, d) => new Date(y, m - 1, d, 0, 0, 0, 0).toISOString()
const dayEnd = (y, m, d) => new Date(y, m - 1, d, 23, 59, 59, 999).toISOString()
const calls = () => api.get.mock.calls.filter((c) => c[0] === '/api/admin/showings')

describe('ReportView', () => {
  it('loads all records initially with no params and renders a semantic table', async () => {
    const { w } = await make()
    expect(calls()[0][1]).toEqual({})
    expect(w.find('table caption').exists()).toBe(true)
    expect(w.findAll('th[scope="col"]').length).toBeGreaterThanOrEqual(5)
    const trs = w.findAll('tbody tr')
    expect(trs).toHaveLength(3)
    expect(trs[0].find('a').attributes('href')).toBe('/admin/ilan/41')
    expect(trs[0].text()).toContain('Ev 1')
    expect(trs[0].text()).toContain('02.11.2026 14:30')
    expect(trs[0].text()).toContain('02.11.2026 16:05')
    expect(w.text()).toContain('3 kayıt')
  })

  it('filters_send_params_and_date_only_values', async () => {
    const { w, router } = await make()
    await w.find('input[name="listingId"]').setValue('5')
    await w.find('input[name="username"]').setValue(' ayse ')
    await w.find('input[name="from"]').setValue('2026-11-01')
    await w.find('input[name="to"]').setValue('2026-11-02')
    await w.find('form').trigger('submit')
    await flushPromises()
    const last = calls().at(-1)[1]
    expect(last).toEqual({ listingId: 5, username: 'ayse', from: dayStart(2026, 11, 1), to: dayEnd(2026, 11, 2) })
    expect(router.currentRoute.value.query).toEqual({ listingId: '5', username: 'ayse', from: '2026-11-01', to: '2026-11-02' })
  })

  it('from_after_to_blocks_request', async () => {
    const { w, router } = await make()
    const before = calls().length
    await w.find('input[name="from"]').setValue('2026-11-05')
    await w.find('input[name="to"]').setValue('2026-11-02')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(calls().length).toBe(before)
    expect(w.find('[role="alert"]').text()).toContain('Başlangıç tarihi bitiş tarihinden sonra olamaz')
    expect(router.currentRoute.value.query.from).toBeUndefined()
  })

  it('invalid range in the URL does not call the API', async () => {
    const { w } = await make('/admin/rapor?from=2026-11-05&to=2026-11-02')
    expect(calls()).toHaveLength(0)
    expect(w.text()).toContain('Başlangıç tarihi bitiş tarihinden sonra olamaz')
  })

  it('non-numeric listingId is rejected before the request', async () => {
    const { w } = await make()
    const before = calls().length
    await w.find('input[name="listingId"]').setValue('abc')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(calls().length).toBe(before)
    expect(w.text()).toContain('İlan no pozitif bir sayı olmalı')
  })

  it('open_badge_when_returned_null', async () => {
    const { w } = await make()
    const trs = w.findAll('tbody tr')
    expect(trs[1].find('.badge-open').text()).toBe('Açık')
    expect(trs[0].find('.badge-open').exists()).toBe(false)
    expect(trs[1].text()).toContain('Anahtar bende')
  })

  it('cap_note_when_500_rows', async () => {
    rows = Array.from({ length: 500 }, (_, i) => row(i + 1))
    const { w } = await make()
    expect(w.text()).toContain('500 kayıt')
    expect(w.text()).toContain('İlk 500 kayıt gösteriliyor, filtreleri daraltın')
  })

  it('no cap note below 500', async () => {
    const { w } = await make()
    expect(w.text()).not.toContain('İlk 500 kayıt')
  })

  it('per_user_summary_counts', async () => {
    const { w } = await make()
    const summary = w.find('[data-testid="summary"]')
    expect(summary.text()).toContain('gösterilen kayıtlar içinde')
    expect(summary.text()).toMatch(/saha1\D+2/)
    expect(summary.text()).toMatch(/admin1\D+1/)
  })

  it('state_in_url', async () => {
    const { w } = await make('/admin/rapor?listingId=7&username=ayse&from=2026-11-01')
    expect(calls()[0][1]).toEqual({ listingId: 7, username: 'ayse', from: dayStart(2026, 11, 1) })
    expect(w.find('input[name="listingId"]').element.value).toBe('7')
    expect(w.find('input[name="username"]').element.value).toBe('ayse')
    expect(w.find('input[name="from"]').element.value).toBe('2026-11-01')
  })

  it('Temizle clears the filters and the URL', async () => {
    const { w, router } = await make('/admin/rapor?username=ayse')
    const clear = w.findAll('button').find((b) => b.text() === 'Temizle')
    await clear.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({})
    expect(calls().at(-1)[1]).toEqual({})
    expect(w.find('input[name="username"]').element.value).toBe('')
  })

  it('empty_state', async () => {
    rows = []
    const { w } = await make()
    expect(w.text()).toContain('Kayıt bulunamadı')
    expect(w.find('table').exists()).toBe(false)
  })

  it('error_state', async () => {
    api.get.mockRejectedValue(new ApiError(500, null))
    const { w } = await make()
    expect(w.find('[role="alert"]').text()).toContain('Rapor yüklenemedi')
    api.get.mockResolvedValue(rows)
    await w.find('[data-testid="retry"]').trigger('click')
    await flushPromises()
    expect(w.findAll('tbody tr')).toHaveLength(3)
  })

  it('stale response is ignored', async () => {
    let resolveFirst
    api.get
      .mockImplementationOnce(() => new Promise((r) => (resolveFirst = r)))
      .mockResolvedValueOnce([row(9, { listingTitle: 'Yeni' })])
    const { w } = await make()
    await w.find('input[name="username"]').setValue('x')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(w.text()).toContain('Yeni')
    resolveFirst([row(1, { listingTitle: 'Eski' })])
    await flushPromises()
    expect(w.text()).not.toContain('Eski')
  })

  it('non-admin triggers no API call and renders nothing', async () => {
    setSession('t', { username: 'saha1', role: 'Field' })
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/admin/rapor', component: ReportView }] })
    router.push('/admin/rapor')
    await router.isReady()
    const w = mount(ReportView, { global: { plugins: [router] } })
    await flushPromises()
    expect(calls()).toHaveLength(0)
    expect(w.find('form').exists()).toBe(false)
  })

  it('shows_the_local_time_zone_hint_and_no_UTC_text', async () => {
    const { w } = await make()
    expect(w.text()).toContain('Tarihler bulunduğunuz saat dilimine göre aranır.')
    expect(w.text()).not.toContain('UTC')
  })

  it('sends_the_chosen_days_as_local_day_instants_and_keeps_plain_days_in_the_URL', async () => {
    const { w, router } = await make()
    await w.find('input[name="from"]').setValue('2026-03-29') // Avrupa/ABD saat değişimi günleri de yerel gün sınırını bozmaz
    await w.find('input[name="to"]').setValue('2026-03-29')
    await w.find('form').trigger('submit')
    await flushPromises()
    const sent = calls().at(-1)[1]
    expect(sent.from).toBe(new Date(2026, 2, 29, 0, 0, 0, 0).toISOString())
    expect(sent.to).toBe(new Date(2026, 2, 29, 23, 59, 59, 999).toISOString())
    expect(new Date(sent.to).getTime() - new Date(sent.from).getTime()).toBeGreaterThanOrEqual(23 * 3600 * 1000)
    expect(sent.from).toMatch(/Z$/)
    expect(router.currentRoute.value.query).toEqual({ from: '2026-03-29', to: '2026-03-29' })
  })
})

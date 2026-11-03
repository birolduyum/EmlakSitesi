import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import ListingPicker from '../components/admin/ListingPicker.vue'
import { api } from '../api.js'

vi.mock('../api.js', () => ({ api: { get: vi.fn() } }))

const item = (id) => ({ id, title: `Ev ${id}`, city: 'Ankara', district: 'Çankaya', rooms: '2+1' })
const page = (...ids) => ({ items: ids.map(item), total: ids.length, page: 1, pageSize: 8 })

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
  api.get.mockReset()
})
afterEach(() => vi.useRealTimers())

async function typeText(w, text) {
  await w.find('input').setValue(text)
}

describe('ListingPicker', () => {
  it('debounced_search_sends_text', async () => {
    api.get.mockResolvedValue(page(1, 2))
    const w = mount(ListingPicker, { props: { modelValue: null } })
    await typeText(w, 'ça')
    await typeText(w, 'çan')
    expect(api.get).not.toHaveBeenCalled()
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    expect(api.get).toHaveBeenCalledTimes(1)
    const [path, params] = api.get.mock.calls[0]
    expect(path).toBe('/api/admin/listings')
    expect(params).toMatchObject({ text: 'çan', status: 'Active' })
    expect(params.type).toBeUndefined()
    const options = w.findAll('[role="option"]')
    expect(options).toHaveLength(2)
    expect(options[0].text()).toContain('Ev 1')
    expect(options[0].text()).toContain('Ankara, Çankaya')
    expect(options[0].text()).toContain('2+1')
    expect(options[0].text()).toContain('#1')
  })

  it('blank text does not query and clears results', async () => {
    api.get.mockResolvedValue(page(1))
    const w = mount(ListingPicker, { props: { modelValue: null } })
    await typeText(w, 'a')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    await typeText(w, '   ')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    expect(api.get).toHaveBeenCalledTimes(1)
    expect(w.findAll('[role="option"]')).toHaveLength(0)
  })

  it('keyboard_arrows_and_enter_select', async () => {
    api.get.mockResolvedValue(page(1, 2, 3))
    const w = mount(ListingPicker, { props: { modelValue: null } })
    const input = w.find('input')
    expect(input.attributes('role')).toBe('combobox')
    await typeText(w, 'ev')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    expect(input.attributes('aria-expanded')).toBe('true')
    await input.trigger('keydown', { key: 'ArrowDown' })
    await input.trigger('keydown', { key: 'ArrowDown' })
    const active = w.findAll('[role="option"]')[1]
    expect(active.attributes('aria-selected')).toBe('true')
    expect(input.attributes('aria-activedescendant')).toBe(active.attributes('id'))
    await input.trigger('keydown', { key: 'ArrowUp' })
    await input.trigger('keydown', { key: 'ArrowDown' })
    await input.trigger('keydown', { key: 'Enter' })
    expect(w.emitted('update:modelValue')[0][0]).toMatchObject({ id: 2, title: 'Ev 2' })
  })

  it('click selects and Escape closes', async () => {
    api.get.mockResolvedValue(page(1, 2))
    const w = mount(ListingPicker, { props: { modelValue: null } })
    await typeText(w, 'ev')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    await w.find('input').trigger('keydown', { key: 'Escape' })
    expect(w.find('input').attributes('aria-expanded')).toBe('false')
    await typeText(w, 'ev2')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    await w.findAll('[role="option"]')[0].trigger('click')
    expect(w.emitted('update:modelValue')[0][0].id).toBe(1)
  })

  it('stale_response_is_ignored', async () => {
    let resolveFirst
    api.get
      .mockImplementationOnce(() => new Promise((r) => (resolveFirst = r)))
      .mockResolvedValueOnce(page(9))
    const w = mount(ListingPicker, { props: { modelValue: null } })
    await typeText(w, 'a')
    await vi.advanceTimersByTimeAsync(300)
    await typeText(w, 'ab')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    expect(w.findAll('[role="option"]').map((o) => o.text())[0]).toContain('Ev 9')
    resolveFirst(page(1, 2, 3))
    await flushPromises()
    const options = w.findAll('[role="option"]')
    expect(options).toHaveLength(1)
    expect(options[0].text()).toContain('Ev 9')
  })

  it('shows empty and error states', async () => {
    api.get.mockResolvedValueOnce(page())
    const w = mount(ListingPicker, { props: { modelValue: null } })
    await typeText(w, 'zzz')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    expect(w.text()).toContain('İlan bulunamadı')
    api.get.mockRejectedValueOnce({ status: 0 })
    await typeText(w, 'yyy')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    expect(w.find('[role="alert"]').text()).toContain('Arama yapılamadı')
  })

  it('selected listing shows as chip with Değiştir that clears', async () => {
    const w = mount(ListingPicker, { props: { modelValue: item(7) } })
    expect(w.find('input').exists()).toBe(false)
    expect(w.text()).toContain('Ev 7')
    const change = w.findAll('button').find((b) => b.text() === 'Değiştir')
    await change.trigger('click')
    expect(w.emitted('update:modelValue')[0][0]).toBeNull()
  })
})

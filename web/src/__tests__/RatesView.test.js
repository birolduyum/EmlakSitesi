import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import RatesView from '../views/admin/RatesView.vue'
import { api, ApiError } from '../api.js'
import { setSession, clearSession } from '../session.js'

vi.mock('../api.js', async (orig) => {
  const real = await orig()
  return { ...real, api: { get: vi.fn(), put: vi.fn() } }
})

const updated = new Date(2026, 10, 2, 14, 30).toISOString()
const rates = (usd = 41.25, eur = 47.5) => [
  { currency: 'USD', tryPerUnit: usd, updatedAt: updated, updatedBy: 'admin1' },
  { currency: 'EUR', tryPerUnit: eur, updatedAt: updated, updatedBy: 'admin2' }
]

beforeEach(() => {
  clearSession()
  setSession('t', { username: 'admin1', role: 'Admin' })
  api.get.mockReset()
  api.put.mockReset()
  api.get.mockResolvedValue(rates())
})

async function make() {
  const w = mount(RatesView, { attachTo: document.body })
  await flushPromises()
  return w
}
const usd = (w) => w.find('input[name="usd"]')
const eur = (w) => w.find('input[name="eur"]')
const save = (w) => w.find('button[type="submit"]')

describe('RatesView', () => {
  it('shows_last_updated_by', async () => {
    const w = await make()
    expect(api.get).toHaveBeenCalledWith('/api/admin/rates')
    expect(w.text()).toContain('Son güncelleme: 02.11.2026 14:30, admin1')
    expect(w.text()).toContain('Son güncelleme: 02.11.2026 14:30, admin2')
    expect(usd(w).element.value).toBe('41,25')
    expect(eur(w).element.value).toBe('47,5')
    expect(w.text()).toContain('Fiyat filtresi ve fiyata göre sıralama')
    expect(w.text()).toContain('İlan kartlarındaki fiyatlar değişmez')
  })

  it.each([['0'], ['-5'], ['1000000,01'], ['2.000.000'], ['abc'], ['']])(
    'rejects_zero_negative_and_huge_before_calling_api (%s)',
    async (bad) => {
      const w = await make()
      await usd(w).setValue(bad)
      await w.find('form').trigger('submit')
      await flushPromises()
      expect(api.put).not.toHaveBeenCalled()
      expect(w.find('#f-usd-err').exists()).toBe(true)
    }
  )

  it('accepts the upper bound 1.000.000', async () => {
    api.put.mockResolvedValue(null)
    const w = await make()
    await usd(w).setValue('1.000.000')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(api.put).toHaveBeenCalledWith('/api/admin/rates', { usd: 1000000, eur: 47.5 })
  })

  it('accepts_comma_decimals', async () => {
    api.put.mockResolvedValue(null)
    const w = await make()
    await usd(w).setValue('42,5')
    await eur(w).setValue('48.75')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(api.put).toHaveBeenCalledWith('/api/admin/rates', { usd: 42.5, eur: 48.75 })
  })

  it('server_400_errors_shown', async () => {
    api.put.mockRejectedValue(new ApiError(400, { errors: ['USD kuru geçersiz.', 'EUR kuru geçersiz.'] }))
    const w = await make()
    await usd(w).setValue('42')
    await w.find('form').trigger('submit')
    await flushPromises()
    const alert = w.find('[role="alert"]')
    expect(alert.text()).toContain('USD kuru geçersiz.')
    expect(alert.text()).toContain('EUR kuru geçersiz.')
  })

  it('network error shows generic message', async () => {
    api.put.mockRejectedValue(new ApiError(0, null))
    const w = await make()
    await usd(w).setValue('42')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(w.find('[role="alert"]').text()).toContain('Bağlantı kurulamadı')
  })

  it('save_disabled_when_unchanged_or_pending', async () => {
    let resolve
    api.put.mockImplementation(() => new Promise((r) => (resolve = r)))
    const w = await make()
    expect(save(w).attributes('disabled')).toBeDefined()
    await usd(w).setValue('42')
    expect(save(w).attributes('disabled')).toBeUndefined()
    await usd(w).setValue('41.25')
    expect(save(w).attributes('disabled')).toBeDefined()
    await usd(w).setValue('42')
    await w.find('form').trigger('submit')
    expect(save(w).attributes('disabled')).toBeDefined()
    await w.find('form').trigger('submit')
    expect(api.put).toHaveBeenCalledTimes(1)
    resolve(null)
    await flushPromises()
  })

  it('success_message', async () => {
    api.put.mockResolvedValue(null)
    const w = await make()
    api.get.mockResolvedValue(rates(42, 47.5).map((r) => ({ ...r, updatedBy: 'admin1' })))
    await usd(w).setValue('42')
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(w.find('[data-testid="status"]').text()).toContain('Kurlar güncellendi — fiyat filtresi artık yeni kurla çalışıyor')
    expect(w.text()).toContain('Son güncelleme: 02.11.2026 14:30, admin1')
    expect(save(w).attributes('disabled')).toBeDefined()
  })

  it('load error shows alert with retry', async () => {
    api.get.mockRejectedValueOnce(new ApiError(500, null))
    const w = await make()
    expect(w.find('[role="alert"]').text()).toContain('Kurlar yüklenemedi')
    await w.find('[data-testid="retry"]').trigger('click')
    await flushPromises()
    expect(usd(w).exists()).toBe(true)
  })
})

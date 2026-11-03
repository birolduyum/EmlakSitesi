import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { defineComponent, ref, h } from 'vue'
import { useAdvisor, callLabels } from '../composables/useAdvisor.js'
import { resetSiteInfo } from '../site.js'
import { api } from '../api.js'

vi.mock('../api.js', () => ({ api: { get: vi.fn() } }))

const site = { advisorName: 'Site Danışmanı', advisorPhone: '+90 555 000 00 00', whatsappUrl: 'https://wa.me/905550000000' }

beforeEach(() => {
  resetSiteInfo()
  api.get.mockReset()
  api.get.mockResolvedValue(site)
})

// Bileşenin dışından okunabilsin diye composable sonucunu döndürür.
function host(source) {
  let result
  const w = mount(
    defineComponent({
      setup() {
        result = useAdvisor(source)
        return () => h('i')
      }
    })
  )
  return { w, result }
}

describe('useAdvisor', () => {
  it('a listing advisor overrides the site info and no site request is made', async () => {
    const { result } = host({ name: 'Ayşe Kaya', phone: '0 532 444 55 66', whatsappUrl: 'https://wa.me/905324445566' })
    await flushPromises()
    expect(result.info.value).toEqual({ advisorName: 'Ayşe Kaya', advisorPhone: '0 532 444 55 66', whatsappUrl: 'https://wa.me/905324445566' })
    expect(result.whatsappUrl.value).toBe('https://wa.me/905324445566')
    expect(result.telHref.value).toBe('tel:05324445566')
    expect(api.get).not.toHaveBeenCalled()
  })

  it('without an advisor (null or nothing) it falls back to /api/site', async () => {
    for (const source of [null, undefined, () => null]) {
      resetSiteInfo()
      api.get.mockClear()
      const { result } = host(source)
      await flushPromises()
      expect(api.get).toHaveBeenCalledWith('/api/site')
      expect(result.info.value.advisorName).toBe('Site Danışmanı')
      expect(result.whatsappUrl.value).toBe('https://wa.me/905550000000')
    }
  })

  it('whatsapp is only taken from http(s); the advisor phone still gives the tel: fallback', async () => {
    const { result } = host({ name: 'X', phone: '+90 532 111 22 33', whatsappUrl: 'javascript:alert(1)' })
    await flushPromises()
    expect(result.whatsappUrl.value).toBeNull()
    expect(result.telHref.value).toBe('tel:+905321112233')
  })

  it('reacts when the advisor appears later (listing loaded after mount) and stops using the site info', async () => {
    const advisor = ref(null)
    const { result } = host(advisor)
    await flushPromises()
    expect(result.info.value.advisorName).toBe('Site Danışmanı')
    advisor.value = { name: 'Zeynep', phone: '0 532 444 55 66', whatsappUrl: null }
    await flushPromises()
    expect(result.info.value.advisorName).toBe('Zeynep')
    expect(result.whatsappUrl.value).toBeNull()
    expect(api.get).toHaveBeenCalledTimes(1)
  })

  it('a site info failure leaves the info empty without throwing', async () => {
    api.get.mockRejectedValue({ status: 500 })
    const { result } = host(null)
    await flushPromises()
    expect(result.info.value).toBeNull()
    expect(result.telHref.value).toBeNull()
  })
})

describe('callLabels', () => {
  it('gives the same label and aria text for the card and the link', () => {
    expect(callLabels(true, 'Ayşe')).toEqual({ label: "WhatsApp'tan Ara", aria: "WhatsApp'tan Ara: Ayşe" })
    expect(callLabels(false, 'Ayşe')).toEqual({ label: 'Ara', aria: 'Ara: Ayşe' })
    expect(callLabels(false, undefined)).toEqual({ label: 'Ara', aria: 'Ara:' })
  })
})

import { describe, it, expect } from 'vitest'
import { whatsappMessageUrl, safeWhatsappUrl } from '../composables/useAdvisor.js'

const text = (u) => new URL(u).searchParams.get('text')

describe('whatsappMessageUrl', () => {
  it('builds the prefilled message with encodeURIComponent', () => {
    const u = whatsappMessageUrl('https://wa.me/905320000001', { id: 7, title: 'Deniz manzaralı 3+1' })
    expect(u).toContain('text=Merhaba%2C%20Deniz%20manzaral%C4%B1%203%2B1%20(%C4%B0lan%20No%3A%207)')
    expect(text(u)).toBe('Merhaba, Deniz manzaralı 3+1 (İlan No: 7) hakkında bilgi almak istiyorum.')
  })
  it('message_url_encodes_title_and_keeps_existing_query', () => {
    const title = 'A&B #1 "tırnak" \'x\' 🏠 Şişli ?=%'
    const u = whatsappMessageUrl('https://wa.me/905320000001?app_absent=1', { id: 3, title })
    const url = new URL(u)
    expect(url.searchParams.get('app_absent')).toBe('1')
    expect(url.searchParams.get('text')).toBe(`Merhaba, ${title} (İlan No: 3) hakkında bilgi almak istiyorum.`)
    expect(url.hash).toBe('')
    expect([...url.searchParams.keys()]).toEqual(['app_absent', 'text'])
  })
  it('replaces an existing text parameter', () => {
    const u = whatsappMessageUrl('https://wa.me/1?text=eski', { id: 1, title: 'T' })
    expect(new URL(u).searchParams.getAll('text')).toHaveLength(1)
    expect(text(u)).toContain('T (İlan No: 1)')
  })
  it('returns null for unsafe, malformed or missing input', () => {
    expect(whatsappMessageUrl('javascript:alert(1)', { id: 1, title: 'x' })).toBeNull()
    expect(whatsappMessageUrl('data:text/html,x', { id: 1, title: 'x' })).toBeNull()
    expect(whatsappMessageUrl(null, { id: 1, title: 'x' })).toBeNull()
    expect(whatsappMessageUrl('https://wa.me/1', null)).toBeNull()
    expect(whatsappMessageUrl('http://', { id: 1, title: 'x' })).toBeNull()
  })
  it('safeWhatsappUrl only passes http(s)', () => {
    expect(safeWhatsappUrl('https://wa.me/1')).toBe('https://wa.me/1')
    expect(safeWhatsappUrl('javascript:alert(1)')).toBeNull()
  })
})

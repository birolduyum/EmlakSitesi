import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import Lightbox from '../components/Lightbox.vue'
import { resetSiteInfo } from '../site.js'
import { api } from '../api.js'

vi.mock('../api.js', () => ({ api: { get: vi.fn() } }))

const photos = ['/photos/a.jpg', '/photos/b.jpg', '/photos/c.jpg']
let wrapper
const q = (sel) => document.body.querySelector(sel)
const shownSrc = () => q('[data-testid="lightbox-image"]').getAttribute('src')
const key = (k, extra = {}) =>
  document.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true, ...extra }))

async function open(startIndex = 0, list = photos) {
  wrapper = mount(Lightbox, { props: { photos: list, startIndex }, attachTo: document.body })
  await flushPromises()
  return wrapper
}

beforeEach(() => {
  resetSiteInfo()
  api.get.mockReset()
  api.get.mockResolvedValue({ advisorName: 'Ayşe', advisorPhone: '+90 532 111 22 33', whatsappUrl: null })
  document.body.style.overflow = ''
})
afterEach(() => {
  wrapper?.unmount()
  wrapper = null
  document.body.innerHTML = ''
})

describe('Lightbox', () => {
  it('opens_on_start_index', async () => {
    await open(2)
    expect(shownSrc()).toBe('/photos/c.jpg')
  })
  it('counter_shows_position', async () => {
    await open(2)
    expect(q('[data-testid="counter"]').textContent.trim()).toBe('3 / 3')
  })
  it('arrow_right_moves_to_next_photo', async () => {
    await open(0)
    q('[data-testid="next"]').click()
    await flushPromises()
    expect(shownSrc()).toBe('/photos/b.jpg')
    q('[data-testid="prev"]').click()
    await flushPromises()
    expect(shownSrc()).toBe('/photos/a.jpg')
  })
  it('left_hidden_on_first_and_right_hidden_on_last', async () => {
    await open(0)
    expect(q('[data-testid="prev"]')).toBeNull()
    expect(q('[data-testid="next"]')).not.toBeNull()
    wrapper.unmount()
    await open(2)
    expect(q('[data-testid="next"]')).toBeNull()
    expect(q('[data-testid="prev"]')).not.toBeNull()
  })
  it('keyboard_arrows_navigate', async () => {
    await open(1)
    key('ArrowRight')
    await flushPromises()
    expect(shownSrc()).toBe('/photos/c.jpg')
    key('ArrowRight')
    await flushPromises()
    expect(shownSrc()).toBe('/photos/c.jpg')
    key('ArrowLeft')
    await flushPromises()
    expect(shownSrc()).toBe('/photos/b.jpg')
  })
  it('escape_emits_close', async () => {
    await open()
    key('Escape')
    expect(wrapper.emitted('close')).toHaveLength(1)
  })
  it('backdrop_click_emits_close', async () => {
    await open()
    q('[data-testid="stage"]').click()
    expect(wrapper.emitted('close')).toHaveLength(1)
  })
  it('clicking the photo itself does not close', async () => {
    await open()
    q('[data-testid="lightbox-image"]').click()
    expect(wrapper.emitted('close')).toBeUndefined()
  })
  it('Geri button emits close and Ara is a tel: link', async () => {
    await open()
    q('[data-testid="back"]').click()
    expect(wrapper.emitted('close')).toHaveLength(1)
    expect(q('a[href="tel:+905321112233"]')).not.toBeNull()
  })
  it('top bar Ara is a WhatsApp link when whatsappUrl exists', async () => {
    api.get.mockResolvedValue({ advisorName: 'Ayşe', advisorPhone: '+90 532 111 22 33', whatsappUrl: 'https://wa.me/905320000001' })
    await open()
    const a = q('a[href="https://wa.me/905320000001"]')
    expect(a.textContent.trim()).toBe("WhatsApp'tan Ara")
    expect(a.getAttribute('aria-label')).toBe("WhatsApp'tan Ara: Ayşe")
    expect(a.getAttribute('rel')).toBe('noopener noreferrer')
  })
  it('is a labelled modal dialog', async () => {
    await open()
    const d = q('[role="dialog"]')
    expect(d.getAttribute('aria-modal')).toBe('true')
    expect(d.getAttribute('aria-label')).toBeTruthy()
  })
  it('focus_returns_to_opener_on_close', async () => {
    const trigger = document.createElement('button')
    document.body.appendChild(trigger)
    trigger.focus()
    expect(document.activeElement).toBe(trigger)
    await open()
    expect(q('[role="dialog"]').contains(document.activeElement)).toBe(true)
    wrapper.unmount()
    wrapper = null
    expect(document.activeElement).toBe(trigger)
  })
  it('focus_returns_to_explicit_opener_when_activeElement_is_body', async () => {
    const trigger = document.createElement('button')
    document.body.appendChild(trigger)
    // Safari/iOS: tıklanan düğme odak almaz, activeElement body kalır.
    document.body.focus()
    expect(document.activeElement).toBe(document.body)
    wrapper = mount(Lightbox, { props: { photos, startIndex: 0, opener: trigger }, attachTo: document.body })
    await flushPromises()
    wrapper.unmount()
    wrapper = null
    expect(document.activeElement).toBe(trigger)
  })
  it('traps Tab inside the dialog', async () => {
    await open(1)
    const dialog = q('[role="dialog"]')
    const focusables = [...dialog.querySelectorAll('a[href], button')]
    focusables[focusables.length - 1].focus()
    key('Tab')
    expect(document.activeElement).toBe(focusables[0])
    key('Tab', { shiftKey: true })
    expect(document.activeElement).toBe(focusables[focusables.length - 1])
  })
  it('body_scroll_locked_while_open_and_restored', async () => {
    document.body.style.overflow = 'auto'
    await open()
    expect(document.body.style.overflow).toBe('hidden')
    wrapper.unmount()
    wrapper = null
    expect(document.body.style.overflow).toBe('auto')
  })
  const ev = (type, x, y = 0, target = null) => {
    const e = new Event(type, { bubbles: true })
    e.clientX = x
    e.clientY = y
    e.pointerId = 1
    return e
  }
  const drag = (el, x1, y1, x2, y2) => {
    el.dispatchEvent(ev('pointerdown', x1, y1))
    el.dispatchEvent(ev('pointerup', x2, y2))
  }
  it('horizontal swipe left goes next, right goes previous', async () => {
    await open(1)
    const stage = q('[data-testid="stage"]')
    drag(stage, 300, 100, 100, 110)
    await flushPromises()
    expect(shownSrc()).toBe('/photos/c.jpg')
    drag(stage, 100, 100, 300, 90)
    await flushPromises()
    expect(shownSrc()).toBe('/photos/b.jpg')
  })
  it('vertical swipe does not change the photo', async () => {
    await open(1)
    drag(q('[data-testid="stage"]'), 200, 100, 140, 400)
    await flushPromises()
    expect(shownSrc()).toBe('/photos/b.jpg')
  })
  it('a tiny horizontal move does not change the photo', async () => {
    await open(1)
    drag(q('[data-testid="stage"]'), 200, 100, 180, 100)
    await flushPromises()
    expect(shownSrc()).toBe('/photos/b.jpg')
  })
  it('a swipe does not close the lightbox through the synthetic click', async () => {
    await open(1)
    const stage = q('[data-testid="stage"]')
    drag(stage, 300, 100, 100, 100)
    stage.click()
    expect(wrapper.emitted('close')).toBeUndefined()
    // sonraki gerçek arka plan tıklaması yine kapatır
    stage.click()
    expect(wrapper.emitted('close')).toHaveLength(1)
  })
  it('a pointerdown on a button does not start a swipe', async () => {
    await open(1)
    const next = q('[data-testid="next"]')
    next.dispatchEvent(ev('pointerdown', 300, 100))
    q('[data-testid="stage"]').dispatchEvent(ev('pointerup', 100, 100))
    await flushPromises()
    expect(shownSrc()).toBe('/photos/b.jpg')
  })
  it('uses_the_advisor_prop_over_the_site_default', async () => {
    api.get.mockResolvedValue({ advisorName: 'Varsayılan', advisorPhone: '+90 555 000 00 00', whatsappUrl: 'https://wa.me/905550000000' })
    wrapper = mount(Lightbox, {
      props: { photos, startIndex: 0, advisor: { name: 'Zeynep', phone: '0 532 444 55 66', whatsappUrl: null } },
      attachTo: document.body
    })
    await flushPromises()
    const a = q('[role="dialog"] .bar a.call')
    expect(a.getAttribute('href')).toBe('tel:05324445566')
    expect(a.getAttribute('aria-label')).toBe('Ara: Zeynep')
    expect(q('a[href="https://wa.me/905550000000"]')).toBeNull()
  })
})

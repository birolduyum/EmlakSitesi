import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PhotoStrip from '../components/PhotoStrip.vue'

const photos = ['/photos/a.jpg', '/photos/b.jpg', '/photos/c.jpg', '/photos/d.jpg', '/photos/e.jpg']
const make = (p = photos) => mount(PhotoStrip, { props: { photos: p, alt: 'Deniz manzaralı daire' } })

describe('PhotoStrip', () => {
  it('renders_one_dot_per_photo', () => {
    expect(make().findAll('[data-testid="dot"]')).toHaveLength(5)
  })
  it('shows the first photo initially with alt and lazy loading', () => {
    const img = make().find('img')
    expect(img.attributes('src')).toBe('/photos/a.jpg')
    expect(img.attributes('alt')).toBe('Deniz manzaralı daire')
    expect(img.attributes('loading')).toBe('lazy')
  })
  it('hover_on_third_dot_shows_third_photo', async () => {
    const w = make()
    await w.findAll('[data-testid="dot"]')[2].trigger('mouseenter')
    expect(w.find('img').attributes('src')).toBe('/photos/c.jpg')
  })
  it('mouseleave_resets_to_first', async () => {
    const w = make()
    await w.findAll('[data-testid="dot"]')[3].trigger('mouseenter')
    await w.trigger('mouseleave')
    expect(w.find('img').attributes('src')).toBe('/photos/a.jpg')
  })
  it('focus_on_dot_shows_its_photo', async () => {
    const w = make()
    const dots = w.findAll('[data-testid="dot"]')
    await dots[1].trigger('focus')
    expect(w.find('img').attributes('src')).toBe('/photos/b.jpg')
    expect(dots[1].attributes('aria-label')).toBe('Fotoğraf 2')
  })
  it('zero_photos_shows_placeholder_and_no_dots', () => {
    const w = make([])
    expect(w.findAll('[data-testid="dot"]')).toHaveLength(0)
    expect(w.find('img').attributes('src')).toContain('placeholder')
  })
  it('one_photo_has_no_dots', () => {
    const w = make(['/photos/a.jpg'])
    expect(w.findAll('[data-testid="dot"]')).toHaveLength(0)
    expect(w.find('img').attributes('src')).toBe('/photos/a.jpg')
  })
})

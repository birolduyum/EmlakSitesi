import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import DetailPhotos from '../components/DetailPhotos.vue'

const photos = (n) => Array.from({ length: n }, (_, i) => `/photos/${i + 1}.jpg`)
function make(n) {
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:p(.*)*', component: { template: '<i/>' } }] })
  return mount(DetailPhotos, { props: { photos: photos(n), allLink: '/ilan/1/fotograflar' }, global: { plugins: [router] } })
}

describe('DetailPhotos layout classes', () => {
  it.each([
    [0, 'n-0', 0],
    [1, 'n-1', 1],
    [2, 'n-2', 2],
    [3, 'n-3', 3],
    [4, 'n-4', 4],
    [5, 'n-5', 5],
    [9, 'n-5', 5]
  ])('%i photos -> %s with %i cells', (n, cls, cells) => {
    const w = make(n)
    expect(w.find('[data-testid="photo-block"]').classes()).toContain(cls)
    const buttons = w.findAll('[data-testid="photo-button"]')
    expect(buttons).toHaveLength(cells)
  })
  it('first cell is big, the rest are small s1..s4 in order', () => {
    const buttons = make(5).findAll('[data-testid="photo-button"]')
    expect(buttons[0].classes()).toContain('big')
    expect(buttons.slice(1).map((b) => b.classes().filter((c) => /^s\d$/.test(c))[0])).toEqual(['s1', 's2', 's3', 's4'])
  })
  it('emits open with the original index and the clicked element', async () => {
    const w = make(5)
    await w.findAll('[data-testid="photo-button"]')[3].trigger('click')
    const [index, el] = w.emitted('open')[0]
    expect(index).toBe(3)
    expect(el).toBeInstanceOf(HTMLButtonElement)
  })
})

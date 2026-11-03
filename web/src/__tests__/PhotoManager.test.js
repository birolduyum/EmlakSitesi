import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import PhotoManager from '../components/admin/PhotoManager.vue'
import { api } from '../api.js'

vi.mock('../api.js', () => ({ api: { put: vi.fn(), delete: vi.fn(), upload: vi.fn() } }))

const photos = (n) => Array.from({ length: n }, (_, i) => ({ id: 100 + i, url: `/photos/${i}.jpg`, sortOrder: i }))
const file = (name, type, size = 1000) => {
  const f = new File(['x'], name, { type })
  Object.defineProperty(f, 'size', { value: size })
  return f
}
const make = (n = 3) => mount(PhotoManager, { props: { listingId: 5, photos: photos(n) }, attachTo: document.body })
const ids = (w) => w.findAll('[data-testid="photo"]').map((x) => Number(x.attributes('data-photo-id')))

async function pick(w, files) {
  const input = w.find('input[type="file"]')
  Object.defineProperty(input.element, 'files', { value: files, configurable: true })
  await input.trigger('change')
  await flushPromises()
}

beforeEach(() => {
  api.put.mockReset()
  api.delete.mockReset()
  api.upload.mockReset()
})

describe('PhotoManager', () => {
  it('cover_marked_on_first', () => {
    const w = make()
    const items = w.findAll('[data-testid="photo"]')
    expect(items).toHaveLength(3)
    expect(items[0].text()).toContain('Kapak')
    expect(items[0].find('.cover').text()).toBe('Kapak')
    expect(items[1].find('.cover').exists()).toBe(false)
    expect(items[0].find('img').attributes('src')).toBe('/photos/0.jpg')
  })
  it('file input accepts only the allowed types and multiple files', () => {
    const w = make()
    const input = w.find('input[type="file"]')
    expect(input.attributes('accept')).toBe('image/jpeg,image/png,image/webp')
    expect(input.attributes('multiple')).toBeDefined()
  })
  it('reorder_sends_full_id_list_and_rolls_back_on_failure', async () => {
    api.put.mockResolvedValueOnce(null).mockRejectedValueOnce({ status: 500 })
    const w = make()
    // 2. fotoğrafı sola taşı → [101, 100, 102]
    await w.findAll('[data-testid="photo"]')[1].find('[data-action="left"]').trigger('click')
    await flushPromises()
    expect(api.put).toHaveBeenLastCalledWith('/api/admin/listings/5/photos/order', [101, 100, 102])
    expect(ids(w)).toEqual([101, 100, 102])
    expect(w.emitted('changed')).toBeUndefined()
    // başarısız: 3. fotoğrafı sağa taşıyamaz, kapak yap dener → hata → eski sıra geri gelir
    await w.findAll('[data-testid="photo"]')[2].find('[data-action="cover"]').trigger('click')
    expect(api.put).toHaveBeenLastCalledWith('/api/admin/listings/5/photos/order', [102, 101, 100])
    await flushPromises()
    expect(ids(w)).toEqual([101, 100, 102])
    expect(w.find('[role="alert"]').text()).toContain('sıralanamadı')
  })
  it('move right and boundaries', async () => {
    api.put.mockResolvedValue(null)
    const w = make()
    const items = () => w.findAll('[data-testid="photo"]')
    expect(items()[0].find('[data-action="left"]').attributes('disabled')).toBeDefined()
    expect(items()[2].find('[data-action="right"]').attributes('disabled')).toBeDefined()
    expect(items()[0].find('[data-action="cover"]').exists()).toBe(false)
    await items()[0].find('[data-action="right"]').trigger('click')
    await flushPromises()
    expect(api.put).toHaveBeenLastCalledWith('/api/admin/listings/5/photos/order', [101, 100, 102])
  })
  it('re-syncs when the parent passes new photos', async () => {
    const w = make(2)
    await w.setProps({ photos: photos(3) })
    expect(ids(w)).toEqual([100, 101, 102])
  })
  it('rejects_wrong_type_and_oversize_before_upload', async () => {
    const w = make()
    await pick(w, [file('a.gif', 'image/gif'), file('b.jpg', 'image/jpeg', 11 * 1024 * 1024), file('c.png', 'image/png')])
    expect(api.upload).not.toHaveBeenCalled()
    const msg = w.find('[role="alert"]').text()
    expect(msg).toContain('a.gif')
    expect(msg).toContain('jpg, png veya webp')
    expect(msg).toContain('b.jpg')
    expect(msg).toContain('10 MB')
  })
  it('rejects_when_over_40', async () => {
    const w = make(39)
    await pick(w, [file('a.jpg', 'image/jpeg'), file('b.jpg', 'image/jpeg')])
    expect(api.upload).not.toHaveBeenCalled()
    expect(w.find('[role="alert"]').text()).toContain('40')
  })
  it('uploads valid files, then asks the parent to refresh', async () => {
    api.upload.mockResolvedValue([{ id: 200, url: '/photos/n.jpg', sortOrder: 3 }])
    const w = make()
    const f = file('a.JPG', 'image/jpeg', 5 * 1024 * 1024)
    await pick(w, [f])
    expect(api.upload).toHaveBeenCalledWith('/api/admin/listings/5/photos', [f])
    expect(w.emitted('changed')).toHaveLength(1)
    expect(w.find('[role="alert"]').exists()).toBe(false)
  })
  it('shows server errors of a 400 upload', async () => {
    api.upload.mockRejectedValue({ status: 400, body: { errors: ['Dosya geçerli bir görüntü değil.'] } })
    const w = make()
    await pick(w, [file('a.jpg', 'image/jpeg')])
    expect(w.find('[role="alert"]').text()).toContain('Dosya geçerli bir görüntü değil.')
    expect(w.emitted('changed')).toBeUndefined()
  })
  it('shows an uploading state while the request runs', async () => {
    let resolve
    api.upload.mockReturnValue(new Promise((r) => { resolve = r }))
    const w = make()
    await pick(w, [file('a.jpg', 'image/jpeg')])
    expect(w.find('[data-testid="uploading"]').exists()).toBe(true)
    expect(w.find('[data-testid="photo-zone"]').attributes('aria-busy')).toBe('true')
    resolve([])
    await flushPromises()
    expect(w.find('[data-testid="uploading"]').exists()).toBe(false)
  })
  it('dropping files on the zone uploads them', async () => {
    api.upload.mockResolvedValue([])
    const w = make()
    const f = file('d.webp', 'image/webp')
    await w.find('[data-testid="photo-zone"]').trigger('drop', { dataTransfer: { files: [f] } })
    await flushPromises()
    expect(api.upload).toHaveBeenCalledWith('/api/admin/listings/5/photos', [f])
  })
  it('delete_asks_confirmation', async () => {
    api.delete.mockResolvedValue(null)
    const w = make()
    await w.findAll('[data-testid="photo"]')[1].find('[data-action="delete"]').trigger('click')
    expect(w.find('[role="dialog"]').exists()).toBe(true)
    expect(api.delete).not.toHaveBeenCalled()
    await w.find('[data-testid="cancel"]').trigger('click')
    expect(api.delete).not.toHaveBeenCalled()
    await w.findAll('[data-testid="photo"]')[1].find('[data-action="delete"]').trigger('click')
    await w.find('[data-testid="confirm"]').trigger('click')
    await flushPromises()
    expect(api.delete).toHaveBeenCalledWith('/api/admin/listings/5/photos/101')
    expect(w.emitted('changed')).toHaveLength(1)
  })
  it('failed delete shows an error and keeps the photo', async () => {
    api.delete.mockRejectedValue({ status: 500 })
    const w = make()
    await w.findAll('[data-testid="photo"]')[0].find('[data-action="delete"]').trigger('click')
    await w.find('[data-testid="confirm"]').trigger('click')
    await flushPromises()
    expect(w.find('[role="alert"]').text()).toContain('silinemedi')
    expect(ids(w)).toEqual([100, 101, 102])
  })
  it('shows an empty hint without photos', () => {
    const w = make(0)
    expect(w.text()).toContain('Henüz fotoğraf yok')
  })
})

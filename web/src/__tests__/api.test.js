import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { api, ApiError, setUnauthorizedHandler } from '../api.js'

function mockFetch(status, body) {
  const fn = vi.fn(async () => ({
    ok: status >= 200 && status < 300,
    status,
    text: async () => (body === undefined ? '' : JSON.stringify(body))
  }))
  vi.stubGlobal('fetch', fn)
  return fn
}

beforeEach(() => {
  localStorage.clear()
  setUnauthorizedHandler(null)
})
afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('api.get', () => {
  it('builds query with repeated keys and skips empty values', async () => {
    const f = mockFetch(200, { items: [] })
    await api.get('/api/listings', { type: 'Satilik', rooms: ['2+1', '3+1'], q: '', city: null, x: undefined, page: 1 })
    expect(f.mock.calls[0][0]).toBe('/api/listings?type=Satilik&rooms=2%2B1&rooms=3%2B1&page=1')
  })
  it('has no ? when no params', async () => {
    const f = mockFetch(200, {})
    await api.get('/api/tags')
    expect(f.mock.calls[0][0]).toBe('/api/tags')
  })
  it('parses JSON', async () => {
    mockFetch(200, { a: 1 })
    expect(await api.get('/x')).toEqual({ a: 1 })
  })
  it('returns null for empty body', async () => {
    mockFetch(204)
    expect(await api.delete('/x')).toBeNull()
  })
})

describe('auth', () => {
  it('sends Bearer token', async () => {
    localStorage.setItem('emlak_token', 'abc')
    const f = mockFetch(200, {})
    await api.get('/api/admin/x')
    expect(f.mock.calls[0][1].headers.Authorization).toBe('Bearer abc')
  })
  it('sends no Authorization without token', async () => {
    const f = mockFetch(200, {})
    await api.get('/x')
    expect(f.mock.calls[0][1].headers.Authorization).toBeUndefined()
  })
  it('works when localStorage throws', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('denied') })
    const f = mockFetch(200, { ok: true })
    expect(await api.get('/x')).toEqual({ ok: true })
    expect(f.mock.calls[0][1].headers.Authorization).toBeUndefined()
  })
  it('401 with token clears session and calls handler', async () => {
    localStorage.setItem('emlak_token', 'abc')
    const h = vi.fn()
    setUnauthorizedHandler(h)
    mockFetch(401, {})
    await expect(api.get('/x')).rejects.toBeInstanceOf(ApiError)
    expect(h).toHaveBeenCalledTimes(1)
    expect(localStorage.getItem('emlak_token')).toBeNull()
  })
  it('401 without token does not call handler (e.g. bad login)', async () => {
    const h = vi.fn()
    setUnauthorizedHandler(h)
    mockFetch(401, { errors: ['x'] })
    await expect(api.post('/api/auth/login', {})).rejects.toMatchObject({ status: 401 })
    expect(h).not.toHaveBeenCalled()
  })
})

describe('errors and bodies', () => {
  it('throws ApiError with status and body on 400', async () => {
    mockFetch(400, { errors: ['bad'] })
    const err = await api.post('/x', { a: 1 }).catch((e) => e)
    expect(err).toBeInstanceOf(ApiError)
    expect(err.status).toBe(400)
    expect(err.body).toEqual({ errors: ['bad'] })
  })
  it('post sends JSON body', async () => {
    const f = mockFetch(200, {})
    await api.post('/x', { a: 1 })
    const init = f.mock.calls[0][1]
    expect(init.method).toBe('POST')
    expect(init.body).toBe('{"a":1}')
    expect(init.headers['Content-Type']).toBe('application/json')
  })
  it('put uses PUT', async () => {
    const f = mockFetch(200, {})
    await api.put('/x', { a: 1 })
    expect(f.mock.calls[0][1].method).toBe('PUT')
  })
  it('upload sends multipart files without JSON content type', async () => {
    const f = mockFetch(200, {})
    const file = new File(['x'], 'a.jpg', { type: 'image/jpeg' })
    await api.upload('/x', [file, file])
    const init = f.mock.calls[0][1]
    expect(init.body).toBeInstanceOf(FormData)
    expect(init.body.getAll('files')).toHaveLength(2)
    expect(init.headers['Content-Type']).toBeUndefined()
  })
})

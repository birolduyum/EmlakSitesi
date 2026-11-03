import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { login, logout, currentUser, isAdmin } from '../auth.js'
import { api } from '../api.js'
import { getToken } from '../session.js'

vi.mock('../api.js', () => ({ api: { post: vi.fn() } }))

beforeEach(() => {
  localStorage.clear()
  api.post.mockReset()
})
afterEach(() => vi.restoreAllMocks())

describe('auth', () => {
  it('login_stores_token_and_role', async () => {
    api.post.mockResolvedValue({ token: 'jwt-1', username: 'ayse', role: 'Admin' })
    const user = await login('ayse', 'gizli')
    expect(api.post).toHaveBeenCalledWith('/api/auth/login', { username: 'ayse', password: 'gizli' })
    expect(user).toEqual({ username: 'ayse', role: 'Admin' })
    expect(getToken()).toBe('jwt-1')
    expect(currentUser()).toEqual({ username: 'ayse', role: 'Admin' })
    expect(isAdmin()).toBe(true)
  })
  it('never stores the password', async () => {
    api.post.mockResolvedValue({ token: 'jwt-1', username: 'ayse', role: 'Admin' })
    await login('ayse', 'cok-gizli-parola')
    const all = JSON.stringify({ ...localStorage })
    expect(all).not.toContain('cok-gizli-parola')
  })
  it('field user is not admin', async () => {
    api.post.mockResolvedValue({ token: 't', username: 'saha', role: 'Field' })
    await login('saha', 'x')
    expect(isAdmin()).toBe(false)
    expect(currentUser().role).toBe('Field')
  })
  it('logout_clears_storage', async () => {
    api.post.mockResolvedValue({ token: 'jwt-1', username: 'ayse', role: 'Admin' })
    await login('ayse', 'x')
    logout()
    expect(getToken()).toBeNull()
    expect(currentUser()).toBeNull()
    expect(isAdmin()).toBe(false)
    expect(localStorage.length).toBe(0)
  })
  it('works_when_localStorage_throws', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    api.post.mockResolvedValue({ token: 't', username: 'a', role: 'Admin' })
    await expect(login('a', 'b')).resolves.toEqual({ username: 'a', role: 'Admin' })
    expect(currentUser()).toBeNull()
    expect(isAdmin()).toBe(false)
    expect(() => logout()).not.toThrow()
  })
  it('login_failure_does_not_store_anything', async () => {
    api.post.mockRejectedValue(Object.assign(new Error('HTTP 401'), { status: 401 }))
    await expect(login('a', 'yanlis')).rejects.toMatchObject({ status: 401 })
    expect(localStorage.length).toBe(0)
    expect(currentUser()).toBeNull()
  })
})

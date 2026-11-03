import { describe, it, expect, beforeEach } from 'vitest'
import { createMemoryHistory } from 'vue-router'
import { createAppRouter } from '../router.js'
import { setSession, clearSession } from '../session.js'

async function go(path) {
  const r = createAppRouter(createMemoryHistory())
  await r.push(path)
  await r.isReady()
  return r.currentRoute.value.fullPath
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

describe('router guard', () => {
  it('public pages open without token', async () => {
    expect(await go('/')).toBe('/')
    expect(await go('/ilan/5')).toBe('/ilan/5')
    expect(await go('/ilan/5/fotograflar')).toBe('/ilan/5/fotograflar')
  })
  it('login page opens without token', async () => {
    expect(await go('/admin/giris')).toBe('/admin/giris')
  })
  it('admin pages redirect to login without token', async () => {
    expect(await go('/admin')).toBe('/admin/giris')
    expect(await go('/admin/ilan/yeni')).toBe('/admin/giris')
    expect(await go('/admin/gosterim')).toBe('/admin/giris')
  })
  it('field user reaches /admin but not rapor/kur', async () => {
    setSession('t', { username: 'saha', role: 'Field' })
    expect(await go('/admin')).toBe('/admin')
    expect(await go('/admin/gosterim')).toBe('/admin/gosterim')
    expect(await go('/admin/rapor')).toBe('/admin')
    expect(await go('/admin/kur')).toBe('/admin')
  })
  it('admin user reaches rapor and kur', async () => {
    setSession('t', { username: 'a', role: 'Admin' })
    expect(await go('/admin/rapor')).toBe('/admin/rapor')
    expect(await go('/admin/kur')).toBe('/admin/kur')
  })
  it('corrupt stored user is treated as no role', async () => {
    localStorage.setItem('emlak_token', 't')
    localStorage.setItem('emlak_user', '{bad json')
    expect(await go('/admin')).toBe('/admin')
    expect(await go('/admin/rapor')).toBe('/admin')
  })
  it('unknown path goes to /', async () => {
    expect(await go('/nope/nothing')).toBe('/')
  })
})

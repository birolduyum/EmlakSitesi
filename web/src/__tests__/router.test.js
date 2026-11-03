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
    expect(decodeURIComponent(await go('/admin/ilan/yeni'))).toBe('/admin/giris?redirect=/admin/ilan/yeni')
    expect(decodeURIComponent(await go('/admin/gosterim'))).toBe('/admin/giris?redirect=/admin/gosterim')
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
  it('redirect_query_keeps_the_requested_admin_path', async () => {
    expect(decodeURIComponent(await go('/admin/ilan/7?x=1'))).toBe('/admin/giris?redirect=/admin/ilan/7?x=1')
    // varsayılan hedefte gereksiz sorgu yok
    expect(await go('/admin')).toBe('/admin/giris')
  })
  it('logged_in_user_at_login_page_goes_to_admin', async () => {
    setSession('t', { username: 'a', role: 'Admin' })
    expect(await go('/admin/giris')).toBe('/admin')
  })
  it('field_blocked_from_new_and_edit_listing_form', async () => {
    setSession('t', { username: 'saha', role: 'Field' })
    expect(await go('/admin/ilan/yeni')).toBe('/admin')
    expect(await go('/admin/ilan/5')).toBe('/admin')
  })
  it('admin_reaches_listing_form', async () => {
    setSession('t', { username: 'a', role: 'Admin' })
    expect(await go('/admin/ilan/yeni')).toBe('/admin/ilan/yeni')
    expect(await go('/admin/ilan/5')).toBe('/admin/ilan/5')
  })
  it('unknown path goes to /', async () => {
    expect(await go('/nope/nothing')).toBe('/')
  })
})

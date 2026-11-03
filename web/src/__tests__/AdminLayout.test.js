import { describe, it, expect, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import AdminLayout from '../components/admin/AdminLayout.vue'
import { setSession, clearSession, getToken } from '../session.js'

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

async function make(role) {
  setSession('t', { username: 'ayse', role })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin/giris', component: { template: '<p>giris</p>' } },
      { path: '/admin', component: AdminLayout, children: [{ path: '', component: { template: '<p>liste</p>' } }] },
      ...['gosterim', 'rapor', 'danismanlar', 'kur'].map((p) => ({ path: `/admin/${p}`, component: { template: '<p>x</p>' } }))
    ]
  })
  router.push('/admin')
  await router.isReady()
  const w = mount({ template: '<router-view />' }, { global: { plugins: [router] } })
  await flushPromises()
  return { w, router }
}

describe('AdminLayout', () => {
  it('admin sees all menu items, user name and role label', async () => {
    const { w } = await make('Admin')
    const menu = w.find('nav[aria-label="Yönetim menüsü"]').text()
    for (const item of ['İlanlar', 'Gösterme', 'Rapor', 'Danışmanlar', 'Kur']) expect(menu).toContain(item)
    expect(w.find('[data-testid="who"]').text()).toBe('ayse (Yönetici)')
    expect(w.text()).toContain('liste')
  })
  it('field user does not get Rapor and Kur', async () => {
    const { w } = await make('Field')
    const menu = w.find('nav[aria-label="Yönetim menüsü"]').text()
    expect(menu).toContain('İlanlar')
    expect(menu).toContain('Gösterme')
    expect(menu).not.toContain('Rapor')
    expect(menu).not.toContain('Danışmanlar')
    expect(menu).not.toContain('Kur')
    expect(w.find('[data-testid="who"]').text()).toBe('ayse (Saha)')
  })
  it('logout clears the session and goes to the login page', async () => {
    const { w, router } = await make('Admin')
    await w.find('[data-testid="logout"]').trigger('click')
    await flushPromises()
    expect(getToken()).toBeNull()
    expect(router.currentRoute.value.path).toBe('/admin/giris')
  })
})

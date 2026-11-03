import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import LoginView from '../views/admin/LoginView.vue'
import { api } from '../api.js'

vi.mock('../api.js', () => ({ api: { post: vi.fn() } }))

beforeEach(() => {
  localStorage.clear()
  api.post.mockReset()
})

async function make(url = '/admin/giris') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin/giris', component: LoginView },
      { path: '/admin', component: { template: '<i/>' } },
      { path: '/admin/ilan/:id', component: { template: '<i/>' } }
    ]
  })
  router.push(url)
  await router.isReady()
  const w = mount(LoginView, { global: { plugins: [router] } })
  return { w, router }
}
async function fill(w, u = 'ayse', p = 'sifre') {
  await w.find('input[name="username"]').setValue(u)
  await w.find('input[name="password"]').setValue(p)
}

describe('LoginView', () => {
  it('has labelled fields with autocomplete hints', async () => {
    const { w } = await make()
    expect(w.find('label[for="login-username"]').exists()).toBe(true)
    expect(w.find('label[for="login-password"]').exists()).toBe(true)
    expect(w.find('input[name="username"]').attributes('autocomplete')).toBe('username')
    expect(w.find('input[name="password"]').attributes('autocomplete')).toBe('current-password')
    expect(w.find('input[name="password"]').attributes('type')).toBe('password')
  })
  it('generic_error_on_401', async () => {
    api.post.mockRejectedValue({ status: 401, body: { error: 'Kullanıcı adı veya parola hatalı.' } })
    const { w } = await make()
    await fill(w)
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(w.find('[role="alert"]').text()).toBe('Kullanıcı adı veya parola hatalı')
    expect(w.find('button[type="submit"]').attributes('disabled')).toBeUndefined()
  })
  it('network error shows server unreachable', async () => {
    api.post.mockRejectedValue({ status: 0 })
    const { w } = await make()
    await fill(w)
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(w.find('[role="alert"]').text()).toBe('Sunucuya ulaşılamadı')
  })
  it('redirects_to_admin_after_success', async () => {
    api.post.mockResolvedValue({ token: 't', username: 'ayse', role: 'Admin' })
    const { w, router } = await make()
    await fill(w)
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/admin')
    expect(router.currentRoute.value.query).toEqual({})
    expect(router.currentRoute.value.fullPath).not.toContain('sifre')
  })
  it('redirects to the originally requested admin path', async () => {
    api.post.mockResolvedValue({ token: 't', username: 'ayse', role: 'Admin' })
    const { w, router } = await make('/admin/giris?redirect=%2Fadmin%2Filan%2F5')
    await fill(w)
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/admin/ilan/5')
  })
  it('ignores a redirect that leaves the admin area', async () => {
    api.post.mockResolvedValue({ token: 't', username: 'ayse', role: 'Admin' })
    const { w, router } = await make('/admin/giris?redirect=%2F%2Fevil.example')
    await fill(w)
    await w.find('form').trigger('submit')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/admin')
  })
  it('pending_disables_button_and_blocks_double_submit', async () => {
    let resolve
    api.post.mockReturnValue(
      new Promise((r) => {
        resolve = r
      })
    )
    const { w } = await make()
    await fill(w)
    await w.find('form').trigger('submit')
    expect(w.find('button[type="submit"]').attributes('disabled')).toBeDefined()
    await w.find('form').trigger('submit')
    expect(api.post).toHaveBeenCalledTimes(1)
    resolve({ token: 't', username: 'a', role: 'Admin' })
    await flushPromises()
  })
  it('empty fields do not call the API', async () => {
    const { w } = await make()
    await w.find('form').trigger('submit')
    expect(api.post).not.toHaveBeenCalled()
    expect(w.find('[role="alert"]').exists()).toBe(true)
  })
})

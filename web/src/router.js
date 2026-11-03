import { createRouter, createWebHistory } from 'vue-router'
import { getToken, getUser } from './session.js'

const routes = [
  { path: '/', component: () => import('./views/ListingsView.vue') },
  { path: '/ilan/:id', component: () => import('./views/DetailView.vue') },
  { path: '/ilan/:id/fotograflar', component: () => import('./views/GalleryView.vue') },
  { path: '/admin/giris', component: () => import('./views/admin/LoginView.vue') },
  {
    // Giriş dışındaki bütün yönetim sayfaları ortak çerçevede (üst çubuk + menü) açılır.
    path: '/admin',
    component: () => import('./components/admin/AdminLayout.vue'),
    children: [
      { path: '', component: () => import('./views/admin/AdminListView.vue') },
      { path: 'ilan/yeni', component: () => import('./views/admin/ListingFormView.vue'), meta: { role: 'Admin' } },
      { path: 'ilan/:id', component: () => import('./views/admin/ListingFormView.vue'), meta: { role: 'Admin' } },
      { path: 'gosterim', component: () => import('./views/admin/ShowingView.vue') },
      { path: 'rapor', component: () => import('./views/admin/ReportView.vue'), meta: { role: 'Admin' } },
      { path: 'danismanlar', component: () => import('./views/admin/AdvisorsView.vue'), meta: { role: 'Admin' } },
      { path: 'kur', component: () => import('./views/admin/RatesView.vue'), meta: { role: 'Admin' } }
    ]
  },
  { path: '/:pathMatch(.*)*', redirect: '/' }
]

export function guard(to) {
  const isAdminArea = to.path === '/admin' || to.path.startsWith('/admin/')
  if (!isAdminArea) return true
  if (to.path === '/admin/giris') return getToken() ? '/admin' : true
  if (!getToken()) {
    // İstenen sayfa girişten sonra açılsın diye sorguda saklanır (varsayılan hedefte gerekmez).
    return to.fullPath === '/admin' ? '/admin/giris' : { path: '/admin/giris', query: { redirect: to.fullPath } }
  }
  const required = to.meta.role
  if (required && getUser()?.role !== required) return '/admin'
  return true
}

export function createAppRouter(history = createWebHistory()) {
  const router = createRouter({ history, routes })
  router.beforeEach(guard)
  return router
}

export default createAppRouter()

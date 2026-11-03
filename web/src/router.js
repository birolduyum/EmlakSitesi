import { createRouter, createWebHistory } from 'vue-router'
import { getToken, getUser } from './session.js'

const routes = [
  { path: '/', component: () => import('./views/ListingsView.vue') },
  { path: '/ilan/:id', component: () => import('./views/DetailView.vue') },
  { path: '/ilan/:id/fotograflar', component: () => import('./views/GalleryView.vue') },
  { path: '/admin/giris', component: () => import('./views/admin/LoginView.vue') },
  { path: '/admin', component: () => import('./views/admin/AdminListView.vue') },
  { path: '/admin/ilan/yeni', component: () => import('./views/admin/ListingFormView.vue') },
  { path: '/admin/ilan/:id', component: () => import('./views/admin/ListingFormView.vue') },
  { path: '/admin/gosterim', component: () => import('./views/admin/ShowingView.vue') },
  { path: '/admin/rapor', component: () => import('./views/admin/ReportView.vue'), meta: { role: 'Admin' } },
  { path: '/admin/kur', component: () => import('./views/admin/RatesView.vue'), meta: { role: 'Admin' } },
  { path: '/:pathMatch(.*)*', redirect: '/' }
]

export function guard(to) {
  const isAdminArea = to.path === '/admin' || to.path.startsWith('/admin/')
  if (!isAdminArea || to.path === '/admin/giris') return true
  if (!getToken()) return '/admin/giris'
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

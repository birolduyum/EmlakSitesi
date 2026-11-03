import { api } from './api.js'
import { getToken, getUser, setSession, clearSession } from './session.js'

// Giriş: token ve kullanıcı (ad + rol) session.js üzerinden saklanır; parola hiçbir yerde tutulmaz.
export async function login(username, password) {
  const data = await api.post('/api/auth/login', { username, password })
  const user = { username: data.username, role: data.role }
  setSession(data.token, user)
  return user
}

export function logout() {
  clearSession()
}

export function currentUser() {
  const user = getUser()
  if (!getToken() || !user || typeof user.username !== 'string' || typeof user.role !== 'string') return null
  return { username: user.username, role: user.role }
}

export function isAdmin() {
  return currentUser()?.role === 'Admin'
}

// Açık çıkış sürerken sayfaların "kaydedilmemiş değişiklik" korumaları devre dışı kalır:
// çıkış yapan kullanıcı her zaman girişe gider (kaydedilmemiş veri kaybolur).
let loggingOut = false
export function isLoggingOut() {
  return loggingOut
}
export async function signOut(router) {
  loggingOut = true
  try {
    logout()
    await router.push('/admin/giris')
  } finally {
    loggingOut = false
  }
}

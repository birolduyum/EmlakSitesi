// Oturum bilgisi (token + kullanıcı) localStorage'da tutulur.
// localStorage erişimi her yerde try/catch içindedir (özel pencere, engelli depolama).
const TOKEN_KEY = 'emlak_token'
const USER_KEY = 'emlak_user'

export function getToken() {
  try {
    return localStorage.getItem(TOKEN_KEY) || null
  } catch {
    return null
  }
}

export function getUser() {
  try {
    const raw = localStorage.getItem(USER_KEY)
    if (!raw) return null
    const user = JSON.parse(raw)
    return user && typeof user === 'object' ? user : null
  } catch {
    return null
  }
}

export function setSession(token, user) {
  try {
    localStorage.setItem(TOKEN_KEY, token)
    localStorage.setItem(USER_KEY, JSON.stringify(user ?? null))
  } catch {
    // depolama kullanılamıyor; oturum kalıcı olmaz
  }
}

export function clearSession() {
  try {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  } catch {
    // yok sayılır
  }
}

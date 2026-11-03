import { getToken, clearSession } from './session.js'

export class ApiError extends Error {
  constructor(status, body) {
    super(`HTTP ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.body = body
  }
}

let unauthorizedHandler = null

export function setUnauthorizedHandler(fn) {
  unauthorizedHandler = fn
}

function buildQuery(params) {
  if (!params) return ''
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    const values = Array.isArray(value) ? value : [value]
    for (const v of values) {
      if (v === null || v === undefined || v === '') continue
      search.append(key, String(v))
    }
  }
  const qs = search.toString()
  return qs ? `?${qs}` : ''
}

async function request(method, path, { params, body, form } = {}) {
  const token = getToken()
  const headers = {}
  if (token) headers.Authorization = `Bearer ${token}`

  const init = { method, headers }
  if (form) {
    init.body = form
  } else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    init.body = JSON.stringify(body)
  }

  const res = await fetch(path + buildQuery(params), init)

  const text = await res.text()
  let data = null
  if (text) {
    try {
      data = JSON.parse(text)
    } catch {
      data = text
    }
  }

  if (!res.ok) {
    if (res.status === 401 && token) {
      clearSession()
      if (unauthorizedHandler) unauthorizedHandler()
    }
    throw new ApiError(res.status, data)
  }
  return data
}

export const api = {
  get: (path, params) => request('GET', path, { params }),
  post: (path, body) => request('POST', path, { body }),
  put: (path, body) => request('PUT', path, { body }),
  delete: (path, body) => request('DELETE', path, { body }),
  upload: (path, files) => {
    const form = new FormData()
    for (const f of files) form.append('files', f)
    return request('POST', path, { form })
  }
}

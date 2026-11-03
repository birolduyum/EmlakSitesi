import { heatingLabels, furnishingLabels, currencyLabels, sortLabels, typeLabels, statusLabels } from './labels.js'

export const PAGE_SIZE = 20

const STRING_KEYS = ['city', 'district', 'neighborhood', 'text']
const DECIMAL_KEYS = ['minPriceTry', 'maxPriceTry']
const INT_KEYS = ['minM2', 'maxM2', 'maxBuildingAge', 'floor']
const BOOL_KEYS = ['balcony', 'elevator', 'parking', 'inSite', 'loanEligible', 'swap']

function first(v) {
  return Array.isArray(v) ? v[0] : v
}

// Boş, sayı olmayan ya da negatif girdi → undefined. (Number('') === 0 tuzağına karşı açıkça elenir.)
export function parseNumber(value, { integer = false } = {}) {
  const v = first(value)
  if (v === null || v === undefined || typeof v === 'boolean') return undefined
  const s = String(v).trim()
  if (s === '') return undefined
  const n = Number(s)
  if (!Number.isFinite(n) || n < 0) return undefined
  return integer ? Math.floor(n) : n
}

function parseText(value) {
  const v = first(value)
  if (v === null || v === undefined) return undefined
  const s = String(v).trim()
  return s === '' ? undefined : s
}

function parseEnum(value, map) {
  const v = parseText(value)
  return v !== undefined && Object.prototype.hasOwnProperty.call(map, v) ? v : undefined
}

function parseRooms(value) {
  if (value === null || value === undefined) return undefined
  const list = (Array.isArray(value) ? value : [value])
    .map((r) => parseText(r))
    .filter((r) => r !== undefined)
  return list.length ? list : undefined
}

// Ham değerleri (URL dizgeleri ya da yazılı değerler) API parametrelerine çevirir; bilinmeyen anahtarlar düşer.
function normalize(raw, admin = false) {
  const out = {}
  // Ziyaretçi listesinde tür her zaman vardır (varsayılan Satılık); yönetici listesinde yoksa iki tür birden gelir.
  const type = parseEnum(raw.type, typeLabels)
  if (type) out.type = type
  else if (!admin) out.type = 'Satilik'
  const page = parseNumber(raw.page, { integer: true })
  out.page = page && page >= 1 ? page : 1
  out.pageSize = PAGE_SIZE

  for (const k of STRING_KEYS) {
    const v = parseText(raw[k])
    if (v !== undefined) out[k] = v
  }
  const currency = parseEnum(raw.currency, currencyLabels)
  if (currency) out.currency = currency
  for (const k of DECIMAL_KEYS) {
    const v = parseNumber(raw[k])
    if (v !== undefined) out[k] = v
  }
  for (const k of INT_KEYS) {
    const v = parseNumber(raw[k], { integer: true })
    if (v !== undefined) out[k] = v
  }
  const rooms = parseRooms(raw.rooms)
  if (rooms) out.rooms = rooms
  const heating = parseEnum(raw.heating, heatingLabels)
  if (heating) out.heating = heating
  const furnishing = parseEnum(raw.furnishing, furnishingLabels)
  if (furnishing) out.furnishing = furnishing
  for (const k of BOOL_KEYS) {
    const v = first(raw[k])
    if (v === true || v === 'true') out[k] = true
  }
  const sort = parseEnum(raw.sort, sortLabels)
  if (sort && sort !== 'Newest') out.sort = sort
  if (admin) {
    const status = parseEnum(raw.status, statusLabels)
    if (status && status !== 'Active') out.status = status
    const ownerText = parseText(raw.ownerText)
    if (ownerText !== undefined) out.ownerText = ownerText
  }
  return out
}

export function routeQueryToParams(query) {
  return normalize(query ?? {})
}

// API parametreleri → URL sorgusu (yalnızca varsayılandan farklı olanlar yazılır).
export function paramsToRouteQuery(params) {
  return toRouteQuery(normalize(params ?? {}), false)
}

function toRouteQuery(p, admin) {
  const q = {}
  for (const [key, value] of Object.entries(p)) {
    if (key === 'pageSize') continue
    if (!admin && key === 'type' && value === 'Satilik') continue
    if (key === 'page' && value === 1) continue
    q[key] = Array.isArray(value) ? value.slice() : String(value)
  }
  return q
}

// Yönetici listesi: tür yoksa iki tür birden; ek süzgeçler status (Aktif/Silinenler/Hepsi) ve ownerText.
export function adminRouteQueryToParams(query) {
  return normalize(query ?? {}, true)
}

export function adminParamsToRouteQuery(params) {
  return toRouteQuery(normalize(params ?? {}, true), true)
}

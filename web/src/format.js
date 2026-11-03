const numberFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 0 })

const SYMBOLS = { TRY: '₺', USD: '$', EUR: '€' }
const MISSING = '-'

// null/undefined/boş/sayı olmayan girdi geçersizdir (Number(null) === 0 tuzağına karşı açıkça elenir).
function toNumber(value) {
  if (value === null || value === undefined || typeof value === 'boolean') return null
  if (typeof value === 'string' && value.trim() === '') return null
  const n = Number(value)
  return Number.isFinite(n) ? n : null
}

export function formatPrice(price, currency, type) {
  const n = toNumber(price)
  if (n === null) return MISSING
  const symbol = SYMBOLS[currency] ?? currency ?? ''
  const text = `${numberFormat.format(n)} ${symbol}`.trim()
  return type === 'Kiralik' ? `${text} / ay` : text
}

export function formatArea(m2) {
  const n = toNumber(m2)
  return n === null ? MISSING : `${numberFormat.format(n)} m²`
}

// Görüntüleyenin yerel saatine göre gün.ay.yıl
export function formatDate(iso) {
  if (iso === null || iso === undefined || iso === '') return MISSING
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return MISSING
  const dd = String(d.getDate()).padStart(2, '0')
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  return `${dd}.${mm}.${d.getFullYear()}`
}

export function formatFloor(floor) {
  const n = toNumber(floor)
  if (n === null) return MISSING
  if (n < 0) return 'Bodrum Kat'
  if (n === 0) return 'Giriş Kat'
  return `${n}. Kat`
}

export function specLine(listing) {
  const l = listing ?? {}
  const rooms = typeof l.rooms === 'string' && l.rooms.trim() ? l.rooms : MISSING
  return [rooms, formatArea(l.grossM2), formatFloor(l.floor), formatDate(l.createdAt)].join(' · ')
}

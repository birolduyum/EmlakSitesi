const numberFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 0 })

const SYMBOLS = { TRY: '₺', USD: '$', EUR: '€' }

export function formatPrice(price, currency, type) {
  const symbol = SYMBOLS[currency] ?? currency
  const text = `${numberFormat.format(Number(price))} ${symbol}`
  return type === 'Kiralik' ? `${text} / ay` : text
}

export function formatArea(m2) {
  return `${numberFormat.format(Number(m2))} m²`
}

// Görüntüleyenin yerel saatine göre gün.ay.yıl
export function formatDate(iso) {
  const d = new Date(iso)
  const dd = String(d.getDate()).padStart(2, '0')
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  return `${dd}.${mm}.${d.getFullYear()}`
}

export function formatFloor(floor) {
  if (floor < 0) return 'Bodrum Kat'
  if (floor === 0) return 'Giriş Kat'
  return `${floor}. Kat`
}

export function specLine(listing) {
  return [
    listing.rooms,
    formatArea(listing.grossM2),
    formatFloor(listing.floor),
    formatDate(listing.createdAt)
  ].join(' · ')
}

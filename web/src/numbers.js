// Kullanıcının yazdığı ondalık sayıyı çözer: "41,25", "41.25", "1.234,5", "1,234.5" hepsi geçerlidir.
// Kural: SON ayırıcı (nokta ya da virgül) ondalıktır, öncekiler binlik ayırıcıdır.
// Tek tür ayırıcı birden çok kez geçiyorsa ("1.234.567") hepsi binliktir. Geçersiz/boş/negatif → null.
export function parseDecimal(value) {
  if (typeof value === 'number') return Number.isFinite(value) && value >= 0 ? value : null
  if (typeof value !== 'string') return null
  const s = value.trim().replace(/\s+/g, '')
  if (s === '' || !/^[0-9.,]+$/.test(s) || !/[0-9]/.test(s)) return null

  const last = Math.max(s.lastIndexOf('.'), s.lastIndexOf(','))
  if (last === -1) return Number(s)

  const sep = s[last]
  const other = sep === '.' ? ',' : '.'
  const head = s.slice(0, last)
  const tail = s.slice(last + 1)
  const sepCount = s.split(sep).length - 1

  let integerPart
  let fraction = ''
  if (sepCount > 1 && !head.includes(other)) {
    // "1.234.567": aynı ayırıcı tekrarlanıyor, hepsi binlik
    const parts = s.split(sep)
    if (parts.some((p) => p === '')) return null
    integerPart = parts.join('')
  } else {
    // Son ayırıcı ondalık; öncekilerde yalnızca diğer tür (binlik) ayırıcı olabilir.
    if (head.includes(sep)) return null
    const groups = head.split(other)
    if (head !== '' && groups.some((g) => g === '')) return null
    integerPart = groups.join('')
    fraction = tail
  }
  if (integerPart === '' && fraction === '') return null
  const n = Number(`${integerPart || '0'}${fraction ? `.${fraction}` : ''}`)
  return Number.isFinite(n) ? n : null
}

// ---- Türkçe para/sayı kuralı (ilan formu, filtreler) ----
// Burada nokta YALNIZCA binlik ayırıcıdır, virgül ondalık ayırıcıdır: "5.000.000" → 5000000, "1.250" → 1250,
// "1.250,50" → 1250.5, "1,5" → 1.5. (Yukarıdaki parseDecimal kur ekranı içindir ve "son ayırıcı ondalıktır" kuralını korur.)
// Geçersiz, boş, eksi işaretli (allowNegative olmadıkça) ya da yanlış gruplanmış ("1.25", "12.34.567") girdi → null.
const TR_INTEGER = /^(?:\d{1,3}(?:\.\d{3})+|\d+)$/
const TR_DECIMAL = /^(?:\d{1,3}(?:\.\d{3})+|\d+)(?:,\d+)?$/

function parseTr(value, pattern, { allowNegative = false, integer = false } = {}) {
  if (typeof value === 'number') {
    if (!Number.isFinite(value) || (integer && !Number.isInteger(value))) return null
    return value >= 0 || allowNegative ? value : null
  }
  if (typeof value !== 'string') return null
  let s = value.trim()
  let negative = false
  if (allowNegative && s.startsWith('-')) {
    negative = true
    s = s.slice(1)
  }
  if (!pattern.test(s)) return null
  const n = Number(s.replace(/\./g, '').replace(',', '.'))
  if (!Number.isFinite(n) || Math.abs(n) > Number.MAX_SAFE_INTEGER) return null
  return negative ? -n : n
}

export function parseTrInteger(value, options) {
  return parseTr(value, TR_INTEGER, { ...options, integer: true })
}

export function parseTrDecimal(value, options) {
  return parseTr(value, TR_DECIMAL, options)
}

// Sayıyı yazı kutusuna konacak biçime çevirir (binliksiz, virgüllü ondalık): 1250.5 → "1250,5".
export function toTrInput(value) {
  if (value === null || value === undefined || value === '') return ''
  const n = Number(value)
  return Number.isFinite(n) ? String(n).replace('.', ',') : String(value)
}

export const INVALID_NUMBER_MESSAGE = 'Geçerli bir sayı girin'

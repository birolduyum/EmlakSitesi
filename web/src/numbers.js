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

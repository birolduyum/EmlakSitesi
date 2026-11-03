import { describe, it, expect } from 'vitest'
import { formatPrice, formatArea, formatDate, formatFloor, specLine } from '../format.js'

// Saat dilimine bağlı olmasın: tarih yerel parçalardan kurulur
const localIso = (y, m, d) => new Date(y, m - 1, d, 12, 0, 0).toISOString()

describe('formatPrice', () => {
  it('formats TRY sale', () => expect(formatPrice(22000000, 'TRY', 'Satilik')).toBe('22.000.000 ₺'))
  it('formats USD', () => expect(formatPrice(450000, 'USD', 'Satilik')).toBe('450.000 $'))
  it('formats EUR', () => expect(formatPrice(1500000, 'EUR', 'Satilik')).toBe('1.500.000 €'))
  it('adds / ay for rentals', () => expect(formatPrice(15000, 'TRY', 'Kiralik')).toBe('15.000 ₺ / ay'))
  it('shows no decimals for whole decimals', () => expect(formatPrice(22000000.0, 'TRY', 'Satilik')).toBe('22.000.000 ₺'))
  it('rounds non-zero decimals', () => expect(formatPrice(1234.56, 'TRY', 'Satilik')).toBe('1.235 ₺'))
  it.each([null, undefined, NaN, '', 'abc'])('prints - for invalid price %s', (v) =>
    expect(formatPrice(v, 'TRY', 'Satilik')).toBe('-'))
  it('prints - for invalid rental price, never "- / ay"', () =>
    expect(formatPrice(null, 'TRY', 'Kiralik')).toBe('-'))
  it('never prints undefined for a missing currency', () =>
    expect(formatPrice(1500, undefined, 'Satilik')).toBe('1.500'))
})

describe('formatArea', () => {
  it('appends m²', () => expect(formatArea(220)).toBe('220 m²'))
  it.each([null, undefined, NaN, ''])('prints - for %s', (v) => expect(formatArea(v)).toBe('-'))
})

describe('formatDate', () => {
  it('formats day.month.year', () => expect(formatDate(localIso(2026, 8, 23))).toBe('23.08.2026'))
  it.each([null, undefined, '', 'not-a-date'])('prints - for %s', (v) => expect(formatDate(v)).toBe('-'))
})

describe('formatFloor', () => {
  it('formats floors', () => {
    expect(formatFloor(3)).toBe('3. Kat')
    expect(formatFloor(0)).toBe('Giriş Kat')
    expect(formatFloor(-2)).toBe('Bodrum Kat')
  })
  it.each([null, undefined, NaN, ''])('prints - for %s', (v) => expect(formatFloor(v)).toBe('-'))
})

describe('specLine', () => {
  const base = { rooms: '3+2', grossM2: 220, createdAt: localIso(2026, 8, 23) }
  it('builds the full line', () =>
    expect(specLine({ ...base, floor: 2 })).toBe('3+2 · 220 m² · 2. Kat · 23.08.2026'))
  it('negative floor is Bodrum Kat', () =>
    expect(specLine({ ...base, floor: -1 })).toBe('3+2 · 220 m² · Bodrum Kat · 23.08.2026'))
  it('zero floor is Giriş Kat', () =>
    expect(specLine({ ...base, floor: 0 })).toBe('3+2 · 220 m² · Giriş Kat · 23.08.2026'))
  it('uses - for missing pieces and never prints NaN/undefined', () => {
    const line = specLine({ rooms: null })
    expect(line).toBe('- · - · - · -')
    expect(line).not.toMatch(/NaN|undefined|null/)
  })
})

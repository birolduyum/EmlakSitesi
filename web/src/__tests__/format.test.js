import { describe, it, expect } from 'vitest'
import { formatPrice, formatArea, formatDate, specLine } from '../format.js'

describe('formatPrice', () => {
  it('formats TRY sale', () => expect(formatPrice(22000000, 'TRY', 'Satilik')).toBe('22.000.000 ₺'))
  it('formats USD', () => expect(formatPrice(450000, 'USD', 'Satilik')).toBe('450.000 $'))
  it('formats EUR', () => expect(formatPrice(1500000, 'EUR', 'Satilik')).toBe('1.500.000 €'))
  it('adds / ay for rentals', () => expect(formatPrice(15000, 'TRY', 'Kiralik')).toBe('15.000 ₺ / ay'))
  it('shows no decimals for whole decimals', () => expect(formatPrice(22000000.0, 'TRY', 'Satilik')).toBe('22.000.000 ₺'))
  it('rounds non-zero decimals', () => expect(formatPrice(1234.56, 'TRY', 'Satilik')).toBe('1.235 ₺'))
})

describe('formatArea', () => {
  it('appends m²', () => expect(formatArea(220)).toBe('220 m²'))
})

describe('formatDate', () => {
  it('formats day.month.year', () => expect(formatDate('2026-08-23T12:00:00Z')).toBe('23.08.2026'))
})

describe('specLine', () => {
  const base = { rooms: '3+2', grossM2: 220, createdAt: '2026-08-23T12:00:00Z' }
  it('builds the full line', () =>
    expect(specLine({ ...base, floor: 2 })).toBe('3+2 · 220 m² · 2. Kat · 23.08.2026'))
  it('negative floor is Bodrum Kat', () =>
    expect(specLine({ ...base, floor: -1 })).toBe('3+2 · 220 m² · Bodrum Kat · 23.08.2026'))
  it('zero floor is Giriş Kat', () =>
    expect(specLine({ ...base, floor: 0 })).toBe('3+2 · 220 m² · Giriş Kat · 23.08.2026'))
})

import { describe, it, expect } from 'vitest'
import { parseDecimal } from '../numbers.js'
import { formatDateTime } from '../format.js'

describe('parseDecimal', () => {
  it.each([
    ['41,25', 41.25],
    ['41.25', 41.25],
    ['41', 41],
    ['  41,5  ', 41.5],
    ['1.234,5', 1234.5],
    ['1,234.5', 1234.5],
    ['1.234.567', 1234567],
    ['1,234,567', 1234567],
    ['0,5', 0.5],
    ['41,', 41]
  ])('parses %s as %s', (input, expected) => expect(parseDecimal(input)).toBe(expected))

  it.each(['', '   ', null, undefined, 'abc', '-5', '-0,5', '4a', ',', '.', '1,2,3,4x', '1..2', '+5', '1e3'])(
    'returns null for %s',
    (input) => expect(parseDecimal(input)).toBeNull()
  )
  it('accepts numbers', () => {
    expect(parseDecimal(41.25)).toBe(41.25)
    expect(parseDecimal(NaN)).toBeNull()
    expect(parseDecimal(-1)).toBeNull()
  })
})

describe('formatDateTime', () => {
  it('formats local dd.MM.yyyy HH:mm', () => {
    expect(formatDateTime(new Date(2026, 10, 2, 14, 30).toISOString())).toBe('02.11.2026 14:30')
    expect(formatDateTime(new Date(2026, 0, 5, 9, 5).toISOString())).toBe('05.01.2026 09:05')
  })
  it.each([null, undefined, '', 'bozuk'])('prints - for %s', (v) => expect(formatDateTime(v)).toBe('-'))
})

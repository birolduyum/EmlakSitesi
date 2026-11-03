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

import { parseTrInteger, parseTrDecimal, toTrInput } from '../numbers.js'

describe('Turkish money convention (dots = thousands only, comma = decimal)', () => {
  it.each([
    ['5.000.000', 5000000],
    ['1.250', 1250],
    ['1.250,50', 1250.5],
    ['1250', 1250],
    ['1,5', 1.5],
    ['0,25', 0.25],
    ['  22.000.000  ', 22000000],
    ['0', 0],
    [1250, 1250],
    [12.5, 12.5]
  ])('parseTrDecimal %s → %s', (input, expected) => expect(parseTrDecimal(input)).toBe(expected))

  it.each(['', '   ', null, undefined, 'abc', '-5', '1.25', '12.34.567', '1.2500', ',5', '5,', '1,2,3', '1..250', '1e3', '5 000', '+5', NaN, -1, true])(
    'parseTrDecimal rejects %s',
    (input) => expect(parseTrDecimal(input)).toBeNull()
  )

  it.each([
    ['5.000.000', 5000000],
    ['1.250', 1250],
    ['120', 120],
    [0, 0]
  ])('parseTrInteger %s → %s', (input, expected) => expect(parseTrInteger(input)).toBe(expected))

  it.each(['1,5', '1.250,50', '', 'x', '-1', '2.5', 2.5, null])('parseTrInteger rejects %s', (input) =>
    expect(parseTrInteger(input)).toBeNull()
  )

  it('allowNegative accepts a leading minus (basement floors)', () => {
    expect(parseTrInteger('-2', { allowNegative: true })).toBe(-2)
    expect(parseTrInteger('-', { allowNegative: true })).toBeNull()
    expect(parseTrDecimal('-1,5', { allowNegative: true })).toBe(-1.5)
  })

  it('toTrInput writes numbers back in the text-box format', () => {
    expect(toTrInput(1250.5)).toBe('1250,5')
    expect(toTrInput(5000000)).toBe('5000000')
    expect(toTrInput(null)).toBe('')
    expect(toTrInput('')).toBe('')
  })

  it('does not change the rates screen rule (last separator is the decimal)', () => {
    expect(parseDecimal('41.25')).toBe(41.25)
    expect(parseDecimal('1.234,5')).toBe(1234.5)
  })
})

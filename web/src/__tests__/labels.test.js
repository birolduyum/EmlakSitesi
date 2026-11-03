import { describe, it, expect } from 'vitest'
import { heatingLabels, furnishingLabels, typeLabels, currencyLabels, sortLabels } from '../labels.js'

// Emlak.Api/Domain/Enums.cs ve ListingQuery.cs ile aynı adlar
const HEATING = ['Yok', 'Soba', 'Dogalgaz', 'Kombi', 'Merkezi', 'YerdenIsitma', 'Klima']
const FURNISHING = ['Esyali', 'Esyasiz']
const TYPES = ['Satilik', 'Kiralik']
const CURRENCIES = ['TRY', 'USD', 'EUR']
const SORTS = ['Newest', 'PriceAsc', 'PriceDesc']

describe('labels', () => {
  it.each([
    ['heating', HEATING, heatingLabels],
    ['furnishing', FURNISHING, furnishingLabels],
    ['type', TYPES, typeLabels],
    ['currency', CURRENCIES, currencyLabels],
    ['sort', SORTS, sortLabels]
  ])('every %s enum name has a label and no extras', (_n, names, map) => {
    for (const n of names) expect(map[n], n).toBeTruthy()
    expect(Object.keys(map).sort()).toEqual([...names].sort())
  })
})

import { describe, it, expect } from 'vitest'
import {
  heatingLabels, furnishingLabels, typeLabels, currencyLabels, sortLabels,
  presence, eligibility, yesNo, usageLabels, deedLabels, kitchenLabels, tagGroupLabels
} from '../labels.js'

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
    ['sort', SORTS, sortLabels],
    ['usage', ['Bos', 'KiraciOturuyor', 'MalSahibiOturuyor'], usageLabels],
    ['deed', ['KatMulkiyeti', 'KatIrtifaki', 'HisseliTapu', 'MustakilTapu'], deedLabels],
    ['kitchen', ['Acik', 'Kapali', 'Amerikan'], kitchenLabels],
    ['tagGroup', ['Ic', 'Dis', 'Konum'], tagGroupLabels]
  ])('every %s enum name has a label and no extras', (_n, names, map) => {
    for (const n of names) expect(map[n], n).toBeTruthy()
    expect(Object.keys(map).sort()).toEqual([...names].sort())
  })
})

// Kural: fiziksel özellikler Var/Yok, krediye uygunluk Uygun/Değil, takas ve site içi Evet/Hayır.
describe('yes/no label rules', () => {
  it('uses the documented wording', () => {
    expect(presence(true)).toBe('Var')
    expect(presence(false)).toBe('Yok')
    expect(eligibility(true)).toBe('Uygun')
    expect(eligibility(false)).toBe('Değil')
    expect(yesNo(true)).toBe('Evet')
    expect(yesNo(false)).toBe('Hayır')
  })
})

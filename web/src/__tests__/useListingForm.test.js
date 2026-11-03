import { describe, it, expect } from 'vitest'
import { useListingForm, toInput, validate, fieldOfError, fromDetail, emptyForm } from '../composables/useListingForm.js'

// AdminListingInput (api/Emlak.Api/Listings/AdminDtos.cs) özellikleri — elle sabitlenmiş küme.
const INPUT_KEYS = [
  'title', 'badge', 'type', 'price', 'currency', 'deposit', 'aidat', 'city', 'district', 'neighborhood', 'rooms',
  'bathrooms', 'grossM2', 'netM2', 'floor', 'totalFloors', 'buildingAge', 'heating', 'furnishing', 'usage', 'deed',
  'kitchen', 'balcony', 'elevator', 'parking', 'inSite', 'loanEligible', 'swap', 'description', 'ownerName',
  'ownerPhone', 'ownerNote', 'tagIds'
].sort()

const filled = (over = {}) => ({
  ...emptyForm(),
  title: ' Deniz manzaralı ', city: 'İzmir', district: 'Karşıyaka', rooms: '3+1', price: 2500000,
  ownerName: 'Mehmet', ownerPhone: '0532', ...over
})

describe('toInput', () => {
  it('toInput_converts_numbers_and_nulls', () => {
    const p = toInput(filled({
      price: '1.250.000,5', bathrooms: '2', grossM2: '120', netM2: '', floor: '-1', totalFloors: 5, buildingAge: '0',
      badge: '  ', neighborhood: '', ownerNote: '', description: ''
    }))
    expect(p.title).toBe('Deniz manzaralı')
    expect(p.price).toBe(1250000.5)
    expect(p.bathrooms).toBe(2)
    expect(p.grossM2).toBe(120)
    expect(p.netM2).toBe(0)
    expect(p.floor).toBe(-1)
    expect(p.totalFloors).toBe(5)
    expect(p.buildingAge).toBe(0)
    expect(p.badge).toBeNull()
    expect(p.neighborhood).toBeNull()
    expect(p.ownerNote).toBeNull()
    expect(p.deposit).toBeNull()
    expect(p.aidat).toBeNull()
  })
  it('never emits NaN, Infinity or numeric strings', () => {
    const p = toInput(filled({ price: 'abc', bathrooms: NaN, grossM2: Infinity, floor: '2' }))
    for (const k of ['price', 'bathrooms', 'grossM2', 'netM2', 'floor', 'totalFloors', 'buildingAge']) {
      expect(typeof p[k]).toBe('number')
      expect(Number.isFinite(p[k])).toBe(true)
    }
    expect(p.price).toBe(0)
    expect(p.floor).toBe(2)
  })
  it('comma decimals are understood', () => {
    expect(toInput(filled({ type: 'Kiralik', price: '15000,5', deposit: '30000', aidat: '250,25' })).price).toBe(15000.5)
    expect(toInput(filled({ type: 'Kiralik', aidat: '250,25' })).aidat).toBe(250.25)
  })
  it('deposit and aidat are only sent for Kiralik', () => {
    const rent = toInput(filled({ type: 'Kiralik', deposit: '30000', aidat: '' }))
    expect(rent.deposit).toBe(30000)
    expect(rent.aidat).toBeNull()
    const sale = toInput(filled({ type: 'Satilik', deposit: '30000', aidat: '10' }))
    expect(sale.deposit).toBeNull()
    expect(sale.aidat).toBeNull()
  })
  it('tagIds are unique integers', () => {
    expect(toInput(filled({ tagIds: [3, 1, 3, '2'] })).tagIds).toEqual([1, 2, 3])
  })
  it('no_unknown_fields_in_payload', () => {
    const f = { ...filled(), showingCount: 4, photos: [], id: 9, isDeleted: false, createdBy: 'x' }
    expect(Object.keys(toInput(f)).sort()).toEqual(INPUT_KEYS)
  })
})

it('Turkish_grouping_in_text_boxes_becomes_the_right_number', () => {
  expect(toInput(filled({ price: '1.250' })).price).toBe(1250)
  expect(toInput(filled({ price: '5.000.000' })).price).toBe(5000000)
  expect(toInput(filled({ type: 'Kiralik', deposit: '30.000', aidat: '1.250,50' }))).toMatchObject({ deposit: 30000, aidat: 1250.5 })
  expect(toInput(filled({ grossM2: '1.200', floor: '-2' }))).toMatchObject({ grossM2: 1200, floor: -2 })
})

it('detail_decimals_are_shown_with_a_comma_so_they_round_trip', () => {
  const f = fromDetail({ price: 1250.5, deposit: 30000, aidat: null, owner: {}, tags: [] })
  expect(f.price).toBe('1250,5')
  expect(toInput(f).price).toBe(1250.5)
})

describe('useListingForm', () => {
  it('kiralik_fields_cleared_when_switching_to_satilik', () => {
    const { form } = useListingForm()
    form.type = 'Kiralik'
    form.deposit = 30000
    form.aidat = 250
    form.type = 'Satilik'
    expect(form.deposit).toBe('')
    expect(form.aidat).toBe('')
  })
  it('kiralik values are kept while staying Kiralik', () => {
    const { form } = useListingForm()
    form.type = 'Kiralik'
    form.deposit = 30000
    expect(form.deposit).toBe(30000)
  })
  it('dirty tracking: clean after load, dirty after edit, clean after markClean', () => {
    const f = useListingForm()
    expect(f.dirty.value).toBe(false)
    f.form.title = 'x'
    expect(f.dirty.value).toBe(true)
    f.markClean()
    expect(f.dirty.value).toBe(false)
    f.form.title = 'y'
    f.form.title = 'x'
    expect(f.dirty.value).toBe(false)
  })
  it('load fills the form from a detail response and is not dirty', () => {
    const f = useListingForm()
    f.load({
      title: 'A', badge: null, type: 'Kiralik', price: 15000, currency: 'USD', deposit: 30000, aidat: null, city: 'Ankara',
      district: 'Çankaya', neighborhood: 'Kızılay', rooms: '2+1', bathrooms: 1, grossM2: 90, netM2: 80, floor: 3, totalFloors: 8,
      buildingAge: 5, heating: 'Kombi', furnishing: 'Esyali', usage: 'Bos', deed: 'KatMulkiyeti', kitchen: 'Acik', balcony: true,
      elevator: false, parking: false, inSite: false, loanEligible: true, swap: false, description: 'd',
      owner: { name: 'M', phone: '1', note: null }, tags: [{ id: 4, name: 'Klima', group: 'Ic' }]
    })
    expect(f.form.type).toBe('Kiralik')
    expect(f.form.deposit).toBe('30000') // yazı kutusu biçimi (Türkçe, binliksiz)
    expect(f.form.aidat).toBe('')
    expect(f.form.badge).toBe('')
    expect(f.form.ownerName).toBe('M')
    expect(f.form.tagIds).toEqual([4])
    expect(f.dirty.value).toBe(false)
  })
  it('fromDetail ignores audit and photo fields', () => {
    const f = fromDetail({ title: 'A', owner: { name: 'M', phone: '1' }, createdBy: 'x', photos: [{ id: 1 }], tags: [] })
    expect(Object.keys(f).sort()).toEqual(INPUT_KEYS)
  })
})

describe('validation', () => {
  it('unreadable_numbers_get_an_inline_error_instead_of_being_dropped', () => {
    const e = validate(filled({ price: '12a', grossM2: '1.5.5', floor: 'x', bathrooms: '2' }))
    expect(e.price).toBe('Geçerli bir sayı girin')
    expect(e.grossM2).toBe('Geçerli bir sayı girin')
    expect(e.floor).toBe('Geçerli bir sayı girin')
    expect(e.bathrooms).toBeUndefined()
    expect(validate(filled({ price: '1.25' })).price).toBe('Geçerli bir sayı girin')
    expect(validate(filled({ price: '5.000.000' })).price).toBeUndefined()
  })
  it('deposit_is_only_checked_for_kiralik', () => {
    expect(validate(filled({ type: 'Satilik', deposit: 'abc' })).deposit).toBeUndefined()
    expect(validate(filled({ type: 'Kiralik', deposit: 'abc' })).deposit).toBe('Geçerli bir sayı girin')
  })
  it('validation_requires_basics', () => {
    const errors = validate(emptyForm())
    expect(Object.keys(errors).sort()).toEqual(['city', 'district', 'ownerName', 'ownerPhone', 'price', 'rooms', 'title'])
  })
  it('price must be greater than zero', () => {
    expect(validate(filled({ price: 0 })).price).toBeTruthy()
    expect(validate(filled({ price: -5 })).price).toBeTruthy()
    expect(validate(filled({ price: '' })).price).toBeTruthy()
    expect(validate(filled({ price: 1 })).price).toBeUndefined()
  })
  it('whitespace-only required text counts as empty', () => {
    expect(validate(filled({ title: '   ' })).title).toBeTruthy()
  })
  it('a complete form has no errors', () => {
    expect(validate(filled())).toEqual({})
  })
})

describe('fieldOfError', () => {
  it('maps server messages to fields', () => {
    expect(fieldOfError('Başlık boş olamaz.')).toBe('title')
    expect(fieldOfError('Fiyat sıfırdan büyük olmalıdır.')).toBe('price')
    expect(fieldOfError('Ev sahibi telefonu boş olamaz.')).toBe('ownerPhone')
    expect(fieldOfError('Ev sahibi adı boş olamaz.')).toBe('ownerName')
    expect(fieldOfError('Depozito yalnızca kiralık ilanda girilebilir.')).toBe('deposit')
    expect(fieldOfError("Net m², brüt m²'den büyük olamaz.")).toBe('netM2')
    expect(fieldOfError('Bilinmeyen etiket seçildi.')).toBeNull()
  })
})

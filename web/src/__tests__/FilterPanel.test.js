import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import FilterPanel from '../components/FilterPanel.vue'

const filters = {
  cities: ['Ankara', 'İzmir'],
  districtsByCity: { Ankara: ['Çankaya', 'Keçiören'], İzmir: ['Bornova'] },
  roomCounts: { '3+1': 4, '2+1': 12 }
}

// Uygulanan durumu (v-model) üst bileşen gibi günceller
function make(modelValue = {}) {
  const w = mount(FilterPanel, {
    props: {
      modelValue,
      filters,
      'onUpdate:modelValue': (v) => w.setProps({ modelValue: v })
    }
  })
  return w
}
const emits = (w) => w.emitted('update:modelValue') ?? []
const last = (w) => emits(w).at(-1)[0]
const apply = (w) => w.find('form').trigger('submit')

describe('FilterPanel (draft + apply)', () => {
  it('is a labelled form with the apply button last in the tab order', () => {
    const w = make()
    expect(w.find('form[aria-label="Filtreler"]').exists()).toBe(true)
    const focusables = w.findAll('input:not([disabled]), select:not([disabled]), button')
    expect(focusables.at(-1).attributes('data-testid')).toBe('apply')
    expect(focusables.at(-1).attributes('type')).toBe('submit')
    expect(focusables.at(-1).text()).toBe('Filtreleri uygula')
  })
  it('nothing_is_emitted_until_apply: every control only changes the draft', async () => {
    const w = make()
    await w.find('input[type="radio"][name="type"][value="Kiralik"]').setValue()
    await w.find('select[name="city"]').setValue('Ankara')
    await w.find('select[name="district"]').setValue('Çankaya')
    await w.find('input[name="neighborhood"]').setValue('Bahçelievler')
    await w.find('input[name="currency"][value="USD"]').setValue()
    await w.find('input[name="minPriceTry"]').setValue('1000000')
    await w.find('input[name="maxM2"]').setValue('150')
    await w.findAll('[data-testid="room"] input')[0].setValue(true)
    await w.find('select[name="minBathrooms"]').setValue('2')
    await w.find('select[name="heating"]').setValue('Kombi')
    await w.find('select[name="furnishing"]').setValue('Esyali')
    await w.find('input[name="balcony"]').setValue(true)
    expect(emits(w)).toHaveLength(0)
  })
  it('apply_emits_all_drafts_at_once_as_one_update', async () => {
    const w = make()
    await w.find('input[type="radio"][name="type"][value="Kiralik"]').setValue()
    await w.find('select[name="city"]').setValue('Ankara')
    await w.find('select[name="district"]').setValue('Çankaya')
    await w.find('input[name="minPriceTry"]').setValue('5.000.000')
    await w.find('input[name="maxPriceTry"]').setValue('2500000')
    await w.findAll('[data-testid="room"] input')[1].setValue(true)
    await w.findAll('[data-testid="room"] input')[0].setValue(true)
    await w.find('input[name="balcony"]').setValue(true)
    await w.find('form').trigger('submit')
    expect(emits(w)).toHaveLength(1)
    expect(last(w)).toEqual({
      type: 'Kiralik', city: 'Ankara', district: 'Çankaya', minPriceTry: 5000000, maxPriceTry: 2500000, rooms: ['2+1', '3+1'], balcony: true
    })
    expect(w.emitted('applied')).toHaveLength(1)
  })
  it('enter_in_field_applies (the form submits)', async () => {
    const w = make()
    await w.find('input[name="neighborhood"]').setValue('Moda')
    await apply(w)
    expect(last(w)).toEqual({ neighborhood: 'Moda' })
  })
  it('apply_without_changes_sends_nothing', async () => {
    const w = make({ type: 'Kiralik', city: 'Ankara', minPriceTry: 100 })
    await apply(w)
    expect(emits(w)).toHaveLength(0)
    expect(w.emitted('applied')).toBeUndefined()
    // aynı değeri yeniden yazmak da değişiklik sayılmaz
    await w.find('input[name="minPriceTry"]').setValue('100')
    await apply(w)
    expect(emits(w)).toHaveLength(0)
  })
  it('type_is_draft_until_apply and type_change_keeps_other_filters', async () => {
    const w = make({ type: 'Satilik', city: 'Ankara', balcony: true })
    await w.find('input[type="radio"][name="type"][value="Kiralik"]').setValue()
    expect(emits(w)).toHaveLength(0)
    await apply(w)
    expect(last(w)).toEqual({ type: 'Kiralik', city: 'Ankara', balcony: true })
  })
  it('clear_applies_immediately and resets the draft including type to Tümü', async () => {
    const w = make({ type: 'Kiralik', city: 'Ankara', district: 'Çankaya', minPriceTry: 5, rooms: ['2+1'], balcony: true, page: 3 })
    await w.find('input[name="neighborhood"]').setValue('Bahçelievler')
    await w.find('[data-testid="clear"]').trigger('click')
    expect(emits(w)).toHaveLength(1)
    expect(last(w)).toEqual({})
    expect(w.find('input[name="neighborhood"]').element.value).toBe('')
    expect(w.find('input[name="type"][value=""]').element.checked).toBe(true)
  })
  it('draft_resyncs_when_url_changes: an unapplied draft is discarded', async () => {
    const w = make({ type: 'Satilik', city: 'Ankara' })
    await w.find('input[name="minM2"]').setValue('80')
    await w.find('select[name="city"]').setValue('İzmir')
    await w.setProps({ modelValue: { type: 'Kiralik', city: 'Ankara', maxPriceTry: 900 } })
    expect(w.find('input[name="minM2"]').element.value).toBe('')
    expect(w.find('select[name="city"]').element.value).toBe('Ankara')
    expect(w.find('input[name="maxPriceTry"]').element.value).toBe('900')
    expect(w.find('input[name="type"][value="Kiralik"]').element.checked).toBe(true)
    expect(w.find('[data-testid="unapplied"]').text()).toBe('')
  })
  it('unapplied_hint_shows_and_hides', async () => {
    const w = make({ type: 'Satilik' })
    const hint = () => w.find('[data-testid="unapplied"]')
    expect(hint().attributes('aria-live')).toBe('polite')
    expect(hint().text()).toBe('')
    expect(w.find('[data-testid="apply"]').classes()).not.toContain('dirty')
    await w.find('input[name="balcony"]').setValue(true)
    expect(hint().text()).toBe('Uygulanmamış değişiklikler var')
    expect(w.find('[data-testid="apply"]').classes()).toContain('dirty')
    await w.find('input[name="balcony"]').setValue(false)
    expect(hint().text()).toBe('')
    await w.find('input[name="balcony"]').setValue(true)
    await apply(w)
    await w.vm.$nextTick()
    expect(hint().text()).toBe('')
  })
  it('district_disabled_until_city_chosen (draft city counts)', async () => {
    const w = make()
    const district = () => w.find('select[name="district"]')
    expect(district().attributes('disabled')).toBeDefined()
    await w.find('select[name="city"]').setValue('Ankara')
    expect(district().attributes('disabled')).toBeUndefined()
    expect(district().findAll('option').map((o) => o.text())).toEqual(['Tüm İlçeler', 'Çankaya', 'Keçiören'])
  })
  it('changing the city clears the district draft', async () => {
    const w = make({ type: 'Satilik', city: 'Ankara', district: 'Çankaya' })
    await w.find('select[name="city"]').setValue('İzmir')
    await apply(w)
    expect(last(w)).toEqual({ type: 'Satilik', city: 'İzmir' })
  })
  it('room_checkboxes_show_counts_and_toggle_the_draft', async () => {
    const w = make()
    expect(w.findAll('[data-testid="room"]').map((l) => l.text())).toEqual(['2+1 (12)', '3+1 (4)'])
    const boxes = w.findAll('[data-testid="room"] input')
    await boxes[0].setValue(true)
    await boxes[1].setValue(true)
    await boxes[0].setValue(false)
    await apply(w)
    expect(last(w).rooms).toEqual(['3+1'])
  })
  it('feature checkbox: checked sends true, unchecked omits', async () => {
    const w = make({ balcony: true })
    await w.find('input[name="elevator"]').setValue(true)
    await w.find('input[name="balcony"]').setValue(false)
    await apply(w)
    expect(last(w)).toEqual({ elevator: true })
  })
  it('unreadable_numbers_show_an_inline_error_and_are_not_sent', async () => {
    const w = make()
    await w.find('input[name="minM2"]').setValue('12a')
    expect(w.find('input[name="minM2"]').attributes('aria-invalid')).toBe('true')
    expect(w.text()).toContain('Geçerli bir sayı girin')
    await w.find('input[name="minM2"]').setValue('120')
    expect(w.text()).not.toContain('Geçerli bir sayı girin')
  })
  it('apply_blocked_when_number_invalid_focuses_first_invalid', async () => {
    const w = mount(FilterPanel, { props: { modelValue: {}, filters }, attachTo: document.body })
    await w.find('input[name="maxM2"]').setValue('200')
    await w.find('input[name="minPriceTry"]').setValue('1,2,3')
    await w.find('input[name="minM2"]').setValue('12a')
    const msg = () => w.find('[data-testid="invalid-msg"]')
    expect(msg().text()).toBe('')
    await apply(w)
    await w.vm.$nextTick()
    expect(emits(w)).toHaveLength(0)
    expect(w.emitted('applied')).toBeUndefined()
    expect(document.activeElement).toBe(w.find('input[name="minPriceTry"]').element) // formdaki ilk hatalı alan
    expect(msg().text()).toBe('Geçersiz sayı var; düzeltin')
    expect(msg().attributes('aria-live')).toBe('polite')
    expect(w.find('input[name="minPriceTry"]').element.value).toBe('1,2,3') // yazılan metin silinmez
    w.unmount()
  })
  it('apply_works_after_fixing_number', async () => {
    const w = make()
    await w.find('input[name="minPriceTry"]').setValue('1,2,3')
    await apply(w)
    expect(emits(w)).toHaveLength(0)
    await w.find('input[name="minPriceTry"]').setValue('1.500')
    expect(w.find('[data-testid="invalid-msg"]').text()).toBe('')
    await apply(w)
    expect(last(w)).toEqual({ minPriceTry: 1500 })
  })
  it('search_text_change_keeps_unapplied_draft', async () => {
    const w = make({ type: 'Satilik' })
    await w.findAll('[data-testid="room"] input')[0].setValue(true)
    await w.find('input[name="minM2"]').setValue('80')
    await w.setProps({ modelValue: { type: 'Satilik', text: 'deniz' } })
    expect(w.findAll('[data-testid="room"] input')[0].element.checked).toBe(true)
    expect(w.find('input[name="minM2"]').element.value).toBe('80')
    await apply(w)
    expect(last(w)).toEqual({ type: 'Satilik', text: 'deniz', rooms: ['2+1'], minM2: 80 })
  })
  it('sort_change_keeps_unapplied_draft', async () => {
    const w = make({ type: 'Kiralik' })
    await w.find('input[name="balcony"]').setValue(true)
    await w.setProps({ modelValue: { type: 'Kiralik', sort: 'PriceAsc' } })
    expect(w.find('input[name="balcony"]').element.checked).toBe(true)
    await apply(w)
    expect(last(w)).toEqual({ type: 'Kiralik', sort: 'PriceAsc', balcony: true })
  })
  it('page_change_keeps_unapplied_draft', async () => {
    const w = make({ type: 'Satilik', page: 1 })
    await w.find('select[name="city"]').setValue('Ankara')
    await w.setProps({ modelValue: { type: 'Satilik', page: 2 } })
    expect(w.find('select[name="city"]').element.value).toBe('Ankara')
    await apply(w)
    expect(last(w)).toEqual({ type: 'Satilik', city: 'Ankara' })
  })
  it('back_navigation_resets_draft', async () => {
    const w = make({ type: 'Satilik', city: 'Ankara' })
    await w.find('input[name="balcony"]').setValue(true)
    await w.setProps({ modelValue: { type: 'Satilik', city: 'İzmir', text: 'x' } })
    expect(w.find('input[name="balcony"]').element.checked).toBe(false)
    expect(w.find('select[name="city"]').element.value).toBe('İzmir')
  })
  it('Turkish grouped price 5.000.000 and 1.250,5 are read', async () => {
    const w = make()
    await w.find('input[name="maxPriceTry"]').setValue('5.000.000')
    await w.find('input[name="minPriceTry"]').setValue('1.250,5')
    await apply(w)
    expect(last(w)).toMatchObject({ minPriceTry: 1250.5, maxPriceTry: 5000000 })
  })
  it('price and area boxes are text inputs with the right keypads', () => {
    const w = make()
    expect(w.find('input[name="minPriceTry"]').attributes('type')).toBe('text')
    expect(w.find('input[name="minPriceTry"]').attributes('inputmode')).toBe('decimal')
    expect(w.find('input[name="minM2"]').attributes('inputmode')).toBe('numeric')
  })
  it('bathroom select and building-floor boxes join the draft', async () => {
    const w = make()
    const select = w.find('select[name="minBathrooms"]')
    expect(select.findAll('option').map((o) => o.text())).toEqual(['Tümü', '1+', '2+', '3+', '4+'])
    await select.setValue('2')
    await w.find('input[name="minTotalFloors"]').setValue('5')
    await w.find('input[name="maxTotalFloors"]').setValue('12')
    await apply(w)
    expect(last(w)).toEqual({ minBathrooms: 2, minTotalFloors: 5, maxTotalFloors: 12 })
  })
  it('shows the applied bathroom and floor-count filters', () => {
    const w = make({ type: 'Satilik', minBathrooms: 3, minTotalFloors: 4 })
    expect(w.find('select[name="minBathrooms"]').element.value).toBe('3')
    expect(w.find('input[name="minTotalFloors"]').element.value).toBe('4')
  })
  it('ignores negative typing', async () => {
    const w = make()
    await w.find('input[name="minM2"]').setValue('-4')
    await apply(w)
    expect(emits(w)).toHaveLength(0)
  })
  it('currency radios and heating select feed the draft (Yerden Isıtma label)', async () => {
    const w = make()
    await w.find('input[name="currency"][value="USD"]').setValue()
    await w.find('select[name="heating"]').setValue('YerdenIsitma')
    expect(w.find('select[name="heating"]').text()).toContain('Yerden Isıtma')
    await apply(w)
    expect(last(w)).toEqual({ currency: 'USD', heating: 'YerdenIsitma' })
  })
  it('price_hint_only_in_tumu: shown while the draft type is Tümü', async () => {
    const w = make({})
    const hint = () => w.find('[data-testid="price-hint"]')
    expect(hint().text()).toBe('Satılık fiyatları ve aylık kiralar birlikte süzülür.')
    await w.find('input[name="type"][value="Kiralik"]').setValue()
    expect(hint().exists()).toBe(false)
    await w.find('input[name="type"][value=""]').setValue()
    expect(hint().exists()).toBe(true)
    expect(make({ type: 'Satilik' }).find('[data-testid="price-hint"]').exists()).toBe(false)
  })
  it('admin_slot_fields_are_part_of_draft', async () => {
    const w = mount(FilterPanel, {
      props: { modelValue: {}, filters, allLabel: 'Hepsi', 'onUpdate:modelValue': (v) => w.setProps({ modelValue: v }) },
      slots: {
        extra: `<template #extra="{ draft, set }">
          <select name="status" :value="draft.status ?? 'Active'" @change="set('status', $event.target.value)"><option value="Active">A</option><option value="Deleted">D</option></select>
          <input name="ownerText" :value="draft.ownerText ?? ''" @input="set('ownerText', $event.target.value)" />
        </template>`
      }
    })
    await w.find('select[name="status"]').setValue('Deleted')
    await w.find('input[name="ownerText"]').setValue('Kaya')
    expect(emits(w)).toHaveLength(0)
    expect(w.find('[data-testid="unapplied"]').text()).toContain('Uygulanmamış')
    await w.find('form').trigger('submit')
    expect(emits(w)).toHaveLength(1)
    expect(last(w)).toEqual({ status: 'Deleted', ownerText: 'Kaya' })
  })
})

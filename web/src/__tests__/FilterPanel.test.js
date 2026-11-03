import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import FilterPanel from '../components/FilterPanel.vue'

const filters = {
  cities: ['Ankara', 'İzmir'],
  districtsByCity: { Ankara: ['Çankaya', 'Keçiören'], İzmir: ['Bornova'] },
  roomCounts: { '3+1': 4, '2+1': 12 }
}

// v-model davranışını taklit eder
function make(modelValue = { type: 'Satilik' }) {
  const w = mount(FilterPanel, {
    props: {
      modelValue,
      filters,
      'onUpdate:modelValue': (v) => w.setProps({ modelValue: v })
    }
  })
  return w
}
const last = (w) => w.emitted('update:modelValue').at(-1)[0]

beforeEach(() => vi.useFakeTimers())
afterEach(() => vi.useRealTimers())

describe('FilterPanel', () => {
  it('is a labelled form', () => {
    const w = make()
    expect(w.find('form[aria-label="Filtreler"]').exists()).toBe(true)
  })
  it('switching_to_kiralik_emits_type_kiralik', async () => {
    const w = make()
    await w.find('input[type="radio"][value="Kiralik"]').setValue()
    expect(last(w).type).toBe('Kiralik')
  })
  it('typing_price_emits_numeric_min_max_after_debounce', async () => {
    const w = make()
    await w.find('input[name="minPriceTry"]').setValue('1000000')
    await w.find('input[name="maxPriceTry"]').setValue('2500000')
    expect(w.emitted('update:modelValue')).toBeUndefined()
    vi.advanceTimersByTime(300)
    expect(w.emitted('update:modelValue')).toHaveLength(1)
    expect(last(w)).toMatchObject({ type: 'Satilik', minPriceTry: 1000000, maxPriceTry: 2500000 })
  })
  it('ignores negative typing', async () => {
    const w = make()
    await w.find('input[name="minM2"]').setValue('-4')
    vi.advanceTimersByTime(300)
    expect(last(w).minM2).toBeUndefined()
  })
  it('clear_resets_everything_except_type', async () => {
    const w = make({ type: 'Kiralik', city: 'Ankara', district: 'Çankaya', minPriceTry: 5, rooms: ['2+1'], balcony: true, page: 3 })
    await w.find('input[name="neighborhood"]').setValue('Bahçelievler') // bekleyen yazma da iptal olmalı
    await w.find('[data-testid="clear"]').trigger('click')
    vi.advanceTimersByTime(500)
    expect(w.emitted('update:modelValue')).toHaveLength(1)
    expect(last(w)).toEqual({ type: 'Kiralik' })
    expect(w.find('input[name="neighborhood"]').element.value).toBe('')
  })
  it('district_disabled_until_city_chosen', async () => {
    const w = make()
    const district = () => w.find('select[name="district"]')
    expect(district().attributes('disabled')).toBeDefined()
    await w.find('select[name="city"]').setValue('Ankara')
    expect(last(w).city).toBe('Ankara')
    expect(district().attributes('disabled')).toBeUndefined()
    expect(district().findAll('option').map((o) => o.text())).toEqual(['Tüm İlçeler', 'Çankaya', 'Keçiören'])
  })
  it('changing the city clears the district', async () => {
    const w = make({ type: 'Satilik', city: 'Ankara', district: 'Çankaya' })
    await w.find('select[name="city"]').setValue('İzmir')
    expect(last(w).district).toBeUndefined()
  })
  it('room_checkboxes_show_counts_and_emit_repeated_rooms', async () => {
    const w = make()
    const labels = w.findAll('[data-testid="room"]').map((l) => l.text())
    expect(labels).toEqual(['2+1 (12)', '3+1 (4)'])
    const boxes = w.findAll('[data-testid="room"] input')
    await boxes[0].setValue(true)
    await boxes[1].setValue(true)
    expect(last(w).rooms).toEqual(['2+1', '3+1'])
    await boxes[0].setValue(false)
    expect(last(w).rooms).toEqual(['3+1'])
  })
  it('feature checkbox sends true, unchecking omits it', async () => {
    const w = make()
    await w.find('input[name="balcony"]').setValue(true)
    expect(last(w).balcony).toBe(true)
    await w.find('input[name="balcony"]').setValue(false)
    expect('balcony' in last(w)).toBe(false)
  })
  it('currency filter and heating select emit immediately', async () => {
    const w = make()
    await w.find('input[name="currency"][value="USD"]').setValue()
    expect(last(w).currency).toBe('USD')
    await w.find('select[name="heating"]').setValue('YerdenIsitma')
    expect(last(w).heating).toBe('YerdenIsitma')
    expect(w.find('select[name="heating"]').text()).toContain('Yerden Isıtma')
  })
})

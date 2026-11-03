import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import SpecGrid from '../components/SpecGrid.vue'

const base = { rooms: '2+1', bathrooms: 1, grossM2: 100, netM2: 90, floor: 3, totalFloors: 5, buildingAge: 0 }
const text = (over) => mount(SpecGrid, { props: { listing: { ...base, ...over } } }).text()

describe('SpecGrid', () => {
  it('shows labelled boxes', () => {
    const t = text({})
    expect(t).toContain('Oda Sayısı')
    expect(t).toContain('100 m² / 90 m²')
    expect(t).toContain('3. Kat')
  })
  it('floors: Bodrum Kat and Giriş Kat', () => {
    expect(text({ floor: -1 })).toContain('Bodrum Kat')
    expect(text({ floor: 0 })).toContain('Giriş Kat')
  })
  it('missing values show a dash', () => {
    expect(text({ floor: null, netM2: null })).toContain('-')
  })
})

import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import FilterPanel from '../components/FilterPanel.vue'
import ListingRow from '../components/ListingRow.vue'
import { adminRouteQueryToParams, adminParamsToRouteQuery, routeQueryToParams } from '../query.js'

describe('FilterPanel type options', () => {
  it('admin label Hepsi, the extra slot renders; choosing Hepsi + apply removes type', async () => {
    const w = mount(FilterPanel, {
      props: { modelValue: { type: 'Kiralik' }, allLabel: 'Hepsi' },
      slots: { extra: '<p data-testid="slot">ek süzgeç</p>' }
    })
    const radios = w.findAll('input[type="radio"][name="type"]')
    expect(radios.map((r) => r.attributes('value'))).toEqual(['', 'Satilik', 'Kiralik'])
    expect(w.findAll('fieldset.segmented label').map((l) => l.text())).toEqual(['Hepsi', 'Satılık', 'Kiralık'])
    expect(w.find('[data-testid="slot"]').text()).toBe('ek süzgeç')
    await w.find('input[type="radio"][name="type"][value=""]').setValue()
    expect(w.emitted('update:modelValue')).toBeUndefined() // taslak: uygulanana kadar yayılmaz
    await w.find('form').trigger('submit')
    expect(w.emitted('update:modelValue').at(-1)[0]).not.toHaveProperty('type')
  })
  it('the visitor panel labels the all-option Tümü', () => {
    const w = mount(FilterPanel, { props: { modelValue: {} } })
    expect(w.findAll('fieldset.segmented label').map((l) => l.text())).toEqual(['Tümü', 'Satılık', 'Kiralık'])
  })
  it('with no type, the all-option is the checked one', () => {
    const w = mount(FilterPanel, { props: { modelValue: {} } })
    expect(w.find('input[type="radio"][name="type"][value=""]').element.checked).toBe(true)
  })
})

describe('ListingRow admin props', () => {
  const listing = { id: 4, title: 'Daire', type: 'Satilik', price: 1, currency: 'TRY', city: 'Ankara', district: 'Çankaya', rooms: '2+1', grossM2: 50, floor: 1, createdAt: new Date(2026, 0, 2, 12).toISOString(), photoUrls: [], deletedBy: 'veli', deletedAt: new Date(2026, 8, 5, 12).toISOString() }
  const mountRow = (props, slots) => {
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:p(.*)*', component: { template: '<i/>' } }] })
    return mount(ListingRow, { props: { listing, ...props }, slots, global: { plugins: [router] } })
  }
  it('deleted prop dims the row and shows who/when', () => {
    const w = mountRow({ deleted: true })
    expect(w.classes()).toContain('deleted')
    expect(w.find('[data-testid="deleted-tag"]').text()).toBe('Silindi — veli, 05.09.2026')
  })
  it('a normal row has no deleted tag', () => {
    expect(mountRow({}).find('[data-testid="deleted-tag"]').exists()).toBe(false)
  })
  it('linkTo overrides the target and the actions slot replaces the advisor box', () => {
    const w = mountRow({ linkTo: '/admin/ilan/4', showAdvisor: false }, { actions: '<button>Düzenle</button>' })
    expect(w.find('h2 a').attributes('href')).toBe('/admin/ilan/4')
    expect(w.find('.side').text()).toBe('Düzenle')
  })
  it('without actions and showAdvisor=false there is no side column', () => {
    expect(mountRow({ showAdvisor: false }).find('.side').exists()).toBe(false)
  })
})

describe('admin query helpers', () => {
  it('type is optional; status and ownerText are read; Active is the omitted default', () => {
    expect(adminRouteQueryToParams({})).toEqual({ page: 1, pageSize: 20 })
    expect(adminRouteQueryToParams({ type: 'Kiralik', status: 'Deleted', ownerText: ' Kaya ' })).toEqual({
      type: 'Kiralik', status: 'Deleted', ownerText: 'Kaya', page: 1, pageSize: 20
    })
    expect(adminRouteQueryToParams({ status: 'Active' })).not.toHaveProperty('status')
    expect(adminRouteQueryToParams({ status: 'Nope', type: 'Arsa' })).toEqual({ page: 1, pageSize: 20 })
  })
  it('admin params round-trip to the URL, writing Satilik explicitly (no hidden default)', () => {
    expect(adminParamsToRouteQuery({ type: 'Satilik', status: 'All', page: 2, pageSize: 20 })).toEqual({ type: 'Satilik', status: 'All', page: '2' })
    expect(adminParamsToRouteQuery({ page: 1 })).toEqual({})
  })
  it('the visitor query is not affected by the admin keys', () => {
    expect(routeQueryToParams({ status: 'All', ownerText: 'x' })).toEqual({ page: 1, pageSize: 20 })
  })
})

import { describe, it, expect } from 'vitest'
import { routeQueryToParams, paramsToRouteQuery } from '../query.js'

describe('routeQueryToParams', () => {
  it('defaults to Satilik, page 1, pageSize 20', () => {
    expect(routeQueryToParams({})).toEqual({ type: 'Satilik', page: 1, pageSize: 20 })
  })
  it('parses numbers, booleans and enums', () => {
    const p = routeQueryToParams({
      type: 'Kiralik', page: '3', minPriceTry: '1000.5', maxM2: '120', floor: '0',
      balcony: 'true', heating: 'Kombi', currency: 'USD', sort: 'PriceAsc', city: ' Ankara ', text: 'deniz'
    })
    expect(p).toEqual({
      type: 'Kiralik', page: 3, pageSize: 20, minPriceTry: 1000.5, maxM2: 120, floor: 0,
      balcony: true, heating: 'Kombi', currency: 'USD', sort: 'PriceAsc', city: 'Ankara', text: 'deniz'
    })
  })
  it('ignores NaN, negative and empty numbers', () => {
    const p = routeQueryToParams({ minPriceTry: 'abc', maxPriceTry: '-5', minM2: '', maxBuildingAge: '-1', floor: 'x', page: '0' })
    expect(p).toEqual({ type: 'Satilik', page: 1, pageSize: 20 })
  })
  it('reads_Turkish_grouped_numbers_from_the_URL', () => {
    const p = routeQueryToParams({ maxPriceTry: '5.000.000', minPriceTry: '1.250,5', maxM2: '1.200' })
    expect(p).toMatchObject({ maxPriceTry: 5000000, minPriceTry: 1250.5, maxM2: 1200 })
  })
  it('parses_bathroom_and_building_floor_count_filters', () => {
    const p = routeQueryToParams({ minBathrooms: '2', minTotalFloors: '5', maxTotalFloors: '12' })
    expect(p).toMatchObject({ minBathrooms: 2, minTotalFloors: 5, maxTotalFloors: 12 })
    expect(routeQueryToParams({ minBathrooms: 'x', minTotalFloors: '-1', maxTotalFloors: '' })).toEqual({ type: 'Satilik', page: 1, pageSize: 20 })
    expect(paramsToRouteQuery({ minBathrooms: 3, minTotalFloors: 4, maxTotalFloors: 9 }))
      .toEqual({ minBathrooms: '3', minTotalFloors: '4', maxTotalFloors: '9' })
    expect(routeQueryToParams(paramsToRouteQuery(p))).toEqual(p)
  })
  it('ignores bad enums and false booleans', () => {
    const p = routeQueryToParams({ type: 'Arsa', heating: 'Nuclear', currency: 'GBP', sort: 'Random', balcony: 'false', swap: '1' })
    expect(p).toEqual({ type: 'Satilik', page: 1, pageSize: 20 })
  })
  it('rooms: single string becomes an array, array stays', () => {
    expect(routeQueryToParams({ rooms: '2+1' }).rooms).toEqual(['2+1'])
    expect(routeQueryToParams({ rooms: ['2+1', '3+1'] }).rooms).toEqual(['2+1', '3+1'])
    expect(routeQueryToParams({ rooms: [''] }).rooms).toBeUndefined()
  })
  it('drops unknown keys', () => {
    const p = routeQueryToParams({ evil: '1', status: 'All', ownerText: 'x', pageSize: '500' })
    expect(p).toEqual({ type: 'Satilik', page: 1, pageSize: 20 })
  })
})

describe('paramsToRouteQuery', () => {
  it('omits defaults', () => {
    expect(paramsToRouteQuery({ type: 'Satilik', page: 1, pageSize: 20, sort: 'Newest' })).toEqual({})
  })
  it('writes strings, booleans and repeated rooms', () => {
    expect(paramsToRouteQuery({ type: 'Kiralik', page: 2, minPriceTry: 5000, balcony: true, rooms: ['2+1', '3+1'] }))
      .toEqual({ type: 'Kiralik', page: '2', minPriceTry: '5000', balcony: 'true', rooms: ['2+1', '3+1'] })
  })
  it('round-trips', () => {
    const params = routeQueryToParams({
      type: 'Kiralik', page: '4', city: 'İzmir', district: 'Bornova', neighborhood: 'Kazımdirik', currency: 'EUR',
      minPriceTry: '100', maxPriceTry: '900', minM2: '50', maxM2: '90', rooms: ['1+1', '2+1'], maxBuildingAge: '10',
      floor: '3', heating: 'Merkezi', furnishing: 'Esyali', balcony: 'true', elevator: 'true', parking: 'true',
      inSite: 'true', loanEligible: 'true', swap: 'true', text: 'merkez', sort: 'PriceDesc'
    })
    expect(routeQueryToParams(paramsToRouteQuery(params))).toEqual(params)
  })
  it('drops NaN/negative on the way out', () => {
    expect(paramsToRouteQuery({ minM2: NaN, maxM2: -3, page: 'x' })).toEqual({})
  })
})

<script setup>
import { ref, computed, watch, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../api.js'
import { routeQueryToParams, paramsToRouteQuery } from '../query.js'
import { sortLabels, typeLabels } from '../labels.js'
import FilterPanel from '../components/FilterPanel.vue'
import ListingRow from '../components/ListingRow.vue'
import Pagination from '../components/Pagination.vue'

const SEARCH_DEBOUNCE_MS = 300

const route = useRoute()
const router = useRouter()

// Bütün arama durumu URL'de; sayfa yalnızca onu okur ve yazar.
const params = computed(() => routeQueryToParams(route.query))
const title = computed(() => `${typeLabels[params.value.type]} Daire`)

const items = ref([])
const total = ref(null)
const status = ref('loading') // loading | ready | error
const filters = ref({ cities: [], districtsByCity: {}, roomCounts: {} })
const filtersOpen = ref(false)
const searchText = ref(params.value.text ?? '')
const countFormat = new Intl.NumberFormat('tr-TR')

let listingsRequest = 0
let filtersRequest = 0
let searchTimer = null

async function load() {
  const id = ++listingsRequest
  status.value = 'loading'
  try {
    const data = await api.get('/api/listings', params.value)
    if (id !== listingsRequest) return // daha yeni bir istek başladı: bu cevap eski
    items.value = data?.items ?? []
    total.value = data?.total ?? 0
    status.value = 'ready'
  } catch {
    if (id !== listingsRequest) return
    status.value = 'error'
  }
}

// Sunucu sırası (ordinal) Türkçe harfleri sona atar (Çankaya, Şişli...); görüntüde Türkçe sıra kullanılır.
const byTr = (a, b) => a.localeCompare(b, 'tr')
const sortedTr = (list) => [...(list ?? [])].sort(byTr)

async function loadFilters(type) {
  const id = ++filtersRequest
  try {
    const data = await api.get('/api/listings/filters', { type })
    if (id !== filtersRequest) return
    filters.value = {
      cities: sortedTr(data?.cities),
      districtsByCity: Object.fromEntries(Object.entries(data?.districtsByCity ?? {}).map(([city, list]) => [city, sortedTr(list)])),
      roomCounts: data?.roomCounts ?? {}
    }
  } catch {
    if (id !== filtersRequest) return
    filters.value = { cities: [], districtsByCity: {}, roomCounts: {} }
  }
}

watch(
  () => JSON.stringify(params.value),
  () => {
    if (route.path !== '/') return // sayfadan ayrılırken boş sorguyla istek atma
    load()
  },
  { immediate: true }
)
watch(() => params.value.type, (type) => loadFilters(type), { immediate: true })

// Arama kutusu adres çubuğuyla (geri tuşu, Temizle) eşit kalır.
watch(
  () => params.value.text,
  (text) => {
    if ((text ?? '') !== searchText.value.trim()) searchText.value = text ?? ''
  }
)

onBeforeUnmount(() => {
  listingsRequest++
  filtersRequest++
  clearTimeout(searchTimer)
})

function writeQuery(next) {
  router.replace({ query: paramsToRouteQuery(next) })
}

// Süzgeç değişince her zaman 1. sayfaya dönülür.
function applyFilters(next) {
  writeQuery({ ...next, page: 1 })
}

function onSearchInput() {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => applyFilters({ ...params.value, text: searchText.value }), SEARCH_DEBOUNCE_MS)
}

function submitSearch() {
  clearTimeout(searchTimer)
  applyFilters({ ...params.value, text: searchText.value })
}

function onSort(event) {
  applyFilters({ ...params.value, sort: event.target.value })
}

function goToPage(page) {
  writeQuery({ ...params.value, page })
  window.scrollTo?.({ top: 0, behavior: 'smooth' })
}

function clearFilters() {
  clearTimeout(searchTimer)
  searchText.value = ''
  applyFilters({ type: params.value.type })
}

const activeFilterCount = computed(() => {
  const ignored = new Set(['type', 'page', 'pageSize', 'sort', 'text'])
  return Object.keys(params.value).filter((k) => !ignored.has(k)).length
})

const countText = computed(() => (total.value === null ? '' : `${countFormat.format(total.value)} ilan bulundu`))
const showSkeleton = computed(() => status.value === 'loading' && items.value.length === 0)
const showEmpty = computed(() => status.value === 'ready' && items.value.length === 0)
const showList = computed(() => status.value !== 'error' && items.value.length > 0)
</script>

<template>
  <div class="page">
    <button
      type="button"
      class="secondary toggle"
      aria-controls="filter-panel"
      :aria-expanded="filtersOpen ? 'true' : 'false'"
      @click="filtersOpen = !filtersOpen"
    >
      Filtreler<span v-if="activeFilterCount"> ({{ activeFilterCount }})</span>
    </button>

    <aside id="filter-panel" class="side" :class="{ open: filtersOpen }" aria-label="Filtreler paneli">
      <FilterPanel :model-value="params" :filters="filters" @update:model-value="applyFilters" />
    </aside>

    <section class="results">
      <nav class="breadcrumb" aria-label="Sayfa yolu">
        <ol>
          <li><router-link to="/">Ana Sayfa</router-link></li>
          <li aria-current="page">{{ title }}</li>
        </ol>
      </nav>

      <header class="head">
        <div class="headline">
          <h1>{{ title }}</h1>
          <p class="total" aria-live="polite">{{ countText }}</p>
        </div>
        <div class="tools">
          <form class="search" role="search" @submit.prevent="submitSearch">
            <label class="sr-only" for="search-box">Ara</label>
            <input
              id="search-box"
              v-model="searchText"
              type="search"
              placeholder="Kelime, semt veya ilan no ile ara"
              autocomplete="off"
              @input="onSearchInput"
            />
          </form>
          <label class="sort">
            <span class="sr-only">Sıralama</span>
            <select name="sort" :value="params.sort ?? 'Newest'" @change="onSort">
              <option v-for="(label, value) in sortLabels" :key="value" :value="value">{{ label }}</option>
            </select>
          </label>
        </div>
      </header>

      <div v-if="showSkeleton" class="list" aria-busy="true">
        <div v-for="n in 4" :key="n" class="skeleton" aria-hidden="true"></div>
        <p class="sr-only">İlanlar yükleniyor</p>
      </div>

      <div v-else-if="status === 'error'" class="notice" role="alert">
        <p><strong>İlanlar yüklenemedi</strong></p>
        <p>Bağlantınızı kontrol edip yeniden deneyin.</p>
        <button type="button" data-testid="retry" @click="load">Tekrar dene</button>
      </div>

      <div v-else-if="showEmpty" class="notice">
        <p><strong>Aramanıza uygun ilan bulunamadı</strong></p>
        <p>Filtreleri değiştirmeyi ya da temizlemeyi deneyin.</p>
        <button type="button" data-testid="clear-filters" @click="clearFilters">Filtreleri temizle</button>
      </div>

      <div v-if="showList" class="list" :class="{ stale: status === 'loading' }" :aria-busy="status === 'loading'">
        <ListingRow v-for="l in items" :key="l.id" :listing="l" />
      </div>

      <Pagination
        v-if="status === 'ready' && total"
        :page="params.page"
        :page-size="params.pageSize"
        :total="total"
        @update:page="goToPage"
      />
    </section>
  </div>
</template>

<style scoped>
.page {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1rem;
  padding: 1rem 0 2rem;
}
.toggle {
  justify-self: start;
}
.side {
  display: none;
}
.side.open {
  display: block;
}
.results {
  min-width: 0;
}
.breadcrumb ol {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  margin: 0 0 0.5rem;
  padding: 0;
  list-style: none;
  font-size: 0.875rem;
  color: var(--muted);
}
.breadcrumb li + li::before {
  content: '›';
  margin-right: 0.25rem;
}
.head {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 0.75rem 1rem;
  margin-bottom: 1rem;
}
h1 {
  margin: 0;
  font-size: clamp(1.25rem, 1rem + 1.2vw, 1.75rem);
}
.total {
  margin: 0;
  min-height: 1.5em;
  color: var(--muted);
}
.tools {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  flex: 1 1 18rem;
  justify-content: flex-end;
}
.search {
  flex: 1 1 14rem;
  max-width: 24rem;
}
.search input {
  width: 100%;
}
.sort select {
  min-width: 9rem;
}
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}
.list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  transition: opacity 0.15s;
}
.list.stale {
  opacity: 0.55;
  pointer-events: none;
}
.skeleton {
  min-height: 11rem;
  border-radius: var(--radius);
  background: linear-gradient(90deg, #f1f5f2 25%, #e5ece7 50%, #f1f5f2 75%);
  background-size: 200% 100%;
  animation: shimmer 1.4s infinite linear;
}
@keyframes shimmer {
  to {
    background-position: -200% 0;
  }
}
@media (prefers-reduced-motion: reduce) {
  .skeleton {
    animation: none;
  }
}
.notice {
  padding: 2rem 1rem;
  border: 1px dashed var(--border);
  border-radius: var(--radius);
  text-align: center;
}
.notice p {
  margin: 0 0 0.5rem;
}

@media (min-width: 900px) {
  .page {
    grid-template-columns: minmax(15rem, 18rem) 1fr;
    gap: 1.5rem;
    align-items: start;
  }
  .toggle {
    display: none;
  }
  .side,
  .side.open {
    display: block;
    grid-column: 1;
    grid-row: 1;
  }
  .results {
    grid-column: 2;
    grid-row: 1;
  }
}
</style>

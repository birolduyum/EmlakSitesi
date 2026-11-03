<script setup>
import { ref, computed, watch, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../../api.js'
import { isAdmin } from '../../auth.js'
import { adminRouteQueryToParams, adminParamsToRouteQuery } from '../../query.js'
import { sortLabels, statusLabels } from '../../labels.js'
import { useFilterPanelHost } from '../../composables/useFilterPanelHost.js'
import { useAdvisorList } from '../../composables/useAdvisorList.js'
import FilterPanel from '../../components/FilterPanel.vue'
import ListingRow from '../../components/ListingRow.vue'
import Pagination from '../../components/Pagination.vue'
import ConfirmDialog from '../../components/admin/ConfirmDialog.vue'
import '../../components/admin/adminForm.css'

const DEBOUNCE_MS = 300

const route = useRoute()
const router = useRouter()
const admin = isAdmin()

// Bütün arama durumu URL'de. Saha rolünde durum ve ev sahibi süzgeçleri yoktur (sunucu zaten yalnızca aktifleri verir).
const params = computed(() => {
  const p = adminRouteQueryToParams(route.query)
  if (!admin) {
    delete p.status
    delete p.ownerText
    delete p.advisorId
  }
  return p
})

const { filtersOpen, resultsEl, onApplied, activeFilterCount } = useFilterPanelHost(params)

// Danışman süzgeci yalnızca Yönetici'de (Saha danışman listesini okuyamaz).
const { advisors, load: loadAdvisors } = useAdvisorList()
if (admin) loadAdvisors()

const items = ref([])
const total = ref(null)
const state = ref('loading') // loading | ready | error
const filters = ref({ cities: [], districtsByCity: {}, roomCounts: {} })
const searchText = ref(params.value.text ?? '')
const toast = ref('')
const pending = ref(null) // { kind: 'delete', listing }
const countFormat = new Intl.NumberFormat('tr-TR')

let listRequest = 0
let filtersRequest = 0
let searchTimer = null

async function load() {
  const id = ++listRequest
  state.value = 'loading'
  try {
    const data = await api.get('/api/admin/listings', params.value)
    if (id !== listRequest) return // daha yeni bir istek başladı: bu cevap eski
    items.value = data?.items ?? []
    total.value = data?.total ?? 0
    state.value = 'ready'
  } catch {
    if (id !== listRequest) return
    state.value = 'error'
  }
}

// Süzgeç seçenekleri herkese açık uçtan gelir; tür seçilmemişse iki türün seçenekleri birleştirilir.
async function loadFilters(type) {
  const id = ++filtersRequest
  const types = type ? [type] : ['Satilik', 'Kiralik']
  try {
    const results = await Promise.all(types.map((t) => api.get('/api/listings/filters', { type: t })))
    if (id !== filtersRequest) return
    const merged = { cities: [], districtsByCity: {}, roomCounts: {} }
    for (const r of results) {
      for (const city of r?.cities ?? []) if (!merged.cities.includes(city)) merged.cities.push(city)
      for (const [city, list] of Object.entries(r?.districtsByCity ?? {})) {
        merged.districtsByCity[city] = [...new Set([...(merged.districtsByCity[city] ?? []), ...list])].sort((a, b) => a.localeCompare(b, 'tr'))
      }
      for (const [room, count] of Object.entries(r?.roomCounts ?? {})) merged.roomCounts[room] = (merged.roomCounts[room] ?? 0) + count
    }
    merged.cities.sort((a, b) => a.localeCompare(b, 'tr'))
    filters.value = merged
  } catch {
    if (id !== filtersRequest) return
    filters.value = { cities: [], districtsByCity: {}, roomCounts: {} }
  }
}

watch(
  () => JSON.stringify(params.value),
  () => {
    if (route.path !== '/admin') return // sayfadan ayrılırken boş sorguyla istek atma
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
  listRequest++
  filtersRequest++
  clearTimeout(searchTimer)
})

function writeQuery(next) {
  router.replace({ query: adminParamsToRouteQuery(next) })
}
// Süzgeç değişince her zaman 1. sayfaya dönülür.
function applyFilters(next) {
  writeQuery({ ...next, page: 1 })
}
function onSearchInput() {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => applyFilters({ ...params.value, text: searchText.value }), DEBOUNCE_MS)
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
  applyFilters({})
}

async function confirmDelete() {
  const listing = pending.value?.listing
  pending.value = null
  if (!listing) return
  try {
    await api.delete(`/api/admin/listings/${listing.id}`)
    toast.value = `"${listing.title}" silindi.`
    await load()
  } catch {
    toast.value = `"${listing.title}" silinemedi. Lütfen tekrar deneyin.`
  }
}

async function restore(listing) {
  try {
    await api.post(`/api/admin/listings/${listing.id}/restore`)
    toast.value = `"${listing.title}" geri alındı.`
    await load()
  } catch {
    toast.value = `"${listing.title}" geri alınamadı. Lütfen tekrar deneyin.`
  }
}

const countText = computed(() => (total.value === null ? '' : `${countFormat.format(total.value)} ilan bulundu`))
const showSkeleton = computed(() => state.value === 'loading' && items.value.length === 0)
const showEmpty = computed(() => state.value === 'ready' && items.value.length === 0)
const showList = computed(() => state.value !== 'error' && items.value.length > 0)
const deleteMessage = computed(
  () => `"${pending.value?.listing.title ?? ''}" ilanı silinecek. Ziyaretçilere görünmez olur; "Silinenler" filtresinden geri alabilirsiniz.`
)
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
      <FilterPanel :model-value="params" :filters="filters" all-label="Hepsi" @update:model-value="applyFilters" @applied="onApplied">
        <template v-if="admin" #extra="{ draft, set }">
          <div class="admin-filters af-field">
            <label for="status-filter">Durum</label>
            <select id="status-filter" name="status" :value="draft.status ?? 'Active'" @change="set('status', $event.target.value === 'Active' ? undefined : $event.target.value)">
              <option v-for="(label, value) in statusLabels" :key="value" :value="value">{{ label }}</option>
            </select>
            <label for="owner-filter">Ev sahibi (ad veya telefon)</label>
            <input id="owner-filter" name="ownerText" type="search" autocomplete="off" :value="draft.ownerText ?? ''" @input="set('ownerText', $event.target.value)" />
            <label for="advisor-filter">Danışman</label>
            <!-- Seçenekler sonradan (danışman listesi yüklenince) gelir; seçili durum her seçenekte açıkça işaretlenir. -->
            <select id="advisor-filter" name="advisorId" @change="set('advisorId', $event.target.value === '' ? undefined : Number($event.target.value))">
              <option value="" :selected="draft.advisorId === undefined">Hepsi</option>
              <option v-if="draft.advisorId !== undefined && !advisors.some((a) => a.id === draft.advisorId)" :value="String(draft.advisorId)" selected>Danışman no {{ draft.advisorId }}</option>
              <option v-for="a in advisors" :key="a.id" :value="String(a.id)" :selected="a.id === draft.advisorId">{{ a.name }}{{ a.isActive ? '' : ' (pasif)' }}</option>
            </select>
          </div>
        </template>
      </FilterPanel>
    </aside>

    <section ref="resultsEl" class="results">
      <header class="head">
        <div class="headline">
          <h1>İlan Yönetimi</h1>
          <p class="total" aria-live="polite">{{ countText }}</p>
        </div>
        <div class="tools">
          <router-link v-if="admin" to="/admin/ilan/yeni" class="btn new">Yeni ilan</router-link>
          <form class="search" role="search" @submit.prevent="submitSearch">
            <label class="af-sr" for="admin-search">Ara</label>
            <input id="admin-search" v-model="searchText" type="search" placeholder="Kelime, semt veya ilan no ile ara" autocomplete="off" @input="onSearchInput" />
          </form>
          <label class="sort">
            <span class="af-sr">Sıralama</span>
            <select name="sort" :value="params.sort ?? 'Newest'" @change="onSort">
              <option v-for="(label, value) in sortLabels" :key="value" :value="value">{{ label }}</option>
            </select>
          </label>
        </div>
      </header>

      <p class="toast" :class="{ empty: !toast }" aria-live="polite" data-testid="toast">{{ toast }}</p>

      <div v-if="showSkeleton" class="list" aria-busy="true">
        <div v-for="n in 4" :key="n" class="skeleton" aria-hidden="true"></div>
        <p class="af-sr">İlanlar yükleniyor</p>
      </div>

      <div v-else-if="state === 'error'" class="notice" role="alert">
        <p><strong>İlanlar yüklenemedi</strong></p>
        <p>Bağlantınızı kontrol edip yeniden deneyin.</p>
        <button type="button" data-testid="retry" @click="load">Tekrar dene</button>
      </div>

      <div v-else-if="showEmpty" class="notice">
        <p><strong>Aramanıza uygun ilan bulunamadı</strong></p>
        <p>Filtreleri değiştirmeyi ya da temizlemeyi deneyin.</p>
        <button type="button" data-testid="clear-filters" @click="clearFilters">Filtreleri temizle</button>
      </div>

      <div v-if="showList" class="list" :class="{ stale: state === 'loading' }" :aria-busy="state === 'loading'">
        <ListingRow
          v-for="l in items"
          :key="l.id"
          :listing="l"
          :link-to="admin ? `/admin/ilan/${l.id}` : null"
          :deleted="l.isDeleted === true"
          :show-advisor="false"
          :show-type="!params.type"
        >
          <template v-if="admin" #meta>
            <p v-if="l.owner" class="owner">Ev sahibi: {{ l.owner.name }} · {{ l.owner.phone }}</p>
            <p class="owner" data-testid="advisor-line">Danışman: {{ l.advisorName ?? 'varsayılan' }}</p>
          </template>
          <template v-if="admin" #actions>
            <div class="actions">
              <router-link :to="`/admin/ilan/${l.id}`" class="btn secondary">Düzenle</router-link>
              <button v-if="l.isDeleted" type="button" @click="restore(l)">Geri al</button>
              <button v-else type="button" class="danger" @click="pending = { kind: 'delete', listing: l }">Sil</button>
            </div>
          </template>
        </ListingRow>
      </div>

      <Pagination v-if="state === 'ready' && total" :page="params.page" :page-size="params.pageSize" :total="total" @update:page="goToPage" />
    </section>

    <ConfirmDialog
      :open="pending !== null"
      title="İlan silinsin mi?"
      :message="deleteMessage"
      confirm-label="Sil"
      danger
      @confirm="confirmDelete"
      @cancel="pending = null"
    />
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
.admin-filters {
  gap: 0.375rem;
}
.admin-filters label {
  font-weight: 600;
  color: var(--green-900);
}
.head {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 0.75rem 1rem;
  margin-bottom: 0.5rem;
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
  align-items: center;
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
.toast {
  min-height: 1.5em;
  margin: 0 0 0.5rem;
  color: var(--green-700);
  font-weight: 600;
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
.owner {
  color: var(--muted);
  font-size: 0.875rem;
}
.actions {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 0.375rem;
  text-align: center;
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

<script setup>
import { ref, computed, watch, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../../api.js'
import { isAdmin } from '../../auth.js'
import { formatDateTime } from '../../format.js'
import FormField from '../../components/admin/FormField.vue'
import '../../components/admin/adminForm.css'

const CAP = 500
const DATE_RE = /^\d{4}-\d{2}-\d{2}$/

const route = useRoute()
const router = useRouter()
const admin = isAdmin()

// URL sorgusu → API parametreleri (geçersiz parçalar düşer).
function fromQuery(query) {
  const one = (v) => (Array.isArray(v) ? v[0] : v)
  const p = {}
  const id = String(one(query.listingId) ?? '').trim()
  if (/^\d+$/.test(id) && Number(id) > 0) p.listingId = Number(id)
  const username = String(one(query.username) ?? '').trim()
  if (username) p.username = username
  for (const key of ['from', 'to']) {
    const v = String(one(query[key]) ?? '').trim()
    if (DATE_RE.test(v)) p[key] = v
  }
  return p
}

// Seçilen gün kullanıcının YEREL gününe göre kıstas olur: başlangıç yerel 00:00:00.000, bitiş yerel 23:59:59.999 (UTC anına çevrilir).
// Adres çubuğunda yalnızca gün (yyyy-MM-dd) kalır; sunucuya saatli an gider (sunucu iki sınırı da dahil sayar).
function localDayBounds(day, endOfDay) {
  const [y, m, d] = day.split('-').map(Number)
  const date = endOfDay ? new Date(y, m - 1, d, 23, 59, 59, 999) : new Date(y, m - 1, d, 0, 0, 0, 0)
  return date.toISOString()
}

function toApiParams(p) {
  const out = { ...p }
  if (out.from) out.from = localDayBounds(out.from, false)
  if (out.to) out.to = localDayBounds(out.to, true)
  return out
}

function toQuery(p) {
  const q = {}
  for (const [k, v] of Object.entries(p)) q[k] = String(v)
  return q
}

const params = computed(() => fromQuery(route.query))

const draft = ref({ listingId: '', username: '', from: '', to: '' })
const errors = ref({ listingId: '', range: '' })
const rows = ref([])
const state = ref('loading') // loading | ready | error
const blocked = ref(false) // geçersiz aralık: istek atılmaz

let requestId = 0

function syncDraft(p) {
  draft.value = { listingId: p.listingId ? String(p.listingId) : '', username: p.username ?? '', from: p.from ?? '', to: p.to ?? '' }
}

function rangeError(p) {
  return p.from && p.to && p.from > p.to ? 'Başlangıç tarihi bitiş tarihinden sonra olamaz.' : ''
}

async function load() {
  if (!admin) return
  const id = ++requestId
  const p = params.value
  errors.value = { listingId: '', range: rangeError(p) }
  if (errors.value.range) {
    blocked.value = true
    rows.value = []
    state.value = 'ready'
    return
  }
  blocked.value = false
  state.value = 'loading'
  try {
    const data = await api.get('/api/admin/showings', toApiParams(p))
    if (id !== requestId) return // daha yeni bir istek başladı: bu cevap eski
    rows.value = Array.isArray(data) ? data : []
    state.value = 'ready'
  } catch {
    if (id !== requestId) return
    state.value = 'error'
  }
}

watch(
  () => JSON.stringify(params.value),
  () => {
    if (route.path !== '/admin/rapor') return // sayfadan ayrılırken boş sorguyla istek atma
    syncDraft(params.value)
    load()
  },
  { immediate: true }
)

onBeforeUnmount(() => requestId++)

function apply(next) {
  const same = JSON.stringify(next) === JSON.stringify(params.value)
  if (same) {
    load() // aynı süzgeç: adres değişmez, yine de yenile
    return
  }
  router.replace({ query: toQuery(next) })
}

function submit() {
  const idText = draft.value.listingId.trim()
  const next = {}
  const e = { listingId: '', range: '' }
  if (idText !== '') {
    if (/^\d+$/.test(idText) && Number(idText) > 0) next.listingId = Number(idText)
    else e.listingId = 'İlan no pozitif bir sayı olmalı.'
  }
  const username = draft.value.username.trim()
  if (username) next.username = username
  if (draft.value.from) next.from = draft.value.from
  if (draft.value.to) next.to = draft.value.to
  e.range = rangeError(next)
  errors.value = e
  if (e.listingId || e.range) return // geçersiz süzgeç: istek atılmaz, adres değişmez
  apply(next)
}

function clear() {
  draft.value = { listingId: '', username: '', from: '', to: '' }
  errors.value = { listingId: '', range: '' }
  apply({})
}

const perUser = computed(() => {
  const counts = new Map()
  for (const r of rows.value) counts.set(r.username, (counts.get(r.username) ?? 0) + 1)
  return [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0], 'tr'))
})
const capped = computed(() => rows.value.length >= CAP)
</script>

<template>
  <section v-if="admin" class="page">
    <h1>Gösterme Raporu</h1>

    <form class="filters af-section" novalidate @submit.prevent="submit">
      <div class="af-grid">
        <FormField name="listingId" label="İlan no" :error="errors.listingId">
          <template #default="{ attrs }">
            <input v-bind="attrs" v-model="draft.listingId" type="text" inputmode="numeric" autocomplete="off" />
          </template>
        </FormField>
        <FormField name="username" label="Kullanıcı">
          <template #default="{ attrs }">
            <input v-bind="attrs" v-model="draft.username" type="text" autocomplete="off" />
          </template>
        </FormField>
        <FormField name="from" label="Başlangıç">
          <template #default="{ attrs }">
            <input v-bind="attrs" v-model="draft.from" type="date" />
          </template>
        </FormField>
        <FormField name="to" label="Bitiş">
          <template #default="{ attrs }">
            <input v-bind="attrs" v-model="draft.to" type="date" />
          </template>
        </FormField>
      </div>
      <p class="af-hint">Tarihler bulunduğunuz saat dilimine göre aranır.</p>
      <p v-if="errors.range" class="af-error range" role="alert">{{ errors.range }}</p>
      <div class="buttons">
        <button type="submit">Filtrele</button>
        <button type="button" class="secondary" @click="clear">Temizle</button>
      </div>
    </form>

    <div v-if="state === 'error'" class="notice" role="alert">
      <p><strong>Rapor yüklenemedi</strong></p>
      <p>Bağlantınızı kontrol edip yeniden deneyin.</p>
      <button type="button" data-testid="retry" @click="load">Tekrar dene</button>
    </div>

    <template v-else-if="!blocked">
      <p v-if="state === 'loading'" class="loading" aria-busy="true">Rapor yükleniyor…</p>

      <div v-else-if="rows.length === 0" class="notice">
        <p><strong>Kayıt bulunamadı</strong></p>
        <p>Süzgeçleri değiştirmeyi ya da temizlemeyi deneyin.</p>
      </div>

      <template v-else>
        <p class="total" aria-live="polite">{{ rows.length }} kayıt</p>
        <p v-if="capped" class="cap">İlk {{ CAP }} kayıt gösteriliyor, filtreleri daraltın.</p>

        <div class="summary" data-testid="summary">
          <p class="summary-title">Kullanıcıya göre (gösterilen kayıtlar içinde)</p>
          <ul>
            <li v-for="[name, count] in perUser" :key="name">
              <strong>{{ name }}</strong>: {{ count }}
            </li>
          </ul>
        </div>

        <table class="report">
          <caption class="af-sr">Gösterme kayıtları, en yeniden eskiye</caption>
          <thead>
            <tr>
              <th scope="col">İlan</th>
              <th scope="col">Kim</th>
              <th scope="col">Alındığı zaman</th>
              <th scope="col">İade zamanı</th>
              <th scope="col">Not</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in rows" :key="r.id">
              <td data-label="İlan"><router-link :to="`/admin/ilan/${r.listingId}`">#{{ r.listingId }} {{ r.listingTitle }}</router-link></td>
              <td data-label="Kim">{{ r.username }}</td>
              <td data-label="Alındığı zaman">{{ formatDateTime(r.takenAt) }}</td>
              <td data-label="İade zamanı">
                <span v-if="r.returnedAt === null || r.returnedAt === undefined" class="badge-open">Açık</span>
                <template v-else>{{ formatDateTime(r.returnedAt) }}</template>
              </td>
              <td data-label="Not" class="note">{{ r.note ?? '' }}</td>
            </tr>
          </tbody>
        </table>
      </template>
    </template>
  </section>
</template>

<style scoped>
.page {
  padding: 1rem 0 2rem;
}
h1 {
  margin: 0 0 1rem;
  font-size: clamp(1.25rem, 1rem + 1.2vw, 1.75rem);
}
.buttons {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.75rem;
}
.range {
  margin-top: 0.5rem;
}
.loading {
  color: var(--muted);
}
.total {
  margin: 0 0 0.25rem;
  font-weight: 700;
}
.cap {
  margin: 0 0 0.5rem;
  color: var(--danger);
  font-weight: 600;
}
.summary {
  margin: 0 0 1rem;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--green-50);
}
.summary-title {
  margin: 0 0 0.25rem;
  color: var(--muted);
  font-size: 0.875rem;
}
.summary ul {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem 1.25rem;
  margin: 0;
  padding: 0;
  list-style: none;
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
.report {
  width: 100%;
  border-collapse: collapse;
  background: #fff;
}
.report th,
.report td {
  padding: 0.5rem 0.625rem;
  border-bottom: 1px solid var(--border);
  text-align: left;
  vertical-align: top;
}
.report th {
  background: var(--green-50);
  color: var(--green-900);
  font-size: 0.875rem;
}
.note {
  overflow-wrap: anywhere;
}
.badge-open {
  display: inline-block;
  padding: 0.0625rem 0.5rem;
  border-radius: 999px;
  background: var(--green-100);
  color: var(--green-900);
  font-size: 0.8125rem;
  font-weight: 600;
}

/* Dar ekranda satırlar etiket/değer blokları olur; yatay kaydırma yok. */
@media (max-width: 40rem) {
  .report,
  .report tbody,
  .report tr,
  .report td {
    display: block;
    width: 100%;
  }
  .report thead {
    position: absolute;
    width: 1px;
    height: 1px;
    overflow: hidden;
    clip: rect(0 0 0 0);
  }
  .report tr {
    margin-bottom: 0.75rem;
    border: 1px solid var(--border);
    border-radius: var(--radius);
    padding: 0.25rem 0;
  }
  .report td {
    display: flex;
    gap: 0.75rem;
    border-bottom: 0;
    padding: 0.25rem 0.75rem;
  }
  .report td::before {
    content: attr(data-label);
    flex: 0 0 7.5rem;
    color: var(--muted);
    font-size: 0.875rem;
  }
}
</style>

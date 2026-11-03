<script setup>
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { api } from '../../api.js'
import { isAdmin } from '../../auth.js'
import { useAsyncAction } from '../../composables/useAsyncAction.js'
import ListingPicker from '../../components/admin/ListingPicker.vue'
import OpenShowingItem from '../../components/admin/OpenShowingItem.vue'
import '../../components/admin/adminForm.css'

const admin = isAdmin()

const selected = ref(null)
const records = ref([])
const listState = ref('loading') // loading | ready | error
const status = ref('')

const take = useAsyncAction(() => api.post('/api/showings', { listingId: selected.value.id }), {
  byStatus: {
    404: 'İlan bulunamadı veya silinmiş.',
    409: 'Bu ilan için zaten açık bir kaydınız var.'
  }
})

let loadId = 0
async function loadMine(quiet = false) {
  const id = ++loadId
  if (quiet !== true) listState.value = 'loading'
  try {
    const data = await api.get('/api/showings/mine')
    if (id !== loadId) return
    records.value = Array.isArray(data) ? data : []
    listState.value = 'ready'
  } catch {
    if (id !== loadId) return
    if (quiet !== true) listState.value = 'error'
  }
}
onMounted(() => loadMine())
onBeforeUnmount(() => loadId++)

async function takeKey() {
  if (!selected.value) return
  status.value = ''
  const result = await take.run()
  if (!result?.ok) return
  const rec = result.data
  loadId++ // uçuştaki ilk /mine cevabı yeni kaydı ezmesin
  selected.value = null
  records.value = [rec, ...records.value.filter((r) => r.id !== rec.id)]
  if (listState.value !== 'ready') listState.value = 'ready'
  status.value = `${rec.listingTitle} için kayıt açıldı.`
}

function onStale(rec) {
  status.value = `${rec.listingTitle} kaydı zaten kapatılmıştı; liste yenilendi.`
  loadMine(true)
}

function onClosed(rec) {
  records.value = records.value.filter((r) => r.id !== rec.id)
  status.value = `${rec.listingTitle} için kayıt kapatıldı.`
}
</script>

<template>
  <section class="page">
    <h1>Gösterim</h1>

    <div class="af-section">
      <h2>Anahtarı aldım</h2>
      <p class="af-note">Gösterime gideceğiniz ilanı seçin; kaydınız anında açılır.</p>
      <ListingPicker v-model="selected" />
      <p v-if="take.error.value" class="af-error msg" role="alert">{{ take.error.value }}</p>
      <button type="button" class="take" :disabled="!selected || take.pending.value" @click="takeKey">Anahtarı aldım / gösterime gidiyorum</button>
    </div>

    <p class="status" :class="{ empty: !status }" aria-live="polite" data-testid="status">{{ status }}</p>

    <div class="af-section">
      <h2>Açık kayıtlarım</h2>
      <p v-if="listState === 'loading'" class="af-note" aria-busy="true">Yükleniyor…</p>
      <div v-else-if="listState === 'error'" role="alert">
        <p class="af-error">Açık kayıtlar yüklenemedi.</p>
        <button type="button" class="secondary" @click="loadMine()">Tekrar dene</button>
      </div>
      <p v-else-if="records.length === 0" class="empty-note">Açık kaydınız yok</p>
      <ul v-else class="recs">
        <OpenShowingItem v-for="r in records" :key="r.id" :record="r" :admin="admin" @closed="onClosed" @stale="onStale" />
      </ul>
    </div>
  </section>
</template>

<style scoped>
.page {
  padding: 1rem 0 2rem;
  max-width: 48rem;
}
h1 {
  margin: 0 0 1rem;
  font-size: clamp(1.25rem, 1rem + 1.2vw, 1.75rem);
}
.take {
  margin-top: 0.75rem;
}
.take:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.msg {
  margin-top: 0.5rem;
}
.status {
  min-height: 1.5em;
  margin: 0 0 0.75rem;
  color: var(--green-700);
  font-weight: 600;
}
.recs {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.empty-note {
  margin: 0;
  color: var(--muted);
}
</style>

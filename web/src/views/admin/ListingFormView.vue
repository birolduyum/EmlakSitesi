<script setup>
import { ref, computed, watch, nextTick, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter, onBeforeRouteLeave, onBeforeRouteUpdate } from 'vue-router'
import { api } from '../../api.js'
import { isAdmin, isLoggingOut } from '../../auth.js'
import { formatDate } from '../../format.js'
import { useListingForm, toInput, validate, fieldOfError } from '../../composables/useListingForm.js'
import FormSectionBasics from '../../components/admin/FormSectionBasics.vue'
import FormSectionLocation from '../../components/admin/FormSectionLocation.vue'
import FormSectionFeatures from '../../components/admin/FormSectionFeatures.vue'
import FormSectionTags from '../../components/admin/FormSectionTags.vue'
import FormSectionDescription from '../../components/admin/FormSectionDescription.vue'
import FormSectionOwner from '../../components/admin/FormSectionOwner.vue'
import PhotoManager from '../../components/admin/PhotoManager.vue'
import ConfirmDialog from '../../components/admin/ConfirmDialog.vue'
import '../../components/admin/adminForm.css'

const route = useRoute()
const router = useRouter()
const allowed = isAdmin() // yetki API'de de zorlanır; Saha buraya gelirse listeye döner

const { form, dirty, markClean, load, reset } = useListingForm()

const id = computed(() => (route.params.id === undefined ? null : Number(route.params.id)))
const isNew = computed(() => id.value === null)

const state = ref('loading') // loading | ready | notfound | error
const detail = ref(null)
const photos = ref([])
const tags = ref([])
const tagsFailed = ref(false)
const saving = ref(false)
const summary = ref([])
const fieldErrors = ref({})
const status = ref('')
const summaryEl = ref(null)
const deleteOpen = ref(false)
const leaveOpen = ref(false)

const readOnly = computed(() => detail.value?.isDeleted === true)
const heading = computed(() => (isNew.value ? 'Yeni ilan' : readOnly.value ? 'İlan (silinmiş)' : 'İlanı düzenle'))

let detailRequest = 0

function clearMessages() {
  summary.value = []
  fieldErrors.value = {}
  status.value = ''
}

async function loadDetail() {
  const mine = ++detailRequest
  // Sayfa zaten açıksa (silme/geri alma sonrası yenileme) form kaybolmaz; yalnızca ilk yüklemede "Yükleniyor" görünür.
  if (!detail.value) state.value = 'loading'
  clearMessages()
  try {
    const data = await api.get(`/api/admin/listings/${id.value}`)
    if (mine !== detailRequest) return
    detail.value = data
    photos.value = data?.photos ?? []
    load(data)
    state.value = 'ready'
  } catch (e) {
    if (mine !== detailRequest) return
    state.value = e?.status === 404 ? 'notfound' : 'error'
  }
}

async function loadTags() {
  try {
    tags.value = (await api.get('/api/tags')) ?? []
    tagsFailed.value = false
  } catch {
    tagsFailed.value = true
  }
}

function init() {
  if (!allowed) return
  detailRequest++
  clearMessages()
  detail.value = null
  photos.value = []
  if (isNew.value) {
    reset()
    state.value = 'ready'
  } else if (!Number.isInteger(id.value)) {
    state.value = 'notfound'
  } else {
    loadDetail()
  }
}

if (!allowed) {
  router.replace('/admin')
} else {
  loadTags()
  watch(id, init, { immediate: true })
}

// Fotoğraf yükleme/silme sonrası sunucudaki gerçek listeyi okur; formdaki yazılanlara dokunmaz.
async function refreshPhotos() {
  try {
    const data = await api.get(`/api/admin/listings/${id.value}`)
    photos.value = data?.photos ?? []
  } catch {
    status.value = 'Fotoğraf listesi yenilenemedi, sayfayı yenileyin.'
  }
}

function onTagCreated(tag) {
  if (!tags.value.some((t) => t.id === tag.id)) tags.value = [...tags.value, tag]
}

async function showSummary(messages) {
  summary.value = messages
  await nextTick()
  summaryEl.value?.focus()
}

function applyServerErrors(messages) {
  const map = {}
  for (const m of messages) {
    const field = fieldOfError(m)
    if (field && !map[field]) map[field] = m
  }
  fieldErrors.value = map
  return showSummary(messages)
}

async function save() {
  if (saving.value || readOnly.value || state.value !== 'ready') return
  clearMessages()
  const clientErrors = validate(form)
  if (Object.keys(clientErrors).length > 0) {
    fieldErrors.value = clientErrors
    await showSummary(Object.values(clientErrors))
    return
  }
  saving.value = true
  try {
    if (isNew.value) {
      const created = await api.post('/api/admin/listings', toInput(form))
      markClean()
      await router.replace(`/admin/ilan/${created.id}`)
    } else {
      const data = await api.put(`/api/admin/listings/${id.value}`, toInput(form))
      detail.value = data
      photos.value = data?.photos ?? photos.value
      load(data)
      status.value = 'Kaydedildi.'
    }
  } catch (e) {
    if (e?.status === 400 && Array.isArray(e.body?.errors) && e.body.errors.length) await applyServerErrors(e.body.errors)
    else if (e?.status === 404) await showSummary(['Bu ilan bulunamadı ya da silinmiş olabilir. Sayfayı yenileyip tekrar deneyin.'])
    else if (e?.status === 0) await showSummary(['Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.'])
    else await showSummary(['İlan kaydedilemedi. Lütfen tekrar deneyin.'])
  } finally {
    saving.value = false
  }
}

async function confirmDelete() {
  deleteOpen.value = false
  try {
    await api.delete(`/api/admin/listings/${id.value}`)
    await loadDetail()
    status.value = 'İlan silindi. İstediğiniz zaman "Geri al" ile geri getirebilirsiniz.'
  } catch {
    await showSummary(['İlan silinemedi. Lütfen tekrar deneyin.'])
  }
}

async function restore() {
  try {
    await api.post(`/api/admin/listings/${id.value}/restore`)
    await loadDetail()
    status.value = 'İlan geri alındı.'
  } catch {
    await showSummary(['İlan geri alınamadı. Lütfen tekrar deneyin.'])
  }
}

// Kaydedilmemiş değişiklikte sayfadan ayrılmadan önce sorulur (uygulama içi gezinme + sekme kapatma/yenileme).
let leaveResolver = null
function guardLeave() {
  if (isLoggingOut() || !dirty.value || saving.value) return true
  // Yeni bir gezinme gelirse öncekinin bekleyen sorusu iptal sayılır (asılı kalmasın).
  if (leaveResolver) {
    const previous = leaveResolver
    leaveResolver = null
    previous(false)
  }
  leaveOpen.value = true
  return new Promise((resolve) => {
    leaveResolver = resolve
  })
}
function answerLeave(ok) {
  leaveOpen.value = false
  const resolve = leaveResolver
  leaveResolver = null
  resolve?.(ok)
}
if (allowed) {
  onBeforeRouteLeave(guardLeave)
  onBeforeRouteUpdate(guardLeave)
}

function onBeforeUnload(event) {
  if (!dirty.value) return
  event.preventDefault()
  event.returnValue = ''
}
onMounted(() => window.addEventListener('beforeunload', onBeforeUnload))
onBeforeUnmount(() => {
  window.removeEventListener('beforeunload', onBeforeUnload)
  detailRequest++
})

const audit = computed(() => {
  const d = detail.value
  if (!d) return []
  const line = (label, by, at) => `${label} ${by ?? '-'}, ${formatDate(at)}`
  const rows = [line('Oluşturan', d.createdBy, d.createdAt)]
  if (d.updatedBy) rows.push(line('Son güncelleyen', d.updatedBy, d.updatedAt))
  if (d.deletedBy) rows.push(line('Silen', d.deletedBy, d.deletedAt))
  if (d.restoredBy) rows.push(line('Geri alan', d.restoredBy, d.restoredAt))
  return rows
})

const deleteMessage = computed(() => {
  const name = `"${detail.value?.title ?? ''}"`
  const lost = dirty.value ? ' Kaydedilmemiş değişiklikleriniz kaybolur.' : ''
  return `${name} ilanı silinecek. Ziyaretçilere görünmez olur; "Silinenler" filtresinden geri alabilirsiniz.${lost}`
})
</script>

<template>
  <div v-if="allowed" class="page">
    <nav class="crumbs" aria-label="Sayfa yolu">
      <router-link to="/admin">İlanlar</router-link>
      <span aria-hidden="true"> › </span>
      <span aria-current="page">{{ heading }}</span>
    </nav>
    <h1>{{ heading }}</h1>

    <div v-if="state === 'loading'" class="notice" aria-busy="true" data-testid="loading">Yükleniyor…</div>
    <div v-else-if="state === 'notfound'" class="notice" role="alert">
      <p><strong>İlan bulunamadı</strong></p>
      <router-link to="/admin" class="btn">İlanlara dön</router-link>
    </div>
    <div v-else-if="state === 'error'" class="notice" role="alert">
      <p><strong>İlan yüklenemedi</strong></p>
      <button type="button" data-testid="retry" @click="init">Tekrar dene</button>
    </div>

    <template v-else>
      <p class="status" aria-live="polite" data-testid="status">{{ status }}</p>

      <section v-if="!isNew && audit.length" class="audit" aria-label="Kayıt bilgisi" data-testid="audit">
        <ul>
          <li v-for="row in audit" :key="row">{{ row }}</li>
        </ul>
      </section>

      <form novalidate @submit.prevent="save">
        <div v-if="summary.length" ref="summaryEl" class="summary" role="alert" tabindex="-1">
          <p><strong>İlan kaydedilemedi:</strong></p>
          <ul>
            <li v-for="(m, i) in summary" :key="i">{{ m }}</li>
          </ul>
        </div>

        <p v-if="readOnly" class="readonly-note">Bu ilan silinmiş; düzenlemek için önce geri alın.</p>

        <fieldset class="af-plain" :disabled="readOnly">
          <FormSectionBasics :form="form" :errors="fieldErrors" />
          <FormSectionLocation :form="form" :errors="fieldErrors" />
          <FormSectionFeatures :form="form" :errors="fieldErrors" />
          <FormSectionTags :form="form" :tags="tags" :load-failed="tagsFailed" @created="onTagCreated" />
          <FormSectionDescription :form="form" :errors="fieldErrors" />
          <FormSectionOwner :form="form" :errors="fieldErrors" />
        </fieldset>

        <div class="actions">
          <button v-if="!readOnly" type="submit" :disabled="saving">{{ saving ? 'Kaydediliyor…' : isNew ? 'İlanı oluştur' : 'Kaydet' }}</button>
          <button v-if="!isNew && !readOnly" type="button" class="danger" data-testid="delete" @click="deleteOpen = true">Sil</button>
          <button v-if="!isNew && readOnly" type="button" data-testid="restore" @click="restore">Geri al</button>
          <router-link to="/admin" class="btn secondary">İlanlara dön</router-link>
        </div>
      </form>

      <PhotoManager v-if="!isNew && !readOnly" :listing-id="id" :photos="photos" @changed="refreshPhotos" />
      <section v-else-if="isNew" class="af-section" aria-labelledby="sec-photos-hint">
        <h2 id="sec-photos-hint">Fotoğraflar</h2>
        <p class="af-note">Fotoğraf eklemek için önce ilanı oluşturun; kayıttan sonra bu sayfada fotoğraf yükleyebilirsiniz.</p>
      </section>
    </template>

    <ConfirmDialog
      :open="deleteOpen"
      title="İlan silinsin mi?"
      :message="deleteMessage"
      confirm-label="Sil"
      danger
      @confirm="confirmDelete"
      @cancel="deleteOpen = false"
    />
    <ConfirmDialog
      :open="leaveOpen"
      title="Kaydedilmemiş değişiklikler var"
      message="Sayfadan ayrılırsanız yaptığınız değişiklikler kaybolur."
      confirm-label="Ayrıl"
      cancel-label="Sayfada kal"
      danger
      @confirm="answerLeave(true)"
      @cancel="answerLeave(false)"
    />
  </div>
</template>

<style scoped>
.page {
  padding: 1rem 0 2rem;
  max-width: 62rem;
}
.crumbs {
  font-size: 0.875rem;
  color: var(--muted);
}
h1 {
  margin: 0.25rem 0 1rem;
  font-size: clamp(1.25rem, 1rem + 1.2vw, 1.75rem);
}
.status {
  min-height: 1.5em;
  margin: 0 0 0.5rem;
  color: var(--green-700);
  font-weight: 600;
}
.audit {
  margin-bottom: 1rem;
  padding: 0.75rem 1rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--green-50);
  font-size: 0.9375rem;
}
.audit ul {
  margin: 0;
  padding-left: 1.25rem;
}
.summary {
  margin-bottom: 1rem;
  padding: 0.75rem 1rem;
  border: 1px solid var(--danger);
  border-radius: var(--radius);
  background: #fef2f2;
  color: var(--danger);
}
.summary p {
  margin: 0 0 0.25rem;
}
.summary ul {
  margin: 0;
  padding-left: 1.25rem;
}
.readonly-note {
  padding: 0.5rem 0.75rem;
  border-radius: var(--radius);
  background: #fef9c3;
}
.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin: 1rem 0 1.5rem;
}
.notice {
  padding: 2rem 1rem;
  border: 1px dashed var(--border);
  border-radius: var(--radius);
  text-align: center;
}
button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>

<script setup>
import { ref, computed, nextTick, onMounted, onBeforeUnmount } from 'vue'
import { api, ApiError } from '../../api.js'
import { isAdmin } from '../../auth.js'
import { formatDateTime } from '../../format.js'
import { sortAdvisors } from '../../composables/useAdvisorList.js'
import { errorMessage } from '../../composables/useAsyncAction.js'
import ConfirmDialog from '../../components/admin/ConfirmDialog.vue'
import '../../components/admin/adminForm.css'

// Danışmanlar: ilanlarda görünen kişiler. Kalıcı silme yoktur (ilanlar danışmana bağlıdır); yalnızca pasifleştirilir.
const admin = isAdmin()

const state = ref('loading') // loading | ready | error
const advisors = ref([])
const status = ref('')
const summary = ref([])
const busy = ref(false) // çift gönderim kilidi: eylem başlarken eşzamanlı olarak kurulur
const editingId = ref(null)
const draft = ref({ name: '', phone: '', whatsApp: '' })
const submitted = ref(false)
const pendingDeactivate = ref(null)
const nameInput = ref(null)
const summaryEl = ref(null)

let loadId = 0

async function load() {
  const id = ++loadId
  if (advisors.value.length === 0) state.value = 'loading'
  try {
    const data = await api.get('/api/admin/advisors')
    if (id !== loadId) return // daha yeni bir istek başladı: bu cevap eski
    advisors.value = sortAdvisors(data)
    state.value = 'ready'
  } catch {
    if (id !== loadId) return
    state.value = 'error'
  }
}
onMounted(() => {
  if (admin) load()
})
onBeforeUnmount(() => loadId++)

const PHONE_CHARS = /^[0-9\s+()-]+$/
const digitCount = (v) => v.replace(/\D/g, '').length
const isPhoneLike = (v) => PHONE_CHARS.test(v) && digitCount(v) >= 7 && digitCount(v) <= 15
const isHttpUrl = (v) => {
  try {
    const u = new URL(v)
    return u.protocol === 'http:' || u.protocol === 'https:'
  } catch {
    return false
  }
}

// Hızlı geri bildirim; son söz sunucudadır. İletilerde sayı yoktur (Türkçe, sade).
const fieldErrors = computed(() => {
  const d = draft.value
  const e = {}
  const name = d.name.trim()
  const phone = d.phone.trim()
  const wa = d.whatsApp.trim()
  if (!name) e.name = 'Ad boş olamaz.'
  if (!phone) e.phone = 'Telefon boş olamaz.'
  else if (!isPhoneLike(phone)) e.phone = 'Telefon geçerli bir numara olmalı; yalnızca rakam, boşluk, artı, parantez ve tire kullanın.'
  if (wa && !isHttpUrl(wa) && !isPhoneLike(wa)) e.whatsApp = 'WhatsApp, geçerli bir numara ya da http(s) bağlantısı olmalı.'
  return e
})
const shown = computed(() => (submitted.value ? fieldErrors.value : {}))

function resetForm() {
  editingId.value = null
  draft.value = { name: '', phone: '', whatsApp: '' }
  submitted.value = false
  summary.value = []
}

async function startEdit(a) {
  if (busy.value) return
  status.value = ''
  summary.value = []
  submitted.value = false
  editingId.value = a.id
  draft.value = { name: a.name, phone: a.phone, whatsApp: a.whatsApp ?? '' }
  await nextTick()
  nameInput.value?.focus()
}

function cancelEdit() {
  resetForm()
  status.value = ''
}

async function showSummary(messages) {
  summary.value = messages
  await nextTick()
  summaryEl.value?.focus()
}

function serverMessages(e) {
  if (e instanceof ApiError && e.status === 400 && Array.isArray(e.body?.errors) && e.body.errors.length) return e.body.errors
  if (e?.status === 404) return ['Bu danışman bulunamadı. Listeyi yenileyip tekrar deneyin.']
  return [errorMessage(e)]
}

async function save() {
  if (busy.value) return
  busy.value = true
  try {
    status.value = ''
    summary.value = []
    submitted.value = true
    const errors = Object.values(fieldErrors.value)
    if (errors.length) {
      await showSummary(errors)
      return
    }
    const d = draft.value
    const body = { name: d.name.trim(), phone: d.phone.trim(), whatsApp: d.whatsApp.trim() || null }
    const wasEdit = editingId.value !== null
    try {
      if (wasEdit) await api.put(`/api/admin/advisors/${editingId.value}`, body)
      else await api.post('/api/admin/advisors', body)
    } catch (e) {
      await showSummary(serverMessages(e))
      return
    }
    resetForm()
    status.value = wasEdit ? `"${body.name}" güncellendi.` : `"${body.name}" eklendi.`
    await load()
  } finally {
    busy.value = false
  }
}

function askDeactivate(a) {
  if (busy.value) return
  pendingDeactivate.value = a
}

const deactivateMessage = computed(() => {
  const n = pendingDeactivate.value?.listingCount ?? 0
  return `Bu danışmanın ${n} ilanı var; ilanlarda görünmeye devam eder, yeni ilana atanamaz.`
})

async function setActive(a, active) {
  if (busy.value) return
  busy.value = true
  try {
    status.value = ''
    summary.value = []
    try {
      await api.post(`/api/admin/advisors/${a.id}/${active ? 'activate' : 'deactivate'}`)
    } catch (e) {
      await showSummary(serverMessages(e))
      return
    }
    status.value = active ? `"${a.name}" etkinleştirildi.` : `"${a.name}" pasifleştirildi.`
    await load()
  } finally {
    busy.value = false
  }
}

async function confirmDeactivate() {
  const a = pendingDeactivate.value
  pendingDeactivate.value = null
  if (a) await setActive(a, false)
}

const auditLine = (a) => {
  const parts = [`Ekleyen ${a.createdBy || '-'}, ${formatDateTime(a.createdAt)}`]
  if (a.updatedBy) parts.push(`Son güncelleyen ${a.updatedBy}, ${formatDateTime(a.updatedAt)}`)
  return parts.join(' · ')
}
const count = (n) => `${n ?? 0} ilan`
</script>

<template>
  <section v-if="admin" class="page">
    <h1>Danışmanlar</h1>
    <p class="explain">
      İlanlarda görünen danışmanlar. Bir ilana danışman atanmazsa sitedeki varsayılan danışman görünür. Danışman bilgileri ziyaretçiye açıktır.
    </p>

    <form class="af-section form" novalidate aria-labelledby="advisor-form-title" @submit.prevent="save">
      <h2 id="advisor-form-title">{{ editingId === null ? 'Yeni danışman' : 'Danışmanı düzenle' }}</h2>

      <div v-if="summary.length" ref="summaryEl" class="af-error summary" role="alert" tabindex="-1" data-testid="summary">
        <p><strong>Danışman kaydedilemedi:</strong></p>
        <p v-for="(m, i) in summary" :key="i">{{ m }}</p>
      </div>

      <div class="af-grid">
        <div class="af-field">
          <label for="adv-name">Ad soyad <span aria-hidden="true">*</span></label>
          <input
            id="adv-name"
            ref="nameInput"
            v-model="draft.name"
            name="name"
            type="text"
            maxlength="100"
            autocomplete="off"
            aria-required="true"
            :aria-invalid="shown.name ? 'true' : undefined"
            :aria-describedby="shown.name ? 'adv-name-err' : undefined"
          />
          <p v-if="shown.name" id="adv-name-err" class="af-error">{{ shown.name }}</p>
        </div>
        <div class="af-field">
          <label for="adv-phone">Telefon <span aria-hidden="true">*</span></label>
          <input
            id="adv-phone"
            v-model="draft.phone"
            name="phone"
            type="tel"
            maxlength="30"
            autocomplete="off"
            aria-required="true"
            :aria-invalid="shown.phone ? 'true' : undefined"
            :aria-describedby="shown.phone ? 'adv-phone-err' : undefined"
          />
          <p v-if="shown.phone" id="adv-phone-err" class="af-error">{{ shown.phone }}</p>
        </div>
        <div class="af-field af-wide">
          <label for="adv-wa">WhatsApp (isteğe bağlı)</label>
          <input
            id="adv-wa"
            v-model="draft.whatsApp"
            name="whatsApp"
            type="text"
            maxlength="200"
            autocomplete="off"
            aria-describedby="adv-wa-hint adv-wa-err"
            :aria-invalid="shown.whatsApp ? 'true' : undefined"
          />
          <p id="adv-wa-hint" class="af-hint">
            Boş bırakılırsa telefon numarasından türetilir. WhatsApp'ta doğrudan arama bağlantısı yoktur; düğme sohbeti açar.
          </p>
          <p id="adv-wa-err" class="af-error" :hidden="!shown.whatsApp">{{ shown.whatsApp }}</p>
        </div>
      </div>

      <div class="form-actions">
        <button type="submit" :disabled="busy" data-testid="save">{{ editingId === null ? 'Danışman ekle' : 'Kaydet' }}</button>
        <button v-if="editingId !== null" type="button" class="secondary" data-testid="cancel-edit" @click="cancelEdit">Vazgeç</button>
      </div>
    </form>

    <p class="status" :class="{ empty: !status }" aria-live="polite" data-testid="status">{{ status }}</p>

    <p v-if="state === 'loading'" class="muted" aria-busy="true">Danışmanlar yükleniyor…</p>

    <div v-else-if="state === 'error'" class="notice" role="alert">
      <p><strong>Danışmanlar yüklenemedi</strong></p>
      <button type="button" data-testid="retry" @click="load">Tekrar dene</button>
    </div>

    <div v-else-if="advisors.length === 0" class="notice" data-testid="empty">
      <p><strong>Henüz danışman eklenmedi</strong></p>
      <p>Yukarıdaki formla ilk danışmanı ekleyin. Eklenene kadar tüm ilanlarda sitedeki varsayılan danışman görünür.</p>
    </div>

    <ul v-else class="list" data-testid="advisor-list">
      <li v-for="a in advisors" :key="a.id" class="item" :class="{ inactive: !a.isActive }" data-testid="advisor">
        <div class="main">
          <h2 class="name">
            {{ a.name }}
            <span class="badge" :class="a.isActive ? 'on' : 'off'" data-testid="state">{{ a.isActive ? 'Aktif' : 'Pasif' }}</span>
          </h2>
          <p class="line">{{ a.phone }}</p>
          <p class="line muted">WhatsApp: {{ a.whatsApp || 'telefondan türetilir' }}</p>
          <p class="line"><span data-testid="count">{{ count(a.listingCount) }}</span></p>
          <p class="audit">{{ auditLine(a) }}</p>
        </div>
        <div class="actions">
          <button type="button" class="secondary" data-testid="edit" :aria-label="`Düzenle: ${a.name}`" @click="startEdit(a)">Düzenle</button>
          <button v-if="a.isActive" type="button" class="danger" data-testid="deactivate" :aria-label="`Pasifleştir: ${a.name}`" :disabled="busy" @click="askDeactivate(a)">Pasifleştir</button>
          <button v-else type="button" data-testid="activate" :aria-label="`Etkinleştir: ${a.name}`" :disabled="busy" @click="setActive(a, true)">Etkinleştir</button>
          <router-link :to="`/admin?advisorId=${a.id}`" class="btn secondary" :aria-label="`İlanlarını gör: ${a.name}`">İlanlarını gör</router-link>
        </div>
      </li>
    </ul>

    <ConfirmDialog
      :open="pendingDeactivate !== null"
      title="Danışman pasifleştirilsin mi?"
      :message="deactivateMessage"
      confirm-label="Pasifleştir"
      danger
      @confirm="confirmDeactivate"
      @cancel="pendingDeactivate = null"
    />
  </section>
</template>

<style scoped>
.page {
  padding: 1rem 0 2rem;
  max-width: 52rem;
}
h1 {
  margin: 0 0 0.5rem;
  font-size: clamp(1.25rem, 1rem + 1.2vw, 1.75rem);
}
.explain,
.muted {
  color: var(--muted);
}
.form-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.75rem;
}
.summary p {
  margin: 0 0 0.25rem;
}
.status {
  min-height: 1.5em;
  margin: 0 0 0.75rem;
  color: var(--green-700);
  font-weight: 600;
}
.list {
  display: grid;
  gap: 0.75rem;
  margin: 0;
  padding: 0;
  list-style: none;
}
.item {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 0.75rem 1rem;
  padding: 0.75rem 1rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: #fff;
}
.item.inactive {
  background: #f6f7f6;
}
.item.inactive .main {
  color: var(--muted);
}
.main {
  min-width: 0;
  flex: 1 1 16rem;
}
.name {
  margin: 0 0 0.25rem;
  font-size: 1.0625rem;
  overflow-wrap: anywhere;
}
.line {
  margin: 0 0 0.125rem;
  overflow-wrap: anywhere;
}
.audit {
  margin: 0.25rem 0 0;
  font-size: 0.8125rem;
  color: var(--muted);
}
.badge {
  display: inline-block;
  margin-left: 0.375rem;
  padding: 0.0625rem 0.5rem;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 600;
  vertical-align: middle;
}
.badge.on {
  background: #e3f1e7;
  color: var(--green-900);
}
.badge.off {
  background: #e6e8e6;
  color: #4a524d;
}
.actions {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: 0.375rem;
}
.actions .btn {
  text-align: center;
}
button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.notice {
  padding: 1.5rem 1rem;
  border: 1px dashed var(--border);
  border-radius: var(--radius);
  text-align: center;
}
.notice p {
  margin: 0 0 0.5rem;
}
</style>

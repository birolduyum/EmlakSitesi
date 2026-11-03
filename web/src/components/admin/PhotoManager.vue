<script setup>
import { ref, watch, nextTick } from 'vue'
import { api } from '../../api.js'
import ConfirmDialog from './ConfirmDialog.vue'
import './adminForm.css'

// Fotoğraf yönetimi: yükleme (seçici + sürükle-bırak), silme, sıralama (düğmelerle), kapak seçimi.
// Sunucu son sözü söyler; buradaki kontroller yalnızca hızlı geri bildirim içindir.
const MAX_PHOTOS = 40
const MAX_BYTES = 10 * 1024 * 1024
const TYPES = ['image/jpeg', 'image/png', 'image/webp']
const EXTENSIONS = ['.jpg', '.jpeg', '.png', '.webp']

const props = defineProps({
  listingId: { type: Number, required: true },
  // [{ id, url, sortOrder }] — sunucudaki sıra
  photos: { type: Array, default: () => [] }
})
const emit = defineEmits(['changed'])

const items = ref([...props.photos])
watch(
  () => props.photos,
  (next) => {
    items.value = [...next]
  }
)

const base = () => `/api/admin/listings/${props.listingId}/photos`
const root = ref(null)
const fileInput = ref(null)
const busy = ref(false)
const uploading = ref(false)
const dragging = ref(false)
const errors = ref([])
const pendingDelete = ref(null)

function fail(...messages) {
  errors.value = messages
}

function serverMessages(e, fallback) {
  if (e?.status === 400 && Array.isArray(e.body?.errors) && e.body.errors.length) return e.body.errors
  if (e?.status === 0) return ['Sunucuya ulaşılamadı.']
  return [fallback]
}

function typeOk(file) {
  if (TYPES.includes(file.type)) return true
  if (file.type) return false
  return EXTENSIONS.some((x) => file.name.toLowerCase().endsWith(x))
}

function precheck(files) {
  const problems = []
  for (const f of files) {
    if (!typeOk(f)) problems.push(`${f.name}: yalnızca jpg, png veya webp yüklenebilir.`)
    else if (f.size > MAX_BYTES) problems.push(`${f.name}: dosya 10 MB'tan büyük olamaz.`)
  }
  if (items.value.length + files.length > MAX_PHOTOS) {
    problems.push(`Bir ilana en çok ${MAX_PHOTOS} fotoğraf eklenebilir (şu an ${items.value.length}, eklenmek istenen ${files.length}).`)
  }
  return problems
}

async function upload(fileList) {
  const files = [...fileList]
  if (files.length === 0 || busy.value) return
  errors.value = []
  const problems = precheck(files)
  if (problems.length) {
    fail(...problems)
    return
  }
  busy.value = true
  uploading.value = true
  try {
    await api.upload(base(), files)
    emit('changed')
  } catch (e) {
    fail(...serverMessages(e, 'Fotoğraflar yüklenemedi, lütfen tekrar deneyin.'))
  } finally {
    busy.value = false
    uploading.value = false
  }
}

function onPick(event) {
  const files = event.target.files ? [...event.target.files] : []
  upload(files)
  // Aynı dosya yeniden seçilebilsin
  try {
    event.target.value = ''
  } catch {
    // yok sayılır
  }
}
function onDrop(event) {
  dragging.value = false
  upload(event.dataTransfer?.files ?? [])
}

// Sıralama: tam kimlik listesi gönderilir; hata olursa arayüz eski sıraya döner.
async function applyOrder(next, focusId, action) {
  if (busy.value) return
  const previous = items.value
  errors.value = []
  items.value = next
  busy.value = true
  try {
    await api.put(`${base()}/order`, next.map((p) => p.id))
  } catch (e) {
    items.value = previous
    fail(...serverMessages(e, 'Fotoğraflar sıralanamadı, değişiklik geri alındı.'))
    return
  } finally {
    busy.value = false
  }
  await nextTick()
  focusAction(focusId, action)
}

function focusAction(id, action) {
  const card = root.value?.querySelector(`[data-photo-id="${id}"]`)
  const preferred = card?.querySelector(`[data-action="${action}"]:not([disabled])`)
  const fallback = card?.querySelector('[data-action]:not([disabled])')
  ;(preferred ?? fallback)?.focus()
}

function move(index, delta) {
  const target = index + delta
  if (target < 0 || target >= items.value.length) return
  const next = [...items.value]
  const [photo] = next.splice(index, 1)
  next.splice(target, 0, photo)
  applyOrder(next, photo.id, delta < 0 ? 'left' : 'right')
}

function makeCover(index) {
  if (index === 0) return
  const next = [...items.value]
  const [photo] = next.splice(index, 1)
  next.unshift(photo)
  applyOrder(next, photo.id, 'right')
}

function askDelete(photo, index) {
  pendingDelete.value = { photo, number: index + 1 }
}

async function confirmDelete() {
  const target = pendingDelete.value
  pendingDelete.value = null
  if (!target || busy.value) return
  busy.value = true
  errors.value = []
  try {
    await api.delete(`${base()}/${target.photo.id}`)
    emit('changed')
  } catch (e) {
    fail(...serverMessages(e, 'Fotoğraf silinemedi, lütfen tekrar deneyin.'))
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <section ref="root" class="af-section photos" aria-labelledby="sec-photos" data-testid="photo-manager">
    <h2 id="sec-photos">Fotoğraflar</h2>
    <p class="af-note">
      İlk fotoğraf kapak olur. jpg, png veya webp; her biri en çok 10 MB; ilan başına en çok {{ MAX_PHOTOS }} fotoğraf.
      ({{ items.length }} / {{ MAX_PHOTOS }})
    </p>

    <div
      class="zone"
      :class="{ over: dragging }"
      data-testid="photo-zone"
      :aria-busy="uploading ? 'true' : 'false'"
      @dragover.prevent="dragging = true"
      @dragleave="dragging = false"
      @drop.prevent="onDrop"
    >
      <p>Fotoğrafları buraya sürükleyin ya da</p>
      <label class="btn secondary pick" :class="{ disabled: busy }">
        Dosya seç
        <input
          ref="fileInput"
          class="af-sr"
          type="file"
          name="photos"
          multiple
          accept="image/jpeg,image/png,image/webp"
          :disabled="busy"
          @change="onPick"
        />
      </label>
      <p v-if="uploading" class="progress" data-testid="uploading" role="status">Yükleniyor… lütfen bekleyin</p>
    </div>

    <div v-if="errors.length" class="af-error errors" role="alert">
      <p v-for="(m, i) in errors" :key="i">{{ m }}</p>
    </div>

    <p v-if="items.length === 0" class="empty">Henüz fotoğraf yok.</p>
    <ol v-else class="grid">
      <li v-for="(p, i) in items" :key="p.id" class="card" data-testid="photo" :data-photo-id="p.id">
        <div class="thumb">
          <img :src="p.url" :alt="`Fotoğraf ${i + 1}`" loading="lazy" />
          <span v-if="i === 0" class="cover">Kapak</span>
        </div>
        <div class="tools">
          <button type="button" class="secondary" data-action="left" :disabled="busy || i === 0" :aria-label="`Fotoğraf ${i + 1}'i sola taşı`" @click="move(i, -1)">Sola taşı</button>
          <button type="button" class="secondary" data-action="right" :disabled="busy || i === items.length - 1" :aria-label="`Fotoğraf ${i + 1}'i sağa taşı`" @click="move(i, 1)">Sağa taşı</button>
          <button v-if="i > 0" type="button" class="secondary" data-action="cover" :disabled="busy" :aria-label="`Fotoğraf ${i + 1}'i kapak yap`" @click="makeCover(i)">Kapak yap</button>
          <button type="button" class="danger" data-action="delete" :disabled="busy" :aria-label="`Fotoğraf ${i + 1}'i sil`" @click="askDelete(p, i)">Sil</button>
        </div>
      </li>
    </ol>

    <ConfirmDialog
      :open="pendingDelete !== null"
      title="Fotoğraf silinsin mi?"
      :message="pendingDelete ? `Fotoğraf ${pendingDelete.number} kalıcı olarak silinecek.` : ''"
      confirm-label="Sil"
      danger
      @confirm="confirmDelete"
      @cancel="pendingDelete = null"
    />
  </section>
</template>

<style scoped>
.zone {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  padding: 1rem;
  border: 2px dashed var(--border);
  border-radius: var(--radius);
  text-align: center;
}
.zone.over {
  border-color: var(--green-600);
  background: var(--green-50);
}
.zone p {
  margin: 0;
}
.pick {
  position: relative;
  cursor: pointer;
}
.pick.disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
.pick:focus-within {
  outline: 2px solid var(--green-600);
  outline-offset: 2px;
}
.progress {
  width: 100%;
  color: var(--muted);
}
.errors {
  margin-top: 0.75rem;
}
.errors p {
  margin: 0 0 0.25rem;
}
.empty {
  color: var(--muted);
}
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(11rem, 1fr));
  gap: 0.75rem;
  margin: 1rem 0 0;
  padding: 0;
  list-style: none;
}
.card {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.5rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
}
.thumb {
  position: relative;
  aspect-ratio: 4 / 3;
  overflow: hidden;
  border-radius: 4px;
  background: #f3f4f6;
}
.thumb img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.cover {
  position: absolute;
  top: 0.375rem;
  left: 0.375rem;
  padding: 0.125rem 0.5rem;
  border-radius: 999px;
  background: var(--green-700);
  color: #fff;
  font-size: 0.75rem;
  font-weight: 600;
}
.tools {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem;
}
.tools button {
  padding: 4px 8px;
  font-size: 0.8125rem;
}
.tools button:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}
button:focus-visible {
  outline: 2px solid var(--green-600);
  outline-offset: 2px;
}
</style>

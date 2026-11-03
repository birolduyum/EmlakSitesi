<script setup>
import { ref, computed } from 'vue'
import { api } from '../../api.js'
import { formatDateTime } from '../../format.js'
import { useAsyncAction } from '../../composables/useAsyncAction.js'
import './adminForm.css'

const NOTE_MAX = 500

const props = defineProps({
  record: { type: Object, required: true },
  admin: { type: Boolean, default: false }
})
const emit = defineEmits(['closed', 'stale'])

const note = ref('')
const link = computed(() => (props.admin ? `/admin/ilan/${props.record.listingId}` : `/ilan/${props.record.listingId}`))

const closing = useAsyncAction(
  () => api.post(`/api/showings/${props.record.id}/close`, { note: note.value.trim() || null }),
  {
    byStatus: {
      403: 'Bu kaydı kapatma yetkiniz yok.',
      404: 'Kayıt bulunamadı.',
      409: 'Bu kayıt zaten kapatılmış.',
      400: `Not en çok ${NOTE_MAX} karakter olabilir.`
    }
  }
)

async function close() {
  const result = await closing.run()
  if (result?.ok) emit('closed', props.record)
  else if (result?.error?.status === 409) emit('stale', props.record)
}
</script>

<template>
  <li class="rec" data-testid="open-record">
    <div class="rec-head">
      <router-link :to="link" class="rec-title">{{ record.listingTitle }}</router-link>
      <span class="rec-time">Alındı: {{ formatDateTime(record.takenAt) }}</span>
    </div>
    <div class="af-field">
      <label :for="`note-${record.id}`">Not (isteğe bağlı)</label>
      <textarea :id="`note-${record.id}`" v-model="note" rows="2" :maxlength="NOTE_MAX" :aria-describedby="`note-${record.id}-count`"></textarea>
      <p :id="`note-${record.id}-count`" class="af-hint count">{{ note.length }} / {{ NOTE_MAX }}</p>
    </div>
    <p v-if="closing.error.value" class="af-error" role="alert">{{ closing.error.value }}</p>
    <button type="button" :disabled="closing.pending.value" @click="close">Gösterdim / anahtarı iade ettim</button>
  </li>
</template>

<style scoped>
.rec {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.625rem;
  padding: 0.875rem 1rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: #fff;
}
.rec-head {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0.25rem 1rem;
}
.rec-title {
  font-weight: 700;
  color: var(--green-700);
}
.rec-time {
  color: var(--muted);
  font-size: 0.875rem;
}
.rec .af-field {
  width: 100%;
}
.count {
  text-align: right;
}
button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>

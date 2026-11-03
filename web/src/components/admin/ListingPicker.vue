<script setup>
import { ref, computed, onBeforeUnmount, nextTick } from 'vue'
import { api } from '../../api.js'
import './adminForm.css'

const DEBOUNCE_MS = 300
const PAGE_SIZE = 8

// v-model: seçilen ilan ({ id, title, city, district, rooms }) ya da null.
const props = defineProps({ modelValue: { type: Object, default: null } })
const emit = defineEmits(['update:modelValue'])

const uid = Math.random().toString(36).slice(2, 8)
const listId = `lp-list-${uid}`
const optionId = (i) => `lp-opt-${uid}-${i}`

const text = ref('')
const results = ref([])
const state = ref('idle') // idle | loading | ready | error
const open = ref(false)
const active = ref(-1)
const input = ref(null)

let timer = null
let requestId = 0

async function search() {
  const q = text.value.trim()
  const id = ++requestId
  if (q === '') {
    results.value = []
    state.value = 'idle'
    open.value = false
    return
  }
  state.value = 'loading'
  try {
    const data = await api.get('/api/admin/listings', { text: q, status: 'Active', page: 1, pageSize: PAGE_SIZE })
    if (id !== requestId) return // daha yeni bir arama başladı: bu cevap eski
    results.value = data?.items ?? []
    active.value = -1
    state.value = 'ready'
    open.value = true
  } catch {
    if (id !== requestId) return
    results.value = []
    state.value = 'error'
    open.value = true
  }
}

function onInput() {
  clearTimeout(timer)
  requestId++ // yazı değişti: uçuştaki cevap artık eski sayılır
  timer = setTimeout(search, DEBOUNCE_MS)
}

onBeforeUnmount(() => {
  clearTimeout(timer)
  requestId++
})

function choose(item) {
  emit('update:modelValue', item)
  open.value = false
  text.value = ''
  results.value = []
  state.value = 'idle'
  active.value = -1
}

function onKeydown(e) {
  if (e.key === 'ArrowDown') {
    e.preventDefault()
    if (!results.value.length) return
    open.value = true
    active.value = (active.value + 1) % results.value.length
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    if (!results.value.length) return
    open.value = true
    active.value = active.value <= 0 ? results.value.length - 1 : active.value - 1
  } else if (e.key === 'Enter') {
    if (open.value && active.value >= 0 && results.value[active.value]) {
      e.preventDefault()
      choose(results.value[active.value])
    }
  } else if (e.key === 'Escape') {
    open.value = false
    active.value = -1
  }
}

async function change() {
  emit('update:modelValue', null)
  await nextTick()
  input.value?.focus()
}

const expanded = computed(() => open.value && state.value === 'ready' && results.value.length > 0)
const activeId = computed(() => (expanded.value && active.value >= 0 ? optionId(active.value) : undefined))
const where = (l) => [l.city, l.district].filter(Boolean).join(', ')
const liveText = computed(() => {
  if (state.value === 'loading') return 'Aranıyor'
  if (state.value === 'ready') return results.value.length ? `${results.value.length} ilan bulundu` : 'İlan bulunamadı'
  return ''
})
</script>

<template>
  <div class="picker">
    <div v-if="props.modelValue" class="chip" data-testid="chip">
      <span class="chip-text">
        <strong>{{ props.modelValue.title }}</strong>
        <span class="chip-meta">#{{ props.modelValue.id }} · {{ where(props.modelValue) }}<template v-if="props.modelValue.rooms"> · {{ props.modelValue.rooms }}</template></span>
      </span>
      <button type="button" class="secondary" @click="change">Değiştir</button>
    </div>

    <div v-else class="search af-field">
      <label :for="`${listId}-input`">İlan ara</label>
      <input
        :id="`${listId}-input`"
        ref="input"
        v-model="text"
        type="text"
        role="combobox"
        autocomplete="off"
        placeholder="Başlık, semt veya ilan no ile ara"
        aria-autocomplete="list"
        :aria-expanded="expanded ? 'true' : 'false'"
        :aria-controls="listId"
        :aria-activedescendant="activeId"
        @input="onInput"
        @keydown="onKeydown"
      />
      <p class="af-sr" aria-live="polite">{{ liveText }}</p>
      <ul v-show="expanded" :id="listId" class="options" role="listbox" aria-label="İlan arama sonuçları">
        <li
          v-for="(item, i) in results"
          :id="optionId(i)"
          :key="item.id"
          role="option"
          :aria-selected="i === active ? 'true' : 'false'"
          :class="{ on: i === active }"
          @mousedown.prevent
          @click="choose(item)"
        >
          <span class="o-title"><span class="o-id">#{{ item.id }}</span> {{ item.title }}</span>
          <span class="o-meta">{{ where(item) }}<template v-if="item.rooms"> · {{ item.rooms }}</template></span>
        </li>
      </ul>
      <p v-if="state === 'ready' && !results.length" class="af-hint">İlan bulunamadı</p>
      <p v-if="state === 'error'" class="af-error" role="alert">Arama yapılamadı. Lütfen tekrar deneyin.</p>
    </div>
  </div>
</template>

<style scoped>
.picker {
  position: relative;
}
.options {
  list-style: none;
  margin: 0;
  padding: 0.25rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: #fff;
  max-height: 18rem;
  overflow-y: auto;
}
.options li {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
  padding: 0.5rem 0.625rem;
  border-radius: 6px;
  cursor: pointer;
}
.options li:hover,
.options li.on {
  background: var(--green-100);
}
.o-id {
  color: var(--muted);
  font-variant-numeric: tabular-nums;
}
.o-meta,
.chip-meta {
  color: var(--muted);
  font-size: 0.875rem;
}
.chip {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  padding: 0.625rem 0.75rem;
  border: 1px solid var(--green-600);
  border-radius: var(--radius);
  background: var(--green-50);
}
.chip-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}
</style>

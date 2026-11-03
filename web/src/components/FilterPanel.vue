<script setup>
import { reactive, computed, watch, onBeforeUnmount } from 'vue'
import { parseTrDecimal, parseTrInteger, INVALID_NUMBER_MESSAGE } from '../numbers.js'
import { heatingLabels, furnishingLabels, typeLabels } from '../labels.js'

const props = defineProps({
  modelValue: { type: Object, required: true },
  filters: { type: Object, default: () => ({ cities: [], districtsByCity: {}, roomCounts: {} }) },
  // Yönetici modu: tür bölümüne "Hepsi" eklenir (seçilince type sorguda yer almaz).
  allowAll: { type: Boolean, default: false }
})
const emit = defineEmits(['update:modelValue'])

const DEBOUNCE_MS = 300

// Yazılan alanlar: [anahtar, tür]. Seçim kutuları ve onay kutuları hemen yayar, bunlar bekler.
const TYPED = [
  ['neighborhood', 'text'],
  ['minPriceTry', 'decimal'],
  ['maxPriceTry', 'decimal'],
  ['minM2', 'integer'],
  ['maxM2', 'integer'],
  ['maxBuildingAge', 'integer'],
  ['floor', 'integer'],
  ['minTotalFloors', 'integer'],
  ['maxTotalFloors', 'integer']
]

const BATHROOMS = [
  ['', 'Tümü'],
  ['1', '1+'],
  ['2', '2+'],
  ['3', '3+'],
  ['4', '4+']
]

const FEATURES = [
  ['balcony', 'Balkon'],
  ['elevator', 'Asansör'],
  ['parking', 'Otopark'],
  ['inSite', 'Site içinde'],
  ['loanEligible', 'Krediye uygun'],
  ['swap', 'Takas']
]

const CURRENCIES = [
  ['', 'Tümü'],
  ['TRY', '₺'],
  ['USD', '$'],
  ['EUR', '€']
]

function parseTyped(raw, kind) {
  if (kind === 'text') {
    const s = String(raw ?? '').trim()
    return s === '' ? undefined : s
  }
  // Türkçe yazım: nokta binlik, virgül ondalık ("5.000.000" → 5000000). Boş değer "yok" demektir (undefined).
  const s = String(raw ?? '').trim()
  if (s === '') return undefined
  const n = kind === 'integer' ? parseTrInteger(s) : parseTrDecimal(s)
  return n === null ? undefined : n
}

function draftFrom(model) {
  const d = {}
  for (const [key] of TYPED) d[key] = model[key] === undefined || model[key] === null ? '' : String(model[key])
  return d
}

const draft = reactive(draftFrom(props.modelValue))

// Dolu ama sayı olarak okunamayan alanlar süzgece girmez; sessizce yutulmasın diye satır içi uyarı gösterilir.
const KINDS = Object.fromEntries(TYPED)
const invalid = (key) => String(draft[key] ?? '').trim() !== '' && parseTyped(draft[key], KINDS[key]) === undefined

// Adres çubuğu / geri tuşu değeri değiştirince taslakları eşitle (kullanıcı yazarken ezme).
watch(
  () => props.modelValue,
  (model) => {
    for (const [key, kind] of TYPED) {
      if (parseTyped(draft[key], kind) !== model[key]) {
        draft[key] = model[key] === undefined || model[key] === null ? '' : String(model[key])
      }
    }
  },
  { deep: true }
)

let timer = null
function cancelTimer() {
  if (timer !== null) {
    clearTimeout(timer)
    timer = null
  }
}
onBeforeUnmount(cancelTimer)

function typedValues() {
  const out = {}
  for (const [key, kind] of TYPED) {
    const v = parseTyped(draft[key], kind)
    if (v !== undefined) out[key] = v
  }
  return out
}

// Yazılan alanları (taslak) ve verilen değişikliği birleştirip yeni sorguyu yayar.
function emitWith(patch = {}) {
  cancelTimer()
  const next = { ...props.modelValue }
  for (const [key] of TYPED) delete next[key]
  Object.assign(next, typedValues(), patch)
  for (const key of Object.keys(next)) if (next[key] === undefined) delete next[key]
  emit('update:modelValue', next)
}

function onTyped(key, event) {
  draft[key] = event.target.value
  cancelTimer()
  timer = setTimeout(() => emitWith(), DEBOUNCE_MS)
}

function setType(type) {
  emitWith({ type: type || undefined })
}
function setCurrency(currency) {
  emitWith({ currency: currency || undefined })
}
function onCity(event) {
  const city = event.target.value || undefined
  emitWith({ city, district: undefined })
}
function onSelect(key, event) {
  emitWith({ [key]: event.target.value || undefined })
}
function onBathrooms(event) {
  const n = Number(event.target.value)
  emitWith({ minBathrooms: Number.isInteger(n) && n > 0 ? n : undefined })
}
function onFeature(key, event) {
  emitWith({ [key]: event.target.checked ? true : undefined })
}
function toggleRoom(room, checked) {
  const current = props.modelValue.rooms ?? []
  const next = checked ? [...current.filter((r) => r !== room), room] : current.filter((r) => r !== room)
  // Seçim sırası değil, listedeki sıra korunur
  const ordered = roomOptions.value.map((o) => o.room).filter((r) => next.includes(r))
  emitWith({ rooms: ordered.length ? ordered : undefined })
}

function clearAll() {
  cancelTimer()
  for (const [key] of TYPED) draft[key] = ''
  emit('update:modelValue', { type: props.modelValue.type })
}

const typeOptions = computed(() => [...(props.allowAll ? [['', 'Hepsi']] : []), ...Object.entries(typeLabels)])

const districts = computed(() => {
  const city = props.modelValue.city
  return city ? props.filters.districtsByCity?.[city] ?? [] : []
})

const roomOptions = computed(() =>
  Object.entries(props.filters.roomCounts ?? {})
    .sort(([a], [b]) => a.localeCompare(b, 'tr', { numeric: true }))
    .map(([room, count]) => ({ room, count }))
)
</script>

<template>
  <form class="filters" aria-label="Filtreler" @submit.prevent>
    <fieldset class="segmented">
      <legend class="sr-only">İlan türü</legend>
      <label v-for="[value, label] in typeOptions" :key="value" :class="{ on: (modelValue.type ?? '') === value }">
        <input
          type="radio"
          name="type"
          :value="value"
          :checked="(modelValue.type ?? '') === value"
          @change="setType(value)"
        />
        <span>{{ label }}</span>
      </label>
    </fieldset>

    <slot name="extra"></slot>

    <div class="group">
      <label class="field">
        <span>İl</span>
        <select name="city" :value="modelValue.city ?? ''" @change="onCity">
          <option value="">Tüm İller</option>
          <option v-for="c in filters.cities ?? []" :key="c" :value="c">{{ c }}</option>
        </select>
      </label>
      <label class="field">
        <span>İlçe</span>
        <select name="district" :value="modelValue.district ?? ''" :disabled="!modelValue.city" @change="onSelect('district', $event)">
          <option value="">Tüm İlçeler</option>
          <option v-for="d in districts" :key="d" :value="d">{{ d }}</option>
        </select>
      </label>
      <label class="field">
        <span>Mahalle</span>
        <input type="text" name="neighborhood" autocomplete="off" :value="draft.neighborhood" @input="onTyped('neighborhood', $event)" />
      </label>
    </div>

    <fieldset class="group">
      <legend>Para birimi</legend>
      <div class="choices">
        <label v-for="[value, label] in CURRENCIES" :key="value" class="choice">
          <input
            type="radio"
            name="currency"
            :value="value"
            :checked="(modelValue.currency ?? '') === value"
            @change="setCurrency(value)"
          />
          <span>{{ label }}</span>
        </label>
      </div>
    </fieldset>

    <fieldset class="group">
      <legend>Fiyat <small>(₺ cinsinden)</small></legend>
      <div class="pair">
        <label class="field">
          <span>En Az</span>
          <input type="text" name="minPriceTry" inputmode="decimal" autocomplete="off" :value="draft.minPriceTry" :aria-invalid="invalid('minPriceTry') ? 'true' : undefined" @input="onTyped('minPriceTry', $event)" />
          <small v-if="invalid('minPriceTry')" class="err" role="alert">{{ INVALID_NUMBER_MESSAGE }}</small>
        </label>
        <label class="field">
          <span>En Fazla</span>
          <input type="text" name="maxPriceTry" inputmode="decimal" autocomplete="off" :value="draft.maxPriceTry" :aria-invalid="invalid('maxPriceTry') ? 'true' : undefined" @input="onTyped('maxPriceTry', $event)" />
          <small v-if="invalid('maxPriceTry')" class="err" role="alert">{{ INVALID_NUMBER_MESSAGE }}</small>
        </label>
      </div>
    </fieldset>

    <fieldset v-if="roomOptions.length" class="group">
      <legend>Oda sayısı</legend>
      <div class="choices">
        <label v-for="o in roomOptions" :key="o.room" class="choice" data-testid="room">
          <input
            type="checkbox"
            name="rooms"
            :value="o.room"
            :checked="(modelValue.rooms ?? []).includes(o.room)"
            @change="toggleRoom(o.room, $event.target.checked)"
          />
          <span>{{ o.room }} ({{ o.count }})</span>
        </label>
      </div>
    </fieldset>

    <fieldset class="group">
      <legend>Metrekare</legend>
      <div class="pair">
        <label class="field">
          <span>En Az</span>
          <input type="text" name="minM2" inputmode="numeric" autocomplete="off" :value="draft.minM2" :aria-invalid="invalid('minM2') ? 'true' : undefined" @input="onTyped('minM2', $event)" />
          <small v-if="invalid('minM2')" class="err" role="alert">{{ INVALID_NUMBER_MESSAGE }}</small>
        </label>
        <label class="field">
          <span>En Fazla</span>
          <input type="text" name="maxM2" inputmode="numeric" autocomplete="off" :value="draft.maxM2" :aria-invalid="invalid('maxM2') ? 'true' : undefined" @input="onTyped('maxM2', $event)" />
          <small v-if="invalid('maxM2')" class="err" role="alert">{{ INVALID_NUMBER_MESSAGE }}</small>
        </label>
      </div>
    </fieldset>

    <fieldset class="group">
      <legend>Binanın kat sayısı</legend>
      <div class="pair">
        <label class="field">
          <span>En Az</span>
          <input type="text" name="minTotalFloors" inputmode="numeric" autocomplete="off" :value="draft.minTotalFloors" :aria-invalid="invalid('minTotalFloors') ? 'true' : undefined" @input="onTyped('minTotalFloors', $event)" />
          <small v-if="invalid('minTotalFloors')" class="err" role="alert">{{ INVALID_NUMBER_MESSAGE }}</small>
        </label>
        <label class="field">
          <span>En Çok</span>
          <input type="text" name="maxTotalFloors" inputmode="numeric" autocomplete="off" :value="draft.maxTotalFloors" :aria-invalid="invalid('maxTotalFloors') ? 'true' : undefined" @input="onTyped('maxTotalFloors', $event)" />
          <small v-if="invalid('maxTotalFloors')" class="err" role="alert">{{ INVALID_NUMBER_MESSAGE }}</small>
        </label>
      </div>
    </fieldset>

    <div class="group">
      <label class="field">
        <span>Binanın yaşı (en fazla)</span>
        <input type="text" name="maxBuildingAge" inputmode="numeric" autocomplete="off" :value="draft.maxBuildingAge" :aria-invalid="invalid('maxBuildingAge') ? 'true' : undefined" @input="onTyped('maxBuildingAge', $event)" />
          <small v-if="invalid('maxBuildingAge')" class="err" role="alert">{{ INVALID_NUMBER_MESSAGE }}</small>
      </label>
      <label class="field">
        <span>Bulunduğu kat</span>
        <input type="text" name="floor" inputmode="numeric" autocomplete="off" :value="draft.floor" :aria-invalid="invalid('floor') ? 'true' : undefined" @input="onTyped('floor', $event)" />
          <small v-if="invalid('floor')" class="err" role="alert">{{ INVALID_NUMBER_MESSAGE }}</small>
      </label>
      <label class="field">
        <span>Banyo sayısı</span>
        <select name="minBathrooms" :value="modelValue.minBathrooms === undefined ? '' : String(modelValue.minBathrooms)" @change="onBathrooms">
          <option v-for="[value, label] in BATHROOMS" :key="value" :value="value">{{ label }}</option>
        </select>
      </label>
      <label class="field">
        <span>Isıtma</span>
        <select name="heating" :value="modelValue.heating ?? ''" @change="onSelect('heating', $event)">
          <option value="">Hepsi</option>
          <option v-for="(label, value) in heatingLabels" :key="value" :value="value">{{ label }}</option>
        </select>
      </label>
      <label class="field">
        <span>Eşya durumu</span>
        <select name="furnishing" :value="modelValue.furnishing ?? ''" @change="onSelect('furnishing', $event)">
          <option value="">Hepsi</option>
          <option v-for="(label, value) in furnishingLabels" :key="value" :value="value">{{ label }}</option>
        </select>
      </label>
    </div>

    <fieldset class="group">
      <legend>Özellikler</legend>
      <div class="choices">
        <label v-for="[key, label] in FEATURES" :key="key" class="choice">
          <input type="checkbox" :name="key" :checked="modelValue[key] === true" @change="onFeature(key, $event)" />
          <span>{{ label }}</span>
        </label>
      </div>
    </fieldset>

    <button type="button" class="secondary clear" data-testid="clear" @click="clearAll">Temizle</button>
  </form>
</template>

<style scoped>
.filters {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: #fff;
}
fieldset {
  min-width: 0;
  margin: 0;
  padding: 0;
  border: 0;
}
legend {
  padding: 0;
  margin-bottom: 0.375rem;
  font-weight: 600;
  color: var(--green-900);
}
legend small {
  font-weight: 400;
  color: var(--muted);
}
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}
.group {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.875rem;
  color: var(--muted);
}
.field input,
.field select {
  width: 100%;
  color: var(--text);
  background: #fff;
}
.pair {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem;
}
.choices {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem 0.875rem;
}
.choice {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  cursor: pointer;
}
.segmented {
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: 1fr;
  border: 1px solid var(--green-700);
  border-radius: var(--radius);
  overflow: hidden;
}
.segmented label {
  position: relative;
  padding: 0.5rem;
  text-align: center;
  font-weight: 600;
  color: var(--green-700);
  background: #fff;
  cursor: pointer;
}
.segmented label.on {
  background: var(--green-700);
  color: #fff;
}
.segmented input {
  position: absolute;
  opacity: 0;
  inset: 0;
  cursor: pointer;
}
.segmented label:focus-within {
  outline: 2px solid var(--green-600);
  outline-offset: -4px;
}
input:focus-visible,
select:focus-visible {
  outline: 2px solid var(--green-600);
  outline-offset: 1px;
}
select:disabled {
  background: #f3f4f6;
  cursor: not-allowed;
}
.err {
  color: var(--danger, #b91c1c);
  font-size: 0.8125rem;
}
.clear {
  align-self: stretch;
}
</style>

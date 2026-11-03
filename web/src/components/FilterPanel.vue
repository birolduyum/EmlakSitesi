<script setup>
import { reactive, computed, watch, ref, nextTick } from 'vue'
import { parseTrDecimal, parseTrInteger, INVALID_NUMBER_MESSAGE } from '../numbers.js'
import { heatingLabels, furnishingLabels, typeLabels } from '../labels.js'

const props = defineProps({
  // Uygulanmış süzgeçler (adres çubuğundaki durum). Panel bunun bir TASLAĞINI tutar.
  modelValue: { type: Object, required: true },
  filters: { type: Object, default: () => ({ cities: [], districtsByCity: {}, roomCounts: {} }) },
  // Tür bölümündeki "hepsi" seçeneğinin etiketi (seçilince type sorguda yer almaz): ziyaretçide "Tümü", yönetimde "Hepsi".
  allLabel: { type: String, default: 'Tümü' }
})
// update:modelValue → uygulanan yeni süzgeçler; applied → yalnızca "Filtreleri uygula" ile uygulandığında (ek olarak) yayılır.
const emit = defineEmits(['update:modelValue', 'applied'])

// Yazılan alanlar: [anahtar, tür]. Taslakta metin olarak durur, uygulanırken sayıya çevrilir.
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
const KINDS = Object.fromEntries(TYPED)
// Taslağa girmeyen, sayfaya ait anahtarlar: uygulanırken sayfa her zaman 1'e döner.
const PAGING = new Set(['page', 'pageSize'])

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
  for (const [key, value] of Object.entries(model)) {
    if (PAGING.has(key) || value === undefined || value === null) continue
    d[key] = Array.isArray(value) ? [...value] : value
  }
  for (const [key] of TYPED) d[key] = model[key] === undefined || model[key] === null ? '' : String(model[key])
  return d
}

const draft = reactive(draftFrom(props.modelValue))

function resetDraft(model) {
  for (const key of Object.keys(draft)) delete draft[key]
  Object.assign(draft, draftFrom(model))
}

// Dış değişiklik (geri/ileri tuşu, tür geçişi, Temizle, sayfa bağlantısı, sıralama) → taslak adres çubuğundan yeniden kurulur;
// uygulanmamış taslak atılır.
// Yalnızca arama metni, sıralama ya da sayfa değiştiyse (arama kutusu, sıralama, sayfalama) taslağın geri kalanı korunur;
// başka her değişiklik (geri/ileri, Temizle, dış gezinme) taslağı baştan kurar.
const SOFT_KEYS = new Set(['text', 'sort', 'page'])
watch(
  () => props.modelValue,
  (next, prev) => {
    const keys = new Set([...Object.keys(next), ...Object.keys(prev ?? {})])
    const changed = [...keys].filter((k) => JSON.stringify(next[k]) !== JSON.stringify(prev?.[k]))
    if (changed.length > 0 && changed.every((k) => SOFT_KEYS.has(k))) {
      for (const k of changed) {
        if (k === 'page') continue
        if (next[k] === undefined || next[k] === null || next[k] === '') delete draft[k]
        else draft[k] = next[k]
      }
      return
    }
    resetDraft(next)
  }
)

const formEl = ref(null)
const blocked = ref(false)
// Dolu ama sayı olarak okunamayan alanlar süzgece girmez; sessizce yutulmasın diye satır içi uyarı gösterilir.
const invalid = (key) => String(draft[key] ?? '').trim() !== '' && parseTyped(draft[key], KINDS[key]) === undefined

// Taslağın alanları (yazılanlar sayıya çevrilmiş) → uygulanacak süzgeç nesnesi.
const built = computed(() => {
  const next = {}
  for (const [key, value] of Object.entries(draft)) {
    if (key in KINDS) continue
    if (value === undefined || value === null || value === '') continue
    if (Array.isArray(value) && value.length === 0) continue
    next[key] = Array.isArray(value) ? [...value] : value
  }
  for (const [key, kind] of TYPED) {
    const v = parseTyped(draft[key], kind)
    if (v !== undefined) next[key] = v
  }
  return next
})

function canon(obj) {
  const out = {}
  for (const key of Object.keys(obj).sort()) {
    if (PAGING.has(key)) continue
    let v = obj[key]
    if (typeof v === 'string') v = v.trim()
    if (v === undefined || v === null || v === '' || (Array.isArray(v) && v.length === 0)) continue
    out[key] = v
  }
  return JSON.stringify(out)
}
const hasInvalid = computed(() => TYPED.some(([key]) => invalid(key)))
watch(hasInvalid, (v) => {
  if (!v) blocked.value = false
})
const dirty = computed(() => canon(built.value) !== canon(props.modelValue))

function setDraft(key, value) {
  if (value === undefined || value === null || value === '') delete draft[key]
  else draft[key] = value
}

function onTyped(key, event) {
  draft[key] = event.target.value
}
function setCurrency(currency) {
  setDraft('currency', currency)
}
function onCity(event) {
  setDraft('city', event.target.value)
  delete draft.district
}
function onSelect(key, event) {
  setDraft(key, event.target.value)
}
function onBathrooms(event) {
  const n = Number(event.target.value)
  setDraft('minBathrooms', Number.isInteger(n) && n > 0 ? n : undefined)
}
function onFeature(key, event) {
  setDraft(key, event.target.checked ? true : undefined)
}
function toggleRoom(room, checked) {
  const current = draft.rooms ?? []
  const next = checked ? [...current.filter((r) => r !== room), room] : current.filter((r) => r !== room)
  // Seçim sırası değil, listedeki sıra korunur
  setDraft('rooms', roomOptions.value.map((o) => o.room).filter((r) => next.includes(r)))
}

// Tür de bir taslak alanıdır: "Filtreleri uygula" ile diğer alanlarla birlikte uygulanır.
function setType(type) {
  setDraft('type', type)
}

// "Filtreleri uygula" / Enter: bütün taslak tek seferde uygulanır. Değişiklik yoksa hiçbir şey yapılmaz.
function apply() {
  const bad = TYPED.map(([key]) => key).find(invalid)
  if (bad) {
    // Okunamayan sayı sessizce atlanmaz: uygulama engellenir, ilk hatalı alana odaklanılır.
    blocked.value = true
    nextTick(() => formEl.value?.querySelector(`[name="${bad}"]`)?.focus())
    return
  }
  if (!dirty.value) return
  emit('update:modelValue', { ...built.value })
  emit('applied')
}

// Temizle: taslağı (tür dahil) sıfırlar ve hemen uygular.
function clearAll() {
  resetDraft({})
  emit('update:modelValue', {})
}

const typeOptions = computed(() => [['', props.allLabel], ...Object.entries(typeLabels)])

const districts = computed(() => {
  const city = draft.city
  return city ? props.filters.districtsByCity?.[city] ?? [] : []
})

const roomOptions = computed(() =>
  Object.entries(props.filters.roomCounts ?? {})
    .sort(([a], [b]) => a.localeCompare(b, 'tr', { numeric: true }))
    .map(([room, count]) => ({ room, count }))
)
</script>

<template>
  <form ref="formEl" class="filters" aria-label="Filtreler" @submit.prevent="apply">
    <fieldset class="segmented">
      <legend class="sr-only">İlan türü</legend>
      <label v-for="[value, label] in typeOptions" :key="value" :class="{ on: (draft.type ?? '') === value }">
        <input
          type="radio"
          name="type"
          :value="value"
          :checked="(draft.type ?? '') === value"
          @change="setType(value)"
        />
        <span>{{ label }}</span>
      </label>
    </fieldset>

    <slot name="extra" :draft="draft" :set="setDraft"></slot>

    <div class="group">
      <label class="field">
        <span>İl</span>
        <select name="city" :value="draft.city ?? ''" @change="onCity">
          <option value="">Tüm İller</option>
          <option v-for="c in filters.cities ?? []" :key="c" :value="c">{{ c }}</option>
        </select>
      </label>
      <label class="field">
        <span>İlçe</span>
        <select name="district" :value="draft.district ?? ''" :disabled="!draft.city" @change="onSelect('district', $event)">
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
            :checked="(draft.currency ?? '') === value"
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
      <p v-if="!draft.type" class="hint-text" data-testid="price-hint">Satılık fiyatları ve aylık kiralar birlikte süzülür.</p>
    </fieldset>

    <fieldset v-if="roomOptions.length" class="group">
      <legend>Oda sayısı</legend>
      <div class="choices">
        <label v-for="o in roomOptions" :key="o.room" class="choice" data-testid="room">
          <input
            type="checkbox"
            name="rooms"
            :value="o.room"
            :checked="(draft.rooms ?? []).includes(o.room)"
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
        <select name="minBathrooms" :value="draft.minBathrooms === undefined ? '' : String(draft.minBathrooms)" @change="onBathrooms">
          <option v-for="[value, label] in BATHROOMS" :key="value" :value="value">{{ label }}</option>
        </select>
      </label>
      <label class="field">
        <span>Isıtma</span>
        <select name="heating" :value="draft.heating ?? ''" @change="onSelect('heating', $event)">
          <option value="">Hepsi</option>
          <option v-for="(label, value) in heatingLabels" :key="value" :value="value">{{ label }}</option>
        </select>
      </label>
      <label class="field">
        <span>Eşya durumu</span>
        <select name="furnishing" :value="draft.furnishing ?? ''" @change="onSelect('furnishing', $event)">
          <option value="">Hepsi</option>
          <option v-for="(label, value) in furnishingLabels" :key="value" :value="value">{{ label }}</option>
        </select>
      </label>
    </div>

    <fieldset class="group">
      <legend>Özellikler</legend>
      <div class="choices">
        <label v-for="[key, label] in FEATURES" :key="key" class="choice">
          <input type="checkbox" :name="key" :checked="draft[key] === true" @change="onFeature(key, $event)" />
          <span>{{ label }}</span>
        </label>
      </div>
    </fieldset>

    <div class="actions">
      <p class="unapplied" aria-live="polite" data-testid="unapplied">{{ dirty ? 'Uygulanmamış değişiklikler var' : '' }}</p>
      <p class="blocked err" aria-live="polite" data-testid="invalid-msg">{{ blocked && hasInvalid ? 'Geçersiz sayı var; düzeltin' : '' }}</p>
      <div class="buttons">
        <button type="button" class="secondary clear" data-testid="clear" @click="clearAll">Temizle</button>
        <button type="submit" class="apply" :class="{ dirty }" data-testid="apply">Filtreleri uygula</button>
      </div>
    </div>
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
.hint-text {
  margin: 0;
  font-size: 0.8125rem;
  color: var(--muted);
}
.actions {
  position: sticky;
  bottom: 0;
  z-index: 3;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  margin: 0 -1rem -1rem;
  padding: 0.75rem 1rem;
  border-top: 1px solid var(--border);
  border-radius: 0 0 var(--radius) var(--radius);
  background: #fff;
}
.blocked {
  margin: 0;
  font-size: 0.8125rem;
  font-weight: 600;
}
.blocked:empty {
  display: none;
}
.unapplied {
  margin: 0;
  min-height: 1.25em;
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--green-900);
}
.buttons {
  display: flex;
  gap: 0.5rem;
}
.apply {
  flex: 1 1 auto;
}
.apply:not(.dirty) {
  background: #fff;
  color: var(--green-700);
}
.apply:not(.dirty):hover {
  background: var(--green-50);
}
.apply.dirty {
  font-weight: 700;
  box-shadow: 0 0 0 3px var(--green-100);
}
.actions button:focus-visible {
  outline: 2px solid var(--green-600);
  outline-offset: 2px;
}
</style>

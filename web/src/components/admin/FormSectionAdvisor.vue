<script setup>
import { computed } from 'vue'
import FormField from './FormField.vue'

// "Danışman" bölümü: ilanda görünecek danışman. Boş seçim = sitedeki varsayılan danışman (advisorId: null).
const props = defineProps({
  form: { type: Object, required: true },
  errors: { type: Object, default: () => ({}) },
  // GET /api/admin/advisors cevabı (aktif ve pasif); pasifler yalnızca ilanın o anki danışmanıysa seçeneklerde görünür.
  advisors: { type: Array, default: () => [] },
  defaultName: { type: String, default: '' }
})

const defaultLabel = computed(() => (props.defaultName ? `Varsayılan danışman (${props.defaultName})` : 'Varsayılan danışman'))
const activeAdvisors = computed(() => props.advisors.filter((a) => a.isActive))
// İlanın mevcut danışmanı pasifse (ya da listede yoksa) seçili kalabilsin diye ayrıca gösterilir.
const currentInactive = computed(() => {
  const id = props.form.advisorId
  if (id === null || id === undefined || activeAdvisors.value.some((a) => a.id === id)) return null
  const found = props.advisors.find((a) => a.id === id)
  return { id, label: found ? `${found.name} (pasif)` : `Danışman no ${id}` }
})

// <select> değerleri metindir; model sayı ya da null tutar.
const selected = computed({
  get: () => (props.form.advisorId === null || props.form.advisorId === undefined ? '' : String(props.form.advisorId)),
  set: (v) => {
    props.form.advisorId = v === '' ? null : Number(v)
  }
})
</script>

<template>
  <section class="af-section" aria-labelledby="sec-advisor">
    <h2 id="sec-advisor">Danışman</h2>
    <div class="af-grid">
      <div class="advisor-field">
        <FormField v-slot="{ attrs }" name="advisorId" label="İlandaki danışman" hint="Seçilmezse sitedeki varsayılan danışman görünür." :error="errors.advisorId">
          <select v-model="selected" v-bind="attrs">
            <option value="">{{ defaultLabel }}</option>
            <option v-if="currentInactive" :value="String(currentInactive.id)">{{ currentInactive.label }}</option>
            <option v-for="a in activeAdvisors" :key="a.id" :value="String(a.id)">{{ a.name }}</option>
          </select>
        </FormField>
      </div>
    </div>
  </section>
</template>

<style scoped>
/* Seçenek etiketleri uzun olabilir (ad + "(pasif)"): alan tüm satırı kaplar, çok geniş de olmaz. */
.advisor-field {
  grid-column: 1 / -1;
  max-width: 30rem;
}
</style>

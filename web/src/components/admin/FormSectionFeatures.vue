<script setup>
import FormField from './FormField.vue'
import { heatingLabels, furnishingLabels, usageLabels, deedLabels, kitchenLabels } from '../../labels.js'

defineProps({
  form: { type: Object, required: true },
  errors: { type: Object, default: () => ({}) }
})

const NUMBERS = [
  ['bathrooms', 'Banyo sayısı', 0],
  ['grossM2', 'Brüt m²', 0],
  ['netM2', 'Net m²', 0],
  ['floor', 'Bulunduğu kat', -5],
  ['totalFloors', 'Kat sayısı', 0],
  ['buildingAge', 'Bina yaşı', 0]
]
const SELECTS = [
  ['heating', 'Isıtma', heatingLabels],
  ['furnishing', 'Eşya durumu', furnishingLabels],
  ['usage', 'Kullanım durumu', usageLabels],
  ['deed', 'Tapu durumu', deedLabels],
  ['kitchen', 'Mutfak', kitchenLabels]
]
const CHECKS = [
  ['balcony', 'Balkon'],
  ['elevator', 'Asansör'],
  ['parking', 'Otopark'],
  ['inSite', 'Site içinde'],
  ['loanEligible', 'Krediye uygun'],
  ['swap', 'Takas']
]
</script>

<template>
  <section class="af-section" aria-labelledby="sec-features">
    <h2 id="sec-features">Özellikler</h2>
    <div class="af-grid">
      <FormField v-slot="{ attrs }" name="rooms" label="Oda sayısı" hint="Örn. 3+1" required :error="errors.rooms">
        <input v-model="form.rooms" v-bind="attrs" type="text" maxlength="20" autocomplete="off" />
      </FormField>
      <FormField v-for="[key, label, min] in NUMBERS" :key="key" v-slot="{ attrs }" :name="key" :label="label" :error="errors[key]">
        <input v-model="form[key]" v-bind="attrs" type="number" :min="min" step="1" inputmode="numeric" />
      </FormField>
      <FormField v-for="[key, label, labels] in SELECTS" :key="key" v-slot="{ attrs }" :name="key" :label="label">
        <select v-model="form[key]" v-bind="attrs">
          <option v-for="(text, value) in labels" :key="value" :value="value">{{ text }}</option>
        </select>
      </FormField>
    </div>
    <fieldset class="af-checks" style="margin-top: 0.75rem">
      <legend>Evet / hayır</legend>
      <label v-for="[key, label] in CHECKS" :key="key" class="af-check">
        <input v-model="form[key]" type="checkbox" :name="key" />
        <span>{{ label }}</span>
      </label>
    </fieldset>
  </section>
</template>

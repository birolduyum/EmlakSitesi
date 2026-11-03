<script setup>
import FormField from './FormField.vue'
import { typeLabels, currencyLabels } from '../../labels.js'

defineProps({
  form: { type: Object, required: true },
  errors: { type: Object, default: () => ({}) }
})
</script>

<template>
  <section class="af-section" aria-labelledby="sec-basics">
    <h2 id="sec-basics">Temel bilgiler</h2>
    <div class="af-grid">
      <div class="af-wide">
        <FormField v-slot="{ attrs }" name="title" label="Başlık" required :error="errors.title">
          <input v-model="form.title" v-bind="attrs" type="text" maxlength="200" autocomplete="off" />
        </FormField>
      </div>
      <FormField
        v-slot="{ attrs }"
        name="badge"
        label="Kısa etiket (rozet)"
        hint="Listede fotoğrafın üstünde görünür, örn. Fırsat"
        :error="errors.badge"
      >
        <input v-model="form.badge" v-bind="attrs" type="text" maxlength="50" autocomplete="off" />
      </FormField>
      <fieldset class="af-plain">
        <legend>İlan türü</legend>
        <div class="af-segmented">
          <label v-for="(label, value) in typeLabels" :key="value" :class="{ on: form.type === value }">
            <input v-model="form.type" type="radio" name="type" :value="value" />
            <span>{{ label }}</span>
          </label>
        </div>
      </fieldset>
      <FormField v-slot="{ attrs }" name="price" label="Fiyat" required :error="errors.price">
        <input v-model="form.price" v-bind="attrs" type="number" min="0" step="0.01" inputmode="decimal" />
      </FormField>
      <FormField v-slot="{ attrs }" name="currency" label="Para birimi">
        <select v-model="form.currency" v-bind="attrs">
          <option v-for="(symbol, value) in currencyLabels" :key="value" :value="value">{{ symbol }} ({{ value }})</option>
        </select>
      </FormField>
      <template v-if="form.type === 'Kiralik'">
        <FormField v-slot="{ attrs }" name="deposit" label="Depozito" :error="errors.deposit">
          <input v-model="form.deposit" v-bind="attrs" type="number" min="0" step="0.01" inputmode="decimal" />
        </FormField>
        <FormField v-slot="{ attrs }" name="aidat" label="Aidat (aylık)" :error="errors.aidat">
          <input v-model="form.aidat" v-bind="attrs" type="number" min="0" step="0.01" inputmode="decimal" />
        </FormField>
      </template>
    </div>
  </section>
</template>

<script setup>
import FormField from './FormField.vue'

defineProps({
  form: { type: Object, required: true },
  errors: { type: Object, default: () => ({}) }
})
</script>

<template>
  <section class="af-section" aria-labelledby="sec-description">
    <h2 id="sec-description">Açıklama</h2>
    <FormField v-slot="{ attrs }" name="description" label="İlan açıklaması" :error="errors.description">
      <textarea v-model="form.description" v-bind="attrs" rows="6"></textarea>
    </FormField>
  </section>
</template>

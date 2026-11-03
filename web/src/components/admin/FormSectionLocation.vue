<script setup>
import FormField from './FormField.vue'

defineProps({
  form: { type: Object, required: true },
  errors: { type: Object, default: () => ({}) }
})
</script>

<template>
  <section class="af-section" aria-labelledby="sec-location">
    <h2 id="sec-location">Konum</h2>
    <div class="af-grid">
      <FormField v-slot="{ attrs }" name="city" label="İl" required :error="errors.city">
        <input v-model="form.city" v-bind="attrs" type="text" maxlength="100" autocomplete="off" />
      </FormField>
      <FormField v-slot="{ attrs }" name="district" label="İlçe" required :error="errors.district">
        <input v-model="form.district" v-bind="attrs" type="text" maxlength="100" autocomplete="off" />
      </FormField>
      <FormField v-slot="{ attrs }" name="neighborhood" label="Mahalle" :error="errors.neighborhood">
        <input v-model="form.neighborhood" v-bind="attrs" type="text" maxlength="100" autocomplete="off" />
      </FormField>
    </div>
  </section>
</template>

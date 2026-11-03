<script setup>
import FormField from './FormField.vue'

defineProps({
  form: { type: Object, required: true },
  errors: { type: Object, default: () => ({}) }
})
</script>

<template>
  <section class="af-section" aria-labelledby="sec-owner">
    <h2 id="sec-owner">Ev sahibi</h2>
    <p class="af-note"><strong>Yalnızca yönetici görür.</strong> Ziyaretçiye ve saha çalışanına hiçbir yerde gösterilmez.</p>
    <div class="af-grid">
      <FormField v-slot="{ attrs }" name="ownerName" label="Ad soyad" required :error="errors.ownerName">
        <input v-model="form.ownerName" v-bind="attrs" type="text" maxlength="200" autocomplete="off" />
      </FormField>
      <FormField v-slot="{ attrs }" name="ownerPhone" label="Telefon" required :error="errors.ownerPhone">
        <input v-model="form.ownerPhone" v-bind="attrs" type="tel" maxlength="50" autocomplete="off" />
      </FormField>
      <div class="af-wide">
        <FormField v-slot="{ attrs }" name="ownerNote" label="Not" :error="errors.ownerNote">
          <textarea v-model="form.ownerNote" v-bind="attrs" rows="3"></textarea>
        </FormField>
      </div>
    </div>
  </section>
</template>

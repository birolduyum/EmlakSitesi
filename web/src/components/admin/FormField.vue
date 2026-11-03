<script setup>
import { computed } from 'vue'
import './adminForm.css'

// Etiket + alan + hata/ipucu: alanın kendisi slot'ta çizilir, erişilebilirlik öznitelikleri `attrs` ile verilir.
const props = defineProps({
  name: { type: String, required: true },
  label: { type: String, required: true },
  error: { type: String, default: '' },
  hint: { type: String, default: '' },
  // Girilen değerin biçimli yansıması (örn. "= 1.250 ₺"); boşsa gösterilmez.
  echo: { type: String, default: '' },
  required: { type: Boolean, default: false }
})

const id = computed(() => `f-${props.name}`)
const attrs = computed(() => {
  const ids = [props.error ? `${id.value}-err` : null, props.hint ? `${id.value}-hint` : null, props.echo ? `${id.value}-echo` : null].filter(Boolean)
  return {
    id: id.value,
    name: props.name,
    'aria-required': props.required ? 'true' : undefined,
    'aria-invalid': props.error ? 'true' : undefined,
    'aria-describedby': ids.length ? ids.join(' ') : undefined
  }
})
</script>

<template>
  <div class="af-field">
    <label :for="id">{{ label }}<span v-if="required" aria-hidden="true"> *</span></label>
    <slot :attrs="attrs"></slot>
    <p v-if="hint" :id="`${id}-hint`" class="af-hint">{{ hint }}</p>
    <p v-if="echo" :id="`${id}-echo`" class="af-echo" data-testid="echo" aria-live="polite">{{ echo }}</p>
    <p v-if="error" :id="`${id}-err`" class="af-error">{{ error }}</p>
  </div>
</template>

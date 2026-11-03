<script setup>
import FormField from './FormField.vue'
import { typeLabels, currencyLabels } from '../../labels.js'
import { formatPrice } from '../../format.js'
import { parseTrDecimal, INVALID_NUMBER_MESSAGE } from '../../numbers.js'

const props = defineProps({
  form: { type: Object, required: true },
  errors: { type: Object, default: () => ({}) }
})

// Yazılan tutarın yansıması: "1.250" yazıldıysa "= 1.250 ₺"; yanlış gruplama kullanıcıya hemen görünür. Boş/geçersizse gizli.
function echo(key) {
  const n = parseTrDecimal(props.form[key])
  return n === null ? '' : `= ${formatPrice(n, props.form.currency)}`
}

// Sunucu/gönderim hatası ya da dolu ama okunamayan metin için satır içi hata.
function error(key) {
  if (props.errors[key]) return props.errors[key]
  const raw = String(props.form[key] ?? '').trim()
  return raw !== '' && parseTrDecimal(raw) === null ? INVALID_NUMBER_MESSAGE : ''
}
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
      <FormField v-slot="{ attrs }" name="price" label="Fiyat" required :error="error('price')" :echo="echo('price')">
        <input v-model="form.price" v-bind="attrs" type="text" inputmode="decimal" autocomplete="off" />
      </FormField>
      <FormField v-slot="{ attrs }" name="currency" label="Para birimi">
        <select v-model="form.currency" v-bind="attrs">
          <option v-for="(symbol, value) in currencyLabels" :key="value" :value="value">{{ symbol }} ({{ value }})</option>
        </select>
      </FormField>
      <template v-if="form.type === 'Kiralik'">
        <FormField v-slot="{ attrs }" name="deposit" label="Depozito" :error="error('deposit')" :echo="echo('deposit')">
          <input v-model="form.deposit" v-bind="attrs" type="text" inputmode="decimal" autocomplete="off" />
        </FormField>
        <FormField v-slot="{ attrs }" name="aidat" label="Aidat (aylık)" :error="error('aidat')" :echo="echo('aidat')">
          <input v-model="form.aidat" v-bind="attrs" type="text" inputmode="decimal" autocomplete="off" />
        </FormField>
      </template>
    </div>
  </section>
</template>

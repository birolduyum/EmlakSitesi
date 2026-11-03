<script setup>
import { computed } from 'vue'
import { useAdvisor } from '../composables/useAdvisor.js'

// "Ara" düğmesi: WhatsApp bağlantısı varsa "WhatsApp'tan Ara" (yeni sekme), yoksa tel: bağlantısı "Ara".
const { info, telHref, whatsappUrl } = useAdvisor()

const href = computed(() => whatsappUrl.value ?? telHref.value)
const label = computed(() => (whatsappUrl.value ? "WhatsApp'tan Ara" : 'Ara'))
const ariaLabel = computed(() => `${label.value}: ${info.value?.advisorName ?? ''}`.trim())
</script>

<template>
  <a
    v-if="href"
    :href="href"
    class="btn call"
    :aria-label="ariaLabel"
    :target="whatsappUrl ? '_blank' : undefined"
    :rel="whatsappUrl ? 'noopener noreferrer' : undefined"
    @click.stop
    >{{ label }}</a
  >
</template>

<style scoped>
.call {
  background: var(--green-600);
  border-color: var(--green-600);
  text-align: center;
  overflow-wrap: anywhere;
}
</style>

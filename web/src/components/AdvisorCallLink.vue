<script setup>
import { computed } from 'vue'
import { useAdvisor, callLabels } from '../composables/useAdvisor.js'

// "Ara" düğmesi: WhatsApp bağlantısı varsa "WhatsApp'tan Ara" (yeni sekme), yoksa tel: bağlantısı "Ara".
const props = defineProps({ advisor: { type: Object, default: null } })
const { info, telHref, whatsappUrl } = useAdvisor(() => props.advisor)

const href = computed(() => whatsappUrl.value ?? telHref.value)
const labels = computed(() => callLabels(!!whatsappUrl.value, info.value?.advisorName))
const label = computed(() => labels.value.label)
const ariaLabel = computed(() => labels.value.aria)
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

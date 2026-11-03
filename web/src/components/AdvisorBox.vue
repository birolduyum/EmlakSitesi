<script setup>
import { useAdvisor } from '../composables/useAdvisor.js'
import AdvisorCallLink from './AdvisorCallLink.vue'

const { info, telHref, whatsappUrl } = useAdvisor()
</script>

<template>
  <div class="advisor">
    <template v-if="info && (info.advisorName || whatsappUrl || telHref)">
      <span v-if="info.advisorName" class="name">
        <span class="role">Danışman</span>
        {{ info.advisorName }}
      </span>
      <AdvisorCallLink />
    </template>
  </div>
</template>

<style scoped>
.advisor {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 0.5rem;
  min-width: 8.5rem;
  max-width: 11rem;
  text-align: center;
}
.name {
  font-weight: 600;
  overflow-wrap: anywhere;
}
.role {
  display: block;
  font-size: 0.75rem;
  font-weight: 400;
  color: var(--muted);
}
.call {
  text-align: center;
}
</style>

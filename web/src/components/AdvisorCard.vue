<script setup>
import { computed } from 'vue'
import { useAdvisor, whatsappMessageUrl } from '../composables/useAdvisor.js'

const props = defineProps({
  showingCount: { type: Number, default: 0 },
  listing: { type: Object, default: null }
})

const { info, telHref, whatsappUrl } = useAdvisor()
// Aynı numara; ikinci düğme ilana özel hazır mesajla açılır. WhatsApp yoksa tel: yedeği ve mesaj düğmesi gizli.
const messageUrl = computed(() => whatsappMessageUrl(whatsappUrl.value, props.listing))
const callAria = computed(() => `${whatsappUrl.value ? "WhatsApp'tan Ara" : 'Ara'}: ${info.value?.advisorName ?? ''}`.trim())
</script>

<template>
  <aside class="advisor-card card" aria-label="Danışman">
    <div v-if="info" class="who">
      <span class="role">Danışman</span>
      <strong v-if="info.advisorName" class="name">{{ info.advisorName }}</strong>
      <span v-if="info.advisorPhone" class="phone">{{ info.advisorPhone }}</span>
    </div>
    <div class="actions">
      <a
        v-if="whatsappUrl"
        :href="whatsappUrl"
        class="btn call"
        :aria-label="callAria"
        target="_blank"
        rel="noopener noreferrer"
        >WhatsApp'tan Ara</a
      >
      <a v-else-if="telHref" :href="telHref" class="btn call" :aria-label="callAria">Ara</a>
      <a v-if="messageUrl" :href="messageUrl" class="btn secondary" target="_blank" rel="noopener noreferrer">WhatsApp'tan Mesaj Gönder</a>
    </div>
    <p v-if="showingCount > 0" class="showing">Bu ilan {{ showingCount }} kez gösterildi</p>
  </aside>
</template>

<style scoped>
.advisor-card {
  display: grid;
  gap: 0.75rem;
  padding: 1rem;
}
.who {
  display: grid;
  gap: 0.125rem;
}
.role {
  font-size: 0.8rem;
  color: var(--muted);
}
.name {
  color: var(--green-900);
}
.actions {
  display: grid;
  gap: 0.5rem;
}
.actions .btn {
  text-align: center;
}
.call {
  background: var(--green-600);
  border-color: var(--green-600);
}
.showing {
  margin: 0;
  font-size: 0.85rem;
  color: var(--muted);
  text-align: center;
}
@media (max-width: 900px) {
  .actions {
    grid-template-columns: 1fr 1fr;
  }
}
</style>

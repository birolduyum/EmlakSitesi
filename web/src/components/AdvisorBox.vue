<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { getSiteInfo } from '../site.js'

const info = ref(null)
let alive = true

onMounted(async () => {
  try {
    const data = await getSiteInfo()
    if (alive && data) info.value = data
  } catch {
    // Danışman kutusu yalnızca bir kolaylıktır; hata olursa boş kalır.
  }
})
onBeforeUnmount(() => {
  alive = false
})

const telHref = computed(() => {
  const phone = info.value?.advisorPhone
  const digits = typeof phone === 'string' ? phone.replace(/[^\d+]/g, '') : ''
  return digits ? `tel:${digits}` : null
})
</script>

<template>
  <div class="advisor">
    <template v-if="info && (info.advisorName || telHref)">
      <span v-if="info.advisorName" class="name">
        <span class="role">Danışman</span>
        {{ info.advisorName }}
      </span>
      <a v-if="telHref" :href="telHref" class="btn call" :aria-label="`Ara: ${info.advisorName ?? ''}`.trim()" @click.stop>Ara</a>
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

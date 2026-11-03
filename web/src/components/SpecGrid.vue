<script setup>
import { computed } from 'vue'
import { formatArea, formatFloor } from '../format.js'

const props = defineProps({
  listing: { type: Object, required: true }
})

function text(v) {
  return v === null || v === undefined || v === '' ? '-' : String(v)
}

const items = computed(() => {
  const l = props.listing
  return [
    { label: 'Oda Sayısı', value: text(l.rooms) },
    { label: 'Banyo Sayısı', value: text(l.bathrooms) },
    { label: 'Brüt / Net m²', value: `${formatArea(l.grossM2)} / ${formatArea(l.netM2)}` },
    { label: 'Bulunduğu Kat', value: formatFloor(l.floor) },
    { label: 'Kat Sayısı', value: text(l.totalFloors) },
    { label: 'Bina Yaşı', value: text(l.buildingAge) }
  ]
})
</script>

<template>
  <div class="spec-grid card" data-testid="spec-grid">
    <div v-for="item in items" :key="item.label" class="box">
      <span class="label">{{ item.label }}</span>
      <strong class="value">{{ item.value }}</strong>
    </div>
  </div>
</template>

<style scoped>
.spec-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 1px;
  overflow: hidden;
  background: var(--border);
}
.box {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
  padding: 0.75rem 1rem;
  background: #fff;
}
.label {
  font-size: 0.8rem;
  color: var(--muted);
}
.value {
  color: var(--green-900);
}
@media (max-width: 600px) {
  .spec-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>

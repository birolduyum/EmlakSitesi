<script setup>
import { computed } from 'vue'
import placeholder from '../assets/placeholder.svg'

const props = defineProps({
  photos: { type: Array, default: () => [] },
  alt: { type: String, default: '' },
  allLink: { type: [String, Object], default: null }
})
const emit = defineEmits(['open'])

// Bir büyük + en çok dört küçük; index orijinal sıradaki yeri korur.
const shown = computed(() => props.photos.slice(0, 5).map((src, index) => ({ src, index })))
const count = computed(() => props.photos.length)
</script>

<template>
  <div class="photo-block" :class="`n-${Math.min(count, 5)}`" data-testid="photo-block">
    <img v-if="!count" class="empty" :src="placeholder" :alt="alt" />
    <button
      v-for="(p, i) in shown"
      :key="p.index"
      type="button"
      data-testid="photo-button"
      class="cell"
      :class="i === 0 ? 'big' : 'small'"
      :aria-label="`Fotoğrafı büyüt: ${p.index + 1} / ${count}`"
      @click="emit('open', p.index, $event.currentTarget)"
    >
      <img :src="p.src" :alt="i === 0 ? alt : ''" :loading="i === 0 ? 'eager' : 'lazy'" decoding="async" />
    </button>
    <router-link v-if="count && allLink" :to="allLink" class="btn all">{{ count }} fotoğrafın tümünü gör</router-link>
  </div>
</template>

<style scoped>
.photo-block {
  position: relative;
  display: grid;
  gap: 8px;
  aspect-ratio: 2 / 1;
  overflow: hidden;
  border-radius: var(--radius);
  background: #eef2ef;
  grid-template-columns: 1fr 1fr;
  grid-template-rows: 1fr 1fr;
}
.cell {
  position: relative;
  min-width: 0;
  min-height: 0;
  padding: 0;
  border: 0;
  border-radius: 0;
  background: #eef2ef;
  overflow: hidden;
}
.cell:hover {
  background: #eef2ef;
}
.cell:focus-visible {
  outline: 3px solid var(--green-600);
  outline-offset: -3px;
}
.cell img,
.empty {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.empty {
  grid-column: 1 / -1;
  grid-row: 1 / -1;
  object-fit: contain;
}
.big {
  grid-column: 1;
  grid-row: 1 / span 2;
}
/* Az fotoğraf: boş kutu bırakma. */
.n-1 .big {
  grid-column: 1 / -1;
}
.n-2 .big {
  grid-column: 1;
}
.n-2 .small {
  grid-column: 2;
  grid-row: 1 / span 2;
}
.n-3 .small:nth-of-type(2) {
  grid-column: 2;
  grid-row: 1;
}
.n-3 .small:nth-of-type(3) {
  grid-column: 2;
  grid-row: 2;
}
.all {
  position: absolute;
  right: 12px;
  bottom: 12px;
  background: rgba(255, 255, 255, 0.95);
  color: var(--green-900);
  border-color: var(--border);
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.25);
}
.all:hover {
  background: #fff;
}
@media (max-width: 900px) {
  .photo-block {
    aspect-ratio: 4 / 3;
    grid-template-columns: 1fr;
    grid-template-rows: 1fr;
  }
  .small {
    display: none;
  }
  .big,
  .n-2 .big,
  .n-1 .big {
    grid-column: 1;
    grid-row: 1;
  }
}
</style>

<script setup>
import { ref, computed, watch } from 'vue'
import { RouterLink } from 'vue-router'
import placeholder from '../assets/placeholder.svg'

const props = defineProps({
  photos: { type: Array, default: () => [] },
  alt: { type: String, default: '' },
  // Verilirse fotoğraf bu adrese giden bir bağlantı olur (orta tık / yeni sekme çalışır).
  to: { type: [String, Object], default: null }
})

const index = ref(0)
watch(
  () => props.photos,
  () => {
    index.value = 0
  }
)

const current = computed(() => (props.photos.length ? props.photos[index.value] ?? props.photos[0] : placeholder))
const showDots = computed(() => props.photos.length > 1)
const mediaTag = computed(() => (props.to ? RouterLink : 'div'))
const mediaAttrs = computed(() => (props.to ? { to: props.to, tabindex: -1, 'aria-hidden': 'true' } : {}))

function show(i) {
  index.value = i
}
function reset() {
  index.value = 0
}
function onFocusOut(event) {
  if (!event.currentTarget.contains(event.relatedTarget)) reset()
}
</script>

<template>
  <div class="strip" @mouseleave="reset" @focusout="onFocusOut">
    <component :is="mediaTag" v-bind="mediaAttrs" class="media">
      <img :src="current" :alt="alt" width="400" height="300" loading="lazy" decoding="async" />
    </component>
    <div v-if="showDots" class="dots" role="group" aria-label="Fotoğraflar">
      <button
        v-for="(_, i) in photos"
        :key="i"
        type="button"
        data-testid="dot"
        class="dot"
        :class="{ active: i === index }"
        :aria-label="`Fotoğraf ${i + 1}`"
        :aria-current="i === index ? 'true' : undefined"
        @mouseenter="show(i)"
        @focus="show(i)"
        @click="show(i)"
      ></button>
    </div>
  </div>
</template>

<style scoped>
.strip {
  position: relative;
  width: 100%;
  aspect-ratio: 4 / 3;
  overflow: hidden;
  border-radius: var(--radius);
  background: #eef2ef;
}
.media {
  display: block;
  width: 100%;
  height: 100%;
}
.media img {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.dots {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  height: 1.75rem;
  padding: 0 0.375rem;
  background: linear-gradient(to top, rgba(0, 0, 0, 0.35), transparent);
}
.dot {
  position: relative;
  flex: 1 1 0;
  min-width: 0;
  padding: 0;
  border: 0;
  border-radius: 0;
  background: transparent;
  cursor: pointer;
}
.dot:hover {
  background: transparent;
}
.dot::after {
  content: '';
  position: absolute;
  left: 2px;
  right: 2px;
  bottom: 0.4rem;
  height: 0.25rem;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.55);
  transition: background 0.15s;
}
.dot.active::after {
  background: #fff;
}
.dot:focus-visible {
  outline: 2px solid #fff;
  outline-offset: -2px;
}
</style>

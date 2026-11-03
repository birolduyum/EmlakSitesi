<script setup>
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useListing } from '../composables/useListing.js'
import { useAdvisor } from '../composables/useAdvisor.js'
import { formatArea } from '../format.js'
import ListingState from '../components/ListingState.vue'
import ListingHeadline from '../components/ListingHeadline.vue'
import Lightbox from '../components/Lightbox.vue'
import placeholder from '../assets/placeholder.svg'

const route = useRoute()
const { listing, state, reload } = useListing(() => route.params.id)
const { telHref } = useAdvisor()

const lightboxIndex = ref(null)
const lightboxOpener = ref(null)
function openLightbox(index, el) {
  lightboxOpener.value = el ?? null
  lightboxIndex.value = index
}
watch(listing, () => {
  lightboxIndex.value = null
})

const photos = computed(() => listing.value?.photoUrls ?? [])
const roomsAndArea = computed(() => `${listing.value?.rooms || '-'} · ${formatArea(listing.value?.grossM2)}`)
// Her 5. fotoğraf (4, 9, ...) tam genişlik kaplar.
const isWide = (i) => i % 5 === 4
</script>

<template>
  <div class="container gallery">
    <ListingState v-if="state !== 'ready'" :state="state" @retry="reload" />
    <template v-else-if="listing">
      <router-link :to="`/ilan/${listing.id}`" class="back">‹ İlana Dön</router-link>

      <div class="layout">
        <div class="photos">
          <div v-if="!photos.length" class="empty" data-testid="empty">
            <img :src="placeholder" :alt="listing.title" />
            <p>Bu ilanda fotoğraf yok.</p>
          </div>
          <div
            v-for="(src, i) in photos"
            :key="i"
            class="item"
            :class="{ wide: isWide(i) }"
            data-testid="gallery-photo"
          >
            <button type="button" :aria-label="`Fotoğrafı büyüt: ${i + 1} / ${photos.length}`" @click="openLightbox(i, $event.currentTarget)">
              <img :src="src" :alt="`${listing.title} - fotoğraf ${i + 1}`" :loading="i < 4 ? 'eager' : 'lazy'" decoding="async" />
            </button>
          </div>
        </div>

        <aside class="summary card" data-testid="summary">
          <ListingHeadline :listing="listing" level="h2" price-first>
            <p class="spec">{{ roomsAndArea }}</p>
          </ListingHeadline>
          <a v-if="telHref" :href="telHref" class="btn call">Ara</a>
        </aside>
      </div>

      <Lightbox v-if="lightboxIndex !== null" :photos="photos" :start-index="lightboxIndex" :opener="lightboxOpener" @close="lightboxIndex = null" />
    </template>
  </div>
</template>

<style scoped>
.gallery {
  padding-top: 1rem;
  padding-bottom: 3rem;
}
.back {
  display: inline-block;
  margin-bottom: 1rem;
  font-weight: 600;
}
.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 18rem;
  gap: 1.5rem;
  align-items: start;
}
.photos {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}
.item {
  min-width: 0;
}
.item.wide {
  grid-column: 1 / -1;
}
.item button {
  display: block;
  width: 100%;
  padding: 0;
  border: 0;
  border-radius: var(--radius);
  overflow: hidden;
  background: #eef2ef;
}
.item button:hover {
  background: #eef2ef;
}
.item img {
  display: block;
  width: 100%;
  aspect-ratio: 4 / 3;
  object-fit: cover;
}
.item.wide img {
  aspect-ratio: 16 / 9;
}
.empty {
  grid-column: 1 / -1;
  text-align: center;
  color: var(--muted);
}
.empty img {
  max-width: 24rem;
}
.summary {
  position: sticky;
  top: 1rem;
  display: grid;
  gap: 1rem;
  padding: 1rem;
}
.spec {
  margin: 0;
}
.call {
  text-align: center;
  background: var(--green-600);
  border-color: var(--green-600);
}
@media (max-width: 900px) {
  .layout {
    display: flex;
    flex-direction: column;
    align-items: stretch;
    gap: 1rem;
  }
  .summary {
    order: -1;
    top: var(--sticky-top, 0px);
    z-index: 5;
    grid-template-columns: 1fr auto;
    align-items: center;
    gap: 0.5rem;
    padding: 0.5rem 0.75rem;
    border-radius: 0;
  }
}
@media (max-width: 600px) {
  .photos {
    grid-template-columns: 1fr;
  }
}
</style>

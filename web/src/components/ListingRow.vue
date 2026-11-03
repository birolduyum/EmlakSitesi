<script setup>
import { computed } from 'vue'
import PhotoStrip from './PhotoStrip.vue'
import AdvisorBox from './AdvisorBox.vue'
import { formatPrice, specLine } from '../format.js'

const props = defineProps({
  listing: { type: Object, required: true }
})

const to = computed(() => `/ilan/${props.listing.id}`)
const price = computed(() => formatPrice(props.listing.price, props.listing.currency, props.listing.type))
const spec = computed(() => specLine(props.listing))
const location = computed(() => [props.listing.city, props.listing.district].filter(Boolean).join(', '))
</script>

<template>
  <article class="row">
    <div class="photo">
      <PhotoStrip :photos="listing.photoUrls ?? []" :alt="listing.title" :to="to" />
      <span v-if="listing.badge" class="badge" data-testid="badge">{{ listing.badge }}</span>
    </div>
    <div class="body">
      <h2 class="title">
        <router-link :to="to" class="stretched">{{ listing.title }}</router-link>
      </h2>
      <p class="location">{{ location }}</p>
      <p class="spec">{{ spec }}</p>
      <p class="price">{{ price }}</p>
    </div>
    <div class="side">
      <AdvisorBox />
    </div>
  </article>
</template>

<style scoped>
.row {
  position: relative;
  display: grid;
  grid-template-columns: minmax(10rem, 14rem) 1fr auto;
  gap: 1rem;
  padding: 0.75rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: #fff;
  transition: box-shadow 0.15s, border-color 0.15s;
}
.row:hover,
.row:focus-within {
  border-color: var(--green-600);
  box-shadow: 0 2px 10px rgba(20, 83, 45, 0.12);
}
/* Fotoğraf ve danışman kutusu, satırı kaplayan bağlantının üstünde kalır. */
.photo,
.side {
  position: relative;
  z-index: 2;
}
.badge {
  position: absolute;
  top: 0.5rem;
  left: 0.5rem;
  max-width: calc(100% - 1rem);
  padding: 0.125rem 0.5rem;
  border-radius: 999px;
  background: var(--green-700);
  color: #fff;
  font-size: 0.75rem;
  font-weight: 600;
  pointer-events: none;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.body {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
.body p {
  margin: 0;
}
.title {
  margin: 0;
  font-size: 1.125rem;
}
.title a {
  color: var(--green-900);
  text-decoration: none;
}
.title a:hover {
  text-decoration: underline;
}
/* Başlık bağlantısı bütün satırı tıklanabilir yapar (orta tık ve klavye de çalışır). */
.stretched::after {
  content: '';
  position: absolute;
  inset: 0;
  z-index: 1;
}
.stretched:focus-visible {
  outline: none;
}
.row:has(.stretched:focus-visible) {
  outline: 2px solid var(--green-600);
  outline-offset: 2px;
}
.location {
  color: var(--muted);
}
.spec {
  font-size: 0.9375rem;
}
.price {
  margin-top: auto !important;
  padding-top: 0.5rem;
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--green-700);
}
.side {
  display: flex;
  align-items: center;
}

@media (max-width: 700px) {
  .row {
    grid-template-columns: 1fr;
  }
  .side {
    justify-content: stretch;
  }
  .side :deep(.advisor) {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    width: 100%;
    text-align: left;
  }
}
</style>

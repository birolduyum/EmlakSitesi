<script setup>
import { computed } from 'vue'
import { formatPrice } from '../format.js'
import { addressOf } from '../composables/useListing.js'

// Başlık, adres ve fiyat bloğu — Detay (başlık h1, adres altta) ve Galeri özeti (fiyat üstte, h2) ortak kullanır.
const props = defineProps({
  listing: { type: Object, required: true },
  level: { type: String, default: 'h1' },
  priceFirst: { type: Boolean, default: false }
})

const price = computed(() => formatPrice(props.listing.price, props.listing.currency, props.listing.type))
const address = computed(() => addressOf(props.listing))
</script>

<template>
  <div class="headline" :class="{ 'price-first': priceFirst }">
    <component :is="level" class="title">{{ listing.title }}</component>
    <slot />
    <p class="address">{{ address }}</p>
    <p class="price" data-testid="price">{{ price }}</p>
  </div>
</template>

<style scoped>
.headline {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
.headline.price-first .price {
  order: -1;
}
.title {
  margin: 0;
  font-size: 1.4rem;
}
.address,
.price {
  margin: 0;
}
.address {
  color: var(--muted);
}
.price {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--green-700);
}
</style>

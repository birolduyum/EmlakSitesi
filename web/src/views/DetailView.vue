<script setup>
import { ref, computed, watch, onBeforeUnmount } from 'vue'
import { useRoute } from 'vue-router'
import { useListing } from '../composables/useListing.js'
import { formatPrice, formatDate } from '../format.js'
import {
  typeLabels, heatingLabels, furnishingLabels, usageLabels, deedLabels, kitchenLabels,
  labelOf, presence, eligibility, yesNo
} from '../labels.js'
import ListingState from '../components/ListingState.vue'
import ListingHeadline from '../components/ListingHeadline.vue'
import DetailPhotos from '../components/DetailPhotos.vue'
import SpecGrid from '../components/SpecGrid.vue'
import FeatureTabs from '../components/FeatureTabs.vue'
import AdvisorCard from '../components/AdvisorCard.vue'
import Lightbox from '../components/Lightbox.vue'

const route = useRoute()
const { listing, state, reload } = useListing(() => route.params.id)

const lightboxIndex = ref(null)
const lightboxOpener = ref(null)
function openLightbox(index, el) {
  lightboxOpener.value = el ?? null
  lightboxIndex.value = index
}
watch(listing, () => {
  lightboxIndex.value = null
})

// Sayfa başlığı: ilan açıkken ilanın başlığı, ayrılınca eski başlık.
const previousTitle = document.title
watch(
  listing,
  (l) => {
    document.title = l ? l.title : previousTitle
  },
  { immediate: true }
)
onBeforeUnmount(() => {
  document.title = previousTitle
})

const category = computed(() => `${labelOf(typeLabels, listing.value?.type)} Daire`)

// Ekmek kırıntısı: her halka listeye ilgili süzgeçle döner.
const crumbs = computed(() => {
  const l = listing.value
  if (!l) return []
  const type = l.type
  const list = [
    { text: 'Ana Sayfa', to: { path: '/', query: { type } } },
    { text: category.value, to: { path: '/', query: { type } } }
  ]
  if (l.city) list.push({ text: l.city, to: { path: '/', query: { type, city: l.city } } })
  if (l.city && l.district) list.push({ text: l.district, to: { path: '/', query: { type, city: l.city, district: l.district } } })
  return list
})

// Künye satırları. Depozito düz tutar, aidat aylıktır ("/ ay"); ikisi de yalnızca kiralıkta ve ilanın kendi para biriminde.
const rows = computed(() => {
  const l = listing.value
  if (!l) return []
  const list = [
    ['İlan Numarası', String(l.id)],
    ['İlan Güncelleme Tarihi', formatDate(l.updatedAt ?? l.createdAt)],
    ['Kategori', category.value],
    ['Isıtma Tipi', labelOf(heatingLabels, l.heating)],
    ['Otopark', presence(l.parking)],
    ['Kullanım Durumu', labelOf(usageLabels, l.usage)],
    ['Krediye Uygunluk', eligibility(l.loanEligible)],
    ['Site İçerisinde', yesNo(l.inSite)],
    ['Tapu Durumu', labelOf(deedLabels, l.deed)],
    ['Takas', yesNo(l.swap)],
    ['Asansör', presence(l.elevator)],
    ['Mutfak', labelOf(kitchenLabels, l.kitchen)],
    ['Eşya Durumu', labelOf(furnishingLabels, l.furnishing)],
    ['Balkon', presence(l.balcony)]
  ]
  if (l.type === 'Kiralik') {
    list.push(['Depozito', formatPrice(l.deposit, l.currency, 'Satilik')])
    list.push(['Aidat', formatPrice(l.aidat, l.currency, 'Kiralik')])
  }
  return list
})
</script>

<template>
  <div class="container detail">
    <ListingState v-if="state !== 'ready'" :state="state" @retry="reload" />
    <template v-else-if="listing">
      <nav class="crumbs" aria-label="Sayfa yolu">
        <template v-for="(c, i) in crumbs" :key="i">
          <span v-if="i > 0" class="sep" aria-hidden="true">›</span>
          <router-link :to="c.to">{{ c.text }}</router-link>
        </template>
      </nav>

      <DetailPhotos
        :photos="listing.photoUrls ?? []"
        :alt="listing.title"
        :all-link="`/ilan/${listing.id}/fotograflar`"
        @open="openLightbox"
      />

      <div class="layout">
        <div class="head">
          <ListingHeadline :listing="listing" />
        </div>
        <div class="side">
          <AdvisorCard :showing-count="listing.showingCount ?? 0" :listing="listing" />
        </div>
        <div class="rest">
          <SpecGrid :listing="listing" />
          <dl class="kunye card">
            <div v-for="[label, value] in rows" :key="label" class="row">
              <dt>{{ label }}</dt>
              <dd>{{ value }}</dd>
            </div>
          </dl>
          <FeatureTabs :tags="listing.tags ?? []" />
          <section class="card description" data-testid="description">
            <h2>{{ listing.title }} Açıklaması</h2>
            <p class="text">{{ listing.description }}</p>
          </section>
        </div>
      </div>

      <Lightbox
        v-if="lightboxIndex !== null"
        :photos="listing.photoUrls ?? []"
        :start-index="lightboxIndex"
        :opener="lightboxOpener"
        :advisor="listing.advisor"
        @close="lightboxIndex = null"
      />
    </template>
  </div>
</template>

<style scoped>
.detail {
  padding-top: 1rem;
  padding-bottom: 3rem;
}
.crumbs {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem;
  margin-bottom: 0.75rem;
  font-size: 0.9rem;
}
.sep {
  color: var(--muted);
}
.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 20rem;
  grid-template-areas:
    'head side'
    'rest side';
  column-gap: 1.5rem;
  row-gap: 1rem;
  margin-top: 1rem;
  align-items: start;
}
.head {
  grid-area: head;
}
.rest {
  grid-area: rest;
  display: grid;
  gap: 1rem;
}
.side {
  grid-area: side;
  position: sticky;
  top: 1rem;
}
.kunye {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0 2rem;
  margin: 0;
  padding: 0.5rem 1rem;
}
.row {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.5rem 0;
  border-bottom: 1px solid var(--border);
}
dt {
  color: var(--muted);
}
dd {
  margin: 0;
  font-weight: 600;
  text-align: right;
}
.description {
  padding: 1rem;
}
.description h2 {
  margin-top: 0;
  font-size: 1.15rem;
}
.text {
  margin: 0;
  white-space: pre-line;
  overflow-wrap: anywhere;
}
@media (max-width: 900px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
    grid-template-areas:
      'head'
      'side'
      'rest';
  }
  .side {
    position: static;
  }
}
@media (max-width: 600px) {
  .kunye {
    grid-template-columns: 1fr;
  }
}
</style>

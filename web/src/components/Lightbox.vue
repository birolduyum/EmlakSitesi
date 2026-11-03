<script setup>
import { ref, computed, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'
import AdvisorCallLink from './AdvisorCallLink.vue'

const props = defineProps({
  photos: { type: Array, default: () => [] },
  startIndex: { type: Number, default: 0 },
  // Açan öğe; Safari/iOS'ta tıklanan düğme odak almadığı için açıkça verilir.
  opener: { type: Object, default: null },
  // İlanın danışmanı (`listing.advisor`); "Ara" düğmesi bunun numarasını kullanır.
  advisor: { type: Object, default: null }
})
const emit = defineEmits(['close'])

function clamp(i) {
  return Math.min(Math.max(Number.isInteger(i) ? i : 0, 0), Math.max(props.photos.length - 1, 0))
}
const index = ref(clamp(props.startIndex))
const dialog = ref(null)
const backButton = ref(null)

const current = computed(() => props.photos[index.value])
const hasPrev = computed(() => index.value > 0)
const hasNext = computed(() => index.value < props.photos.length - 1)

function prev() {
  if (hasPrev.value) index.value--
}
function next() {
  if (hasNext.value) index.value++
}

// Bir sonraki fotoğrafı önceden yükle.
watch(
  index,
  (i) => {
    const url = props.photos[i + 1]
    if (url && typeof Image !== 'undefined') new Image().src = url
  },
  { immediate: true }
)

// Odak ve kaydırma kilidi: açılışta eski durum alınır, kapanışta geri verilir.
const fallbackOpener = typeof document !== 'undefined' ? document.activeElement : null
let previousOverflow = ''

function focusables() {
  return dialog.value ? [...dialog.value.querySelectorAll('a[href], button:not([disabled])')] : []
}

function onKeydown(e) {
  if (e.key === 'Escape') {
    e.preventDefault()
    emit('close')
  } else if (e.key === 'ArrowLeft') {
    prev()
  } else if (e.key === 'ArrowRight') {
    next()
  } else if (e.key === 'Tab') {
    const items = focusables()
    if (!items.length) return
    const first = items[0]
    const last = items[items.length - 1]
    const active = document.activeElement
    if (!dialog.value.contains(active)) {
      e.preventDefault()
      first.focus()
    } else if (e.shiftKey && active === first) {
      e.preventDefault()
      last.focus()
    } else if (!e.shiftKey && active === last) {
      e.preventDefault()
      first.focus()
    }
  }
}

onMounted(() => {
  previousOverflow = document.body.style.overflow
  document.body.style.overflow = 'hidden'
  document.addEventListener('keydown', onKeydown)
  nextTick(() => backButton.value?.focus())
})
onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydown)
  document.body.style.overflow = previousOverflow
  const target = props.opener && document.contains(props.opener) ? props.opener : fallbackOpener
  if (target && target !== document.body && typeof target.focus === 'function' && document.contains(target)) target.focus()
})

// Dokunmatik kaydırma: yatay niyetli (|dy| < |dx|) ve eşiği aşan hareket ileri/geri gider.
const SWIPE_THRESHOLD = 50
let start = null
let swiped = false
function onPointerDown(e) {
  start = e.target?.closest?.('button, a') ? null : { x: e.clientX, y: e.clientY ?? 0 }
}
function onPointerUp(e) {
  if (!start) return
  const dx = e.clientX - start.x
  const dy = (e.clientY ?? 0) - start.y
  start = null
  if (Math.abs(dx) < SWIPE_THRESHOLD || Math.abs(dy) >= Math.abs(dx)) return
  // Arkasından gelen yapay tıklama kapatmasın.
  swiped = true
  setTimeout(() => {
    swiped = false
  }, 400)
  if (dx < 0) next()
  else prev()
}
function onPointerCancel() {
  start = null
}

// Koyu zemine (fotoğrafın dışına) tıklayınca kapanır.
function onStageClick(e) {
  if (swiped) {
    swiped = false
    return
  }
  if (e.target === e.currentTarget) emit('close')
}
</script>

<template>
  <Teleport to="body">
    <div ref="dialog" class="lightbox" role="dialog" aria-modal="true" aria-label="Fotoğraf görüntüleyici">
      <div class="bar">
        <button ref="backButton" type="button" class="bar-btn" data-testid="back" @click="emit('close')">Geri</button>
        <span class="counter" data-testid="counter" aria-live="polite">{{ index + 1 }} / {{ photos.length }}</span>
        <AdvisorCallLink :advisor="advisor" />
      </div>
      <div
        class="stage"
        data-testid="stage"
        @click="onStageClick"
        @pointerdown="onPointerDown"
        @pointerup="onPointerUp"
        @pointercancel="onPointerCancel"
      >
        <img v-if="current" :key="current" :src="current" :alt="`Fotoğraf ${index + 1}`" data-testid="lightbox-image" draggable="false" />
        <button v-if="hasPrev" type="button" class="arrow left" data-testid="prev" aria-label="Önceki fotoğraf" @click.stop="prev">‹</button>
        <button v-if="hasNext" type="button" class="arrow right" data-testid="next" aria-label="Sonraki fotoğraf" @click.stop="next">›</button>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.lightbox {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: flex;
  flex-direction: column;
  background: #000;
  color: #fff;
}
.bar {
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
}
.bar-btn {
  justify-self: start;
  background: transparent;
  border-color: rgba(255, 255, 255, 0.6);
}
.bar-btn:hover {
  background: rgba(255, 255, 255, 0.15);
}
.call {
  justify-self: end;
  background: var(--green-600);
  border-color: var(--green-600);
  color: #fff;
}
.counter {
  grid-column: 2;
  font-variant-numeric: tabular-nums;
}
.stage {
  position: relative;
  flex: 1 1 auto;
  min-height: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 4rem 1rem;
  touch-action: pan-y;
  user-select: none;
}
.stage img {
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
}
.arrow {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  width: 3rem;
  height: 3rem;
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.18);
  font-size: 2rem;
  line-height: 1;
}
.arrow:hover {
  background: rgba(255, 255, 255, 0.35);
}
.arrow.left {
  left: 0.75rem;
}
.arrow.right {
  right: 0.75rem;
}
@media (max-width: 600px) {
  .stage {
    padding: 0 0.5rem 1rem;
  }
}
</style>

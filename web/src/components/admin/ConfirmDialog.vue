<script setup>
import { ref, watch, nextTick, onBeforeUnmount } from 'vue'

// Sayfa içi onay penceresi (window.confirm yerine): odak içeride döner, Escape vazgeçer, kapanınca odak açan öğeye döner.
const props = defineProps({
  open: { type: Boolean, default: false },
  title: { type: String, default: 'Emin misiniz?' },
  message: { type: String, default: '' },
  confirmLabel: { type: String, default: 'Onayla' },
  cancelLabel: { type: String, default: 'Vazgeç' },
  danger: { type: Boolean, default: false }
})
const emit = defineEmits(['confirm', 'cancel'])

const uid = `confirm-${Math.random().toString(36).slice(2, 9)}`
const dialog = ref(null)
const cancelButton = ref(null)
let opener = null

const FOCUSABLE = 'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

function restoreFocus() {
  const target = opener
  opener = null
  if (target && typeof target.focus === 'function' && document.contains(target)) target.focus()
}

watch(
  () => props.open,
  async (isOpen) => {
    if (isOpen) {
      opener = document.activeElement
      await nextTick()
      // Yıkıcı işlemde ilk odak güvenli seçenekte (Vazgeç) durur.
      cancelButton.value?.focus()
    } else {
      await nextTick()
      restoreFocus()
    }
  },
  { immediate: true }
)

onBeforeUnmount(() => {
  if (props.open) restoreFocus()
})

function onKeydown(event) {
  if (event.key === 'Escape') {
    event.stopPropagation()
    emit('cancel')
    return
  }
  if (event.key !== 'Tab') return
  const items = [...(dialog.value?.querySelectorAll(FOCUSABLE) ?? [])]
  if (items.length === 0) {
    event.preventDefault()
    return
  }
  const first = items[0]
  const last = items[items.length - 1]
  const active = document.activeElement
  if (event.shiftKey && (active === first || !dialog.value.contains(active))) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && (active === last || !dialog.value.contains(active))) {
    event.preventDefault()
    first.focus()
  }
}
</script>

<template>
  <div v-if="open" class="backdrop" @click.self="emit('cancel')">
    <div
      ref="dialog"
      class="dialog"
      role="dialog"
      aria-modal="true"
      :aria-labelledby="`${uid}-title`"
      :aria-describedby="message ? `${uid}-msg` : undefined"
      @keydown="onKeydown"
    >
      <h2 :id="`${uid}-title`">{{ title }}</h2>
      <p v-if="message" :id="`${uid}-msg`">{{ message }}</p>
      <div class="actions">
        <button ref="cancelButton" type="button" class="secondary" data-testid="cancel" @click="emit('cancel')">{{ cancelLabel }}</button>
        <button type="button" :class="{ danger }" data-testid="confirm" @click="emit('confirm')">{{ confirmLabel }}</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.backdrop {
  position: fixed;
  inset: 0;
  z-index: 100;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
  background: rgba(17, 24, 39, 0.5);
}
.dialog {
  width: 100%;
  max-width: 28rem;
  padding: 1.25rem;
  border-radius: var(--radius);
  background: #fff;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.25);
}
h2 {
  margin: 0 0 0.5rem;
  font-size: 1.125rem;
}
p {
  margin: 0 0 1rem;
  overflow-wrap: anywhere;
}
.actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}
button.danger {
  border-color: var(--danger);
  background: var(--danger);
}
button.danger:hover {
  background: #7f1d1d;
}
button:focus-visible {
  outline: 2px solid var(--green-600);
  outline-offset: 2px;
}
</style>

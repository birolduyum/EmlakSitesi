<script setup>
import { computed } from 'vue'

const props = defineProps({
  page: { type: Number, required: true },
  pageSize: { type: Number, default: 20 },
  total: { type: Number, default: 0 }
})
const emit = defineEmits(['update:page'])

const pageCount = computed(() => Math.max(1, Math.ceil(props.total / props.pageSize)))

// 1 … 4 5 [6] 7 8 … 20
const pages = computed(() => {
  const last = pageCount.value
  const current = Math.min(props.page, last)
  const set = new Set([1, last])
  for (let p = current - 2; p <= current + 2; p++) if (p >= 1 && p <= last) set.add(p)
  const sorted = [...set].sort((a, b) => a - b)
  const out = []
  let prev = 0
  for (const p of sorted) {
    if (p - prev > 1) out.push('gap')
    out.push(p)
    prev = p
  }
  return out
})

function go(p) {
  if (p < 1 || p > pageCount.value || p === props.page) return
  emit('update:page', p)
}
</script>

<template>
  <nav v-if="pageCount > 1" class="pagination" aria-label="Sayfalama">
    <button type="button" class="secondary" data-testid="prev-page" :disabled="page <= 1" @click="go(page - 1)">
      Önceki
    </button>
    <template v-for="(p, i) in pages" :key="p === 'gap' ? `gap-${i}` : p">
      <span v-if="p === 'gap'" class="gap" aria-hidden="true">…</span>
      <button
        v-else
        type="button"
        class="num"
        :class="{ current: p === page }"
        :aria-current="p === page ? 'page' : undefined"
        :aria-label="`Sayfa ${p}`"
        @click="go(p)"
      >
        {{ p }}
      </button>
    </template>
    <button type="button" class="secondary" data-testid="next-page" :disabled="page >= pageCount" @click="go(page + 1)">
      Sonraki
    </button>
  </nav>
</template>

<style scoped>
.pagination {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  gap: 0.375rem;
  margin: 1.5rem 0;
}
.num {
  min-width: 2.5rem;
  padding: 0.5rem 0.5rem;
  background: #fff;
  color: var(--green-700);
}
.num:hover {
  background: var(--green-50);
}
.num.current {
  background: var(--green-700);
  color: #fff;
}
.secondary:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}
.gap {
  padding: 0 0.25rem;
  color: var(--muted);
}
</style>

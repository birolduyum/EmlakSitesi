<script setup>
import { ref, computed, watch, nextTick } from 'vue'
import { tagGroupLabels } from '../labels.js'

const props = defineProps({
  tags: { type: Array, default: () => [] }
})

const ORDER = ['Ic', 'Dis', 'Konum']
const uid = Math.random().toString(36).slice(2, 8)

// Yalnızca etiketi olan gruplar sekme olur.
const groups = computed(() =>
  ORDER.map((key) => ({ key, label: tagGroupLabels[key], tags: props.tags.filter((t) => t.group === key) })).filter(
    (g) => g.tags.length > 0
  )
)

const selected = ref(null)
const active = computed(() => groups.value.find((g) => g.key === selected.value) ?? groups.value[0] ?? null)
watch(groups, () => {
  if (!groups.value.some((g) => g.key === selected.value)) selected.value = null
})

const tabEls = ref([])

async function onKeydown(event, i) {
  const n = groups.value.length
  let next = null
  if (event.key === 'ArrowRight') next = (i + 1) % n
  else if (event.key === 'ArrowLeft') next = (i - 1 + n) % n
  else if (event.key === 'Home') next = 0
  else if (event.key === 'End') next = n - 1
  if (next === null) return
  event.preventDefault()
  selected.value = groups.value[next].key
  await nextTick()
  tabEls.value[next]?.focus()
}
</script>

<template>
  <section v-if="groups.length" class="features card">
    <div class="tablist" role="tablist" aria-label="İlan özellikleri">
      <button
        v-for="(g, i) in groups"
        :id="`tab-${uid}-${g.key}`"
        :key="g.key"
        :ref="(el) => (tabEls[i] = el)"
        type="button"
        role="tab"
        class="tab"
        :class="{ on: active && active.key === g.key }"
        :aria-selected="active && active.key === g.key ? 'true' : 'false'"
        :aria-controls="`panel-${uid}`"
        :tabindex="active && active.key === g.key ? 0 : -1"
        @click="selected = g.key"
        @keydown="onKeydown($event, i)"
      >
        {{ g.label }}
      </button>
    </div>
    <div v-if="active" :id="`panel-${uid}`" role="tabpanel" :aria-labelledby="`tab-${uid}-${active.key}`" class="panel">
      <ul class="chips">
        <li v-for="t in active.tags" :key="t.id" class="chip"><span aria-hidden="true">✓</span> {{ t.name }}</li>
      </ul>
    </div>
  </section>
</template>

<style scoped>
.features {
  overflow: hidden;
}
.tablist {
  display: flex;
  flex-wrap: wrap;
  border-bottom: 1px solid var(--border);
}
.tab {
  padding: 0.75rem 1rem;
  border: 0;
  border-bottom: 3px solid transparent;
  border-radius: 0;
  background: transparent;
  color: var(--muted);
  font-weight: 600;
}
.tab:hover {
  background: var(--green-50);
}
.tab.on {
  color: var(--green-900);
  border-bottom-color: var(--green-600);
}
.panel {
  padding: 1rem;
}
.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin: 0;
  padding: 0;
  list-style: none;
}
.chip {
  padding: 0.25rem 0.75rem;
  border-radius: 999px;
  background: var(--green-50);
  border: 1px solid var(--green-100);
  color: var(--green-900);
  font-size: 0.9rem;
}
.chip span {
  color: var(--green-600);
}
</style>

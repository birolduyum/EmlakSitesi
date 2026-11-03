<script setup>
import { ref, computed } from 'vue'
import { api } from '../../api.js'
import { tagGroupLabels } from '../../labels.js'

const props = defineProps({
  form: { type: Object, required: true },
  tags: { type: Array, default: () => [] },
  // Etiket listesi yüklenemediyse kullanıcıya söylenir
  loadFailed: { type: Boolean, default: false }
})
const emit = defineEmits(['created'])

const groups = computed(() =>
  Object.entries(tagGroupLabels)
    .map(([group, label]) => ({ group, label, items: props.tags.filter((t) => t.group === group) }))
    .filter((g) => g.items.length > 0)
)

const newName = ref('')
const newGroup = ref('Ic')
const adding = ref(false)
const tagError = ref('')

async function addTag() {
  const name = newName.value.trim()
  if (adding.value) return
  if (name === '') {
    tagError.value = 'Etiket adı boş olamaz'
    return
  }
  adding.value = true
  tagError.value = ''
  try {
    const tag = await api.post('/api/admin/tags', { name, group: newGroup.value })
    emit('created', tag)
    if (!props.form.tagIds.includes(tag.id)) props.form.tagIds.push(tag.id)
    newName.value = ''
  } catch (e) {
    if (e?.status === 409) tagError.value = 'Bu etiket zaten var'
    else if (e?.status === 400 && e.body?.errors?.length) tagError.value = e.body.errors[0]
    else if (e?.status === 0) tagError.value = 'Sunucuya ulaşılamadı'
    else tagError.value = 'Etiket eklenemedi'
  } finally {
    adding.value = false
  }
}
</script>

<template>
  <section class="af-section" aria-labelledby="sec-tags" data-testid="tags">
    <h2 id="sec-tags">Etiketler</h2>
    <p v-if="loadFailed" class="af-error" role="status">Etiket listesi yüklenemedi; mevcut etiketler gösterilemiyor.</p>
    <fieldset v-for="g in groups" :key="g.group" class="af-checks group">
      <legend>{{ g.label }}</legend>
      <label v-for="t in g.items" :key="t.id" class="af-check">
        <input v-model="form.tagIds" type="checkbox" name="tagIds" :value="t.id" />
        <span>{{ t.name }}</span>
      </label>
    </fieldset>

    <div class="add" role="group" aria-label="Yeni etiket ekle">
      <div class="af-field">
        <label for="new-tag-name">Yeni etiket ekle</label>
        <input id="new-tag-name" v-model="newName" name="newTagName" type="text" maxlength="60" autocomplete="off" @keydown.enter.prevent="addTag" />
      </div>
      <div class="af-field">
        <label for="new-tag-group">Grup</label>
        <select id="new-tag-group" v-model="newGroup" name="newTagGroup">
          <option v-for="(label, value) in tagGroupLabels" :key="value" :value="value">{{ label }}</option>
        </select>
      </div>
      <button type="button" class="secondary" data-testid="add-tag" :disabled="adding" @click="addTag">Ekle</button>
    </div>
    <p v-if="tagError" class="af-error" role="alert" data-testid="tag-error">{{ tagError }}</p>
  </section>
</template>

<style scoped>
.group {
  margin-bottom: 0.75rem;
}
.add {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 0.75rem;
  padding-top: 0.75rem;
  border-top: 1px dashed var(--border);
}
</style>

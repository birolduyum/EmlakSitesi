<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { api, ApiError } from '../../api.js'
import { isAdmin } from '../../auth.js'
import { formatDateTime } from '../../format.js'
import { parseDecimal } from '../../numbers.js'
import { errorMessage, useAsyncAction } from '../../composables/useAsyncAction.js'
import FormField from '../../components/admin/FormField.vue'
import '../../components/admin/adminForm.css'

const MAX_RATE = 1000000

const admin = isAdmin()

const state = ref('loading') // loading | ready | error
const original = ref({ usd: null, eur: null }) // sunucudaki değerler (sayı)
const meta = ref({ usd: null, eur: null }) // { updatedAt, updatedBy }
const draft = ref({ usd: '', eur: '' })
const submitted = ref(false)
const status = ref('')
const serverErrors = ref([])

let loadId = 0

function applyRates(list) {
  const find = (c) => (Array.isArray(list) ? list : []).find((r) => r.currency === c)
  const usd = find('USD')
  const eur = find('EUR')
  original.value = { usd: usd?.tryPerUnit ?? null, eur: eur?.tryPerUnit ?? null }
  meta.value = {
    usd: usd ? { updatedAt: usd.updatedAt, updatedBy: usd.updatedBy } : null,
    eur: eur ? { updatedAt: eur.updatedAt, updatedBy: eur.updatedBy } : null
  }
  const show = (n) => (n === null ? '' : String(n).replace('.', ','))
  draft.value = { usd: show(original.value.usd), eur: show(original.value.eur) }
}

async function load() {
  const id = ++loadId
  state.value = 'loading'
  try {
    const data = await api.get('/api/admin/rates')
    if (id !== loadId) return
    applyRates(data)
    state.value = 'ready'
  } catch {
    if (id !== loadId) return
    state.value = 'error'
  }
}
onMounted(() => {
  if (admin) load()
})
onBeforeUnmount(() => loadId++)

const parsed = computed(() => ({ usd: parseDecimal(draft.value.usd), eur: parseDecimal(draft.value.eur) }))

function check(value, name) {
  if (value === null) return `${name} kuru için geçerli bir sayı girin (örn. 41,25).`
  if (value <= 0) return `${name} kuru 0'dan büyük olmalı.`
  if (value > MAX_RATE) return `${name} kuru en çok 1.000.000 olabilir.`
  return ''
}
const fieldErrors = computed(() => ({
  usd: submitted.value ? check(parsed.value.usd, 'Dolar') : '',
  eur: submitted.value ? check(parsed.value.eur, 'Euro') : ''
}))

const changed = computed(() => parsed.value.usd !== original.value.usd || parsed.value.eur !== original.value.eur)

const saving = useAsyncAction(() => api.put('/api/admin/rates', { usd: parsed.value.usd, eur: parsed.value.eur }))

async function save() {
  if (saving.pending.value) return
  status.value = ''
  serverErrors.value = []
  submitted.value = true
  if (check(parsed.value.usd, 'Dolar') || check(parsed.value.eur, 'Euro')) return
  const result = await saving.run()
  if (!result) return
  if (!result.ok) {
    const e = result.error
    if (e instanceof ApiError && e.status === 400 && Array.isArray(e.body?.errors) && e.body.errors.length) {
      serverErrors.value = e.body.errors
    } else {
      serverErrors.value = [errorMessage(e)]
    }
    return
  }
  submitted.value = false
  const sent = { ...parsed.value }
  try {
    applyRates(await api.get('/api/admin/rates'))
  } catch {
    // Yeniden okunamadıysa gönderilen değerler geçerli sayılır.
    original.value = sent
  }
  status.value = 'Kurlar güncellendi — fiyat filtresi artık yeni kurla çalışıyor'
}

const updatedText = (m) => (m ? `Son güncelleme: ${formatDateTime(m.updatedAt)}, ${m.updatedBy}` : 'Henüz güncellenmedi')
</script>

<template>
  <section v-if="admin" class="page">
    <h1>Kurlar</h1>
    <p class="explain">
      Fiyat filtresi ve fiyata göre sıralama, ₺ dışındaki ilanları bu kurla ₺'ye çevirerek karşılaştırır. İlan kartlarındaki fiyatlar değişmez.
    </p>

    <p v-if="state === 'loading'" class="muted" aria-busy="true">Kurlar yükleniyor…</p>

    <div v-else-if="state === 'error'" class="notice" role="alert">
      <p><strong>Kurlar yüklenemedi</strong></p>
      <button type="button" data-testid="retry" @click="load">Tekrar dene</button>
    </div>

    <form v-else class="af-section" novalidate @submit.prevent="save">
      <div class="rate">
        <FormField name="usd" label="1 $ = ? ₺" :error="fieldErrors.usd">
          <template #default="{ attrs }">
            <div class="eq">
              <span aria-hidden="true">1 $ =</span>
              <input v-bind="attrs" v-model="draft.usd" type="text" inputmode="decimal" autocomplete="off" />
              <span aria-hidden="true">₺</span>
            </div>
          </template>
        </FormField>
        <p class="updated">{{ updatedText(meta.usd) }}</p>
      </div>

      <div class="rate">
        <FormField name="eur" label="1 € = ? ₺" :error="fieldErrors.eur">
          <template #default="{ attrs }">
            <div class="eq">
              <span aria-hidden="true">1 € =</span>
              <input v-bind="attrs" v-model="draft.eur" type="text" inputmode="decimal" autocomplete="off" />
              <span aria-hidden="true">₺</span>
            </div>
          </template>
        </FormField>
        <p class="updated">{{ updatedText(meta.eur) }}</p>
      </div>

      <div v-if="serverErrors.length" class="af-error" role="alert">
        <p v-for="(m, i) in serverErrors" :key="i">{{ m }}</p>
      </div>

      <button type="submit" :disabled="saving.pending.value || !changed">Kaydet</button>
      <p class="status" :class="{ empty: !status }" aria-live="polite" data-testid="status">{{ status }}</p>
    </form>
  </section>
</template>

<style scoped>
.page {
  padding: 1rem 0 2rem;
  max-width: 36rem;
}
h1 {
  margin: 0 0 0.5rem;
  font-size: clamp(1.25rem, 1rem + 1.2vw, 1.75rem);
}
.explain,
.muted {
  color: var(--muted);
}
.rate {
  margin-bottom: 1rem;
}
.eq {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.eq input {
  flex: 1 1 auto;
  min-width: 0;
}
.updated {
  margin: 0.25rem 0 0;
  color: var(--muted);
  font-size: 0.875rem;
}
.af-error p {
  margin: 0 0 0.25rem;
}
button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.status {
  min-height: 1.5em;
  margin: 0.75rem 0 0;
  color: var(--green-700);
  font-weight: 600;
}
.notice {
  padding: 1.5rem 1rem;
  border: 1px dashed var(--border);
  border-radius: var(--radius);
  text-align: center;
}
</style>

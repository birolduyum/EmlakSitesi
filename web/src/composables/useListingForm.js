import { reactive, computed, ref, watch } from 'vue'

// İlan formunun modeli: boş form, ayrıntıdan doldurma, API gövdesi (toInput), istemci doğrulaması, kirlilik takibi.
// API gövdesi AdminListingInput ile birebir aynı alanları taşır; fazladan alan gönderilmez.

const TEXT_REQUIRED = ['title', 'city', 'district', 'rooms', 'ownerName', 'ownerPhone']
const TEXT_OPTIONAL = ['badge', 'neighborhood', 'description', 'ownerNote']
const INT_FIELDS = ['bathrooms', 'grossM2', 'netM2', 'floor', 'totalFloors', 'buildingAge']
const ENUM_DEFAULTS = {
  type: 'Satilik', currency: 'TRY', heating: 'Yok', furnishing: 'Esyasiz', usage: 'Bos', deed: 'KatMulkiyeti', kitchen: 'Acik'
}
export const BOOL_FIELDS = ['balcony', 'elevator', 'parking', 'inSite', 'loanEligible', 'swap']

export function emptyForm() {
  const f = { ...ENUM_DEFAULTS, price: '', deposit: '', aidat: '', tagIds: [] }
  for (const k of [...TEXT_REQUIRED, ...TEXT_OPTIONAL]) f[k] = ''
  for (const k of INT_FIELDS) f[k] = ''
  for (const k of BOOL_FIELDS) f[k] = false
  return f
}

// Girdi sayı değilse (boş, NaN, sonsuz, harf) null döner; virgüllü ondalık da anlaşılır.
function toNumber(value) {
  if (value === null || value === undefined || typeof value === 'boolean') return null
  if (typeof value === 'number') return Number.isFinite(value) ? value : null
  const s = String(value).trim().replace(',', '.')
  if (s === '') return null
  const n = Number(s)
  return Number.isFinite(n) ? n : null
}

function trimmed(value) {
  return value === null || value === undefined ? '' : String(value).trim()
}

export function toInput(form) {
  const rent = form.type === 'Kiralik'
  const input = {
    type: form.type,
    currency: form.currency,
    price: toNumber(form.price) ?? 0,
    deposit: rent ? toNumber(form.deposit) : null,
    aidat: rent ? toNumber(form.aidat) : null,
    heating: form.heating,
    furnishing: form.furnishing,
    usage: form.usage,
    deed: form.deed,
    kitchen: form.kitchen,
    tagIds: [...new Set((form.tagIds ?? []).map(Number).filter(Number.isInteger))].sort((a, b) => a - b)
  }
  for (const k of TEXT_REQUIRED) input[k] = trimmed(form[k])
  for (const k of TEXT_OPTIONAL) input[k] = trimmed(form[k]) || null
  for (const k of INT_FIELDS) input[k] = Math.trunc(toNumber(form[k]) ?? 0)
  for (const k of BOOL_FIELDS) input[k] = form[k] === true
  return input
}

const REQUIRED_MESSAGES = {
  title: 'Başlık boş olamaz.',
  city: 'İl boş olamaz.',
  district: 'İlçe boş olamaz.',
  rooms: 'Oda sayısı boş olamaz.',
  ownerName: 'Ev sahibi adı boş olamaz.',
  ownerPhone: 'Ev sahibi telefonu boş olamaz.'
}

// Hızlı geri bildirim için temel kontroller; son söz sunucudadır. Dönüş: { alan: ileti }.
export function validate(form) {
  const errors = {}
  for (const [k, message] of Object.entries(REQUIRED_MESSAGES)) {
    if (trimmed(form[k]) === '') errors[k] = message
  }
  const price = toNumber(form.price)
  if (price === null || price <= 0) errors.price = 'Fiyat sıfırdan büyük olmalıdır.'
  return errors
}

// Sunucu iletisinin başındaki etiketten alanı bulur; tanınmayan ileti null (yalnızca özette görünür).
const ERROR_FIELDS = [
  ['Başlık', 'title'], ['İl ', 'city'], ['İlçe', 'district'], ['Oda sayısı', 'rooms'], ['Ev sahibi adı', 'ownerName'],
  ['Ev sahibi telefonu', 'ownerPhone'], ['Mahalle', 'neighborhood'], ['Rozet', 'badge'], ['Fiyat', 'price'],
  ['Depozito', 'deposit'], ['Aidat', 'aidat'], ['Banyo', 'bathrooms'], ['Brüt', 'grossM2'], ['Net', 'netM2'],
  ['Bulunduğu kat', 'floor'], ['Toplam kat', 'totalFloors'], ['Bina yaşı', 'buildingAge']
]
export function fieldOfError(message) {
  const text = String(message ?? '')
  for (const [prefix, field] of ERROR_FIELDS) {
    if (text.startsWith(prefix)) return field
  }
  return null
}

const str = (v) => (v === null || v === undefined ? '' : v)

// AdminListingDetail → form modeli (denetim ve fotoğraf alanları alınmaz).
export function fromDetail(d) {
  const f = emptyForm()
  for (const k of ['title', 'city', 'district', 'rooms', 'badge', 'neighborhood', 'description']) f[k] = str(d[k])
  for (const k of [...INT_FIELDS, 'price', 'deposit', 'aidat']) f[k] = str(d[k])
  for (const k of Object.keys(ENUM_DEFAULTS)) f[k] = d[k] ?? ENUM_DEFAULTS[k]
  for (const k of BOOL_FIELDS) f[k] = d[k] === true
  f.ownerName = str(d.owner?.name)
  f.ownerPhone = str(d.owner?.phone)
  f.ownerNote = str(d.owner?.note)
  f.tagIds = (d.tags ?? []).map((t) => t.id)
  return f
}

export function useListingForm() {
  const form = reactive(emptyForm())
  const snapshot = ref(JSON.stringify(form))

  // Depozito ve aidat yalnızca kiralıkta vardır: Satılığa dönünce temizlenir (gizli değer sessizce gitmesin).
  watch(
    () => form.type,
    (type) => {
      if (type !== 'Kiralik') {
        form.deposit = ''
        form.aidat = ''
      }
    },
    { flush: 'sync' }
  )

  const dirty = computed(() => JSON.stringify(form) !== snapshot.value)

  function markClean() {
    snapshot.value = JSON.stringify(form)
  }

  function load(detail) {
    Object.assign(form, fromDetail(detail))
    markClean()
  }

  // Boş ilan formuna döner (yeni ilan sayfasına geçişte).
  function reset() {
    Object.assign(form, emptyForm())
    markClean()
  }

  return { form, dirty, markClean, load, reset }
}

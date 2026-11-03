// Sunucudaki sabit listelerin (enum adı) ekranda görünen Türkçe karşılıkları — tek yerde.
export const heatingLabels = {
  Yok: 'Yok',
  Soba: 'Soba',
  Dogalgaz: 'Doğalgaz',
  Kombi: 'Kombi',
  Merkezi: 'Merkezi Sistem',
  YerdenIsitma: 'Yerden Isıtma',
  Klima: 'Klima'
}

export const furnishingLabels = {
  Esyali: 'Eşyalı',
  Esyasiz: 'Eşyasız'
}

export const typeLabels = {
  Satilik: 'Satılık',
  Kiralik: 'Kiralık'
}

export const currencyLabels = {
  TRY: '₺',
  USD: '$',
  EUR: '€'
}

export const sortLabels = {
  Newest: 'En yeni',
  PriceAsc: 'Fiyat: artan',
  PriceDesc: 'Fiyat: azalan'
}

export function labelOf(map, name) {
  return map[name] ?? name
}

export const usageLabels = {
  Bos: 'Boş',
  KiraciOturuyor: 'Kiracı Oturuyor',
  MalSahibiOturuyor: 'Mal Sahibi Oturuyor'
}

export const deedLabels = {
  KatMulkiyeti: 'Kat Mülkiyeti',
  KatIrtifaki: 'Kat İrtifakı',
  HisseliTapu: 'Hisseli Tapu',
  MustakilTapu: 'Müstakil Tapu'
}

export const kitchenLabels = {
  Acik: 'Açık',
  Kapali: 'Kapalı',
  Amerikan: 'Amerikan'
}

export const tagGroupLabels = {
  Ic: 'İç Özellikler',
  Dis: 'Dış Özellikler',
  Konum: 'Konum Özellikleri'
}

// Evet/hayır alanları için tek kural: fiziksel özellik (otopark, asansör, balkon) Var/Yok,
// krediye uygunluk Uygun/Değil, takas ve site içi Evet/Hayır.
export const presence = (v) => (v ? 'Var' : 'Yok')
export const eligibility = (v) => (v ? 'Uygun' : 'Değil')
export const yesNo = (v) => (v ? 'Evet' : 'Hayır')

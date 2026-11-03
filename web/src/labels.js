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

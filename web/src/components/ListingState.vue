<script setup>
// Yükleniyor / bulunamadı / hata durumları — Detay ve Galeri ortak kullanır.
defineProps({
  state: { type: String, required: true }
})
defineEmits(['retry'])
</script>

<template>
  <div v-if="state === 'loading'" class="skeleton" aria-busy="true" aria-label="Yükleniyor" data-testid="loading">
    <div class="bar wide"></div>
    <div class="bar"></div>
    <div class="bar short"></div>
  </div>
  <div v-else-if="state === 'notfound'" class="message" role="alert">
    <h1>İlan bulunamadı</h1>
    <p>Aradığınız ilan kaldırılmış ya da hiç var olmamış olabilir.</p>
    <router-link to="/" class="btn">Ana Sayfaya Dön</router-link>
  </div>
  <div v-else-if="state === 'error'" class="message" role="alert">
    <h1>İlan yüklenemedi</h1>
    <p>Bir sorun oluştu. Lütfen tekrar deneyin.</p>
    <button type="button" data-testid="retry" @click="$emit('retry')">Tekrar dene</button>
  </div>
</template>

<style scoped>
.skeleton {
  display: grid;
  gap: 1rem;
  padding: 2rem 0;
}
.bar {
  height: 2rem;
  border-radius: var(--radius);
  background: linear-gradient(90deg, #eef2ef, #f7faf8, #eef2ef);
}
.bar.wide {
  height: 16rem;
}
.bar.short {
  width: 40%;
}
.message {
  padding: 3rem 0;
  text-align: center;
}
</style>

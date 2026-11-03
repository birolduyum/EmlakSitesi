<script setup>
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { login } from '../../auth.js'

const route = useRoute()
const router = useRouter()

const username = ref('')
const password = ref('')
const pending = ref(false)
const error = ref('')

// Yalnızca yönetim alanı içindeki bir yola dönülür (başka siteye yönlendirme açığı olmasın).
const target = computed(() => {
  const r = route.query.redirect
  const ok = typeof r === 'string' && /^\/admin(\/|\?|$)/.test(r) && !r.startsWith('/admin/giris')
  return ok ? r : '/admin'
})

async function submit() {
  if (pending.value) return
  if (username.value.trim() === '' || password.value === '') {
    error.value = 'Kullanıcı adı ve parolayı girin'
    return
  }
  pending.value = true
  error.value = ''
  try {
    await login(username.value.trim(), password.value)
    password.value = ''
    await router.replace(target.value)
  } catch (e) {
    error.value = e?.status === 401 ? 'Kullanıcı adı veya parola hatalı' : e?.status === 0 ? 'Sunucuya ulaşılamadı' : 'Giriş yapılamadı, lütfen tekrar deneyin'
    password.value = ''
  } finally {
    pending.value = false
  }
}
</script>

<template>
  <section class="login">
    <h1>Yönetici Girişi</h1>
    <form class="card" @submit.prevent="submit">
      <p v-if="error" class="error" role="alert">{{ error }}</p>
      <div class="field">
        <label for="login-username">Kullanıcı adı</label>
        <input id="login-username" v-model="username" name="username" type="text" autocomplete="username" autocapitalize="none" spellcheck="false" />
      </div>
      <div class="field">
        <label for="login-password">Parola</label>
        <input id="login-password" v-model="password" name="password" type="password" autocomplete="current-password" />
      </div>
      <button type="submit" :disabled="pending">{{ pending ? 'Giriş yapılıyor…' : 'Giriş yap' }}</button>
    </form>
  </section>
</template>

<style scoped>
.login {
  max-width: 24rem;
  margin: 2rem auto;
}
.card {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1.25rem;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
.field label {
  font-weight: 600;
  color: var(--green-900);
}
.error {
  margin: 0;
  padding: 0.5rem 0.75rem;
  border-radius: var(--radius);
  background: #fef2f2;
}
button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
input:focus-visible {
  outline: 2px solid var(--green-600);
  outline-offset: 1px;
}
</style>

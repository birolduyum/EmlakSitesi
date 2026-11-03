<script setup>
import { useRouter } from 'vue-router'
import { currentUser, isAdmin, signOut as doSignOut } from '../../auth.js'
import { roleLabels, labelOf } from '../../labels.js'

// Giriş dışındaki bütün yönetim sayfalarının çerçevesi: menü, kullanıcı adı + rol, çıkış.
// (Site başlığı bağlantısı App.vue'daki üst şeritte durur.)
const router = useRouter()
const user = currentUser()
const admin = isAdmin()

function signOut() {
  return doSignOut(router)
}
</script>

<template>
  <div class="admin">
    <div class="bar">
      <nav class="menu" aria-label="Yönetim menüsü">
        <router-link to="/admin" exact-active-class="current" :class="{ current: $route.path.startsWith('/admin/ilan') }">İlanlar</router-link>
        <router-link to="/admin/gosterim" active-class="current">Gösterme</router-link>
        <template v-if="admin">
          <router-link to="/admin/rapor" active-class="current">Rapor</router-link>
          <router-link to="/admin/kur" active-class="current">Kur</router-link>
        </template>
      </nav>
      <div class="who">
        <span v-if="user" class="name" data-testid="who">{{ user.username }} <span class="role">({{ labelOf(roleLabels, user.role) }})</span></span>
        <button type="button" class="secondary" data-testid="logout" @click="signOut">Çıkış</button>
      </div>
    </div>
    <router-view v-slot="{ Component, route }">
      <component :is="Component" :key="route.path" />
    </router-view>
  </div>
</template>

<style scoped>
.bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  padding: 0.5rem 0;
  border-bottom: 1px solid var(--border);
}
.menu {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}
.menu a {
  padding: 0.375rem 0.75rem;
  border-radius: var(--radius);
  color: var(--green-900);
  font-weight: 600;
  text-decoration: none;
}
.menu a:hover {
  background: var(--green-50);
}
.menu a.current {
  background: var(--green-100);
  color: var(--green-900);
}
.menu a:focus-visible {
  outline: 2px solid var(--green-600);
  outline-offset: 2px;
}
.who {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}
.role {
  color: var(--muted);
}
</style>

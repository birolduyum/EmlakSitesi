import { createApp } from 'vue'
import App from './App.vue'
import router from './router.js'
import { setUnauthorizedHandler } from './api.js'
import './styles.css'

setUnauthorizedHandler(() => {
  router.push('/admin/giris')
})

createApp(App).use(router).mount('#app')

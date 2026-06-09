import { createApp } from 'vue'
import { createPinia } from 'pinia'
import '@tfoodies/design-tokens/tokens.css'
import './style.css'
import App from './App.vue'
import { router } from './router'
import { setAccessTokenProvider } from './lib/apiClient'
import { useAuthStore } from './stores/auth'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(router)

// Let the API client read the in-memory access token from the auth store.
const auth = useAuthStore(pinia)
setAccessTokenProvider(() => auth.accessToken)

app.mount('#app')

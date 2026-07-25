import { createApp } from 'vue'
import { createPinia } from 'pinia'
import '@tfoodies/design-tokens/tokens.css'
import './style.css'
import App from './App.vue'
import { router } from './router'
import { setAccessTokenProvider } from './lib/apiClient'
import { installZoomReset } from './lib/viewportZoom'
import { useAuthStore } from './stores/auth'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(router)

// 手機上雙指放大後，轉頁/重整會沿用縮放狀態，於此統一復原（見 lib/viewportZoom.ts）
installZoomReset(router)

// Let the API client read the in-memory access token from the auth store.
const auth = useAuthStore(pinia)
setAccessTokenProvider(() => auth.accessToken)

app.mount('#app')

import { defineStore } from 'pinia'
import { apiFetch } from '../lib/apiClient'

// Storage keys (all non-sensitive: username/perms are display-only; real enforcement is server-side)
const KEYS = {
  refresh: 'admin_rt',
  username: 'admin_user',
  perms: 'admin_perms',
}

interface LoginResponse {
  accessToken: string
  refreshToken: string
  username: string
  permissions: string[]
}

interface RefreshResponse {
  accessToken: string
  refreshToken: string
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    // Access token: memory only (XSS-safe, lost on refresh — restored via refreshToken below)
    accessToken: null as string | null,
    // Username and permissions: persisted to localStorage for immediate display on page reload
    username: localStorage.getItem(KEYS.username) ?? '',
    permissions: JSON.parse(localStorage.getItem(KEYS.perms) ?? '[]') as string[],
  }),
  getters: {
    isAuthenticated: (s) => !!s.accessToken,
  },
  actions: {
    // Called once per page load (router guard). Silently restores the access token
    // using the stored refresh token. No-op if no stored token exists.
    async initialize() {
      const rt = localStorage.getItem(KEYS.refresh)
      if (!rt) return
      try {
        const res = await apiFetch<RefreshResponse>('/auth/refresh', {
          method: 'POST',
          body: JSON.stringify({ refreshToken: rt }),
        })
        this.accessToken = res.accessToken
        localStorage.setItem(KEYS.refresh, res.refreshToken)
      } catch {
        // Refresh token expired or revoked — clear session and let user log in again
        this._clearSession()
      }
    },
    async login(username: string, password: string) {
      const res = await apiFetch<LoginResponse>('/auth/admin/login', {
        method: 'POST',
        body: JSON.stringify({ username, password }),
      })
      this.accessToken = res.accessToken
      this.username = res.username
      this.permissions = res.permissions
      localStorage.setItem(KEYS.refresh, res.refreshToken)
      localStorage.setItem(KEYS.username, res.username)
      localStorage.setItem(KEYS.perms, JSON.stringify(res.permissions))
    },
    async logout() {
      try {
        await apiFetch('/auth/admin/logout', { method: 'POST' })
      } finally {
        this._clearSession()
      }
    },
    can(permission: string): boolean {
      return this.permissions.includes(permission)
    },
    _clearSession() {
      this.accessToken = null
      this.username = ''
      this.permissions = []
      localStorage.removeItem(KEYS.refresh)
      localStorage.removeItem(KEYS.username)
      localStorage.removeItem(KEYS.perms)
    },
  },
})

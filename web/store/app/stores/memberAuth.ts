import { defineStore } from 'pinia'

export interface MemberAuthState {
  accessToken: string | null
  memberName: string
  memberId: string
}

const STORAGE_KEY = 'tfoodies.auth'

// 後端 JWT 以 ClaimTypes.Name 簽發姓名，序列化後 claim key 為 `unique_name`（非 `name`）。
function nameFromPayload(payload: Record<string, unknown>): string | undefined {
  return (
    (payload['unique_name'] as string) ??
    (payload['name'] as string) ??
    (payload['memberName'] as string) ??
    undefined
  )
}

function decodeJwtPayload(token: string): Record<string, unknown> {
  try {
    const parts = token.split('.')
    if (parts.length < 2) return {}
    // base64url → base64
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(base64.length + (4 - (base64.length % 4)) % 4, '=')
    const json = atob(padded)
    return JSON.parse(json)
  } catch {
    return {}
  }
}

export const useMemberAuthStore = defineStore('memberAuth', {
  state: (): MemberAuthState => ({
    accessToken: null,
    memberName: '',
    memberId: '',
  }),

  getters: {
    isAuthenticated: (s) => !!s.accessToken,
  },

  actions: {
    // Restore a saved session on the client (login otherwise resets on every reload).
    // Expired JWTs are discarded so a stale token never looks "logged in".
    // "記住帳號" (remember me) → localStorage (survives browser close, like the legacy
    // 3-month `tfd` cookie); unchecked → sessionStorage (cleared when the tab closes).
    hydrate() {
      if (!import.meta.client) return
      const raw = localStorage.getItem(STORAGE_KEY) ?? sessionStorage.getItem(STORAGE_KEY)
      if (!raw) return
      try {
        const saved = JSON.parse(raw) as MemberAuthState
        if (!saved.accessToken) return
        const exp = decodeJwtPayload(saved.accessToken)['exp']
        if (typeof exp === 'number' && exp * 1000 < Date.now()) {
          localStorage.removeItem(STORAGE_KEY)
          sessionStorage.removeItem(STORAGE_KEY)
          return
        }
        this.accessToken = saved.accessToken
        // 從 token 重新導出姓名，修正舊版曾把手機號碼存進 memberName 的情況。
        this.memberName = nameFromPayload(decodeJwtPayload(saved.accessToken)) ?? saved.memberName ?? ''
        this.memberId = saved.memberId ?? ''
      } catch {
        localStorage.removeItem(STORAGE_KEY)
        sessionStorage.removeItem(STORAGE_KEY)
      }
    },
    persist(remember = true) {
      if (!import.meta.client) return
      const payload = JSON.stringify({
        accessToken: this.accessToken,
        memberName: this.memberName,
        memberId: this.memberId,
      })
      // Write to exactly one store so the other can't resurrect a stale session.
      if (remember) {
        localStorage.setItem(STORAGE_KEY, payload)
        sessionStorage.removeItem(STORAGE_KEY)
      } else {
        sessionStorage.setItem(STORAGE_KEY, payload)
        localStorage.removeItem(STORAGE_KEY)
      }
    },

    async login(mobile: string, password: string, remember = true) {
      const config = useRuntimeConfig()
      const res = await $fetch<{ accessToken: string; memberName?: string }>(
        `${config.public.apiBase}/auth/login`,
        {
          method: 'POST',
          // API 以 camelCase 反序列化（大小寫敏感）；用 PascalCase 會被視為缺欄位回 400。
          body: { role: 'member', identifier: mobile, password },
        },
      )
      this.accessToken = res.accessToken
      const payload = decodeJwtPayload(res.accessToken)
      // Try common JWT claim names for member id and name
      this.memberId =
        (payload['sub'] as string) ??
        (payload['memberId'] as string) ??
        (payload['nameid'] as string) ??
        ''
      this.memberName = res.memberName ?? nameFromPayload(payload) ?? mobile
      this.persist(remember)
    },

    logout() {
      this.accessToken = null
      this.memberName = ''
      this.memberId = ''
      if (import.meta.client) {
        localStorage.removeItem(STORAGE_KEY)
        sessionStorage.removeItem(STORAGE_KEY)
      }
    },
  },
})

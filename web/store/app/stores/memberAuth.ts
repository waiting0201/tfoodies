import { defineStore } from 'pinia'

export interface MemberAuthState {
  accessToken: string | null
  memberName: string
  memberId: string
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
    async login(mobile: string, password: string) {
      const config = useRuntimeConfig()
      const res = await $fetch<{ accessToken: string; memberName?: string }>(
        `${config.public.apiBase}/auth/login`,
        {
          method: 'POST',
          body: { Role: 'member', Identifier: mobile, Password: password },
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
      this.memberName =
        res.memberName ??
        (payload['name'] as string) ??
        (payload['memberName'] as string) ??
        mobile
    },

    logout() {
      this.accessToken = null
      this.memberName = ''
      this.memberId = ''
    },
  },
})

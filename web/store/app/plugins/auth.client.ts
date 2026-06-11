// Restore the saved member session as early as possible on the client, before any page's
// auth guard runs. Pairs with `routeRules['/Member/**'].ssr = false` (nuxt.config) so the
// guarded pages render client-side — where localStorage (the token store) actually exists —
// instead of redirecting to /Member/Login during SSR.
export default defineNuxtPlugin(() => {
  useMemberAuthStore().hydrate()
})

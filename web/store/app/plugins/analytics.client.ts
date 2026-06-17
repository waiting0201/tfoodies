// 在瀏覽器端載入 Google 代碼管理工具(GTM)。GA4、Meta Pixel、Google Ads 轉換等所有追蹤
// 都在 GTM 後台掛載；本檔只負責把 GTM 容器載進來並初始化 dataLayer。
// 容器 ID 由 runtimeConfig.public.gtmId 提供（NUXT_PUBLIC_GTM_ID）；留空則不載入（例如本機開發）。
export default defineNuxtPlugin(() => {
  const id = useRuntimeConfig().public.gtmId
  if (!id) return

  const w = window as unknown as { dataLayer?: Record<string, unknown>[] }
  w.dataLayer = w.dataLayer || []
  w.dataLayer.push({ 'gtm.start': Date.now(), event: 'gtm.js' })

  const s = document.createElement('script')
  s.async = true
  s.src = `https://www.googletagmanager.com/gtm.js?id=${id}`
  document.head.appendChild(s)
})

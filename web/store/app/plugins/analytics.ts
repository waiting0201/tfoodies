// 在 <head> 以 GTM 官方標準片段注入容器：SSR 階段就寫進 HTML（view-source 看得到），
// 且在 hydration 前就開始載入 gtm.js（更早、較不會漏掉秒跳出訪客的事件）。
// GA4 / Meta Pixel / Google Ads 皆掛在此容器下；容器 ID 由 NUXT_PUBLIC_GTM_ID 提供，空則不載入。
export default defineNuxtPlugin(() => {
  const id = useRuntimeConfig().public.gtmId
  if (!id) return
  useHead({
    script: [
      {
        key: 'gtm',
        innerHTML:
          `(function(w,d,s,l,i){w[l]=w[l]||[];w[l].push({'gtm.start':new Date().getTime(),event:'gtm.js'});` +
          `var f=d.getElementsByTagName(s)[0],j=d.createElement(s),dl=l!='dataLayer'?'&l='+l:'';j.async=true;` +
          `j.src='https://www.googletagmanager.com/gtm.js?id='+i+dl;f.parentNode.insertBefore(j,f);` +
          `})(window,document,'script','dataLayer','${id}');`,
      },
    ],
  })
})

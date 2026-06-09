// https://nuxt.com/docs/api/configuration/nuxt-config
//
// Front store = SSR for SEO. URL structure matches legacy RouteConfig.cs 1:1
// (indexed by Google at https://www.tfoodies.com).
//
// VISUAL FIDELITY (plan §7.1): the legacy main.css + assets are served VERBATIM from
// /public (NOT imported through Vite, so url(../images/...) keeps resolving). Pages/
// components reproduce the exact legacy DOM/classes. jQuery + the original visual plugins
// (slick/owl/bxSlider/slidebars/magnificPopup/sticky/tabber) load at body close, same as
// the legacy _Scripts.cshtml, so carousels/mobile-menu/lightbox behave identically.
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },

  modules: ['@pinia/nuxt'],

  runtimeConfig: {
    public: {
      apiBase: 'http://localhost:7071/api',
      siteUrl: 'https://www.tfoodies.com',
    },
  },

  app: {
    head: {
      htmlAttrs: { lang: 'zh-Hant-TW' },
      titleTemplate: '%s｜食在呼 TFoodies',
      link: [
        { rel: 'icon', type: 'image/x-icon', href: '/content/images/favicon.ico' },
        // Same Google Fonts as legacy _Styles.cshtml.
        { rel: 'stylesheet', href: 'https://fonts.googleapis.com/css?family=GFS+Didot|Noto+Sans' },
        // Legacy stylesheets, served verbatim from /public/content/styles.
        { rel: 'stylesheet', href: '/content/styles/jquery-confirm.min.css' },
        { rel: 'stylesheet', href: '/content/styles/main.css' },
      ],
      script: [
        // Same load order as legacy _Scripts.cshtml; at body close so the DOM exists.
        { src: '/scripts/vendor.js', tagPosition: 'bodyClose' },
        { src: '/scripts/plugins.js', tagPosition: 'bodyClose' },
        { src: '/scripts/jquery.validate.min.js', tagPosition: 'bodyClose' },
        { src: '/scripts/jquery-confirm.min.js', tagPosition: 'bodyClose' },
        { src: '/scripts/main.js', tagPosition: 'bodyClose' },
      ],
    },
  },
})

// Legacy visual effects (slick / owl / sticky / mobile-menu …) driven by the original
// jQuery plugins. The legacy main.js initialises everything on `$(document).ready`, which
// under Nuxt SSR fires BEFORE Vue finishes hydrating: the plugins mutate the DOM (owl-loaded,
// cloned slick slides, sticky-wrapper), Vue then sees a hydration mismatch and re-renders from
// its virtual DOM — wiping every carousel/slider the plugins just built. Net result on the page
// is the effects are missing (static stacked content, no arrows/autoplay, no sticky header).
//
// Fix: don't load main.js the racing way. Instead, after the first page has hydrated we inject
// main.js once on a STABLE DOM (binds the global chrome — sticky header, mobile menu, nav/cart
// hover — plus the first page's sliders, all cleanly). On every subsequent client-side
// navigation we rebuild the page's content sliders (slick/owl/tabber) for the freshly rendered
// DOM, since main.js's one-time `ready` handler never fires again on SPA route changes.
//
// Slider options below are copied verbatim from reference/old `main.js` to preserve identical
// behaviour. Each (re)init destroys any existing instance first so it is safe to call repeatedly.

type JQ = ((sel: unknown) => any) & { fn?: { slick?: unknown; sticky?: unknown } }

function getJQuery(): JQ | null {
  const $ = (window as unknown as { $?: JQ }).$
  return $ && $.fn && $.fn.slick ? $ : null
}

function slick($: JQ, sel: string, opts: Record<string, unknown>) {
  const el = $(sel)
  if (!el.length) return
  if (el.hasClass('slick-initialized')) {
    try { el.slick('unslick') } catch { /* not yet initialised */ }
  }
  el.slick(opts)
}

function reinitSliders() {
  const $ = getJQuery()
  if (!$) return

  // Synced product/recipe carousels — order matters (the *-for slider references the *-nav).
  slick($, '.slider-for', { slidesToShow: 1, slidesToScroll: 1, arrows: false, fade: true, asNavFor: '.slider-nav' })
  slick($, '.slider-nav', { slidesToShow: 3, slidesToScroll: 1, asNavFor: '.slider-for', dots: true, centerMode: true, focusOnSelect: true })
  slick($, '.video-slider-for', { slidesToShow: 1, slidesToScroll: 1, adaptiveHeight: true, arrows: false, fade: true, asNavFor: '.video-slider-nav' })
  slick($, '.video-slider-nav', { slidesToShow: 3, slidesToScroll: 1, asNavFor: '.video-slider-for', dots: true, centerMode: true, focusOnSelect: true })
  slick($, '.single-item', { arrows: true, fade: true, autoplay: true, dots: true })

  // Hot/latest product strips.
  slick($, '.responsive', {
    dots: false, infinite: true, speed: 3000, autoplay: true, autoplaySpeed: 2000, slidesToShow: 4, slidesToScroll: 4,
    responsive: [
      { breakpoint: 1024, settings: { speed: 2500, slidesToShow: 3, slidesToScroll: 3, infinite: true, dots: false } },
      { breakpoint: 768, settings: { speed: 2000, slidesToShow: 2, slidesToScroll: 2 } },
      { breakpoint: 320, settings: { speed: 1500, slidesToShow: 1, slidesToScroll: 1 } },
    ],
  })

  // bxSlider strips (no idempotency hook in bx; only init when not already wrapped).
  const bx = $('.bxslider')
  if (bx.length && !bx.closest('.bx-wrapper').length) {
    bx.bxSlider({ speed: 500, mode: 'horizontal', auto: true, pager: true, controls: true, adaptiveHeight: true })
  }
  const allbx = $('.allslider')
  if (allbx.length && !allbx.closest('.bx-wrapper').length) {
    allbx.bxSlider({ speed: 3000, mode: 'fade', auto: true, pager: true, controls: false, adaptiveHeight: true })
  }

  // Owl banner carousel.
  const owl = $('.owl-carousel')
  if (owl.length) {
    if (owl.hasClass('owl-loaded')) {
      try { owl.trigger('destroy.owl.carousel'); owl.removeClass('owl-loaded owl-hidden') } catch { /* not yet initialised */ }
    }
    owl.owlCarousel({ items: 1, merge: true, loop: true, margin: 10, video: true, lazyLoad: true, nav: true, dots: false, center: true, autoplay: true, autoplayHoverPause: true })
  }

  // Tab strips.
  if ($('#tabber').length && $.fn.tabber) {
    try { $('#tabber').tabber({ anchor: '.tabber-anchor', content: '.tabber-content' }) } catch { /* idempotent enough */ }
  }
}

function loadMainJs(): Promise<void> {
  return new Promise((resolve) => {
    if (document.getElementById('legacy-main-js')) return resolve()
    const s = document.createElement('script')
    s.id = 'legacy-main-js'
    s.src = '/scripts/main.js'
    s.onload = () => resolve()
    s.onerror = () => resolve()
    document.body.appendChild(s)
  })
}

export default defineNuxtPlugin((nuxtApp) => {
  let firstPage = true
  nuxtApp.hook('page:finish', async () => {
    // Wait for Vue to commit the route's DOM before touching it with jQuery.
    await nextTick()
    if (firstPage) {
      firstPage = false
      // First load: hydration is done, so main.js now runs on a stable DOM (global chrome +
      // this page's sliders). Re-sync the content sliders afterwards to guarantee a clean state.
      await loadMainJs()
      // Remove the legacy hover-to-open mini-cart (main.js binds mouseenter/mouseleave on
      // .shopping-cart). The badge is the cart indicator now; the dropdown only appears
      // briefly on add-to-cart (driven by SiteHeader).
      const $ = (window as unknown as { $?: { (s: string): { off(e: string): void } } }).$
      if ($) $('.shopping-cart').off('mouseenter mouseleave')
      reinitSliders()
    } else {
      // Client-side navigation: main.js's one-time bindings already exist; only the page's
      // content sliders need rebuilding for the newly rendered DOM.
      reinitSliders()
    }
  })
})

<script setup lang="ts">
// 首頁 Facebook 粉絲專頁外掛（timeline），響應式（RWD）。
//
// 用 Facebook 官方 iframe 版外掛(plugins/page.php)，不載任何 FB JS-SDK：自包 iframe、無全域
// callback、SPA 導航移除它就只是移除 iframe，因此我方 frame 不會有 FB 相關錯誤（舊 JS-SDK 版
// 會在我方 frame 拋 DataStore/reflow 錯誤）。注意 console 中 `Could not find element "u_x_x_xx"`
// 是 facebook.com iframe 內部自家雜訊，非本站 bug、進不了本站錯誤監控，嵌任何粉專外掛都會有。
//
// RWD：FB 的 iframe 由 facebook.com 以「載入當下指定的固定像素寬」伺服器端算好版面，CSS width:100%
// 只會拉寬外框、內容不重排。故改以 JS 量測容器寬度，夾在 FB 允許範圍 [180, 500]，用該寬度載入；
// 視窗縮放時（debounce）以新寬度重新載入。client-only(.client.vue) 避免 SSR 量測不到寬度的 hydration
// 不一致；外層保留固定高度避免內容載入造成版面跳動(CLS)。

const PAGE_URL = 'https://www.facebook.com/trulyfoodies/'
const HEIGHT = 380
const MIN_W = 180 // FB 粉專外掛允許的最小寬
const MAX_W = 500 // FB 粉專外掛允許的最大寬

const wrap = ref<HTMLElement | null>(null)
const width = ref(MAX_W)

const src = computed(() => {
  const qs = new URLSearchParams({
    href: PAGE_URL,
    tabs: 'timeline',
    width: String(width.value),
    height: String(HEIGHT),
    small_header: 'true',
    adapt_container_width: 'true',
    hide_cover: 'false',
    show_facepile: 'false',
  })
  return `https://www.facebook.com/plugins/page.php?${qs.toString()}`
})

function measure() {
  const w = wrap.value?.clientWidth ?? MAX_W
  width.value = Math.max(MIN_W, Math.min(MAX_W, Math.floor(w)))
}

let ro: ResizeObserver | null = null
let t: ReturnType<typeof setTimeout> | null = null
function onResize() {
  if (t) clearTimeout(t)
  t = setTimeout(measure, 200) // debounce：縮放停下來才重新載入 iframe，避免狂 reflow
}

onMounted(() => {
  // 初次量測延後到版面定位後再做：main.css 是非同步載入的 stylesheet，首次 paint 時容器的
  // 響應式寬度(width:40% 等)尚未套用、量到的是滿版寬而被夾到上限。rAF 抓下一個 paint，
  // window.load 再保險一次（所有 CSS/資源就緒、版面最終定案）。
  requestAnimationFrame(measure)
  if (document.readyState !== 'complete') window.addEventListener('load', measure, { once: true })

  // 容器尺寸變化(版面/字級) → ResizeObserver；視窗縮放 → resize。兩者都重算，互補。
  if ('ResizeObserver' in window && wrap.value) {
    ro = new ResizeObserver(onResize)
    ro.observe(wrap.value)
  }
  window.addEventListener('resize', onResize)
})

onBeforeUnmount(() => {
  if (t) clearTimeout(t)
  if (ro) ro.disconnect()
  window.removeEventListener('resize', onResize)
  window.removeEventListener('load', measure)
})
</script>

<template>
  <!-- 外層量測容器寬度；保留固定高度避免 iframe 載入時版面跳動 -->
  <div ref="wrap" class="index-fb" :style="{ minHeight: HEIGHT + 'px' }">
    <iframe
      :src="src"
      :width="width"
      :height="HEIGHT"
      :style="{ border: 'none', overflow: 'hidden', width: width + 'px', maxWidth: '100%' }"
      scrolling="no"
      frameborder="0"
      allowfullscreen
      loading="lazy"
      title="食在呼 TFoodies Facebook 粉絲專頁"
      allow="autoplay; clipboard-write; encrypted-media; picture-in-picture; web-share"
    ></iframe>
  </div>
</template>

// 把固定 header 的高度發佈成 <html> 的 CSS 變數 `--header-sticky-top`，讓全站任何
// `position: sticky` 元素（checkout 訂單摘要、recipe hero、article-detail 側欄）都能釘在
// 「固定 menu 之下」。legacy main.js 以 `$("#header").sticky({topSpacing:0})` 在捲動時把整個
// #header 固定於 top:0，其高度隨斷點變動，故動態量測而非寫死。client-only（需已渲染的 DOM）。
const GAP = 12 // header 與 sticky 元素之間留一點呼吸空間（px）

function update() {
  const h = document.getElementById('header')?.offsetHeight ?? 0
  // 量不到時設空字串移除變數，CSS 的 fallback（var(..., 1em) 等）即接手。
  document.documentElement.style.setProperty('--header-sticky-top', h ? `${h + GAP}px` : '')
}

export default defineNuxtPlugin((nuxtApp) => {
  let bound = false
  // page:finish 在每次路由的 DOM commit 後觸發（含首次載入，此時 legacy sticky header 已綁定）。
  nuxtApp.hook('page:finish', async () => {
    await nextTick()
    update()
    if (!bound) {
      bound = true
      window.addEventListener('resize', update)
    }
  })
})

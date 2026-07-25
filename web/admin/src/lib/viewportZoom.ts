// 手機版縮放復原 —— 轉頁 / 重新整理後把畫面縮放拉回 100%。
//
// 後台清單頁一律「.card { overflow:auto } + .data-table { min-width:720px }」靠水平捲動呈現
// （見 docs/10-admin-ui-design.md 的 RWD 規則），所以在 375px 手機上看寬表格本來就得雙指放大。
// 但瀏覽器會把該縮放狀態沿用到下一頁與重新整理之後，而本專案是 SPA（createWebHistory），
// 換路由不會觸發 viewport 重算 → 使用者換頁後畫面仍卡在放大狀態，得手動縮回。
//
// 作法：暫時把 viewport meta 收成 maximum-scale=1 逼瀏覽器回到 100%，下一輪 frame 再還原。
// 刻意「不」用 user-scalable=no 永久鎖死縮放：iOS 10 起會刻意忽略該值，且小字看不清時沒有退路。

import type { Router } from 'vue-router'

/** 必須與 index.html 的 <meta name="viewport"> 完全一致，否則還原後版面會變。 */
const BASE = 'width=device-width, initial-scale=1.0'
const LOCKED = `${BASE}, maximum-scale=1.0`

/**
 * 目前是否處於「使用者雙指放大」的狀態。
 * 只有 pinch-zoom 會反映在 visualViewport.scale；桌機的 Ctrl +/- 頁面縮放不會，
 * 因此這個守衛同時避免了去干擾桌機使用者刻意設定的頁面縮放。
 */
function isPinchZoomed(): boolean {
  const vv = window.visualViewport
  return !!vv && vv.scale > 1.01
}

/** 未被放大時完全不碰 meta，避免每次換頁做無謂的 DOM 寫入與潛在閃動。 */
export function resetZoom(): void {
  const meta = document.querySelector<HTMLMetaElement>('meta[name="viewport"]')
  if (!meta || !isPinchZoomed()) return

  meta.setAttribute('content', LOCKED)

  // 雙 rAF：iOS Safari 偶爾在單一 frame 內還沒套用 LOCKED 就被還原，導致重置失效。
  requestAnimationFrame(() => {
    requestAnimationFrame(() => meta.setAttribute('content', BASE))
  })
}

/**
 * 掛在 router 層而非 AdminLayout —— layout 的 watch(route.path) 蓋不到 /login
 * 與 /admin/ar-invoices/:id/print 這兩個不套 layout 的路由。
 */
export function installZoomReset(router: Router): void {
  router.afterEach(() => resetZoom())

  // 重新整理：瀏覽器會保留使用者原本的縮放，載入完成後再補一次。
  if (document.readyState === 'complete') resetZoom()
  else window.addEventListener('load', () => resetZoom(), { once: true })
}

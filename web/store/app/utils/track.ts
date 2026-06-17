// 電商事件追蹤工具。把事件推進 GTM 的 dataLayer，再由 GTM 後台分流給 GA4 / Meta Pixel /
// Google Ads（程式碼只負責「發事件」，接收端全在 GTM 設定，新增平台不必再改 code）。
// 事件格式採 GA4 建議的 ecommerce 結構：view_item / add_to_cart / begin_checkout / purchase。

export interface TrackItem {
  item_id: string
  item_name: string
  price: number
  quantity: number
}

export interface PurchasePayload {
  transaction_id: string
  value: number
  shipping?: number
  currency: string
  items: TrackItem[]
}

interface DataLayerWindow {
  dataLayer?: Record<string, unknown>[]
}

const PENDING_KEY = 'tfoodies.pendingPurchase'

// 推一筆事件到 dataLayer。只在瀏覽器端執行（SSR 期間沒有 window）。
export function track(event: string, data: Record<string, unknown> = {}) {
  if (!import.meta.client) return
  const w = window as unknown as DataLayerWindow
  w.dataLayer = w.dataLayer || []
  // 先清掉上一筆 ecommerce，避免品項資料黏在後續事件上（GA4 官方建議做法）。
  w.dataLayer.push({ ecommerce: null })
  w.dataLayer.push({ event, ...data })
}

// 結帳送單成功時，先把訂單摘要暫存（信用卡會跳轉外部刷卡頁，導回完成頁後才讀取；
// sessionStorage 在同分頁的跨站來回仍會保留）。
export function setPendingPurchase(p: PurchasePayload) {
  if (import.meta.client) sessionStorage.setItem(PENDING_KEY, JSON.stringify(p))
}

// 取出並清除暫存的訂單摘要（取一次就移除，避免重新整理完成頁時重複計算營收）。
export function takePendingPurchase(): PurchasePayload | null {
  if (!import.meta.client) return null
  const raw = sessionStorage.getItem(PENDING_KEY)
  if (!raw) return null
  sessionStorage.removeItem(PENDING_KEY)
  try {
    return JSON.parse(raw) as PurchasePayload
  } catch {
    return null
  }
}

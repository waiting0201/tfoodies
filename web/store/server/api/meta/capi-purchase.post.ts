// Meta 轉換 API(CAPI)— server 端補送 Purchase 事件。
// 由完成頁(Order/Success.vue)在觸發瀏覽器 Pixel 的同時呼叫；兩邊帶相同 event_id(訂單編號)，
// Meta 會自動去重，因此「Pixel 被擋廣告漏掉」時仍有 server 端這筆，提升廣告優化準確度。
// 機密由 server-only runtimeConfig 提供（NUXT_META_PIXEL_ID / NUXT_META_CAPI_TOKEN）；任一為空即略過。
import { createHash } from 'node:crypto'

interface Content {
  id: string
  quantity: number
  item_price?: number
}

interface Body {
  eventId: string
  value: number
  currency?: string
  contents?: Content[]
  email?: string | null
  phone?: string | null
  sourceUrl?: string
}

// Meta 要求 email/phone 以 SHA-256 雜湊後傳送（正規化：去空白、email 轉小寫、
// 電話轉純數字並把開頭 0 換成台灣國碼 886）。
const sha256 = (s: string) => createHash('sha256').update(s).digest('hex')
const hashEmail = (e: string) => sha256(e.trim().toLowerCase())
const hashPhone = (p: string) => {
  let d = p.replace(/\D/g, '')
  if (d.startsWith('0')) d = '886' + d.slice(1)
  return sha256(d)
}

export default defineEventHandler(async (event) => {
  const cfg = useRuntimeConfig()
  const pixelId = String(cfg.metaPixelId || '')
  const token = String(cfg.metaCapiToken || '')
  // 未設定 Pixel/Token → 不送（本機開發與尚未設定時安全略過）。
  if (!pixelId || !token) return { skipped: true }

  const body = await readBody<Body>(event)
  if (!body?.eventId) {
    setResponseStatus(event, 400)
    return { error: 'eventId required' }
  }

  // 比對用使用者資料：IP / UA / fbp / fbc cookie / 雜湊後的 email、phone。資料越齊全比對率越高。
  const headers = getRequestHeaders(event)
  const cookies = parseCookies(event)
  const userData: Record<string, unknown> = {
    client_ip_address: getRequestIP(event, { xForwardedFor: true }) || '',
    client_user_agent: headers['user-agent'] || '',
  }
  if (cookies._fbp) userData.fbp = cookies._fbp
  if (cookies._fbc) userData.fbc = cookies._fbc
  if (body.email) userData.em = [hashEmail(body.email)]
  if (body.phone) userData.ph = [hashPhone(body.phone)]

  const payload = {
    data: [
      {
        event_name: 'Purchase',
        event_time: Math.floor(Date.now() / 1000),
        event_id: body.eventId, // 與瀏覽器 Pixel 相同 → Meta 自動去重
        action_source: 'website',
        event_source_url: body.sourceUrl || headers['referer'] || undefined,
        user_data: userData,
        custom_data: {
          currency: body.currency || 'TWD',
          value: body.value,
          content_type: 'product',
          contents: (body.contents || []).map((c) => ({
            id: c.id,
            quantity: c.quantity,
            item_price: c.item_price,
          })),
        },
      },
    ],
  }

  try {
    const res = await $fetch<{ events_received?: number }>(
      `https://graph.facebook.com/v19.0/${pixelId}/events`,
      { method: 'POST', query: { access_token: token }, body: payload },
    )
    return { ok: true, eventsReceived: res.events_received ?? 0 }
  } catch (e) {
    // 追蹤失敗不可影響使用者（完成頁照常顯示）；僅記錄 server log。
    console.error('[meta-capi] purchase send failed', (e as { data?: unknown })?.data ?? e)
    setResponseStatus(event, 502)
    return { ok: false }
  }
})

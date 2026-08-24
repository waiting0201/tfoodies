/**
 * 發起財金 FISC FOCAS_WEBPOS 刷卡。
 *
 * 後端回傳 form action 與隱藏欄位（金額由後端權威計算，前端不傳金額），這裡動態建表單
 * auto-submit，把使用者整頁導向財金刷卡頁；結果由 /store/payment/return 導回。
 *
 * 結帳頁與會員中心「重新付款」共用同一份：兩處各寫一份 form 組裝，遲早會走樣。
 */
export function useFiscCheckout() {
  const config = useRuntimeConfig()
  const memberAuth = useMemberAuthStore()

  // 下單／發起付款都必須有上限：$fetch 預設不逾時，後端一慢畫面就永遠停在「送出中…」。
  const PAYMENT_TIMEOUT_MS = 15000

  /**
   * 取得刷卡欄位並整頁導向財金。成功時**不會 return**（頁面已離開）。
   * @param orderCode 訂單編號
   */
  async function redirectToFisc(orderCode: string) {
    const headers: Record<string, string> = {}
    // 帶上會員 token：後端據以驗證這張訂單確實屬於本人（訂單編號可被猜出）。
    // 訪客結帳沒有 token，後端維持原本不驗歸屬的行為。
    if (memberAuth.accessToken) headers['Authorization'] = `Bearer ${memberAuth.accessToken}`

    const init = await $fetch<{ actionUrl: string; fields: Record<string, string> }>(
      `${config.public.apiBase}/store/payment/create`,
      {
        method: 'POST',
        // returnOrigin：帶上目前所在網域，讓付款返回時導回「同一個網域」的結果頁（多網域服務時
        // 避免跨域把使用者甩到主網域、且 purchase 追蹤的 sessionStorage 跨域讀不到而漏單）。
        // 後端會以白名單驗證。
        body: { orderCode, returnOrigin: window.location.origin },
        headers,
        timeout: PAYMENT_TIMEOUT_MS,
      },
    )

    const f = document.createElement('form')
    f.method = 'post'
    f.action = init.actionUrl
    f.acceptCharset = 'UTF-8'
    for (const [k, v] of Object.entries(init.fields)) {
      const i = document.createElement('input')
      i.type = 'hidden'
      i.name = k
      i.value = v ?? ''
      f.appendChild(i)
    }
    document.body.appendChild(f)
    f.submit()
  }

  return { redirectToFisc }
}

// API 錯誤格式是 { error: { code, message } }（見後端 ApiErrorResponse）。
// 只讀 e.data.message 會把後端所有具體訊息（庫存不足／折扣碼已使用／商品下架／
// 訂單已付款…）吃掉成通用錯誤，顧客就看不到真正該處理的事。
export function apiErrorMessage(e: unknown, fallback: string): string {
  const d = (e as { data?: { message?: string; error?: { message?: string } } })?.data
  return d?.error?.message ?? d?.message ?? fallback
}

// $fetch 逾時（AbortSignal.timeout）在不同瀏覽器/版本的錯誤形態不一，一律用特徵字串判斷。
export function isTimeout(e: unknown): boolean {
  const err = e as { name?: string; message?: string; cause?: { name?: string } }
  const text = `${err?.name ?? ''} ${err?.cause?.name ?? ''} ${err?.message ?? ''}`.toLowerCase()
  return text.includes('timeout') || text.includes('abort')
}

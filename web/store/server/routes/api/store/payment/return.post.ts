import { proxyRequest } from 'h3'

// 財金 FISC WEBPOS 授權結果回呼（AuthResURL）。
// 把刷卡頁送 FISC 時帶的 AuthResURL 設成「store 自己的網域」（見 FiscOptions.StoreApiBaseUrl），
// 讓「刷卡頁網域 = AuthResURL 網域 = 財金登錄網域」一致（還原舊系統單體同網域送單）。
// 本路由把 FISC 的 POST 原樣反代到 Functions /store/payment/return，由其處理付款完成 + 回 302，
// 再把 302 透傳給瀏覽器導回 /Order/Success。
export default defineEventHandler((event) => {
  const apiBase = useRuntimeConfig(event).public.apiBase.replace(/\/$/, '')
  return proxyRequest(event, `${apiBase}/store/payment/return`)
})

// /llms.txt (llmstxt.org) — a plain-Markdown site summary for AI answer engines (GEO). Static
// brand/policy facts + live product types / products from the Store API, so it never drifts
// from the catalogue. Facts mirror 聯絡資訊 / 購物說明 and utils/shipping.
import { FREIGHT_FEE, FREIGHT_THRESHOLD } from '../../app/utils/shipping'

interface ProductsResponse {
  products: { title: string; price: number }[]
  productTypes: { title: string }[]
}

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const apiBase = String(config.public.apiBase).replace(/\/+$/, '')
  const site = String(config.public.siteUrl).replace(/\/+$/, '')
  const link = (path: string) => `${site}${encodeURI(path)}`

  const data = await $fetch<ProductsResponse>(`${apiBase}/store/products`)
    .catch((): ProductsResponse => ({ products: [], productTypes: [] }))

  const lines = [
    '# 食在呼 TFoodies',
    '',
    '> 台灣食品電商，嚴選天然安心食材與頂級橄欖油（澳洲樂霸 La Barre 特級初榨橄欖油等），產地直送餐桌；'
      + '並提供料理食譜、食材小知識與品牌故事。營運公司：庭富國際有限公司（台中市北屯區）。',
    '',
    `- 運費：單筆滿 NT$${FREIGHT_THRESHOLD} 免運，未滿酌收 NT$${FREIGHT_FEE}；新竹物流宅配，外島以郵局配送`,
    '- 出貨：付款完成後 3~7 個工作天寄出，寄出後約 3 日內送達',
    '- 退貨：收到商品 7 日內如有破損請聯絡客服；已開封商品不受理退貨',
    '- 付款：信用卡（VISA / Master / JCB）、ATM 轉帳',
    '- 客服：04-2436-6659、hi@tfoodies.com（週一至週五 9:00~18:00）',
    '',
    '## 主要頁面',
    '',
    `- [所有商品](${link('/Products')})`,
    `- [料理食譜](${link('/Recipes')})：使用自家商品的家常料理，含食材與步驟`,
    `- [食材小知識](${link('/Knowledges')})：橄欖油、鹽等食材的常見問答`,
    `- [綠誌專題](${link('/Issues')})`,
    `- [檢驗報告](${link('/Reports')})：橄欖油品質、農藥、重金屬等第三方檢驗`,
    `- [購物說明](${link('/PageMs/Howtobuy')})：付款、配送、退換貨、發票常見問題`,
    `- [關於食在呼](${link('/TFoodies')})`,
    `- [聯絡資訊](${link('/PageMs/Contact')})`,
  ]

  if (data.productTypes.length) {
    lines.push('', '## 商品分類', '')
    for (const t of data.productTypes) lines.push(`- [${t.title}](${link(`/Products/${t.title}`)})`)
  }
  if (data.products.length) {
    lines.push('', '## 商品', '')
    for (const p of data.products) {
      lines.push(`- [${p.title}](${link(`/Product/${p.title.replaceAll('/', '-')}`)})：NT$${Math.trunc(p.price)}`)
    }
  }

  setHeader(event, 'content-type', 'text/plain; charset=utf-8')
  setHeader(event, 'cache-control', 'public, max-age=3600')
  return lines.join('\n') + '\n'
})

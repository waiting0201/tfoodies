// 運費規則（顯示用；最終金額以後端 Order__FreightLimit / Order__FreightAmount 為準）。
// 結帳頁與商品 JSON-LD（OfferShippingDetails）共用，改後端設定時一併改這裡。
export const FREIGHT_THRESHOLD = 2000
export const FREIGHT_FEE = 180

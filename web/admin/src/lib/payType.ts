// 付款方式（Orders.paytype）— 對齊 src/TFoodies.Domain/Enums/Enums.cs 的 PayType。
// DB 欄位值為凍結值，新增只能往後接。此檔為後台的單一真相，各畫面請引用而非各自複製一份。

export const PAY_TYPE = {
  CREDIT_CARD: 1,
  CASH_ON_DELIVERY: 2,
  ATM_TRANSFER: 3,
  NO_PAYMENT: 4,
  CASH: 5,
  WIRE_TRANSFER: 6,
  CHECK: 7,
  LINE_PAY: 8,
} as const

/** 簡短標籤（訂單詳情、列表）。 */
export const PAY_TYPE_LABELS: Record<number, string> = {
  [PAY_TYPE.CREDIT_CARD]: '信用卡',
  [PAY_TYPE.CASH_ON_DELIVERY]: '貨到付款',
  [PAY_TYPE.ATM_TRANSFER]: 'ATM轉帳',
  [PAY_TYPE.NO_PAYMENT]: '免付款',
  [PAY_TYPE.CASH]: '現金',
  [PAY_TYPE.WIRE_TRANSFER]: '電匯',
  [PAY_TYPE.CHECK]: '支票',
  [PAY_TYPE.LINE_PAY]: 'LINE Pay',
}

/** 完整標籤（報表、下拉選單）。 */
export const PAY_TYPE_LONG_LABELS: Record<number, string> = {
  [PAY_TYPE.CREDIT_CARD]: '信用卡線上刷卡',
  [PAY_TYPE.CASH_ON_DELIVERY]: '宅配貨到付款',
  [PAY_TYPE.ATM_TRANSFER]: 'ATM轉帳付款',
  [PAY_TYPE.NO_PAYMENT]: '免付款',
  [PAY_TYPE.CASH]: '現金支付',
  [PAY_TYPE.WIRE_TRANSFER]: '電匯',
  [PAY_TYPE.CHECK]: '支票',
  [PAY_TYPE.LINE_PAY]: 'LINE Pay',
}

export function payTypeLabel(v: number | null | undefined): string {
  return v == null ? '-' : (PAY_TYPE_LABELS[v] ?? `類型${v}`)
}

/**
 * 後台建立/編輯訂單可選的付款方式。
 * 不含 LINE Pay 與免付款：LINE Pay 需顧客本人在手機授權，後台代客建單付不了款
 * （要向客人收 LINE Pay 請改用「收款連結」）。
 */
export const ORDER_PAY_TYPE_OPTIONS = [
  PAY_TYPE.CREDIT_CARD,
  PAY_TYPE.CASH_ON_DELIVERY,
  PAY_TYPE.ATM_TRANSFER,
  PAY_TYPE.CASH,
  PAY_TYPE.WIRE_TRANSFER,
].map(value => ({ value, label: PAY_TYPE_LONG_LABELS[value]! }))

/** 收款連結可選的付款方式（客人自行於連結頁完成付款）。 */
export const PAYMENT_LINK_PAY_METHOD_OPTIONS = [
  PAY_TYPE.CREDIT_CARD,
  PAY_TYPE.LINE_PAY,
].map(value => ({ value, label: PAY_TYPE_LONG_LABELS[value]! }))

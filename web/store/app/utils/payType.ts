// 付款方式（Orders.paytype）— 對齊 src/TFoodies.Domain/Enums/Enums.cs 的 PayType。
// DB 欄位值為凍結值，新增只能往後接。

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

export const PAY_TYPE_LABELS: Record<number, string> = {
  [PAY_TYPE.CREDIT_CARD]: '信用卡',
  [PAY_TYPE.CASH_ON_DELIVERY]: '貨到付款',
  [PAY_TYPE.ATM_TRANSFER]: 'ATM 轉帳',
  [PAY_TYPE.NO_PAYMENT]: '免付款',
  [PAY_TYPE.CASH]: '現金',
  [PAY_TYPE.WIRE_TRANSFER]: '電匯',
  [PAY_TYPE.CHECK]: '支票',
  [PAY_TYPE.LINE_PAY]: 'LINE Pay',
}

export function payTypeLabel(v: number | null | undefined): string {
  return v == null ? '-' : (PAY_TYPE_LABELS[v] ?? '-')
}

/** 需離開站台到第三方付款頁完成付款的方式（結帳後不直接進訂單完成頁）。 */
export function isOffsitePayment(v: number): boolean {
  return v === PAY_TYPE.CREDIT_CARD || v === PAY_TYPE.LINE_PAY
}

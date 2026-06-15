# 13 · 前後台刷卡 + 電子發票串接流程

> 信用卡（財金 FISC FOCAS_WEBPOS）與電子發票（ezPay）的端到端流程。設定參數見 [docs/12](12-payment-invoice-config.md)。

## 共用核心：`PaymentCompletionService`

三條付款路徑（前台刷卡返回 / 後台刷卡返回 / 後台標記已付款）最終都呼叫同一個 `MarkPaidAsync`，確保入帳、開票、寄信一致且**冪等**。

檔案：[PaymentCompletionService.cs](../src/TFoodies.Infrastructure/Payments/PaymentCompletionService.cs)、介面 [IPaymentCompletionService.cs](../src/TFoodies.Application/Abstractions/IPaymentCompletionService.cs)

```
MarkPaidAsync(orderCode, lastPan4, txnRef, payDate?)
├─ MarkOrderPaidAsync（交易內，冪等）
│    ├─ 已付款 → 直接結束（回 false，不重複）
│    ├─ UPDATE Orders: paystatus=已付款, paydate, lastpan4
│    └─ 建 Incomes（C# 產生 incomeId 回傳，供發票關聯）
├─ 寄「付款完成通知信」（best-effort，BuildPaidMailHtml）
└─ await IssueInvoiceAsync(orderCode, incomeId)   ← 同步開電子發票

IssueInvoiceAsync(orderCode, incomeId?)（冪等）
├─ 跳過條件：invoicestatus != NotIssued 或 invoicetype == None
├─ 呼叫 ezPay IInvoiceService.IssueAsync(Immediate) → 取得發票號
└─ 交易內（UPDATE Orders ... WHERE invoicestatus=0 護欄）：
     UPDATE Orders(invoicestatus=已開, invoicecode=ezPay 發票號)
     + INSERT Invoices（incomeid 關聯收入、invoicecode=發票號）
     + INSERT Invoicedetails（accountingid 銷貨科目、price=含稅、tax=5% 拆分）
   失敗 → 不拋例外、invoicestatus 留「未開」（付款仍算完成，後台可補開）
```

## 前台（store）顧客刷卡

檔案：[Checkout/index.vue](../web/store/app/pages/Checkout/index.vue)、[PaymentController.cs](../src/TFoodies.Api.Functions/Controllers/PaymentController.cs)

```
結帳頁（選信用卡 payType=1）
 1. POST /store/orders            → 建未付款訂單，回 orderCode
 2. POST /store/payment/create    → CreatePayment 驗證(信用卡+未付款)
                                     → FiscWebpos.BuildFields(AuthResUrl=API/store/payment/return)
                                     → 回 { actionUrl, fields }
 3. 前端動態建 <form> → f.submit()（整頁 POST 到財金刷卡頁）
   ▼ 財金 FOCAS_WEBPOS 刷卡頁（顧客輸入卡號授權）
 4a. 導回 AuthResURL = /store/payment/return → Return
        ParseFields(status=="0" 且 authCode 非空 = 成功) → MarkPaidAsync
        → 302 導回 StoreSuccessUrl?code=&paid=（/Order/Success）
 4b. 主動通知 /store/payment/notify（背景補償，冪等）→ MarkPaidAsync
```

## 後台（admin）線上刷卡

用於信用卡未付款訂單（電話單、首刷失敗補刷）。檔案：[OrderDetailView.vue](../web/admin/src/views/orders/OrderDetailView.vue)、[OrderAdminController.cs](../src/TFoodies.Api.Functions/Controllers/Admin/OrderAdminController.cs)

```
訂單詳情（信用卡+未付款 → 顯示「線上刷卡」按鈕）
 1. POST /admin/orders/{code}/charge → Charge 驗證
                                       → FiscWebpos.BuildFields(AuthResUrl=API/store/payment/return-admin)
                                       → 回 { actionUrl, fields }
 2. 前端建 <form> → submit（整頁導向財金刷卡頁，管理員代客輸入）
   ▼
 3. 導回 /store/payment/return-admin → ReturnAdmin（同 Return 解析 + MarkPaidAsync）
        → 302 導回 AdminSuccessUrl/{code}?paid=（後台訂單詳情）
   ▼ 訂單詳情重新載入：已付款、已建 Income、已開發票
```

> 前台/後台**唯一差別**是 AuthResURL（`/return` vs `/return-admin`）與最終導回頁；欄位產生（[FiscWebpos.cs](../src/TFoodies.Infrastructure/Payments/Fisc/FiscWebpos.cs)）、成功判定、入帳開票全部共用。

## 後台新增訂單（線下單）的刷卡/發票

[OrderCreateView.vue](../web/admin/src/views/orders/OrderCreateView.vue) → `POST /admin/orders` → `Create`。**新增本身不刷卡、不開發票、不建 Income**，只把訂單建成「未付款」（`paystatus=0`，硬寫；`payType=4 免付款`才設 NoPayment，但建單下拉未提供此選項），帶著建單時選的 `invoiceType`/統編/愛心碼。

新增成功後**導向該訂單詳情頁**（`/admin/orders/{orderCode}`），刷卡/開票走與其他訂單**完全相同**的詳情頁流程：

- 信用卡(1)＋未付款 → 「線上刷卡」按鈕 → charge 流程 → `MarkPaidAsync` → Income＋電子發票
- 貨到/ATM/現金/電匯 → 收款後「標記已付款」按鈕 → pay 流程 → `MarkPaidAsync` → Income＋電子發票

開票時用的就是建單時設定的發票類型（二聯/三聯統編/捐贈）。

## 後台其他入口（同樣走核心）

| 動作 | 端點 | 行為 |
|---|---|---|
| 標記已付款（ATM/現金確認，非刷卡）| `PATCH /admin/orders/{code}/pay` → `MarkPaid` | 呼叫 `MarkPaidAsync` 走完整流程（建 Income＋開票＋寄信）|
| 補開發票（開票失敗或當下未開）| `POST /admin/orders/{code}/invoice` → `IssueInvoice` | 直接呼叫 `IssueInvoiceAsync` |

按鈕在訂單詳情頁依條件顯示：線上刷卡（信用卡＋未付款）、補開發票（二/三聯＋未開）。

## 電子發票（ezPay）管理

檔案：[EzPayInvoiceService.cs](../src/TFoodies.Infrastructure/Invoicing/EzPay/EzPayInvoiceService.cs)、[InvoiceAdminController.cs](../src/TFoodies.Api.Functions/Controllers/Admin/InvoiceAdminController.cs)

- 開立：`IssueAsync` → AES 加密參數（[EzPayCodec](../src/TFoodies.Infrastructure/Invoicing/EzPay/EzPayCodec.cs)）POST 到 ezPay；成功回發票號。
- 後台發票管理 `/admin/invoices`：列表、作廢（`VoidAsync`→invoicestatus=2）、折讓（`AllowanceAsync`→invoicestatus=3），讀本地 `Invoices`/`Invoicedetails`。
- 時機：付款完成**當下自動即時開立**；失敗留「未開」，後台補開。

## 冪等與失敗處理

- `MarkPaidAsync`：已付款回 false → return＋notify 雙觸發不重複入帳/開票/寄信。
- `IssueInvoiceAsync`：`UPDATE ... WHERE invoicestatus=0` 護欄，避免重複建發票。
- 開票失敗：付款照算完成，發票狀態留「未開」→ 後台補開。

## WEBPOS 送出欄位（與舊系統可運作表單一致，實測成功）

`merID, MerchantID, TerminalID, lidm(=orderCode), purchAmt(=total+freight−discount), AuthResURL, enCodeType=UTF-8, PayType=0, AutoCap=1`。
成功判定：財金回傳 `status=="0"` 且 `authCode` 非空。欄位/錯誤碼細節見 [docs/12](12-payment-invoice-config.md)。

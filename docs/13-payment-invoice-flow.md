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
├─ 跳過條件：invoicetype == None（免開）或 invoicestatus == Issued(1)（已開）
│    └─ 可開立：status ∈ {未開(0), 已作廢(2)}；status=2 即「作廢後重新開立」取得新號
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
        body 帶 returnOrigin = window.location.origin（使用者當前網域）
                                     → origin 經白名單(AllowedStoreOrigins)驗證；通過才附進
                                       AuthResURL = API/store/payment/return?origin=<當前網域>
                                     → FiscWebpos.BuildFields(AuthResUrl=上者)
                                     → 回 { actionUrl, fields }
 3. 前端動態建 <form> → f.submit()（整頁 POST 到財金刷卡頁）
   ▼ 財金 FOCAS_WEBPOS 刷卡頁（顧客輸入卡號授權）
 4a. 導回 AuthResURL = /store/payment/return(?origin=…) → Return
        ParseFields(status=="0" 且 authCode 非空 = 成功) → MarkPaidAsync
        → 決定回跳網域：query 的 origin 再經白名單驗證 → {origin}/Order/Success（同使用者網域）；
          讀不到/不在白名單 → 退回 Fisc__StoreSuccessUrl（fallback，防 open redirect）
        → 302 導回 <success>?code=&paid=
 4b. 主動通知 /store/payment/notify（背景補償，冪等）→ MarkPaidAsync
```

> 🌐 **多網域動態回跳**：store 正式同時服務多網域（www.tfoodies.com 等 4 個），上面 `?origin=` 機制讓刷卡後
> 導回「使用者結帳的同一個網域」，避免跨域把人甩到主網域、且 `purchase` 追蹤 sessionStorage 跨域漏單。
> 兩端（create 附帶 / return 導回）都用 `Fisc__AllowedStoreOrigins` 白名單驗證 → 防 open redirect。
> 純邏輯由 `FiscOptions.NormalizeOrigin` + `AllowedStoreOriginSet` 負責，測試見 `FiscOriginTests`。

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

建單表單（對齊舊系統 `OrderMs/AddOrders`）含：會員、**訂單日期**（必填，預設今天、可補登；`OrderDate` 無值才回退當天）、**出貨倉**（必填，`GET /admin/warehouses`）、**物流商**（必填，`GET /admin/logistics`）、收件人、**收件縣市/鄉鎮級聯**（必填，`GET /admin/zipcodes/cities`+`/areas`，帶出 `reciverzipcodeid`）、商品（autocomplete；每列可填**折數折扣**與**可覆寫小計**）、運費、訂單折扣、發票。明細 `discount` 存**折數**（如 8=八折），金額效果反映於 `subtotal`。

> **運費／免運政策（後台建單）**：`computedShippingFee = 小計 ≥ 2000 ? 0 : (運費欄 || 180)`，與店面 `OrderService`（未滿門檻收 `FreightAmount`）一致。運費欄預設 **180**、承辦人可覆寫；未滿 2000 且未填時**自動帶 180**（修正前預設 0，會把未滿門檻的單存成 `freight=0`）。
> **NULL 防護**：後台訂單相關 SELECT 對 `o.freight` 一律 `ISNULL(o.freight,0) AS freight`（與 `discount` 一致），避免舊資料 `freight=NULL` 造成詳情頁 `toLocaleString()` 例外使運費列消失；詳情頁前端亦以 `(shippingFee ?? 0)` 防護。

新增成功後**導向該訂單詳情頁**（`/admin/orders/{orderCode}`），刷卡/開票走與其他訂單**完全相同**的詳情頁流程：

- 信用卡(1)＋未付款 → 「線上刷卡」按鈕 → charge 流程 → `MarkPaidAsync` → Income＋電子發票
- 貨到/ATM/現金/電匯 → 收款後「標記已付款」按鈕 → pay 流程 → `MarkPaidAsync` → Income＋電子發票

開票時用的就是建單時設定的發票類型（二聯/三聯統編/捐贈）。

## 後台其他入口（同樣走核心）

| 動作 | 端點 | 行為 |
|---|---|---|
| 標記已付款（ATM/現金確認，非刷卡）| `PATCH /admin/orders/{code}/pay` → `MarkPaid` | 呼叫 `MarkPaidAsync` 走完整流程（建 Income＋開票＋寄信）|
| 補開／重新開立發票（開票失敗、當下未開、或作廢後重開）| `POST /admin/orders/{code}/invoice` → `IssueInvoice` | 直接呼叫 `IssueInvoiceAsync` |
| 作廢發票（退貨／開錯）| `POST /admin/orders/{code}/invoice/void` → `VoidInvoice` | 呼叫 `VoidInvoiceAsync`（ezPay 作廢＋invoicestatus=2）|

> ⚠️ **權限**：訂單詳情頁的「補開發票／作廢發票」兩端點以 **`OrderMs.Update`** 授權（與標記已付款/刷卡同頁同模組）。**不可用 `InvoiceMs`** ─ 電子發票並非 Lims RBAC 樹的獨立模組（`Lims` 表查無 `InvoiceMs`/`DiscountMs`，僅 itadmin/888 繞過），舊系統發票歸於訂單／會計流程。曾誤用 `InvoiceMs` 導致一般管理員按下補開發票顯示「無 InvoiceMs 模組的 Update 權限」。

按鈕在訂單詳情頁依條件顯示（二/三聯發票）：線上刷卡（信用卡＋未付款）、補開發票（未開 status=0）、**重新開立發票**（已作廢 status=2）、**作廢發票**（已開 status=1）。

## 訂單詳情頁作廢 → 重新開立流程（對齊舊系統 `AjaxController/CancelInv`）

```
已開發票(status=1)
 └─「作廢發票」按鈕 → POST /invoice/void（prompt 輸入原因，預設「退貨」）
       → VoidInvoiceAsync：僅 status=1 才作廢（冪等護欄）
         ├─ ezPay invoice_invalid（InvoiceNumber＋InvalidReason）
         └─ UPDATE Orders invoicestatus=2（不刪本地 Invoices，保留稽核）
   ▼ status=2（已作廢）
 └─「重新開立發票」按鈕 → POST /invoice → IssueInvoiceAsync
       → 前置放寬：允許 status∈{0,2}；冪等護欄 WHERE invoicestatus IN (0,2)
         → ezPay 重開取得**新發票號** → UPDATE Orders invoicestatus=1, invoicecode=新號
           + INSERT 新 Invoices（incomeid=null）
   ▼ status=1（已開，新號）
```

> **與舊系統差異**：舊系統作廢後設 `invoicestatus=0`＋`invoicecode=null`（重置為未開）；新系統設 `status=2`（明確「已作廢」終態，符合本檔發票管理小節規範），改由「放寬 `IssueInvoiceAsync` 允許從 status=2 重開」達成同等的「作廢→重開」能力。作廢原因由使用者輸入（舊系統硬寫「退貨」）。
>
> ⚠️ **`/admin/invoices` 列表顯示限制**：發票狀態存於 `Orders.invoicestatus`（DB schema 唯讀，`Invoices` 表無 status 欄位）。同一訂單作廢後重開會在 `Invoices` 留兩筆（舊作廢號＋新號），但兩筆 join 同一 `Orders` 都顯示最新狀態，故**舊作廢發票號在列表會顯示為新狀態**而非「已作廢」。如需逐張發票精確狀態，須以 ezPay 後台為準。

## 電子發票（ezPay）管理

檔案：[EzPayInvoiceService.cs](../src/TFoodies.Infrastructure/Invoicing/EzPay/EzPayInvoiceService.cs)、[InvoiceAdminController.cs](../src/TFoodies.Api.Functions/Controllers/Admin/InvoiceAdminController.cs)

- 開立：`IssueAsync` → AES 加密參數（[EzPayCodec](../src/TFoodies.Infrastructure/Invoicing/EzPay/EzPayCodec.cs)）POST 到 ezPay；成功回發票號。
- 後台發票管理 `/admin/invoices`：列表、作廢（`VoidAsync`→invoicestatus=2）、折讓（`AllowanceAsync`→invoicestatus=3），讀本地 `Invoices`/`Invoicedetails`。
- 時機：付款完成**當下自動即時開立**；失敗留「未開」，後台補開。

> ⚠️ **ezPay 串接端點與 `Version` 必須精確比照手冊 EZP_INVI_1.2.2（與舊系統實證一致），否則 API 直接拒絕、整條開票靜默失敗（付款仍完成、發票留未開）**：
>
> | API | 端點（接於 `BaseUrl=…/Api`）| `Version` | 發票號參數 |
> |---|---|---|---|
> | 開立 | `invoice_issue`（**非** `invoice/issue`）| `1.5`（舊系統 1.4；**非** 1.0）| 回應讀 `InvoiceNumber` |
> | 作廢 | `invoice_invalid`（**非** `invoice/void`）| `1.0` | 請求帶 `InvoiceNumber`（**非** `InvoiceNo`）|
> | 折讓 | `allowance_issue`（**非** `invoice/allowance`）| `1.3`（**非** 1.0）| 請求帶 `InvoiceNo`；另須帶 `ItemTaxAmt`、`Status` |
>
> 另：B2B（三聯式）`BuyerName` 須帶**公司抬頭**（`Orders.companytitle`），非會員姓名。前台結帳後不自動開票，多半即上述端點/版本錯誤所致；可查 App Insights 中 `PaymentCompletionService` 的 Warning/Error。
>
> ⚠️ **B2B 統編校驗（2026-07 修）**：ezPay `Category` 由**是否有統編**決定（`EzPayInvoiceService` 對 `BuyerUbn` 一律用 `IsNullOrWhiteSpace` 判斷，B2B 才帶 `BuyerUbn` 並 `Trim()`），避免空字串/空白被判成 B2B 卻無統編、被 ezPay 以「統編沒有」拒絕。
> `IssueInvoiceAsync` 另加**前置校驗**：`invoicetype=3` 但 `companynumber` 為空 → 直接回 `Error.Validation`（「此訂單為三聯式發票，但缺少統一編號…」），不再靜默降級成二聯。舊資料中有一批三聯式訂單 `companynumber` 為 NULL（多為舊系統誤標，抬頭亦缺），後台補開會被此校驗擋下，須先於「編輯訂單」補填統編或改回二聯式。後台詳情頁亦以 `triplicateMissingUbn` 提示並禁用「補開發票」鈕。
>
> 🐞 **BuyerUBN 參數名大小寫 bug（2026-07-22 修）**：ezPay 參數名**大小寫敏感**，統編欄位須為 `BuyerUBN`（全大寫，同舊系統 `AjaxController`）。新系統一度誤植為 `BuyerUbn`，ezPay 收不到統編、卻因 `Category=B2B` 有送而回「**B2B 類別的發票，買受人統編不可為空白**」（HTTP 422 `UNPROCESSABLE_ENTITY`）。症狀：即使訂單統編/抬頭齊全（如 `O20260722002` 統編 83150659）仍開不出 B2B，且新系統從未成功開過任何 B2B 發票（既有已開 B2B 皆舊系統開立）。修正僅改 `EzPayInvoiceService` 送出的 key 名；**須重新部署 tfoodies-api 才生效**。
>
> 🐞 **明細逐項小計校驗（2026-07-22 修）**：ezPay 逐項要求 `ItemAmt == ItemPrice × ItemCount`，否則回「**請檢查商品資訊第N項金額小計是否正確**」。管理員議價單的 `Orderdetails.subtotal` 可能不等於 `price×qty`（單價談過、未記 `discount` 旗標），原本直接把 `subtotal` 當 `ItemAmt` 送出被退。`PaymentCompletionService` 已比照舊系統 `AjaxController`：主項帶 `price×qty`，差額另拆一條負數調整明細，確保每項自洽且加總＝實付。⚠️ 訂單層折扣（折扣碼 `Orders.discount`）目前仍未拆成明細列，`Σ ItemAmt` 會比 `TotalAmt` 多出折扣額；舊系統多年如此、ezPay 容忍，惟若日後 ezPay 收緊需另補訂單層調整列。

## 冪等與失敗處理

- `MarkPaidAsync`：已付款回 false → return＋notify 雙觸發不重複入帳/開票/寄信。
- `IssueInvoiceAsync`：`UPDATE ... WHERE invoicestatus=0` 護欄，避免重複建發票。
- 開票失敗：付款照算完成，發票狀態留「未開」→ 後台補開。

## WEBPOS 送出欄位（與舊系統可運作表單一致，實測成功）

`merID, MerchantID, TerminalID, lidm(=orderCode), purchAmt(=total+freight−discount), AuthResURL, enCodeType=UTF-8, PayType=0, AutoCap=1`。
成功判定：財金回傳 `status=="0"` 且 `authCode` 非空。欄位/錯誤碼細節見 [docs/12](12-payment-invoice-config.md)。

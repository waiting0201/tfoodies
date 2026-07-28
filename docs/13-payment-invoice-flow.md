# 13 · 前後台刷卡 / LINE Pay + 電子發票串接流程

> 信用卡（財金 FISC FOCAS_WEBPOS）、LINE Pay（Online API v3 直連）與電子發票（ezPay）的端到端流程。設定參數見 [docs/12](12-payment-invoice-config.md)。

## 共用核心：`PaymentCompletionService`

四條付款路徑（前台刷卡返回 / 後台刷卡返回 / LINE Pay 請款確認 / 後台標記已付款）最終都呼叫同一個 `MarkPaidAsync`，確保入帳、開票、寄信一致且**冪等**。

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
├─ 若訂單有折扣(Orders.discount>0) → 帶 ezPay 發票備註 Comment「訂單折扣 NT$X」
├─ 呼叫 ezPay IInvoiceService.IssueAsync(Immediate) → 取得發票號
└─ 交易內（UPDATE Orders ... WHERE invoicestatus=0 護欄）：
     UPDATE Orders(invoicestatus=已開, invoicecode=ezPay 發票號)
     + INSERT Invoices（incomeid 關聯收入、invoicecode=發票號）
     + INSERT Invoicedetails（accountingid 銷貨科目、price=含稅、tax=5% 拆分）
   失敗 → 不拋例外、invoicestatus 留「未開」（付款仍算完成，後台可補開）
```

## 前台（store）顧客刷卡

檔案：[Checkout/index.vue](../web/store/app/pages/Checkout/index.vue)、[PaymentController.cs](../src/TFoodies.Api.Functions/Controllers/PaymentController.cs)

> ⚠️ **應付 0 元不進金流**：100% 折扣 + 滿額免運會產生 `total + freight − discount = 0` 的訂單。
> `OrderService` 下單時即把 `paystatus` 設為 3(免付款)，`CreatePayment`（FISC / LINE Pay 皆是）在狀態
> 檢查前先擋下並回「本訂單應付金額為 0，無須刷卡」，前台則直接跳過金流進完成頁。

```
結帳頁（選信用卡 payType=1）
 1. POST /store/orders            → 建未付款訂單，回 orderCode（含 total/freight/discount）
 2. POST /store/payment/create    → CreatePayment 驗證(信用卡+未付款)
        body 帶 returnOrigin = window.location.origin（使用者當前網域）
                                     → origin 經白名單(AllowedStoreOrigins)驗證；通過才附進
                                       AuthResURL = API/store/payment/return?origin=<當前網域>
                                     → FiscWebpos.BuildFields(AuthResUrl=上者)
                                     → 回 { actionUrl, fields }
 3. 前端動態建 <form> → f.submit()（整頁 POST 到財金刷卡頁）
    ⚠️ 此時「不」清空購物車——顧客可能從刷卡頁退回；購物車由 /Order/Success 清空
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

## 前台（store）顧客 LINE Pay

檔案：[Checkout/index.vue](../web/store/app/pages/Checkout/index.vue)、[LinePayController.cs](../src/TFoodies.Api.Functions/Controllers/LinePayController.cs)、[LinePayClient.cs](../src/TFoodies.Infrastructure/Payments/LinePay/LinePayClient.cs)

```
結帳頁（選 LINE Pay payType=8）
 0. 頁面載入時 GET /store/payment/methods → 可用付款方式清單（LinePay__Enabled=false 就不會出現）
 1. POST /store/orders                    → 建未付款訂單，回 orderCode
        ⚠️ 後端以 StorePaymentMethods.IsAllowed 白名單驗證 payType（未啟用時連 8 都不收）
 2. POST /store/payment/linepay/create    → 驗證(LINE Pay + 未付款 + Enabled)
        應付 = total + freight − discount（後端權威，前端不傳金額）
        POST {BaseUrl}/v4/payments/request（HMAC 簽章）
          redirectUrls.confirmUrl = API/store/payment/linepay/confirm?origin=<當前網域>
          redirectUrls.cancelUrl  = API/store/payment/linepay/cancel?origin=<當前網域>
        → 回 { paymentUrl, transactionId }
 3. 前端 window.location.href = paymentUrl（整頁導向）
   ▼ LINE Pay 付款頁（顧客於 LINE 授權，此時尚未扣款）
 4a. 導回 confirmUrl（LINE Pay 主動 GET，query 帶 transactionId 與 orderId）
        訂單已付款 → 直接視為成功（回跳重放，連 API 都不打）
        否則 POST /v4/payments/{transactionId}/confirm { amount 由 DB 重算, currency:TWD }
          returnCode 0000 成功；1172「已完成」亦視同成功
        → MarkPaidAsync(orderCode, lastPan4: null, txnRef: "LINEPay transactionId:{id}")
        → 302 導回 {origin}/Order/Success?code=&paid=1
 4b. 顧客於 LINE Pay 取消 → cancelUrl → 302 …?paid=0（訂單維持未付款，未扣款）
```

> 💡 **沒有 notify 補償路徑，是刻意的**：LINE Pay 為 reserve → confirm 兩段式，**未 confirm 就不會扣款**
> （逾時自動失效）。顧客中途關瀏覽器最壞情況是訂單停在未付款、顧客未被扣款，風險遠低於財金的直接授權。
>
> 💡 **交易序號沒有新欄位**：`Orders` 無第三方交易 ID 欄位（schema 凍結），沿用財金的作法寫進
> `Incomes.note`（`LINEPay transactionId:…`，對照財金的 `FISC authCode:… xid:…`）。因此本次**零 schema 變更**
> ——唯一的 DDL 是收款連結的 `paymethod` 欄位（見下）。
>
> 💡 **package 只送一筆**：`request` 的 package/product 各一筆、金額 = 應付總額，不拆運費/折扣
> （對齊財金只送 `purchAmt`），避開 `Orders.total` 語意為純商品小計的坑。

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
         └─ UPDATE Orders invoicestatus=2, invoicecode=NULL（清除訂單上的發票號；本地 Invoices 保留稽核）
   ▼ status=2（已作廢，訂單發票號已清空）→ 前端顯示成功訊息
 └─（重開前）訂單詳情「發票號碼」顯示「—」
 └─「重新開立發票」按鈕 → POST /invoice → IssueInvoiceAsync
       → 前置放寬：允許 status∈{0,2}；冪等護欄 WHERE invoicestatus IN (0,2)
         → ezPay 重開取得**新發票號** → UPDATE Orders invoicestatus=1, invoicecode=新號
           + INSERT 新 Invoices（incomeid=null）
   ▼ status=1（已開，新號）
```

> **與舊系統差異**：舊系統作廢後設 `invoicestatus=0`＋`invoicecode=null`（重置為未開）；新系統設 `status=2`（明確「已作廢」終態）**並同樣清除 `invoicecode`（2026-07-23）**，改由「放寬 `IssueInvoiceAsync` 允許從 status=2 重開」達成「作廢→重開」。作廢成功前端顯示綠色成功訊息（`actionSuccess`），詳情「發票號碼」在重開前顯示「—」。作廢原因由使用者輸入（舊系統硬寫「退貨」）。
>
> **`/admin/invoices` 列表狀態（2026-07-23 改善）**：因作廢會清 `Orders.invoicecode`，已作廢/已換號的 `Invoices` 列會 join 不到訂單；列表改以 `COALESCE(o.invoicestatus, 2)` 呈現與篩選（join 得到＝當前狀態；join 不到＝作廢(2)），修正了先前「舊作廢號顯示為新狀態」的 quirk。
>
> ⚠️ **重開的 `MerchantOrderNo` 不可重複（2026-07-23 修）**：ezPay 不允許同一 `MerchantOrderNo` 重複開立（**即使前一張已作廢**），故作廢後重開不能再用原 `orderCode`。新系統以「該訂單第 N 次開立」推導：**首開＝`orderCode`，第 N 次(N≥2)＝`orderCode`+`R`+(N-1)**（純英數後綴，避開 ezPay 特殊字元限制），規則見 `PaymentCompletionService.MerchantOrderNoFor`。開立時 N = 現有 `Invoices` 筆數＋1；**作廢時**須送「開立當時那個號」，故取該發票在此訂單的開立序（依 `Invoices.createdate`）用同規則還原—兩端一致（測試 `MerchantOrderNoTests`）。⚠️ 不改存於 `Invoices.note`（該欄是使用者可見/可編輯的發票備註），改用**開立序推導**、無需新欄位、與既有資料相容。
>
> ℹ️ **`/admin/invoices` 列表狀態**：發票狀態存於 `Orders.invoicestatus`（DB schema 唯讀，`Invoices` 表無 status 欄位），列表以 `invoicecode` join 訂單取得。自 2026-07-23 作廢會清 `Orders.invoicecode`，故同一訂單作廢後重開留下的兩筆（舊作廢號＋新號）中，**舊號 join 不到訂單 → `COALESCE` 顯示為「作廢(2)」，新號 join 得到 → 顯示當前狀態**，兩筆狀態各自正確（先前 quirk 已解）。如需逐張發票最精確狀態，仍以 ezPay 後台為準。

## 電子發票（ezPay）管理

檔案：[EzPayInvoiceService.cs](../src/TFoodies.Infrastructure/Invoicing/EzPay/EzPayInvoiceService.cs)、[InvoiceAdminController.cs](../src/TFoodies.Api.Functions/Controllers/Admin/InvoiceAdminController.cs)

- 開立：`IssueAsync` → AES 加密參數（[EzPayCodec](../src/TFoodies.Infrastructure/Invoicing/EzPay/EzPayCodec.cs)）POST 到 ezPay；成功回發票號。
- 後台發票管理 `/admin/invoices`：列表、作廢（`VoidAsync`→invoicestatus=2）、折讓（`AllowanceAsync`→invoicestatus=3），讀本地 `Invoices`/`Invoicedetails`。
- 時機：付款完成**當下自動即時開立**；失敗留「未開」，後台補開。

> 🐞 **`EzPay__BaseUrl` 只能是 base（`https://inv.ezpay.com.tw/API`），不可含端點（2026-07-23 修）**：`CallAsync` 以 `BaseUrl + "/" + endpoint` 組網址。曾把 `EzPay__BaseUrl` 誤設為 `https://inv.ezpay.com.tw/API/invoice_issue`（開立完整端點），使**每個呼叫都變成 `.../API/invoice_issue/<endpoint>`，被 ezPay 依 `/invoice_issue` 路由到「開立」端點**——症狀是**作廢/折讓一直被當成開立驗證**，逐一索取 `MerchantOrderNo/BuyerName/Category/TaxType/PrintFlag/Item…`（先前為此逐項補欄位其實都在治錯路由的症狀）。修正：**設定改回 base**（`vars.EzPay__BaseUrl` 或 Function App app setting = `https://inv.ezpay.com.tw/API`）；並加程式防呆 `EzPayInvoiceService.NormalizeBaseUrl`（BaseUrl 誤含端點名時自動剝除，測試 `EzPayBaseUrlTests`），即使設定沒改也會導到正確端點。
>
> ⚠️ **ezPay 串接端點與 `Version` 必須精確比照手冊 EZP_INVI_1.2.2（與舊系統實證一致），否則 API 直接拒絕、整條開票靜默失敗（付款仍完成、發票留未開）**：
>
> | API | 端點（接於 `BaseUrl=…/Api`）| `Version` | 發票號參數 |
> |---|---|---|---|
> | 開立 | `invoice_issue`（**非** `invoice/issue`）| `1.5`（舊系統 1.4；**非** 1.0）| 回應讀 `InvoiceNumber` |
> | 作廢 | `invoice_invalid`（**非** `invoice/void`）| `1.0` | 只需 `InvoiceNumber`（**非** `InvoiceNo`）＋ `InvalidReason`（同舊系統；ezPay 以發票號識別）|
> | 折讓 | `allowance_issue`（**非** `invoice/allowance`）| `1.3`（**非** 1.0）| 請求帶 `InvoiceNo` **＋ `MerchantOrderNo`（= 訂單編號，非發票號）**；另須帶 `ItemTaxAmt`、`Status` |
>
> 另：B2B（三聯式）`BuyerName` 須帶**公司抬頭**（`Orders.companytitle`），非會員姓名。前台結帳後不自動開票，多半即上述端點/版本錯誤所致；可查 App Insights 中 `PaymentCompletionService` 的 Warning/Error。
>
> ⚠️ **B2B 統編校驗（2026-07 修）**：ezPay `Category` 由**是否有統編**決定（`EzPayInvoiceService` 對 `BuyerUbn` 一律用 `IsNullOrWhiteSpace` 判斷，B2B 才帶 `BuyerUbn` 並 `Trim()`），避免空字串/空白被判成 B2B 卻無統編、被 ezPay 以「統編沒有」拒絕。
> `IssueInvoiceAsync` 另加**前置校驗**：`invoicetype=3` 但 `companynumber` 為空 → 直接回 `Error.Validation`（「此訂單為三聯式發票，但缺少統一編號…」），不再靜默降級成二聯。舊資料中有一批三聯式訂單 `companynumber` 為 NULL（多為舊系統誤標，抬頭亦缺），後台補開會被此校驗擋下，須先於「編輯訂單」補填統編或改回二聯式。後台詳情頁亦以 `triplicateMissingUbn` 提示並禁用「補開發票」鈕。
>
> 🐞 **BuyerUBN 參數名大小寫 bug（2026-07-22 修）**：ezPay 參數名**大小寫敏感**，統編欄位須為 `BuyerUBN`（全大寫，同舊系統 `AjaxController`）。新系統一度誤植為 `BuyerUbn`，ezPay 收不到統編、卻因 `Category=B2B` 有送而回「**B2B 類別的發票，買受人統編不可為空白**」（HTTP 422 `UNPROCESSABLE_ENTITY`）。症狀：即使訂單統編/抬頭齊全（如 `O20260722002` 統編 83150659）仍開不出 B2B，且新系統從未成功開過任何 B2B 發票（既有已開 B2B 皆舊系統開立）。修正僅改 `EzPayInvoiceService` 送出的 key 名；**須重新部署 tfoodies-api 才生效**。
>
> 🐞 **明細逐項小計校驗（2026-07-22 修）**：ezPay 逐項要求 `ItemAmt == ItemPrice × ItemCount`，否則回「**請檢查商品資訊第N項金額小計是否正確**」。管理員議價單的 `Orderdetails.subtotal` 可能不等於 `price×qty`（單價談過、未記 `discount` 旗標），原本直接把 `subtotal` 當 `ItemAmt` 送出被退。`PaymentCompletionService` 已比照舊系統 `AjaxController`：主項帶 `price×qty`，差額另拆一條負數調整明細，確保每項自洽且加總＝實付。⚠️ 訂單層折扣（折扣碼 `Orders.discount`）目前仍未拆成明細列，`Σ ItemAmt` 會比 `TotalAmt` 多出折扣額；舊系統多年如此、ezPay 容忍，惟若日後 ezPay 收緊需另補訂單層調整列。
>
> 🐞 **作廢一直被當「開立」驗證 → 病根是 BaseUrl 誤含端點（2026-07-23 釐清）**：症狀是作廢逐一索取 `MerchantOrderNo/BuyerName/Category/TaxType/PrintFlag/Item…`，一度誤判為「ezPay 收緊作廢驗證」而逐項補齊、甚至試 `RespondType=String`（皆無效）。**真因**：`EzPay__BaseUrl` 被設成 `.../API/invoice_issue`，使作廢網址變 `.../invoice_issue/invoice_invalid` 被路由到**開立**端點（見上方 BaseUrl 🐞）。設定改回 base ＋ `NormalizeBaseUrl` 後，作廢正確打到 `invoice_invalid`，**請求已還原為最小集 `InvoiceNumber`＋`InvalidReason`**（同舊系統），`VoidAsync(invoiceNumber, reason)`。折讓（`allowance_issue`，走正確端點）的 `MerchantOrderNo` 須為 `Orders.ordercode`（非發票號）—此為獨立既有 bug，保留修正。
>
> 🐞 **`Orders.total` 語意雙重扣折扣（2026-07-22 修）**：`Orders.total` 的**權威語意 = 純商品小計**（`Σ Orderdetails.subtotal`，**不含**運費、**不含**訂單層折扣），對齊舊系統（`Cart.TotalPrice()`＋`order.total = ca.TotalPrice()`）；所有消費端一律 `應付 = total + freight − discount` 還原（發票 `TotalAmt`、FISC `purchAmt`、Income 金額、會計報表、Excel）。新系統一度在**寫入端**（store `OrderService`、admin 建單/編輯前端）把「最終金額 `subtotal+freight−discount`」直接寫進 `total`，導致消費端再減一次 → **運費多加、折扣多扣**（B2B 折扣單最明顯：折扣被扣兩次，發票 `TotalAmt = subtotal + 2×freight − 2×discount`）。因多數單免運（freight=0）、無折扣碼而長期未爆。**修正**：寫入端改回存商品小計；顯示端（admin 清單/詳情總計、store 會員清單）改算 `total + freight − discount`；消費端不動；歷史資料以冪等腳本 [`scripts/fix-orders-total-semantics.sql`](../scripts/fix-orders-total-semantics.sql)（`total ← Σ Orderdetails.subtotal`，舊單 no-op）校正。store 會員清單為此在 `GetMemberOrdersAsync`／`OrderListItem` 補帶 `freight`/`discount`。

## 收款連結（Paymentlinks）— 不走訂單的臨時收款

後台填一個金額就產生一次性付款連結（客訂、補款、活動費用等不走商城流程的收款），客人開連結填收件資料後直接付款。**付款方式由後台建立時指定**（`paymethod`：1=信用卡走 FISC、8=LINE Pay），客人不能改。**與訂單付款共用 FISC 欄位產生器 / LINE Pay client 與回傳解析，但不共用 `PaymentCompletionService`。**

### 流程

```
後台 /admin/paymentlinks（OrderMs 權限）
  └ POST /admin/paymentlinks {title, note, amount, validDays, payMethod}
      ├ payMethod 白名單 1|8；選 8 但 LinePay__Enabled=false → 400
      ├ CodeKind.PaymentLink → PL+yyyyMMdd+3碼（=FISC lidm / LINE Pay orderId，13 字元）
      ├ token = RandomNumberGenerator 16 bytes → 32 字元 hex（128-bit）
      └ 201 {id, code, token, url}   url = {StoreOrigin}/Pay/{token}
                    ↓ 後台人員複製連結傳給客人（LINE / email）
客人開 /Pay/{token}（免登入，layouts/pay.vue 極簡外殼）
  ├ GET  /store/paylinks/{token}          → 只回 {code,title,amount,status,isExpired,payMethod}
  └ POST /store/paylinks/{token}/checkout → 寫入客人資料 + 依 payMethod 回：
        payMethod=1 → {payMethod, actionUrl, fields}  前端 auto-submit 至 FISC
        payMethod=8 → {payMethod, redirectUrl}        前端整頁導向 LINE Pay
                    ↓ 金額一律取自 DB，前端不傳金額
[信用卡] FISC 刷卡頁 → POST /store/payment/return-paylink（PayLinkAuthResUrl）
  ├ FiscWebposParser.ParseForm（與訂單同一份成功判定）
  ├ PaymentLinkService.MarkPaidAsync（冪等）→ 寄通知信給 angela@tfoodies.com
  └ 302 → {origin 或 StoreOrigin}/Pay/Result?code=PL...&paid=1|0

[LINE Pay] LINE Pay 付款頁 → GET /store/payment/linepay/confirm-paylink
  ├ CompleteLinePayAsync：狀態已付款 → 直接成功；否則以 DB 金額 confirm
  ├ MarkPaidAsync（同一份冪等 UPDATE）→ txnref = "LINEPay transactionId:{id}"
  └ 302 → {origin 或 StoreOrigin}/Pay/Result?code=PL...&paid=1|0
     取消 → /store/payment/linepay/cancel-paylink → …?paid=0
```

主動通知：財金的 notify URL 在特店端只登錄一組，訂單與收款連結都會打到 `/store/payment/notify`，由 `PaymentController.Notify` 依 **lidm `PL` 前綴**分派（訂單標記不到才轉收款連結）。

### ⚠️ 刻意不寫 Incomes、不開發票（不是漏做）

`Orders.memberid` 與 `Incomes.memberid` 都是 `NOT NULL` + FK 到 `Members`，而收款連結**不綁會員**（客人只填姓名/手機/地址，連 email 都不收）。硬塞假 memberid 會污染會員數與會計報表：銷貨收入有金額卻無對應 `Orderdetails`/`Invoicedetails`，帳會不平。

因此金額**只存在 `Paymentlinks` 表**，由營運依通知信人工入帳與開立發票；通知信中亦明載此事。後台列表另提供「手動標記已付款」，作為 FISC 未回呼（客人關瀏覽器）時的補救。

### 與訂單刷卡的差異

| | 訂單刷卡 | 收款連結 |
|---|---|---|
| lidm | `Orders.ordercode` | `Paymentlinks.paymentlinkcode`（`PL` 前綴） |
| 金額 | `total + freight − discount` | `amount`（管理員填多少收多少，不加運費、不套折扣） |
| 會員 | 必須有 | 無 |
| 完成處理 | `PaymentCompletionService`（Incomes + 發票 + 寄信） | `PaymentLinkService`（只有冪等標記 + 寄信） |
| 回呼 | `/store/payment/return`(-admin)、`/store/payment/linepay/confirm` | `/store/payment/return-paylink`、`/store/payment/linepay/confirm-paylink` |
| 結果頁 | `/Order/Success` | `/Pay/Result` |
| 付款方式 | 顧客於結帳頁自選 | 後台建立時指定（`paymethod`），客人不能改 |

### 相關檔案

- [IPaymentLinkService.cs](../src/TFoodies.Application/Abstractions/IPaymentLinkService.cs) · [PaymentLinkService.cs](../src/TFoodies.Infrastructure/Payments/PaymentLinkService.cs)
- [PaymentLinkController.cs](../src/TFoodies.Api.Functions/Controllers/PaymentLinkController.cs)（客人端，`store/` 前綴故免 JWT）· [PaymentLinkAdminController.cs](../src/TFoodies.Api.Functions/Controllers/Admin/PaymentLinkAdminController.cs)
- 共用元件：[FiscWebposParser.cs](../src/TFoodies.Api.Functions/Helpers/FiscWebposParser.cs)（授權結果解析）· [FiscRedirect.cs](../src/TFoodies.Api.Functions/Helpers/FiscRedirect.cs)（回跳白名單，防 open redirect）· [LinePayClient.cs](../src/TFoodies.Infrastructure/Payments/LinePay/LinePayClient.cs)
- 建表：[scripts/add-paymentlinks.sql](../scripts/add-paymentlinks.sql)（冪等）· [scripts/add-paymentlink-paymethod.sql](../scripts/add-paymentlink-paymethod.sql)（追加 `paymethod`，DEFAULT 1 故既有連結行為不變）
- 前端：[web/admin/src/views/paymentlinks/PaymentLinksView.vue](../web/admin/src/views/paymentlinks/PaymentLinksView.vue) · [web/store/app/pages/Pay/[token].vue](../web/store/app/pages/Pay/) · `layouts/pay.vue`

## 冪等與失敗處理

- `MarkPaidAsync`：已付款回 false → return＋notify 雙觸發不重複入帳/開票/寄信。
- LINE Pay confirm 回跳被重放（使用者重整/上一頁）：先查訂單/連結狀態，已付款直接視為成功不再打 API；真的打到 LINE Pay 時 `returnCode 1172`（交易已完成）亦視同成功。三道防線都不會造成重複入帳。
- `PaymentLinkService.MarkPaidAsync`：`UPDATE ... WHERE paymentlinkcode=@code AND status=0`，`rows==0` 即不寄信（同上，雙觸發只寄一封）。
- `SmtpEmailService` 對 Bcc 與收件人相同者跳過：營運信箱本就在 Bcc 名單，收款連結通知信正是寄給他，不去重會重複投遞。
- `IssueInvoiceAsync`：`UPDATE ... WHERE invoicestatus=0` 護欄，避免重複建發票。
- 開票失敗：付款照算完成，發票狀態留「未開」→ 後台補開。

## WEBPOS 送出欄位（與舊系統可運作表單一致，實測成功）

`merID, MerchantID, TerminalID, lidm(=orderCode), purchAmt(=total+freight−discount), AuthResURL, enCodeType=UTF-8, PayType=0, AutoCap=1`。
成功判定：財金回傳 `status=="0"` 且 `authCode` 非空。欄位/錯誤碼細節見 [docs/12](12-payment-invoice-config.md)。

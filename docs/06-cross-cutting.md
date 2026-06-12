# 06 · 跨切面：列舉、業務流程、整合、技術債

> 統整跨多層的狀態機、端到端業務流程、外部整合與必須先知道的技術債/安全問題。

## 列舉狀態機（`tfoodies.Libs/Enum.cs`）

整個 ERP 的狀態機核心（對應 DB 整數欄位，中文名）：

| Enum | 值 |
|---|---|
| `EnumPayStatus` | 未付款=0, 已付款=1, 退款=2, 免付款=3, 取消=4 |
| `EnumDeliverStatus` | 未出貨=0, 已出貨=1, 退貨=2, 取消=3, **待出貨=4**(已揀未出) |
| `EnumOrderType` | 線上單=1, 線下單=2, 自用=3, **預購=4**, 公關=5 |
| `EnumWarehouseType` | 線上倉=1, 線下倉=2, 瑕疵品倉=3 |
| `EnumWarehouseStatus` | 未入庫=0, 已入庫=1 |
| `EnumDiscountType` | 折扣=0(百分比 rate), 折價=1(固定金額) |
| `EnumInvoiceType` | 二聯式=1, 捐贈=2, 三聯式=3, 免開=4, POS機=5 |
| `EnumInvoiceStatus` | 未開=0, 已開=1, 作廢=2 |
| `EnumSourceType` | 手動輸入=0, 採購帶入=1, 退貨帶入=2（庫存來源） |
| `EnumExpenditureStatus` | 未付款=0, 部分付款=1, 已付款=2 |
| 其他 | `EnumRefundStatus`, `EnumReceiveStatus`, `EnumReciverTime`, `EnumBannerType`, `EnumPayType`(信用卡/宅配貨到付款/ATM/免付款/現金/電匯/支票) |

## 端到端業務流程

服務層是「持久化動詞」；編排在 `tfoodiesBackend` 控制器，用**共享 DbContext 建構子**讓每個流程成為一次 `SaveChanges()` 工作單元。

### (a) 下單 → 配貨/扣庫存 → 單號 → 發票/會計
1. 建 `Orders`（`ordertype`、`paystatus=未付款(0)`、`deliverstatus=未出貨(0)`、total/freight/discount）+ `Ordercodes`；建 `Orderdetails`。
2. 揀/出貨時，每行 `WarehouseStocksService.GetStockWarehouses(warehouseid, productid)` → **依 `expiredate` FIFO** 排序；逐批消耗 `qty` 扣 `Warehousestocks.quantity_left`，每消耗一批寫一筆 `Orderdetailstocks`。訂單 未出貨→待出貨→已出貨。
3. 發票：建 `Invoices`/`Invoicedetails`，VAT 拆分 `tax=price-round(price/1.05)`；連結 `incomeid` 前為未收款。
4. 收款 → 建 `Incomes` 連結發票，`paystatus`→已付款(1)；寫 `Accountings` GL 分錄。

### (b) 向供應商採購 → 入庫
1. 建 `Purchases`+`Purchasedetails`+`Purchasecodes`（對 `Suppliers`）。
2. 入庫（未入庫→已入庫）：建 `Stocks` 批（`expiredate`, `SourceType=採購帶入(1)`）+ `Warehousestocks`（即 `GetStockWarehouses` 之來源）。`General.CheckPurchaseStatus` 推進採購狀態。
3. 建 `Expenditures` 應付（`totalsum=Σ明細`）；累積 `Outcomes` 付款直到 `totalpaid≥totalsum` → 已付款(2)。

### (c) 預購流程
`Preorders` 捕捉需求**不預留庫存**；庫存到貨後後台轉成正式 `Orders`（`ordertype=預購(4)`）走流程 (a)。

### (d) 退貨/退款/換貨
1. `Returns`+`Returndetails`+`Returncodes` 記錄退貨；退貨入庫為 `Stocks`(`SourceType=退貨帶入(2)`，常入瑕疵品倉=3)。訂單 `deliverstatus`→退貨(2)。
2. `Refounds`+`Refoundcodes` 記錄退款；訂單 `paystatus`→退款(2)。折讓為退款狀態變體。
3. `Exchanges`(此處指 FX 匯率；商品換貨經退貨流程)。

### (e) 折扣/促銷
`Discounts` 定義 `EnumDiscountType`：折扣=0(百分比) vs 折價=1(固定)。前台/後台購物車計算產生 `DiscountResponse{discount, discountid, amountprice}`，寫入 `Orders.discount` 後流入流程 (a) 的稅額計算。

### (f) ATM 入帳對帳
前台 `IncomeMsController.Index`：POST `globalmyb2b.com` 證券 API，解析 XML 比對 ATM 碼(`codeatm`)+金額 → 標記訂單已付款、建 Incomes、連 Invoices、記 `GlobalMyB2B`。

## 外部整合一覽

| 整合 | 位置 | 備註 |
|---|---|---|
| Azure Blob | `tfoodiesBackend/Commons/AzureBlob.cs` | 圖檔；上傳前 `ImageResize`/`Watermark`(System.Drawing) |
| 簡訊 三竹 Mitake | `tfoodiesBackend/AjaxController.cs:822` | Big5 INI POST `smexpress.mitake.com.tw:9600`，**帳密寫死** |
| reCAPTCHA v2/v3 | `Libs/GoogleReCaptcha.cs` / `Libs/Librarys.cs` | secret 寫死 |
| MongoDB 瀏覽紀錄 | 前台 `AjaxController.RecordLog` | 由 `mongodb.isrecord` 切換 Mongo/SQL；geoplugin + UAParser |
| ezPay/NewebPay 金流+電子發票 | `tfoodiesBackend/Commons/General.cs`(AES-256) + `AjaxController.CreateInv/CancelInv` | HashKey/HashIV 寫死 |
| ATM 虛擬帳號 | `Libs.GetAtmCode` | 本地產生（國泰格式+檢查碼） |
| Email | `Libs.SendMail` | Sendinblue SMTP（帳密寫死，⚠️ 失敗無限遞迴） |
| 台灣 GCIS 公司登記 | `Libs.GetCompany*` | 統編查詢 |
| globalmyb2b 證券 API | 前台 `IncomeMsController` | ATM 對帳 |

## 通知信清單 (Email Notifications)

> 系統所有「寄送 Email」情境集中於此。SMS（三竹簡訊）為獨立管線，見上表，與 email 無關。
> 共用寄信方法：舊系統 `Libs.SendMail`；新系統 `IEmailService.SendAsync`（實作 `Infrastructure/Email/SmtpEmailService.cs`，皆走 Sendinblue/Brevo SMTP relay，固定 BCC 營運信箱）。
> 版型慣例：新系統信件採純 table + inline-style、相容 Outlook/Gmail，共用品牌視覺（主色 `#26B7BC`、深色 `#156467`）。

| 情境 | 主旨 | 觸發點（新系統） | 觸發點（舊系統） | 內容重點 |
|---|---|---|---|---|
| **訂單成立通知** | `食在呼 TFoodies–訂單通知 {ordercode}` | `StoreOrderController.PlaceOrder` 成功後 best-effort 寄送，版型 `BuildOrderMailHtml`（POST `/store/orders`） | `MainMsController.cs:529`（`ShoppingSuccess`） | 訂單編號、訂購日期、付款方式、金額摘要（小計/運費/折扣/應付總額）；ATM 付款時附匯款卡（013 國泰世華、虛擬帳號、金額、繳款截止日）。收件者為 `BuyerEmail`，無 email 則不寄。 |
| **付款完成通知** | `食在呼 TFoodies–付款完成通知 {ordercode}` | 財金 WEBPOS 授權成功、**首次**標記為已付款時寄送；由 `PaymentCompletionService.MarkPaidAsync` 觸發、版型 `BuildPaidMailHtml`。兩條路徑共用：AuthResURL 導回（POST `/store/payment/return`）與主動通知（POST `/store/payment/notify`） | 舊系統**無**（付款完成不寄 email） | 付款成功確認、訂單編號、信用卡末四碼、付款時間、實付金額。收件者取自 `Members.email`（JOIN）。**冪等**：重送/雙路徑時 `MarkPaidAsync` 已付款則回 false，不重複寄信/開票。電子發票另行開立。 |
| **忘記密碼通知** | `食在呼 TFoodies–忘記密碼通知` | `MemberAuthController.ForgotPassword`，版型 `BuildResetMailHtml`（POST `/auth/forgot-password`） | `AjaxController.cs:702`（`PasswordSend`） | 比對 mobile+email→產生亂數新密碼→更新 DB→寄出明文新密碼，請其登入後自行修改。新系統寄送失敗回 422。 |

**設計注意**：
- 寄訂單通知信為 **best-effort**：`SendAsync` 內部已 catch 並回傳 `false`，寄信失敗**不影響**下單成功回應（不像舊系統 `SendMail` 失敗無限遞迴）。
- 新系統 `PlaceOrderResult` 不含商品明細，故訂單通知信僅呈現金額摘要（對齊舊系統，舊信亦未列明細）；明細請於會員中心查詢。
- **電子發票**由 ezPay/NewebPay 金流商以 `BuyerEmail` 代為寄送，非本系統 `SendMail`/`SendAsync`，故不列於本表。
- 「付款完成通知」為**新系統新增**（舊系統付款完成不寄信），於財金 WEBPOS 信用卡授權成功首次標記已付款時觸發，與發票開立共用 `MarkPaidAsync` 冪等判斷（已付款回 false 即跳過）。
- 舊系統「註冊」「出貨通知」**皆未寄 email**；如新系統需擴充，於此表補列。

## ⚠️ 技術債 / 安全（移轉重點清單）

1. **機密全明文且寫死**：SQL `sa` 密碼、Azure Blob key、三竹/SMTP 帳密、reCAPTCHA secret、ezPay HashKey/IV、DES cookie key(`16816888`)/IV(`88888888`)。
2. **密碼明文儲存與比對**（Members + Admins）；寫死後門 `itadmin/QQQQQ`(AdminID 888) 繞過全部 RBAC。
3. **API 零驗證**（匿名唯讀）。
4. `BaseService` 死碼、CRUD 大量複製貼上；`Result.Exception` 靜默吞錯。
5. **無真正 UnitOfWork**；單號產生器(`Librarys.New*Code`)與 ATM 碼有 **race condition**（讀-增-寫非原子）。
6. `SendMail` 失敗**無限遞迴重試**。
7. **字串去尾綴式 RBAC**（`CheckSessionAttribute`），對動作改名脆弱；多個 Export/Ajax 端點缺逐功能授權。
8. `customErrors=Off` + `debug=true`（全環境）；in-proc session（非 web farm 安全）。
9. **雙影像庫**（System.Drawing + ImageSharp）；DocumentDB 已引入但停用改 MongoDB。
10. **三處設定分歧**（前台/後台 Web.config vs Service App.config 的 blob 帳號與 freight）。
11. Schema：金額混型(Int32/Decimal)、9 張 `*codes` + 5 張 `*medias` 可泛化、多個 FK 形狀欄位無參照完整性、`Orders` god-table、`Refounds` 拼字錯誤已固化。
12. .NET Framework 4.8 + EF6 DB-first + 大量 4.3 `System.*` shim + bindingRedirect — 升級 .NET (Core) 須重做資料層與設定。

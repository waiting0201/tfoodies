# 04 · 後台管理 (Backend Admin — `tfoodiesBackend/`)

> ASP.NET MVC 5 (Razor) + SmartAdmin 佈景。控制器繼承 `BaseController`，只呼叫 `tfoodies.Service`，繫結 `tfoodies.Models`。「**Ms** = Management System（管理模組）」。
> 路徑相對於 `reference/old`。

## 權限與驗證模型

### 登入（`Controllers/MainController.cs`）
- `GET Login()` → `Views/Main/Login.cshtml`。
- `POST Login(username, password, form)`(async)：驗 Google reCAPTCHA v3(`form["GoogleCaptchaToken"]`，`IsCaptchaValid`：Success + Action==`contact_us` + Score>0.5) → `ValidateUser`。
- `ValidateUser`（private）：⚠️ **寫死超級管理員後門** `itadmin/QQQQQ` → `Session["AdminID"]=888`；否則查 `Admins`(Username + Isenable==1) **明文比對 Password**。成功設 Session：`IsLogin`、`Username`、`AdminID`、`AdminLims`。
- `Logout()` → `Session.RemoveAll()`。
- `Index()` `[CheckSession]` — 儀表板。
- `CardResult(lidm, lastPan4, status)` — 信用卡回呼頁：依 `ordercode==lidm` 找單，建 Invoices+明細；`status=="0"` 標記已付款+建 Incomes(`Librarys.NewIncomeCode`)+連結發票。

### 驗證 Filter（`Filters/CheckSessionAttribute.cs`，屬性 `IsAuth`）
`OnActionExecuting` 兩層：
1. **登入閘門（恆）**：`Session["IsLogin"]` 非 true → 導 `Main/Login`。
2. **權限閘門（`IsAuth=true` 且 `AdminID != 888`）**：由路由推導權限 key — 剝除動作字尾(`AddNo→Add`、`EditNo→Edit`，再移除 `Add/Edit/Delete/Result/Export/Sort/details`，並映射 `Brandphotos→Brands`、`Productphotos→Products`、`Eventphotos→Events`)得基底 `ac`；查父 `Lims`(Key==controller)→子 `Lims`(Key==ac && ParentID==parent)→`limid`→`AdminLims(AdminID,limid)`。無則導 `/Error/Validation`。再檢查 CRUD 位元：動作含 `Add` 需 `IsAdd`、`Edit` 需 `IsUpdate`、`Delete` 需 `IsDelete`。
⚠️ AdminID 888 繞過全部；多數 Export/Ajax 僅 `[CheckSession]`(登入即可)。字串去尾綴式 RBAC 對動作改名脆弱。

### 權限資料模型
- `Lims`：自參考選單/功能樹（`Key` 頂層=控制器名、子層=動作基底名；`Value` 顯示標籤；`ParentID`；導覽 `Lims1`=子、`Lims2`=父）。
- `AdminLims`：管理員↔Lims 授予，旗標 `IsAdd/IsUpdate/IsDelete`。
- `Admins`：後台使用者（明文 Password）。

### 選單渲染（`App_Helpers/HtmlHelperExtensions.cs`）
`BaseController.OnActionExecuted` 每請求載入 `ViewBag.SiteLinks`(全 Lims 排序) + `ViewBag.BlobUrl`。`_Aside.cshtml` → `Html.SiteMenuAsUnorderedList`，`buildMenuItems` 遞迴建側欄；AdminID≠888 時依 `Session["AdminLims"]` 隱藏無權節點。實際存取強制在 `CheckSession` filter。

### Commons 共用
- `Commons/General.cs`：`CheckPurchaseStatus`(由明細重算採購狀態 1未入庫/2已入庫/3部分入庫)、`CheckExpenditureStatus`(由 outcomes vs 明細重算 未付款/部分付款/已付款)、AES-256(Rijndael CBC)加解密(金流/發票)。
- `Commons/AzureBlob.cs`、`ImageResize.cs`、`Watermark.cs` — Azure Blob 圖檔。
- `tfoodies.Libs.Librarys` — 單號產生器、`CheckInventory`/`SetInventory`(庫存扣減)。

## 模組逐一說明

> ⚠️ 命名：**`HomeMs` = 前台內容(CMS)管理**（非分析儀表板；儀表板是 `Main/Index`）。

### HomeMs — 前台內容 CMS（`HomeMsController`）
Banners(首頁輪播)、Recipes(食材/調味/步驟+關聯商品)、Issues(特集 連結食譜+商品)、News、Blogs、Events(+Eventphotos 圖庫)、Knowledges(問答+關聯商品)。統一 list/Sort/Add/Edit/Delete；圖檔上傳 Azure Blob；富文本以 HtmlAgilityPack 抽出 base64 圖→Blob、YouTube iframe→媒體表。全 `[CheckSession(IsAuth=true)]`。

> **關聯商品（帶貨）**：新系統後台 Issue / Knowledge 編輯表單皆有「相關商品」挑選器（同 Recipe 做法），存入 `Issueproducts` / `Knowledgeproducts`（delete-insert，`SaveIssueProductsAsync` / `SaveKnowledgeProductsAsync` 於 transaction 中）。前台 `/Issue`、`/Knowledge` 詳細頁據此顯示「購買相關商品」區塊（含一鍵加入購物車）。`Knowledgeproducts` 為本次新增表（`scripts/add-knowledgeproducts.sql`，無 migration；本機與正式環境皆已套用）。

### ProductMs — 商品型錄（`ProductMsController`）
Brands(+Brandphotos 圖庫)、Producttypes、Tags、Products(+Productphotos)。
- Products：依 type/brand/disabled/keyword 篩選；支援**套裝**(`isset`+`Setproducts`)、標籤 M:N(`tagids`)、價格欄、SEO、旗標 ishot/isnew/isgroupbuy/isdisabled。**軟刪除**(`isdisabled=true`)+Blob 清理。EditProducts 帶 `ViewBag.Warehousestocks`。

### OrderMs — 訂單處理與出貨（`OrderMsController`）
依 `EnumDeliverStatus`(未出貨/待出貨/已出貨/退貨/取消) 驅動。常數 `lovecode="01170"`(發票捐贈碼)。
- Logistics：物流商。
- **Orders**(未出貨佇列)：依日/月篩選；`OrdersExport`→NPOI .xls；Add/Edit 含 Orderdetails、會員/收件/郵遞、發票類型(二/三聯/捐贈)、付款、運費/折扣/總額、物流+追蹤、倉庫；EditOrders 重算發票稅；`ExportOrders`→出貨單(`Template/deliver.xlsx`)。
- **Shipments**(待出貨)：`ShipmentsExport(orderids[])`→揀貨單(以 `GetStockWarehouses` 解析各倉批號，建 `PickUp` 行)。
- **Shipped**(已出貨)：依日期/狀態篩選；`ShippedExport` 兩版面 `tfoodies`(內部) / `shopcom`(美安佣金報表，RID/Click_ID + 20% 佣金 + 5% 稅)。
- **Canceled**(取消/退款)、**Returns**(RMA 含 Returndetails，設 `warehousestatus=未入庫`)、**Outofnotices**(缺貨通知)、**Declarations**(報關 stub/TODO)。

### InventoryMs — 庫存與倉庫（`InventoryMsController`）
- Warehouses：含 ResultWarehouses；有庫存則不可刪。
- **Stocks**(進貨 `stocktype=1`報關/`=2`非報關)：Add/Edit(barcode/通知號/報關號/品項/製造效期/qty/weight) 與 AddNoStocks/EditNoStocks(僅 qty)；建立時產生 `Warehousestocks` 行並 `General.CheckPurchaseStatus` 推進採購狀態；綁定 `status==1||3` 的採購。
- **Warehousestocks**(各倉庫存帳/調撥)：`AddWarehousestocks` 執行**倉庫調撥**(FIFO 遞減來源 `quantity_left`，建目的批)。

### PurchaseMs — 採購與供應商（`PurchaseMsController`）
- Suppliers。
- **Purchases**(採購單)：Add 產生 `purchasecode`(`NewPurchaseCode`)、`status=1`；含 Purchasedetails(product/qty/unitprice/subtotal/status)；EditPurchases 做明細新增/更新/刪除差異比對。**無刪除**。

### AccountingMs — 財務 AP/AR（`AccountingMsController`，最大財務樞紐）
全 CRUD，皆 `[CheckSession(IsAuth=true)]`：
- Exchanges(匯率，被採購用則不可刪)、Accountings(會計科目，被引用則不可刪)。
- **Expenditures**(應付憑單)：Add 產生 `expenditurecode`、`sourcetype=0`；含 Expendituredetails(accountingid/price/summary)；僅未付款可刪並還原採購 `isexpenditure=false`。
- **Outcomes**(應付付款)：對 Expenditure 付款，`CheckExpenditureStatus` 重算狀態；`GetUnpayList()`。
- **Refounds**(客戶退款)：綁 Returns，設 return `refundstatus=已退款`、order `paystatus=退款`；刪除反轉。
- **Invoices**(應收發票)：將多筆未付 Orders(`orderid[]`)包成發票明細，`TotalAmt=total+freight-discount` 拆 5% VAT(Amt/TaxAmt)；未連結 income 才可刪。
- **Incomes**(應收收款)：連結多筆 Invoices(`invoiceid[]`)，標記其 orders `paystatus=已付款`+設 paydate；刪除反轉。
- 明細部分檢視：`Shared/_IncomeDetail`/`_InvoiceDetail`/`_RefoundDetail`（由 Ajax `GetXxxDetail` 餵）。

### StatementMs — 財務報表
`Incomestatements()` → 損益表畫面。

### ReportMs — 銷售報表（`ReportMsController`）
- `Salereports(ordermonth)`：月商品數量報表（套裝展開為組件 qty，計已出貨/退貨）。
- `Amountreports(sd, ed, paystatus)`：日期區間營收報表（需起訖日，可選付款狀態）。

### MemberMs — 客戶與簡訊行銷（`MemberMsController`）
- Members：依姓名/手機搜尋；軟刪除(`isenable=false`)；含經銷旗標/折扣、郵遞/地址、明文密碼(僅提供時更新)。
- Sms(群發)、Smsdetails(收件人；已送出不可刪)；加收件人/發送走 Ajax `AddSmsMembers`/`SendSms`。

### SettingMs — 系統設定與管理員/權限（`SettingMsController`）
- **Admins**(後台使用者+權限)：Add/Edit 載頂層 Lims(`ParentID==null`)權限矩陣，繫結 `AdminLims` 集合(LimID+IsAdd/IsUpdate/IsDelete)；EditAdmins 做差異比對。**權限在此管理**。軟刪除(`Isenable=0`)。
- Questiontypes/Questions(FAQ，富文本同 HomeMs 抽圖)、Discounts(折價券，`Checkdiscountcode` 驗唯一，軟刪 `isdisable=1`)。

### Ajax — 非同步與整合（`AjaxController`，全 `[CheckSession]`）
- 唯一性驗證：`Checknoticenumber/Checkproductnum/Checkproductname/Checkproducttypename/Checkmobile/Checkdiscountcode/Checkaccountingcode`。
- 查詢/連動：`GetProductbyProductnum/GetAccountingbyAccountingcode/GetZipcodeByCity/GetPurchaseDetailByPurchaseid/GetWarehousesWithoutSelf/GetWarehouseStock/GetCompanyTitle/GetMember/GetOrderdetailByOrdercode/GetXxxDetail`。
- **電子發票**：`CreateInv(orderid)`/`CancelInv(orderid)`（POST `InvCreateUrl`/`InvCancelUrl`，AES via General）。
- **訂單狀態機**：`ChangeToBeShipped`(未出貨→待出貨)、`ChangeToShipped(orderid, warehouseid, deliverdate)`(待出貨→已出貨：`Librarys.CheckInventory` 檢查、`SetInventory` 扣庫存、無發票則自動建發票+5% 拆分)。
- **簡訊**：`AddSmsMembers`/`SendSms`（POST 三竹）。
- **退貨/採購工作流**：`ChangeWarehousing(returnid)`(退貨回庫)、`ChangeToExpenditure(purchaseid)`(採購轉應付憑單)。

### CaptchaController
`VerificationCode()` — GDI+ 4 位數 GIF（登入實際用 reCAPTCHA，此為遺留/未用）。

## 後台模組地圖
| 控制器 | Lims.Key 頂層 | 主要畫面 | 領域 |
|---|---|---|---|
| MainController | (登入/儀表板) | Login, Index, CardResult | 驗證/落地/卡片回呼 |
| HomeMsController | HomeMs | Banners, Recipes, Issues, News, Blogs, Events(+photos), Knowledges | 前台內容 CMS |
| ProductMsController | ProductMs | Brands(+photos), Producttypes, Tags, Products(+photos) | 商品型錄 |
| OrderMsController | OrderMs | Orders, Shipments, Shipped, Canceled, Returns, Logistics, Outofnotices, Declarations | 訂單生命週期/出貨 |
| InventoryMsController | InventoryMs | Warehouses, Stocks, Warehousestocks | 庫存/倉庫 |
| PurchaseMsController | PurchaseMs | Suppliers, Purchases | 採購 |
| AccountingMsController | AccountingMs | Exchanges, Accountings, Expenditures, Outcomes, Refounds, Invoices, Incomes | 財務 AP/AR |
| StatementMsController | StatementMs | Incomestatements | 財務報表 |
| ReportMsController | ReportMs | Salereports, Amountreports | 銷售/營收報表 |
| MemberMsController | MemberMs | Members, Sms, Smsdetails | 客戶/簡訊 |
| SettingMsController | SettingMs | Admins, Questiontypes, Questions, Discounts | 系統設定/權限/FAQ/折價券 |
| AjaxController | — | (JSON) | 驗證/查詢/發票/狀態變更 |

## 跨切面慣例
- **權限 key** = `CheckSession` 剝除規則後的動作基底名；圖庫/明細頁映射回父功能。
- **CRUD 樣板統一**：`<Entity>(p=...)` list(PagedList 15-20) + 選用 `Sort<Entity>` + `Add/Edit`(GET+POST) + `Delete`(常軟刪)。POST 用 `TryUpdateModel(entity, whitelist[])`。
- **軟刪除**：Products(isdisabled)、Members(isenable)、Admins(Isenable)、Discounts(isdisable)；其餘硬刪並有 FK 存在檢查。
- 圖檔→Azure Blob(`ViewBag.BlobUrl`)；Excel→NPOI；出貨單範本 `Template/deliver.xlsx`。
- Layout：`_Layout` + `_Aside`(權限選單)/`_Header`/`_Ribbon`/`_Footer`/`_Scripts` + 財務明細 partials。

## ⚠️ 安全觀察
- Admins/Members 密碼明文；寫死後門 `itadmin/QQQQQ`(888) 繞過全權限。
- 多個 Export/Ajax 端點僅登入檢查或無屬性，缺逐功能授權。

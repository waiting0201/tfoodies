# 05 · Web API 與服務層 (API & Service Layer)

> `tfoodiesApi/`（極薄唯讀 Web API 2）與 `tfoodies.Service/`（~70 個服務）。
> **關鍵發現：服務層幾乎無業務邏輯** — 每個服務是 `GenericRepository<TEntity>` 的 per-entity CRUD 包裝，回傳 `IResult`。真正交易性編排（庫存扣減、發票/會計產生、單號、FIFO 揀貨）在 **`tfoodiesBackend` 控制器**（見 [04](04-backend-admin.md)、[06](06-cross-cutting.md)）。
> 路徑相對於 `reference/old`。

## API 端點（`tfoodiesApi/`）
路由 `WebApiConfig.cs`：`MapHttpAttributeRoutes()` + `api/{controller}/{id}`。
| Verb | 路由 | 參數 | 回傳 | 服務 |
|---|---|---|---|---|
| GET | `api/Product` | title(選用子字串)、skip(0)、take(10) | `SearchProductResponse` | `ProductsService.Get()` |

- `ProductController.Get`：載入全部 Products，選用 `title.Contains` 篩選，**在記憶體中** Skip/Take（非 DB 分頁）。有結果回 `code="200"`+datas+paging，否則 `code="202"`。
- `OrderController` — **空殼**，無端點。
- `BaseController` — 空殼 `ApiController`，無 filter。
- ⚠️ **完全無驗證**（無 token/api key/`[Authorize]`/handler）。`Web.config` 空 appSettings。此 API 非行動/結帳後端，僅匿名唯讀商品搜尋。

## 服務層模式（`tfoodies.Service/`）
- **`Result` / `IResult`**：寫入回傳包裝 `{Guid ID; bool Success; string Message; Exception Exception; List<IResult> InnerResults}`。無 `Result<T>`；讀取直接回實體/`IEnumerable`/`IQueryable`；⚠️ **例外吞進 `Exception`**，呼叫端須查 `Success`。刪除查無設 `"找不到資料"`。
- **`BaseService` / `IBaseService<TEntity>`**：定義標準介面（Create/Update/SpecificUpdate/Delete/GetByID/Get/SaveChanges）。⚠️ **具體服務並未繼承**，而是各自複製相同 CRUD（~60 檔幾乎逐字相同）。
- **`GenericRepository`**：包 `tfoodiesEntities`+`DbSet`；`Get()` 回 `IQueryable`（無 eager `Include`）；`SpecificUpdate` 停用驗證、僅標記指定欄位 `IsModified`。
- **DbContext 共享（交易）**：每服務有 `Service()`(自建 context) 與 `Service(tfoodiesEntities context)`(共享) 兩建構子。後台用共享多載讓多服務參與一次 `SaveChanges()` 工作單元。⚠️ 服務層無分頁輔助（分頁在控制器臨時做）。

## 核心業務服務（詳）
真正「業務規則」來自：(a) 少數自訂查詢/投影方法、(b) 狀態欄位語意（`Enum.cs`，見 [06](06-cross-cutting.md#列舉狀態機-enumcs)）。

- **`OrdersService`** — CRUD + `GetUnpayListByMemberID`(paystatus 0 未付款 或 5)、`GetListByOrderIDs`、`GetInvoicedetailsByMemberID`（⚠️ **唯一真正稅務計算**：`price=total+freight-discount`，`tax=price-round(price/1.05)` 拆 5% VAT，`MidpointRounding.AwayFromZero`）。
- **`OrderDetailsService` / `OrderDetailStocksService`** — 純 CRUD。`Orderdetailstocks` 是訂單行↔`Warehousestocks` 批號的配貨 join（批號級可追溯），由後台 FIFO 流程寫入。
- **`OrderCodesService`** — 純 CRUD，訂單流水號登記。
- **`ProductsService`** — 純 CRUD（公開 API 查詢對象）。
- **`StocksService`** — 純 CRUD。`Stocks` 為綁 `Purchasedetails` 的進貨批，帶 `expiredate`。
- **`WarehouseStocksService`** — ⚠️ **唯一含庫存邏輯**：`GetStockWarehouses(warehouseid, productid)` =
  ```
  Get().Where(a => a.warehouseid==warehouseid
                && a.Stocks.Purchasedetails.productid==productid
                && a.quantity_left!=0)
       .OrderBy(o => o.Stocks.expiredate).ThenBy(o => o.quantity)
  ```
  **依效期 FIFO 揀貨**（最早到期先出）。後台逐批消耗 `qty` 直到滿足。
- **`PurchasesService`/`PurchaseDetailsService`/`PurchaseCodesService`** — 純 CRUD。收貨後（後台）建 `Stocks`(SourceType=採購帶入)+`Warehousestocks`+`Expenditures`。
- **`DiscountsService`** — 純 CRUD（折扣計算在前台/後台購物車，產生 `DiscountResponse`）。
- **`PreordersService`** — 純 CRUD。預購為獨立實體（`EnumOrderType.預購=4`），下單時**不預留庫存**；庫存到後由後台轉正式訂單。
- **`MembersService`** — ⚠️ 唯一帶驗證：`ValidateUser(mobile, password)` 比對 `mobile && isenable && ismember==1` + **明文密碼**，成功填 `HttpContext.Current.Session`。
- **`InvoicesService`/`InvoiceCodesService`/`InvoiceDetailsService`** — CRUD + `GetUnpayListByMemberID`(`incomeid==null`)、`GetListByInvoiceIDs`、`GetListByIncomeID`。規則：**發票連結 Income 前視為未收款**。
- **`AccountingsService`** — 純 CRUD（GL 分錄由後台產生）。
- **`SmsService`/`SmsdetailsService`** — 純 CRUD（無實際發送邏輯，發送外部/後台驅動）。
- **`ExpendituresService`** — ⚠️ 最豐富投影：`GetList/GetUnpayList/GetUnpayListBySupplierID` 回 `ExpenditureItem`（suppliertitle/exchangetitle/`totalsum=Σ明細price`/`totalpaid=ΣOutcomes.amount`/status）。規則：累積 `Outcomes` 直到 `totalpaid≥totalsum` → 狀態已付款(2)。

## 服務目錄（簡，皆 CRUD 除非註明）
- **訂單/銷售**：Orders, OrderDetails, OrderDetailStocks, OrderCodes, Preorders。
- **型錄/商品**：Products, ProductTypes, ProductPhotos, SetProducts, Brands, BrandPhotos, Tags。
- **庫存/採購**：Stocks, WarehouseStocks(FIFO), Warehouses, Purchases, PurchaseDetails, PurchaseCodes, Suppliers, Outofnotices。
- **退貨/退款/換貨**：Returns(`GetUnpayListByMemberID`), ReturnDetails, ReturnCodes, Refounds, RefoundCodes, Exchanges, Declarations。
- **財務/會計**：Invoices, InvoiceDetails, InvoiceCodes, Accountings, Incomes, IncomeCodes, Outcomes, OutcomeCodes, Expenditures, ExpenditureDetails, ExpenditureCodes。
- **物流/地理/通訊**：Logistics, Zipcodes, AtmCodes, Sms, Smsdetails。
- **會員/權限**：Members(`ValidateUser`), Admins, Lims, AdminLims, ViewLogs。
- **內容 CMS**：Banners, Blogs, News, Events, EventPhotos, Recipes(+Ingredients/Seasonings/Steps), Knowledges, Questions, QuestionTypes, Issues。
- **整合**：Globalmyb2b（外部 myb2b/B2B 電子發票整合 stub）。

## ⚠️ 後續注意
- API 僅單一匿名商品搜尋，非結帳/行動後端。
- 無 `Result<T>`；讀取回原始實體；錯誤吞進 `IResult.Exception`。
- 密碼明文；驗證 Session-based（後台用，非 API）。
- 服務複製 CRUD 而非繼承 `BaseService` — 改一個不影響其他。

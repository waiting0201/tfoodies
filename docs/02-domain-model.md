# 02 · 領域模型 / 資料庫 (Domain Model)

> EF6 Database-First。Schema 真實來源：`reference/old/tfoodies.Models/tfoodiesModel.edmx`。
> DbContext + 72 個 `DbSet`：`tfoodies.Models/tfoodiesModel.Context.cs`。實體類別：`tfoodies.Models/*.cs`。

## 全域慣例
- **主鍵多為 `Guid`** (`uniqueidentifier`)；少數為 `Int32` identity：`Admins.AdminID`、`Lims.LimID`、`Zipcodes.zipcodeid`、`Setproducts.setproductid`。
- **金額多為 `Int32`**（台幣整數）；唯 `Purchasedetails`/`Stocks` 用 `Decimal` — 跨域報表須注意混型。
- **`*codes` 表**（Order/Atm/Purchase/Return/Refound/Income/Outcome/Invoice/Expenditure codes）皆同型 `(year, month, day:String, code:Int32)`，為**各文件類型的流水號產生器**，無 FK。⚠️ 9 張幾乎相同。
- **`*medias` 表**（Product/Issue/New/Question/Knowledge medias）同型 `(mediatype, videourl, filename, filenamepath[, photo])`，多型媒體附件。⚠️ 5 張相同。
- `shortener`（URL slug）出現在多數公開內容實體。
- ⚠️ 拼字：**`Refounds`/`refoundcode`**（refund 拼錯）已固化於 schema 與導覽屬性名，須照抄。

## 實體目錄（依業務領域分組）

### 商品型錄 (Catalog)
| 表 | 用途 | PK | 主要關聯 |
|---|---|---|---|
| `Products` | 商品主檔/SKU（productnum, title, fixprice, price, capacity, weight, unit, 旗標 ishot/isnew/isset/isgroupbuy/isdisabled, added 庫存) | productid | →Producttypes, →Brands；M:N Tags/Recipes/Issues/Members；1:* Orderdetails/Purchasedetails/Productphotos/Productmedias |
| `Producttypes` | 商品分類 | producttypeid | 1:* Products |
| `Productphotos` / `Productmedias` | 圖庫 / 影音 | — | →Products |
| `Brands` | 品牌微網站（重 CMS：故事/人物/版型/logo/影片) | brandid | →Suppliers；1:* Products/Brandphotos |
| `Brandphotos` | 品牌圖庫 | brandphotoid | →Brands |
| `Tags` | 商品標籤 | tagid | M:N Products（junction `Producttags`） |
| `Setproducts` | **組合/套裝商品**：`oproductid`(套裝)→`productid`(組件)+qty | setproductid(Int32) | 兩 FK 指向 Products（自參考組合） |
| `Promotes` / `Promoteproducts` | 促銷活動 / 活動商品 | — | Promotes 1:* Promoteproducts →Products |
| `Discounts` | 折價券（discountcode, istype, v, isonetime, 效期) | discountid | 1:* Orders |

### 訂單 / 銷售 (Orders / Sales)
| 表 | 用途 | PK | 主要關聯 |
|---|---|---|---|
| `Orders` | ⚠️ **訂單主檔（god-table ~38 欄）**：收件人/運費/折扣/總額/paytype/paystatus/deliverstatus/trackingnumber/invoicetype/codeatm/聯盟 RID/Click_ID | orderid | →Members, →Zipcodes, →Logistics, →Warehouses, →Discounts；1:* Orderdetails/Returns/Invoicedetails/Declarationdetails |
| `Orderdetails` | 訂單明細行（qty, price, subtotal, discount, isgift, status) | orderdetailid | →Orders, →Products；1:* Orderdetailstocks/Returndetails |
| `Orderdetailstocks` | **明細→實體庫存配貨**（每筆 warehousestock 的 qty，批號級可追溯) | orderdetailstockid | →Orderdetails, →Warehousestocks |
| `Preorders` / `Preorderdetails` | 預購主檔/明細（複製 Orders 大部分而非共用基底） | — | Preorders 1:* Preorderdetails →Products |
| `Logistics` | 物流商/取貨店（logisticcode, address) | logisticid | 1:* Orders |
| `Returns` / `Returndetails` / `Returncodes` | 退貨主檔(receivestatus/refundstatus/warehousestatus)/明細/單號 | — | Returns →Members,→Orders；Returndetails →Orderdetails,→Accountings |
| `Refounds` / `Refoundcodes` | 退款紀錄(amount,note) / 單號 | refoundid | →Members, →Returns |
| `Exchanges` | ⚠️ **匯率表**(title, rate) — 供 Purchases 用，非商品換貨 | exchangeid | 1:* Purchases |
| `Invoices` / `Invoicedetails` / `Invoicecodes` | 電子發票主檔/明細(price,tax)/單號 | — | Invoices →Incomes,→Members；Invoicedetails →Orders,→Accountings |

### 庫存 / 採購 (Inventory / Procurement)
| 表 | 用途 | PK | 主要關聯 |
|---|---|---|---|
| `Suppliers` | 供應商 | supplierid | 1:* Brands/Purchases/Expenditures |
| `Purchases` / `Purchasedetails` / `Purchasecodes` | 採購單主檔(purchasecode,etd,payment,status,isexpenditure)/明細(unitprice,qty)/單號 | — | Purchases →Suppliers,→Exchanges；Purchasedetails →Products；1:* Stocks |
| `Stocks` | **進貨批次/批號**（barcode, noticenumber, manufacturedate, expiredate, quantity, stocktype, status) | stockid | →Purchasedetails；1:* Warehousestocks |
| `Warehouses` | 倉庫（warehousetype, title) | warehouseid | 1:* Warehousestocks/Orders |
| `Warehousestocks` | **各倉各批的在庫量**（quantity, quantity_left, transdate) | warehousestockid | →Warehouses, →Stocks；1:* Orderdetailstocks |
| `Outofnotices` | 補貨到貨通知訂閱 | outofnoticeid | →Members(nullable), →Products |

### 會員 / CRM
| 表 | 用途 | PK | 主要關聯 |
|---|---|---|---|
| `Members` | 客戶/經銷（name, mobile, password, email, address, isagent, agentdiscount, ismember, gender, birthday) | memberid | →Zipcodes；M:N Products(收藏, junction `Memberproducts`)；1:* Orders/Returns/Refounds/Incomes/Invoices/Outofnotices/Smsdetails |
| `Issues` / `Issuemedias` | 「特集」編輯文章 / 媒體 | issueid | M:N Products & Recipes；1:* Issuemedias |
| `Questions` / `Questiontypes` / `Questionmedias` | FAQ 問答 / 分類 / 媒體 | — | Questiontypes 1:* Questions 1:* Questionmedias |
| `Sms` / `Smsdetails` | 簡訊群發 / 各會員發送紀錄(msgid,statuscode,issend) | — | Sms 1:* Smsdetails →Members |

### 會計 / 財務 (Accounting / Finance)
| 表 | 用途 | PK | 主要關聯 |
|---|---|---|---|
| `Accountings` | 會計科目(accountingcode, title) | accountingid | 1:* Expendituredetails/Invoicedetails/Returndetails |
| `Incomes` / `Incomecodes` | 收款紀錄(amount, fee, incomedate) / 單號 | incomeid | →Members；1:* Invoices |
| `Outcomes` / `Outcomecodes` | 付款（對應 expenditure) / 單號 | outcomeid | →Expenditures |
| `Expenditures` / `Expendituredetails` / `Expenditurecodes` | 應付主檔(sourcetype, status, 連 purchase)/明細(accountingid,price)/單號 | — | Expenditures →Suppliers,→Purchases；1:* Expendituredetails/Outcomes |
| `Declarations` / `Declarationdetails` | 報關/銷售申報主檔 / →Orders | — | Declarations 1:* Declarationdetails →Orders |

### CMS / 內容
| 表 | 用途 | 關聯 |
|---|---|---|
| `Recipes` + `Recipeingredients`/`Recipeseasonings`/`Recipesteps` | 食譜（時長/份量/youtube)+ 食材/調味/步驟 | M:N Products & Issues |
| `Blogs` | 部落客合作連結卡 | (獨立) |
| `News` / `Newmedias` | 最新消息/活動 + 媒體 | News 1:* Newmedias |
| `Events` / `Eventphotos` | 活動 + 圖庫 | Events 1:* Eventphotos |
| `Banners` | 首頁輪播(title,subtitle,url,photo,style,sort) | (獨立) |
| `Knowledges` / `Knowledgemedias` | 知識庫文章(question/answer) + 媒體 | Knowledges 1:* Knowledgemedias |

### 系統 / 權限 / 查詢表
| 表 | 用途 | PK | 關聯 |
|---|---|---|---|
| `Admins` | 後台使用者(Username, Password 明文, Isenable) | AdminID(Int32) | 1:* AdminLims |
| `Lims` | **權限/選單節點，自參考樹**(Key, Value, Icon, ParentID) | LimID(Int32) | self FK；1:* AdminLims |
| `AdminLims` | 管理員↔權限授予(IsAdd/IsUpdate/IsDelete) | AdminLimID(Guid) | →Admins, →Lims |
| `Atmcodes` | ATM 虛擬帳號流水號 | atmcodeid | (獨立) |
| `Zipcodes` | 郵遞區號(countryid, city, area, zipcode) | zipcodeid(Int32) | 1:* Members/Orders |
| `Viewlogs` | 網站分析點擊(sessionid, browser, device, geo, url, referrer) | viewlogid | memberid 欄無 FK |
| `GlobalMyB2B` | 外部「MyB2B」電子發票整合錯誤/稽核紀錄 | globalmyb2bid | (獨立) |

### 隱藏 M:N junction（無實體類別，僅集合導覽）
`Producttags`(Products↔Tags)、`Recipeproducts`(Recipes↔Products)、`Issueproducts`(Issues↔Products)、`Issuerecipes`(Issues↔Recipes)、`Knowledgeproducts`(Knowledges↔Products，新系統新增；建表腳本 `scripts/add-knowledgeproducts.sql`)、`Memberproducts`(Members↔Products 收藏)。

## 核心 FK 鏈：訂單→庫存→商品

```
Members ──< Orders ──< Orderdetails ──< Orderdetailstocks
   │           │            │                   │
   │           ├─> Zipcodes  └─> Products        └─> Warehousestocks ──> Warehouses
   │           ├─> Logistics                          │
   │           ├─> Warehouses                          └─> Stocks ──> Purchasedetails ──> Purchases
   │           └─> Discounts                                           (Products)        (→Suppliers,→Exchanges)
```

- **進貨→在庫鏈**：`Purchases → Purchasedetails → Stocks → Warehousestocks → Orderdetailstocks → Orderdetails → Orders`。批次（`Stocks`）由採購明細產生，放入倉庫（`Warehousestocks`，`quantity_left` 遞減），經 `Orderdetailstocks` 被訂單消耗。
- **退貨/退款鏈**：`Orders → Returns → Returndetails(→Orderdetails,→Accountings)`；`Returns → Refounds(→Members)`。
- **財務鏈**：應付 `Purchases/Suppliers → Expenditures → Expendituredetails(→Accountings)` 與 `Expenditures → Outcomes`；應收 `Members → Incomes → Invoices → Invoicedetails(→Orders,→Accountings)`。
- **型錄**：`Producttypes 1─< Products >─1 Brands >─1 Suppliers`，加 Products M:N {Tags, Recipes, Issues, Members}，與 `Setproducts` 自參考組合。

## 非 DB 的 View/DTO 模型 (`Objects/*`)
- `CartItem` — Session 購物車行；`SubTotal = Price*Quantity`（⚠️ 忽略 Added/FixPrice）。
- `CheckInventoryResponse` `{code, productid, message}` — 庫存檢查結果。
- `DiscountResponse` `{rscode, rsmessage, discount, discountid, amountprice}` — 折價券驗證結果。
- `PickUp` — 倉庫揀貨單行（barcode, noticenumber, expiredate, product, warehouse, qty）。
- `ReportItem` `{productid, name, amount}` — 銷售彙總報表行。
- `SearchProductResponse` `{code, message, datas, paging}` — 商品搜尋包裝（API 用）。
- `PagingResponse` `{total, returned, skip, take}` — 分頁中介資料。
- `ViewLog` — 分析資料寫入前載體（`memberid` 為 string，與 DB `Viewlogs` 的 Guid 不同）。
- `ExpenditureItem` — 攤平的應付摘要（join 供應商/匯率, totalsum/totalpaid）。
- `Geoplugin` — geoplugin.com IP 地理 API 回應 DTO。
- `CaptchaResponseViewModel` — reCAPTCHA v3 驗證回應。

## ⚠️ Schema 觀察 / 技術債
- **多個 FK 形狀欄位無宣告關聯/參照完整性**：`Expendituredetails.purchasedetailid`、`Viewlogs.memberid`、`Orders.invoicecode/invoicestatus`、`Stocks.purchaseid`(冗餘，真正 FK 是 `purchasedetailid`)。完整性靠應用程式碼維護。
- 金額混型（Int32 vs Decimal）；9 張 `*codes`、5 張 `*medias` 可泛化合併。
- `Orders` god-table 混合收件/付款/物流/發票/廣告追蹤；`Preorders` 重複而非共用基底。
- PK 型別不一致（多 Guid，少數 Int32）。
- `Partial/*.cs`（Admins/AdminLims/Lims）僅加 `[Serializable]`，無業務邏輯。

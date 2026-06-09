# 11 · 後台功能稽核（Admin Feature Audit）

> 分析基準：舊系統 `reference/old/tfoodiesBackend/Controllers/` vs 新系統 `RouteTable.cs` + `web/admin/src/views/`  
> 初版日期：2026-06-09  
> 更新規則：每完成一項功能，將 ❌/⚠️ 改為 ✅，並附完成日期

---

## 稽核總表

| 模組 | ✅ 已完成 | ⚠️ 部分完成 | ❌ 缺少 |
|---|---|---|---|
| 訂單管理 | 6 | 3 | 7 |
| 商品管理 | 7 | 3 | 5 |
| 庫存管理 | 3 | 2 | 4 |
| 採購管理 | 7 | 2 | 3 |
| 財務管理 | 8 | 2 | 9 |
| CMS 內容 | 6 | 3 | 6 |
| 會員管理 | 4 | 2 | 2 |
| 報表管理 | 2 | 1 | 1 |
| 系統設定 | 1 | 1 | 2 |
| 損益表   | 0 | 0 | 1 |
| 設計規範 | 1 | - | 5 |

---

## 設計規範符合度

| 問題 | 嚴重度 | 狀態 |
|---|---|---|
| `:root` CSS 變數未定義 → 所有品牌色失效 | 🔴 緊急 | ✅ 2026-06-09 已修正（`style.css` 補 `:root`） |
| `OrdersView.vue` 重複 padding | 🟡 中 | ✅ 2026-06-09（原本已正確，有注釋說明） |
| `ProductFormView.vue` 重複 padding | 🟡 中 | ✅ 2026-06-09 已移除 `.product-form` 的 `padding: 2rem 1rem` |
| `DiscountsView.vue` 混用 Tailwind class | 🟡 中 | ✅ 2026-06-09 按鈕色改用 CSS var，SVG 改 inline style |
| `MembersView.vue` 無編輯入口 | 🟡 中 | ✅ 2026-06-09 新增 side panel 編輯表單（姓名/email/性別/生日/地址/備註/密碼）|
| `ReturnsView.vue` 缺少後台主動建立退貨的入口 | 🟡 中 | ❌ 未修正 |
| 各 view 分頁按鈕未共用 `.btn` 系統 | 🟢 低 | ❌ 未修正 |

---

## 訂單管理（OrderMsController）

### ✅ 已完成
- 訂單清單（分頁、狀態 Tab 篩選、關鍵字搜尋）
- 訂單詳情
- 手動建立訂單（+ 會員搜尋、商品搜尋含縮圖+編號）
- 訂單編輯（收件/付款/出貨/發票/品項 diff）
- 訂單狀態流轉（pending / ship / cancel / pay）
- 退貨清單與處理（receive / refund）

### ⚠️ 部分完成
- **出貨操作**：API 完整，UI 用 `prompt()` 取追蹤號，應改為 Modal 對話框
- **退貨管理**：UI 僅清單 + 詳情展開，缺後台主動建立退貨單入口
- **發票金額聯動**：編輯訂單後是否同步更新 `Invoicedetails` 金額待確認

### ❌ 缺少
- 訂單匯出 Excel（`OrdersExport`）
- 批次勾選出貨（多選 → 一次出貨）
- 撿貨單匯出（`ShipmentsExport`，依效期 FIFO 計算各倉）
- 出貨單匯出（依 `deliver.xlsx` 模板單筆產生）
- 已出貨清單匯出（`ShippedExport`，含美安 shopcom 格式）
- 申報管理（`Declarations`）
- 物流商管理（`Logistics` CRUD）

---

## 商品管理（ProductMsController）

### ✅ 已完成
- 商品清單（分頁、篩選分類/品牌/停用/關鍵字）
- 商品 CRUD（新增/詳情/編輯/軟刪除）
- 品牌清單（唯讀）
- 商品分類清單（唯讀）

### ⚠️ 部分完成
- **商品分類管理**：✅ 2026-06-09 完成 CRUD
- **品牌管理**：✅ 2026-06-09 完成 CRUD
- **套組商品**：`ProductFormView.vue` 是否完整支援 `isset=true` + `Setproducts` 子項結構待確認

### ❌ 缺少
- 品牌 CRUD（新增/編輯/刪除 + 品牌主圖）`POST/PUT/DELETE /admin/brands`
- 品牌相冊（`Brandphotos`）CRUD + 排序
- ~~商品標籤管理（`Tags` CRUD）~~ ✅ 2026-06-09 完成
- 商品附加相冊（`Productphotos`）CRUD + 排序
- 商品排序（`SortProducts`，手動 `sort` 欄位）

---

## 庫存管理（InventoryMsController）

### ✅ 已完成
- 倉庫清單（唯讀）
- 庫存清單（分頁，依產品）
- 新增入庫（`POST /admin/stocks`）

### ⚠️ 部分完成
- **倉庫管理**：✅ 2026-06-09 完成 CRUD
- **入庫類型分流**：未區分申報貨（stocktype=1）與免申報貨（stocktype=2）

### ❌ 缺少
- ~~倉庫 CRUD（`POST/PUT/DELETE /admin/warehouses`）~~ ✅ 2026-06-09 完成
- ~~倉庫庫存調撥（`AddWarehousestocks`：FIFO 移轉，一倉移出 → 另一倉）~~ ✅ 2026-06-09 完成（UI 已整合）
- 庫存批次紀錄編輯（`EditStocks`/`EditNoStocks`：效期/條碼/數量修正）

---

## 採購管理（PurchaseMsController）

### ✅ 已完成
- 供應商清單
- 新增供應商
- 編輯供應商（API 存在）
- 採購單清單（分頁）
- 新增採購單（含明細）
- 採購單詳情
- 採購單轉支出（`PATCH .../expenditure`）

### ⚠️ 部分完成
- **供應商編輯 UI**：✅ 2026-06-09 完成（行內編輯列）
- **採購單編輯**：✅ 2026-06-09 API 完成（`PUT /admin/purchases/:id`），UI 透過既有 PurchaseFormView 處理

### ❌ 缺少
- 供應商刪除（`DELETE /admin/suppliers/:id`）
- 採購單編輯 API + UI（`PUT /admin/purchases/:id`）
- 匯率管理（`Exchanges` CRUD）

---

## 財務管理（AccountingMsController）

### ✅ 已完成
- 支出單清單（應付帳款，含明細展開）
- 新增付款記錄（Outcome）
- 應收帳款清單（AR Invoice）+ 新增
- 收款記錄清單 + 新增
- 退款處理（`POST /admin/refounds`）

### ⚠️ 部分完成
- **支出單手動新增**：API 存在，UI 的明細（`Expendituredetails`）輸入是否完整待確認
- **退款流程**：是否同步更新訂單 `paystatus=退款` 待確認

### ❌ 缺少
- ~~支出單刪除（`DELETE /admin/expenditures/:id`）~~ ✅ 2026-06-09 完成
- 支出單編輯（`PUT /admin/expenditures/:id`）
- ~~付款記錄刪除（`DELETE /admin/outcomes/:id`）~~ ✅ 2026-06-09 完成
- 付款記錄編輯（`PUT /admin/outcomes/:id`）
- ~~應收帳款刪除（`DELETE /admin/ar-invoices/:id`）~~ ✅ 2026-06-09 完成
- 應收帳款編輯（`PUT /admin/ar-invoices/:id`）
- ~~收款記錄刪除（`DELETE /admin/incomes/:id`）~~ ✅ 2026-06-09 完成
- 收款記錄編輯（`PUT /admin/incomes/:id`）
- 退款記錄編輯/刪除
- 會計科目管理（`Accountings` CRUD）
- 匯率管理（AccountingMs 部分）
- 損益表（`Incomestatements`，財務合規需求）

---

## CMS 內容管理（HomeMsController）

### ✅ 已完成
- Banner CRUD
- 最新消息（News）CRUD
- 食譜（Recipes）CRUD
- 期刊（Issues）CRUD
- 活動（Events）CRUD
- 知識庫 / FAQ（Knowledges）CRUD

### ⚠️ 部分完成
- **食譜子集合**：食材（`Recipeingredients`）/ 調料（`Recipeseasonings`）/ 步驟（`Recipesteps`）輸入是否完整實作待確認
- **期刊關聯**：關聯食譜（`recipeids`）/ 關聯商品（`productids`）多選是否完整待確認
- **活動相冊**：`/admin/cms/events/:id/photos` 路由缺失

### ❌ 缺少
- 活動相冊 CRUD（`Eventphotos`）+ 排序
- 各內容類型排序 API（Banner/Recipe/Issue/Event/Knowledge `PATCH .../sort`）
- 部落格連結管理（`Blogs` CRUD + 排序）

---

## 會員管理（MemberMsController）

### ✅ 已完成
- 會員清單（分頁、關鍵字搜尋）
- 會員詳情（inline 展開）
- 會員編輯（API 存在）
- 會員停用

### ⚠️ 部分完成
- **會員編輯 UI**：`PUT /admin/members/:id` 存在，但 `MembersView.vue` 的展開面板是否有 form 元素待確認
- **搜尋範圍**：目前僅通用 `keyword`，舊系統可指定 `name`/`mobile` 欄位

### ❌ 缺少
- 後台主動新增會員（`POST /admin/members`）
- 簡訊群發管理（`Sms`/`Smsdetails` CRUD）

---

## 報表管理（ReportMsController）

### ✅ 已完成
- 商品銷量報表（年/月）
- 訂單金額報表（日期範圍 + 付款狀態）

### ⚠️ 部分完成
- **套組展開**：銷量報表是否展開套組子品項計算待確認

### ❌ 缺少
- 報表匯出 Excel（已出貨清單 xls）

---

## 系統設定（SettingMsController）

### ✅ 已完成
- 折扣碼管理完整 CRUD

### ⚠️ 部分完成
- **管理員帳號 + 權限**：API 完整，UI 的樹狀 RBAC（`IsAdd/IsUpdate/IsDelete` 三維度）是否完整待確認

### ❌ 缺少
- 問題分類管理（`Questiontypes` CRUD + 排序）
- FAQ 問答管理（`Questions` CRUD，與 `Knowledges` 是不同實體）

---

## 實作優先順序建議

### 🔴 Phase 1 — 設計規範補齊（本 sprint）
1. ✅ CSS 變數 `:root` 定義（2026-06-09 已修正）
2. ✅ `ProductFormView` 重複 padding 修正（2026-06-09）
3. ✅ `MembersView` 補編輯入口 + side panel（2026-06-09）
4. ✅ `DiscountsView` 按鈕色改 CSS var，SVG inline style（2026-06-09）
5. ✅ 出貨操作改為 Modal（2026-06-09，替換 `prompt()`）

### 🟠 Phase 2 — 核心業務補齊（下 sprint）
6. ✅ 品牌管理完整 CRUD（2026-06-09）— `POST/PUT/DELETE /admin/brands` + `BrandsView.vue`
7. ✅ 商品分類 CRUD（2026-06-09）— `POST/PUT/DELETE /admin/producttypes` + `ProductTypesView.vue`
8. ✅ 商品標籤管理（2026-06-09）— Tags CRUD + `TagsView.vue`
9. ✅ 倉庫 CRUD（2026-06-09）— `POST/PUT/DELETE /admin/warehouses` + `WarehousesView.vue`
10. ✅ 倉庫庫存調撥（2026-06-09）— `POST /admin/warehouse-transfer` + InventoryView 調撥 Modal
11. ✅ 採購單編輯 API（2026-06-09）— `PUT /admin/purchases/:id`（diff 比對明細）
12. ✅ 供應商編輯/刪除 UI（2026-06-09）— 行內編輯列 + 刪除確認；`DELETE /admin/suppliers/:id` API

### 🟡 Phase 3 — 財務 CRUD 補齊
12. ✅ 支出單 / 付款記錄 / 應收 / 收款 Delete API + UI（2026-06-09）
13. 會計科目管理

### 🟢 Phase 4 — 匯出與輔助功能
14. 訂單 / 出貨 Excel 匯出
15. 撿貨單匯出
16. 報表 Excel 匯出
17. 物流商管理
18. 簡訊群發

### ⚪ Phase 5 — 低頻功能
19. 部落格連結管理
20. FAQ 問答管理（Questions 實體）
21. 申報管理
22. 損益表
23. 匯率管理

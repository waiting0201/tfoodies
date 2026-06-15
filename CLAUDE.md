# CLAUDE.md — TFoodies 食在呼（檢索索引）

> 本檔為 **harness/context engineering 檢索索引**：摘要關鍵事實並把代理導向 `docs/` 中的細節文件。深入某主題前，先讀本檔對應列，再開該文件。
> 分析對象為 **舊系統**，原始碼位於 `reference/old/`（唯讀參考）。所有文件路徑相對於 `reference/old/`。

## 這是什麼系統
「食在呼 TFoodies」食品電商**前台 + 後台 ERP**。
技術棧：**ASP.NET MVC 5 / Web API 2 (.NET Framework 4.8)** · **EF6 Database-First** (`tfoodiesModel.edmx`) · **SQL Server (`tfoodies`)** · Azure Blob 圖檔 · MongoDB(瀏覽紀錄) · ezPay/NewebPay 金流+電子發票 · 三竹簡訊 · reCAPTCHA。

6 個專案：`tfoodies`(前台 MVC) · `tfoodiesBackend`(後台 MVC) · `tfoodiesApi`(Web API) · `tfoodies.Models`(EF6 資料層) · `tfoodies.Service`(~70 服務) · `tfoodies.Libs`(工具+部分業務邏輯)。

## 文件導覽（依任務檢索）

| 我要做的事 | 讀這個 | 負責 Agent |
|---|---|---|
| 了解整體架構、分層、相依、設定檔、共用工具、驗證機制、外部整合總覽 | [docs/01-architecture.md](docs/01-architecture.md) | `system-analyst` |
| 查資料表/欄位/關聯、ER 模型、DTO | [docs/02-domain-model.md](docs/02-domain-model.md) | `system-analyst` |
| 改前台顧客功能（商城/購物車/結帳/會員/預購/CMS 頁） | [docs/03-frontend-site.md](docs/03-frontend-site.md) | `frontend-architect` · `visual-design-architect` |
| 改後台管理（商品/訂單/庫存/採購/財務/報表/會員/設定/權限） | [docs/04-backend-admin.md](docs/04-backend-admin.md) | `backend-engineer` · `frontend-architect` |
| 串 API 或查服務層業務邏輯 | [docs/05-api-and-services.md](docs/05-api-and-services.md) | `backend-engineer` |
| 查狀態機列舉、端到端業務流程、整合、技術債/安全 | [docs/06-cross-cutting.md](docs/06-cross-cutting.md) | `backend-engineer` |
| 實作 API 端點（Controller/Service/DI/路由/Response/Middleware 模式） | [docs/07-api-pattern.md](docs/07-api-pattern.md) | `backend-engineer` |
| 了解**新系統**多專案分層（Domain/Application/Infrastructure/Contracts/Api.Functions 各做什麼、程式碼放哪裡） | [docs/08-new-architecture.md](docs/08-new-architecture.md) | `software-architect-blueprint` · `system-analyst` |
| 撰寫或審查測試（xUnit Domain/Application/Infrastructure、Playwright 視覺回歸） | [docs/09-testing.md](docs/09-testing.md) | `qa-test-engineer` · `backend-engineer` · `frontend-architect` |
| 後台 Vue 元件設計規範（清單、表單、按鈕、Badge、CSS 變數定義） | [docs/10-admin-ui-design.md](docs/10-admin-ui-design.md) | `frontend-architect` · `visual-design-architect` |
| 部署前台 store（Container Apps SSR：建 ACR/環境、首次部署映像、自訂網域切換、退役舊 SWA） | [docs/11-store-deployment.md](docs/11-store-deployment.md) | `backend-engineer` |
| 設定金流（FISC）/電子發票（ezPay）參數（四層同名對照、回呼網址導出、WEBPOS 欄位/成功判定/錯誤碼對照，依手冊 v2.7） | [docs/12-payment-invoice-config.md](docs/12-payment-invoice-config.md) | `backend-engineer` |
| 了解前後台刷卡 + 電子發票**串接流程**（MarkPaidAsync/IssueInvoiceAsync 共用核心、store/admin create→return 流程、標記已付款/補開發票、冪等） | [docs/13-payment-invoice-flow.md](docs/13-payment-invoice-flow.md) | `backend-engineer` |

## 速查關鍵事實

**分層相依**：`Models ← Service ← Libs ← {前台, 後台, API}`。⚠️ `Libs.Librarys` 含業務邏輯（單號、庫存扣減），非純工具。

**資料模型**：72 個 `DbSet`（`tfoodies.Models/tfoodiesModel.Context.cs`），PK 多為 `Guid`。核心鏈：`Purchases→Purchasedetails→Stocks→Warehousestocks→Orderdetailstocks→Orderdetails→Orders`。庫存**依效期 FIFO 揀貨**（`WarehouseStocksService.GetStockWarehouses`）。

**模組命名陷阱（極易誤解）**：
- 前台 `GroupMs` = 團購/預購下單表單；`PreMs` = 上線前啟動頁；`IncomeMs` = 銀行對帳排程。
- 後台 `HomeMs` = 前台內容 CMS（非儀表板，儀表板是 `Main/Index`）；「Ms」= Management System。

**驗證**：無 ASP.NET Identity。前台會員 Session + DES `tfd` 記住我 cookie；後台 Session + `Lims`/`AdminLims` 樹狀 RBAC（`CheckSessionAttribute`）；**API 無驗證**。

**服務層**：薄 CRUD 包裝；真正交易編排在 `tfoodiesBackend` 控制器（用共享 DbContext 建構子 = 一次 `SaveChanges` 工作單元）。寫入回傳 `IResult`（⚠️ 例外被吞）。

**稅務**：台灣 5% VAT，`tax = price - round(price/1.05)`（`OrdersService.GetInvoicedetailsByMemberID`）。運費滿 2000 免運。

## ⚠️ 移轉/修改前必知（詳見 docs/06）
- 機密全明文寫死（SQL sa 密碼、Azure key、金流 HashKey/IV、簡訊/SMTP 帳密、cookie DES key）。
- 會員/管理員密碼**明文**；後門帳號 `itadmin/QQQQQ`(AdminID 888) 繞過權限。
- 單號產生器與 ATM 碼有 race condition；`SendMail` 失敗無限遞迴；`customErrors=Off`+`debug=true`。
- `BaseService` 為死碼、CRUD 重複；`Refounds` 拼字錯誤已固化於 schema。

## 慣例
- **Claude 一律以繁體中文回覆**（對話與說明文字）。
- `reference/old/` 為**唯讀**舊系統參考，請勿修改。
- 新文件以繁體中文撰寫，置於 `docs/`，並在本表登錄。
- 引用程式碼用 `檔案:行號`（相對 `reference/old/`）。

## 文件更新規則

> **任何以下情況發生時，必須同步更新對應的 `docs/` 文件（以及本表如有新增）：**

| 觸發情境 | 要更新的內容 |
|---|---|
| **功能新增** | 受影響模組的文件（架構/API/前台/後台等）、Agent 分工表 |
| **功能修改** | 對應文件中的行為說明、欄位、流程、範例 |
| **錯誤修復** | 若修復揭露了文件中的錯誤描述，一併修正；重大 bug 可在 docs/06-cross-cutting.md 技術債區段備註 |
| **功能完成（移轉完畢）** | 更新 docs/08-new-architecture.md 的完成狀態；移除舊系統對應的 ⚠️ 警示（如已解決） |

不需要為每次小改動單獨建新文件，更新現有文件的對應段落即可。

# 01 · 架構與基礎建設 (Architecture & Infrastructure)

> 舊系統技術盤點。所有路徑相對於 `reference/old/`。

## 系統概觀

「食在呼 TFoodies」是一套 **食品電商前台 + 後台 ERP** 系統，技術棧為 **ASP.NET MVC 5 / Web API 2 (.NET Framework 4.8)**，資料層為 **EF6 Database-First (`tfoodiesModel.edmx`)**，資料庫為 **SQL Server (`tfoodies`)**。共 6 個專案，全部 `v4.8`、皆為 `Library` 輸出（三個 Web App 由 IIS 託管）。

## 專案分層與相依

```
tfoodies.Models   ← EF6 DB-first 資料層 + GenericRepository + POCO 實體 + DTO(Objects)
      ▲
tfoodies.Service  ← 業務服務層（~70 個 *Service，僅參考 Models）
      ▲
tfoodies.Libs     ← 共用工具 + 部分業務邏輯（參考 Models + Service）
      ▲
┌─────┴───────────────┬──────────────────────┐
tfoodies            tfoodiesBackend         tfoodiesApi
(前台 MVC5)          (後台 MVC5)             (Web API 2)
```

- 三個 Web 專案皆參考 `Libs` + `Models` + `Service`（見各 `.csproj` 的 `ProjectReference`）。
- `tfoodies.Libs` → 參考 `Models` + `Service`；`tfoodies.Service` → 僅參考 `Models`；`tfoodies.Models` → 葉節點。
- ⚠️ **`Libs` 不是純工具層**：`Librarys.cs` 直接 `new OrderCodesService()` 等並含庫存扣減、單號產生等業務邏輯。

## 技術棧 / 主要 NuGet（各 `packages.config`）

共用：**EntityFramework 6.4.4**、**Newtonsoft.Json 13.0.3**。

| 專案 | 關鍵套件 |
|---|---|
| tfoodies (前台) | MVC 5.3.0、Razor 3.3、Web.Optimization、jQuery 3.1.1 + Validation、**MiniProfiler 4.3.8 (+EF6/MVC4)**、**MongoDB.Driver 2.3.0**、Azure.DocumentDB 2.18（已停用/註解）、PagedList(.Mvc)、UAParser 3.1.47 |
| tfoodiesBackend (後台) | MVC 5.3.0、**WindowsAzure.Storage 8.3.0**(Blob)、**NPOI 2.6.2**(Excel)、**SixLabors.ImageSharp 2.1.4** + `System.Drawing`(兩套影像庫並存)、**HtmlAgilityPack 1.11.59**、**ini-parser 2.5.2**(解析三竹簡訊回應)、Enums.NET |
| tfoodiesApi | AspNet.WebApi 5.3.0、Json.Bson（極薄唯讀 API） |
| Models / Service / Libs | EF6 + Json（Libs 另引 MVC/Razor 供 `ViewExtensions`/reCaptcha） |

⚠️ 大量 .NET 4.5.1 時代的 `System.*` 4.3 NuGet shim + 眾多 `bindingRedirect` — 升級遷移痛點。

## 設定檔 (Web.config / App.config)

### 連線字串（前台與後台 Web.config 相同）
EF6 EDMX 連線 `tfoodiesEntities`：`data source=(local);initial catalog=tfoodies;user id=sa;password=twvsjp0205;MultipleActiveResultSets=True`。
⚠️ **明文 `sa` 密碼**。`tfoodiesApi/Web.config` **未定義連線字串**（依賴繼承/預設工廠）。

### 前台 appSettings（`tfoodies/Web.config:14-28`）
| Key | 用途 |
|---|---|
| `azure.blob.connectionstring` / `.container=tfoodies` / `.url` | Azure Blob 圖檔儲存（key 明文，**機密**） |
| `mongodb.isrecord` (false) / `mongodb.url` | 切換瀏覽紀錄寫 Mongo 或 SQL |
| `freightset` (180) / `freightlimit` (2000) | 運費 / 免運門檻（滿 2000 免運） |
| `paylimit` (3) | ATM 付款天數限制 |
| `websitetitle` | 注入 `ViewBag.WebsiteTitle` |

### 後台 appSettings（`tfoodiesBackend/Web.config`）
同上 Azure Blob，另含 **ezPay (NewebPay 智付寶) 電子發票/金流**：`MerchantID=32760529`、`HashKey`、`HashIV`（皆**機密**）、`InvCreateUrl`/`InvCancelUrl`（cinv.ezpay.com.tw）、`config:CurrentTheme`（SmartAdmin 佈景）。後台放大上傳限制 `maxRequestLength=1024000`、session timeout 60 分（前台 20 分）。

⚠️ `tfoodies.Service/App.config` 用了**不同的 Azure 帳號** `weyprostorage` 且 `freightset=100` — 三處設定不一致。所有 Web.config 皆 `customErrors=Off` + `debug=true`（不適合正式環境）。

## 共用工具 `tfoodies.Libs`

- **`Enum.cs`** — 17 個中文命名領域列舉，對應 DB 整數欄位（付款/出貨/倉別/折扣/發票狀態機）。詳見 [06-cross-cutting.md](06-cross-cutting.md#列舉狀態機-enumcs)。
- **`Librarys.cs`** (943 行，全 `static`，真正主力)：
  - 單號產生器 `NewOrderCode/NewPurchaseCode/NewReturnCode/NewExpenditureCode/NewOutcomeCode/NewIncomeCode/NewRefoundCode/NewInvoiceCode` — 每日計數器讀取+遞增，存 Session。⚠️ **非原子，有 race condition**。
  - 庫存引擎 `CheckInventory/SetAdded/SetInventory/ReturnInventory`（FIFO 倉庫扣減；退貨倉 GUID `06CDFDD5-…E17A` 寫死）。
  - `GetAtmCode`/`GetCheckCode` — 國泰 ATM 虛擬帳號 + 檢查碼。
  - `SendMail` — Sendinblue SMTP（**帳密寫死** tim@weypro.com）；固定 BCC；⚠️ **失敗時無限遞迴重試**。
  - reCAPTCHA v3 key（寫死）、台灣 GCIS 公司登記查詢、`ConvertUTF8toBIG5`。
- **`GoogleReCaptcha.cs`** — reCAPTCHA v2 驗證（secret 寫死）。
- **`StringExtensions.cs`** — `UnDash()`(供 DashRouteHandler)、`ToStrSplit`。
- **`ViewExtensions.cs`** — `RenderToString(PartialViewResult)`（組 email/SMS HTML 內容）。

## 跨切面 MVC 基礎建設

### 路由
- **前台 `RouteConfig.cs`**：大量 SEO 美化路由全指向 `MainMs`（`/Products`、`/Product/{title}`、`/Brand/{title}`、`/News`、`/Recipes`、`/Issues`、`/Blogs`、`/TFoodies`→About、`/Login`），預設控制器 `MainMs/Index`。Slug 慣例：標題存 `/`，URL 用 `-`，控制器 `.Replace("-","/")`。
- **後台 `RouteConfig.cs`**：僅 Default 路由，預設控制器 `Main`。另有 `DashRouteHandler`（去除 `-`）。
- **API `WebApiConfig.cs`**：`MapHttpAttributeRoutes()` + `api/{controller}/{id}`。

### 自訂 Filter（`ActionFilterAttribute`）
- 前台 `CheckSessionAttribute` — 登入閘門（`Session["IsLogin"]` 否則導 `MainMs/Login`）。
- 後台 `CheckSessionAttribute` — 登入 + **細粒度權限檢查**（見 [04-backend-admin.md](04-backend-admin.md#權限與驗證模型)）。
- 前台 `CheckShopComAttribute` — 擷取聯盟行銷 `RID`/`Click_ID` 至 Session。
- 前台 `CheckShoppingCartItemAttribute` — 購物車空則導回 `Products`。

### Base Controller
- 前台 `BaseController` — `OnActionExecuted` 重新驗證 `tfd` 記住我 cookie、填入 `ViewBag`（BlobUrl、WebsiteTitle、購物車、品牌）。
- 後台 `BaseController` — 載入 `Lims` 選單至 `ViewBag.SiteLinks` + BlobUrl。
- API `BaseController` — 空殼 `: ApiController`。

## Repository / Service / Result 模式

- **`GenericRepository<TEntity>`** (`tfoodies.Models/Repository/`)：包一個 `tfoodiesEntities` + `DbSet`。預設建構子自建 context，另有接收共享 context 的多載（實現窮人版 UnitOfWork）。方法：`Get()`(回傳 `IQueryable`)、`GetByID`(`Find`)、`Insert/Update/Delete`、`SpecificUpdate(entity, props[])`(僅標記指定欄位 `IsModified`)、`SaveChanges`。
- **`BaseService<TEntity>`** (`tfoodies.Service/`)：⚠️ **形同死碼** — 具體服務並未繼承它，而是各自複製貼上相同 CRUD。
- **`Result` / `IResult`**：寫入操作的統一回傳 `{ Guid ID; bool Success; string Message; Exception Exception; List<IResult> InnerResults }`。⚠️ **例外被吞進 `result.Exception` 而非拋出**，多數呼叫端忽略。
- **無真正 UnitOfWork**：每個自建 context 的服務是獨立工作單元；跨服務一致性靠手動傳入共享 `tfoodiesEntities`（後台用 `new XxxService(db)` 共享）。

## 驗證 / Session 機制（無 ASP.NET Identity / FormsAuth）

- **前台會員**：`MembersService.ValidateUser` 比對 `mobile` + **明文 password**，設 `Session["IsLogin"/"MemberID"/"Username"]`。記住我用 **DES 加密 `tfd` cookie**（key `16816888`/IV `88888888` 寫死，存 mobile+password），每次請求重新驗證。
- **後台管理員**：`MainController.ValidateUser` 有 **寫死超級管理員** `itadmin/QQQQQ` → `AdminID=888`（繞過所有權限）；否則比對 `Admins.Username` + 明文 Password。登入受 reCAPTCHA 保護。
- **API**：**完全無驗證**。

## 外部整合

- **Azure Blob** — `tfoodiesBackend/Commons/AzureBlob.cs`（上傳/刪除，每次設公開存取）；上傳前經 `ImageResize.cs`/`Watermark.cs`(System.Drawing)。
- **簡訊（三竹 Mitake）** — `tfoodiesBackend/Controllers/AjaxController.cs:822`，POST Big5 INI 至 `smexpress.mitake.com.tw:9600`（**帳密寫死**），ini-parser 解析回應。
- **reCAPTCHA** — v2(`Libs`) + v3（後台登入、前台聯絡）。
- **瀏覽紀錄** — `RecordLog`：依 `mongodb.isrecord` 寫 MongoDB 或 SQL `Viewlogs`；含 geoplugin 地理 + UAParser。
- **金流/電子發票（ezPay/NewebPay）** — AES-256(`General.cs`) 加密 TradeInfo；`AjaxController.CreateInv/CancelInv` 開立/作廢發票；信用卡結果於 `MainController.CardResult`/前台 `ShoppingSuccess` 處理。
- **ATM 虛擬帳號** — 本地產生（`Libs.GetAtmCode`，國泰格式）。
- **Email** — Sendinblue SMTP relay。
- **台灣 GCIS 公司登記** — 開放資料查詢。

## ⚠️ 主要技術債（移轉重點）
1. **機密散落於原始碼與設定檔**：SQL sa 密碼、Azure key、簡訊/SMTP 帳密、reCAPTCHA secret、ezPay HashKey/IV、DES cookie key/IV — 全明文。
2. **會員/管理員密碼明文儲存與比對**；寫死後門 `itadmin/QQQQQ` (AdminID 888) 繞過 RBAC。
3. `BaseService` 死碼、CRUD 大量重複；`Result.Exception` 靜默吞錯。
4. 無真正 UnitOfWork；單號產生器/ATM 碼有 race condition。
5. `SendMail` 失敗無限遞迴；字串去尾綴式 RBAC 脆弱；`customErrors=Off`+`debug=true`；in-proc session（非 web farm 安全）；API 零驗證。
6. 雙影像庫（System.Drawing + ImageSharp）；DocumentDB 已引入但停用改用 MongoDB；三處 blob/運費設定分歧。

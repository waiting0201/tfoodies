# 03 · 前台網站 (Frontend Site — `tfoodies/`)

> 顧客端 ASP.NET MVC 5 (Razor) 商城。EF6 + `*Service`、Azure Blob 圖檔(`ViewBag.BlobUrl`)、Session 購物車、`tfd` 記住我 cookie、reCAPTCHA v3 + 自製 GDI 圖形驗證、MiniProfiler。
> 路徑相對於 `reference/old/tfoodies/`。

## ⚠️ 命名陷阱（務必先讀）
- **`GroupMs`** = 實際的**團購/預購下單表單**（寫入 `Preorders`），不是獨立的團購系統。
- **`PreMs`** = **上線前 coming-soon 啟動頁**，不是顧客預購。
- **`IncomeMs`** = **銀行對帳/入帳排程**（非顧客頁），比對 ATM 入帳與訂單。

## Controllers

### `BaseController`（多數控制器之基底，非路由）
- `OnActionExecuting`：設 `ViewBag.BlobUrl`。
- `OnActionExecuted`：重新驗證 `tfd` cookie（DES 解密 mobile+password → `ValidateUser`）；填入 `ViewBag`：BlobUrl、WebsiteTitle、CartContents、CartItems、AddActive、Brands。含已註解的「上線前導向 PreMs」邏輯。

### `MainMsController`（主商城，繼承 BaseController）
多數 GET，設 SEO ViewBag。
| Action | 參數 | 用途 |
|---|---|---|
| `Index` `[CheckShopCom]` | — | 首頁：banners、熱門商品(8)、3 食譜、9 隨機新品、3 特集、2 消息、1 活動 |
| `Products` `[CheckShopCom]` | producttypetitle, brandid | 商品列表（依分類+選用品牌篩選） |
| `ProductDetail` `[CheckShopCom]` | producttitle | 商品詳情；解析套裝子商品；查無導回 Products |
| `Brand` | brandtitle | 品牌頁，前 4 商品 |
| `News`/`NewsDetail`, `Events`/`EventsDetail`, `Recipes`/`RecipeDetail`, `Issues`/`IssueDetail`(綠誌), `Knowledges`/`KnowledgeDetail`(小知識) | 列表 p / 詳情 | 內容頁（PagedList，部分支援關鍵字 k） |
| `Blogs`, `Reports`, `GreenIssues`, `About` | — | 部落客合作 / 檢驗報告 / 綠誌 / 關於 |
| `Login` | — | 登入頁（購物車非空則登入後轉結帳） |
| `Forget` | — | 忘記密碼 |
| `ShoppingCart` `[CheckShoppingCartItem]` | — | 購物車檢視；算 TotalPrice/Discount/Freight/Amount（`General`） |
| `ShoppingProfile` `[CheckShoppingCartItem]` | — | 結帳第 2 步（訂購/收件資訊），登入者預填 |
| `ShoppingSuccess` | lidm(ordercode), lastPan4, status | **訂單完成 / 金流回呼**：建立 Invoices+明細(5% 稅拆分)；信用卡 `status==0` 標記已付款+建立 Incomes+連結發票；寄訂單信(含 ATM)；登入會員；清 Session 購物車 |

### `MemberMsController`（會員中心，類別級 `[CheckSession]`）
`Orders`(訂單歷史)、`OrderDetail`、`Mylists`(收藏)、`EditProfile`、`EditPassword`、`PayResult`(lidm,status 金流回呼設付款狀態後導向 OrderDetail)、`Logout`(清 Session + 過期 tfd cookie)。

### `PageMsController`（CMS/靜態頁，繼承 BaseController）
`Contact`、`Terms`、`Policy`、`Disclaimer`、`Howtobuy(questiontypeid?)`（購物 FAQ，依 Questiontypes→Questions 分組）。

### `AjaxController`（JSON/部分檢視端點，繼承 BaseController）
含 `IsCaptchaValid`(reCAPTCHA v3，action 須 `contact_us`、score>0.5)。
| Action | 用途 |
|---|---|
| `GetZipcodeByCity` | 回傳城市區域 `<option>` |
| `AddToCart`/`RemoveToCart`/`UpdateToCart` | Session 購物車增刪改（檢查 `product.added` 庫存）；回傳 `_PartialCartItem` 或重算金額 |
| `Login`(async) | reCAPTCHA + ValidateUser；記住我設 `tfd` cookie(3 個月) |
| `PostOutofnotice` | 補貨通知（**GDI 圖形驗證** vs `Session["ValidateCode"]`） |
| **`PostOrder`**(async) | **主結帳送出**：reCAPTCHA；找/建會員(訪客自動註冊 ismember=1)；由購物車建/更新 Orders+Orderdetails；ATM 碼 `Librarys.GetAtmCode`；套用折扣；可用 `Session["orderCode"]` 重送。回 `{code, ordercode, paytype}` |
| **`PostPreorder`** | **預購送出**（GDI 驗證）：建 `Preorders`（明細程式碼**已註解**，目前僅主檔）。由 GroupMs 頁呼叫 |
| `EditProfile`/`EditPassword` `[CheckSession]` | 更新會員 |
| `PasswordSend`(async) | reCAPTCHA；產生隨機 6 碼密碼存檔並寄信 |
| `Checkmobile` | 遠端驗證手機未註冊 |
| `CheckCompanynumber`/`GetCompanyTitle`/`GetSubCompanyTitle` | 統編公司名查詢(`Librarys`) |
| `AddToMylist`/`RemoveToMylist` | 收藏增刪 |
| `RecordLog` | 訪客分析 → MongoDB 或 SQL（geoplugin + UAParser + `_wa` cookie） |
| `GetBrandMoreProducts` | 品牌頁無限捲動 → `_PartialProductList` 字串 |
| `GetDiscountCode` | 存 `Session["DiscountCode"]`，回 `General.GetDiscount()` + 重算金額 |

### `CaptchaController`（純 Controller）
`VerificationCode()` — GDI+ 畫 4 位數字 GIF，明文存 `Session["ValidateCode"]`，每分鐘重生。用於 `PostOutofnotice`/`PostPreorder`（reCAPTCHA 流程則用 Google）。

### `GroupMsController`（純 Controller）
`Index()` — 團購/預購表單，載 `isgroupbuy==true` 商品，View 送至 `Ajax/PostPreorder`。⚠️ `Views/GroupMs/Profile.cshtml` 無對應 action（孤兒/遺留）。

### `PreMsController`（純 Controller）
`Index()` — 靜態 coming-soon 啟動頁。

### `IncomeMsController`（純 Controller）
`Index()`(async) — **後台銀行對帳**：POST 至 `globalmyb2b.com` 證券 API（日期寫死 `20190325`），解析 XML(`TX10D0`)，依 ATM 碼(`codeatm`)+金額比對訂單，標記已付款、建 Incomes、連結 Invoices、記 `GlobalMyB2B`。

## 顧客功能地圖
- **瀏覽**：首頁、Products(`/Products`,`/Products/{type}`)、ProductDetail(`/Product/{title}`)、Brand(`/Brand/{title}`)+無限捲動。
- **內容行銷**：News / Events / Recipes / Issues(綠誌) / Knowledges(小知識/FAQ) / Blogs / Reports / GreenIssues / About。
  - 🆕 新 store 四個詳細頁（`NewsDetail`/`EventsDetail`/`IssueDetail`/`KnowledgeDetail`）已重新設計成秀氣整齊的卡片式版面（teal 設計語彙，與 `RecipeDetail` 一致），功能對齊舊系統：麵包屑、標題、日期/活動 chips、分享、內文、其他文章側欄；綠誌另含相關商品/食譜；活動花絮改為相片牆＋原生燈箱（取代 magnificPopup）。共用樣式置於 `web/store/app/assets/css/article-detail.css`（命名空間 `.article-detail`/`.events-detail`），共用元件 `ArticleShare.vue`(FB/LINE/複製)、`ArticleAside.vue`(其他文章側欄)。
  - 🆕 **內容帶貨橋（新 store）**：食譜詳情頁 `pages/Recipe/[recipeid]/[[p]].vue` 的「購買相關商品」區塊新增 **「🛒 一鍵把 N 項商品加入購物車」** 按鈕（`addAllToCart()` 把 `item.products`(排除 `isdisabled`) 逐一 `cart.add()`，順帶觸發 `add_to_cart` 追蹤），解決「看完食譜想煮卻找不到食材在哪買、看完就走」的流失。相關商品資料 API(`/store/recipes/detail`) 早已回傳 `products`，原本僅以 `ProductCard`(只連商品頁)呈現。**綠誌(Issue)詳情頁 `pages/Issue/[issuetitle]/[[p]].vue` 已比照加入同一顆按鈕**(用 `sortedProducts`、同樣 `cart.add()`+toast)。
- **購物車**：Session `Session["myCart"]` = `List<CartItem>`（見 `Commons/Cart.cs`）；增刪改走 Ajax；mini-cart 於 `_Header`/`_PartialCartItem`；庫存以 `Products.added` 把關。
- **結帳流程**：`ShoppingCart`(檢視+折扣碼→`GetDiscountCode`) → `ShoppingProfile`(訂購/收件；手機唯一性`Checkmobile`；統編查詢；郵遞`GetZipcodeByCity`) → 送 **`Ajax/PostOrder`** → 成功 JS 導向 `ShoppingSuccess?lidm={ordercode}`。訪客自動建會員。信用卡由金流回呼 `ShoppingSuccess`/`MemberMs/PayResult`。定價於 `Commons/General.cs`：`GetFreight`(滿 2000 免運)、`GetDiscount`(折扣%/折價固定、效期/一次性)、`GetAmountPrice`。
- **會員/帳戶**：結帳隱式註冊；登入 `Login`→`Ajax/Login`(reCAPTCHA+記住我)；忘記密碼 `Forget`→`Ajax/PasswordSend`。會員中心 `MemberMs/*`。Session key：`IsLogin`、`MemberID`、`Username`。
- **預購/團購**：`GroupMs/Index` → `Ajax/PostPreorder` → `Preorders`（明細未實作）。
- **驗證碼**：reCAPTCHA v3（Login/PostOrder/PasswordSend）；GDI 圖形(`Captcha/VerificationCode`)（補貨通知/預購）。
- **聯盟追蹤**：`[CheckShopCom]` 存 `RID`/`Click_ID` 至 Session，`PostOrder` 寫入 Orders。
- 🆕 **數位追蹤/電商事件（新 store）**：經 GTM 容器分流給 GA4 / Meta Pixel / Google Ads。容器以 `runtimeConfig.public.gtmId`（`NUXT_PUBLIC_GTM_ID`）驅動，由 `app/plugins/analytics.ts` 以 GTM 官方標準片段在 SSR `<head>` 注入（view-source 可見、hydration 前即載入，較不漏秒跳出訪客）；事件透過 `app/utils/track.ts` 的 `track()` 推進 `dataLayer`（GA4 ecommerce 結構）。四個漏斗事件埋點：`view_item`(`pages/Product/[producttitle].vue` onMounted)、`add_to_cart`(`stores/cart.ts` `add()`，凡加入購物車必觸發)、`begin_checkout`(`pages/Checkout/index.vue` onMounted)、`purchase`(`pages/Order/Success.vue` onMounted)。`purchase` 因信用卡會跳轉外部 FISC 刷卡頁，於結帳送單成功時以 `setPendingPurchase()` 將訂單摘要(金額/品項)暫存 sessionStorage，導回完成頁再以 `takePendingPurchase()` 取出觸發；信用卡 `paid!=1`(cardFailed) 不計入營收。⚠️ 接收端(GA4/Pixel 標籤)需在 GTM 後台設定。`purchase` 另以 **Meta 轉換 API(CAPI) server 端補送**:完成頁觸發瀏覽器事件時，同時帶 `event_id`(=訂單編號)並 `$fetch('/api/meta/capi-purchase')`，由 server 路由 `server/api/meta/capi-purchase.post.ts` 雜湊 email/phone(僅 CAPI 用，不進 dataLayer)後送 Meta Graph API；兩邊相同 `event_id` → Meta 自動去重，避免被擋廣告漏單。機密 `metaPixelId`/`metaCapiToken` 走 server-only runtimeConfig(`NUXT_META_PIXEL_ID`/`NUXT_META_CAPI_TOKEN`，來源 GitHub var `META_PIXEL_ID` / secret `META_CAPI_TOKEN`)，任一為空即略過送出。

## 路由與檢視
- **`RouteConfig.cs`**：SEO 顯式路由（見 [01-architecture.md](01-architecture.md#路由)），預設 `MainMs/Index`，fallback `{controller}/{action}/{id}`。Slug：標題存 `/`、URL 用 `-`。
- **`_ViewStart`** → `Views/Shared/_Layout.cshtml`(`zh-Hant-TW`)：head meta + OG/FB、reCAPTCHA v3、GA(UA-88479607-1)、FB SDK；body `_Header`→`@RenderBody()`→`_Footer`→`_SubMenu`→`_Scripts`(+`scripts` section)→MiniProfiler。**唯一具名 section 為 `scripts`**。
- **Shared partials**：`_Header`(導覽/登入/購物車/品牌 mega-nav)、`_Footer`(社群/連結/補貨通知 popup `#checkOutofnotice`)、`_SubMenu`(行動側欄)、`_Styles`、`_Scripts`(~346 行)、`_Addthis`、`_PartialProduct(List)`、`_PartialCartItem`。
- **View 資料夾**：`MainMs/`(全商城頁)、`MemberMs/`(會員中心，Logout/PayResult 無 view)、`PageMs/`(法務/FAQ)、`GroupMs/`(Index 團購表單 `Layout=null`、Profile 孤兒)、`PreMs/`(coming-soon `Layout=null`)、`IncomeMs/`(對帳結果 dump)。

## ⚠️ 重建注意
- 購物車純 Session（未依會員持久化）。
- 「記住我」用 **DES 寫死 key/IV** 存 mobile+password 於 `tfd` cookie，每次請求重驗 — 不安全，須重新設計。
- 訪客結帳自動建會員；`PostOrder` 含重複手機調解（處理 `ismember==2` 佔位帳號）。
- 付款：ATM 虛擬帳號（國泰 013，`codeatm`，效期來自 `paylimit`）+ 信用卡（外部金流回呼 `ShoppingSuccess`/`PayResult`）。發票 5% 稅拆分。

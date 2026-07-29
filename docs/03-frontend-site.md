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
  - 🆕 **下架商品(`isdisabled=1`)前台一律不出現**：後端 `StoreQueryService` 所有商品查詢(首頁熱門/新品、商品列表、商品詳情、品牌頁/無限捲動、食譜/綠誌相關商品)均帶 `p.isdisabled = 0`；sitemap 來源(`/store/products`)亦已濾除。商品詳情頁 `pages/Product/[producttitle].vue` 若 API 回 null(下架或不存在)直接 `throw createError(404)`，不再殘留可瀏覽的空殼頁(對齊舊系統「查無導回」精神，改用正確 not-found 狀態以利搜尋引擎下架)。
  - 🆕 **商品詳情頁改版(品牌內現代化)**：`pages/Product/[producttitle].vue` 重做版面,全部樣式 scoped(`pd-*`,沿用青綠品牌色,不動 `main.css` 與其他頁)。桌機左右兩欄=圖庫 + **sticky 購買卡**(品牌眉標→標題→價格→庫存狀態→數量→加入購物車/收藏→信任徽章),下方單欄=商品介紹(條列)、行銷圖文(`memo`,`:deep(img){max-width:100%}` 壓住後台寫死寬度)、適合料理卡片、品牌故事滿版區;手機改為底部 sticky 購買列。**圖庫改自製 Vue 元件**(固定 4:3 框 + `object-fit:contain` + 縮圖切換),取代舊 slick `.slider-for/.slider-nav`——根治混比例相簿(橫式封面 + 直式照)造成的高度跑版,此頁已不再依賴 slick。購物車/收藏/到貨通知/`view_item` 追蹤/SEO/JSON-LD 行為不變。
- **內容行銷**：News / Events / Recipes / Issues(綠誌) / Knowledges(小知識/FAQ) / Blogs / Reports / GreenIssues / About。
  - 🆕 新 store 四個詳細頁（`NewsDetail`/`EventsDetail`/`IssueDetail`/`KnowledgeDetail`）已重新設計成秀氣整齊的卡片式版面（teal 設計語彙，與 `RecipeDetail` 一致），功能對齊舊系統：麵包屑、標題、日期/活動 chips、分享、內文、其他文章側欄；綠誌另含相關商品/食譜；活動花絮改為相片牆＋原生燈箱（取代 magnificPopup）。共用樣式置於 `web/store/app/assets/css/article-detail.css`（命名空間 `.article-detail`/`.events-detail`），共用元件 `ArticleShare.vue`(FB/LINE/複製)、`ArticleAside.vue`(其他文章側欄)。
  - 🆕 **首頁 Facebook 粉專外掛（新 store）**：首頁右側的粉專 timeline 改由元件 `app/components/FacebookPage.client.vue` 以 **Facebook 官方 iframe 版外掛**(`plugins/page.php`) 渲染，取代原先靠 legacy `main.js` 注入 **已停用的 FB JS-SDK v3.1 + 全頁 xfbml 自動掃描** 的做法。`public/scripts/main.js` 內原本的 FB SDK 注入 IIFE 已一併移除（不再需要任何 SDK）。
    - **為何改 iframe**：JS-SDK 版會在「我們自己的 frame」載入 SDK 並為渲染元素註冊全域 reflow callback；Nuxt SPA 導航離開首頁後 Vue 移除該 DOM，SDK 仍持有舊 id，於 callback 觸發時在我方 frame 拋 `DataStore.get: namespace…` 等錯誤。iframe 版完全不載 SDK、無全域 callback，徹底清掉**我方 frame** 的 FB 錯誤。
    - **RWD**：FB iframe 由 facebook.com 以「載入當下指定的固定像素寬」伺服器端算好版面，純 CSS `width:100%` 只會拉寬外框、內容不重排。故元件 client-only(`.client.vue`)，以 JS 量測容器寬度夾在 FB 允許範圍 `[180, 500]` 後用該寬度載入；`requestAnimationFrame`+`window.load` 處理 main.css 非同步載入造成的初次量測過早問題，`ResizeObserver`+`resize`(debounce 200ms) 處理後續尺寸變化；外層保留 `min-height` 避免 CLS。實測桌機/平板/手機 iframe 寬度＝容器寬。
    - ⚠️ **`Could not find element "u_x_x_xx"` 是 Facebook 自家雜訊，非本站 bug**：經 Playwright 逐則歸因，此錯誤 100% 來自粉專 iframe 內部的 fbcdn 腳本(`static.xx.fbcdn.net/...`)，0 則來自 localhost。瀏覽器/devtools 會把跨來源子 frame 的 console 一併顯示，故看得到，但它**進不了本站 `window.onerror`/錯誤監控**，且只要頁面嵌任何形式的粉專外掛就會出現、無法由我方關閉。要 console 完全無此訊息只能「不嵌粉專」。
  - 🆕 **內容帶貨橋（新 store）**：食譜詳情頁 `pages/Recipe/[recipeid]/[[p]].vue` 的「購買相關商品」區塊新增 **「🛒 一鍵把 N 項商品加入購物車」** 按鈕（`addAllToCart()` 把 `item.products`(排除 `isdisabled`) 逐一 `cart.add()`，順帶觸發 `add_to_cart` 追蹤），解決「看完食譜想煮卻找不到食材在哪買、看完就走」的流失。相關商品資料 API(`/store/recipes/detail`) 早已回傳 `products`，原本僅以 `ProductCard`(只連商品頁)呈現。**綠誌(Issue)詳情頁 `pages/Issue/[issuetitle]/[[p]].vue` 已比照加入同一顆按鈕**(用 `sortedProducts`、同樣 `cart.add()`+toast)。
- **購物車**：Session `Session["myCart"]` = `List<CartItem>`（見 `Commons/Cart.cs`）；增刪改走 Ajax；mini-cart 於 `_Header`/`_PartialCartItem`；庫存以 `Products.added` 把關。
- **結帳流程**：`ShoppingCart`(檢視+折扣碼→`GetDiscountCode`) → `ShoppingProfile`(訂購/收件；手機唯一性`Checkmobile`；統編查詢；郵遞`GetZipcodeByCity`) → 送 **`Ajax/PostOrder`** → 成功 JS 導向 `ShoppingSuccess?lidm={ordercode}`。訪客自動建會員。信用卡由金流回呼 `ShoppingSuccess`/`MemberMs/PayResult`。定價於 `Commons/General.cs`：`GetFreight`(滿 2000 免運)、`GetDiscount`(折扣%/折價固定、效期/一次性)、`GetAmountPrice`。
- 🆕 **收款連結（新 store，舊系統無此功能）**：`pages/Pay/[token].vue` + `pages/Pay/Result.vue`，供**不走商城流程的臨時收款**（客訂、補款、活動費用）。後台產生連結後由人員傳給客人，客人免登入直接開啟：確認金額 → 填姓名/手機/縣市區/地址 → auto-submit 至 FISC 刷卡頁 → 導回 `/Pay/Result?code=&paid=`。
  - **不套 `layouts/default.vue`**，改用專用極簡外殼 `layouts/pay.vue`（只留 logo + SSL 提示）：這位訪客是被動收到連結、目的單一的人，`SiteHeader` 的社群列/免運跑馬燈/品牌導覽/mini-cart（此情境購物車必為空）全是流失出口；但完全裸表單又會讓人起疑，尤其常在 LINE 內建瀏覽器開啟、網址列被摺疊。
  - 四態：載入中／未付款（金額收據卡 + 表單）／已付款（保留收據卡供核對，無表單無 CTA）／已失效。**逾期、作廢、不存在合併為同一句「此連結已失效」**，不暴露內部狀態機、也不再顯示金額。
  - 地址欄位重用 `useZipcodes()` 與 `Checkout` 的縣市→區 cascade；手機另有 sticky 底部確認列（金額全程可見）。跳轉刷卡前以全頁覆蓋層 + 按鈕 disabled 兩道防線擋重複點擊。
  - **不觸發 `purchase` 追蹤**（收款連結不是電商交易，計入會污染 GA4/Meta 營收）。`/Pay/**` 已加入 `nuxt.config.ts` 的 `sitemap.exclude` 與 `robots.disallow`，頁面另帶 `noindex,nofollow`。
  - Result 頁 query 只有 `code` 沒有 `token`，失敗態的「返回重新付款」改讀付款頁在 mounted 時寄存於 `sessionStorage` 的 token（`history.back()` 會退回銀行刷卡頁並觸發表單重送警告）；讀不到則不顯示按鈕、改給客服聯絡方式。
  - 後端與流程詳見 [docs/13](13-payment-invoice-flow.md#收款連結paymentlinks--不走訂單的臨時收款)。
- 🆕 **結帳付款方式（新 store）**：選項不再寫死在 `pages/Checkout/index.vue`，改由 `GET /store/payment/methods` 提供（後端 `Helpers/StorePaymentMethods.cs` 為單一真相，同時驅動下單時的白名單驗證——舊做法完全不驗 `payType`，任何 int 都能寫進 `Orders.paytype`）。目前開放：信用卡(1)、**LINE Pay(8)**、貨到付款(2)；LINE Pay 由設定鍵 `LinePay__Enabled` 控制，關閉時前台不顯示且後端拒收。API 讀不到時退回「信用卡＋貨到付款」的 fallback，避免整頁不能結帳。送出後分支：信用卡→動態 form auto-submit 至 FISC；LINE Pay→整頁導向 `paymentUrl`；其餘→ `/Order/Success`。⚠️ **購物車一律由完成頁 `/Order/Success` 清空**，結帳頁在導向付款前不清（舊做法在 `f.submit()` 前就清空，只要顧客從刷卡頁退回或刷卡頁載入失敗，回到結帳頁就只剩「購物車是空的」，連重試都做不到；已實測修正）。付款方式標籤集中於 `app/utils/payType.ts`。流程見 [docs/13](13-payment-invoice-flow.md)。
- 🆕 **訪客結帳不設密碼（新 store，2026-07-29 改版）**：結帳頁**已移除「設定密碼／密碼確認」兩個必填欄位**（Shopify 式流程：下單不等於註冊，密碼是事後才設定的東西）。
  - **舊行為的問題**：後端 `ResolveGuestMemberAsync` 以手機號查到既有會員時會**直接沿用該會員、把送來的密碼整包丟棄**（不覆蓋、也不驗證）。老顧客在結帳頁填的那組密碼從來沒生效過，事後拿它登入必定失敗，而畫面上沒有任何提示。
  - **現行為**：訪客送出訂單時不再送 `password`；後端替新會員寫入一組隨機、無人知悉的 PBKDF2 密碼（＝尚未設定密碼）。手機號命中既有會員時行為不變（沿用該會員、不改任何既有資料），但顧客不會再被騙填一組無效密碼。
  - **顧客要登入怎麼辦**：走會員中心「[忘記密碼](../web/store/app/pages/Member/Forget.vue)」→ `POST /auth/forgot-password`，以**手機 + Email 兩者相符**核對身分後產生 6 碼新密碼寄到信箱。因此**訪客的 Email 仍是必填**（沒 Email = 永遠拿不回帳號）。
  - ⚠️ 結帳頁**刻意不放**「如何取得密碼」與「此號碼已是會員」的說明文字（2026-07-29 決策：結帳頁不談帳號的事，避免增加閱讀負擔）。改版後若客服接到「我要登入但沒有密碼」的詢問，引導至忘記密碼即可。
  - ⚠️ 後端 `PlaceOrderRequest.Password` **刻意保留**（後台代客下單等其他呼叫端仍可送），有送就用、沒送才產隨機密碼——部署順序因此不敏感。
- 🆕 **結帳頁的失敗處理（新 store，2026-07-28 修正）**：以下四點都經 Playwright 情境實測。
  - **逾時上限**：`POST /store/orders` 20 秒、發起付款 15 秒（`$fetch` 預設不逾時，後端一慢畫面會永遠停在「送出中…」，使用者只能重整而重整常變成第二筆訂單）。逾時文案明確告知「訂單可能已成立、請勿重複送出」。
  - **訂單已成立但發起付款失敗**：錯誤訊息帶出 `orderCode` 並導引至會員中心重新付款；回到結帳頁時以 `peekPendingPurchase()` 顯示「您有一筆訂單已成立、尚未完成付款」提示條，避免重複下單。
  - **錯誤訊息解析**：API 錯誤格式是 `{ error: { code, message } }`，必須讀 `e.data.error.message`（舊寫法只讀 `e.data.message`，導致庫存不足／折扣碼已使用／商品下架等具體訊息全被吃成通用的「訂單送出失敗」）。
  - **購物車對帳 `useCartSync()`**：購物車頁與結帳頁 onMounted 呼叫 `POST /store/cart/sync`，用現價/名稱覆蓋 localStorage 的舊值並標出已下架品項 —— 購物車頁顯示「價格已更新 A → B」與「已下架」badge＋一鍵移除＋鎖住「前往結帳」，結帳頁同樣提示並在送出前擋下（否則後端必拒，等於白填一整張表單）。對帳失敗不擋流程（後端仍會以現價計算並指名擋下的商品）。
  - **購物車 localStorage 防護**：`cart.hydrate()` 的 `JSON.parse` 必須包 try/catch 並過濾非法項目。這支由 `SiteHeader` 在**每一頁** onMounted 呼叫，資料一壞（舊格式、寫入被截斷、被亂改）整站每頁都會變成錯誤頁，使用者除了自清瀏覽器資料無法自救（已實測重現並修正）。
  - **完成頁清購物車要有 `code`**：`/Order/Success` 沒帶訂單編號（書籤、瀏覽紀錄、誤觸）不得清空購物車。
  - **登入態失效自動改訪客**：`/member/profile` 回 401/403 時（token 未過期但簽章/帳號已失效，`hydrate` 只驗 `exp`）必須 `logout()` 並提示。否則畫面仍是「已登入」樣式、訂購人欄位空白且 `readonly` → 要求「請填寫訂購人手機號碼」卻不能打字，訂單永遠送不出去。
  - **後端錯誤訊息要能自救**：商品下架回「『商品名』已下架或無法購買，請從購物車移除」、庫存不足回「『商品名』庫存不足，目前僅剩 N 件」（`AllocationResult.Available`）。舊訊息是「商品不存在或已下架」與「商品 {GUID} 庫存不足」，顧客無從得知該移除哪一項。
  - **前端驗證回饋**：驗證失敗時標紅欄位 + `scrollIntoView` + `focus`（錯誤訊息在右側摘要，表單很長，若在頁面上方按 Enter 送出，訊息落在畫面外，看起來像「按了沒反應」）。郵遞區號 API 載入失敗時顯示錯誤與「重新載入」按鈕（否則縣市下拉全空，顧客卡在「請選擇收件人縣市」卻無從選起）。
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

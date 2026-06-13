# 10 — 後台 UI 設計規範

> **唯一設計標準**。新增或修改任何 view/component 前必須先讀本文。  
> 有疑問時以本文為準，不以舊程式碼為準。

---

## 兩層樣式策略

| 層次 | 檔案 | 樣式方式 |
|---|---|---|
| **Layout 層** | `AdminLayout.vue`、`DashboardView.vue` | Tailwind utility class |
| **View 層** | 所有 `views/**/*.vue` | `<style scoped>` + CSS 變數 `--tf-*` |

**View 層禁止使用任何 Tailwind class**（包含 `w-5`、`h-5` 等）。需要尺寸時改用 `style="width:1.25rem"`。

---

## CSS 變數（定義於 `src/style.css :root`）

```css
--tf-color-primary:      #26b7bc;
--tf-color-primary-dark: #156467;
--tf-color-primary-mid:  #1d8e92;
--tf-color-accent:       #ea5520;
--tf-color-accent-warm:  #eda02f;
--tf-color-border:       #e2e8f0;
--tf-color-muted:        #94a3b8;
--tf-font-heading:       'IBM Plex Sans', sans-serif;
```

---

## ⚠️ 黃金規則：View 根元素不加 padding

`AdminLayout <main class="p-4 md:p-6">` 是**唯一頁面邊距來源**（手機 1rem、md 以上 1.5rem）。所有 view 的根元素不得加任何 `padding`。

```html
<!-- ✅ 正確 -->
<main class="brands"> …

<!-- ❌ 錯誤 -->
<main class="brands" style="padding: 2rem 1rem;"> …
```

---

## ⚠️ 黃金規則：圖片一律用 `toBlobUrl()`

**API 回傳的圖片欄位是裸檔名**（如 `20250609123456.jpg`），不是完整 URL。直接綁 `:src="x.photo"` 圖片必破（缺 blob 帳號 + container 前綴）。

```html
<!-- ✅ 正確 -->
<img v-if="x.photo" :src="toBlobUrl(x.photo)" />

<!-- ❌ 錯誤（漏了 blob base + container，圖片裂） -->
<img v-if="x.photo" :src="x.photo" />
```

- helper：[`src/lib/blobUrl.ts`](../web/admin/src/lib/blobUrl.ts) `toBlobUrl(photo)` → `VITE_BLOB_URL/VITE_BLOB_CONTAINER/photo`；已處理 null 與「本身已是 http(s) 完整網址」。
- **凡是 `:src` 綁後端來的圖片欄位（`photo` / `photoUrl` / `logo` / `banner` …），一律包 `toBlobUrl()`**。清單頁縮圖、表單預覽、下拉選項縮圖都算（過去 List 頁與訂單頁就是漏在這裡）。
- 例外：本地 `data:` URL、靜態 asset、CSS 漸層 `background-image` 不需要。
- 環境變數來源見 [docs/08 §7.1](08-new-architecture.md#71-環境變數)；production 由共通 GitHub 變數 `BLOB_BASE_URL` / `BLOB_CONTAINER` 驅動，**不要在元件或 .env 寫死帳號**。

> 對照：store（Nuxt）前台用 `useRuntimeConfig().public.blobUrl`（合併形狀，結尾帶 `/`），不是 `toBlobUrl()`。

---

## 表單類型決策

| 情境 | 做法 | 參照 |
|---|---|---|
| **設定類簡單表單**（品牌、分類、標籤、倉庫等欄位少） | **右側滑出面板**，留在同一頁 | `BrandsView.vue` |
| **複雜業務表單**（訂單、採購單、商品等多 section） | **開新頁**，無 max-width 限制 | `OrderCreateView.vue` |

「設定類」判斷標準：欄位 ≤ 8 個，不需要子明細列表，操作後回到同一清單頁。

---

## ⚠️ 整數編碼欄位的表單綁定（前端必讀，後台設定共用）

> 後台多數「設定類」欄位在凍結的 DB 是**整數編碼（0/1/2…），不是 boolean**。後端 API 原樣回傳整數、`Detail` 端點直接回 Dapper row（欄位名為 DB 原始小寫，**非 camelCase**，因 `DictionaryKeyPolicy` 未設定）。前端綁定時若把整數當 boolean 會踩雷。

### 鐵則

1. **radio**：`:value` 必須是**數字字面量**且配 `v-model.number`，數字要對齊 DB 編碼。
   ```html
   <input type="radio" v-model.number="form.istype" :value="0" /> 折扣比例
   <input type="radio" v-model.number="form.istype" :value="1" /> 折抵金額
   ```
2. **checkbox 不可直接綁整數欄位**。Vue 的 `looseEqual` 以 `String(a)===String(b)` 比對，`String(1) !== String(true)` → **值為 `1` 也永遠不打勾**。兩種正解：
   - 載入時 `Number(x) === 1` 轉布林、送出時 `form.x ? 1 : 0` 轉回整數；或
   - 標明 `:true-value="1" :false-value="0"`。
3. **多於兩態**（如 `isonetime` 有 0/1/2）**不能用單一 checkbox**，改用 radio 群組或 `<select>`，否則第三態無法表達。
4. **載入 Detail 時對所有編碼欄位做 `Number(detail.x)`**，別假設型別；送出前確認送的是整數（後端 DTO 多為 `int`，送 boolean 會被 STJ 擋下 → 400）。

> 殷鑑：折扣碼維護曾把 `istype` 用 1/2（實際是 0/1）、`isonetime`/`isdisable` 當 boolean，導致編輯時 radio/checkbox 全不預選、且新增直接 400。修正見 `DiscountsView.vue`。

### 編碼權威來源

- 多數欄位定義於 **`src/TFoodies.Domain/Enums/Enums.cs`**（值已凍結對齊 DB；另見 [docs/06](06-cross-cutting.md) 列舉表）。
- **例外**：`Discounts.isonetime` **不在** Enums.cs，語意僅在 `src/TFoodies.Infrastructure/Orders/DiscountService.cs` 註解中，見下表。

### 折扣碼維護（SettingMs → Discounts）欄位編碼

| 欄位 | 編碼 | 前端控件 | 備註 |
|---|---|---|---|
| `istype` | 0=折扣比例、1=折抵金額 | radio（`:value` 0/1） | `Domain.DiscountType`；計算見 DiscountService |
| `v` | 折扣值（decimal） | number input | istype=0 時為**折扣後比例**（0.85=85折）；istype=1 時為固定金額 |
| `isonetime` | 0=不限、1=全站限一次、2=每位會員限一次 | radio／select（三態） | **不在 Enums.cs**；語意在 DiscountService |
| `isdisable` | 0=啟用、1=停用 | checkbox（需 int↔bool 轉換） | 軟刪欄位 |

---

## 一、清單頁規範（參照 `BrandsView.vue`）

### 結構

```
<main class="xxx">
  <div class="xxx__header">          ← 標題 + 右側「+ 新增」按鈕
  <p class="xxx__muted">載入中…      ← v-if="loading"
  <p class="xxx__error">             ← v-if="error"
  <div class="card">                 ← 白底圓角卡片，包住 table
    <table class="data-table">
      <thead><tr><th>…</th></tr>
      <tbody>
        <tr class="data-table__row" v-for>
          <td class="action-cell">   ← 編輯 + 刪除按鈕
        <tr v-if="empty">
  <!-- 右側滑出面板 -->
  <!-- 刪除確認 Modal -->
```

### 表格卡片規則

表格**必須**用 `.card` 包住，不可直接裸放 `<table>`：

```html
<div class="card">
  <table class="data-table"> … </table>
</div>
<!-- 分頁列放在 .card 外面 -->
<div class="xxx__pagination"> … </div>
```

`.card` 的三個核心屬性缺一不可：

| 屬性 | 值 | 作用 |
|---|---|---|
| `border-radius` | `10px` | 圓角 |
| `border` | `1px solid var(--tf-color-border)` | 輪廓線（必須用 CSS 變數，不可硬碼） |
| `overflow` | `hidden` | 讓 `th` 頂角被圓角裁切，視覺才正確 |

### CSS 完整樣板

```css
/* ── 根容器：無 max-width，依 AdminLayout p-6 ── */
.xxx {}

/* ── 標題列 ── */
.xxx__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.xxx__title  { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.xxx__error  { color: #dc3545; }
.xxx__muted  { color: var(--tf-color-muted); }

/* ── 表格卡片 ── */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th {
  background: var(--tf-color-primary);
  color: #fff;
  text-align: left;
  padding: 0.65rem 0.75rem;
  font-size: 0.875rem;
  font-weight: 600;
  white-space: nowrap;
}
.action-th { width: 130px; }
.data-table td {
  padding: 0.65rem 0.9rem;
  border-bottom: 1px solid var(--tf-color-border);
  vertical-align: middle;
  color: #334155;
}
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }
.font-mono    { font-family: 'IBM Plex Mono', monospace; }
.font-semibold{ font-weight: 600; }
.text-muted   { color: var(--tf-color-muted); font-size: 0.85rem; }

/* ── 分頁列（有分頁時使用） ── */
.xxx__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.xxx__page-info  { font-size: 0.875rem; color: var(--tf-color-muted); }
```

分頁模板：

```html
<div class="xxx__pagination">
  <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
  <span class="xxx__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
  <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
</div>
```

### 搜尋 / 篩選列（Filter bar）

篩選列放在 `.xxx__header` 與 `.card` 之間，不放進 `.card` 內。

```html
<!-- 狀態 Tab（可選，有狀態切換才加） -->
<div class="xxx__tabs">
  <button
    v-for="tab in TABS" :key="tab.value"
    :class="['xxx__tab', { 'xxx__tab--active': activeTab === tab.value }]"
    @click="selectTab(tab.value)"
  >{{ tab.label }}</button>
</div>

<!-- 篩選列 -->
<div class="xxx__filters">
  <input v-model="filter.keyword" class="filter-input" placeholder="搜尋…" @keyup.enter="search" />
  <input v-model="filter.dateFrom" type="date" class="filter-input filter-input--date" @change="search" />
  <span class="filter-sep">—</span>
  <input v-model="filter.dateTo"   type="date" class="filter-input filter-input--date" @change="search" />
  <select v-model="filter.status" class="filter-select" @change="search">
    <option value="">全部狀態</option>
  </select>
  <button class="btn btn--secondary" @click="search">搜尋</button>
</div>
```

```css
/* ── 狀態 Tab ── */
.xxx__tabs {
  display: flex; flex-wrap: wrap; gap: 0.25rem;
  margin-bottom: 1rem;
  border-bottom: 2px solid var(--tf-color-border);
  padding-bottom: 0;
}
.xxx__tab {
  padding: 0.45rem 1rem;
  border: 1px solid var(--tf-color-border); border-bottom: none;
  border-radius: 4px 4px 0 0;
  background: #fff; color: #495057;
  cursor: pointer; font-size: 0.875rem; font-family: inherit;
  transition: background 0.15s, color 0.15s;
  position: relative; bottom: -2px;
}
.xxx__tab:hover:not(.xxx__tab--active) { background: #f1f3f5; }
.xxx__tab--active {
  background: var(--tf-color-primary); color: #fff;
  border-color: var(--tf-color-primary); font-weight: 500;
}

/* ── 篩選列 ── */
.xxx__filters { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; align-items: center; }
.filter-input {
  flex: 1 1 200px;
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  font-size: 0.875rem; font-family: inherit; background: #fff;
  transition: border-color 0.15s;
}
.filter-input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.filter-input--date { flex: 0 0 auto; width: 9rem; }
.filter-sep  { color: var(--tf-color-muted); font-size: 0.85rem; }
.filter-select {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  background: #fff; font-size: 0.875rem; cursor: pointer; font-family: inherit;
  transition: border-color 0.15s;
}
.filter-select:focus { outline: none; border-color: var(--tf-color-primary); }
```

**篩選列規則：**
- keyword input：`flex: 1 1 200px`（自動填滿空白），不設死寬
- date input：`flex: 0 0 auto; width: 9rem`（固定寬）
- select：固定寬，不設 `flex: 1`
- 搜尋按鈕：`btn--secondary`（中性操作，不搶主按鈕視覺焦點）
- 每個欄位 `@change` 觸發搜尋，keyword 用 `@keyup.enter`

### 分頁列規範

分頁列放在 `.card` **外部**，`margin-top: 1rem`，右對齊。

```html
<div class="xxx__pagination">
  <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
  <span class="xxx__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
  <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
</div>
```

```css
.xxx__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.xxx__page-info  { font-size: 0.875rem; color: var(--tf-color-muted); }
```

**規則：**

| 項目 | 規範值 |
|---|---|
| 按鈕樣式 | `btn--sm btn--ghost` |
| 按鈕文字 | 「上一頁」／「下一頁」 |
| 資訊文字格式 | `第 X 頁（共 N 筆）`（不加總頁數，只顯示總筆數） |
| 上一頁 disabled | `page <= 1` |
| 下一頁 disabled | `page * pageSize >= total`（或等價的 `page >= totalPages`） |
| 位置 | `.card` 外部 |
| 對齊 | `justify-content: flex-end`（右對齊，不置中） |
| gap | `0.75rem` |
| info font-size | `0.875rem`（不可用 `0.85rem` 或 `0.9rem`） |
| info color | `var(--tf-color-muted)`（不可硬碼 `#64748b`） |

---

### 右側滑出面板（create / edit）

```css
/* Overlay */
.panel-overlay {
  position: fixed; inset: 0; z-index: 50;
  background: rgba(15, 23, 42, 0.4); backdrop-filter: blur(1px);
  display: flex; justify-content: flex-end;
  animation: fadeIn 0.15s ease;
}
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }

/* 面板本體 */
.side-panel {
  width: 100%; max-width: 440px; height: 100%;
  background: #fff; box-shadow: -8px 0 40px rgba(0,0,0,0.15);
  display: flex; flex-direction: column;
  animation: slideInRight 0.22s cubic-bezier(0.25,0.46,0.45,0.94);
}
@keyframes slideInRight { from { transform: translateX(100%); } to { transform: none; } }

.panel__header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 1.25rem 1.5rem; border-bottom: 1px solid var(--tf-color-border);
}
.panel__title { font-size: 1.05rem; font-weight: 700; color: #1e293b; margin: 0; }
.panel__close {
  background: none; border: none; cursor: pointer;
  color: var(--tf-color-muted); padding: 0.25rem; border-radius: 4px; display: flex;
}
.panel__close:hover { color: #475569; background: #f1f5f9; }
.panel__body   { flex: 1; overflow-y: auto; padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
.panel__footer { padding: 1rem 1.5rem; border-top: 1px solid var(--tf-color-border); display: flex; justify-content: flex-end; gap: 0.5rem; }

/* 面板內的表單欄位 */
.form-field        { display: flex; flex-direction: column; gap: 0.35rem; }
.form-row          { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.required          { color: #ef4444; }
.form-field__input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem; color: #1e293b; background: #fff;
  transition: border-color 0.15s; font-family: inherit;
}
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38,183,188,0.15); }
.checkbox-option       { display: flex; align-items: center; gap: 0.5rem; font-size: 0.875rem; color: #475569; cursor: pointer; padding-top: 0.4rem; }
.checkbox-option input { accent-color: var(--tf-color-primary); width: 16px; height: 16px; }
.form-error { color: #dc3545; font-size: 0.85rem; }
```

### 刪除確認 Modal

```css
.modal-overlay {
  position: fixed; inset: 0; z-index: 60;
  background: rgba(15,23,42,0.45);
  display: flex; align-items: center; justify-content: center; padding: 1rem;
}
.modal         { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 380px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title  { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body   { padding: 1.25rem 1.4rem; }
.modal__hint   { font-size: 0.85rem; color: var(--tf-color-muted); margin: 0; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }
```

Modal 按鈕順序：左 `btn--ghost`（取消）→ 右 `btn--danger`（確認刪除）。  
`@click.self` 綁定在 overlay 上允許點外部關閉。

---

## 二、表單頁規範（參照 `OrderCreateView.vue`）

### 結構

```
<div class="xxx">
  <div class="xxx__header">          ← 左：← 返回 (btn--ghost) + h1
  <form @submit.prevent>
    <div class="xxx__layout">        ← 兩欄 grid（複雜）或單欄（簡單）
      <div class="xxx__main">
        <div class="form-card">      ← 每個 section 一個卡片
          <h2 class="form-section__title">Section 名稱</h2>
          <div class="form-row">
            <div class="form-field [form-field--full]">
              <label class="label">欄位名 <span class="req">*</span></label>
              <input class="input" / <select class="select"> / <textarea class="textarea">
      <div class="xxx__aside">       ← 設定類欄位 + 提交列（複雜表單才有）
```

### CSS 完整樣板

```css
/* ── 根容器 ── */
.xxx { width: 100%; }   /* 不設 max-width，由 AdminLayout 控制 */

/* ── 頁首 ── */
.xxx__header {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: 1.5rem;
}
.xxx__title {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  font-size: 1.25rem; margin: 0;
}

/* ── 兩欄版型（複雜表單） ── */
.xxx__layout { display: grid; grid-template-columns: 1fr; gap: 1.25rem; align-items: start; }
@media (min-width: 1024px) {
  .xxx__layout  { grid-template-columns: 1fr 360px; }
  .xxx__aside   { position: sticky; top: 1.5rem; }
}
@media (min-width: 1280px) {
  .xxx__layout  { grid-template-columns: 1fr 400px; }
}

/* ── 卡片 ── */
.form-card {
  background: #fff;
  border: 1px solid var(--tf-color-border);
  border-radius: 6px;
  padding: 1.25rem;
  margin-bottom: 1.25rem;
}
.xxx__aside .form-card { padding: 1rem; }   /* 右欄卡片稍緊湊 */

/* ── Section 標題 ── */
.form-section__title {
  font-size: 1rem; font-weight: 600;
  color: var(--tf-color-primary-dark);
  margin: 0 0 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--tf-color-border);
}
.xxx__aside .form-section__title { font-size: 0.875rem; margin-bottom: 0.75rem; padding-bottom: 0.4rem; }

/* ── 欄位佈局 ── */
.form-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 0.75rem 1.25rem;
  margin-bottom: 0.75rem;
}
.form-field      { display: flex; flex-direction: column; gap: 0.3rem; }
.form-field--full{ grid-column: 1 / -1; }

/* ── 標籤 ── */
.label { font-size: 0.8rem; font-weight: 500; color: #374151; }
.req   { color: var(--tf-color-accent); margin-left: 0.1rem; }

/* ── 輸入控制項 ── */
.input,
.select,
.textarea {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.9rem; font-family: inherit; background: #fff;
  transition: border-color 0.15s;
}
.input:focus,
.select:focus,
.textarea:focus {
  outline: none;
  border-color: var(--tf-color-primary);
  box-shadow: 0 0 0 2px rgba(38,183,188,0.15);
}
.textarea { resize: vertical; }

/* ── 操作列（右欄底部 or 單欄底部） ── */
.xxx__submit-row { display: flex; flex-direction: column; gap: 0.5rem; }
.xxx__submit-row .btn { width: 100%; justify-content: center; }

/* ── 訊息 ── */
.form-msg--error   { background: #fbeaea; color: #c0392b; border: 1px solid #f5c6c6; border-radius: 4px; padding: 0.6rem 0.9rem; font-size: 0.875rem; margin-bottom: 1rem; }
.form-msg--success { background: #e6f4ea; color: #1e7e34; border: 1px solid #b8dfc0; border-radius: 4px; padding: 0.6rem 0.9rem; font-size: 0.875rem; margin-bottom: 1rem; }
```

---

## 三、按鈕規範

### 使用情境

| Variant | 使用情境 | 典型文字 |
|---|---|---|
| `btn--primary` | **主要動作**：儲存、建立、送出 | 儲存、建立訂單、確認 |
| `btn--ghost` | **次要動作**：取消、返回清單（與 primary 配對） | 取消、返回 |
| `btn--secondary` | **中性操作**：搜尋、匯出、不明確的觸發 | 搜尋、重設 |
| `btn--accent` | **特殊強調**：付款確認、重要狀態變更 | 確認付款 |
| `btn--danger` | **不可逆確認**：只用在 Delete Modal 內部 | 確認刪除 |
| `btn--danger-ghost` | **清單行刪除觸發**：列表行的輕量刪除按鈕 | 刪除 |
| `btn--sm` | **修飾符**：表格行內操作，搭配上述任一 variant | 編輯、刪除 |

### 按鈕順序規則

```
[取消 / 返回]  →  [主動作]
btn--ghost        btn--primary / btn--danger
```

- 表單 panel footer：取消（ghost）**左**，儲存（primary）**右**
- Delete Modal footer：取消（ghost）**左**，確認刪除（danger）**右**
- 頁面頂部 header：返回（ghost）**左**，主操作（primary）**右**

### CSS 完整定義

```css
/* ── 基底 ── */
.btn {
  display: inline-flex; align-items: center; justify-content: center;
  padding: 0.45rem 1rem;
  border: 1px solid transparent; border-radius: 4px;
  cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit;
  text-decoration: none; transition: opacity 0.15s, background 0.15s;
  white-space: nowrap;
}
.btn:disabled { opacity: 0.45; cursor: not-allowed; }

/* ── 尺寸 ── */
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }   /* 表格行內 */

/* ── 主要 ── */
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }

/* ── 次要（透明框線） ── */
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }

/* ── 中性 ── */
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }

/* ── 強調（橘） ── */
.btn--accent { background: var(--tf-color-accent); color: #fff; border-color: var(--tf-color-accent); }
.btn--accent:hover:not(:disabled) { opacity: 0.85; }

/* ── 危險（實心，Modal 確認用） ── */
.btn--danger { background: #dc3545; color: #fff; border-color: #dc3545; }
.btn--danger:hover:not(:disabled) { background: #b02a37; }

/* ── 危險（透明框線，行內觸發用） ── */
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }
```

### 狀態 Tab（僅篩選列使用）

狀態 Tab 不是通用 `.btn`，是清單頁**篩選列**專用的 Tab 切換元件，已在「搜尋/篩選列」段落定義。勿在其他情境使用 `xxx__tab` 樣式。

### 圖示關閉按鈕（面板 / Modal 右上角）

```css
.panel__close {
  background: none; border: none; cursor: pointer;
  color: var(--tf-color-muted); padding: 0.25rem; border-radius: 4px; display: flex;
}
.panel__close:hover { color: #475569; background: #f1f5f9; }
```

```html
<button class="panel__close" @click="close" aria-label="關閉">
  <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
  </svg>
</button>
```

SVG 尺寸一律用 `style="width:1.25rem;height:1.25rem"`，不用 Tailwind `w-5 h-5`。

---

## 四、Badge 規範

```css
.badge {
  display: inline-block;
  padding: 0.2em 0.5em; border-radius: 3px;
  font-size: 0.78rem; font-weight: 500; white-space: nowrap;
}

/* 付款 */
.badge--paid    { background: #d4edda; color: #155724; }
.badge--unpaid  { background: #fff3cd; color: #856404; }
.badge--partial { background: #fff3cd; color: #856404; }

/* 出貨 */
.badge--pending  { background: #e2e3e5; color: #383d41; }
.badge--queue    { background: #cce5ff; color: #004085; }
.badge--shipped  { background: #d4edda; color: #155724; }
.badge--canceled { background: #f8d7da; color: #721c24; }
.badge--returned { background: #fce8d5; color: #7d3900; }

/* 通用狀態 */
.badge--active   { background: #dcfce7; color: #166534; }
.badge--disabled { background: #f1f5f9; color: #64748b; }
```

---

## 五、行內展開面板

```css
/* 詳情展開（主色左邊框） */
.detail-panel {
  background: rgba(38, 183, 188, 0.04);
  border-left: 3px solid var(--tf-color-primary);
  padding: 1rem 1.25rem;
}

/* 快速新增表單（強調色左邊框） */
.inline-form { background: #fffbf7; border-left: 3px solid var(--tf-color-accent); padding: 1rem 1.25rem; }
.inline-form__title { margin: 0 0 0.75rem; font-size: 0.95rem; color: var(--tf-color-accent); }
```

---

## 六、命名與 RWD 規則

### CSS 命名
- **清單 view**：BEM 模組前綴，根類為 `.xxx`（如 `.orders`、`.brands`）；子元素 `.xxx__header`、`.xxx__title`。
- **表單 view**：語意通用名，如 `.form-card`、`.form-row`、`.label`、`.input`（scoped 已隔離）。
- `<style scoped>` 已隔離作用域，無需擔心命名衝突。

### 表格 RWD（已全站套用，新清單頁沿用）
清單頁的表格一律用 `.card` 包住，並讓 `.card` 在窄螢幕水平捲動、`.data-table` 設 `min-width`：
```css
.card { background:#fff; border-radius:10px; border:1px solid var(--tf-color-border); overflow: auto; }  /* 不可用 overflow:hidden，否則窄螢幕裁切表格 */
.data-table { width:100%; border-collapse:collapse; font-size:0.875rem; min-width: 720px; }              /* 欄多時可調大 */
```
> 既有 44 個清單頁已批次套用此規則（`.card overflow:auto` + `.data-table min-width`），新頁直接照抄即可。

### 版面外殼 RWD（`AdminLayout.vue`）
- 側欄 `w-60 fixed`，`md:` 以下為 **off-canvas 抽屜**：預設 `-translate-x-full` 隱藏，`md:translate-x-0` 常駐；以 `sidebarOpen` 控制 `translate-x-0` 滑入。
- topbar 在 `md:` 以下顯示 **漢堡按鈕**（`md:hidden`）開啟側欄；換頁（`watch route.path`）與點遮罩會自動收合。
- 主區 `md:ml-60`（手機不留側欄寬）、topbar `px-4 md:px-6`、`食在呼 ERP /` 麵包屑前綴與使用者 chip 於 `sm:` 以下隱藏。

### 斷點
| Prefix | 最小寬度 |
|---|---|
| `sm:` | 640px |
| `md:` | 768px |
| `lg:` | 1024px |
| `xl:` | 1280px |

後台最低支援寬度：**375px**（一般手機）。`md:`（768px）為「側欄常駐／off-canvas」的分界。

---

## 七、快速對照（選哪個 pattern？）

| 情境 | Pattern | 參照檔案 |
|---|---|---|
| 管理簡單設定項（CRUD，欄位少） | 清單 + 右側滑出面板 | `BrandsView.vue` |
| 新增/編輯複雜業務資料 | 獨立表單頁（新路由） | `OrderCreateView.vue` |
| 確認刪除 | 中央 Modal | `BrandsView.vue` |
| 列表行展開詳情 | 行內展開 detail-panel | `AccountingView.vue`、`MembersView.vue` |
| 列表行快速操作（付款等） | 行內展開 inline-form | `AccountingView.vue` |

---

## Agent 分工

| 任務 | Agent |
|---|---|
| 套用規範、重構現有 view | `frontend-architect` |
| 審查 UI 一致性、找偏差 | `code-review-optimizer` |
| 設計新版型或討論元件調整 | `visual-design-architect` |
| 測試 UI 邊界案例 | `qa-test-engineer` |

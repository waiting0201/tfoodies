# 09 — 測試架構

## 總覽

| 層次 | 測試專案 | 框架 | 說明 |
|---|---|---|---|
| Domain | `tests/TFoodies.Domain.Tests` | xUnit | 純領域邏輯（Money、VAT、值物件） |
| Application | `tests/TFoodies.Application.Tests` | xUnit | 用例/服務層（Command/Query handler） |
| Infrastructure | `tests/TFoodies.Infrastructure.Tests` | xUnit | Codec、金流、ATM、發票整合 |
| 前台視覺回歸 | `web/visual-regression` | Playwright | 新舊前台像素比對（parity gate） |

---

## 後端 .NET 測試（xUnit）

### 執行

```bash
# 從 repo root
dotnet test tests/TFoodies.Domain.Tests
dotnet test tests/TFoodies.Application.Tests
dotnet test tests/TFoodies.Infrastructure.Tests
# 全部一次
dotnet test
```

### 現有測試清單

| 檔案 | 測試對象 |
|---|---|
| `Domain.Tests/Common/MoneyTests.cs` | `Money` 值物件加減乘、幣別防護 |
| `Domain.Tests/Common/TaiwanVatTests.cs` | 台灣 5% VAT 計算（`tax = price - round(price/1.05)`） |
| `Application.Tests/Tax/TaiwanVatApplicationTests.cs` | Application 層 VAT 用例 |
| `Infrastructure.Tests/Payments/FiscMessageCodecTests.cs` | 財政部發票 FISC 訊息 Codec |
| `Infrastructure.Tests/Orders/OrderServiceAtmTests.cs` | ATM 轉帳碼生成與驗證 |
| `Infrastructure.Tests/Invoicing/EzPayCodecTests.cs` | ezPay 電子發票 Codec |

### 套件

- `xunit` 2.9.2 + `xunit.runner.visualstudio` 2.8.2
- `coverlet.collector` 6.0.2（覆蓋率）
- 各專案已設 `<Using Include="Xunit" />` global using

### 慣例

- 新測試依層次放入對應專案；不跨層相依（Domain.Tests 不可引用 Infrastructure）。
- Infrastructure 測試若需外部服務（SQL、ezPay）一律使用假資料/mock，避免 CI 需網路。
- 測試方法命名：`MethodName_Scenario_ExpectedResult`（已有測試為準）。

---

## 前台視覺回歸（Playwright）

位於 `web/visual-regression/`，用於確保新 Nuxt 前台與舊 ASP.NET 前台像素一致（計畫 §7.1）。

### 執行

```bash
cd web/visual-regression
# 擷取基準（對舊站）
BASE_URL=https://www.tfoodies.com npm run test:update
# 比對新站
BASE_URL=http://localhost:3000 npx playwright test
```

### 設定摘要（`playwright.config.ts`）

- 快照路徑：`tests/__legacy_baseline__/`
- 容差：`maxDiffPixelRatio: 0.001`（≤ 0.1% 像素差異）
- 視窗：desktop(1280) · laptop(1024) · tablet(768) · mobile(375)

### 受測路由（`tests/parity.spec.ts`）

`/` · `/Products` · `/News` · `/Recipes` · `/Issues` · `/TFoodies`
（商品詳頁 `/Product/<slug>` 與品牌頁 `/Brand/<slug>` 待補 known slug 後啟用）

---

## Agent 分工

| 任務 | Agent |
|---|---|
| 審查測試品質、找邊界案例 | `qa-test-engineer` |
| 新增 xUnit 單元／整合測試（C#） | `backend-engineer` |
| 新增 Playwright / Vitest 前端測試 | `frontend-architect` |
| 重構測試結構、消除重複 | `code-review-optimizer` |

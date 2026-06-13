<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { apiFetch } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────
interface BalanceSheet {
  asOf: string
  assets: { cash: number; accountsReceivable: number; inventory: number; total: number }
  liabilities: { accountsPayable: number; total: number }
  equity: { retainedEarnings: number; total: number }
}

// ─── helpers ──────────────────────────────────────────────────────────────────
function fmtMoney(n: number) {
  return (n ?? 0).toLocaleString('zh-TW', {
    style: 'currency', currency: 'TWD', maximumFractionDigits: 0,
  })
}

// ─── state ────────────────────────────────────────────────────────────────────
const now = new Date()
const pad = (n: number) => String(n).padStart(2, '0')
const today = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`

const asOf = ref(today)
const loading = ref(false)
const error = ref('')
const data = ref<BalanceSheet | null>(null)

// 資產 = 負債 + 權益（推導後恆等，僅作視覺核對）
const balanced = computed(() => {
  if (!data.value) return true
  return data.value.assets.total === data.value.liabilities.total + data.value.equity.total
})

async function query() {
  if (!asOf.value) {
    error.value = '請選擇基準日'
    return
  }
  loading.value = true
  error.value = ''
  try {
    const qs = new URLSearchParams({ asOf: asOf.value })
    data.value = await apiFetch<BalanceSheet>(`/admin/statements/balance-sheet?${qs}`)
  } catch (e: any) {
    error.value = e.message ?? '查詢失敗'
    data.value = null
  } finally {
    loading.value = false
  }
}

onMounted(query)
</script>

<template>
  <main class="bs">
    <h1 class="bs__title">資產負債表</h1>

    <!-- 篩選列（依 docs/10 規範） -->
    <div class="bs__filters">
      <input
        v-model="asOf"
        type="date"
        class="filter-input filter-input--date"
        title="基準日"
        @change="query"
      />
      <button class="btn btn--secondary" :disabled="loading" @click="query">
        {{ loading ? '查詢中…' : '搜尋' }}
      </button>
    </div>

    <p class="bs__note">
      ※ 現行資料庫無獨立資產／負債科目，本表由交易資料推導：現金 = 累計收款 − 付款 − 退款；
      應收 = 未收款請款單；存貨 = 各倉現有量 × 進貨單價（現值）；應付 = 未付款應付憑單；權益為差額平衡項。
    </p>

    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="error" class="state-msg state-msg--error">{{ error }}</div>

    <template v-else-if="data">
      <!-- Summary cards -->
      <div class="stat-grid mt-4">
        <div class="stat-card stat-card--indigo">
          <div class="stat-card__label">資產總額</div>
          <div class="stat-card__value font-mono">{{ fmtMoney(data.assets.total) }}</div>
        </div>
        <div class="stat-card stat-card--amber">
          <div class="stat-card__label">負債總額</div>
          <div class="stat-card__value font-mono">{{ fmtMoney(data.liabilities.total) }}</div>
        </div>
        <div class="stat-card stat-card--green">
          <div class="stat-card__label">業主權益（淨值）</div>
          <div class="stat-card__value font-mono">{{ fmtMoney(data.equity.total) }}</div>
        </div>
      </div>

      <div class="bs__columns mt-4">
        <!-- 資產 -->
        <div class="card">
          <div class="result-header"><span class="result-title">資產</span></div>
          <table class="data-table">
            <tbody>
              <tr class="data-table__row">
                <td>現金及約當現金</td>
                <td class="num-cell font-mono">{{ fmtMoney(data.assets.cash) }}</td>
              </tr>
              <tr class="data-table__row">
                <td>應收帳款</td>
                <td class="num-cell font-mono">{{ fmtMoney(data.assets.accountsReceivable) }}</td>
              </tr>
              <tr class="data-table__row">
                <td>存貨（現值）</td>
                <td class="num-cell font-mono">{{ fmtMoney(data.assets.inventory) }}</td>
              </tr>
              <tr class="total-row">
                <td class="total-label">資產總額</td>
                <td class="num-cell font-mono total-val">{{ fmtMoney(data.assets.total) }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- 負債及權益 -->
        <div class="card">
          <div class="result-header"><span class="result-title">負債及權益</span></div>
          <table class="data-table">
            <tbody>
              <tr class="section-row"><td colspan="2">負債</td></tr>
              <tr class="data-table__row">
                <td>應付帳款</td>
                <td class="num-cell font-mono">{{ fmtMoney(data.liabilities.accountsPayable) }}</td>
              </tr>
              <tr class="subtotal-row">
                <td class="subtotal-label">負債總額</td>
                <td class="num-cell font-mono subtotal-val">{{ fmtMoney(data.liabilities.total) }}</td>
              </tr>

              <tr class="section-row"><td colspan="2">權益</td></tr>
              <tr class="data-table__row">
                <td>業主權益（淨值）</td>
                <td class="num-cell font-mono">{{ fmtMoney(data.equity.retainedEarnings) }}</td>
              </tr>
              <tr class="subtotal-row">
                <td class="subtotal-label">權益總額</td>
                <td class="num-cell font-mono subtotal-val">{{ fmtMoney(data.equity.total) }}</td>
              </tr>

              <tr class="total-row">
                <td class="total-label">負債及權益總額</td>
                <td class="num-cell font-mono total-val">
                  {{ fmtMoney(data.liabilities.total + data.equity.total) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <p v-if="!balanced" class="bs__warn">⚠ 資產與負債及權益總額不平衡，請檢查資料。</p>
    </template>
  </main>
</template>

<style scoped>
.bs { }
.bs__title { font-size: 1.5rem; font-weight: 700; color: #1e293b; margin-bottom: 1.5rem; letter-spacing: -0.02em; }
.bs__note { font-size: 0.78rem; color: #94a3b8; line-height: 1.6; margin: 0.75rem 0 0; }
.bs__warn { margin-top: 1rem; color: #dc2626; font-size: 0.85rem; font-weight: 600; }

/* Card */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }

/* Two-column layout */
.bs__columns { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; align-items: start; }
@media (max-width: 768px) { .bs__columns { grid-template-columns: 1fr; } }

/* 篩選列（依 docs/10 規範） */
.bs__filters { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; align-items: center; }
.filter-input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  font-size: 0.875rem; font-family: inherit; background: #fff;
  transition: border-color 0.15s;
}
.filter-input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.filter-input--date { flex: 0 0 auto; width: 9rem; }

/* 按鈕（scoped，.btn 非全域） */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; transition: background 0.15s, opacity 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }

/* Stat cards */
.stat-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; }
@media (max-width: 768px) { .stat-grid { grid-template-columns: 1fr; } }
.stat-card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); padding: 1.25rem 1.5rem; }
.stat-card--indigo { border-color: #c7d2fe; background: #eef2ff; }
.stat-card--amber { border-color: #fde68a; background: #fffbeb; }
.stat-card--green { border-color: #bbf7d0; background: #f0fdf4; }
.stat-card__label { font-size: 0.75rem; font-weight: 600; color: #64748b; margin-bottom: 0.5rem; }
.stat-card--indigo .stat-card__label { color: #4338ca; }
.stat-card--amber .stat-card__label { color: #b45309; }
.stat-card--green .stat-card__label { color: #166534; }
.stat-card__value { font-size: 1.4rem; font-weight: 700; color: #1e293b; line-height: 1.2; }
.stat-card--indigo .stat-card__value { color: #3730a3; }
.stat-card--amber .stat-card__value { color: #92400e; }
.stat-card--green .stat-card__value { color: #14532d; }

/* Result header */
.result-header { display: flex; align-items: center; justify-content: space-between; padding: 0.9rem 1rem; border-bottom: 1px solid #e2e8f0; }
.result-title { font-size: 0.9rem; font-weight: 600; color: #334155; }

/* Tables */
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }.data-table td { padding: 0.65rem 1rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:hover td { background: #f8faf8; }
.num-cell { text-align: right; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }

/* Section header row */
.section-row td { background: #f1f5f9; font-weight: 600; color: #475569; font-size: 0.83rem; padding: 0.5rem 1rem; }

/* Subtotal row */
.subtotal-row td { background: #fbfdfd; font-weight: 600; }
.subtotal-label { color: #475569; }
.subtotal-val { color: #1e293b; }

/* Total row */
.total-row td { border-top: 2px solid var(--tf-color-primary); background: #ecfeff; font-weight: 700; padding: 0.8rem 1rem; }
.total-label { color: #155e63; }
.total-val { font-size: 1.0rem; }

/* State messages */
.state-msg { padding: 2rem; text-align: center; color: #94a3b8; }
.state-msg--error { color: #dc2626; }

/* Spacing */
.mt-4 { margin-top: 1rem; }
</style>

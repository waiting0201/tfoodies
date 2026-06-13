<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { apiFetch } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────
interface Line {
  accountingCode: string
  accountingTitle: string
  amount: number
}

interface IncomeStatement {
  startDate: string
  endDate: string
  revenues: Line[]
  revenueTotal: number
  returns: Line[]
  returnTotal: number
  expenses: Line[]
  expenseTotal: number
  netRevenue: number
  netIncome: number
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

const startDate = ref(`${now.getFullYear()}-01-01`)
const endDate = ref(today)
const loading = ref(false)
const error = ref('')
const data = ref<IncomeStatement | null>(null)

const netIncomePositive = computed(() => (data.value?.netIncome ?? 0) >= 0)

async function query() {
  if (!startDate.value || !endDate.value) {
    error.value = '請選擇起訖日期'
    return
  }
  loading.value = true
  error.value = ''
  try {
    const qs = new URLSearchParams({ startDate: startDate.value, endDate: endDate.value })
    data.value = await apiFetch<IncomeStatement>(`/admin/statements/income-statement?${qs}`)
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
  <main class="stmt">
    <h1 class="stmt__title">損益表</h1>

    <!-- 篩選列（依 docs/10 規範） -->
    <div class="stmt__filters">
      <input
        v-model="startDate"
        type="date"
        class="filter-input filter-input--date"
        title="起始日期"
        @change="query"
      />
      <span class="filter-sep">—</span>
      <input
        v-model="endDate"
        type="date"
        class="filter-input filter-input--date"
        title="截止日期"
        @change="query"
      />
      <button class="btn btn--secondary" :disabled="loading" @click="query">
        {{ loading ? '查詢中…' : '搜尋' }}
      </button>
    </div>

    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="error" class="state-msg state-msg--error">{{ error }}</div>

    <template v-else-if="data">
      <!-- Net income summary -->
      <div class="stat-grid mt-4">
        <div class="stat-card stat-card--indigo">
          <div class="stat-card__label">營業收入（淨額）</div>
          <div class="stat-card__value font-mono">{{ fmtMoney(data.revenueTotal) }}</div>
        </div>
        <div class="stat-card stat-card--amber">
          <div class="stat-card__label">營業支出</div>
          <div class="stat-card__value font-mono">{{ fmtMoney(data.expenseTotal) }}</div>
        </div>
        <div class="stat-card" :class="netIncomePositive ? 'stat-card--green' : 'stat-card--rose'">
          <div class="stat-card__label">本期損益</div>
          <div class="stat-card__value font-mono">{{ fmtMoney(data.netIncome) }}</div>
        </div>
      </div>

      <!-- Statement table -->
      <div class="card mt-4">
        <div class="result-header">
          <span class="result-title">損益表</span>
          <span class="result-count">{{ data.startDate.slice(0, 10) }} ～ {{ data.endDate.slice(0, 10) }}</span>
        </div>
        <table class="data-table">
          <thead>
            <tr>
              <th style="width:120px">科目代號</th>
              <th>科目名稱</th>
              <th class="num-th">金額</th>
            </tr>
          </thead>
          <tbody>
            <!-- 營業收入 -->
            <tr class="section-row"><td colspan="3">營業收入</td></tr>
            <tr v-for="r in data.revenues" :key="'rev-' + r.accountingCode + r.accountingTitle" class="data-table__row">
              <td class="font-mono text-sm text-muted">{{ r.accountingCode }}</td>
              <td>{{ r.accountingTitle }}</td>
              <td class="num-cell font-mono">{{ fmtMoney(r.amount) }}</td>
            </tr>
            <tr v-if="data.revenues.length === 0"><td colspan="3" class="empty-cell">本期無收入資料</td></tr>
            <tr class="subtotal-row">
              <td colspan="2" class="subtotal-label">營業收入合計</td>
              <td class="num-cell font-mono subtotal-val">{{ fmtMoney(data.revenueTotal) }}</td>
            </tr>

            <!-- 銷貨退回 -->
            <tr class="section-row"><td colspan="3">減：銷貨退回</td></tr>
            <tr v-for="r in data.returns" :key="'ret-' + r.accountingCode + r.accountingTitle" class="data-table__row">
              <td class="font-mono text-sm text-muted">{{ r.accountingCode }}</td>
              <td>{{ r.accountingTitle }}</td>
              <td class="num-cell font-mono text-rose">({{ fmtMoney(r.amount) }})</td>
            </tr>
            <tr v-if="data.returns.length === 0"><td colspan="3" class="empty-cell">本期無退回資料</td></tr>
            <tr class="subtotal-row">
              <td colspan="2" class="subtotal-label">銷貨退回合計</td>
              <td class="num-cell font-mono subtotal-val text-rose">({{ fmtMoney(data.returnTotal) }})</td>
            </tr>

            <!-- 營業淨額 -->
            <tr class="subtotal-row subtotal-row--strong">
              <td colspan="2" class="subtotal-label">營業淨收入</td>
              <td class="num-cell font-mono subtotal-val">{{ fmtMoney(data.netRevenue) }}</td>
            </tr>

            <!-- 營業支出 -->
            <tr class="section-row"><td colspan="3">減：營業支出</td></tr>
            <tr v-for="r in data.expenses" :key="'exp-' + r.accountingCode + r.accountingTitle" class="data-table__row">
              <td class="font-mono text-sm text-muted">{{ r.accountingCode }}</td>
              <td>{{ r.accountingTitle }}</td>
              <td class="num-cell font-mono text-rose">({{ fmtMoney(r.amount) }})</td>
            </tr>
            <tr v-if="data.expenses.length === 0"><td colspan="3" class="empty-cell">本期無支出資料</td></tr>
            <tr class="subtotal-row">
              <td colspan="2" class="subtotal-label">營業支出合計</td>
              <td class="num-cell font-mono subtotal-val text-rose">({{ fmtMoney(data.expenseTotal) }})</td>
            </tr>

            <!-- 本期損益 -->
            <tr class="total-row">
              <td colspan="2" class="total-label">本期損益</td>
              <td class="num-cell font-mono total-val" :class="netIncomePositive ? 'text-green' : 'text-rose'">
                {{ fmtMoney(data.netIncome) }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </main>
</template>

<style scoped>
.stmt { }
.stmt__title { font-size: 1.5rem; font-weight: 700; color: #1e293b; margin-bottom: 1.5rem; letter-spacing: -0.02em; }

/* Card */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }

/* 篩選列（依 docs/10 規範） */
.stmt__filters { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; align-items: center; }
.filter-input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  font-size: 0.875rem; font-family: inherit; background: #fff;
  transition: border-color 0.15s;
}
.filter-input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.filter-input--date { flex: 0 0 auto; width: 9rem; }
.filter-sep { color: var(--tf-color-muted); font-size: 0.85rem; }

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
.stat-card--rose { border-color: #fecdd3; background: #fff1f2; }
.stat-card__label { font-size: 0.75rem; font-weight: 600; color: #64748b; margin-bottom: 0.5rem; }
.stat-card--indigo .stat-card__label { color: #4338ca; }
.stat-card--amber .stat-card__label { color: #b45309; }
.stat-card--green .stat-card__label { color: #166534; }
.stat-card--rose .stat-card__label { color: #be123c; }
.stat-card__value { font-size: 1.4rem; font-weight: 700; color: #1e293b; line-height: 1.2; }
.stat-card--indigo .stat-card__value { color: #3730a3; }
.stat-card--amber .stat-card__value { color: #92400e; }
.stat-card--green .stat-card__value { color: #14532d; }
.stat-card--rose .stat-card__value { color: #9f1239; }

/* Result header */
.result-header { display: flex; align-items: center; justify-content: space-between; padding: 0.9rem 1rem; border-bottom: 1px solid #e2e8f0; }
.result-title { font-size: 0.9rem; font-weight: 600; color: #334155; }
.result-count { font-size: 0.8rem; color: #94a3b8; font-family: 'IBM Plex Mono', monospace; }

/* Tables */
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 1rem; border-bottom: 1px solid #e2e8f0; font-size: 0.875rem; font-weight: 600; }
.num-th { text-align: right; }
.data-table td { padding: 0.6rem 1rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:hover td { background: #f8faf8; }
.num-cell { text-align: right; }
.empty-cell { text-align: center; color: #94a3b8; padding: 1.5rem; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }
.text-muted { color: #94a3b8; }
.text-sm { font-size: 0.83rem; }
.text-green { color: #16a34a; }
.text-rose { color: #e11d48; }

/* Section header row */
.section-row td { background: #f1f5f9; font-weight: 600; color: #475569; font-size: 0.83rem; padding: 0.5rem 1rem; }

/* Subtotal row */
.subtotal-row td { background: #fbfdfd; font-weight: 600; }
.subtotal-row--strong td { border-top: 1px solid #cbd5e1; }
.subtotal-label { color: #475569; }
.subtotal-val { color: #1e293b; }

/* Total row */
.total-row td { border-top: 2px solid var(--tf-color-primary); background: #ecfeff; font-weight: 700; padding: 0.8rem 1rem; }
.total-label { color: #155e63; }
.total-val { font-size: 1.05rem; }

/* State messages */
.state-msg { padding: 2rem; text-align: center; color: #94a3b8; }
.state-msg--error { color: #dc2626; }

/* Spacing */
.mt-4 { margin-top: 1rem; }
</style>

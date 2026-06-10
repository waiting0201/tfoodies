<script setup lang="ts">
import { ref } from 'vue'
import { apiFetch } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────
interface AmountOrder {
  ordercode: string
  ordertype: number
  orderdate: string
  paytype: number
  paystatus: number
  amount: number
  recivername: string
  warehouseTitle: string | null
  memberName: string
  memberMobile: string
}
interface AmountsReport {
  grandTotal: number
  orders: AmountOrder[]
}

// ─── enum 對照（比照舊系統 EnumOrderType / EnumPayType / EnumPayStatus） ──────────
const ORDER_TYPE_LABEL: Record<number, string> = { 1: '線上單', 2: '線下單', 3: '自用', 4: '預購', 5: '公關' }
const ORDER_TYPE_CLASS: Record<number, string> = {
  1: 'badge--otype-online', 2: 'badge--otype-offline', 3: 'badge--otype-self',
  4: 'badge--otype-pre', 5: 'badge--otype-pr',
}
const PAY_TYPE_LABEL: Record<number, string> = {
  1: '信用卡線上刷卡', 2: '宅配貨到付款', 3: 'ATM轉帳付款',
  4: '免付款', 5: '現金支付', 6: '電匯', 7: '支票',
}
const PAY_STATUS_LABEL: Record<number, string> = { 0: '未付款', 1: '已付款', 2: '退款', 3: '免付款', 4: '取消' }
const PAY_STATUS_CLASS: Record<number, string> = {
  0: 'badge--unpaid', 1: 'badge--paid', 2: 'badge--returned', 3: 'badge--queue', 4: 'badge--canceled',
}
// 付款狀態篩選選項（比照舊 Amountreports 下拉）
const PAY_FILTER_OPTIONS = [
  { label: '付款狀態', value: '' },
  { label: '未付款', value: '0' },
  { label: '已付款', value: '1' },
  { label: '退款', value: '2' },
  { label: '免付款', value: '3' },
]

// ─── helpers ──────────────────────────────────────────────────────────────────
function fmtDate(d: string) {
  return d ? d.slice(0, 10) : '—'
}
function fmtInt(n: number) {
  return (n ?? 0).toLocaleString('en-US')
}

// ─── state ────────────────────────────────────────────────────────────────────
const now = new Date()
const startDate = ref(`${now.getFullYear()}-01-01`)
const endDate = ref(now.toISOString().slice(0, 10))
const payStatus = ref('')
const loading = ref(false)
const error = ref('')
const data = ref<AmountsReport | null>(null)

async function query() {
  if (!startDate.value || !endDate.value) {
    error.value = '起始日期與結束日期為必填。'
    return
  }
  loading.value = true
  error.value = ''
  try {
    const qs = new URLSearchParams({ startDate: startDate.value, endDate: endDate.value })
    if (payStatus.value !== '') qs.set('payStatus', payStatus.value)
    data.value = await apiFetch<AmountsReport>(`/admin/reports/amounts?${qs}`)
  } catch (e: any) {
    error.value = e.message ?? '查詢失敗'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="rpt">
    <div class="rpt__header">
      <h1 class="rpt__title">銷售額報表</h1>
    </div>

    <div class="rpt__filters">
      <input v-model="startDate" type="date" class="filter-input filter-input--date" />
      <span class="filter-sep">—</span>
      <input v-model="endDate" type="date" class="filter-input filter-input--date" />
      <select v-model="payStatus" class="filter-select">
        <option v-for="opt in PAY_FILTER_OPTIONS" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
      </select>
      <button class="btn btn--secondary" :disabled="loading" @click="query">
        {{ loading ? '查詢中…' : '查詢' }}
      </button>
    </div>

    <p v-if="loading" class="rpt__muted">載入中…</p>
    <p v-else-if="error" class="rpt__error">{{ error }}</p>

    <div v-else class="card">
      <div class="rpt__table-wrap">
        <table class="data-table rpt__table">
          <thead>
            <tr>
              <th class="text-center">訂單類型</th>
              <th class="text-center">出貨倉</th>
              <th class="text-center">訂單編號</th>
              <th class="text-center">訂單日期</th>
              <th>購買人姓名</th>
              <th class="text-center">購買人電話</th>
              <th>收件人</th>
              <th class="text-center">付款方式</th>
              <th class="num-th">總金額</th>
              <th class="text-center">付款狀態</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(o, i) in data?.orders ?? []" :key="o.ordercode + '-' + i" class="data-table__row">
              <td class="text-center">
                <span class="badge" :class="ORDER_TYPE_CLASS[o.ordertype]">{{ ORDER_TYPE_LABEL[o.ordertype] ?? o.ordertype }}</span>
              </td>
              <td class="text-center">{{ o.warehouseTitle || '—' }}</td>
              <td class="text-center font-mono">{{ o.ordercode }}</td>
              <td class="text-center">{{ fmtDate(o.orderdate) }}</td>
              <td>{{ o.memberName }}</td>
              <td class="text-center">{{ o.memberMobile || '—' }}</td>
              <td>{{ o.recivername }}</td>
              <td class="text-center">{{ PAY_TYPE_LABEL[o.paytype] ?? o.paytype }}</td>
              <td class="num-cell font-semibold">{{ fmtInt(o.amount) }}</td>
              <td class="text-center">
                <span class="badge" :class="PAY_STATUS_CLASS[o.paystatus]">{{ PAY_STATUS_LABEL[o.paystatus] ?? o.paystatus }}</span>
              </td>
            </tr>
            <tr v-if="!data || data.orders.length === 0">
              <td colspan="10" class="empty-cell">查無資料</td>
            </tr>
          </tbody>
          <tfoot v-if="data && data.orders.length > 0">
            <tr class="total-row">
              <td colspan="8" class="text-right total-label">總計</td>
              <td class="num-cell total-val">{{ fmtInt(data.grandTotal) }}</td>
              <td></td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  </main>
</template>

<style scoped>
.rpt {}
.rpt__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.rpt__title  { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.rpt__error  { color: #dc3545; }
.rpt__muted  { color: var(--tf-color-muted); }

/* ── 篩選列 ── */
.rpt__filters { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; align-items: center; }
.filter-input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  font-size: 0.875rem; font-family: inherit; background: #fff;
  transition: border-color 0.15s;
}
.filter-input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.filter-input--date { flex: 0 0 auto; width: 9rem; }
.filter-sep { color: var(--tf-color-muted); font-size: 0.85rem; }
.filter-select {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  background: #fff; font-size: 0.875rem; cursor: pointer; font-family: inherit;
  transition: border-color 0.15s;
}
.filter-select:focus { outline: none; border-color: var(--tf-color-primary); }

/* ── 表格卡片 ── */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.rpt__table-wrap { overflow-x: auto; }
.rpt__table { min-width: 960px; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th {
  background: var(--tf-color-primary);
  color: #fff; text-align: left;
  padding: 0.65rem 0.75rem;
  font-size: 0.875rem; font-weight: 600; white-space: nowrap;
}
.data-table td {
  padding: 0.65rem 0.9rem;
  border-bottom: 1px solid var(--tf-color-border);
  vertical-align: middle; color: #334155;
}
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.num-th  { text-align: right; }
.num-cell { text-align: right; white-space: nowrap; }
.text-center { text-align: center; }
.text-right  { text-align: right; }
.font-mono    { font-family: 'IBM Plex Mono', monospace; }
.font-semibold{ font-weight: 600; }

/* ── 總計列 ── */
.total-row td { border-top: 2px solid var(--tf-color-border); background: #f8fafc; }
.total-label  { color: #475569; font-weight: 600; }
.total-val    { color: var(--tf-color-primary-dark); font-weight: 700; }

/* ── Badge ── */
.badge {
  display: inline-block;
  padding: 0.2em 0.5em; border-radius: 3px;
  font-size: 0.78rem; font-weight: 500; white-space: nowrap;
}
.badge--paid     { background: #d4edda; color: #155724; }
.badge--unpaid   { background: #fff3cd; color: #856404; }
.badge--returned { background: #fce8d5; color: #7d3900; }
.badge--queue    { background: #cce5ff; color: #004085; }
.badge--canceled { background: #f8d7da; color: #721c24; }
.badge--otype-online  { background: #cfe2ff; color: #084298; }
.badge--otype-offline { background: #fff3cd; color: #856404; }
.badge--otype-self    { background: #d4edda; color: #155724; }
.badge--otype-pre     { background: #f8d7da; color: #721c24; }
.badge--otype-pr      { background: #cff4fc; color: #055160; }

/* ── 按鈕 ── */
.btn {
  display: inline-flex; align-items: center; justify-content: center;
  padding: 0.45rem 1rem;
  border: 1px solid transparent; border-radius: 4px;
  cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit;
  transition: opacity 0.15s, background 0.15s; white-space: nowrap;
}
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }
</style>

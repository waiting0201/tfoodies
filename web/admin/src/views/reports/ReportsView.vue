<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { apiFetch } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────
interface SalesItem {
  productId: string
  title: string
  capacity: string
  totalQty: number
  totalAmount: number
}

interface SalesReport {
  year: number
  month: number
  items: SalesItem[]
}

interface OrderItem {
  ordercode: string
  orderdate: string
  total: number
  freight: number
  discount: number
  paystatus: number | string
  deliverstatus: number | string
  memberName: string
}

interface AmountsReport {
  totalOrders: number
  totalAmount: number
  totalFreight: number
  totalDiscount: number
  orders: OrderItem[]
}

// ─── helpers ──────────────────────────────────────────────────────────────────
function fmtDate(d: string) {
  return d ? d.slice(0, 10) : '—'
}

function fmtMoney(n: number) {
  return n?.toLocaleString('zh-TW', { style: 'currency', currency: 'TWD', maximumFractionDigits: 0 }) ?? '—'
}

function fmtNum(n: number) {
  return n?.toLocaleString() ?? '—'
}

const PAY_STATUS_LABEL: Record<string | number, string> = {
  0: '待付款', 1: '已付款', 2: '退款',
}
const PAY_STATUS_CLASS: Record<string | number, string> = {
  0: 'badge--pending', 1: 'badge--paid', 2: 'badge--refund',
}

// ─── tab ──────────────────────────────────────────────────────────────────────
type Tab = 'sales' | 'amounts'
const activeTab = ref<Tab>('sales')

function switchTab(t: Tab) {
  activeTab.value = t
}

// ─── current year/month helpers ───────────────────────────────────────────────
const now = new Date()
const YEARS = Array.from({ length: 5 }, (_, i) => now.getFullYear() - i)
const MONTHS = Array.from({ length: 12 }, (_, i) => i + 1)

// ─── Sales tab state ──────────────────────────────────────────────────────────
const salesYear = ref(now.getFullYear())
const salesMonth = ref(now.getMonth() + 1)
const salesLoading = ref(false)
const salesError = ref('')
const salesData = ref<SalesReport | null>(null)

const salesTotal = computed(() => {
  if (!salesData.value) return { qty: 0, amount: 0 }
  return salesData.value.items.reduce(
    (acc, item) => ({ qty: acc.qty + item.totalQty, amount: acc.amount + item.totalAmount }),
    { qty: 0, amount: 0 }
  )
})

async function querySales() {
  salesLoading.value = true
  salesError.value = ''
  try {
    salesData.value = await apiFetch<SalesReport>(
      `/admin/reports/sales?year=${salesYear.value}&month=${salesMonth.value}`
    )
  } catch (e: any) {
    salesError.value = e.message ?? '查詢失敗'
  } finally {
    salesLoading.value = false
  }
}

// ─── Amounts tab state ────────────────────────────────────────────────────────
const amtStartDate = ref(`${now.getFullYear()}-01-01`)
const amtEndDate = ref(now.toISOString().slice(0, 10))
const amtPayStatus = ref('')
const amtLoading = ref(false)
const amtError = ref('')
const amtData = ref<AmountsReport | null>(null)

const PAY_FILTER_OPTIONS = [
  { label: '全部', value: '' },
  { label: '待付款', value: '0' },
  { label: '已付款', value: '1' },
  { label: '退款', value: '2' },
]

async function queryAmounts() {
  amtLoading.value = true
  amtError.value = ''
  try {
    const qs = new URLSearchParams({
      startDate: amtStartDate.value,
      endDate: amtEndDate.value,
    })
    if (amtPayStatus.value !== '') qs.set('payStatus', amtPayStatus.value)
    amtData.value = await apiFetch<AmountsReport>(`/admin/reports/amounts?${qs}`)
  } catch (e: any) {
    amtError.value = e.message ?? '查詢失敗'
  } finally {
    amtLoading.value = false
  }
}

onMounted(querySales)
</script>

<template>
  <main class="rpt">
    <h1 class="rpt__title">銷售報表</h1>

    <!-- Tab bar -->
    <div class="tabs" role="tablist">
      <button
        v-for="t in ([['sales', '商品銷量'], ['amounts', '訂單金額']] as [Tab, string][])"
        :key="t[0]"
        class="tabs__btn"
        :class="{ 'tabs__btn--active': activeTab === t[0] }"
        role="tab"
        :aria-selected="activeTab === t[0]"
        @click="switchTab(t[0])"
      >{{ t[1] }}</button>
    </div>

    <!-- ── 商品銷量 Tab ─────────────────────────────────────────────────────── -->
    <section v-if="activeTab === 'sales'" class="tab-panel">
      <!-- Query controls -->
      <div class="query-bar card">
        <div class="query-field">
          <label class="query-label" for="sales-year">年份</label>
          <select id="sales-year" v-model="salesYear" class="query-select">
            <option v-for="y in YEARS" :key="y" :value="y">{{ y }} 年</option>
          </select>
        </div>
        <div class="query-field">
          <label class="query-label" for="sales-month">月份</label>
          <select id="sales-month" v-model="salesMonth" class="query-select">
            <option v-for="m in MONTHS" :key="m" :value="m">{{ m }} 月</option>
          </select>
        </div>
        <button class="btn btn--secondary" :disabled="salesLoading" @click="querySales">
          {{ salesLoading ? '查詢中…' : '查詢' }}
        </button>
      </div>

      <div v-if="salesLoading" class="state-msg">載入中…</div>
      <div v-else-if="salesError" class="state-msg state-msg--error">{{ salesError }}</div>
      <template v-else-if="salesData">
        <div class="card mt-4">
          <div class="result-header">
            <span class="result-title">
              {{ salesData.year }} 年 {{ salesData.month }} 月 — 商品銷量
            </span>
            <span class="result-count">共 {{ salesData.items.length }} 項商品</span>
          </div>
          <table class="data-table">
            <thead>
              <tr>
                <th>商品名稱</th>
                <th>規格</th>
                <th class="num-th">銷量（件）</th>
                <th class="num-th">銷售金額</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in salesData.items" :key="item.productId" class="data-table__row">
                <td>{{ item.title }}</td>
                <td class="text-muted text-sm">{{ item.capacity || '—' }}</td>
                <td class="num-cell font-mono">{{ fmtNum(item.totalQty) }}</td>
                <td class="num-cell font-mono">{{ fmtMoney(item.totalAmount) }}</td>
              </tr>

              <!-- Total row -->
              <tr v-if="salesData.items.length > 0" class="total-row">
                <td colspan="2" class="total-label">合計</td>
                <td class="num-cell font-mono total-val">{{ fmtNum(salesTotal.qty) }}</td>
                <td class="num-cell font-mono total-val">{{ fmtMoney(salesTotal.amount) }}</td>
              </tr>
              <tr v-if="salesData.items.length === 0">
                <td colspan="4" class="empty-cell">本月無銷售資料</td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </section>

    <!-- ── 訂單金額 Tab ─────────────────────────────────────────────────────── -->
    <section v-if="activeTab === 'amounts'" class="tab-panel">
      <!-- Query controls -->
      <div class="query-bar card">
        <div class="query-field">
          <label class="query-label" for="amt-start">起始日期</label>
          <input id="amt-start" v-model="amtStartDate" type="date" class="query-input" />
        </div>
        <div class="query-field">
          <label class="query-label" for="amt-end">截止日期</label>
          <input id="amt-end" v-model="amtEndDate" type="date" class="query-input" />
        </div>
        <div class="query-field">
          <label class="query-label" for="amt-pay">付款狀態</label>
          <select id="amt-pay" v-model="amtPayStatus" class="query-select">
            <option v-for="opt in PAY_FILTER_OPTIONS" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </div>
        <button class="btn btn--secondary" :disabled="amtLoading" @click="queryAmounts">
          {{ amtLoading ? '查詢中…' : '查詢' }}
        </button>
      </div>

      <div v-if="amtLoading" class="state-msg">載入中…</div>
      <div v-else-if="amtError" class="state-msg state-msg--error">{{ amtError }}</div>
      <template v-else-if="amtData">
        <!-- Stat cards -->
        <div class="stat-grid mt-4">
          <div class="stat-card">
            <div class="stat-card__label">訂單數</div>
            <div class="stat-card__value font-mono">{{ fmtNum(amtData.totalOrders) }}</div>
          </div>
          <div class="stat-card stat-card--indigo">
            <div class="stat-card__label">總金額</div>
            <div class="stat-card__value font-mono">{{ fmtMoney(amtData.totalAmount) }}</div>
          </div>
          <div class="stat-card stat-card--amber">
            <div class="stat-card__label">運費合計</div>
            <div class="stat-card__value font-mono">{{ fmtMoney(amtData.totalFreight) }}</div>
          </div>
          <div class="stat-card stat-card--green">
            <div class="stat-card__label">折扣合計</div>
            <div class="stat-card__value font-mono">{{ fmtMoney(amtData.totalDiscount) }}</div>
          </div>
        </div>

        <!-- Orders table -->
        <div class="card mt-4">
          <div class="result-header">
            <span class="result-title">訂單明細</span>
            <span class="result-count">共 {{ amtData.orders.length }} 筆</span>
          </div>
          <table class="data-table">
            <thead>
              <tr>
                <th>訂單編號</th>
                <th>訂單日期</th>
                <th>會員</th>
                <th class="num-th">金額</th>
                <th class="num-th">運費</th>
                <th class="num-th">折扣</th>
                <th>付款狀態</th>
                <th>出貨狀態</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="order in amtData.orders" :key="order.ordercode" class="data-table__row">
                <td class="font-mono text-sm">{{ order.ordercode }}</td>
                <td>{{ fmtDate(order.orderdate) }}</td>
                <td>{{ order.memberName }}</td>
                <td class="num-cell font-mono">{{ fmtMoney(order.total) }}</td>
                <td class="num-cell font-mono text-sm text-muted">{{ fmtMoney(order.freight) }}</td>
                <td class="num-cell font-mono text-sm">
                  <span v-if="order.discount" class="text-green">-{{ fmtMoney(order.discount) }}</span>
                  <span v-else class="text-muted">—</span>
                </td>
                <td>
                  <span class="badge" :class="PAY_STATUS_CLASS[order.paystatus] ?? 'badge--pending'">
                    {{ PAY_STATUS_LABEL[order.paystatus] ?? String(order.paystatus) }}
                  </span>
                </td>
                <td class="text-sm text-muted">{{ order.deliverstatus }}</td>
              </tr>
              <tr v-if="amtData.orders.length === 0">
                <td colspan="8" class="empty-cell">符合條件的訂單為空</td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </section>
  </main>
</template>

<style scoped>
.rpt { }
.rpt__title { font-size: 1.5rem; font-weight: 700; color: #1e293b; margin-bottom: 1.5rem; letter-spacing: -0.02em; }

/* Tabs */
.tabs { display: flex; gap: 0; border-bottom: 2px solid var(--tf-color-border); margin-bottom: 1.5rem; }
.tabs__btn { padding: 0.45rem 1rem; border: 1px solid var(--tf-color-border); border-bottom: none; border-radius: 4px 4px 0 0; background: #fff; cursor: pointer; font-size: 0.875rem; font-weight: 500; color: #495057; position: relative; bottom: -2px; transition: all 0.15s; }
.tabs__btn--active { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); font-weight: 500; }
.tabs__btn:not(.tabs__btn--active):hover { color: #334155; background: #f8fafc; }

.tab-panel { animation: fadeIn 0.15s ease; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(4px); } to { opacity: 1; transform: none; } }

/* Card */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }

/* Query bar */
.query-bar { display: flex; align-items: flex-end; gap: 1rem; padding: 1rem 1.25rem; flex-wrap: wrap; }
.query-field { display: flex; flex-direction: column; gap: 0.3rem; }
.query-label { font-size: 0.75rem; font-weight: 600; color: #64748b; }
.query-select, .query-input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem;
  color: #1e293b;
  background: #fff;
  min-width: 110px;
  transition: border-color 0.15s, box-shadow 0.15s;
}
.query-select:focus, .query-input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }

/* Stat cards */
.stat-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1rem; }
@media (max-width: 768px) { .stat-grid { grid-template-columns: repeat(2, 1fr); } }
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
.result-count { font-size: 0.8rem; color: #94a3b8; }

/* Tables */
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 1rem; border-bottom: 1px solid #e2e8f0; font-size: 0.875rem; font-weight: 600; }
.num-th { text-align: right; }
.data-table td { padding: 0.65rem 1rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.num-cell { text-align: right; }
.empty-cell { text-align: center; color: #94a3b8; padding: 3rem; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }
.text-muted { color: #94a3b8; }
.text-sm { font-size: 0.83rem; }
.text-green { color: #16a34a; font-weight: 500; }

/* Total row */
.total-row td { border-top: 2px solid #e2e8f0; background: #f8fafc; font-weight: 600; }
.total-label { color: #475569; }
.total-val { color: #1e293b; }

/* Badge */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.75rem; font-weight: 600; }
.badge--pending { background: #fef3c7; color: #92400e; }
.badge--paid    { background: #dcfce7; color: #166534; }
.badge--refund  { background: #fce7f3; color: #9d174d; }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: all 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }

/* State messages */
.state-msg { padding: 2rem; text-align: center; color: #94a3b8; }
.state-msg--error { color: #dc2626; }

/* Spacing */
.mt-4 { margin-top: 1rem; }
</style>

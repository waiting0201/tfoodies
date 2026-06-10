<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, apiDownload, ApiError } from '../../lib/apiClient'

interface Order {
  orderId: string
  code: string
  orderDate: string
  memberName: string
  memberMobile: string
  total: number
  freight: number
  discount: number
  payType: number
  payStatus: number
  deliverStatus: number
  atmCode: string | null
  atmExpiry: string | null
  receiverName: string
  receiverAddress: string
  createdAt: string
}

interface OrdersPage {
  items: Order[]
  total: number
  page: number
  pageSize: number
}

interface StatusTab {
  label: string
  value: number // -1 = 全部
}

const router = useRouter()

const STATUS_TABS: StatusTab[] = [
  { label: '全部', value: -1 },
  { label: '未出貨', value: 0 },
  { label: '待出貨', value: 4 },
  { label: '已出貨', value: 1 },
  { label: '已取消', value: 3 },
]

const filters = reactive({
  keyword: '',
  deliverStatus: -1,   // number; -1 = 全部
  payStatus: -1,       // number; -1 = 全部
  dateFrom: '',
  dateTo: '',
})

const page = ref(1)
const pageSize = 20
const data = ref<OrdersPage | null>(null)
const loading = ref(false)
const error = ref('')
const actionBusy = ref<string | null>(null)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams()
    if (filters.keyword) params.set('keyword', filters.keyword)
    if (filters.deliverStatus >= 0) params.set('deliverStatus', String(filters.deliverStatus))
    if (filters.payStatus >= 0) params.set('payStatus', String(filters.payStatus))
    if (filters.dateFrom) params.set('dateFrom', filters.dateFrom)
    if (filters.dateTo) params.set('dateTo', filters.dateTo)
    params.set('page', String(page.value))
    params.set('pageSize', String(pageSize))

    data.value = await apiFetch<OrdersPage>(`/admin/orders?${params}`)
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

function search() {
  page.value = 1
  load()
}

function selectTab(value: number) {
  filters.deliverStatus = value
  search()
}

function prevPage() {
  if (page.value > 1) { page.value--; load() }
}

function nextPage() {
  if (data.value && page.value * pageSize < data.value.total) { page.value++; load() }
}

function goToOrder(code: string) {
  router.push(`/admin/orders/${code}`)
}

const PAY_LABELS: Record<number, string> = { 0: '未付款', 1: '已付款' }
const DELIVER_LABELS: Record<number, string> = { 0: '未出貨', 1: '已出貨', 3: '已取消', 4: '待出貨' }

function payLabel(s: number) { return PAY_LABELS[s] ?? `${s}` }
function deliverLabel(s: number) { return DELIVER_LABELS[s] ?? `${s}` }

function formatDate(s: string | null | undefined) {
  if (!s) return '—'
  return new Date(s).toLocaleDateString('zh-TW')
}

function formatDateTime(s: string | null | undefined) {
  if (!s) return '—'
  return new Date(s).toLocaleString('zh-TW')
}

async function patchOrder(code: string, action: string, body?: object) {
  actionBusy.value = code
  error.value = ''
  try {
    await apiFetch(`/admin/orders/${code}/${action}`, {
      method: 'PATCH',
      body: body ? JSON.stringify(body) : undefined,
    })
    await load()
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
  } finally {
    actionBusy.value = null
  }
}

async function handlePending(code: string) {
  await patchOrder(code, 'pending')
}

async function handleShip(code: string) {
  // TODO: replace with modal dialog
  const tracking = prompt('請輸入物流追蹤號碼：')
  if (tracking === null) return
  await patchOrder(code, 'ship', { trackingNumber: tracking })
}

async function handleCancel(code: string) {
  if (!confirm(`確認取消訂單 ${code}？`)) return
  await patchOrder(code, 'cancel')
}

async function handlePay(code: string) {
  await patchOrder(code, 'pay')
}

// ── 匯出 / 揀貨單 ──────────────────────────────────────────────────────
const selectedIds = ref<Set<string>>(new Set())

function toggleSelect(orderId: string) {
  const next = new Set(selectedIds.value)
  if (next.has(orderId)) next.delete(orderId)
  else next.add(orderId)
  selectedIds.value = next
}

function currentFilterQuery(): string {
  const params = new URLSearchParams()
  if (filters.keyword) params.set('keyword', filters.keyword)
  if (filters.deliverStatus >= 0) params.set('deliverStatus', String(filters.deliverStatus))
  if (filters.payStatus >= 0) params.set('payStatus', String(filters.payStatus))
  if (filters.dateFrom) params.set('dateFrom', filters.dateFrom)
  if (filters.dateTo) params.set('dateTo', filters.dateTo)
  return params.toString()
}

async function exportExcel(category: 'tfoodies' | 'shopcom') {
  error.value = ''
  try {
    const q = currentFilterQuery()
    await apiDownload(`/admin/orders/export?category=${category}&${q}`, `${category}_export.xlsx`)
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '匯出失敗'
  }
}

async function exportPicking() {
  error.value = ''
  if (selectedIds.value.size === 0) { error.value = '請先勾選要揀貨的訂單'; return }
  try {
    const ids = Array.from(selectedIds.value).join(',')
    await apiDownload(`/admin/orders/picking?orderIds=${encodeURIComponent(ids)}`, 'shipment.xlsx')
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '匯出失敗'
  }
}

onMounted(load)
</script>

<template>
  <div>
    <!-- Page header -->
    <div class="orders__header">
      <h1 class="orders__title">訂單管理</h1>
      <div class="orders__header-actions">
        <button class="orders__btn orders__btn--secondary" @click="exportExcel('tfoodies')">匯出 Excel</button>
        <button class="orders__btn orders__btn--secondary" @click="exportExcel('shopcom')">美安報表</button>
        <button
          v-if="filters.deliverStatus === 4"
          class="orders__btn orders__btn--secondary"
          @click="exportPicking"
        >匯出揀貨單{{ selectedIds.size ? `（${selectedIds.size}）` : '' }}</button>
        <RouterLink to="/admin/orders/new" class="orders__btn orders__btn--primary">
          + 新增訂單
        </RouterLink>
      </div>
    </div>

    <!-- Status tabs -->
    <div class="orders__tabs">
      <button
        v-for="tab in STATUS_TABS"
        :key="tab.value"
        :class="['orders__tab', { 'orders__tab--active': filters.deliverStatus === tab.value }]"
        @click="selectTab(tab.value)"
      >{{ tab.label }}</button>
    </div>

    <!-- Filter bar -->
    <div class="orders__filters">
      <input
        v-model="filters.keyword"
        class="orders__input"
        placeholder="搜尋訂單編號 / 會員 / 收件人"
        @keyup.enter="search"
      />
      <input
        v-model="filters.dateFrom"
        type="date"
        class="orders__input orders__input--date"
        title="起始日期"
        @change="search"
      />
      <span class="orders__date-sep">—</span>
      <input
        v-model="filters.dateTo"
        type="date"
        class="orders__input orders__input--date"
        title="結束日期"
        @change="search"
      />
      <select v-model="filters.payStatus" class="orders__select" @change="search">
        <option :value="-1">付款：全部</option>
        <option :value="0">未付款</option>
        <option :value="1">已付款</option>
      </select>
      <button class="orders__btn orders__btn--secondary" @click="search">搜尋</button>
    </div>

    <!-- Error -->
    <p v-if="error" class="orders__error">{{ error }}</p>

    <!-- Loading -->
    <p v-if="loading" class="orders__muted">載入中…</p>

    <!-- Table -->
    <template v-else-if="data">
      <div class="orders__card">
        <div class="orders__table-wrap">
          <table class="orders__table">
            <thead>
              <tr>
                <th v-if="filters.deliverStatus === 4" style="width:32px"></th>
                <th>訂單編號</th>
                <th>訂單日期</th>
                <th>會員</th>
                <th>收件人</th>
                <th>總額</th>
                <th>付款</th>
                <th>出貨狀態</th>
                <th>ATM 到期</th>
                <th>操作</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="o in data.items" :key="o.code" class="orders__row">
                <td v-if="filters.deliverStatus === 4">
                  <input type="checkbox" :checked="selectedIds.has(o.orderId)" @change="toggleSelect(o.orderId)" />
                </td>
                <td>
                  <button class="orders__link" @click="goToOrder(o.code)">{{ o.code }}</button>
                </td>
                <td class="orders__date">{{ formatDate(o.orderDate) }}</td>
                <td>
                  <div>{{ o.memberName }}</div>
                  <div class="orders__sub">{{ o.memberMobile }}</div>
                </td>
                <td>
                  <div>{{ o.receiverName }}</div>
                  <div class="orders__sub orders__sub--addr">{{ o.receiverAddress }}</div>
                </td>
                <td class="orders__amount">NT$ {{ o.total.toLocaleString() }}</td>
                <td>
                  <span :class="['orders__badge', o.payStatus === 1 ? 'orders__badge--paid' : 'orders__badge--unpaid']">
                    {{ payLabel(o.payStatus) }}
                  </span>
                </td>
                <td>
                  <span :class="['orders__badge', `orders__badge--deliver-${o.deliverStatus}`]">
                    {{ deliverLabel(o.deliverStatus) }}
                  </span>
                </td>
                <td class="orders__atm">
                  <template v-if="o.atmExpiry">
                    <div class="orders__atm-code">{{ o.atmCode ?? '—' }}</div>
                    <div class="orders__sub">{{ formatDateTime(o.atmExpiry) }}</div>
                  </template>
                  <template v-else>—</template>
                </td>
                <td class="orders__actions">
                  <button
                    v-if="o.deliverStatus === 0"
                    class="orders__btn orders__btn--sm orders__btn--secondary"
                    :disabled="actionBusy === o.code"
                    @click="handlePending(o.code)"
                  >待出貨</button>
                  <button
                    v-if="o.deliverStatus === 4"
                    class="orders__btn orders__btn--sm orders__btn--primary"
                    :disabled="actionBusy === o.code"
                    @click="handleShip(o.code)"
                  >出貨</button>
                  <button
                    v-if="o.deliverStatus !== 3 && o.deliverStatus !== 1"
                    class="orders__btn orders__btn--sm orders__btn--danger"
                    :disabled="actionBusy === o.code"
                    @click="handleCancel(o.code)"
                  >取消</button>
                  <button
                    v-if="o.payStatus === 0"
                    class="orders__btn orders__btn--sm orders__btn--accent"
                    :disabled="actionBusy === o.code"
                    @click="handlePay(o.code)"
                  >標記付款</button>
                </td>
              </tr>
              <tr v-if="data.items.length === 0">
                <td :colspan="filters.deliverStatus === 4 ? 10 : 9" class="orders__empty">無訂單資料</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Pagination -->
      <div class="orders__pagination">
        <button class="orders__btn orders__btn--sm orders__btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
        <span class="orders__page-info">第 {{ page }} 頁（共 {{ data.total }} 筆）</span>
        <button class="orders__btn orders__btn--sm orders__btn--ghost" :disabled="page * pageSize >= data.total" @click="nextPage">下一頁</button>
      </div>
    </template>
  </div>
</template>

<style scoped>
/* Root element: no extra padding or max-width — AdminLayout's <main class="p-6"> handles spacing */

.orders__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.25rem;
}

.orders__header-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.orders__title {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  margin: 0;
  font-size: 1.25rem;
}

/* Status tabs */
.orders__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  margin-bottom: 1rem;
  border-bottom: 2px solid var(--tf-color-border);
  padding-bottom: 0;
}

.orders__tab {
  padding: 0.45rem 1rem;
  border: 1px solid var(--tf-color-border);
  border-bottom: none;
  border-radius: 4px 4px 0 0;
  background: #fff;
  color: #495057;
  cursor: pointer;
  font-size: 0.875rem;
  transition: background 0.15s, color 0.15s;
  position: relative;
  bottom: -2px;
}

.orders__tab:hover:not(.orders__tab--active) {
  background: #f1f3f5;
}

.orders__tab--active {
  background: var(--tf-color-primary);
  color: #fff;
  border-color: var(--tf-color-primary);
  font-weight: 500;
}

/* Filter bar */
.orders__filters {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-bottom: 1rem;
  align-items: center;
}

.orders__input {
  flex: 1 1 200px;
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem;
  font-family: inherit;
  transition: border-color 0.15s;
}

.orders__input--date {
  flex: 0 0 auto;
  width: 9rem;
}

.orders__date-sep {
  color: var(--tf-color-muted);
  font-size: 0.85rem;
  padding: 0 0.1rem;
}

.orders__select {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  background: #fff;
  font-size: 0.875rem;
  cursor: pointer;
  font-family: inherit;
  transition: border-color 0.15s;
}

.orders__input:focus,
.orders__select:focus {
  outline: none;
  border-color: var(--tf-color-primary);
  box-shadow: 0 0 0 2px rgba(38,183,188,0.15);
}

/* Card wrapper */
.orders__card {
  background: #fff;
  border-radius: 10px;
  border: 1px solid var(--tf-color-border);
  overflow: hidden;
  margin-bottom: 1rem;
}

/* Table */
.orders__table-wrap {
  overflow-x: auto;
}

.orders__table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.orders__table th {
  background: var(--tf-color-primary);
  color: #fff;
  padding: 0.65rem 0.75rem;
  text-align: left;
  white-space: nowrap;
}

.orders__table td {
  padding: 0.6rem 0.75rem;
  border-bottom: 1px solid var(--tf-color-border);
  vertical-align: middle;
}

.orders__row:hover td {
  background: #f8faf8;
}

.orders__link {
  background: none;
  border: none;
  color: var(--tf-color-primary);
  cursor: pointer;
  padding: 0;
  font-size: inherit;
  text-decoration: underline;
  font-family: inherit;
}

.orders__link:hover {
  color: var(--tf-color-primary-dark);
}

.orders__date {
  white-space: nowrap;
  color: var(--tf-color-muted);
}

.orders__amount {
  white-space: nowrap;
  font-weight: 500;
}

/* Secondary text lines within a cell */
.orders__sub {
  font-size: 0.78rem;
  color: var(--tf-color-muted);
  margin-top: 0.1rem;
}

.orders__sub--addr {
  max-width: 16rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* ATM cell */
.orders__atm-code {
  font-family: monospace;
  font-size: 0.82rem;
}

.orders__actions {
  display: flex;
  gap: 0.35rem;
  flex-wrap: wrap;
  min-width: 5rem;
}

.orders__empty {
  text-align: center;
  color: var(--tf-color-muted);
  padding: 2rem 0;
}

/* Badges */
.orders__badge {
  display: inline-block;
  padding: 0.2rem 0.5rem;
  border-radius: 3px;
  font-size: 0.78rem;
  white-space: nowrap;
}

.orders__badge--paid     { background: #d4edda; color: #155724; }
.orders__badge--unpaid   { background: #fff3cd; color: #856404; }
.orders__badge--deliver-0 { background: #e2e3e5; color: #383d41; }
.orders__badge--deliver-4 { background: #cce5ff; color: #004085; }
.orders__badge--deliver-1 { background: #d4edda; color: #155724; }
.orders__badge--deliver-3 { background: #f8d7da; color: #721c24; }

/* Buttons */
.orders__btn {
  padding: 0.45rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.875rem;
  font-family: inherit;
  transition: opacity 0.15s, background 0.15s;
}
.orders__btn:disabled { opacity: 0.5; cursor: not-allowed; }

.orders__btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }

.orders__btn--primary { background: var(--tf-color-primary); color: #fff; }
.orders__btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); }

.orders__btn--secondary { background: #e9ecef; color: #495057; }
.orders__btn--secondary:hover:not(:disabled) { background: #dee2e6; }

.orders__btn--danger { background: #dc3545; color: #fff; }
.orders__btn--danger:hover:not(:disabled) { background: #b02a37; }

.orders__btn--accent { background: var(--tf-color-accent); color: #fff; }
.orders__btn--accent:hover:not(:disabled) { opacity: 0.85; }

.orders__btn--ghost { background: transparent; border: 1px solid var(--tf-color-border); color: #495057; }
.orders__btn--ghost:hover:not(:disabled) { background: #f1f3f5; }

/* Pagination */
.orders__pagination {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-top: 1rem;
  justify-content: flex-end;
}

.orders__page-info {
  font-size: 0.875rem;
  color: var(--tf-color-muted);
}

.orders__error {
  color: #dc3545;
  margin-bottom: 0.75rem;
}

.orders__muted {
  color: var(--tf-color-muted);
}
</style>

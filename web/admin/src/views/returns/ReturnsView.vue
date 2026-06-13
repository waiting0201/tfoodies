<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { apiFetch } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────
interface ReturnItem {
  returnId: string
  returnCode: string
  orderCode: string
  returnDate: string
  memberName: string
  memberMobile: string
  refundStatus: number    // EnumRefundStatus
  receiveStatus: number   // EnumReceiveStatus
  warehouseStatus: number // EnumWarehouseStatus
}
interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

// ─── list state ───────────────────────────────────────────────────────────────
const items = ref<ReturnItem[]>([])
const loading = ref(false)
const error = ref('')
const page = ref(1)
const total = ref(0)
const PAGE_SIZE = 20
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / PAGE_SIZE)))

// ─── filter ───────────────────────────────────────────────────────────────────
const filterReceive = ref<'' | '0' | '1' | '2' | '3'>('')

// ─── 狀態列舉（對齊舊系統 Enum.cs） ──────────────────────────────────────────────
const RECEIVE: Record<number, { label: string; cls: string }> = {
  0: { label: '退貨中', cls: 'badge--warning' },
  1: { label: '已到達', cls: 'badge--default' },
  2: { label: '取消',   cls: 'badge--danger' },
  3: { label: '免退回', cls: 'badge--success' },
}
const REFUND: Record<number, { label: string; cls: string }> = {
  0: { label: '未退款', cls: 'badge--warning' },
  1: { label: '已退款', cls: 'badge--default' },
  2: { label: '折讓',   cls: 'badge--primary' },
  3: { label: '免退款', cls: 'badge--success' },
  4: { label: '取消',   cls: 'badge--danger' },
}
const WAREHOUSE: Record<number, { label: string; cls: string }> = {
  0: { label: '未入庫', cls: 'badge--warning' },
  1: { label: '已入庫', cls: 'badge--default' },
}

function fmtDate(d: string) { return d ? d.slice(0, 10) : '—' }

// ─── data loading ─────────────────────────────────────────────────────────────
async function loadItems() {
  loading.value = true; error.value = ''
  try {
    let qs = `page=${page.value}&pageSize=${PAGE_SIZE}`
    if (filterReceive.value !== '') qs += `&receivestatus=${filterReceive.value}`
    const d = await apiFetch<PagedResult<ReturnItem> | ReturnItem[]>(`/admin/returns?${qs}`)
    if (Array.isArray(d)) {
      items.value = d
      total.value = d.length
    } else {
      items.value = (d as PagedResult<ReturnItem>).items ?? []
      total.value = (d as PagedResult<ReturnItem>).totalCount ?? 0
    }
  } catch (e: any) { error.value = e.message ?? '載入失敗' }
  finally { loading.value = false }
}

function applyFilter() {
  page.value = 1
  loadItems()
}

watch(page, loadItems)

onMounted(loadItems)
</script>

<template>
  <main class="returns">
    <div class="returns__header">
      <h1 class="returns__title">退貨管理</h1>
      <RouterLink to="/admin/returns/new" class="btn btn--primary">+ 新增退貨單</RouterLink>
    </div>

    <!-- Filter bar -->
    <div class="returns__filters">
      <select v-model="filterReceive" class="filter-select" @change="applyFilter">
        <option value="">全部收貨狀態</option>
        <option value="0">退貨中</option>
        <option value="1">已到達</option>
        <option value="2">取消</option>
        <option value="3">免退回</option>
      </select>
      <button class="btn btn--secondary" @click="applyFilter">搜尋</button>
    </div>

    <!-- Loading / error -->
    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="error" class="state-msg state-msg--error">{{ error }}</div>

    <template v-else>
      <div class="card">
        <table class="data-table">
          <thead>
            <tr>
              <th>退貨編號</th>
              <th>訂單編號</th>
              <th>退貨日期</th>
              <th>退貨人姓名</th>
              <th>退貨人電話</th>
              <th>退款狀態</th>
              <th>收貨狀態</th>
              <th>入庫狀態</th>
              <th class="action-th"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in items" :key="r.returnId" class="data-table__row">
              <td class="code-cell">{{ r.returnCode }}</td>
              <td class="code-cell">{{ r.orderCode }}</td>
              <td>{{ fmtDate(r.returnDate) }}</td>
              <td>{{ r.memberName }}</td>
              <td>{{ r.memberMobile || '—' }}</td>
              <td><span class="badge" :class="REFUND[r.refundStatus]?.cls">{{ REFUND[r.refundStatus]?.label ?? '—' }}</span></td>
              <td><span class="badge" :class="RECEIVE[r.receiveStatus]?.cls">{{ RECEIVE[r.receiveStatus]?.label ?? '—' }}</span></td>
              <td><span class="badge" :class="WAREHOUSE[r.warehouseStatus]?.cls">{{ WAREHOUSE[r.warehouseStatus]?.label ?? '—' }}</span></td>
              <td class="action-cell">
                <RouterLink :to="`/admin/returns/${r.returnId}/edit`" class="btn btn--sm btn--ghost">編輯</RouterLink>
              </td>
            </tr>
            <tr v-if="items.length === 0">
              <td colspan="9" class="empty-cell">沒有符合的退貨資料</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="pagination">
        <button class="btn btn--ghost btn--sm" :disabled="page <= 1" @click="page--">‹ 上一頁</button>
        <span class="pagination__info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
        <button class="btn btn--ghost btn--sm" :disabled="page >= totalPages" @click="page++">下一頁 ›</button>
      </div>
    </template>
  </main>
</template>

<style scoped>
.returns { }
.returns__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.returns__title { font-family: var(--tf-font-heading, inherit); color: var(--tf-color-primary-dark); margin: 0; }

/* Filter bar */
.returns__filters { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; align-items: center; }
.filter-select {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  background: #fff; font-size: 0.875rem; cursor: pointer; font-family: inherit;
  transition: border-color 0.15s;
}
.filter-select:focus { outline: none; border-color: var(--tf-color-primary); }

/* Card wrapper */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }

/* Table */
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 80px; }
.data-table td { padding: 0.65rem 0.75rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.code-cell { font-family: 'IBM Plex Mono', monospace; font-size: 0.85rem; }
.action-cell { white-space: nowrap; text-align: right; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }

/* Badges */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 600; }
.badge--warning { background: #fff3cd; color: #856404; }
.badge--default { background: #e9ecef; color: #495057; }
.badge--success { background: #d4edda; color: #155724; }
.badge--primary { background: #cce5ff; color: #004085; }
.badge--danger  { background: #f8d7da; color: #721c24; }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: opacity 0.15s, background 0.15s; white-space: nowrap; text-decoration: none; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: #f0f5f1; }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }

/* Pagination */
.pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.pagination__info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* State messages */
.state-msg { padding: 2rem; text-align: center; color: var(--tf-color-muted); }
.state-msg--error { color: #c0392b; }
</style>

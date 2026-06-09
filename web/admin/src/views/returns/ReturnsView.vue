<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { apiFetch } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────
interface ReturnItem {
  returnId: string
  returnCode: string
  memberName: string
  orderCode: string
  returnDate: string
  receiveStatus: number   // 0 未收 / 1 已收
  refundStatus: number    // 0 未退 / 1 已退
}
interface ReturnDetail extends ReturnItem {
  reason?: string
  items?: { productName: string; qty: number; price: number }[]
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
const filterReceive = ref<'' | '0' | '1'>('')

// ─── inline detail ────────────────────────────────────────────────────────────
const expandedId = ref<string | null>(null)
const detail = ref<ReturnDetail | null>(null)
const detailLoading = ref(false)
const detailError = ref('')

// ─── action state ──────────────────────────────────────────────────────────────
const actionLoading = ref<string | null>(null)  // stores returnId of in-flight action
const actionError = ref('')

// ─── helpers ──────────────────────────────────────────────────────────────────
const RECEIVE_LABEL: Record<number, string> = { 0: '未收到', 1: '已收到' }
const RECEIVE_CLASS: Record<number, string> = { 0: 'badge--pending', 1: 'badge--received' }
const REFUND_LABEL: Record<number, string> = { 0: '未退款', 1: '已退款' }
const REFUND_CLASS: Record<number, string> = { 0: 'badge--pending', 1: 'badge--refunded' }

function fmtDate(d: string) { return d ? d.slice(0, 10) : '—' }
function fmtMoney(n: number) {
  return n?.toLocaleString('zh-TW', { style: 'currency', currency: 'TWD', maximumFractionDigits: 0 }) ?? '—'
}

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

// ─── row expand ───────────────────────────────────────────────────────────────
async function toggleRow(id: string) {
  if (expandedId.value === id) { expandedId.value = null; detail.value = null; return }
  expandedId.value = id
  detail.value = null; detailError.value = ''
  detailLoading.value = true
  try {
    detail.value = await apiFetch<ReturnDetail>(`/admin/returns/${id}`)
  } catch (e: any) { detailError.value = e.message ?? '載入失敗' }
  finally { detailLoading.value = false }
}

// ─── actions ──────────────────────────────────────────────────────────────────
async function confirmReceive(id: string) {
  actionLoading.value = id; actionError.value = ''
  try {
    await apiFetch(`/admin/returns/${id}/receive`, { method: 'PATCH' })
    await loadItems()
    if (expandedId.value === id) {
      detail.value = await apiFetch<ReturnDetail>(`/admin/returns/${id}`)
    }
  } catch (e: any) { actionError.value = e.message ?? '操作失敗' }
  finally { actionLoading.value = null }
}

async function doRefund(id: string) {
  actionLoading.value = id; actionError.value = ''
  try {
    await apiFetch(`/admin/returns/${id}/refund`, { method: 'PATCH' })
    await loadItems()
    if (expandedId.value === id) {
      detail.value = await apiFetch<ReturnDetail>(`/admin/returns/${id}`)
    }
  } catch (e: any) { actionError.value = e.message ?? '退款失敗' }
  finally { actionLoading.value = null }
}

onMounted(loadItems)
</script>

<template>
  <main class="returns">
    <h1 class="returns__title">退貨管理</h1>

    <!-- Filter bar -->
    <div class="filter-bar">
      <label class="filter-bar__item">
        收貨狀態
        <select v-model="filterReceive" @change="applyFilter">
          <option value="">全部</option>
          <option value="0">未收到</option>
          <option value="1">已收到</option>
        </select>
      </label>
      <button class="btn btn--ghost btn--sm" @click="applyFilter">套用篩選</button>
    </div>

    <!-- Error banner -->
    <div v-if="actionError" class="alert alert--error">{{ actionError }}</div>

    <!-- Loading / error -->
    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="error" class="state-msg state-msg--error">{{ error }}</div>

    <template v-else>
      <div class="card">
        <table class="data-table">
          <thead>
            <tr>
              <th>退貨單號</th>
              <th>會員</th>
              <th>訂單</th>
              <th>日期</th>
              <th>收貨狀態</th>
              <th>退款狀態</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <template v-for="r in items" :key="r.returnId">
              <tr class="data-table__row" :class="{ 'data-table__row--expanded': expandedId === r.returnId }" @click="toggleRow(r.returnId)">
                <td class="code-cell">{{ r.returnCode }}</td>
                <td>{{ r.memberName }}</td>
                <td>{{ r.orderCode }}</td>
                <td>{{ fmtDate(r.returnDate) }}</td>
                <td><span class="badge" :class="RECEIVE_CLASS[r.receiveStatus]">{{ RECEIVE_LABEL[r.receiveStatus] }}</span></td>
                <td><span class="badge" :class="REFUND_CLASS[r.refundStatus]">{{ REFUND_LABEL[r.refundStatus] }}</span></td>
                <td class="action-cell" @click.stop>
                  <button
                    v-if="r.receiveStatus === 0"
                    class="btn btn--ghost btn--sm"
                    :disabled="actionLoading === r.returnId"
                    @click="confirmReceive(r.returnId)"
                  >{{ actionLoading === r.returnId ? '…' : '確認收到' }}</button>
                  <button
                    v-if="r.receiveStatus === 1 && r.refundStatus === 0"
                    class="btn btn--accent btn--sm"
                    :disabled="actionLoading === r.returnId"
                    @click="doRefund(r.returnId)"
                  >{{ actionLoading === r.returnId ? '…' : '退款' }}</button>
                </td>
              </tr>

              <!-- Inline detail row -->
              <tr v-if="expandedId === r.returnId" class="detail-row">
                <td colspan="7">
                  <div class="detail-panel">
                    <div v-if="detailLoading" class="state-msg state-msg--sm">載入中…</div>
                    <div v-else-if="detailError" class="state-msg state-msg--error state-msg--sm">{{ detailError }}</div>
                    <template v-else-if="detail">
                      <div v-if="detail.reason" class="detail-reason">
                        <strong>退貨原因：</strong>{{ detail.reason }}
                      </div>
                      <table v-if="detail.items?.length" class="sub-table">
                        <thead>
                          <tr><th>商品</th><th>數量</th><th>單價</th><th>小計</th></tr>
                        </thead>
                        <tbody>
                          <tr v-for="(item, i) in detail.items" :key="i">
                            <td>{{ item.productName }}</td>
                            <td>{{ item.qty }}</td>
                            <td>{{ fmtMoney(item.price) }}</td>
                            <td>{{ fmtMoney(item.qty * item.price) }}</td>
                          </tr>
                        </tbody>
                      </table>
                      <p v-else class="sub-empty">無商品明細</p>
                    </template>
                  </div>
                </td>
              </tr>
            </template>
            <tr v-if="items.length === 0">
              <td colspan="7" class="empty-cell">沒有符合的退貨資料</td>
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
.returns__title { font-family: var(--tf-font-heading, inherit); color: var(--tf-color-primary-dark); margin-bottom: 1.5rem; }

/* Filter bar */
.filter-bar { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.25rem; flex-wrap: wrap; }
.filter-bar__item { display: flex; align-items: center; gap: 0.5rem; font-size: 0.87rem; color: #444; }
.filter-bar__item select { padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; }

/* Alert */
.alert { padding: 0.7rem 1rem; border-radius: 4px; margin-bottom: 1rem; font-size: 0.88rem; }
.alert--error { background: #fde8e8; color: #c0392b; border: 1px solid #f5c6c6; }

/* Card wrapper */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }

/* Table */
.data-table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; border-bottom: 2px solid var(--tf-color-border); font-size: 0.875rem; }
.data-table td { padding: 0.6rem 0.75rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; }
.data-table__row { cursor: pointer; transition: background 0.1s; }
.data-table__row:hover { background: #f8faf8; }
.data-table__row--expanded { background: #edf5ef; }
.code-cell { font-family: monospace; font-size: 0.85rem; }
.action-cell { white-space: nowrap; text-align: right; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2rem; }

/* Detail panel */
.detail-row > td { padding: 0; }
.detail-panel { background: #f8fdf9; border-left: 3px solid var(--tf-color-primary); padding: 1rem 1.25rem; }
.detail-reason { font-size: 0.88rem; margin-bottom: 0.75rem; }
.sub-table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
.sub-table th { background: var(--tf-color-primary); color: #fff; padding: 0.4rem 0.65rem; border-bottom: 1px solid #d0e2d4; text-align: left; font-size: 0.875rem; }
.sub-table td { padding: 0.38rem 0.65rem; border-bottom: 1px solid var(--tf-color-border); }
.sub-empty { color: var(--tf-color-muted); font-size: 0.85rem; margin: 0; }

/* Badges */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 600; }
.badge--pending { background: #fff3cd; color: #856404; }
.badge--received { background: #d4edda; color: #155724; }
.badge--refunded { background: #d4edda; color: #155724; }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: #f0f5f1; }
.btn--accent { background: var(--tf-color-accent); color: #fff; border-color: var(--tf-color-accent); }
.btn--accent:hover:not(:disabled) { opacity: 0.85; }

/* Pagination */
.pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.pagination__info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* State messages */
.state-msg { padding: 2rem; text-align: center; color: var(--tf-color-muted); }
.state-msg--sm { padding: 0.75rem; }
.state-msg--error { color: #c0392b; }
</style>

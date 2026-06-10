<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────

interface OutofnoticeItem {
  outofnoticeId: string
  productId: string
  productTitle: string
  productNum: string
  name: string
  email: string
  mobile: string
  createdate: string
  isnotice: boolean
}

interface PagedResult {
  items: OutofnoticeItem[]
  total: number
  page: number
  pageSize: number
}

// ─── list state ───────────────────────────────────────────────────────────────

const items = ref<OutofnoticeItem[]>([])
const loading = ref(false)
const error = ref('')
const page = ref(1)
const total = ref(0)
const PAGE_SIZE = 20

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / PAGE_SIZE)))

// ─── action state ─────────────────────────────────────────────────────────────

const actionLoading = ref<string | null>(null)
const actionError = ref('')

// ─── delete confirm state ─────────────────────────────────────────────────────

const deleteTarget = ref<OutofnoticeItem | null>(null)
const deleteError = ref('')
const deleting = ref(false)

// ─── helpers ──────────────────────────────────────────────────────────────────

function errMsg(e: unknown, fallback: string) {
  const ae = e as ApiError
  if (ae.problem?.status === 501) return '此功能尚未開放（API 尚未實作）'
  return ae.problem?.detail ?? (e as Error).message ?? fallback
}

function fmtDate(d: string) {
  return d ? d.slice(0, 10) : '—'
}

// ─── data loading ─────────────────────────────────────────────────────────────

async function load() {
  loading.value = true
  error.value = ''
  try {
    const d = await apiFetch<PagedResult>(`/admin/outofnotices?page=${page.value}&pageSize=${PAGE_SIZE}`)
    items.value = d.items ?? []
    total.value = d.total ?? 0
  } catch (e) {
    error.value = errMsg(e, '載入失敗')
  } finally {
    loading.value = false
  }
}

function prevPage() {
  if (page.value > 1) { page.value--; load() }
}

function nextPage() {
  if (page.value < totalPages.value) { page.value++; load() }
}

// ─── mark noticed ─────────────────────────────────────────────────────────────

async function markNoticed(item: OutofnoticeItem) {
  actionLoading.value = item.outofnoticeId
  actionError.value = ''
  try {
    await apiFetch(`/admin/outofnotices/${item.outofnoticeId}/notice`, { method: 'PATCH' })
    await load()
  } catch (e) {
    actionError.value = errMsg(e, '操作失敗')
  } finally {
    actionLoading.value = null
  }
}

// ─── delete ───────────────────────────────────────────────────────────────────

function askDelete(item: OutofnoticeItem) {
  deleteTarget.value = item
  deleteError.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/outofnotices/${deleteTarget.value.outofnoticeId}`, { method: 'DELETE' })
    deleteTarget.value = null
    await load()
  } catch (e) {
    deleteError.value = errMsg(e, '刪除失敗')
  } finally {
    deleting.value = false
  }
}

onMounted(load)
</script>

<template>
  <main class="outofnotices">
    <div class="outofnotices__header">
      <h1 class="outofnotices__title">缺貨通知管理</h1>
    </div>

    <div v-if="actionError" class="outofnotices__alert">{{ actionError }}</div>

    <p v-if="loading" class="outofnotices__muted">載入中…</p>
    <p v-if="error" class="outofnotices__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <div class="outofnotices__table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>商品</th>
              <th>登記人</th>
              <th>Email</th>
              <th>手機</th>
              <th style="width:8rem">登記日</th>
              <th style="width:7rem">通知狀態</th>
              <th class="action-th"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in items" :key="item.outofnoticeId" class="data-table__row">
              <td>
                <div class="outofnotices__product-name">{{ item.productTitle }}</div>
                <div class="outofnotices__product-num text-muted">{{ item.productNum }}</div>
              </td>
              <td class="font-semibold">{{ item.name }}</td>
              <td class="text-muted">{{ item.email || '—' }}</td>
              <td>{{ item.mobile || '—' }}</td>
              <td>{{ fmtDate(item.createdate) }}</td>
              <td>
                <span class="badge" :class="item.isnotice ? 'badge--active' : 'badge--pending'">
                  {{ item.isnotice ? '已通知' : '未通知' }}
                </span>
              </td>
              <td class="action-cell">
                <button
                  v-if="!item.isnotice"
                  class="btn btn--sm btn--ghost"
                  :disabled="actionLoading === item.outofnoticeId"
                  @click="markNoticed(item)"
                >
                  {{ actionLoading === item.outofnoticeId ? '…' : '標記已通知' }}
                </button>
                <button class="btn btn--sm btn--danger-ghost" @click="askDelete(item)">刪除</button>
              </td>
            </tr>
            <tr v-if="items.length === 0">
              <td colspan="7" class="empty-cell">目前沒有缺貨通知資料</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- 分頁列 -->
    <div v-if="!loading && total > 0" class="outofnotices__pagination">
      <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
      <span class="outofnotices__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
      <button class="btn btn--sm btn--ghost" :disabled="page * PAGE_SIZE >= total" @click="nextPage">下一頁</button>
    </div>

    <!-- 刪除確認 Modal -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget = null">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認刪除缺貨通知</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除 <strong>{{ deleteTarget.name }}</strong> 對商品「{{ deleteTarget.productTitle }}」的缺貨通知嗎？此操作無法復原。</p>
          <p v-if="deleteError" class="form-error">{{ deleteError }}</p>
        </div>
        <div class="modal__footer">
          <button class="btn btn--ghost" @click="deleteTarget = null">取消</button>
          <button class="btn btn--danger" :disabled="deleting" @click="confirmDelete">
            {{ deleting ? '刪除中…' : '確認刪除' }}
          </button>
        </div>
      </div>
    </div>
  </main>
</template>

<style scoped>
.outofnotices {}
.outofnotices__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.outofnotices__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.outofnotices__error { color: #dc3545; }
.outofnotices__muted { color: var(--tf-color-muted); }
.outofnotices__alert { background: #fde8e8; color: #c0392b; border: 1px solid #f5c6c6; border-radius: 4px; padding: 0.7rem 1rem; margin-bottom: 1rem; font-size: 0.875rem; }

.outofnotices__table-wrap { overflow-x: auto; }
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.data-table { width: 100%; min-width: 720px; border-collapse: collapse; font-size: 0.875rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 160px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.font-semibold { font-weight: 600; }
.text-muted { color: var(--tf-color-muted); font-size: 0.85rem; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }

.outofnotices__product-name { font-weight: 500; color: #1e293b; }
.outofnotices__product-num { font-family: 'IBM Plex Mono', monospace; }

.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 500; white-space: nowrap; }
.badge--active { background: #dcfce7; color: #166534; }
.badge--pending { background: #fff3cd; color: #856404; }

/* 分頁 */
.outofnotices__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.outofnotices__page-info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* 刪除 Modal */
.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15,23,42,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 380px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1.25rem 1.4rem; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }
.form-error { color: #dc3545; font-size: 0.85rem; }

.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; text-decoration: none; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
.btn--danger { background: #dc3545; color: #fff; border-color: #dc3545; }
.btn--danger:hover:not(:disabled) { background: #b02a37; }
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }
</style>

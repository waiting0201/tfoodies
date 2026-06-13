<script setup lang="ts">
import { ref, reactive, computed } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, apiDownload, ApiError } from '../../lib/apiClient'

interface Supplier { supplierId: string; title: string }
interface Purchase {
  purchaseId: string
  purchaseCode: string
  purchaseDate: string
  status: number
  isExpenditure: boolean
  supplierId: string
  supplierName?: string
  exchangeName?: string
  payment?: string
  total: number
}
interface Paged<T> { items: T[]; totalCount: number }

const router = useRouter()

function errMsg(e: unknown, fallback: string) {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

// 供應商（僅供篩選下拉用）
const suppliers = ref<Supplier[]>([])
async function loadSuppliers() {
  try {
    suppliers.value = await apiFetch<Supplier[]>('/admin/suppliers')
  } catch { /* 篩選下拉非必要，失敗不阻斷 */ }
}

const purchases = ref<Purchase[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref('')
const page = ref(1)
const pageSize = 20
const filter = reactive({ status: '', supplierId: '' })

const STATUS_OPTIONS = [
  { value: '', label: '全部狀態' },
  { value: '1', label: '未入庫' },
  { value: '2', label: '已入庫' },
  { value: '3', label: '部分入庫' },
]

function statusLabel(s: number) {
  return s === 2 ? '已入庫' : s === 3 ? '部分入庫' : '未入庫'
}
function statusBadge(s: number) {
  return s === 2 ? 'badge--shipped' : s === 3 ? 'badge--queue' : 'badge--pending'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize) })
    if (filter.status) params.set('status', filter.status)
    if (filter.supplierId) params.set('supplierId', filter.supplierId)
    const res = await apiFetch<Paged<Purchase>>(`/admin/purchases?${params}`)
    purchases.value = res.items
    total.value = res.totalCount
  } catch (e) {
    error.value = errMsg(e, '載入失敗')
  } finally {
    loading.value = false
  }
}

function search() { page.value = 1; selected.value = new Set(); load() }
function prevPage() { if (page.value > 1) { page.value--; load() } }
function nextPage() { if (page.value * pageSize < total.value) { page.value++; load() } }

// ── 勾選 / 匯出 ───────────────────────────────────────────
const selected = ref<Set<string>>(new Set())
const allChecked = computed(() =>
  purchases.value.length > 0 && purchases.value.every(p => selected.value.has(p.purchaseId))
)
function toggleOne(id: string, checked: boolean) {
  const next = new Set(selected.value)
  if (checked) next.add(id); else next.delete(id)
  selected.value = next
}
function toggleAll(checked: boolean) {
  selected.value = checked ? new Set(purchases.value.map(p => p.purchaseId)) : new Set()
}

const exporting = ref(false)
async function exportExcel() {
  exporting.value = true
  error.value = ''
  try {
    const params = new URLSearchParams()
    if (selected.value.size > 0) {
      // 勾選匯出
      params.set('purchaseIds', Array.from(selected.value).join(','))
    } else {
      // 未勾選 → 依目前篩選匯出全部
      if (filter.status) params.set('status', filter.status)
      if (filter.supplierId) params.set('supplierId', filter.supplierId)
    }
    await apiDownload(`/admin/purchases/export?${params}`, 'purchases_export.xlsx')
  } catch (e) {
    error.value = errMsg(e, '匯出失敗')
  } finally {
    exporting.value = false
  }
}

const expenditureLoading = reactive<Record<string, boolean>>({})
async function convertToExpenditure(p: Purchase) {
  if (!confirm(`確定將採購單「${p.purchaseCode}」轉為應付憑單？此後將無法再編輯。`)) return
  expenditureLoading[p.purchaseId] = true
  try {
    await apiFetch(`/admin/purchases/${p.purchaseId}/expenditure`, { method: 'PATCH' })
    await load()
  } catch (e) {
    alert(errMsg(e, '操作失敗'))
  } finally {
    expenditureLoading[p.purchaseId] = false
  }
}

function formatDate(d?: string) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('zh-TW')
}
function formatMoney(n: number) {
  return 'NT$ ' + (n ?? 0).toLocaleString()
}

loadSuppliers()
load()
</script>

<template>
  <main class="purchases">
    <div class="purchases__header">
      <h1 class="purchases__title">採購單維護</h1>
      <div class="purchases__actions">
        <button class="btn btn--secondary" :disabled="exporting" @click="exportExcel">
          {{ exporting ? '匯出中…' : '匯出 Excel' }}
        </button>
        <button class="btn btn--primary" @click="router.push('/admin/purchases/new')">+ 新增採購單</button>
      </div>
    </div>

    <div class="purchases__filters">
      <select v-model="filter.status" class="filter-select" @change="search">
        <option v-for="o in STATUS_OPTIONS" :key="o.value" :value="o.value">{{ o.label }}</option>
      </select>
      <select v-model="filter.supplierId" class="filter-select" @change="search">
        <option value="">全部供應商</option>
        <option v-for="s in suppliers" :key="s.supplierId" :value="s.supplierId">{{ s.title }}</option>
      </select>
      <button class="btn btn--secondary" @click="search">搜尋</button>
    </div>

    <p v-if="loading" class="purchases__muted">載入中…</p>
    <p v-if="error" class="purchases__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th style="width:2.5rem">
              <input type="checkbox" :checked="allChecked" @change="toggleAll(($event.target as HTMLInputElement).checked)" />
            </th>
            <th>採購編號</th>
            <th>採購日期</th>
            <th>供應商</th>
            <th>幣別</th>
            <th style="text-align:right">金額</th>
            <th style="width:6rem">狀態</th>
            <th class="action-th"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="p in purchases" :key="p.purchaseId" class="data-table__row">
            <td>
              <input
                type="checkbox"
                :checked="selected.has(p.purchaseId)"
                @change="toggleOne(p.purchaseId, ($event.target as HTMLInputElement).checked)"
              />
            </td>
            <td class="font-mono">{{ p.purchaseCode }}</td>
            <td>{{ formatDate(p.purchaseDate) }}</td>
            <td>{{ p.supplierName || '—' }}</td>
            <td class="text-muted">{{ p.exchangeName || '—' }}</td>
            <td style="text-align:right">{{ formatMoney(p.total) }}</td>
            <td>
              <span class="badge" :class="statusBadge(p.status)">{{ statusLabel(p.status) }}</span>
              <span v-if="p.isExpenditure" class="badge badge--disabled" style="margin-left:0.25rem">已轉應付</span>
            </td>
            <td class="action-cell">
              <router-link
                v-if="!p.isExpenditure"
                :to="`/admin/purchases/${p.purchaseId}/edit`"
                class="btn btn--sm btn--ghost"
              >編輯</router-link>
              <button
                v-if="!p.isExpenditure"
                class="btn btn--sm btn--accent"
                :disabled="expenditureLoading[p.purchaseId]"
                @click="convertToExpenditure(p)"
              >{{ expenditureLoading[p.purchaseId] ? '處理中…' : '轉應付' }}</button>
            </td>
          </tr>
          <tr v-if="purchases.length === 0">
            <td colspan="8" class="empty-cell">目前沒有採購單資料</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="!loading" class="purchases__pagination">
      <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
      <span class="purchases__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
      <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
    </div>
  </main>
</template>

<style scoped>
.purchases {}
.purchases__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.purchases__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.purchases__actions { display: flex; gap: 0.5rem; align-items: center; }
.data-table input[type="checkbox"] { accent-color: var(--tf-color-primary); width: 16px; height: 16px; cursor: pointer; }
.purchases__error { color: #dc3545; }
.purchases__muted { color: var(--tf-color-muted); }

/* Filters */
.purchases__filters { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; align-items: center; }
.filter-select {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  background: #fff; font-size: 0.875rem; cursor: pointer; font-family: inherit;
  transition: border-color 0.15s;
}
.filter-select:focus { outline: none; border-color: var(--tf-color-primary); }

/* Card table */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 160px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }
.text-muted { color: var(--tf-color-muted); font-size: 0.85rem; }

/* Pagination */
.purchases__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.purchases__page-info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* Badge */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 500; white-space: nowrap; }
.badge--pending { background: #e2e3e5; color: #383d41; }
.badge--queue { background: #cce5ff; color: #004085; }
.badge--shipped { background: #d4edda; color: #155724; }
.badge--disabled { background: #f1f5f9; color: #64748b; }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: all 0.15s; white-space: nowrap; text-decoration: none; font-family: inherit; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }
.btn--accent { background: var(--tf-color-accent); color: #fff; border-color: var(--tf-color-accent); }
.btn--accent:hover:not(:disabled) { opacity: 0.85; }
</style>

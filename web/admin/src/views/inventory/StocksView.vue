<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

interface StockRow {
  stockId: string
  stockType: number
  createDate: string
  barcode?: string
  noticeNumber?: string
  declarationNumber?: string
  item?: number
  manufactureDate?: string
  expireDate?: string
  quantity: number
  remaining: number
  weight?: number
  status?: number
  productNum?: string
  productTitle?: string
}
interface Paged<T> { items: T[]; totalCount: number }

const router = useRouter()

const items = ref<StockRow[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref('')
const page = ref(1)
const pageSize = 20
const filter = reactive({ keyword: '' })

function errMsg(e: unknown, fallback: string) {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize) })
    if (filter.keyword.trim()) params.set('keyword', filter.keyword.trim())
    const res = await apiFetch<Paged<StockRow>>(`/admin/stocks?${params}`)
    items.value = res.items
    total.value = res.totalCount
  } catch (e) {
    error.value = errMsg(e, '載入失敗')
  } finally {
    loading.value = false
  }
}

function search() { page.value = 1; load() }
function prevPage() { if (page.value > 1) { page.value--; load() } }
function nextPage() { if (page.value * pageSize < total.value) { page.value++; load() } }

function goNew(type: 1 | 2) { router.push(`/admin/inventory/new?type=${type}`) }
function goEdit(s: StockRow) { router.push(`/admin/inventory/${s.stockId}/edit`) }

function formatDate(d?: string) {
  if (!d) return '—'
  return String(d).slice(0, 10)
}

load()
</script>

<template>
  <main class="stocks">
    <div class="stocks__header">
      <h1 class="stocks__title">入庫維護</h1>
      <div class="stocks__actions">
        <button class="btn btn--primary" @click="goNew(1)">+ 新增需申報入庫</button>
        <button class="btn btn--accent" @click="goNew(2)">+ 新增不需申報入庫</button>
      </div>
    </div>

    <div class="stocks__filters">
      <input
        v-model="filter.keyword"
        class="filter-input"
        placeholder="搜尋產品編號 / 名稱 / 通知號碼…"
        @keyup.enter="search"
      />
      <button class="btn btn--secondary" @click="search">搜尋</button>
    </div>

    <p v-if="loading" class="stocks__muted">載入中…</p>
    <p v-if="error" class="stocks__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <div class="stocks__table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>分類</th>
              <th>入庫日期</th>
              <th>產品名稱</th>
              <th>通知號碼</th>
              <th>報單號及項次</th>
              <th style="text-align:right">入庫數量</th>
              <th style="text-align:right">入庫淨重</th>
              <th>製造日期</th>
              <th>有效日期</th>
              <th>狀態</th>
              <th class="action-th"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="s in items" :key="s.stockId" class="data-table__row">
              <td>
                <span class="badge" :class="s.stockType === 1 ? 'badge--queue' : 'badge--shipped'">
                  {{ s.stockType === 1 ? '需申報' : '不需申報' }}
                </span>
              </td>
              <td>{{ formatDate(s.createDate) }}</td>
              <td>
                <span class="font-mono text-muted">{{ s.productNum || '—' }}</span>
                <span class="stocks__pname">{{ s.productTitle || '—' }}</span>
              </td>
              <td class="font-mono">{{ s.stockType === 1 ? (s.noticeNumber || '—') : '—' }}</td>
              <td class="text-muted">
                {{ s.stockType === 1 ? `${s.declarationNumber || '—'} ${s.item ?? ''}`.trim() : '—' }}
              </td>
              <td style="text-align:right">{{ s.quantity }}</td>
              <td style="text-align:right" class="text-muted">{{ s.stockType === 1 && s.weight != null ? s.weight : '—' }}</td>
              <td class="text-muted">{{ s.stockType === 1 ? formatDate(s.manufactureDate) : '—' }}</td>
              <td class="text-muted">{{ s.stockType === 1 ? formatDate(s.expireDate) : '—' }}</td>
              <td>
                <span v-if="s.stockType === 1 && s.status != null"
                      class="badge" :class="s.status === 1 ? 'badge--active' : 'badge--canceled'">
                  {{ s.status === 1 ? '合格' : '待複檢' }}
                </span>
                <span v-else class="text-muted">—</span>
              </td>
              <td class="action-cell">
                <button class="btn btn--sm btn--ghost" @click="goEdit(s)">編輯</button>
              </td>
            </tr>
            <tr v-if="items.length === 0">
              <td colspan="11" class="empty-cell">目前沒有入庫批次資料</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div v-if="!loading" class="stocks__pagination">
      <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
      <span class="stocks__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
      <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
    </div>
  </main>
</template>

<style scoped>
.stocks {}
.stocks__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.stocks__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.stocks__actions { display: flex; gap: 0.5rem; align-items: center; }
.stocks__error { color: #dc3545; }
.stocks__muted { color: var(--tf-color-muted); }

/* Filters */
.stocks__filters { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; align-items: center; }
.filter-input { flex: 1 1 200px; padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; font-family: inherit; background: #fff; transition: border-color 0.15s; }
.filter-input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }

/* Card table */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.stocks__table-wrap { overflow-x: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 980px; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 80px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; white-space: nowrap; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; text-align: right; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }
.text-muted { color: var(--tf-color-muted); font-size: 0.85rem; }
.stocks__pname { display: block; }

/* Pagination */
.stocks__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.stocks__page-info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* Badge */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 500; white-space: nowrap; }
.badge--queue { background: #cce5ff; color: #004085; }
.badge--shipped { background: #d4edda; color: #155724; }
.badge--active { background: #dcfce7; color: #166534; }
.badge--canceled { background: #f8d7da; color: #721c24; }

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

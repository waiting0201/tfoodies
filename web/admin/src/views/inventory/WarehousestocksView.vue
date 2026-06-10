<script setup lang="ts">
import { ref, reactive, computed } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

interface Warehouse { warehouseid: string; warehousetype: number; title: string }
interface SourceStock {
  stockId: string
  noticeNumber?: string
  expireDate?: string
  productNum?: string
  productTitle?: string
  available: number
}
interface WsRow {
  warehouseStockId: string
  transDate: string
  quantity: number
  quantityLeft: number
  memo?: string
  warehouseId: string
  warehouseName?: string
  stockId: string
  noticeNumber?: string
  expireDate?: string
  productNum?: string
  productTitle?: string
}
interface Paged<T> { items: T[]; totalCount: number }

const today = new Date().toISOString().slice(0, 10)

const warehouses = ref<Warehouse[]>([])
const items = ref<WsRow[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref('')
const page = ref(1)
const pageSize = 20
const filter = reactive({ warehouseId: '', keyword: '' })

function errMsg(e: unknown, fallback: string) {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

async function loadWarehouses() {
  try { warehouses.value = await apiFetch<Warehouse[]>('/admin/warehouses') } catch { /* 篩選非必要 */ }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize) })
    if (filter.warehouseId) params.set('warehouseId', filter.warehouseId)
    if (filter.keyword.trim()) params.set('keyword', filter.keyword.trim())
    const res = await apiFetch<Paged<WsRow>>(`/admin/warehousestocks?${params}`)
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

function formatDate(d?: string) { return d ? String(d).slice(0, 10) : '—' }

function esc(v: unknown) {
  return String(v ?? '—').replace(/[&<>"]/g, c => (
    { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c] as string
  ))
}

// 列印單筆移倉/在庫批次單（舊系統僅有按鈕死連結、無實作，此為依資料設計的移倉單）
function printRow(r: WsRow) {
  const rows: [string, string][] = [
    ['移倉日期', formatDate(r.transDate)],
    ['倉庫', esc(r.warehouseName)],
    ['通知號碼', esc(r.noticeNumber)],
    ['產品編號', esc(r.productNum)],
    ['產品名稱', esc(r.productTitle)],
    ['有效日期', formatDate(r.expireDate)],
    ['入庫數量', String(r.quantity)],
    ['剩餘數量', String(r.quantityLeft)],
    ['備註', esc(r.memo)],
  ]
  const body = rows.map(([k, v]) => `<tr><th>${k}</th><td>${v}</td></tr>`).join('')
  const html = `<!doctype html><html lang="zh-Hant"><head><meta charset="utf-8" />
<title>移倉單 ${esc(r.noticeNumber)}</title>
<style>
  * { box-sizing: border-box; }
  body { font-family: 'Microsoft JhengHei','PingFang TC',sans-serif; color: #1e293b; margin: 32px; }
  h1 { font-size: 20px; margin: 0 0 4px; }
  .sub { color: #64748b; font-size: 12px; margin: 0 0 20px; }
  table { width: 100%; border-collapse: collapse; font-size: 14px; }
  th, td { border: 1px solid #cbd5e1; padding: 8px 12px; text-align: left; vertical-align: top; }
  th { background: #f1f5f9; width: 28%; font-weight: 600; }
  @media print { body { margin: 12mm; } }
</style></head><body>
  <h1>移倉單</h1>
  <p class="sub">食在呼 TFoodies｜在庫批次</p>
  <table><tbody>${body}</tbody></table>
  <script>window.onload=function(){window.print();}<\/script>
</body></html>`

  const w = window.open('', '_blank', 'width=760,height=640')
  if (!w) { alert('無法開啟列印視窗，請允許彈出視窗。'); return }
  w.document.open()
  w.document.write(html)
  w.document.close()
}

// ── 移倉（調撥）面板 ────────────────────────────────────────────
const transferOpen = ref(false)
const sourceStocks = ref<SourceStock[]>([])
const transferSaving = ref(false)
const transferError = ref('')
const transfer = reactive({
  transDate: today,
  fromWarehouseId: '',
  stockId: '',
  toWarehouseId: '',
  qty: 1,
  memo: '',
})

const selectedSource = computed(() => sourceStocks.value.find(s => s.stockId === transfer.stockId) ?? null)
const transferMax = computed(() => selectedSource.value?.available ?? 0)
const toWarehouseOptions = computed(() => warehouses.value.filter(w => w.warehouseid !== transfer.fromWarehouseId))

function openTransfer() {
  transfer.transDate = today
  transfer.fromWarehouseId = ''
  transfer.stockId = ''
  transfer.toWarehouseId = ''
  transfer.qty = 1
  transfer.memo = ''
  sourceStocks.value = []
  transferError.value = ''
  transferOpen.value = true
}
function closeTransfer() { transferOpen.value = false }

async function onFromWarehouseChange() {
  transfer.stockId = ''
  transfer.toWarehouseId = ''
  sourceStocks.value = []
  if (!transfer.fromWarehouseId) return
  try {
    sourceStocks.value = await apiFetch<SourceStock[]>(`/admin/warehousestocks/source?warehouseId=${transfer.fromWarehouseId}`)
  } catch (e) {
    transferError.value = errMsg(e, '載入來源庫存失敗')
  }
}

async function submitTransfer() {
  transferError.value = ''
  if (!transfer.fromWarehouseId) { transferError.value = '請選擇移出倉'; return }
  if (!transfer.stockId) { transferError.value = '請選擇產品批次'; return }
  if (!transfer.toWarehouseId) { transferError.value = '請選擇移入倉'; return }
  if (transfer.qty <= 0) { transferError.value = '數量必須大於 0'; return }
  if (transfer.qty > transferMax.value) { transferError.value = `數量不可超過可調撥量 ${transferMax.value}`; return }

  transferSaving.value = true
  try {
    await apiFetch('/admin/warehousestocks/transfer', {
      method: 'POST',
      body: JSON.stringify({
        stockId: transfer.stockId,
        fromWarehouseId: transfer.fromWarehouseId,
        toWarehouseId: transfer.toWarehouseId,
        qty: transfer.qty,
        transDate: transfer.transDate || null,
        memo: transfer.memo || null,
      }),
    })
    closeTransfer()
    await load()
  } catch (e) {
    transferError.value = errMsg(e, '調撥失敗')
  } finally {
    transferSaving.value = false
  }
}

// ── 編輯在庫帳面板 ──────────────────────────────────────────────
const editOpen = ref(false)
const editTarget = ref<WsRow | null>(null)
const editSaving = ref(false)
const editError = ref('')
const editForm = reactive({ quantity: 0, quantityLeft: 0, memo: '' })

function openEdit(r: WsRow) {
  editTarget.value = r
  editForm.quantity = r.quantity
  editForm.quantityLeft = r.quantityLeft
  editForm.memo = r.memo ?? ''
  editError.value = ''
  editOpen.value = true
}
function closeEdit() { editOpen.value = false }

async function submitEdit() {
  if (!editTarget.value) return
  editError.value = ''
  if (editForm.quantityLeft > editForm.quantity) { editError.value = '剩餘量不可大於入庫量'; return }
  editSaving.value = true
  try {
    await apiFetch(`/admin/warehousestocks/${editTarget.value.warehouseStockId}`, {
      method: 'PUT',
      body: JSON.stringify({
        quantity: editForm.quantity,
        quantityLeft: editForm.quantityLeft,
        memo: editForm.memo || null,
      }),
    })
    closeEdit()
    await load()
  } catch (e) {
    editError.value = errMsg(e, '更新失敗')
  } finally {
    editSaving.value = false
  }
}

loadWarehouses()
load()
</script>

<template>
  <main class="ws">
    <div class="ws__header">
      <h1 class="ws__title">移庫維護</h1>
      <button class="btn btn--primary" @click="openTransfer">+ 新增移倉</button>
    </div>

    <div class="ws__filters">
      <select v-model="filter.warehouseId" class="filter-select" @change="search">
        <option value="">全部倉庫</option>
        <option v-for="w in warehouses" :key="w.warehouseid" :value="w.warehouseid">{{ w.title }}</option>
      </select>
      <input
        v-model="filter.keyword"
        class="filter-input"
        placeholder="搜尋產品編號 / 名稱 / 通知號碼…"
        @keyup.enter="search"
      />
      <button class="btn btn--secondary" @click="search">搜尋</button>
    </div>

    <p v-if="loading" class="ws__muted">載入中…</p>
    <p v-if="error" class="ws__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <div class="ws__table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>移倉日期</th>
              <th>倉庫</th>
              <th>通知號碼</th>
              <th>產品</th>
              <th style="text-align:right">入庫數量</th>
              <th style="text-align:right">剩餘數量</th>
              <th class="action-th"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in items" :key="r.warehouseStockId" class="data-table__row">
              <td>{{ formatDate(r.transDate) }}</td>
              <td>{{ r.warehouseName || '—' }}</td>
              <td class="font-mono">{{ r.noticeNumber || '—' }}</td>
              <td>
                <span class="font-mono text-muted">{{ r.productNum || '—' }}</span>
                <span class="ws__pname">{{ r.productTitle || '—' }}</span>
              </td>
              <td style="text-align:right">{{ r.quantity }}</td>
              <td style="text-align:right" :class="{ 'ws__zero': r.quantityLeft <= 0 }">{{ r.quantityLeft }}</td>
              <td class="action-cell">
                <button class="btn btn--sm btn--secondary" @click="printRow(r)">列印</button>
                <button class="btn btn--sm btn--ghost" @click="openEdit(r)">編輯</button>
              </td>
            </tr>
            <tr v-if="items.length === 0">
              <td colspan="7" class="empty-cell">目前沒有在庫/移倉資料</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div v-if="!loading" class="ws__pagination">
      <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
      <span class="ws__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
      <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
    </div>

    <!-- 移倉面板 -->
    <div v-if="transferOpen" class="panel-overlay" @click.self="closeTransfer">
      <aside class="side-panel">
        <div class="panel__header">
          <h2 class="panel__title">新增移倉</h2>
          <button class="panel__close" @click="closeTransfer" aria-label="關閉">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div class="panel__body">
          <div class="form-field">
            <label class="form-field__label">移倉日期 <span class="required">*</span></label>
            <input v-model="transfer.transDate" class="form-field__input" type="date" />
          </div>
          <div class="form-field">
            <label class="form-field__label">移出倉 <span class="required">*</span></label>
            <select v-model="transfer.fromWarehouseId" class="form-field__input" @change="onFromWarehouseChange">
              <option value="">請選擇移出倉</option>
              <option v-for="w in warehouses" :key="w.warehouseid" :value="w.warehouseid">{{ w.title }}</option>
            </select>
          </div>
          <div class="form-field">
            <label class="form-field__label">產品 / 批次 <span class="required">*</span></label>
            <select v-model="transfer.stockId" class="form-field__input" :disabled="!transfer.fromWarehouseId">
              <option value="">請選擇產品批次</option>
              <option v-for="s in sourceStocks" :key="s.stockId" :value="s.stockId">
                {{ s.productNum }} {{ s.productTitle }}
                <template v-if="s.noticeNumber">（{{ s.noticeNumber }}）</template>
                — 可調 {{ s.available }}
              </option>
            </select>
          </div>
          <div class="form-field">
            <label class="form-field__label">移入倉 <span class="required">*</span></label>
            <select v-model="transfer.toWarehouseId" class="form-field__input" :disabled="!transfer.fromWarehouseId">
              <option value="">請選擇移入倉</option>
              <option v-for="w in toWarehouseOptions" :key="w.warehouseid" :value="w.warehouseid">{{ w.title }}</option>
            </select>
          </div>
          <div class="form-field">
            <label class="form-field__label">
              數量 <span class="required">*</span>
              <span v-if="selectedSource" class="ws__avail">（可調撥 {{ transferMax }}）</span>
            </label>
            <input v-model.number="transfer.qty" class="form-field__input" type="number" min="1" :max="transferMax" step="1" />
          </div>
          <div class="form-field">
            <label class="form-field__label">備註</label>
            <textarea v-model="transfer.memo" class="form-field__input" rows="2"></textarea>
          </div>
          <p v-if="transferError" class="form-error">{{ transferError }}</p>
        </div>
        <div class="panel__footer">
          <button class="btn btn--ghost" @click="closeTransfer">取消</button>
          <button class="btn btn--primary" :disabled="transferSaving" @click="submitTransfer">
            {{ transferSaving ? '調撥中…' : '確認移倉' }}
          </button>
        </div>
      </aside>
    </div>

    <!-- 編輯在庫帳面板 -->
    <div v-if="editOpen" class="panel-overlay" @click.self="closeEdit">
      <aside class="side-panel">
        <div class="panel__header">
          <h2 class="panel__title">編輯在庫帳</h2>
          <button class="panel__close" @click="closeEdit" aria-label="關閉">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div class="panel__body">
          <p v-if="editTarget" class="ws__edit-meta">
            {{ editTarget.warehouseName }}｜{{ editTarget.productNum }} {{ editTarget.productTitle }}
          </p>
          <div class="form-row">
            <div class="form-field">
              <label class="form-field__label">入庫數量 <span class="required">*</span></label>
              <input v-model.number="editForm.quantity" class="form-field__input" type="number" min="0" step="1" />
            </div>
            <div class="form-field">
              <label class="form-field__label">剩餘數量 <span class="required">*</span></label>
              <input v-model.number="editForm.quantityLeft" class="form-field__input" type="number" min="0" step="1" />
            </div>
          </div>
          <div class="form-field">
            <label class="form-field__label">備註</label>
            <textarea v-model="editForm.memo" class="form-field__input" rows="2"></textarea>
          </div>
          <p v-if="editError" class="form-error">{{ editError }}</p>
        </div>
        <div class="panel__footer">
          <button class="btn btn--ghost" @click="closeEdit">取消</button>
          <button class="btn btn--primary" :disabled="editSaving" @click="submitEdit">
            {{ editSaving ? '儲存中…' : '儲存' }}
          </button>
        </div>
      </aside>
    </div>
  </main>
</template>

<style scoped>
.ws {}
.ws__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.ws__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.ws__error { color: #dc3545; }
.ws__muted { color: var(--tf-color-muted); }

/* Filters */
.ws__filters { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; align-items: center; }
.filter-input { flex: 1 1 200px; padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; font-family: inherit; background: #fff; transition: border-color 0.15s; }
.filter-input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.filter-select { padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; background: #fff; font-size: 0.875rem; cursor: pointer; font-family: inherit; transition: border-color 0.15s; }
.filter-select:focus { outline: none; border-color: var(--tf-color-primary); }

/* Card table */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.ws__table-wrap { overflow-x: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 140px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; white-space: nowrap; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }
.text-muted { color: var(--tf-color-muted); font-size: 0.85rem; }
.ws__pname { display: block; }
.ws__zero { color: #c0392b; font-weight: 600; }

/* Pagination */
.ws__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.ws__page-info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* Panel */
.panel-overlay { position: fixed; inset: 0; z-index: 50; background: rgba(15, 23, 42, 0.4); backdrop-filter: blur(1px); display: flex; justify-content: flex-end; animation: fadeIn 0.15s ease; }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
.side-panel { width: 100%; max-width: 440px; height: 100%; background: #fff; box-shadow: -8px 0 40px rgba(0,0,0,0.15); display: flex; flex-direction: column; animation: slideInRight 0.22s cubic-bezier(0.25,0.46,0.45,0.94); }
@keyframes slideInRight { from { transform: translateX(100%); } to { transform: none; } }
.panel__header { display: flex; align-items: center; justify-content: space-between; padding: 1.25rem 1.5rem; border-bottom: 1px solid var(--tf-color-border); }
.panel__title { font-size: 1.05rem; font-weight: 700; color: #1e293b; margin: 0; }
.panel__close { background: none; border: none; cursor: pointer; color: var(--tf-color-muted); padding: 0.25rem; border-radius: 4px; display: flex; }
.panel__close:hover { color: #475569; background: #f1f5f9; }
.panel__body { flex: 1; overflow-y: auto; padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
.panel__footer { padding: 1rem 1.5rem; border-top: 1px solid var(--tf-color-border); display: flex; justify-content: flex-end; gap: 0.5rem; }

.form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.required { color: #ef4444; }
.form-field__input { padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; color: #1e293b; background: #fff; transition: border-color 0.15s; font-family: inherit; }
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38,183,188,0.15); }
.form-field__input:disabled { background: #f1f5f9; color: #64748b; }
.form-error { color: #dc3545; font-size: 0.85rem; }
.ws__avail { color: var(--tf-color-muted); font-weight: 400; }
.ws__edit-meta { font-size: 0.85rem; color: #475569; margin: 0; }

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
</style>

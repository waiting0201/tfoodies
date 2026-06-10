<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

const router = useRouter()

// ── 型別 ──────────────────────────────────────────────────────────

interface ArInvoiceItem {
  invoiceId: string
  invoiceCode: string
  requestDate: string
  incomeId: string | null
  note: string | null
  memberName: string
  totalPrice: number
}

interface ArInvoiceDetail {
  invoiceDetailId: string
  orderCode: string
  orderDate: string | null
  orderInvoiceCode: string | null
  price: number
  tax: number
  note: string | null
}

interface ArInvoiceDetailResponse {
  invoice: {
    invoiceCode: string
    requestDate: string
    incomeId: string | null
    note: string | null
    memberName: string
  }
  details: ArInvoiceDetail[]
}

// ── 列表狀態 ──────────────────────────────────────────────────────

const items = ref<ArInvoiceItem[]>([])
const loading = ref(false)
const error = ref('')
const page = ref(1)
const pageSize = 20
const total = ref(0)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize) })
    const res = await apiFetch<{ items: ArInvoiceItem[]; total: number; page: number; pageSize: number; totalPages: number }>(
      `/admin/ar-invoices?${params}`
    )
    items.value = res.items
    total.value = res.total
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

function prevPage() {
  if (page.value <= 1) return
  page.value--
  load()
}

function nextPage() {
  if (page.value * pageSize >= total.value) return
  page.value++
  load()
}

// ── 行內展開詳情 ──────────────────────────────────────────────────

const expandedId = ref<string | null>(null)
const detailLoading = ref(false)
const detailError = ref('')
const detailData = ref<ArInvoiceDetailResponse | null>(null)

async function toggleDetail(inv: ArInvoiceItem) {
  if (expandedId.value === inv.invoiceId) {
    expandedId.value = null
    detailData.value = null
    return
  }
  expandedId.value = inv.invoiceId
  detailLoading.value = true
  detailError.value = ''
  detailData.value = null
  try {
    detailData.value = await apiFetch<ArInvoiceDetailResponse>(`/admin/ar-invoices/${inv.invoiceId}`)
  } catch (e) {
    detailError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入詳情失敗'
  } finally {
    detailLoading.value = false
  }
}

// ── 編輯面板 ──────────────────────────────────────────────────────

const panelTarget = ref<ArInvoiceItem | null>(null)
const editForm = reactive({ requestDate: '', note: '' })
const editDetails = ref<ArInvoiceDetail[]>([])
const editDetailLoading = ref(false)
const saving = ref(false)
const saveError = ref('')

async function openEdit(inv: ArInvoiceItem) {
  panelTarget.value = inv
  editForm.requestDate = fmtDate(inv.requestDate)
  editForm.note = inv.note ?? ''
  saveError.value = ''
  // 載入請款明細（唯讀），對照舊系統 EditInvoices：顯示已請款訂單
  editDetails.value = []
  editDetailLoading.value = true
  try {
    const d = await apiFetch<ArInvoiceDetailResponse>(`/admin/ar-invoices/${inv.invoiceId}`)
    editDetails.value = d.details
  } catch {
    editDetails.value = []
  } finally {
    editDetailLoading.value = false
  }
}

function closePanel() {
  panelTarget.value = null
}

// 列印請款單：開啟獨立可列印頁面（新分頁），對照舊系統每列的「列印」鈕
function printInvoice(inv: ArInvoiceItem) {
  const url = router.resolve({ name: 'ar-invoice-print', params: { id: inv.invoiceId } }).href
  window.open(url, '_blank')
}

async function saveEdit() {
  if (!panelTarget.value) return
  saving.value = true
  saveError.value = ''
  try {
    await apiFetch(`/admin/ar-invoices/${panelTarget.value.invoiceId}`, {
      method: 'PUT',
      body: JSON.stringify({ requestDate: editForm.requestDate, note: editForm.note }),
    })
    closePanel()
    // 若展開詳情所屬為此筆，重新載入詳情
    if (expandedId.value === panelTarget.value?.invoiceId) {
      await toggleDetail(panelTarget.value!)
      await toggleDetail(panelTarget.value!)
    }
    await load()
  } catch (e) {
    saveError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '儲存失敗'
  } finally {
    saving.value = false
  }
}

// ── 刪除 Modal ────────────────────────────────────────────────────

const deleteTarget = ref<ArInvoiceItem | null>(null)
const deleteError = ref('')
const deleting = ref(false)

function askDelete(inv: ArInvoiceItem) {
  deleteTarget.value = inv
  deleteError.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/ar-invoices/${deleteTarget.value.invoiceId}`, { method: 'DELETE' })
    if (expandedId.value === deleteTarget.value.invoiceId) {
      expandedId.value = null
      detailData.value = null
    }
    deleteTarget.value = null
    await load()
  } catch (e) {
    deleteError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '刪除失敗'
  } finally {
    deleting.value = false
  }
}

// ── 工具函式 ──────────────────────────────────────────────────────

function fmtDate(d: string | null) { return d ? String(d).slice(0, 10) : '—' }
function fmtMoney(n: number) { return n == null ? '—' : `NT$ ${Number(n).toLocaleString('zh-TW')}` }

onMounted(load)
</script>

<template>
  <main class="ar-invoices">
    <div class="ar-invoices__header">
      <h1 class="ar-invoices__title">請款維護</h1>
      <button class="btn btn--primary" @click="router.push('/admin/ar-invoices/new')">+ 新增請款</button>
    </div>

    <p v-if="loading" class="ar-invoices__muted">載入中…</p>
    <p v-if="error" class="ar-invoices__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th>請款單號</th>
            <th>會員</th>
            <th>請款日</th>
            <th>金額</th>
            <th>收款狀態</th>
            <th class="action-th"></th>
          </tr>
        </thead>
        <tbody>
          <template v-for="inv in items" :key="inv.invoiceId">
            <!-- 主資料列 -->
            <tr
              class="data-table__row"
              :class="{ 'data-table__row--expanded': expandedId === inv.invoiceId }"
              style="cursor:pointer"
              @click="toggleDetail(inv)"
            >
              <td class="font-mono">{{ inv.invoiceCode }}</td>
              <td>{{ inv.memberName }}</td>
              <td>{{ fmtDate(inv.requestDate) }}</td>
              <td>{{ fmtMoney(inv.totalPrice) }}</td>
              <td>
                <span v-if="inv.incomeId" class="badge badge--paid">已收款</span>
                <span v-else class="badge badge--unpaid">未收款</span>
              </td>
              <td class="action-cell" @click.stop>
                <button class="btn btn--sm btn--secondary" @click="printInvoice(inv)">列印</button>
                <button class="btn btn--sm btn--ghost" @click="openEdit(inv)">編輯</button>
                <button class="btn btn--sm btn--danger-ghost" @click="askDelete(inv)">刪除</button>
              </td>
            </tr>
            <!-- 展開詳情列 -->
            <tr v-if="expandedId === inv.invoiceId" :key="inv.invoiceId + '-detail'">
              <td colspan="6" style="padding:0; border-bottom:1px solid var(--tf-color-border);">
                <div class="detail-panel">
                  <p v-if="detailLoading" class="ar-invoices__muted">載入詳情中…</p>
                  <p v-else-if="detailError" class="ar-invoices__error">{{ detailError }}</p>
                  <template v-else-if="detailData">
                    <p v-if="detailData.invoice.note" class="detail-note">備註：{{ detailData.invoice.note }}</p>
                    <table class="data-table detail-table">
                      <thead>
                        <tr>
                          <th>訂單</th>
                          <th style="text-align:right">未稅</th>
                          <th style="text-align:right">稅額</th>
                          <th style="text-align:right">合計</th>
                          <th>備註</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr
                          v-for="d in detailData.details"
                          :key="d.invoiceDetailId"
                          class="data-table__row"
                        >
                          <td class="font-mono">{{ d.orderCode }}</td>
                          <td style="text-align:right">{{ fmtMoney(d.price) }}</td>
                          <td style="text-align:right">{{ fmtMoney(d.tax) }}</td>
                          <td style="text-align:right">{{ fmtMoney(d.price + d.tax) }}</td>
                          <td>{{ d.note ?? '—' }}</td>
                        </tr>
                        <tr v-if="detailData.details.length === 0">
                          <td colspan="5" class="empty-cell">無明細資料</td>
                        </tr>
                      </tbody>
                    </table>
                  </template>
                </div>
              </td>
            </tr>
          </template>
          <tr v-if="items.length === 0">
            <td colspan="6" class="empty-cell">目前沒有請款資料</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 分頁 -->
    <div class="ar-invoices__pagination">
      <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
      <span class="ar-invoices__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
      <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
    </div>

    <!-- 編輯右側面板（內容對照舊系統 EditInvoices：會員/請款日期/備註 + 唯讀訂單明細） -->
    <div v-if="panelTarget" class="panel-overlay" @click.self="closePanel">
      <aside class="side-panel side-panel--wide">
        <div class="panel__header">
          <h2 class="panel__title">編輯請款</h2>
          <button class="panel__close" @click="closePanel" aria-label="關閉">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div class="panel__body">
          <div class="form-field">
            <label class="form-field__label">會員</label>
            <input :value="panelTarget.memberName" class="form-field__input" disabled />
          </div>
          <div class="form-field">
            <label class="form-field__label">請款日期 <span class="required">*</span></label>
            <input
              v-model="editForm.requestDate"
              type="date"
              class="form-field__input"
            />
          </div>
          <div class="form-field">
            <label class="form-field__label">備註</label>
            <textarea
              v-model="editForm.note"
              class="form-field__input"
              rows="3"
              style="resize:vertical"
              placeholder="備註（選填）"
            ></textarea>
          </div>

          <div class="form-field">
            <label class="form-field__label">請款明細</label>
            <p v-if="editDetailLoading" class="ar-invoices__muted" style="font-size:0.85rem">載入明細中…</p>
            <div v-else class="card">
              <table class="data-table edit-detail-table">
                <thead>
                  <tr>
                    <th>訂單單號</th>
                    <th>訂單日期</th>
                    <th>發票號碼</th>
                    <th style="text-align:right">金額</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="d in editDetails" :key="d.invoiceDetailId" class="data-table__row">
                    <td class="font-mono">{{ d.orderCode }}</td>
                    <td>{{ fmtDate(d.orderDate) }}</td>
                    <td class="font-mono">{{ d.orderInvoiceCode || '—' }}</td>
                    <td style="text-align:right">{{ fmtMoney(d.price + d.tax) }}</td>
                  </tr>
                  <tr v-if="editDetails.length === 0">
                    <td colspan="4" class="empty-cell">無明細資料</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <p v-if="saveError" class="form-error">{{ saveError }}</p>
        </div>
        <div class="panel__footer">
          <button class="btn btn--ghost" @click="closePanel">取消</button>
          <button class="btn btn--primary" :disabled="saving" @click="saveEdit">
            {{ saving ? '儲存中…' : '儲存' }}
          </button>
        </div>
      </aside>
    </div>

    <!-- 刪除 Modal -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget = null">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認刪除請款單</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除請款單 <strong>{{ deleteTarget.invoiceCode }}</strong> 嗎？</p>
          <p class="modal__hint">已連結收款記錄者無法刪除。</p>
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
.ar-invoices {}
.ar-invoices__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.ar-invoices__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.ar-invoices__error { color: #dc3545; }
.ar-invoices__muted { color: var(--tf-color-muted); }

.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 190px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.data-table__row--expanded td { background: #f0fafa; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }

/* ── 行內展開 ── */
.detail-panel { background: rgba(38, 183, 188, 0.04); border-left: 3px solid var(--tf-color-primary); padding: 1rem 1.25rem; }
.detail-note { font-size: 0.85rem; color: #475569; margin: 0 0 0.75rem; }
.detail-table { border: 1px solid var(--tf-color-border); border-radius: 4px; overflow: hidden; }
.detail-table th { background: var(--tf-color-primary-mid, #1d8e92); }

/* ── 分頁 ── */
.ar-invoices__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.ar-invoices__page-info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* ── Badge ── */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 500; white-space: nowrap; }
.badge--paid { background: #d4edda; color: #155724; }
.badge--unpaid { background: #fff3cd; color: #856404; }

/* ── 按鈕 ── */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; text-decoration: none; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }
.btn--danger { background: #dc3545; color: #fff; border-color: #dc3545; }
.btn--danger:hover:not(:disabled) { background: #b02a37; }
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }

/* ── 右側面板 ── */
.panel-overlay { position: fixed; inset: 0; z-index: 50; background: rgba(15, 23, 42, 0.4); backdrop-filter: blur(1px); display: flex; justify-content: flex-end; animation: fadeIn 0.15s ease; }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
.side-panel { width: 100%; max-width: 440px; height: 100%; background: #fff; box-shadow: -8px 0 40px rgba(0,0,0,0.15); display: flex; flex-direction: column; animation: slideInRight 0.22s cubic-bezier(0.25,0.46,0.45,0.94); }
.side-panel--wide { max-width: 600px; }
@keyframes slideInRight { from { transform: translateX(100%); } to { transform: none; } }
.panel__header { display: flex; align-items: center; justify-content: space-between; padding: 1.25rem 1.5rem; border-bottom: 1px solid var(--tf-color-border); }
.panel__title { font-size: 1.05rem; font-weight: 700; color: #1e293b; margin: 0; }
.panel__close { background: none; border: none; cursor: pointer; color: var(--tf-color-muted); padding: 0.25rem; border-radius: 4px; display: flex; }
.panel__close:hover { color: #475569; background: #f1f5f9; }
.panel__body { flex: 1; overflow-y: auto; padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
.panel__footer { padding: 1rem 1.5rem; border-top: 1px solid var(--tf-color-border); display: flex; justify-content: flex-end; gap: 0.5rem; }

.form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.required { color: #ef4444; }
.form-field__input { padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; color: #1e293b; background: #fff; transition: border-color 0.15s; font-family: inherit; }
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38,183,188,0.15); }
.form-field__input:disabled { background: #f1f5f9; color: #64748b; cursor: not-allowed; }
.form-error { color: #dc3545; font-size: 0.85rem; }

/* ── 編輯面板內的唯讀明細表 ── */
.edit-detail-table { font-size: 0.82rem; }
.edit-detail-table th { padding: 0.5rem 0.6rem; }
.edit-detail-table td { padding: 0.45rem 0.6rem; }

/* ── 刪除 Modal ── */
.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15,23,42,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 380px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1.25rem 1.4rem; display: flex; flex-direction: column; gap: 0.5rem; }
.modal__hint { font-size: 0.85rem; color: var(--tf-color-muted); margin: 0; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }
</style>

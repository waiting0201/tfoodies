<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

const router = useRouter()

// ── 型別 ───────────────────────────────────────────────────────────

interface IncomeItem {
  incomeId: string
  incomeCode: string
  incomeDate: string
  amount: number
  fee: number
  note: string | null
  memberName: string
}

interface InvoiceLink {
  invoiceId: string
  invoiceCode: string
  requestDate: string
  totalPrice: number
}

interface IncomeDetail {
  income: {
    incomeCode: string
    incomeDate: string
    amount: number
    fee: number
    note: string | null
    memberName: string
  }
  invoices: InvoiceLink[]
}

interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

// ── 清單 ───────────────────────────────────────────────────────────

const items = ref<IncomeItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 20
const loading = ref(false)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    const res = await apiFetch<PaginatedResponse<IncomeItem>>(
      `/admin/incomes?page=${page.value}&pageSize=${pageSize}`
    )
    items.value = res.items
    total.value = res.total
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

function prevPage() { if (page.value > 1) { page.value--; load() } }
function nextPage() { if (page.value * pageSize < total.value) { page.value++; load() } }

// ── 行內展開 ───────────────────────────────────────────────────────

const expandedId = ref<string | null>(null)
const detailMap = ref<Record<string, IncomeDetail>>({})
const detailLoading = ref<Record<string, boolean>>({})

async function toggleDetail(incomeId: string) {
  if (expandedId.value === incomeId) {
    expandedId.value = null
    return
  }
  expandedId.value = incomeId
  if (detailMap.value[incomeId]) return
  detailLoading.value[incomeId] = true
  try {
    const res = await apiFetch<IncomeDetail>(`/admin/incomes/${incomeId}`)
    detailMap.value[incomeId] = res
  } catch {
    // 展開失敗靜默處理
  } finally {
    detailLoading.value[incomeId] = false
  }
}

// ── 編輯面板 ───────────────────────────────────────────────────────

const editTarget = ref<IncomeItem | null>(null)
const editForm = reactive({
  incomeDate: '',
  amount: 0,
  fee: 0,
  note: '',
})
const editInvoices = ref<InvoiceLink[]>([])
const editDetailLoading = ref(false)
const saving = ref(false)
const saveError = ref('')

async function openEdit(item: IncomeItem) {
  editTarget.value = item
  editForm.incomeDate = fmtDate(item.incomeDate)
  editForm.amount = item.amount
  editForm.fee = item.fee
  editForm.note = item.note ?? ''
  saveError.value = ''
  // 載入連結請款單（唯讀），對照舊系統 EditIncomes
  editInvoices.value = []
  editDetailLoading.value = true
  try {
    const d = detailMap.value[item.incomeId] ?? await apiFetch<IncomeDetail>(`/admin/incomes/${item.incomeId}`)
    editInvoices.value = d.invoices
  } catch {
    editInvoices.value = []
  } finally {
    editDetailLoading.value = false
  }
}

function closePanel() {
  editTarget.value = null
}

async function saveEdit() {
  if (!editTarget.value) return
  saving.value = true
  saveError.value = ''
  try {
    await apiFetch(`/admin/incomes/${editTarget.value.incomeId}`, {
      method: 'PUT',
      body: JSON.stringify({
        amount: Number(editForm.amount),
        fee: Number(editForm.fee),
        incomeDate: editForm.incomeDate,
        note: editForm.note || null,
      }),
    })
    closePanel()
    // 若此筆有展開的詳情，清除快取讓下次重抓
    if (expandedId.value === editTarget.value?.incomeId) {
      delete detailMap.value[editTarget.value.incomeId]
    }
    await load()
  } catch (e) {
    saveError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '儲存失敗'
  } finally {
    saving.value = false
  }
}

// ── 刪除 Modal ─────────────────────────────────────────────────────

const deleteTarget = ref<IncomeItem | null>(null)
const deleting = ref(false)
const deleteError = ref('')

function askDelete(item: IncomeItem) {
  deleteTarget.value = item
  deleteError.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/incomes/${deleteTarget.value.incomeId}`, { method: 'DELETE' })
    if (expandedId.value === deleteTarget.value.incomeId) expandedId.value = null
    delete detailMap.value[deleteTarget.value.incomeId]
    deleteTarget.value = null
    await load()
  } catch (e) {
    deleteError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '刪除失敗'
  } finally {
    deleting.value = false
  }
}

// ── 格式化 ─────────────────────────────────────────────────────────

function fmtDate(d: string | null) { return d ? String(d).slice(0, 10) : '—' }
function fmtMoney(n: number) { return n == null ? '—' : `NT$ ${Number(n).toLocaleString('zh-TW')}` }

load()
</script>

<template>
  <main class="incomes">
    <div class="incomes__header">
      <h1 class="incomes__title">入帳維護</h1>
      <button class="btn btn--primary" @click="router.push('/admin/incomes/new')">+ 新增收款</button>
    </div>

    <p v-if="loading" class="incomes__muted">載入中…</p>
    <p v-if="error" class="incomes__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th>收款單號</th>
            <th>會員</th>
            <th>日期</th>
            <th>金額</th>
            <th>手續費</th>
            <th class="action-th"></th>
          </tr>
        </thead>
        <tbody>
          <template v-for="item in items" :key="item.incomeId">
            <tr
              class="data-table__row data-table__row--clickable"
              @click="toggleDetail(item.incomeId)"
            >
              <td class="font-mono">{{ item.incomeCode }}</td>
              <td>{{ item.memberName }}</td>
              <td>{{ fmtDate(item.incomeDate) }}</td>
              <td>{{ fmtMoney(item.amount) }}</td>
              <td>{{ fmtMoney(item.fee) }}</td>
              <td class="action-cell" @click.stop>
                <button class="btn btn--sm btn--ghost" @click="openEdit(item)">編輯</button>
                <button class="btn btn--sm btn--danger-ghost" @click="askDelete(item)">刪除</button>
              </td>
            </tr>
            <!-- 行內展開詳情 -->
            <tr v-if="expandedId === item.incomeId" :key="item.incomeId + '-detail'">
              <td colspan="6" class="detail-cell">
                <div class="detail-panel">
                  <div v-if="detailLoading[item.incomeId]" class="incomes__muted">載入詳情中…</div>
                  <template v-else-if="detailMap[item.incomeId]">
                    <p class="detail-panel__label">連結請款單</p>
                    <template v-if="detailMap[item.incomeId].invoices.length > 0">
                      <table class="data-table detail-sub-table">
                        <thead>
                          <tr>
                            <th>請款單號</th>
                            <th>請款日</th>
                            <th>金額</th>
                          </tr>
                        </thead>
                        <tbody>
                          <tr
                            v-for="inv in detailMap[item.incomeId].invoices"
                            :key="inv.invoiceId"
                            class="data-table__row"
                          >
                            <td class="font-mono">{{ inv.invoiceCode }}</td>
                            <td>{{ fmtDate(inv.requestDate) }}</td>
                            <td>{{ fmtMoney(inv.totalPrice) }}</td>
                          </tr>
                        </tbody>
                      </table>
                    </template>
                    <p v-else class="incomes__muted">無連結請款單</p>
                    <p v-if="detailMap[item.incomeId].income.note" class="detail-panel__note">
                      備註：{{ detailMap[item.incomeId].income.note }}
                    </p>
                  </template>
                </div>
              </td>
            </tr>
          </template>
          <tr v-if="items.length === 0">
            <td colspan="6" class="empty-cell">目前沒有入帳資料</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 分頁列（card 外） -->
    <div v-if="total > 0" class="incomes__pagination">
      <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
      <span class="incomes__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
      <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
    </div>

    <!-- 編輯右側面板（內容對照舊系統 EditIncomes：會員/入帳日期/實收金額/扣除金額/備註 + 唯讀請款明細） -->
    <div v-if="editTarget" class="panel-overlay" @click.self="closePanel">
      <aside class="side-panel side-panel--wide">
        <div class="panel__header">
          <h2 class="panel__title">編輯入帳</h2>
          <button class="panel__close" @click="closePanel" aria-label="關閉">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div class="panel__body">
          <div class="form-field">
            <label class="form-field__label">會員</label>
            <input :value="editTarget.memberName" class="form-field__input" disabled />
          </div>
          <div class="form-row">
            <div class="form-field">
              <label class="form-field__label">入帳日期 <span class="required">*</span></label>
              <input v-model="editForm.incomeDate" type="date" class="form-field__input" />
            </div>
            <div class="form-field">
              <label class="form-field__label">實收金額 <span class="required">*</span></label>
              <input v-model.number="editForm.amount" type="number" min="0" class="form-field__input" />
            </div>
            <div class="form-field">
              <label class="form-field__label">扣除金額</label>
              <input v-model.number="editForm.fee" type="number" min="0" class="form-field__input" />
            </div>
          </div>
          <div class="form-field">
            <label class="form-field__label">備註</label>
            <textarea v-model="editForm.note" class="form-field__input" rows="3" style="resize:vertical" placeholder="備註（選填）"></textarea>
          </div>

          <div class="form-field">
            <label class="form-field__label">請款明細</label>
            <p v-if="editDetailLoading" class="incomes__muted" style="font-size:0.85rem">載入明細中…</p>
            <div v-else class="card">
              <table class="data-table edit-detail-table">
                <thead>
                  <tr>
                    <th>請款單號</th>
                    <th>請款日期</th>
                    <th style="text-align:right">金額</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="inv in editInvoices" :key="inv.invoiceId" class="data-table__row">
                    <td class="font-mono">{{ inv.invoiceCode }}</td>
                    <td>{{ fmtDate(inv.requestDate) }}</td>
                    <td style="text-align:right">{{ fmtMoney(inv.totalPrice) }}</td>
                  </tr>
                  <tr v-if="editInvoices.length === 0">
                    <td colspan="3" class="empty-cell">無連結請款單</td>
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

    <!-- 刪除確認 Modal -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget = null">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認刪除收款</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除收款單 <strong>{{ deleteTarget.incomeCode }}</strong> 嗎？</p>
          <p class="modal__hint">刪除後將還原連結請款單與訂單的付款狀態。</p>
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
.incomes {}
.incomes__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.incomes__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.incomes__error { color: #dc3545; }
.incomes__muted { color: var(--tf-color-muted); }

/* ── 表格卡片 ── */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 130px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.data-table__row--clickable { cursor: pointer; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }

/* ── 行內展開 ── */
.detail-cell { padding: 0; border-bottom: 1px solid var(--tf-color-border); }
.detail-panel { background: rgba(38, 183, 188, 0.04); border-left: 3px solid var(--tf-color-primary); padding: 1rem 1.25rem; }
.detail-panel__label { font-size: 0.8rem; font-weight: 600; color: #475569; margin: 0 0 0.5rem; }
.detail-panel__note { font-size: 0.85rem; color: var(--tf-color-muted); margin: 0.75rem 0 0; }
.detail-sub-table { border: 1px solid var(--tf-color-border); border-radius: 4px; overflow: hidden; }
.detail-sub-table th { font-size: 0.78rem; padding: 0.4rem 0.65rem; }
.detail-sub-table td { font-size: 0.85rem; padding: 0.4rem 0.65rem; }

/* ── 分頁列 ── */
.incomes__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.incomes__page-info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* ── 按鈕 ── */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; text-decoration: none; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
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

/* ── 面板表單欄位 ── */
.form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.required { color: #ef4444; }
.form-field__input { padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; color: #1e293b; background: #fff; transition: border-color 0.15s; font-family: inherit; }
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38,183,188,0.15); }
.form-field__input:disabled { background: #f1f5f9; color: #64748b; cursor: not-allowed; }
.form-row { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 0.75rem; }
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

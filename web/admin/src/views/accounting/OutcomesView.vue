<script setup lang="ts">
import { ref, reactive } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

interface OutcomeItem {
  outcomeId: string
  outcomeCode: string
  outcomeDate: string | null
  amount: number
  note: string | null
  expenditureId: string
  expenditureCode: string
}

interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

interface PayableExpenditure {
  expenditureId: string
  expenditureCode: string
  supplierTitle: string
  totalAmount: number
  paidAmount: number
  status: string
}

const items = ref<OutcomeItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 20
const loading = ref(false)
const error = ref('')

const panelMode = ref<'create' | 'edit' | null>(null)
const panelTarget = ref<OutcomeItem | null>(null)
const payableList = ref<PayableExpenditure[]>([])
const payableLoading = ref(false)

const form = reactive({
  expenditureId: '',
  amount: 0,
  outcomeDate: '',
  note: ''
})
const saving = ref(false)
const saveError = ref('')

const deleteTarget = ref<OutcomeItem | null>(null)
const deleteError = ref('')
const deleting = ref(false)

function fmtDate(d: string | null) { return d ? String(d).slice(0, 10) : '—' }
function fmtMoney(n: number) { return n == null ? '—' : `NT$ ${Number(n).toLocaleString('zh-TW')}` }

async function load() {
  loading.value = true
  error.value = ''
  try {
    const res = await apiFetch<PaginatedResponse<OutcomeItem>>(
      `/admin/outcomes?page=${page.value}&pageSize=${pageSize}`
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

async function openCreate() {
  form.expenditureId = ''
  form.amount = 0
  form.outcomeDate = ''
  form.note = ''
  saveError.value = ''
  panelTarget.value = null
  panelMode.value = 'create'
  payableLoading.value = true
  try {
    payableList.value = await apiFetch<PayableExpenditure[]>('/admin/expenditures/payable')
  } catch (e) {
    saveError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '無法載入支出單'
  } finally {
    payableLoading.value = false
  }
}

function onExpenditureChange() {
  const exp = payableList.value.find(e => e.expenditureId === form.expenditureId)
  if (exp) {
    form.amount = exp.totalAmount - exp.paidAmount
  }
}

function openEdit(item: OutcomeItem) {
  panelTarget.value = item
  form.expenditureId = item.expenditureId
  form.amount = item.amount
  form.outcomeDate = fmtDate(item.outcomeDate)
  form.note = item.note ?? ''
  saveError.value = ''
  panelMode.value = 'edit'
}

function closePanel() { panelMode.value = null }

async function save() {
  if (panelMode.value === 'create') {
    if (!form.expenditureId) { saveError.value = '請選擇支出單'; return }
    if (!form.amount || form.amount < 1) { saveError.value = '金額為必填且須大於 0'; return }
    saving.value = true
    saveError.value = ''
    try {
      await apiFetch('/admin/outcomes', {
        method: 'POST',
        body: JSON.stringify({
          expenditureId: form.expenditureId,
          amount: Number(form.amount),
          outcomeDate: form.outcomeDate || null,
          note: form.note || null
        })
      })
      closePanel()
      await load()
    } catch (e) {
      saveError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
    } finally {
      saving.value = false
    }
  } else {
    if (!form.amount || form.amount < 1) { saveError.value = '金額為必填且須大於 0'; return }
    saving.value = true
    saveError.value = ''
    try {
      await apiFetch(`/admin/outcomes/${panelTarget.value!.outcomeId}`, {
        method: 'PUT',
        body: JSON.stringify({
          amount: Number(form.amount),
          outcomeDate: form.outcomeDate || null,
          note: form.note || null
        })
      })
      closePanel()
      await load()
    } catch (e) {
      saveError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
    } finally {
      saving.value = false
    }
  }
}

function askDelete(item: OutcomeItem) {
  deleteTarget.value = item
  deleteError.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/outcomes/${deleteTarget.value.outcomeId}`, { method: 'DELETE' })
    deleteTarget.value = null
    await load()
  } catch (e) {
    deleteError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
  } finally {
    deleting.value = false
  }
}

load()
</script>

<template>
  <main class="outcomes">
    <div class="outcomes__header">
      <h1 class="outcomes__title">付款維護</h1>
      <button class="btn btn--primary" @click="openCreate">+ 新增付款</button>
    </div>

    <p v-if="loading" class="outcomes__muted">載入中…</p>
    <p v-if="error" class="outcomes__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th>付款單號</th>
            <th>對應支出單</th>
            <th>日期</th>
            <th>金額</th>
            <th>備註</th>
            <th class="action-th"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in items" :key="item.outcomeId" class="data-table__row">
            <td class="font-mono">{{ item.outcomeCode }}</td>
            <td class="font-mono">{{ item.expenditureCode }}</td>
            <td>{{ fmtDate(item.outcomeDate) }}</td>
            <td>{{ fmtMoney(item.amount) }}</td>
            <td>{{ item.note || '—' }}</td>
            <td class="action-cell">
              <button class="btn btn--sm btn--ghost" @click="openEdit(item)">編輯</button>
              <button class="btn btn--sm btn--danger-ghost" @click="askDelete(item)">刪除</button>
            </td>
          </tr>
          <tr v-if="items.length === 0">
            <td colspan="6" class="empty-cell">目前沒有付款資料</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="outcomes__pagination">
      <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
      <span class="outcomes__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
      <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
    </div>

    <!-- Side panel -->
    <div v-if="panelMode" class="panel-overlay" @click.self="closePanel">
      <aside class="side-panel">
        <div class="panel__header">
          <h2 class="panel__title">{{ panelMode === 'create' ? '新增付款' : '編輯付款' }}</h2>
          <button class="panel__close" @click="closePanel" aria-label="關閉">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div class="panel__body">
          <!-- Create: 選支出單 -->
          <div v-if="panelMode === 'create'" class="form-field">
            <label class="form-field__label">支出單 <span class="required">*</span></label>
            <p v-if="payableLoading" class="outcomes__muted" style="margin:0;font-size:0.875rem">載入支出單中…</p>
            <select
              v-else
              v-model="form.expenditureId"
              class="form-field__input"
              @change="onExpenditureChange"
            >
              <option value="">請選擇支出單</option>
              <option
                v-for="exp in payableList"
                :key="exp.expenditureId"
                :value="exp.expenditureId"
              >
                {{ exp.expenditureCode }}（{{ exp.supplierTitle }}，未付 {{ fmtMoney(exp.totalAmount - exp.paidAmount) }}）
              </option>
            </select>
          </div>

          <!-- Edit: 支出單唯讀 -->
          <div v-if="panelMode === 'edit'" class="form-field">
            <label class="form-field__label">對應支出單</label>
            <div class="form-field__readonly font-mono">{{ panelTarget?.expenditureCode }}</div>
          </div>

          <div class="form-field">
            <label class="form-field__label">金額 <span class="required">*</span></label>
            <input
              v-model.number="form.amount"
              type="number"
              min="1"
              class="form-field__input"
              placeholder="請輸入金額"
            />
          </div>

          <div class="form-field">
            <label class="form-field__label">付款日期</label>
            <input
              v-model="form.outcomeDate"
              type="date"
              class="form-field__input"
            />
          </div>

          <div class="form-field">
            <label class="form-field__label">備註</label>
            <input
              v-model="form.note"
              type="text"
              class="form-field__input"
              placeholder="選填"
            />
          </div>

          <p v-if="saveError" class="form-error">{{ saveError }}</p>
        </div>
        <div class="panel__footer">
          <button class="btn btn--ghost" @click="closePanel">取消</button>
          <button class="btn btn--primary" :disabled="saving" @click="save">
            {{ saving ? '儲存中…' : '儲存' }}
          </button>
        </div>
      </aside>
    </div>

    <!-- Delete modal -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget = null">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認刪除付款</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除付款單 <strong class="font-mono">{{ deleteTarget.outcomeCode }}</strong> 嗎？</p>
          <p class="modal__hint">刪除後將重算對應支出單的付款狀態。</p>
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
.outcomes {}
.outcomes__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.outcomes__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.outcomes__error { color: #dc3545; }
.outcomes__muted { color: var(--tf-color-muted); }

.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 130px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }

.outcomes__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.outcomes__page-info { font-size: 0.875rem; color: var(--tf-color-muted); }

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
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.required { color: #ef4444; }
.form-field__input { padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; color: #1e293b; background: #fff; transition: border-color 0.15s; font-family: inherit; }
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38,183,188,0.15); }
.form-field__readonly { padding: 0.45rem 0.65rem; background: #f8fafc; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; color: #64748b; }
.form-error { color: #dc3545; font-size: 0.85rem; }

.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15,23,42,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 380px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1.25rem 1.4rem; display: flex; flex-direction: column; gap: 0.5rem; }
.modal__hint { font-size: 0.85rem; color: var(--tf-color-muted); margin: 0; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }
</style>

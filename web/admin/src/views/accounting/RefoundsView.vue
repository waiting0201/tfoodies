<script setup lang="ts">
import { ref, reactive } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

interface RefoundItem {
  refoundId: string
  refoundCode: string
  refoundDate: string | null
  amount: number
  note: string | null
  memberName: string | null
  returnCode: string | null
  orderCode: string | null
}

interface RefundableMember {
  memberId: string
  memberName: string
}

interface RefundableReturn {
  returnId: string
  returnCode: string
  returnDate: string | null
  orderCode: string
  orderTotal: number
}

interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

const items = ref<RefoundItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 20
const loading = ref(false)
const error = ref('')

const panelMode = ref<'create' | 'edit' | null>(null)
const panelTarget = ref<RefoundItem | null>(null)

const form = reactive({
  memberId: '',
  returnId: '',
  amount: 0,
  refoundDate: '',
  note: '',
})

const refundableMembers = ref<RefundableMember[]>([])
const refundableReturns = ref<RefundableReturn[]>([])
const loadingMembers = ref(false)
const loadingReturns = ref(false)
const saving = ref(false)
const saveError = ref('')

const deleteTarget = ref<RefoundItem | null>(null)
const deleteError = ref('')
const deleting = ref(false)

function fmtDate(d: string | null) { return d ? String(d).slice(0, 10) : '—' }
function fmtMoney(n: number) { return n == null ? '—' : `NT$ ${Number(n).toLocaleString('zh-TW')}` }

async function load() {
  loading.value = true
  error.value = ''
  try {
    const res = await apiFetch<PaginatedResponse<RefoundItem>>(
      `/admin/refounds?page=${page.value}&pageSize=${pageSize}`
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
  if (page.value > 1) { page.value--; load() }
}

function nextPage() {
  if (page.value * pageSize < total.value) { page.value++; load() }
}

async function openCreate() {
  form.memberId = ''
  form.returnId = ''
  form.amount = 0
  form.refoundDate = ''
  form.note = ''
  saveError.value = ''
  refundableReturns.value = []
  panelTarget.value = null
  panelMode.value = 'create'

  loadingMembers.value = true
  try {
    refundableMembers.value = await apiFetch<RefundableMember[]>('/admin/refounds/refundable-members')
  } catch (e) {
    saveError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '無法載入會員清單'
  } finally {
    loadingMembers.value = false
  }
}

async function onMemberChange() {
  form.returnId = ''
  form.amount = 0
  refundableReturns.value = []
  if (!form.memberId) return
  loadingReturns.value = true
  try {
    refundableReturns.value = await apiFetch<RefundableReturn[]>(
      `/admin/refounds/refundable-returns?memberId=${form.memberId}`
    )
  } catch (e) {
    saveError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '無法載入退貨單清單'
  } finally {
    loadingReturns.value = false
  }
}

function onReturnChange() {
  const selected = refundableReturns.value.find(r => r.returnId === form.returnId)
  if (selected) {
    form.amount = selected.orderTotal
  }
}

function openEdit(row: RefoundItem) {
  form.memberId = ''
  form.returnId = ''
  form.amount = row.amount
  form.refoundDate = fmtDate(row.refoundDate) === '—' ? '' : fmtDate(row.refoundDate)
  form.note = row.note ?? ''
  saveError.value = ''
  panelTarget.value = row
  panelMode.value = 'edit'
}

function closePanel() { panelMode.value = null }

async function save() {
  saveError.value = ''

  if (panelMode.value === 'create') {
    if (!form.returnId) { saveError.value = '請選擇退貨單'; return }
    if (!form.amount || form.amount < 1) { saveError.value = '退款金額必須大於 0'; return }
    saving.value = true
    try {
      await apiFetch('/admin/refounds', {
        method: 'POST',
        body: JSON.stringify({
          returnId: form.returnId,
          amount: Number(form.amount),
          refoundDate: form.refoundDate || null,
          note: form.note || null,
        }),
      })
      closePanel()
      await load()
    } catch (e) {
      saveError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
    } finally {
      saving.value = false
    }
  } else {
    if (!form.amount || form.amount < 1) { saveError.value = '退款金額必須大於 0'; return }
    saving.value = true
    try {
      await apiFetch(`/admin/refounds/${panelTarget.value!.refoundId}`, {
        method: 'PUT',
        body: JSON.stringify({
          amount: Number(form.amount),
          refoundDate: form.refoundDate || null,
          note: form.note || null,
        }),
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

function askDelete(row: RefoundItem) {
  deleteTarget.value = row
  deleteError.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/refounds/${deleteTarget.value.refoundId}`, { method: 'DELETE' })
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
  <main class="refounds">
    <div class="refounds__header">
      <h1 class="refounds__title">退款維護</h1>
      <button class="btn btn--primary" @click="openCreate">+ 新增退款</button>
    </div>

    <p v-if="loading" class="refounds__muted">載入中…</p>
    <p v-if="error" class="refounds__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th>退款單號</th>
            <th>會員</th>
            <th>退貨單</th>
            <th>訂單</th>
            <th>日期</th>
            <th>金額</th>
            <th class="action-th"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in items" :key="row.refoundId" class="data-table__row">
            <td class="font-mono">{{ row.refoundCode }}</td>
            <td>{{ row.memberName ?? '—' }}</td>
            <td class="font-mono">{{ row.returnCode ?? '—' }}</td>
            <td class="font-mono">{{ row.orderCode ?? '—' }}</td>
            <td>{{ fmtDate(row.refoundDate) }}</td>
            <td>{{ fmtMoney(row.amount) }}</td>
            <td class="action-cell">
              <button class="btn btn--sm btn--ghost" @click="openEdit(row)">編輯</button>
              <button class="btn btn--sm btn--danger-ghost" @click="askDelete(row)">刪除</button>
            </td>
          </tr>
          <tr v-if="items.length === 0">
            <td colspan="7" class="empty-cell">目前沒有退款資料</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="refounds__pagination">
      <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
      <span class="refounds__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
      <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
    </div>

    <!-- Side panel -->
    <div v-if="panelMode" class="panel-overlay" @click.self="closePanel">
      <aside class="side-panel">
        <div class="panel__header">
          <h2 class="panel__title">{{ panelMode === 'create' ? '新增退款' : '編輯退款' }}</h2>
          <button class="panel__close" @click="closePanel" aria-label="關閉">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <div class="panel__body">
          <!-- Create: 會員 → 退貨單 → 金額 -->
          <template v-if="panelMode === 'create'">
            <div class="form-field">
              <label class="form-field__label">會員 <span class="required">*</span></label>
              <select
                v-model="form.memberId"
                class="form-field__input"
                :disabled="loadingMembers"
                @change="onMemberChange"
              >
                <option value="">{{ loadingMembers ? '載入中…' : '請選擇會員' }}</option>
                <option v-for="m in refundableMembers" :key="m.memberId" :value="m.memberId">
                  {{ m.memberName }}
                </option>
              </select>
            </div>

            <div class="form-field">
              <label class="form-field__label">退貨單 <span class="required">*</span></label>
              <select
                v-model="form.returnId"
                class="form-field__input"
                :disabled="!form.memberId || loadingReturns"
                @change="onReturnChange"
              >
                <option value="">{{ loadingReturns ? '載入中…' : '請選擇退貨單' }}</option>
                <option v-for="r in refundableReturns" :key="r.returnId" :value="r.returnId">
                  {{ r.returnCode }}（訂單 {{ r.orderCode }}）
                </option>
              </select>
            </div>
          </template>

          <!-- Edit: 會員 / 退貨單為唯讀 -->
          <template v-else>
            <div class="form-field">
              <label class="form-field__label">會員</label>
              <div class="form-field__readonly">{{ panelTarget?.memberName ?? '—' }}</div>
            </div>
            <div class="form-field">
              <label class="form-field__label">退貨單</label>
              <div class="form-field__readonly font-mono">{{ panelTarget?.returnCode ?? '—' }}</div>
            </div>
          </template>

          <div class="form-field">
            <label class="form-field__label">退款金額 <span class="required">*</span></label>
            <input
              v-model.number="form.amount"
              type="number"
              min="1"
              class="form-field__input"
              placeholder="請輸入金額（TWD）"
            />
          </div>

          <div class="form-field">
            <label class="form-field__label">退款日期</label>
            <input v-model="form.refoundDate" type="date" class="form-field__input" />
          </div>

          <div class="form-field">
            <label class="form-field__label">備註</label>
            <input v-model="form.note" type="text" class="form-field__input" placeholder="選填備註" />
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
          <h3 class="modal__title">確認刪除退款</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除退款單 <strong class="font-mono">{{ deleteTarget.refoundCode }}</strong> 嗎？</p>
          <p class="modal__hint">刪除後將還原退貨單與訂單的付款狀態</p>
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
.refounds {}
.refounds__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.refounds__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.refounds__error { color: #dc3545; }
.refounds__muted { color: var(--tf-color-muted); }

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
.font-semibold { font-weight: 600; }

.refounds__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.refounds__page-info { font-size: 0.875rem; color: var(--tf-color-muted); }

.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
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
.form-field__readonly { padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; color: #64748b; background: #f8fafc; }
.form-error { color: #dc3545; font-size: 0.85rem; }

.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15,23,42,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 380px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1.25rem 1.4rem; display: flex; flex-direction: column; gap: 0.5rem; }
.modal__hint { font-size: 0.85rem; color: var(--tf-color-muted); margin: 0; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }
</style>

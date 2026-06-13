<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { apiFetch } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────
interface Invoice {
  invoiceId: string
  invoiceCode: string
  invoiceDate: string
  memberName: string
  invoiceStatus: number | null
}

interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

type ModalMode = 'void' | 'allowance' | null

// ─── helpers ──────────────────────────────────────────────────────────────────
function fmtDate(d: string) {
  return d ? d.slice(0, 10) : '—'
}

function statusLabel(s: number | null): string {
  if (s === 2) return '已作廢'
  if (s === 3) return '已折讓'
  return '正常'
}

function statusClass(s: number | null): string {
  if (s === 2) return 'badge--void'
  if (s === 3) return 'badge--allowance'
  return 'badge--normal'
}

// ─── filter state ─────────────────────────────────────────────────────────────
type FilterStatus = '' | '2' | '3'
const filterStatus = ref<FilterStatus>('')

// ─── list state ───────────────────────────────────────────────────────────────
const loading = ref(false)
const error = ref('')
const items = ref<Invoice[]>([])
const page = ref(1)
const total = ref(0)
const PAGE_SIZE = 20
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / PAGE_SIZE)))

async function loadList() {
  loading.value = true
  error.value = ''
  try {
    const qs = new URLSearchParams({
      page: String(page.value),
      pageSize: String(PAGE_SIZE),
    })
    if (filterStatus.value !== '') qs.set('invoiceStatus', filterStatus.value)
    const data = await apiFetch<PagedResult<Invoice>>(`/admin/invoices?${qs}`)
    items.value = data.items ?? []
    total.value = data.total ?? items.value.length
  } catch (e: any) {
    error.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

watch(page, loadList)
watch(filterStatus, () => {
  page.value = 1
  loadList()
})

onMounted(loadList)

// ─── modal state ──────────────────────────────────────────────────────────────
const modalMode = ref<ModalMode>(null)
const modalInvoice = ref<Invoice | null>(null)
const modalReason = ref('')
const modalAmount = ref('')
const modalLoading = ref(false)
const modalError = ref('')

function openVoid(inv: Invoice) {
  modalMode.value = 'void'
  modalInvoice.value = inv
  modalReason.value = ''
  modalAmount.value = ''
  modalError.value = ''
}

function openAllowance(inv: Invoice) {
  modalMode.value = 'allowance'
  modalInvoice.value = inv
  modalReason.value = ''
  modalAmount.value = ''
  modalError.value = ''
}

function closeModal() {
  modalMode.value = null
  modalInvoice.value = null
  modalError.value = ''
}

async function submitModal() {
  if (!modalInvoice.value || !modalMode.value) return
  modalLoading.value = true
  modalError.value = ''
  try {
    const id = modalInvoice.value.invoiceId
    if (modalMode.value === 'void') {
      const body: Record<string, any> = {}
      if (modalReason.value.trim()) body.reason = modalReason.value.trim()
      await apiFetch(`/admin/invoices/${id}/void`, {
        method: 'PATCH',
        body: JSON.stringify(body),
      })
    } else {
      const body: Record<string, any> = {}
      if (modalReason.value.trim()) body.reason = modalReason.value.trim()
      if (modalAmount.value) body.amount = Number(modalAmount.value)
      await apiFetch(`/admin/invoices/${id}/allowance`, {
        method: 'PATCH',
        body: JSON.stringify(body),
      })
    }
    closeModal()
    await loadList()
  } catch (e: any) {
    modalError.value = e.message ?? '操作失敗'
  } finally {
    modalLoading.value = false
  }
}
</script>

<template>
  <main class="inv">
    <h1 class="inv__title">發票管理</h1>

    <!-- Filter bar -->
    <div class="filter-bar">
      <div class="filter-group">
        <label class="filter-label">篩選狀態</label>
        <div class="seg-ctrl">
          <button
            v-for="opt in ([
              { label: '全部', value: '' },
              { label: '正常', value: 'normal' },
              { label: '已作廢', value: '2' },
              { label: '已折讓', value: '3' },
            ] as { label: string; value: string }[])"
            :key="opt.value"
            class="seg-ctrl__btn"
            :class="{
              'seg-ctrl__btn--active':
                (opt.value === 'normal' && filterStatus === '') ||
                (opt.value === '' && filterStatus === '' && opt.label === '全部') ||
                filterStatus === opt.value,
            }"
            @click="filterStatus = (opt.value === 'normal' ? '' : opt.value) as FilterStatus"
          >{{ opt.label }}</button>
        </div>
      </div>
    </div>

    <!-- State -->
    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="error" class="state-msg state-msg--error">{{ error }}</div>

    <!-- Table -->
    <template v-else>
      <div class="card">
        <table class="data-table">
          <thead>
            <tr>
              <th>發票號碼</th>
              <th>開立日期</th>
              <th>會員姓名</th>
              <th>狀態</th>
              <th class="action-th"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="inv in items" :key="inv.invoiceId" class="data-table__row">
              <td class="font-mono text-sm">{{ inv.invoiceCode }}</td>
              <td>{{ fmtDate(inv.invoiceDate) }}</td>
              <td>{{ inv.memberName }}</td>
              <td>
                <span class="badge" :class="statusClass(inv.invoiceStatus)">
                  {{ statusLabel(inv.invoiceStatus) }}
                </span>
              </td>
              <td class="action-cell">
                <template v-if="inv.invoiceStatus === null || inv.invoiceStatus === undefined || inv.invoiceStatus === 0">
                  <!-- 僅正常狀態顯示操作按鈕（invoiceStatus 為 null 代表一般） -->
                </template>
                <template v-if="inv.invoiceStatus !== 2 && inv.invoiceStatus !== 3">
                  <button class="btn btn--sm btn--danger" @click="openVoid(inv)">作廢</button>
                  <button class="btn btn--sm btn--warning" style="margin-left:0.375rem" @click="openAllowance(inv)">折讓</button>
                </template>
              </td>
            </tr>
            <tr v-if="items.length === 0">
              <td colspan="5" class="empty-cell">目前沒有發票資料</td>
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

    <!-- ── Modal overlay ──────────────────────────────────────────────────── -->
    <Teleport to="body">
      <div v-if="modalMode" class="modal-overlay" @click.self="closeModal">
        <div class="modal" role="dialog" :aria-label="modalMode === 'void' ? '作廢發票' : '折讓發票'">
          <div class="modal__header">
            <h2 class="modal__title">
              {{ modalMode === 'void' ? '作廢發票' : '折讓發票' }}
            </h2>
            <button class="modal__close" aria-label="關閉" @click="closeModal">
              <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div class="modal__body">
            <div class="info-row">
              <span class="info-label">發票號碼</span>
              <span class="info-value font-mono">{{ modalInvoice?.invoiceCode }}</span>
            </div>
            <div class="info-row">
              <span class="info-label">會員</span>
              <span class="info-value">{{ modalInvoice?.memberName }}</span>
            </div>

            <div v-if="modalMode === 'allowance'" class="form-field mt-4">
              <label class="form-field__label" for="modal-amount">折讓金額</label>
              <input
                id="modal-amount"
                v-model="modalAmount"
                type="number"
                min="1"
                placeholder="請輸入折讓金額"
                class="form-field__input"
              />
            </div>

            <div class="form-field mt-3">
              <label class="form-field__label" :for="'modal-reason'">
                原因 <span class="form-field__hint">（選填）</span>
              </label>
              <textarea
                id="modal-reason"
                v-model="modalReason"
                rows="3"
                placeholder="請輸入原因…"
                class="form-field__input"
              ></textarea>
            </div>

            <div v-if="modalError" class="form-error mt-2">{{ modalError }}</div>
          </div>

          <div class="modal__footer">
            <button class="btn btn--ghost" :disabled="modalLoading" @click="closeModal">取消</button>
            <button
              class="btn"
              :class="modalMode === 'void' ? 'btn--danger' : 'btn--warning'"
              :disabled="modalLoading || (modalMode === 'allowance' && !modalAmount)"
              @click="submitModal"
            >
              {{ modalLoading ? '處理中…' : (modalMode === 'void' ? '確認作廢' : '確認折讓') }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </main>
</template>

<style scoped>
.inv { }
.inv__title {
  font-size: 1.5rem;
  font-weight: 700;
  color: #1e293b;
  margin-bottom: 1.5rem;
  letter-spacing: -0.02em;
}

/* Filter bar */
.filter-bar { display: flex; gap: 1.5rem; align-items: flex-end; margin-bottom: 1.25rem; flex-wrap: wrap; }
.filter-group { display: flex; flex-direction: column; gap: 0.35rem; }
.filter-label { font-size: 0.75rem; font-weight: 600; color: #64748b; }

/* Segmented control */
.seg-ctrl { display: flex; background: #f1f5f9; border-radius: 8px; padding: 3px; gap: 2px; }
.seg-ctrl__btn { padding: 0.3rem 0.85rem; font-size: 0.85rem; font-weight: 500; border: none; background: transparent; cursor: pointer; border-radius: 6px; color: #64748b; transition: all 0.15s; }
.seg-ctrl__btn--active { background: #fff; color: #4f46e5; box-shadow: 0 1px 3px rgba(0,0,0,0.12); font-weight: 600; }
.seg-ctrl__btn:not(.seg-ctrl__btn--active):hover { background: rgba(255,255,255,0.5); color: #334155; }

/* Card */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }

/* Tables */
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; border-bottom: 1px solid #e2e8f0; font-size: 0.875rem; font-weight: 600; }
.action-th { width: 140px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: #94a3b8; padding: 3rem; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }

/* Badge */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.76rem; font-weight: 600; }
.badge--normal   { background: #dcfce7; color: #166534; }
.badge--void     { background: #fee2e2; color: #991b1b; }
.badge--allowance { background: #fef3c7; color: #92400e; }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: all 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--ghost { background: transparent; color: #4f46e5; border-color: #c7d2fe; }
.btn--ghost:hover:not(:disabled) { background: #eef2ff; }
.btn--danger { background: #ef4444; color: #fff; border-color: #ef4444; }
.btn--danger:hover:not(:disabled) { background: #dc2626; border-color: #dc2626; }
.btn--warning { background: #f59e0b; color: #fff; border-color: #f59e0b; }
.btn--warning:hover:not(:disabled) { background: #d97706; border-color: #d97706; }
/* Action column */
.action-cell { white-space: nowrap; text-align: right; }

/* Pagination */
.pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.pagination__info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* State messages */
.state-msg { padding: 2rem; text-align: center; color: #94a3b8; }
.state-msg--error { color: #dc2626; }

/* Modal overlay */
.modal-overlay {
  position: fixed; inset: 0; z-index: 50;
  background: rgba(15, 23, 42, 0.55);
  backdrop-filter: blur(2px);
  display: flex; align-items: center; justify-content: center;
  padding: 1rem;
  animation: fadeIn 0.15s ease;
}
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }

.modal {
  background: #fff;
  border-radius: 14px;
  width: 100%;
  max-width: 480px;
  box-shadow: 0 20px 60px rgba(0,0,0,0.2);
  animation: slideUp 0.2s ease;
}
@keyframes slideUp { from { opacity: 0; transform: translateY(16px); } to { opacity: 1; transform: none; } }

.modal__header { display: flex; align-items: center; justify-content: space-between; padding: 1.25rem 1.5rem 0; }
.modal__title { font-size: 1.1rem; font-weight: 700; color: #1e293b; }
.modal__close { background: none; border: none; cursor: pointer; color: #94a3b8; padding: 0.25rem; border-radius: 4px; display: flex; align-items: center; }
.modal__close:hover { color: #475569; background: #f1f5f9; }

.modal__body { padding: 1.25rem 1.5rem; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 0 1.5rem 1.25rem; }

/* Info rows inside modal */
.info-row { display: flex; gap: 0.75rem; align-items: baseline; padding: 0.3rem 0; }
.info-label { font-size: 0.8rem; color: #94a3b8; font-weight: 500; min-width: 4.5rem; }
.info-value { font-size: 0.9rem; color: #334155; }

/* Form fields */
.form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.form-field__hint { font-weight: 400; color: #94a3b8; }
.form-field__input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem;
  color: #1e293b;
  transition: border-color 0.15s, box-shadow 0.15s;
  resize: vertical;
  font-family: inherit;
}
.form-field__input:focus { outline: none; border-color: #6366f1; box-shadow: 0 0 0 3px rgba(99,102,241,0.15); }
.form-error { color: #dc2626; font-size: 0.85rem; }
.mt-4 { margin-top: 1rem; }
.mt-3 { margin-top: 0.75rem; }
.mt-2 { margin-top: 0.5rem; }
</style>

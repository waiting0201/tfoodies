<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

const router = useRouter()

// ── 型別 ─────────────────────────────────────────────────────────

interface ExpenditureRow {
  expenditureId: string
  expenditureCode: string
  expenditureDate: string
  status: 0 | 1 | 2
  sourceType: string
  note: string | null
  supplierTitle: string | null
  totalAmount: number
  paidAmount: number
}

interface ExpenditureDetail {
  expenditureDetailId: string
  accountingCode: string
  accountingTitle: string
  summary: string | null
  price: number
  purchaseDetailId: string | null
}

interface OutcomeRow {
  outcomeId: string
  outcomeCode: string
  outcomeDate: string
  amount: number
  note: string | null
}

interface ExpenditureExpanded {
  expenditure: {
    note: string | null
    supplierTitle: string | null
  }
  details: ExpenditureDetail[]
  outcomes: OutcomeRow[]
}

// ── 本地助手 ──────────────────────────────────────────────────────

function fmtDate(d: string | null) { return d ? String(d).slice(0, 10) : '—' }
function fmtMoney(n: number) { return n == null ? '—' : `NT$ ${Number(n).toLocaleString('zh-TW')}` }

// ── 清單 ─────────────────────────────────────────────────────────

const items = ref<ExpenditureRow[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 20
const loading = ref(false)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({
      page: String(page.value),
      pageSize: String(pageSize),
    })
    const data = await apiFetch<{ items: ExpenditureRow[]; total: number }>(`/admin/expenditures?${params}`)
    items.value = data.items
    total.value = data.total
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

function prevPage() { if (page.value > 1) { page.value--; load() } }
function nextPage() { if (page.value * pageSize < total.value) { page.value++; load() } }

// ── 行內展開詳情 ──────────────────────────────────────────────────

const expandedId = ref<string | null>(null)
const expandedData = ref<ExpenditureExpanded | null>(null)
const expandLoading = ref(false)

async function toggleRow(id: string) {
  if (expandedId.value === id) {
    expandedId.value = null
    expandedData.value = null
    paymentFormId.value = null
    return
  }
  expandedId.value = id
  expandedData.value = null
  paymentFormId.value = null
  await loadDetail(id)
}

async function loadDetail(id: string) {
  expandLoading.value = true
  try {
    expandedData.value = await apiFetch<ExpenditureExpanded>(`/admin/expenditures/${id}`)
  } catch {
    expandedData.value = null
  } finally {
    expandLoading.value = false
  }
}

// ── 行內付款表單 ──────────────────────────────────────────────────

const paymentFormId = ref<string | null>(null)
const paymentForm = reactive({ amount: '', outcomeDate: '', note: '' })
const paymentSaving = ref(false)
const paymentError = ref('')

function openPaymentForm(id: string) {
  paymentFormId.value = id
  paymentForm.amount = ''
  paymentForm.outcomeDate = ''
  paymentForm.note = ''
  paymentError.value = ''
}

function closePaymentForm() {
  paymentFormId.value = null
}

async function submitPayment(expenditureId: string) {
  if (!paymentForm.amount || Number(paymentForm.amount) <= 0) {
    paymentError.value = '請輸入有效金額'
    return
  }
  if (!paymentForm.outcomeDate) {
    paymentError.value = '請選擇付款日期'
    return
  }
  paymentSaving.value = true
  paymentError.value = ''
  try {
    await apiFetch('/admin/outcomes', {
      method: 'POST',
      body: JSON.stringify({
        expenditureId,
        amount: Number(paymentForm.amount),
        outcomeDate: paymentForm.outcomeDate,
        note: paymentForm.note || null,
      }),
    })
    paymentFormId.value = null
    await load()
    if (expandedId.value === expenditureId) {
      await loadDetail(expenditureId)
    }
  } catch (e) {
    paymentError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '付款失敗'
  } finally {
    paymentSaving.value = false
  }
}

// ── 刪除 Modal ────────────────────────────────────────────────────

const deleteTarget = ref<ExpenditureRow | null>(null)
const deleteError = ref('')
const deleting = ref(false)

function askDelete(row: ExpenditureRow) {
  deleteTarget.value = row
  deleteError.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/expenditures/${deleteTarget.value.expenditureId}`, { method: 'DELETE' })
    deleteTarget.value = null
    if (expandedId.value) { expandedId.value = null; expandedData.value = null }
    await load()
  } catch (e) {
    deleteError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '刪除失敗'
  } finally {
    deleting.value = false
  }
}

// ── badge helper ──────────────────────────────────────────────────

function statusBadgeClass(s: 0 | 1 | 2) {
  if (s === 2) return 'badge badge--paid'
  if (s === 1) return 'badge badge--partial'
  return 'badge badge--unpaid'
}
function statusLabel(s: 0 | 1 | 2) {
  if (s === 2) return '已付款'
  if (s === 1) return '部分付款'
  return '未付款'
}

// ── 初始載入 ──────────────────────────────────────────────────────

load()
</script>

<template>
  <main class="exps">
    <!-- 頁首 -->
    <div class="exps__header">
      <h1 class="exps__title">營業支出維護</h1>
      <button class="btn btn--primary" @click="router.push('/admin/expenditures/new')">+ 新增支出</button>
    </div>

    <p v-if="loading" class="exps__muted">載入中…</p>
    <p v-if="error" class="exps__error">{{ error }}</p>

    <!-- 表格卡片 -->
    <div v-if="!loading" class="card">
      <div class="exps__table-wrap">
        <table class="data-table exps__table">
          <thead>
            <tr>
              <th>支出單號</th>
              <th>日期</th>
              <th>供應商</th>
              <th style="text-align:right">應付金額</th>
              <th style="text-align:right">已付金額</th>
              <th style="text-align:right">剩餘金額</th>
              <th>狀態</th>
              <th class="action-th">操作</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="row in items" :key="row.expenditureId">
              <!-- 資料列 -->
              <tr
                class="data-table__row exps__data-row"
                @click="toggleRow(row.expenditureId)"
              >
                <td class="font-mono">{{ row.expenditureCode }}</td>
                <td>{{ fmtDate(row.expenditureDate) }}</td>
                <td>{{ row.supplierTitle ?? '—' }}</td>
                <td style="text-align:right">{{ fmtMoney(row.totalAmount) }}</td>
                <td style="text-align:right">{{ fmtMoney(row.paidAmount) }}</td>
                <td style="text-align:right">{{ fmtMoney(row.totalAmount - row.paidAmount) }}</td>
                <td><span :class="statusBadgeClass(row.status)">{{ statusLabel(row.status) }}</span></td>
                <td class="action-cell" @click.stop>
                  <button
                    v-if="row.status !== 2"
                    class="btn btn--sm btn--accent"
                    @click="paymentFormId === row.expenditureId ? closePaymentForm() : openPaymentForm(row.expenditureId)"
                  >付款</button>
                  <button
                    class="btn btn--sm btn--ghost"
                    @click="router.push('/admin/expenditures/' + row.expenditureId + '/edit')"
                  >編輯</button>
                  <button
                    class="btn btn--sm btn--danger-ghost"
                    @click="askDelete(row)"
                  >刪除</button>
                </td>
              </tr>

              <!-- 行內付款表單列 -->
              <tr v-if="paymentFormId === row.expenditureId" :key="'pay-' + row.expenditureId">
                <td colspan="8" style="padding:0">
                  <div class="inline-form" @click.stop>
                    <p class="inline-form__title">記錄付款</p>
                    <div class="inline-form__fields">
                      <div class="inline-form__field">
                        <label class="inline-form__label">金額 <span class="req">*</span></label>
                        <input
                          v-model="paymentForm.amount"
                          type="number"
                          min="1"
                          class="inline-form__input"
                          placeholder="0"
                        />
                      </div>
                      <div class="inline-form__field">
                        <label class="inline-form__label">付款日期 <span class="req">*</span></label>
                        <input
                          v-model="paymentForm.outcomeDate"
                          type="date"
                          class="inline-form__input"
                        />
                      </div>
                      <div class="inline-form__field inline-form__field--grow">
                        <label class="inline-form__label">備註</label>
                        <input
                          v-model="paymentForm.note"
                          type="text"
                          class="inline-form__input"
                          placeholder="備註（選填）"
                        />
                      </div>
                      <div class="inline-form__actions">
                        <button class="btn btn--sm btn--ghost" @click="closePaymentForm">取消</button>
                        <button
                          class="btn btn--sm btn--accent"
                          :disabled="paymentSaving"
                          @click="submitPayment(row.expenditureId)"
                        >{{ paymentSaving ? '送出中…' : '確認付款' }}</button>
                      </div>
                    </div>
                    <p v-if="paymentError" class="inline-form__error">{{ paymentError }}</p>
                  </div>
                </td>
              </tr>

              <!-- 行內展開詳情列 -->
              <tr v-if="expandedId === row.expenditureId" :key="'detail-' + row.expenditureId">
                <td colspan="8" style="padding:0">
                  <div class="detail-panel">
                    <p v-if="expandLoading" class="detail-panel__loading">載入中…</p>
                    <template v-if="expandedData">
                      <!-- 明細子表格 -->
                      <p class="detail-panel__section-title">支出明細</p>
                      <table class="sub-table">
                        <thead>
                          <tr>
                            <th>科目</th>
                            <th>摘要</th>
                            <th style="text-align:right">金額</th>
                          </tr>
                        </thead>
                        <tbody>
                          <tr v-for="d in expandedData.details" :key="d.expenditureDetailId">
                            <td class="font-mono">{{ d.accountingCode }} {{ d.accountingTitle }}</td>
                            <td>{{ d.summary ?? '—' }}</td>
                            <td style="text-align:right">{{ fmtMoney(d.price) }}</td>
                          </tr>
                          <tr v-if="expandedData.details.length === 0">
                            <td colspan="3" class="sub-empty">無明細資料</td>
                          </tr>
                        </tbody>
                      </table>

                      <!-- 付款紀錄子表格 -->
                      <p class="detail-panel__section-title" style="margin-top:1rem">付款紀錄</p>
                      <template v-if="expandedData.outcomes.length > 0">
                        <table class="sub-table">
                          <thead>
                            <tr>
                              <th>付款單號</th>
                              <th>日期</th>
                              <th style="text-align:right">金額</th>
                              <th>備註</th>
                            </tr>
                          </thead>
                          <tbody>
                            <tr v-for="o in expandedData.outcomes" :key="o.outcomeId">
                              <td class="font-mono">{{ o.outcomeCode }}</td>
                              <td>{{ fmtDate(o.outcomeDate) }}</td>
                              <td style="text-align:right">{{ fmtMoney(o.amount) }}</td>
                              <td>{{ o.note ?? '—' }}</td>
                            </tr>
                          </tbody>
                        </table>
                      </template>
                      <p v-else class="detail-panel__empty">尚無付款紀錄</p>
                    </template>
                  </div>
                </td>
              </tr>
            </template>

            <tr v-if="items.length === 0">
              <td colspan="8" class="empty-cell">目前沒有支出資料</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- 分頁列 -->
    <div class="exps__pagination">
      <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
      <span class="exps__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
      <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
    </div>

    <!-- 刪除 Modal -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget = null">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認刪除支出單</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除支出單 <strong>{{ deleteTarget.expenditureCode }}</strong> 嗎？</p>
          <p class="modal__hint">若已有付款記錄，系統將拒絕刪除。</p>
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
.exps {}
.exps__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.exps__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.exps__error { color: #dc3545; }
.exps__muted { color: var(--tf-color-muted); }

/* ── 表格卡片 ── */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.exps__table-wrap { overflow-x: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.exps__table { min-width: 700px; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 160px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.exps__data-row { cursor: pointer; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }

/* ── Badge ── */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 500; white-space: nowrap; }
.badge--paid    { background: #d4edda; color: #155724; }
.badge--unpaid  { background: #fff3cd; color: #856404; }
.badge--partial { background: #fff3cd; color: #856404; }

/* ── 分頁列 ── */
.exps__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.exps__page-info  { font-size: 0.875rem; color: var(--tf-color-muted); }

/* ── 行內展開 detail-panel ── */
.detail-panel {
  background: rgba(38, 183, 188, 0.04);
  border-left: 3px solid var(--tf-color-primary);
  padding: 1rem 1.25rem;
}
.detail-panel__loading { color: var(--tf-color-muted); font-size: 0.875rem; margin: 0; }
.detail-panel__section-title { font-size: 0.82rem; font-weight: 600; color: var(--tf-color-primary-dark); margin: 0 0 0.5rem; }
.detail-panel__empty { font-size: 0.82rem; color: var(--tf-color-muted); margin: 0; }

/* ── 子表格 ── */
.sub-table { width: 100%; border-collapse: collapse; font-size: 0.82rem; }
.sub-table th { background: rgba(38,183,188,0.12); color: var(--tf-color-primary-dark); text-align: left; padding: 0.4rem 0.65rem; font-size: 0.8rem; font-weight: 600; white-space: nowrap; }
.sub-table td { padding: 0.4rem 0.65rem; border-bottom: 1px solid rgba(38,183,188,0.1); color: #334155; }
.sub-table tr:last-child td { border-bottom: none; }
.sub-empty { text-align: center; color: var(--tf-color-muted); padding: 1rem; }

/* ── 行內付款表單 ── */
.inline-form { background: #fffbf7; border-left: 3px solid var(--tf-color-accent); padding: 1rem 1.25rem; }
.inline-form__title { margin: 0 0 0.75rem; font-size: 0.95rem; color: var(--tf-color-accent); font-weight: 600; }
.inline-form__fields { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 0.75rem; }
.inline-form__field { display: flex; flex-direction: column; gap: 0.3rem; }
.inline-form__field--grow { flex: 1 1 200px; }
.inline-form__label { font-size: 0.8rem; font-weight: 500; color: #374151; }
.req { color: var(--tf-color-accent); margin-left: 0.1rem; }
.inline-form__input { padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; font-family: inherit; background: #fff; transition: border-color 0.15s; }
.inline-form__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.inline-form__actions { display: flex; gap: 0.5rem; align-items: flex-end; }
.inline-form__error { color: #dc3545; font-size: 0.85rem; margin: 0.5rem 0 0; }

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
.btn--accent { background: var(--tf-color-accent); color: #fff; border-color: var(--tf-color-accent); }
.btn--accent:hover:not(:disabled) { opacity: 0.85; }
.btn--danger { background: #dc3545; color: #fff; border-color: #dc3545; }
.btn--danger:hover:not(:disabled) { background: #b02a37; }
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }

/* ── 刪除 Modal ── */
.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15,23,42,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 380px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1.25rem 1.4rem; display: flex; flex-direction: column; gap: 0.5rem; }
.modal__hint { font-size: 0.85rem; color: var(--tf-color-muted); margin: 0; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }
.form-error { color: #dc3545; font-size: 0.85rem; margin: 0; }
</style>

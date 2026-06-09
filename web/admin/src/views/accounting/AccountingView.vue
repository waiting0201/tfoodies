<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────
interface Expenditure {
  expenditureId: string
  expenditureCode: string
  supplierName: string
  expenditureDate: string
  payStatus: 0 | 1 | 2
}
interface ExpenditureDetail {
  expenditureId: string
  expenditureCode: string
  supplierName: string
  expenditureDate: string
  payStatus: 0 | 1 | 2
  totalAmount: number
  outcomes: Outcome[]
}
interface Outcome {
  outcomeId: string
  amount: number
  outcomeDate: string
}
interface ArInvoice {
  arInvoiceId: string
  invoiceCode: string
  invoiceDate: string
  amount: number
}
interface Income {
  incomeId: string
  incomeCode: string
  incomeDate: string
  amount: number
}
interface Return {
  returnId: string
  returnCode: string
  memberName: string
  orderCode: string
  returnDate: string
  receiveStatus: number
  refundStatus: number
}
interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

// ─── tab state ────────────────────────────────────────────────────────────────
type Tab = 'ap' | 'ar' | 'refund'
const activeTab = ref<Tab>('ap')

// ─── shared helpers ───────────────────────────────────────────────────────────
const PAY_STATUS_LABEL: Record<number, string> = { 0: '未付', 1: '部分付款', 2: '已付' }
const PAY_STATUS_CLASS: Record<number, string> = { 0: 'badge--unpaid', 1: 'badge--partial', 2: 'badge--paid' }

function fmtDate(d: string) {
  return d ? d.slice(0, 10) : '—'
}
function fmtMoney(n: number) {
  return n?.toLocaleString('zh-TW', { style: 'currency', currency: 'TWD', maximumFractionDigits: 0 }) ?? '—'
}

// ─── AP ───────────────────────────────────────────────────────────────────────
const apLoading = ref(false)
const apError = ref('')
const apItems = ref<Expenditure[]>([])
const apPage = ref(1)
const apTotal = ref(0)
const AP_PAGE_SIZE = 20
const apTotalPages = computed(() => Math.max(1, Math.ceil(apTotal.value / AP_PAGE_SIZE)))

const apExpandedId = ref<string | null>(null)
const apDetail = ref<ExpenditureDetail | null>(null)
const apDetailLoading = ref(false)
const apDetailError = ref('')

const payForm = ref<{ expenditureId: string; amount: string; outcomeDate: string } | null>(null)
const payLoading = ref(false)
const payError = ref('')

async function loadAp() {
  apLoading.value = true
  apError.value = ''
  try {
    const data = await apiFetch<PagedResult<Expenditure>>(
      `/admin/expenditures?page=${apPage.value}&pageSize=${AP_PAGE_SIZE}`
    )
    apItems.value = data.items ?? (data as any)
    apTotal.value = data.totalCount ?? apItems.value.length
  } catch (e: any) {
    apError.value = e.message ?? '載入失敗'
  } finally {
    apLoading.value = false
  }
}

async function toggleApRow(id: string) {
  if (apExpandedId.value === id) {
    apExpandedId.value = null
    apDetail.value = null
    payForm.value = null
    return
  }
  apExpandedId.value = id
  apDetail.value = null
  apDetailError.value = ''
  payForm.value = null
  apDetailLoading.value = true
  try {
    apDetail.value = await apiFetch<ExpenditureDetail>(`/admin/expenditures/${id}`)
  } catch (e: any) {
    apDetailError.value = e.message ?? '載入失敗'
  } finally {
    apDetailLoading.value = false
  }
}

function openPayForm(id: string) {
  payForm.value = { expenditureId: id, amount: '', outcomeDate: '' }
  payError.value = ''
}

async function submitPay() {
  if (!payForm.value) return
  payLoading.value = true
  payError.value = ''
  try {
    const body: Record<string, any> = {
      expenditureId: payForm.value.expenditureId,
      amount: Number(payForm.value.amount),
    }
    if (payForm.value.outcomeDate) body.outcomeDate = payForm.value.outcomeDate
    await apiFetch('/admin/outcomes', { method: 'POST', body: JSON.stringify(body) })
    payForm.value = null
    await loadAp()
    if (apExpandedId.value) await toggleApRow(apExpandedId.value)
  } catch (e: any) {
    payError.value = e.message ?? '付款失敗'
  } finally {
    payLoading.value = false
  }
}

watch(apPage, loadAp)

// ─── AP Delete ────────────────────────────────────────────────────────────────
type DeleteTarget =
  | { kind: 'expenditure'; id: string; label: string }
  | { kind: 'outcome'; id: string; label: string; expenditureId: string }
  | { kind: 'ar-invoice'; id: string; label: string }
  | { kind: 'income'; id: string; label: string }

const deleteTarget = ref<DeleteTarget | null>(null)
const deleteError = ref('')
const deleting = ref(false)

function askDelete(target: DeleteTarget) {
  deleteTarget.value = target
  deleteError.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    const t = deleteTarget.value
    if (t.kind === 'expenditure') {
      await apiFetch(`/admin/expenditures/${t.id}`, { method: 'DELETE' })
      deleteTarget.value = null
      await loadAp()
    } else if (t.kind === 'outcome') {
      await apiFetch(`/admin/outcomes/${t.id}`, { method: 'DELETE' })
      deleteTarget.value = null
      await loadAp()
      if (apExpandedId.value === t.expenditureId) {
        apDetail.value = null
        await toggleApRow(t.expenditureId)
      }
    } else if (t.kind === 'ar-invoice') {
      await apiFetch(`/admin/ar-invoices/${t.id}`, { method: 'DELETE' })
      deleteTarget.value = null
      await loadAr()
    } else if (t.kind === 'income') {
      await apiFetch(`/admin/incomes/${t.id}`, { method: 'DELETE' })
      deleteTarget.value = null
      await loadAr()
    }
  } catch (e) {
    deleteError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '刪除失敗'
  } finally {
    deleting.value = false
  }
}

// ─── AR ───────────────────────────────────────────────────────────────────────
const arInvoices = ref<ArInvoice[]>([])
const arIncomes = ref<Income[]>([])
const arLoading = ref(false)
const arError = ref('')

async function loadAr() {
  arLoading.value = true
  arError.value = ''
  try {
    const [inv, inc] = await Promise.all([
      apiFetch<ArInvoice[] | PagedResult<ArInvoice>>('/admin/ar-invoices'),
      apiFetch<Income[] | PagedResult<Income>>('/admin/incomes'),
    ])
    arInvoices.value = Array.isArray(inv) ? inv : (inv as any).items ?? []
    arIncomes.value = Array.isArray(inc) ? inc : (inc as any).items ?? []
  } catch (e: any) {
    arError.value = e.message ?? '載入失敗'
  } finally {
    arLoading.value = false
  }
}

// ─── Refund (returns awaiting refund) ─────────────────────────────────────────
const refundItems = ref<Return[]>([])
const refundLoading = ref(false)
const refundError = ref('')
const refundingId = ref<string | null>(null)

async function loadRefundReturns() {
  refundLoading.value = true
  refundError.value = ''
  try {
    const data = await apiFetch<Return[] | PagedResult<Return>>('/admin/returns?receivestatus=1')
    refundItems.value = Array.isArray(data) ? data : (data as any).items ?? []
  } catch (e: any) {
    refundError.value = e.message ?? '載入失敗'
  } finally {
    refundLoading.value = false
  }
}

async function doRefund(id: string) {
  refundingId.value = id
  try {
    await apiFetch(`/admin/returns/${id}/refund`, { method: 'PATCH' })
    await loadRefundReturns()
  } catch (e: any) {
    refundError.value = e.message ?? '退款失敗'
  } finally {
    refundingId.value = null
  }
}

// ─── lifecycle ────────────────────────────────────────────────────────────────
onMounted(loadAp)

function switchTab(t: Tab) {
  activeTab.value = t
  if (t === 'ap' && apItems.value.length === 0) loadAp()
  if (t === 'ar' && arInvoices.value.length === 0) loadAr()
  if (t === 'refund' && refundItems.value.length === 0) loadRefundReturns()
}
</script>

<template>
  <main class="acct">
    <h1 class="acct__title">財務管理</h1>

    <!-- Tab bar -->
    <div class="tabs" role="tablist">
      <button
        v-for="t in ([['ap','應付 AP'],['ar','應收 AR'],['refund','退款']] as [Tab, string][])"
        :key="t[0]"
        class="tabs__btn"
        :class="{ 'tabs__btn--active': activeTab === t[0] }"
        role="tab"
        :aria-selected="activeTab === t[0]"
        @click="switchTab(t[0])"
      >{{ t[1] }}</button>
    </div>

    <!-- ── AP tab ───────────────────────────────────────────────────────────── -->
    <section v-if="activeTab === 'ap'" class="tab-panel">
      <div v-if="apLoading" class="state-msg">載入中…</div>
      <div v-else-if="apError" class="state-msg state-msg--error">{{ apError }}</div>
      <template v-else>
        <table class="data-table">
          <thead>
            <tr>
              <th>應付單號</th><th>供應商</th><th>日期</th><th>狀態</th><th></th>
            </tr>
          </thead>
          <tbody>
            <template v-for="row in apItems" :key="row.expenditureId">
              <tr class="data-table__row" :class="{ 'data-table__row--expanded': apExpandedId === row.expenditureId }" @click="toggleApRow(row.expenditureId)">
                <td>{{ row.expenditureCode }}</td>
                <td>{{ row.supplierName }}</td>
                <td>{{ fmtDate(row.expenditureDate) }}</td>
                <td>
                  <span class="badge" :class="PAY_STATUS_CLASS[row.payStatus]">
                    {{ PAY_STATUS_LABEL[row.payStatus] }}
                  </span>
                </td>
                <td class="action-cell">
                  <button
                    v-if="row.payStatus !== 2"
                    class="btn btn--sm btn--primary"
                    @click.stop="openPayForm(row.expenditureId)"
                  >付款</button>
                  <button
                    class="btn btn--sm btn--danger-ghost"
                    @click.stop="askDelete({ kind: 'expenditure', id: row.expenditureId, label: row.expenditureCode })"
                  >刪除</button>
                </td>
              </tr>

              <!-- Inline pay form -->
              <tr v-if="payForm && payForm.expenditureId === row.expenditureId" class="inline-form-row">
                <td colspan="5">
                  <div class="inline-form">
                    <h4 class="inline-form__title">新增付款紀錄</h4>
                    <div class="form-row">
                      <label>金額 <input v-model="payForm.amount" type="number" min="1" placeholder="0" /></label>
                      <label>付款日期 <input v-model="payForm.outcomeDate" type="date" /></label>
                    </div>
                    <div v-if="payError" class="form-error">{{ payError }}</div>
                    <div class="form-actions">
                      <button class="btn btn--ghost" @click="payForm = null">取消</button>
                      <button class="btn btn--primary" :disabled="payLoading" @click="submitPay">
                        {{ payLoading ? '處理中…' : '確認付款' }}
                      </button>
                    </div>
                  </div>
                </td>
              </tr>

              <!-- Inline detail -->
              <tr v-if="apExpandedId === row.expenditureId" class="detail-row">
                <td colspan="5">
                  <div class="detail-panel">
                    <div v-if="apDetailLoading" class="state-msg">載入中…</div>
                    <div v-else-if="apDetailError" class="state-msg state-msg--error">{{ apDetailError }}</div>
                    <template v-else-if="apDetail">
                      <div class="detail-meta">
                        <span>總金額：<strong>{{ fmtMoney(apDetail.totalAmount) }}</strong></span>
                      </div>
                      <table v-if="apDetail.outcomes?.length" class="sub-table">
                        <thead><tr><th>付款記錄</th><th>金額</th><th>付款日</th><th></th></tr></thead>
                        <tbody>
                          <tr v-for="o in apDetail.outcomes" :key="o.outcomeId">
                            <td>{{ o.outcomeId.slice(0, 8) }}…</td>
                            <td>{{ fmtMoney(o.amount) }}</td>
                            <td>{{ fmtDate(o.outcomeDate) }}</td>
                            <td class="action-cell">
                              <button
                                class="btn btn--sm btn--danger-ghost"
                                @click="askDelete({ kind: 'outcome', id: o.outcomeId, label: fmtMoney(o.amount), expenditureId: apDetail!.expenditureId })"
                              >刪除</button>
                            </td>
                          </tr>
                        </tbody>
                      </table>
                      <p v-else class="sub-empty">尚無付款紀錄</p>
                    </template>
                  </div>
                </td>
              </tr>
            </template>
            <tr v-if="apItems.length === 0">
              <td colspan="5" class="empty-cell">目前沒有應付帳款資料</td>
            </tr>
          </tbody>
        </table>

        <!-- Pagination -->
        <div v-if="apTotalPages > 1" class="pagination">
          <button class="btn btn--ghost btn--sm" :disabled="apPage <= 1" @click="apPage--">‹ 上一頁</button>
          <span class="pagination__info">第 {{ apPage }} 頁（共 {{ apTotal }} 筆）</span>
          <button class="btn btn--ghost btn--sm" :disabled="apPage >= apTotalPages" @click="apPage++">下一頁 ›</button>
        </div>
      </template>
    </section>

    <!-- ── AR tab ───────────────────────────────────────────────────────────── -->
    <section v-if="activeTab === 'ar'" class="tab-panel">
      <div v-if="arLoading" class="state-msg">載入中…</div>
      <div v-else-if="arError" class="state-msg state-msg--error">{{ arError }}</div>
      <template v-else>
        <h3 class="sub-section-title">應收發票</h3>
        <table class="data-table">
          <thead><tr><th>發票代號</th><th>日期</th><th>金額</th><th></th></tr></thead>
          <tbody>
            <tr v-for="inv in arInvoices" :key="inv.arInvoiceId">
              <td>{{ inv.invoiceCode }}</td>
              <td>{{ fmtDate(inv.invoiceDate) }}</td>
              <td>{{ fmtMoney(inv.amount) }}</td>
              <td class="action-cell">
                <button
                  class="btn btn--sm btn--danger-ghost"
                  @click="askDelete({ kind: 'ar-invoice', id: inv.arInvoiceId, label: inv.invoiceCode })"
                >刪除</button>
              </td>
            </tr>
            <tr v-if="arInvoices.length === 0">
              <td colspan="4" class="empty-cell">無資料</td>
            </tr>
          </tbody>
        </table>

        <h3 class="sub-section-title" style="margin-top:2rem">收款紀錄</h3>
        <table class="data-table">
          <thead><tr><th>收款代號</th><th>日期</th><th>金額</th><th></th></tr></thead>
          <tbody>
            <tr v-for="inc in arIncomes" :key="inc.incomeId">
              <td>{{ inc.incomeCode }}</td>
              <td>{{ fmtDate(inc.incomeDate) }}</td>
              <td>{{ fmtMoney(inc.amount) }}</td>
              <td class="action-cell">
                <button
                  class="btn btn--sm btn--danger-ghost"
                  @click="askDelete({ kind: 'income', id: inc.incomeId, label: inc.incomeCode })"
                >刪除</button>
              </td>
            </tr>
            <tr v-if="arIncomes.length === 0">
              <td colspan="4" class="empty-cell">無資料</td>
            </tr>
          </tbody>
        </table>
      </template>
    </section>

    <!-- ── Delete confirm modal ─────────────────────────────────────────────── -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget = null">
      <div class="acct-modal">
        <div class="acct-modal__header">
          <h3 class="acct-modal__title">確認刪除</h3>
        </div>
        <div class="acct-modal__body">
          <p>確定要刪除 <strong>{{ deleteTarget.label }}</strong> 嗎？此操作無法復原。</p>
          <p v-if="deleteTarget.kind === 'expenditure'" class="acct-modal__hint">若已有付款記錄，系統將拒絕刪除。</p>
          <p v-if="deleteTarget.kind === 'ar-invoice'" class="acct-modal__hint">若已連結收款記錄，系統將拒絕刪除。</p>
          <p v-if="deleteTarget.kind === 'income'" class="acct-modal__hint">刪除後將取消連結的發票與訂單付款標記。</p>
          <p v-if="deleteError" class="form-error">{{ deleteError }}</p>
        </div>
        <div class="acct-modal__footer">
          <button class="btn btn--ghost" @click="deleteTarget = null">取消</button>
          <button class="btn btn--danger" :disabled="deleting" @click="confirmDelete">
            {{ deleting ? '刪除中…' : '確認刪除' }}
          </button>
        </div>
      </div>
    </div>

    <!-- ── Refund tab ────────────────────────────────────────────────────────── -->
    <section v-if="activeTab === 'refund'" class="tab-panel">
      <div v-if="refundLoading" class="state-msg">載入中…</div>
      <div v-else-if="refundError" class="state-msg state-msg--error">{{ refundError }}</div>
      <template v-else>
        <table class="data-table">
          <thead>
            <tr><th>退貨單號</th><th>會員</th><th>訂單</th><th>日期</th><th>退款狀態</th><th></th></tr>
          </thead>
          <tbody>
            <tr v-for="r in refundItems" :key="r.returnId">
              <td>{{ r.returnCode }}</td>
              <td>{{ r.memberName }}</td>
              <td>{{ r.orderCode }}</td>
              <td>{{ fmtDate(r.returnDate) }}</td>
              <td>{{ r.refundStatus === 1 ? '已退款' : '待退款' }}</td>
              <td class="action-cell">
                <button
                  v-if="r.refundStatus !== 1"
                  class="btn btn--sm btn--accent"
                  :disabled="refundingId === r.returnId"
                  @click="doRefund(r.returnId)"
                >{{ refundingId === r.returnId ? '處理中…' : '退款' }}</button>
              </td>
            </tr>
            <tr v-if="refundItems.length === 0">
              <td colspan="6" class="empty-cell">無待退款的退貨單</td>
            </tr>
          </tbody>
        </table>
      </template>
    </section>
  </main>
</template>

<style scoped>
.acct {}
.acct__title { font-family: var(--tf-font-heading, inherit); color: var(--tf-color-primary-dark); margin-bottom: 1.5rem; }

/* Tabs */
.tabs { display: flex; gap: 0.25rem; border-bottom: 2px solid var(--tf-color-border); margin-bottom: 1.5rem; }
.tabs__btn { padding: 0.45rem 1rem; border: 1px solid transparent; border-bottom: none; background: transparent; cursor: pointer; font-size: 0.875rem; font-family: inherit; color: var(--tf-color-muted); border-radius: 4px 4px 0 0; position: relative; bottom: -2px; transition: background 0.15s; }
.tabs__btn--active { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.tabs__btn:not(.tabs__btn--active):hover { background: #f0f5f1; color: var(--tf-color-primary-dark); }

.tab-panel { animation: fadeIn 0.15s ease; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(4px); } to { opacity: 1; transform: none; } }

/* Tables */
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; border-bottom: 2px solid #d0e2d4; }
.data-table td { padding: 0.6rem 0.75rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; }
.data-table__row { cursor: pointer; transition: background 0.1s; }
.data-table__row:hover { background: #f8faf8; }
.data-table__row--expanded { background: #edf5ef; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2rem; }

/* Sub-section */
.sub-section-title { color: var(--tf-color-primary-dark); font-size: 1rem; margin: 0 0 0.75rem; font-weight: 600; }
.sub-table { width: 100%; border-collapse: collapse; font-size: 0.85rem; margin-top: 0.5rem; }
.sub-table th { background: #f0f5f1; padding: 0.45rem 0.65rem; border-bottom: 1px solid #d0e2d4; text-align: left; }
.sub-table td { padding: 0.4rem 0.65rem; border-bottom: 1px solid #eaf0eb; }
.sub-empty { color: var(--tf-color-muted); font-size: 0.85rem; margin: 0.5rem 0; }

/* Detail / inline panels */
.detail-row > td { padding: 0; }
.detail-panel { background: #f8fdf9; border-left: 3px solid var(--tf-color-primary); padding: 1rem 1.25rem; }
.detail-meta { display: flex; gap: 2rem; font-size: 0.875rem; margin-bottom: 0.75rem; }

.inline-form-row > td { padding: 0; }
.inline-form { background: #fffbf7; border-left: 3px solid var(--tf-color-accent); padding: 1rem 1.25rem; }
.inline-form__title { margin: 0 0 0.75rem; font-size: 0.95rem; color: var(--tf-color-accent); }
.form-row { display: flex; gap: 1.25rem; flex-wrap: wrap; margin-bottom: 0.75rem; }
.form-row label { display: flex; flex-direction: column; gap: 0.3rem; font-size: 0.85rem; color: #444; }
.form-row input { padding: 0.4rem 0.6rem; border: 1px solid #ccc; border-radius: 4px; font-size: 0.875rem; }
.form-error { color: #c0392b; font-size: 0.85rem; margin-bottom: 0.5rem; }
.form-actions { display: flex; gap: 0.5rem; }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--accent { background: var(--tf-color-accent); color: #fff; border-color: var(--tf-color-accent); }
.btn--accent:hover:not(:disabled) { opacity: 0.85; }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: #f0f5f1; }

/* Badge */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 600; }
.badge--unpaid { background: #fde8e8; color: #c0392b; }
.badge--partial { background: #fff3cd; color: #856404; }
.badge--paid { background: #d4edda; color: #155724; }

/* Action column */
.action-cell { white-space: nowrap; text-align: right; }

/* Pagination */
.pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.pagination__info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* State messages */
.state-msg { padding: 2rem; text-align: center; color: var(--tf-color-muted); }
.state-msg--error { color: #c0392b; }

.btn--danger { background: #dc3545; color: #fff; border-color: #dc3545; }
.btn--danger:hover:not(:disabled) { background: #b02a37; }
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }

.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15,23,42,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.acct-modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 420px; }
.acct-modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid #e2e8f0; }
.acct-modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.acct-modal__body { padding: 1.25rem 1.4rem; display: flex; flex-direction: column; gap: 0.5rem; }
.acct-modal__hint { font-size: 0.85rem; color: var(--tf-color-muted); margin: 0; }
.acct-modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid #e2e8f0; }
</style>

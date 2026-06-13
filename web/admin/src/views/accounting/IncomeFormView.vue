<script setup lang="ts">
import { ref, computed, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

const router = useRouter()

// ── 型別 ───────────────────────────────────────────────────────────

interface BillableMember {
  memberId: string
  memberName: string
}

interface BillableInvoice {
  invoiceId: string
  invoiceCode: string
  requestDate: string
  totalPrice: number
}

// ── 會員清單 ───────────────────────────────────────────────────────

const members = ref<BillableMember[]>([])
const membersLoading = ref(false)

onMounted(async () => {
  membersLoading.value = true
  try {
    members.value = await apiFetch<BillableMember[]>('/admin/incomes/billable-members')
  } catch {
    members.value = []
  } finally {
    membersLoading.value = false
  }
})

// ── 請款單清單 ─────────────────────────────────────────────────────

const selectedMemberId = ref('')
const invoices = ref<BillableInvoice[]>([])
const invoicesLoading = ref(false)
const selectedInvoiceIds = ref<Set<string>>(new Set())

async function onMemberChange() {
  selectedInvoiceIds.value = new Set()
  invoices.value = []
  form.amount = 0
  if (!selectedMemberId.value) return
  invoicesLoading.value = true
  try {
    invoices.value = await apiFetch<BillableInvoice[]>(
      `/admin/incomes/billable-invoices?memberId=${selectedMemberId.value}`
    )
  } catch {
    invoices.value = []
  } finally {
    invoicesLoading.value = false
  }
}

function toggleInvoice(invoiceId: string) {
  const next = new Set(selectedInvoiceIds.value)
  if (next.has(invoiceId)) {
    next.delete(invoiceId)
  } else {
    next.add(invoiceId)
  }
  selectedInvoiceIds.value = next
  // 預設金額為已選請款單加總
  form.amount = selectedTotal.value
}

const selectedTotal = computed(() =>
  invoices.value
    .filter(inv => selectedInvoiceIds.value.has(inv.invoiceId))
    .reduce((sum, inv) => sum + inv.totalPrice, 0)
)

// ── 表單 ───────────────────────────────────────────────────────────

const form = reactive({
  amount: 0,
  fee: 0,
  incomeDate: '',
  note: '',
})

// ── 提交 ───────────────────────────────────────────────────────────

const submitting = ref(false)
const submitError = ref('')

async function handleSubmit() {
  submitError.value = ''
  if (!selectedMemberId.value) {
    submitError.value = '請選擇會員'
    return
  }
  if (selectedInvoiceIds.value.size === 0) {
    submitError.value = '請至少選擇一筆請款單'
    return
  }
  if (!form.amount || form.amount <= 0) {
    submitError.value = '收款金額必須大於 0'
    return
  }
  submitting.value = true
  try {
    await apiFetch('/admin/incomes', {
      method: 'POST',
      body: JSON.stringify({
        memberId: selectedMemberId.value,
        amount: Number(form.amount),
        fee: Number(form.fee),
        incomeDate: form.incomeDate || null,
        note: form.note || null,
        invoiceIds: [...selectedInvoiceIds.value],
      }),
    })
    router.push('/admin/incomes')
  } catch (e) {
    submitError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
  } finally {
    submitting.value = false
  }
}

// ── 格式化 ─────────────────────────────────────────────────────────

function fmtDate(d: string | null) { return d ? String(d).slice(0, 10) : '—' }
function fmtMoney(n: number) { return n == null ? '—' : `NT$ ${Number(n).toLocaleString('zh-TW')}` }
</script>

<template>
  <div class="income-form">
    <!-- 頁首 -->
    <div class="income-form__header">
      <div class="income-form__header-left">
        <button class="btn btn--ghost" @click="router.push('/admin/incomes')">&larr; 返回</button>
        <h1 class="income-form__title">新增收款</h1>
      </div>
    </div>

    <!-- 錯誤訊息 -->
    <p v-if="submitError" class="form-msg--error">{{ submitError }}</p>

    <form @submit.prevent="handleSubmit">
      <div class="income-form__layout">
        <div class="income-form__main">

          <!-- 選擇會員 -->
          <div class="form-card">
            <h2 class="form-section__title">選擇會員</h2>
            <div class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="memberId">會員 <span class="req">*</span></label>
                <select
                  id="memberId"
                  v-model="selectedMemberId"
                  class="select"
                  :disabled="membersLoading"
                  @change="onMemberChange"
                  required
                >
                  <option value="">{{ membersLoading ? '載入中…' : '請選擇會員' }}</option>
                  <option
                    v-for="m in members"
                    :key="m.memberId"
                    :value="m.memberId"
                  >{{ m.memberName }}</option>
                </select>
              </div>
            </div>
          </div>

          <!-- 選擇請款單 -->
          <div class="form-card">
            <h2 class="form-section__title">選擇請款單</h2>
            <div v-if="!selectedMemberId" class="income-form__hint">請先選擇會員</div>
            <div v-else-if="invoicesLoading" class="income-form__hint">載入中…</div>
            <div v-else-if="invoices.length === 0" class="income-form__hint">此會員目前無可請款項目</div>
            <template v-else>
              <div class="invoice-table-wrap">
                <table class="data-table">
                  <thead>
                    <tr>
                      <th class="check-th"></th>
                      <th>請款單號</th>
                      <th>請款日</th>
                      <th>金額</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="inv in invoices"
                      :key="inv.invoiceId"
                      class="data-table__row invoice-row"
                      @click="toggleInvoice(inv.invoiceId)"
                    >
                      <td class="check-cell">
                        <input
                          type="checkbox"
                          :checked="selectedInvoiceIds.has(inv.invoiceId)"
                          @change.stop="toggleInvoice(inv.invoiceId)"
                          @click.stop
                          class="invoice-checkbox"
                        />
                      </td>
                      <td class="font-mono">{{ inv.invoiceCode }}</td>
                      <td>{{ fmtDate(inv.requestDate) }}</td>
                      <td>{{ fmtMoney(inv.totalPrice) }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <div class="invoice-subtotal">
                合計：<strong>{{ fmtMoney(selectedTotal) }}</strong>
              </div>
            </template>
          </div>

          <!-- 收款資訊 -->
          <div class="form-card">
            <h2 class="form-section__title">收款資訊</h2>
            <div class="form-row">
              <div class="form-field">
                <label class="label" for="amount">收款金額 <span class="req">*</span></label>
                <input
                  id="amount"
                  v-model.number="form.amount"
                  type="number"
                  min="1"
                  class="input"
                  required
                />
              </div>
              <div class="form-field">
                <label class="label" for="fee">手續費</label>
                <input
                  id="fee"
                  v-model.number="form.fee"
                  type="number"
                  min="0"
                  class="input"
                />
              </div>
              <div class="form-field">
                <label class="label" for="incomeDate">收款日期</label>
                <input
                  id="incomeDate"
                  v-model="form.incomeDate"
                  type="date"
                  class="input"
                />
              </div>
              <div class="form-field form-field--full">
                <label class="label" for="note">備註</label>
                <textarea
                  id="note"
                  v-model="form.note"
                  class="textarea"
                  rows="3"
                  placeholder="選填"
                ></textarea>
              </div>
            </div>
          </div>

        </div><!-- /.income-form__main -->

        <!-- 右欄提交 -->
        <div class="income-form__aside">
          <div class="form-card">
            <h2 class="form-section__title">操作</h2>
            <div class="income-form__submit-row">
              <button type="button" class="btn btn--ghost" @click="router.push('/admin/incomes')">取消</button>
              <button type="submit" class="btn btn--primary" :disabled="submitting">
                {{ submitting ? '建立中…' : '建立收款' }}
              </button>
            </div>
          </div>
        </div>

      </div><!-- /.income-form__layout -->
    </form>
  </div>
</template>

<style scoped>
/* ── 容器 ── */
.income-form { width: 100%; }

/* ── 頁首 ── */
.income-form__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.5rem; }
.income-form__header-left { display: flex; align-items: center; gap: 0.75rem; }
.income-form__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); font-size: 1.25rem; margin: 0; }

/* ── 兩欄版型 ── */
.income-form__layout { display: grid; grid-template-columns: 1fr; gap: 1.25rem; align-items: start; }
@media (min-width: 1024px) {
  .income-form__layout { grid-template-columns: 1fr 360px; }
  .income-form__aside { position: sticky; top: 1.5rem; }
}
@media (min-width: 1280px) {
  .income-form__layout { grid-template-columns: 1fr 400px; }
}

/* ── 卡片 ── */
.form-card { background: #fff; border: 1px solid var(--tf-color-border); border-radius: 6px; padding: 1.25rem; margin-bottom: 1.25rem; }
.income-form__aside .form-card { padding: 1rem; }

/* ── Section 標題 ── */
.form-section__title { font-size: 1rem; font-weight: 600; color: var(--tf-color-primary-dark); margin: 0 0 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid var(--tf-color-border); }
.income-form__aside .form-section__title { font-size: 0.875rem; margin-bottom: 0.75rem; padding-bottom: 0.4rem; }

/* ── 欄位 ── */
.form-row { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 0.75rem 1.25rem; margin-bottom: 0.75rem; }
.form-field { display: flex; flex-direction: column; gap: 0.3rem; }
.form-field--full { grid-column: 1 / -1; }
.label { font-size: 0.8rem; font-weight: 500; color: #374151; }
.req { color: var(--tf-color-accent); margin-left: 0.1rem; }
.input, .select, .textarea { padding: 0.5rem 0.75rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.9rem; font-family: inherit; background: #fff; transition: border-color 0.15s; }
.input:focus, .select:focus, .textarea:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.textarea { resize: vertical; }

/* ── 提示文字 ── */
.income-form__hint { font-size: 0.85rem; color: var(--tf-color-muted); padding: 0.5rem 0; }

/* ── 請款單表格 ── */
.invoice-table-wrap { overflow-x: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.check-th { width: 2.5rem; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.invoice-row { cursor: pointer; }
.check-cell { width: 2.5rem; text-align: center; }
.invoice-checkbox { accent-color: var(--tf-color-primary); width: 16px; height: 16px; cursor: pointer; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }
.invoice-subtotal { text-align: right; font-size: 0.875rem; color: #374151; margin-top: 0.75rem; padding-top: 0.5rem; border-top: 1px solid var(--tf-color-border); }

/* ── 訊息 ── */
.form-msg--error { background: #fbeaea; color: #c0392b; border: 1px solid #f5c6c6; border-radius: 4px; padding: 0.6rem 0.9rem; font-size: 0.875rem; margin-bottom: 1rem; }

/* ── 提交列 ── */
.income-form__submit-row { display: flex; flex-direction: column; gap: 0.5rem; }
.income-form__submit-row .btn { width: 100%; justify-content: center; }

/* ── 按鈕 ── */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; text-decoration: none; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
</style>

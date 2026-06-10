<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

const router = useRouter()

// ── 型別 ──────────────────────────────────────────────────────────

interface BillableMember {
  memberId: string
  memberName: string
}

interface BillableOrder {
  orderId: string
  orderCode: string
  orderDate: string
  total: number
  freight: number
  discount: number
  payable: number
}

// ── 會員選單 ──────────────────────────────────────────────────────

const members = ref<BillableMember[]>([])
const selectedMemberId = ref('')
const membersLoading = ref(false)

async function loadMembers() {
  membersLoading.value = true
  try {
    members.value = await apiFetch<BillableMember[]>('/admin/ar-invoices/billable-members')
  } catch {
    members.value = []
  } finally {
    membersLoading.value = false
  }
}

// ── 可請款訂單 ────────────────────────────────────────────────────

const billableOrders = ref<BillableOrder[]>([])
const ordersLoading = ref(false)
const selectedOrderIds = ref<Set<string>>(new Set())

async function onMemberChange() {
  selectedOrderIds.value = new Set()
  billableOrders.value = []
  if (!selectedMemberId.value) return
  ordersLoading.value = true
  try {
    const params = new URLSearchParams({ memberId: selectedMemberId.value })
    billableOrders.value = await apiFetch<BillableOrder[]>(`/admin/ar-invoices/billable-orders?${params}`)
  } catch {
    billableOrders.value = []
  } finally {
    ordersLoading.value = false
  }
}

function toggleOrder(orderId: string) {
  const next = new Set(selectedOrderIds.value)
  if (next.has(orderId)) {
    next.delete(orderId)
  } else {
    next.add(orderId)
  }
  selectedOrderIds.value = next
}

const selectedTotal = computed(() =>
  billableOrders.value
    .filter(o => selectedOrderIds.value.has(o.orderId))
    .reduce((sum, o) => sum + o.payable, 0)
)

// ── 備註 ──────────────────────────────────────────────────────────

const note = ref('')

// ── 提交 ──────────────────────────────────────────────────────────

const submitting = ref(false)
const submitError = ref('')

async function handleSubmit() {
  submitError.value = ''
  if (!selectedMemberId.value) {
    submitError.value = '請選擇會員'
    return
  }
  if (selectedOrderIds.value.size === 0) {
    submitError.value = '請至少勾選一筆訂單'
    return
  }
  submitting.value = true
  try {
    await apiFetch('/admin/ar-invoices', {
      method: 'POST',
      body: JSON.stringify({
        orderIds: Array.from(selectedOrderIds.value),
        note: note.value || null,
      }),
    })
    router.push('/admin/ar-invoices')
  } catch (e) {
    submitError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
  } finally {
    submitting.value = false
  }
}

// ── 工具函式 ──────────────────────────────────────────────────────

function fmtDate(d: string | null) { return d ? String(d).slice(0, 10) : '—' }
function fmtMoney(n: number) { return n == null ? '—' : `NT$ ${Number(n).toLocaleString('zh-TW')}` }

onMounted(loadMembers)
</script>

<template>
  <div class="arform">
    <!-- 頁首 -->
    <div class="arform__header">
      <div class="arform__header-left">
        <button class="btn btn--ghost" @click="router.push('/admin/ar-invoices')">&larr; 返回</button>
        <h1 class="arform__title">新增請款單</h1>
      </div>
    </div>

    <!-- 錯誤訊息 -->
    <div v-if="submitError" class="form-msg--error">{{ submitError }}</div>

    <form @submit.prevent="handleSubmit">
      <div class="arform__layout">
        <div class="arform__main">

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
                  required
                  :disabled="membersLoading"
                  @change="onMemberChange"
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

          <!-- 選擇訂單 -->
          <div class="form-card">
            <h2 class="form-section__title">選擇訂單</h2>

            <p v-if="!selectedMemberId" class="arform__hint">請先選擇會員，以顯示可請款訂單。</p>
            <p v-else-if="ordersLoading" class="arform__hint">載入中…</p>
            <p v-else-if="billableOrders.length === 0" class="arform__hint">此會員目前無可請款訂單。</p>

            <template v-else>
              <div class="arform__table-wrap">
                <table class="data-table">
                  <thead>
                    <tr>
                      <th style="width:2.5rem"></th>
                      <th>訂單編號</th>
                      <th>日期</th>
                      <th style="text-align:right">應收</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="o in billableOrders"
                      :key="o.orderId"
                      class="data-table__row arform__order-row"
                      :class="{ 'arform__order-row--selected': selectedOrderIds.has(o.orderId) }"
                      @click="toggleOrder(o.orderId)"
                    >
                      <td style="text-align:center">
                        <input
                          type="checkbox"
                          :checked="selectedOrderIds.has(o.orderId)"
                          class="arform__checkbox"
                          @click.stop
                          @change="toggleOrder(o.orderId)"
                        />
                      </td>
                      <td class="font-mono">{{ o.orderCode }}</td>
                      <td>{{ fmtDate(o.orderDate) }}</td>
                      <td style="text-align:right">{{ fmtMoney(o.payable) }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <div class="arform__order-total">
                合計：<strong>{{ fmtMoney(selectedTotal) }}</strong>
                <span class="arform__order-count">（已選 {{ selectedOrderIds.size }} 筆）</span>
              </div>
            </template>
          </div>

          <!-- 備註 -->
          <div class="form-card">
            <h2 class="form-section__title">備註</h2>
            <div class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="note">備註</label>
                <textarea
                  id="note"
                  v-model="note"
                  class="textarea"
                  rows="3"
                  placeholder="備註（選填）"
                ></textarea>
              </div>
            </div>
          </div>

        </div><!-- /.arform__main -->

        <!-- 右欄：送出 -->
        <div class="arform__aside">
          <div class="form-card">
            <h2 class="form-section__title">建立請款單</h2>
            <div class="arform__submit-row">
              <button type="button" class="btn btn--ghost" @click="router.push('/admin/ar-invoices')">取消</button>
              <button
                type="submit"
                class="btn btn--primary"
                :disabled="submitting || selectedOrderIds.size === 0"
              >
                {{ submitting ? '建立中…' : '建立請款單' }}
              </button>
            </div>
          </div>
        </div>

      </div><!-- /.arform__layout -->
    </form>
  </div>
</template>

<style scoped>
/* ── 根容器 ── */
.arform { width: 100%; }

/* ── 頁首 ── */
.arform__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.5rem;
}
.arform__header-left {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}
.arform__title {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  font-size: 1.25rem;
  margin: 0;
}

/* ── 兩欄版型 ── */
.arform__layout {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.25rem;
  align-items: start;
}
@media (min-width: 1024px) {
  .arform__layout { grid-template-columns: 1fr 360px; }
  .arform__aside { position: sticky; top: 1.5rem; }
}
@media (min-width: 1280px) {
  .arform__layout { grid-template-columns: 1fr 400px; }
}

/* ── 右欄卡片稍緊湊 ── */
.arform__aside .form-card { padding: 1rem; }
.arform__aside .form-section__title { font-size: 0.875rem; margin-bottom: 0.75rem; padding-bottom: 0.4rem; }

/* ── 卡片 ── */
.form-card {
  background: #fff;
  border: 1px solid var(--tf-color-border);
  border-radius: 6px;
  padding: 1.25rem;
  margin-bottom: 1.25rem;
}

/* ── Section 標題 ── */
.form-section__title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--tf-color-primary-dark);
  margin: 0 0 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--tf-color-border);
}

/* ── 欄位佈局 ── */
.form-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 0.75rem 1.25rem;
  margin-bottom: 0.75rem;
}
.form-field { display: flex; flex-direction: column; gap: 0.3rem; }
.form-field--full { grid-column: 1 / -1; }

/* ── 標籤 ── */
.label { font-size: 0.8rem; font-weight: 500; color: #374151; }
.req { color: var(--tf-color-accent); margin-left: 0.1rem; }

/* ── 輸入控制項 ── */
.select,
.textarea {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.9rem;
  font-family: inherit;
  background: #fff;
  transition: border-color 0.15s;
}
.select:focus,
.textarea:focus {
  outline: none;
  border-color: var(--tf-color-primary);
  box-shadow: 0 0 0 2px rgba(38,183,188,0.15);
}
.textarea { resize: vertical; }

/* ── 訂單表格 ── */
.arform__table-wrap { overflow-x: auto; margin-bottom: 0.75rem; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }

.arform__order-row { cursor: pointer; }
.arform__order-row:hover td { background: #f0fafa; }
.arform__order-row--selected td { background: rgba(38, 183, 188, 0.07); }

.arform__checkbox {
  accent-color: var(--tf-color-primary);
  width: 15px;
  height: 15px;
  cursor: pointer;
}

.font-mono { font-family: 'IBM Plex Mono', monospace; }

/* ── 合計列 ── */
.arform__order-total {
  font-size: 0.9rem;
  color: #1e293b;
  text-align: right;
  padding: 0.5rem 0.25rem 0;
}
.arform__order-count {
  font-size: 0.8rem;
  color: var(--tf-color-muted);
  margin-left: 0.5rem;
}

/* ── 提示 ── */
.arform__hint {
  font-size: 0.85rem;
  color: var(--tf-color-muted);
  margin: 0.25rem 0;
}

/* ── 送出列 ── */
.arform__submit-row {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
.arform__submit-row .btn {
  width: 100%;
  justify-content: center;
}

/* ── 錯誤訊息 ── */
.form-msg--error {
  background: #fbeaea;
  color: #c0392b;
  border: 1px solid #f5c6c6;
  border-radius: 4px;
  padding: 0.6rem 0.9rem;
  font-size: 0.875rem;
  margin-bottom: 1rem;
}

/* ── 按鈕 ── */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; text-decoration: none; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
</style>

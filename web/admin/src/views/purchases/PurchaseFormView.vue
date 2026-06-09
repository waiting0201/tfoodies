<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch } from '../../lib/apiClient'

interface Supplier {
  supplierId: string
  name: string
}

interface LineItem {
  productId: string
  unitPrice: number
  qty: number
}

const router = useRouter()

const suppliers = ref<Supplier[]>([])
const saving = ref(false)
const error = ref('')
const successMsg = ref('')

const header = reactive({
  supplierId: '',
  exchangeId: '',
  purchaseDate: new Date().toISOString().slice(0, 10),
  payment: '',
  note: '',
})

const lineItems = ref<LineItem[]>([
  { productId: '', unitPrice: 0, qty: 1 },
])

function addRow() {
  lineItems.value.push({ productId: '', unitPrice: 0, qty: 1 })
}

function removeRow(index: number) {
  if (lineItems.value.length === 1) return
  lineItems.value.splice(index, 1)
}

const grandTotal = computed(() =>
  lineItems.value.reduce((sum, item) => sum + item.unitPrice * item.qty, 0)
)

async function loadSuppliers() {
  try {
    suppliers.value = await apiFetch<Supplier[]>('/admin/suppliers')
  } catch {
    // non-fatal; user can still type manually
  }
}

async function submit() {
  if (!header.supplierId) {
    error.value = '請選擇供應商'
    return
  }
  const validItems = lineItems.value.filter(i => i.productId.trim() && i.qty > 0)
  if (validItems.length === 0) {
    error.value = '至少需要一筆採購明細'
    return
  }

  saving.value = true
  error.value = ''
  successMsg.value = ''

  try {
    await apiFetch('/admin/purchases', {
      method: 'POST',
      body: JSON.stringify({
        supplierId: header.supplierId,
        exchangeId: header.exchangeId || undefined,
        purchaseDate: header.purchaseDate,
        payment: header.payment || undefined,
        note: header.note || undefined,
        details: validItems.map(i => ({
          productId: i.productId,
          unitPrice: i.unitPrice,
          qty: i.qty,
        })),
      }),
    })
    successMsg.value = '採購單已建立'
    setTimeout(() => router.push('/admin/purchases'), 800)
  } catch (e: any) {
    error.value = e.message ?? '建立失敗'
  } finally {
    saving.value = false
  }
}

onMounted(loadSuppliers)
</script>

<template>
  <main class="purchase-form">
    <div class="purchase-form__header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/purchases')">
        &larr; 返回採購管理
      </button>
      <h1>新增採購單</h1>
    </div>

    <form class="form-card" @submit.prevent="submit">
      <p v-if="error" class="form-msg form-msg--error">{{ error }}</p>
      <p v-if="successMsg" class="form-msg form-msg--success">{{ successMsg }}</p>

      <!-- Header section -->
      <div class="form-section">
        <h2 class="form-section__title">採購資訊</h2>

        <div class="form-row">
          <div class="form-field">
            <label class="label label--required">供應商</label>
            <select v-model="header.supplierId" class="select">
              <option value="">請選擇供應商</option>
              <option v-for="s in suppliers" :key="s.supplierId" :value="s.supplierId">
                {{ s.name }}
              </option>
            </select>
          </div>
          <div class="form-field">
            <label class="label">匯率/換算 ID</label>
            <input
              v-model="header.exchangeId"
              class="input"
              type="text"
              placeholder="選填"
            />
          </div>
          <div class="form-field">
            <label class="label label--required">採購日期</label>
            <input v-model="header.purchaseDate" class="input" type="date" />
          </div>
        </div>

        <div class="form-row">
          <div class="form-field">
            <label class="label">付款方式</label>
            <select v-model="header.payment" class="select">
              <option value="">請選擇</option>
              <option value="現金">現金</option>
              <option value="匯款">匯款</option>
              <option value="月結">月結</option>
              <option value="其他">其他</option>
            </select>
          </div>
          <div class="form-field form-field--grow">
            <label class="label">備註</label>
            <input v-model="header.note" class="input" type="text" placeholder="選填備註" />
          </div>
        </div>
      </div>

      <!-- Line items section -->
      <div class="form-section">
        <div class="items-header">
          <h2 class="form-section__title">採購明細</h2>
          <button type="button" class="btn btn--sm btn--ghost" @click="addRow">
            + 新增一行
          </button>
        </div>

        <div class="items-table-wrapper">
          <table class="items-table">
            <thead>
              <tr>
                <th class="col-product">商品 ID</th>
                <th class="col-price">單價 (NT$)</th>
                <th class="col-qty">數量</th>
                <th class="col-subtotal">小計</th>
                <th class="col-action"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, idx) in lineItems" :key="idx">
                <td>
                  <input
                    v-model="item.productId"
                    class="input input--table"
                    type="text"
                    placeholder="productId (Guid)"
                  />
                </td>
                <td>
                  <input
                    v-model.number="item.unitPrice"
                    class="input input--table input--number"
                    type="number"
                    min="0"
                    step="1"
                  />
                </td>
                <td>
                  <input
                    v-model.number="item.qty"
                    class="input input--table input--number"
                    type="number"
                    min="1"
                    step="1"
                  />
                </td>
                <td class="subtotal-cell">
                  NT$ {{ (item.unitPrice * item.qty).toLocaleString() }}
                </td>
                <td class="action-cell">
                  <button
                    type="button"
                    class="btn-remove"
                    :disabled="lineItems.length === 1"
                    title="刪除此行"
                    @click="removeRow(idx)"
                  >
                    &times;
                  </button>
                </td>
              </tr>
            </tbody>
            <tfoot>
              <tr>
                <td colspan="3" class="total-label">合計</td>
                <td class="total-amount">NT$ {{ grandTotal.toLocaleString() }}</td>
                <td></td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

      <div class="form-actions">
        <button type="button" class="btn btn--ghost" @click="router.push('/admin/purchases')">
          取消
        </button>
        <button type="submit" class="btn btn--primary" :disabled="saving">
          {{ saving ? '建立中...' : '建立採購單' }}
        </button>
      </div>
    </form>
  </main>
</template>

<style scoped>
.purchase-form {
}

.purchase-form__header {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.purchase-form__header h1 {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  margin: 0;
}

.form-card {
  background: #fff;
  border: 1px solid #e5e5e5;
  border-radius: 8px;
  padding: 1.75rem;
}

.form-section {
  margin-bottom: 1.75rem;
  padding-bottom: 1.75rem;
  border-bottom: 1px solid #f0f0f0;
}

.form-section:last-of-type {
  border-bottom: none;
}

.form-section__title {
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--tf-color-primary);
  margin: 0 0 1rem 0;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.items-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.items-header .form-section__title {
  margin: 0;
}

.form-row {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  margin-bottom: 0.75rem;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  min-width: 150px;
}

.form-field--grow {
  flex: 1;
}

.label {
  font-size: 0.82rem;
  font-weight: 600;
  color: #444;
}

.label--required::after {
  content: ' *';
  color: #e74c3c;
}

.input,
.select {
  padding: 0.45rem 0.65rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 0.875rem;
  background: #fff;
  font-family: inherit;
}

.input:focus,
.select:focus {
  outline: none;
  border-color: var(--tf-color-primary);
  box-shadow: 0 0 0 2px rgba(62, 107, 68, 0.15);
}

/* Line items table */
.items-table-wrapper {
  overflow-x: auto;
}

.items-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.items-table th {
  background: var(--tf-color-primary);
  color: #fff;
  padding: 0.5rem 0.65rem;
  text-align: left;
  font-weight: 600;
  font-size: 0.875rem;
  border-bottom: 2px solid #cce0cd;
}

.items-table td {
  padding: 0.4rem 0.5rem;
  border-bottom: 1px solid var(--tf-color-border);
  vertical-align: middle;
}

.col-product { width: 40%; }
.col-price   { width: 20%; }
.col-qty     { width: 12%; }
.col-subtotal { width: 18%; }
.col-action  { width: 10%; }

.input--table {
  width: 100%;
  box-sizing: border-box;
  padding: 0.35rem 0.5rem;
}

.input--number {
  text-align: right;
}

.subtotal-cell {
  text-align: right;
  font-weight: 500;
  color: #333;
}

.action-cell {
  text-align: center;
}

.btn-remove {
  width: 26px;
  height: 26px;
  border: none;
  border-radius: 50%;
  background: #fbeaea;
  color: #c0392b;
  font-size: 1rem;
  line-height: 1;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: background 0.15s;
}

.btn-remove:hover:not(:disabled) {
  background: #f5c6c6;
}

.btn-remove:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.items-table tfoot td {
  border-top: 2px solid #cce0cd;
  padding-top: 0.6rem;
}

.total-label {
  text-align: right;
  font-weight: 700;
  color: #555;
}

.total-amount {
  text-align: right;
  font-weight: 700;
  font-size: 1rem;
  color: var(--tf-color-primary-dark);
}

/* Form messages */
.form-msg {
  padding: 0.6rem 0.9rem;
  border-radius: 4px;
  font-size: 0.875rem;
  margin-bottom: 1rem;
}

.form-msg--error {
  background: #fbeaea;
  color: #c0392b;
  border: 1px solid #f5c6c6;
}

.form-msg--success {
  background: #e6f4ea;
  color: #1e7e34;
  border: 1px solid #b8dfc0;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  margin-top: 1.5rem;
  padding-top: 1.25rem;
  border-top: 1px solid #f0f0f0;
}

/* Buttons */
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.45rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.875rem;
  font-weight: 500;
  text-decoration: none;
  transition: opacity 0.15s;
}

.btn:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.btn--primary {
  background: var(--tf-color-primary);
  color: #fff;
}

.btn--primary:hover:not(:disabled) {
  background: var(--tf-color-primary-dark);
}

.btn--ghost {
  background: transparent;
  color: var(--tf-color-primary);
  border: 1px solid var(--tf-color-primary);
}

.btn--ghost:hover:not(:disabled) {
  background: #f0f5f1;
}

.btn--sm {
  padding: 0.25rem 0.6rem;
  font-size: 0.8rem;
}
</style>

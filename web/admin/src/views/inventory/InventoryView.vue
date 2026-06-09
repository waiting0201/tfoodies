<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

interface Warehouse {
  warehouseid: string
  warehousetype: number
  title: string
}
interface StockItem {
  stockId: string
  productId: string
  productTitle?: string
  warehouseId: string
  warehouseName?: string
  qty: number
  expireDate?: string
}
interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

const WAREHOUSE_TYPES: Record<number, string> = { 1: '一般倉', 2: '冷藏倉', 3: '冷凍倉' }

const warehouses = ref<Warehouse[]>([])
const stockItems = ref<StockItem[]>([])
const totalCount = ref(0)
const loadingWarehouses = ref(false)
const loadingStocks = ref(false)
const warehouseError = ref('')
const stockError = ref('')
const addError = ref('')
const addSuccess = ref('')
const adding = ref(false)

const pagination = reactive({ page: 1, pageSize: 30 })

const addForm = reactive({
  purchaseDetailId: '',
  productId: '',
  warehouseId: '',
  qty: 1,
  expireDate: '',
})

// ── Transfer ──────────────────────────────────────────────────────────────────
const transferModal = ref(false)
const transferTarget = ref<StockItem | null>(null)
const transferForm = reactive({ toWarehouseId: '', qty: 1 })
const transferSaving = ref(false)
const transferError = ref('')

function openTransfer(s: StockItem) {
  transferTarget.value = s
  transferForm.toWarehouseId = ''
  transferForm.qty = s.qty > 0 ? s.qty : 1
  transferError.value = ''
  transferModal.value = true
}

function closeTransfer() { transferModal.value = false }

async function submitTransfer() {
  if (!transferForm.toWarehouseId) { transferError.value = '請選擇目的倉庫'; return }
  if (transferForm.qty <= 0) { transferError.value = '數量必須大於 0'; return }
  transferSaving.value = true
  transferError.value = ''
  try {
    await apiFetch('/admin/warehouse-transfer', {
      method: 'POST',
      body: JSON.stringify({
        productId: transferTarget.value!.productId,
        fromWarehouseId: transferTarget.value!.warehouseId,
        toWarehouseId: transferForm.toWarehouseId,
        qty: transferForm.qty,
      }),
    })
    closeTransfer()
    await loadStocks()
  } catch (e) {
    transferError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '調撥失敗'
  } finally {
    transferSaving.value = false
  }
}

async function loadWarehouses() {
  loadingWarehouses.value = true
  warehouseError.value = ''
  try {
    warehouses.value = await apiFetch<Warehouse[]>('/admin/warehouses')
  } catch (e: any) {
    warehouseError.value = e.message ?? '載入倉庫失敗'
  } finally {
    loadingWarehouses.value = false
  }
}

async function loadStocks() {
  loadingStocks.value = true
  stockError.value = ''
  try {
    const params = new URLSearchParams({
      page: String(pagination.page),
      pageSize: String(pagination.pageSize),
    })
    const res = await apiFetch<PagedResult<StockItem>>(`/admin/inventory?${params}`)
    stockItems.value = res.items
    totalCount.value = res.totalCount
  } catch (e: any) {
    stockError.value = e.message ?? '載入庫存失敗'
  } finally {
    loadingStocks.value = false
  }
}

async function submitAddStock() {
  if (!addForm.purchaseDetailId.trim() || !addForm.productId.trim() || !addForm.warehouseId) {
    addError.value = '採購明細 ID、商品 ID 及倉庫為必填'
    return
  }
  adding.value = true
  addError.value = ''
  addSuccess.value = ''
  try {
    await apiFetch('/admin/stocks', {
      method: 'POST',
      body: JSON.stringify({
        purchaseDetailId: addForm.purchaseDetailId,
        productId: addForm.productId,
        warehouseId: addForm.warehouseId,
        qty: addForm.qty,
        expireDate: addForm.expireDate || undefined,
      }),
    })
    addSuccess.value = '庫存已新增'
    addForm.purchaseDetailId = ''
    addForm.productId = ''
    addForm.warehouseId = ''
    addForm.qty = 1
    addForm.expireDate = ''
    await loadStocks()
  } catch (e: any) {
    addError.value = e.message ?? '新增失敗'
  } finally {
    adding.value = false
  }
}

function goToPage(p: number) {
  pagination.page = p
  loadStocks()
}

const totalPages = () => Math.max(1, Math.ceil(totalCount.value / pagination.pageSize))

onMounted(() => {
  loadWarehouses()
  loadStocks()
})
</script>

<template>
  <main class="inventory">
    <h1 class="inventory__title">庫存管理</h1>

    <!-- Section 1: Warehouses -->
    <section class="inv-section">
      <h2 class="inv-section__heading">倉庫一覽</h2>
      <p v-if="warehouseError" class="msg msg--error">{{ warehouseError }}</p>
      <p v-if="loadingWarehouses" class="msg msg--muted">載入中...</p>
      <div v-else class="warehouse-grid">
        <div v-if="warehouses.length === 0" class="msg msg--muted">尚無倉庫資料</div>
        <div v-for="w in warehouses" :key="w.warehouseid" class="warehouse-card">
          <div class="warehouse-card__name">{{ w.title }}</div>
          <div class="warehouse-card__meta">{{ WAREHOUSE_TYPES[w.warehousetype] ?? `類型${w.warehousetype}` }}</div>
          <div class="warehouse-card__id">{{ w.warehouseid.slice(0, 8) }}…</div>
        </div>
      </div>
    </section>

    <!-- Section 2: Stock levels -->
    <section class="inv-section">
      <h2 class="inv-section__heading">庫存明細</h2>
      <p v-if="stockError" class="msg msg--error">{{ stockError }}</p>
      <p v-if="loadingStocks" class="msg msg--muted">載入中...</p>
      <table v-else class="table">
        <thead>
          <tr>
            <th>商品</th>
            <th>倉庫</th>
            <th>數量</th>
            <th>效期</th>
            <th class="th--action"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="stockItems.length === 0">
            <td colspan="5" class="table__empty">無庫存資料</td>
          </tr>
          <tr v-for="s in stockItems" :key="s.stockId">
            <td>
              <span class="stock-product">{{ s.productTitle ?? s.productId }}</span>
              <span class="stock-id">{{ s.productId.slice(0, 8) }}…</span>
            </td>
            <td>{{ s.warehouseName ?? s.warehouseId }}</td>
            <td :class="['td--number', s.qty <= 0 ? 'qty--zero' : '']">{{ s.qty }}</td>
            <td>{{ s.expireDate ? new Date(s.expireDate).toLocaleDateString('zh-TW') : '—' }}</td>
            <td class="td--action">
              <button
                class="btn btn--sm btn--ghost"
                :disabled="s.qty <= 0"
                @click="openTransfer(s)"
              >調撥</button>
            </td>
          </tr>
        </tbody>
      </table>

      <div class="inv-pagination">
        <button
          class="btn btn--sm btn--ghost"
          :disabled="pagination.page <= 1"
          @click="goToPage(pagination.page - 1)"
        >
          &laquo; 上一頁
        </button>
        <span class="inv-pagination__info">
          第 {{ pagination.page }} / {{ totalPages() }} 頁（共 {{ totalCount }} 筆）
        </span>
        <button
          class="btn btn--sm btn--ghost"
          :disabled="pagination.page >= totalPages()"
          @click="goToPage(pagination.page + 1)"
        >
          下一頁 &raquo;
        </button>
      </div>
    </section>

    <!-- Section 3: Add stock form -->
    <section class="inv-section">
      <h2 class="inv-section__heading">新增庫存</h2>
      <p v-if="addError" class="msg msg--error">{{ addError }}</p>
      <p v-if="addSuccess" class="msg msg--success">{{ addSuccess }}</p>

      <form class="add-stock-form" @submit.prevent="submitAddStock">
        <div class="form-row">
          <div class="form-field">
            <label class="label label--required">採購明細 ID</label>
            <input
              v-model="addForm.purchaseDetailId"
              class="input"
              type="text"
              placeholder="Guid（採購明細 purchaseDetailId）"
            />
          </div>
          <div class="form-field">
            <label class="label label--required">商品 ID</label>
            <input
              v-model="addForm.productId"
              class="input"
              type="text"
              placeholder="productId (Guid)"
            />
          </div>
        </div>

        <div class="form-row">
          <div class="form-field">
            <label class="label label--required">倉庫</label>
            <select v-model="addForm.warehouseId" class="select">
              <option value="">請選擇倉庫</option>
              <option v-for="w in warehouses" :key="w.warehouseid" :value="w.warehouseid">
                {{ w.title }}
              </option>
            </select>
          </div>
          <div class="form-field">
            <label class="label label--required">數量</label>
            <input v-model.number="addForm.qty" class="input" type="number" min="1" step="1" />
          </div>
          <div class="form-field">
            <label class="label">效期</label>
            <input v-model="addForm.expireDate" class="input" type="date" />
          </div>
        </div>

        <div class="form-actions">
          <button type="submit" class="btn btn--primary" :disabled="adding">
            {{ adding ? '新增中...' : '新增庫存' }}
          </button>
        </div>
      </form>
    </section>

    <!-- Transfer modal -->
    <div v-if="transferModal" class="modal-overlay" @click.self="closeTransfer">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">庫存調撥</h3>
        </div>
        <div class="modal__body" v-if="transferTarget">
          <p class="modal__desc">
            商品：<strong>{{ transferTarget.productTitle ?? transferTarget.productId }}</strong><br>
            來源：{{ transferTarget.warehouseName ?? transferTarget.warehouseId }}（可調撥 {{ transferTarget.qty }} 件）
          </p>
          <div class="form-field">
            <label class="label label--required">目的倉庫</label>
            <select v-model="transferForm.toWarehouseId" class="select">
              <option value="">請選擇</option>
              <option
                v-for="w in warehouses.filter(w => w.warehouseid !== transferTarget!.warehouseId)"
                :key="w.warehouseid"
                :value="w.warehouseid"
              >{{ w.title }}</option>
            </select>
          </div>
          <div class="form-field" style="margin-top:0.75rem">
            <label class="label label--required">調撥數量</label>
            <input
              v-model.number="transferForm.qty"
              class="input"
              type="number"
              min="1"
              :max="transferTarget.qty"
              step="1"
            />
          </div>
          <p v-if="transferError" class="msg msg--error" style="margin-top:0.5rem">{{ transferError }}</p>
        </div>
        <div class="modal__footer">
          <button class="btn btn--ghost" @click="closeTransfer">取消</button>
          <button class="btn btn--primary" :disabled="transferSaving" @click="submitTransfer">
            {{ transferSaving ? '調撥中…' : '確認調撥' }}
          </button>
        </div>
      </div>
    </div>
  </main>
</template>

<style scoped>
.inventory {
}

.inventory__title {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  margin-bottom: 2rem;
}

.inv-section {
  margin-bottom: 2.5rem;
  padding-bottom: 2rem;
  border-bottom: 2px solid #f0f0f0;
}

.inv-section:last-child {
  border-bottom: none;
}

.inv-section__heading {
  font-size: 1.05rem;
  font-weight: 700;
  color: var(--tf-color-primary);
  margin: 0 0 1.25rem 0;
  padding-bottom: 0.4rem;
  border-bottom: 2px solid var(--tf-color-primary);
  display: inline-block;
}

/* Warehouse grid */
.warehouse-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1rem;
}

.warehouse-card {
  background: #f8faf8;
  border: 1px solid #d4e8d5;
  border-radius: 8px;
  padding: 1rem;
}

.warehouse-card__name {
  font-weight: 600;
  color: var(--tf-color-primary-dark);
  margin-bottom: 0.3rem;
}

.warehouse-card__meta {
  font-size: 0.82rem;
  color: #555;
}

.warehouse-card__id {
  font-family: monospace;
  font-size: 0.75rem;
  color: var(--tf-color-muted);
  margin-top: 0.5rem;
}

/* Table */
.table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.table th {
  background: var(--tf-color-primary);
  color: #fff;
  padding: 0.6rem 0.75rem;
  text-align: left;
  font-weight: 600;
  font-size: 0.875rem;
}

.th--action { width: 80px; }

.table td {
  padding: 0.55rem 0.75rem;
  border-bottom: 1px solid var(--tf-color-border);
  vertical-align: middle;
}

.table tbody tr:hover td { background: #f8faf8; }

.table__empty {
  text-align: center;
  color: var(--tf-color-muted);
  padding: 2rem !important;
}

.td--number {
  text-align: right;
  font-weight: 600;
}

.td--action {
  text-align: right;
  white-space: nowrap;
}

.qty--zero {
  color: #c0392b;
}

.stock-product {
  display: block;
  font-weight: 500;
}

.stock-id {
  display: block;
  font-family: monospace;
  font-size: 0.75rem;
  color: var(--tf-color-muted);
}

/* Pagination */
.inv-pagination {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-top: 1rem;
  justify-content: flex-end;
}

.inv-pagination__info {
  font-size: 0.875rem;
  color: var(--tf-color-muted);
}

/* Add stock form */
.add-stock-form {
  background: #fff;
  border: 1px solid #e5e5e5;
  border-radius: 8px;
  padding: 1.5rem;
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
  min-width: 160px;
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
  box-shadow: 0 0 0 2px rgba(38, 183, 188, 0.15);
}

.form-actions {
  margin-top: 1rem;
  display: flex;
  justify-content: flex-end;
}

/* Messages */
.msg {
  padding: 0.5rem 0.75rem;
  border-radius: 4px;
  font-size: 0.875rem;
  margin-bottom: 0.75rem;
}

.msg--error {
  background: #fbeaea;
  color: #c0392b;
}

.msg--success {
  background: #e6f4ea;
  color: #1e7e34;
}

.msg--muted {
  color: var(--tf-color-muted);
}

/* Buttons */
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.45rem 1rem;
  border: 1px solid transparent;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.875rem;
  font-weight: 500;
  text-decoration: none;
  transition: opacity 0.15s, background 0.15s;
}

.btn:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.btn--primary {
  background: var(--tf-color-primary);
  color: #fff;
  border-color: var(--tf-color-primary);
}

.btn--primary:hover:not(:disabled) {
  background: var(--tf-color-primary-dark);
  border-color: var(--tf-color-primary-dark);
}

.btn--ghost {
  background: transparent;
  color: var(--tf-color-primary);
  border-color: var(--tf-color-primary);
}

.btn--ghost:hover:not(:disabled) {
  background: rgba(38, 183, 188, 0.06);
}

.btn--sm {
  padding: 0.25rem 0.6rem;
  font-size: 0.8rem;
}

/* Transfer modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  z-index: 60;
  background: rgba(15, 23, 42, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
}

.modal {
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.2);
  width: 100%;
  max-width: 440px;
}

.modal__header {
  padding: 1.1rem 1.4rem;
  border-bottom: 1px solid var(--tf-color-border);
}

.modal__title {
  font-size: 1rem;
  font-weight: 700;
  color: #1e293b;
  margin: 0;
}

.modal__body {
  padding: 1.25rem 1.4rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.modal__desc {
  font-size: 0.9rem;
  color: #334155;
  line-height: 1.6;
  margin: 0 0 0.75rem;
}

.modal__footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  padding: 1rem 1.4rem;
  border-top: 1px solid var(--tf-color-border);
}
</style>

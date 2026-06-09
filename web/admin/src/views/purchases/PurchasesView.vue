<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch } from '../../lib/apiClient'

interface Supplier {
  supplierId: string
  name: string
  contact?: string
  phone?: string
}
interface Purchase {
  purchaseId: string
  supplierName?: string
  supplierId: string
  purchaseDate: string
  status: string | number
}
interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}
interface PurchaseDetail {
  purchaseId: string
  supplierName?: string
  purchaseDate: string
  status: string | number
  payment?: string
  note?: string
  details?: Array<{
    purchaseDetailId: string
    productId: string
    productTitle?: string
    unitPrice: number
    qty: number
  }>
}

const router = useRouter()
const tabIndex = ref<0 | 1>(0)

// Suppliers
const suppliers = ref<Supplier[]>([])
const loadingSuppliers = ref(false)
const supplierError = ref('')
const newSupplier = reactive({ name: '', contact: '', phone: '' })
const savingSupplier = ref(false)
const supplierSaveError = ref('')
const supplierSaveSuccess = ref('')

const editSupplierId = ref<string | null>(null)
const editSupplier = reactive({ name: '', contact: '', phone: '' })
const editingSaving = ref(false)
const editingError = ref('')

// Purchases
const purchases = ref<Purchase[]>([])
const totalCount = ref(0)
const loadingPurchases = ref(false)
const purchaseError = ref('')
const pagination = reactive({ page: 1, pageSize: 20 })
const detailMap = reactive<Record<string, PurchaseDetail | null>>({})
const loadingDetail = reactive<Record<string, boolean>>({})
const expenditureLoading = reactive<Record<string, boolean>>({})

async function loadSuppliers() {
  loadingSuppliers.value = true
  supplierError.value = ''
  try {
    suppliers.value = await apiFetch<Supplier[]>('/admin/suppliers')
  } catch (e: any) {
    supplierError.value = e.message ?? '載入失敗'
  } finally {
    loadingSuppliers.value = false
  }
}

async function createSupplier() {
  if (!newSupplier.name.trim()) {
    supplierSaveError.value = '供應商名稱為必填'
    return
  }
  savingSupplier.value = true
  supplierSaveError.value = ''
  supplierSaveSuccess.value = ''
  try {
    await apiFetch('/admin/suppliers', {
      method: 'POST',
      body: JSON.stringify({ ...newSupplier }),
    })
    supplierSaveSuccess.value = '供應商已建立'
    newSupplier.name = ''
    newSupplier.contact = ''
    newSupplier.phone = ''
    await loadSuppliers()
  } catch (e: any) {
    supplierSaveError.value = e.message ?? '建立失敗'
  } finally {
    savingSupplier.value = false
  }
}

function openEditSupplier(s: Supplier) {
  editSupplierId.value = s.supplierId
  editSupplier.name = s.name
  editSupplier.contact = s.contact ?? ''
  editSupplier.phone = s.phone ?? ''
  editingError.value = ''
}

async function saveEditSupplier() {
  if (!editSupplierId.value) return
  editingSaving.value = true
  editingError.value = ''
  try {
    await apiFetch(`/admin/suppliers/${editSupplierId.value}`, {
      method: 'PUT',
      body: JSON.stringify({ title: editSupplier.name, contactor: editSupplier.contact, phone: editSupplier.phone, address: null }),
    })
    editSupplierId.value = null
    await loadSuppliers()
  } catch (e: any) {
    editingError.value = e.message ?? '儲存失敗'
  } finally {
    editingSaving.value = false
  }
}

async function deleteSupplier(s: Supplier) {
  if (!confirm(`確認刪除供應商「${s.name}」？此操作無法復原。`)) return
  try {
    await apiFetch(`/admin/suppliers/${s.supplierId}`, { method: 'DELETE' })
    await loadSuppliers()
  } catch (e: any) {
    alert(e.message ?? '刪除失敗')
  }
}

async function loadPurchases() {
  loadingPurchases.value = true
  purchaseError.value = ''
  try {
    const params = new URLSearchParams({
      page: String(pagination.page),
      pageSize: String(pagination.pageSize),
    })
    const res = await apiFetch<PagedResult<Purchase>>(`/admin/purchases?${params}`)
    purchases.value = res.items
    totalCount.value = res.totalCount
  } catch (e: any) {
    purchaseError.value = e.message ?? '載入失敗'
  } finally {
    loadingPurchases.value = false
  }
}

async function toggleDetail(purchaseId: string) {
  if (purchaseId in detailMap) {
    // toggle visibility: set null to collapse
    if (detailMap[purchaseId] !== null) {
      detailMap[purchaseId] = null
      return
    }
  }
  loadingDetail[purchaseId] = true
  try {
    const d = await apiFetch<PurchaseDetail>(`/admin/purchases/${purchaseId}`)
    detailMap[purchaseId] = d
  } catch (e: any) {
    alert(e.message ?? '載入詳情失敗')
  } finally {
    loadingDetail[purchaseId] = false
  }
}

async function convertToExpenditure(purchaseId: string) {
  if (!confirm('確定要將此採購單轉為應付帳款？')) return
  expenditureLoading[purchaseId] = true
  try {
    await apiFetch(`/admin/purchases/${purchaseId}/expenditure`, { method: 'PATCH' })
    await loadPurchases()
  } catch (e: any) {
    alert(e.message ?? '操作失敗')
  } finally {
    expenditureLoading[purchaseId] = false
  }
}

function goToPage(p: number) {
  pagination.page = p
  loadPurchases()
}

const totalPages = () => Math.max(1, Math.ceil(totalCount.value / pagination.pageSize))

function formatDate(d: string) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('zh-TW')
}

onMounted(() => {
  loadSuppliers()
  loadPurchases()
})
</script>

<template>
  <main class="purchases">
    <div class="purchases__header">
      <h1>採購管理</h1>
    </div>

    <!-- Tab navigation -->
    <div class="tabs">
      <button
        class="tab-btn"
        :class="{ 'tab-btn--active': tabIndex === 0 }"
        @click="tabIndex = 0"
      >
        供應商
      </button>
      <button
        class="tab-btn"
        :class="{ 'tab-btn--active': tabIndex === 1 }"
        @click="tabIndex = 1"
      >
        採購單
      </button>
    </div>

    <!-- Tab 0: Suppliers -->
    <section v-show="tabIndex === 0" class="tab-content">
      <p v-if="supplierError" class="msg msg--error">{{ supplierError }}</p>
      <p v-if="loadingSuppliers" class="msg msg--muted">載入中...</p>

      <table v-else class="table">
        <thead>
          <tr>
            <th>供應商名稱</th>
            <th>聯絡人</th>
            <th>電話</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="suppliers.length === 0">
            <td colspan="4" class="table__empty">尚無供應商</td>
          </tr>
          <template v-for="s in suppliers" :key="s.supplierId">
            <tr>
              <td class="td--name">{{ s.name }}</td>
              <td>{{ s.contact ?? '—' }}</td>
              <td>{{ s.phone ?? '—' }}</td>
              <td class="td--actions">
                <button class="btn btn--sm btn--ghost" @click="openEditSupplier(s)">編輯</button>
                <button class="btn btn--sm btn--danger-ghost" @click="deleteSupplier(s)">刪除</button>
              </td>
            </tr>
            <!-- Inline edit row -->
            <tr v-if="editSupplierId === s.supplierId" class="supplier-edit-row">
              <td colspan="4">
                <div class="supplier-edit-form">
                  <input v-model="editSupplier.name" class="input input--sm" placeholder="名稱" />
                  <input v-model="editSupplier.contact" class="input input--sm" placeholder="聯絡人" />
                  <input v-model="editSupplier.phone" class="input input--sm" placeholder="電話" />
                  <button class="btn btn--sm btn--primary" :disabled="editingSaving" @click="saveEditSupplier">
                    {{ editingSaving ? '儲存中…' : '儲存' }}
                  </button>
                  <button class="btn btn--sm btn--ghost" @click="editSupplierId = null">取消</button>
                  <span v-if="editingError" class="msg msg--error">{{ editingError }}</span>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>

      <!-- Inline create form -->
      <div class="inline-form-card">
        <h3 class="inline-form-card__title">新增供應商</h3>
        <p v-if="supplierSaveError" class="msg msg--error">{{ supplierSaveError }}</p>
        <p v-if="supplierSaveSuccess" class="msg msg--success">{{ supplierSaveSuccess }}</p>
        <form class="inline-form" @submit.prevent="createSupplier">
          <div class="form-row">
            <div class="form-field">
              <label class="label label--required">名稱</label>
              <input v-model="newSupplier.name" class="input" type="text" placeholder="供應商名稱" />
            </div>
            <div class="form-field">
              <label class="label">聯絡人</label>
              <input v-model="newSupplier.contact" class="input" type="text" placeholder="聯絡人姓名" />
            </div>
            <div class="form-field">
              <label class="label">電話</label>
              <input v-model="newSupplier.phone" class="input" type="text" placeholder="02-xxxx-xxxx" />
            </div>
            <div class="form-field form-field--action">
              <label class="label">&nbsp;</label>
              <button type="submit" class="btn btn--primary" :disabled="savingSupplier">
                {{ savingSupplier ? '儲存中...' : '新增' }}
              </button>
            </div>
          </div>
        </form>
      </div>
    </section>

    <!-- Tab 1: Purchases -->
    <section v-show="tabIndex === 1" class="tab-content">
      <div class="tab-content__toolbar">
        <button class="btn btn--primary" @click="router.push('/admin/purchases/new')">
          + 新增採購單
        </button>
      </div>

      <p v-if="purchaseError" class="msg msg--error">{{ purchaseError }}</p>
      <p v-if="loadingPurchases" class="msg msg--muted">載入中...</p>

      <table v-else class="table">
        <thead>
          <tr>
            <th>採購編號</th>
            <th>供應商</th>
            <th>日期</th>
            <th>狀態</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="purchases.length === 0">
            <td colspan="5" class="table__empty">無採購單</td>
          </tr>
          <template v-for="p in purchases" :key="p.purchaseId">
            <tr>
              <td class="td--mono">{{ p.purchaseId.slice(0, 8) }}…</td>
              <td>{{ p.supplierName ?? p.supplierId }}</td>
              <td>{{ formatDate(p.purchaseDate) }}</td>
              <td>
                <span class="badge badge--neutral">{{ p.status }}</span>
              </td>
              <td class="td--actions">
                <button
                  class="btn btn--sm btn--ghost"
                  :disabled="loadingDetail[p.purchaseId]"
                  @click="toggleDetail(p.purchaseId)"
                >
                  {{ detailMap[p.purchaseId] ? '收起' : loadingDetail[p.purchaseId] ? '載入...' : '查看' }}
                </button>
                <button
                  class="btn btn--sm btn--accent"
                  :disabled="expenditureLoading[p.purchaseId]"
                  @click="convertToExpenditure(p.purchaseId)"
                >
                  {{ expenditureLoading[p.purchaseId] ? '處理中...' : '轉應付' }}
                </button>
              </td>
            </tr>
            <!-- Inline detail row -->
            <tr v-if="detailMap[p.purchaseId]" class="detail-row">
              <td colspan="5">
                <div class="detail-panel">
                  <div class="detail-panel__meta">
                    <span><strong>付款方式：</strong>{{ detailMap[p.purchaseId]?.payment ?? '—' }}</span>
                    <span><strong>備註：</strong>{{ detailMap[p.purchaseId]?.note ?? '—' }}</span>
                  </div>
                  <table class="detail-table">
                    <thead>
                      <tr>
                        <th>商品</th>
                        <th>單價</th>
                        <th>數量</th>
                        <th>小計</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr
                        v-for="d in (detailMap[p.purchaseId]?.details ?? [])"
                        :key="d.purchaseDetailId"
                      >
                        <td>{{ d.productTitle ?? d.productId }}</td>
                        <td>NT$ {{ d.unitPrice.toLocaleString() }}</td>
                        <td>{{ d.qty }}</td>
                        <td>NT$ {{ (d.unitPrice * d.qty).toLocaleString() }}</td>
                      </tr>
                      <tr v-if="!(detailMap[p.purchaseId]?.details?.length)">
                        <td colspan="4" class="table__empty">無明細</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>

      <div class="purchases__pagination">
        <button
          class="btn btn--sm btn--ghost"
          :disabled="pagination.page <= 1"
          @click="goToPage(pagination.page - 1)"
        >
          &laquo; 上一頁
        </button>
        <span class="pagination__info">
          第 {{ pagination.page }} 頁（共 {{ totalCount }} 筆）
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
  </main>
</template>

<style scoped>
.purchases {
}

.purchases__header {
  margin-bottom: 1.25rem;
}

.purchases__header h1 {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  margin: 0;
}

/* Tabs */
.tabs {
  display: flex;
  gap: 0;
  border-bottom: 2px solid var(--tf-color-border);
  margin-bottom: 1.5rem;
}

.tab-btn {
  padding: 0.45rem 1rem;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  margin-bottom: -2px;
  cursor: pointer;
  font-size: 0.875rem;
  font-family: inherit;
  font-weight: 500;
  color: var(--tf-color-muted);
  position: relative;
  bottom: -2px;
  transition: color 0.15s;
}

.tab-btn--active {
  color: var(--tf-color-primary-dark);
  border-bottom-color: var(--tf-color-primary);
  font-weight: 700;
}

.tab-content {
  min-height: 200px;
}

.tab-content__toolbar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 1rem;
}

/* Table */
.table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
  margin-bottom: 0.5rem;
}

.table th {
  background: var(--tf-color-primary);
  color: #fff;
  padding: 0.6rem 0.75rem;
  text-align: left;
  font-weight: 600;
  font-size: 0.875rem;
}

.table td {
  padding: 0.55rem 0.75rem;
  border-bottom: 1px solid var(--tf-color-border);
  vertical-align: middle;
}

.table__empty {
  text-align: center;
  color: var(--tf-color-muted);
  padding: 2rem !important;
}

.td--mono {
  font-family: monospace;
  font-size: 0.8rem;
  color: var(--tf-color-muted);
}

.td--name {
  font-weight: 500;
}

.td--actions {
  white-space: nowrap;
  display: flex;
  gap: 0.4rem;
}

/* Detail row */
.detail-row td {
  padding: 0;
  background: #f8faf8;
}

.detail-panel {
  padding: 1rem 1.25rem;
  border-left: 4px solid var(--tf-color-primary);
}

.detail-panel__meta {
  display: flex;
  gap: 2rem;
  font-size: 0.85rem;
  margin-bottom: 0.75rem;
  color: #555;
}

.detail-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.85rem;
}

.detail-table th {
  background: var(--tf-color-primary);
  color: #fff;
  padding: 0.4rem 0.6rem;
  text-align: left;
  font-size: 0.875rem;
}

.detail-table td {
  padding: 0.35rem 0.6rem;
  border-bottom: 1px solid var(--tf-color-border);
}

/* Inline form */
.inline-form-card {
  margin-top: 1.5rem;
  background: #f8faf8;
  border: 1px solid #d4e8d5;
  border-radius: 8px;
  padding: 1.25rem;
}

.inline-form-card__title {
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--tf-color-primary);
  margin: 0 0 1rem 0;
}

.inline-form {
  width: 100%;
}

.form-row {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  align-items: flex-end;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  min-width: 150px;
  flex: 1;
}

.form-field--action {
  flex: 0;
  min-width: auto;
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

.input {
  padding: 0.45rem 0.65rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 0.875rem;
  background: #fff;
}

.input:focus {
  outline: none;
  border-color: var(--tf-color-primary);
  box-shadow: 0 0 0 2px rgba(62, 107, 68, 0.15);
}

/* Pagination */
.purchases__pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  margin-top: 1rem;
}

.pagination__info {
  font-size: 0.875rem;
  color: var(--tf-color-muted);
}

/* Badge */
.badge {
  display: inline-block;
  padding: 0.2rem 0.5rem;
  border-radius: 3px;
  font-size: 0.78rem;
  font-weight: 600;
}

.badge--neutral {
  background: #f0f0f0;
  color: #555;
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

.btn--accent {
  background: var(--tf-color-accent);
  color: #fff;
}

.btn--accent:hover:not(:disabled) {
  opacity: 0.85;
}

.btn--sm {
  padding: 0.25rem 0.6rem;
  font-size: 0.8rem;
}

.btn--danger-ghost {
  background: transparent;
  color: #dc3545;
  border: 1px solid #f5c6cb;
}
.btn--danger-ghost:hover:not(:disabled) { background: #fff5f5; }

.td--actions {
  white-space: nowrap;
  display: flex;
  gap: 0.35rem;
  align-items: center;
}

.supplier-edit-row td { background: #f8fafc; }
.supplier-edit-form {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  flex-wrap: wrap;
  padding: 0.5rem 0;
}
.input--sm {
  padding: 0.35rem 0.6rem;
  font-size: 0.85rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 5px;
  font-family: inherit;
}
</style>

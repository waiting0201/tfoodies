<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

interface Warehouse { warehouseid: string; warehousetype: number; title: string }
interface PurchaseOption { purchaseId: string; purchaseCode: string; purchaseDate: string; supplierName?: string }
interface DetailOption {
  purchaseDetailId: string
  productId: string
  productNum?: string
  productTitle?: string
  qty: number
  status: number
}

const route = useRoute()
const router = useRouter()
const stockId = computed(() => (route.params.id as string) || '')
const isEdit = computed(() => !!stockId.value)
// 類型：新增由 query 帶入；編輯由載入結果決定
const stockType = ref<number>(Number(route.query.type) === 2 ? 2 : 1)
const isDeclared = computed(() => stockType.value === 1)

const today = new Date().toISOString().slice(0, 10)

const warehouses = ref<Warehouse[]>([])
const purchaseOptions = ref<PurchaseOption[]>([])
const detailOptions = ref<DetailOption[]>([])

const loading = ref(false)
const saving = ref(false)
const error = ref('')
const successMsg = ref('')

// 編輯時鎖定採購/產品，僅顯示
const lockedPurchaseCode = ref('')
const lockedProduct = ref('')
const quantityEditable = ref(true)

const form = reactive({
  purchaseId: '',
  purchaseDetailId: '',
  warehouseId: '',
  createDate: today,
  detailStatus: 1,
  barcode: '',
  noticeNumber: '',
  declarationNumber: '',
  item: null as number | null,
  manufactureDate: '',
  expireDate: '',
  quantity: 1,
  weight: null as number | null,
  status: 1,
})

const noticeValid = ref(true)
const noticeChecking = ref(false)

const title = computed(() =>
  isEdit.value
    ? (isDeclared.value ? '編輯需申報入庫' : '編輯不需申報入庫')
    : (isDeclared.value ? '新增需申報入庫' : '新增不需申報入庫')
)

const selectedDetail = computed(() =>
  detailOptions.value.find(d => d.purchaseDetailId === form.purchaseDetailId) ?? null
)

function errMsg(e: unknown, fallback: string) {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

async function loadRefs() {
  const [wh, po] = await Promise.allSettled([
    apiFetch<Warehouse[]>('/admin/warehouses'),
    apiFetch<PurchaseOption[]>('/admin/stocks/purchasable'),
  ])
  if (wh.status === 'fulfilled') warehouses.value = wh.value
  if (po.status === 'fulfilled') purchaseOptions.value = po.value
}

async function onPurchaseChange() {
  form.purchaseDetailId = ''
  detailOptions.value = []
  if (!form.purchaseId) return
  try {
    detailOptions.value = await apiFetch<DetailOption[]>(`/admin/stocks/purchasable/${form.purchaseId}/details`)
  } catch (e) {
    error.value = errMsg(e, '載入採購明細失敗')
  }
}

async function checkNotice() {
  if (!isDeclared.value || !form.noticeNumber.trim()) { noticeValid.value = true; return }
  noticeChecking.value = true
  try {
    const params = new URLSearchParams({ noticeNumber: form.noticeNumber.trim() })
    if (isEdit.value) params.set('excludeId', stockId.value)
    const res = await apiFetch<{ valid: boolean }>(`/admin/stocks/check-notice?${params}`)
    noticeValid.value = res.valid
  } catch {
    noticeValid.value = true
  } finally {
    noticeChecking.value = false
  }
}

async function loadStock() {
  if (!isEdit.value) return
  loading.value = true
  try {
    const s = await apiFetch<any>(`/admin/stocks/${stockId.value}`)
    stockType.value = s.stockType
    lockedPurchaseCode.value = s.purchaseCode ?? ''
    lockedProduct.value = `${s.productNum ?? ''} ${s.productTitle ?? ''}`.trim()
    quantityEditable.value = !!s.quantityEditable
    form.purchaseId = s.purchaseId ?? ''
    form.purchaseDetailId = s.purchaseDetailId ?? ''
    form.detailStatus = s.detailStatus ?? 1
    form.createDate = s.createDate ? String(s.createDate).slice(0, 10) : today
    form.barcode = s.barcode ?? ''
    form.noticeNumber = s.noticeNumber ?? ''
    form.declarationNumber = s.declarationNumber ?? ''
    form.item = s.item ?? null
    form.manufactureDate = s.manufactureDate ? String(s.manufactureDate).slice(0, 10) : ''
    form.expireDate = s.expireDate ? String(s.expireDate).slice(0, 10) : ''
    form.quantity = s.quantity ?? 1
    form.weight = s.weight ?? null
    form.status = s.status ?? 1
  } catch (e) {
    error.value = errMsg(e, '載入入庫批次失敗')
  } finally {
    loading.value = false
  }
}

async function submit() {
  error.value = ''
  successMsg.value = ''

  if (!isEdit.value) {
    if (!form.purchaseId) { error.value = '請選擇採購單'; return }
    if (!form.purchaseDetailId) { error.value = '請選擇採購明細'; return }
    if (!form.warehouseId) { error.value = '請選擇入庫倉'; return }
  }
  if (!form.createDate) { error.value = '請選擇入庫日期'; return }
  if (!form.quantity || form.quantity <= 0) { error.value = '入庫數量必須大於 0'; return }
  if (isDeclared.value) {
    if (!form.expireDate) { error.value = '請選擇有效日期'; return }
    if (form.noticeNumber.trim()) {
      await checkNotice()
      if (!noticeValid.value) { error.value = '通知號碼已存在'; return }
    }
  }

  saving.value = true
  try {
    if (isEdit.value) {
      const payload = buildDeclaredPayload({
        detailStatus: form.detailStatus,
        createDate: form.createDate,
        quantity: form.quantity,
      })
      await apiFetch(`/admin/stocks/${stockId.value}`, { method: 'PUT', body: JSON.stringify(payload) })
      successMsg.value = '入庫批次已更新'
    } else {
      const payload = {
        purchaseDetailId: form.purchaseDetailId,
        warehouseId: form.warehouseId,
        stockType: stockType.value,
        ...buildDeclaredPayload({
          detailStatus: form.detailStatus,
          createDate: form.createDate,
          quantity: form.quantity,
        }),
      }
      await apiFetch('/admin/stocks', { method: 'POST', body: JSON.stringify(payload) })
      successMsg.value = '入庫已建立'
    }
    setTimeout(() => router.push('/admin/inventory'), 700)
  } catch (e) {
    error.value = errMsg(e, '儲存失敗')
  } finally {
    saving.value = false
  }
}

// 共用 payload：不需申報只送基本欄位，需申報帶上申報欄位
function buildDeclaredPayload(base: { detailStatus: number; createDate: string; quantity: number }) {
  if (!isDeclared.value) {
    return { ...base, barcode: null, noticeNumber: null, declarationNumber: null, item: null, manufactureDate: null, expireDate: null, weight: null, status: null }
  }
  return {
    ...base,
    barcode: form.barcode || null,
    noticeNumber: form.noticeNumber || null,
    declarationNumber: form.declarationNumber || null,
    item: form.item,
    manufactureDate: form.manufactureDate || null,
    expireDate: form.expireDate || null,
    weight: form.weight,
    status: form.status,
  }
}

onMounted(async () => {
  await loadRefs()
  await loadStock()
})
</script>

<template>
  <div class="sform">
    <div class="sform__header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/inventory')">← 返回入庫維護</button>
      <h1 class="sform__title">{{ title }}</h1>
    </div>

    <p v-if="loading" class="sform__muted">載入中…</p>

    <form v-else @submit.prevent="submit">
      <p v-if="error" class="form-msg form-msg--error">{{ error }}</p>
      <p v-if="successMsg" class="form-msg form-msg--success">{{ successMsg }}</p>

      <div class="sform__layout">
        <div class="sform__main">
          <!-- 採購來源 -->
          <div class="form-card">
            <h2 class="form-section__title">採購來源</h2>

            <div v-if="isEdit" class="form-row">
              <div class="form-field">
                <label class="label">採購單</label>
                <input class="input" :value="lockedPurchaseCode" disabled />
              </div>
              <div class="form-field">
                <label class="label">產品</label>
                <input class="input" :value="lockedProduct" disabled />
              </div>
            </div>

            <div v-else class="form-row">
              <div class="form-field">
                <label class="label">採購單 <span class="req">*</span></label>
                <select v-model="form.purchaseId" class="select" @change="onPurchaseChange">
                  <option value="">請選擇採購單</option>
                  <option v-for="p in purchaseOptions" :key="p.purchaseId" :value="p.purchaseId">
                    {{ p.purchaseCode }}（{{ p.supplierName || '—' }}）
                  </option>
                </select>
              </div>
              <div class="form-field">
                <label class="label">採購明細 <span class="req">*</span></label>
                <select v-model="form.purchaseDetailId" class="select" :disabled="!form.purchaseId">
                  <option value="">請選擇採購明細</option>
                  <option v-for="d in detailOptions" :key="d.purchaseDetailId" :value="d.purchaseDetailId">
                    {{ d.productNum }} {{ d.productTitle }}（採購量 {{ d.qty }}）
                  </option>
                </select>
              </div>
            </div>
            <p v-if="!isEdit && selectedDetail" class="sform__hint">
              採購數量：{{ selectedDetail.qty }}
            </p>
          </div>

          <!-- 入庫資訊 -->
          <div class="form-card">
            <h2 class="form-section__title">入庫資訊</h2>
            <div class="form-row">
              <div v-if="!isEdit" class="form-field">
                <label class="label">入庫倉 <span class="req">*</span></label>
                <select v-model="form.warehouseId" class="select">
                  <option value="">請選擇入庫倉</option>
                  <option v-for="w in warehouses" :key="w.warehouseid" :value="w.warehouseid">{{ w.title }}</option>
                </select>
              </div>
              <div class="form-field">
                <label class="label">入庫日期 <span class="req">*</span></label>
                <input v-model="form.createDate" class="input" type="date" />
              </div>
              <div class="form-field">
                <label class="label">入庫數量 <span class="req">*</span></label>
                <input
                  v-model.number="form.quantity"
                  class="input"
                  type="number" min="1" step="1"
                  :disabled="isEdit && !quantityEditable"
                />
                <span v-if="isEdit && !quantityEditable" class="sform__note">已有出庫或拆批，數量不可修改</span>
              </div>
              <div class="form-field form-field--full">
                <label class="label">入庫狀態 <span class="req">*</span></label>
                <div class="radio-row">
                  <label class="radio"><input type="radio" :value="1" v-model="form.detailStatus" /> 完整</label>
                  <label class="radio"><input type="radio" :value="2" v-model="form.detailStatus" /> 有缺</label>
                  <label class="radio"><input type="radio" :value="3" v-model="form.detailStatus" /> 有多</label>
                </div>
              </div>
            </div>
          </div>

          <!-- 申報資訊（僅需申報） -->
          <div v-if="isDeclared" class="form-card">
            <h2 class="form-section__title">申報資訊</h2>
            <div class="form-row">
              <div class="form-field">
                <label class="label">條碼</label>
                <input v-model="form.barcode" class="input" type="text" />
              </div>
              <div class="form-field">
                <label class="label">通知號碼</label>
                <input
                  v-model="form.noticeNumber"
                  class="input"
                  :class="{ 'input--error': !noticeValid }"
                  type="text"
                  @blur="checkNotice"
                />
                <span v-if="noticeChecking" class="sform__note">檢查中…</span>
                <span v-else-if="!noticeValid" class="sform__error">通知號碼已存在</span>
              </div>
              <div class="form-field">
                <label class="label">報單號</label>
                <input v-model="form.declarationNumber" class="input" type="text" />
              </div>
              <div class="form-field">
                <label class="label">項次</label>
                <input v-model.number="form.item" class="input" type="number" min="0" step="1" />
              </div>
              <div class="form-field">
                <label class="label">製造日期</label>
                <input v-model="form.manufactureDate" class="input" type="date" />
              </div>
              <div class="form-field">
                <label class="label">有效日期 <span class="req">*</span></label>
                <input v-model="form.expireDate" class="input" type="date" />
              </div>
              <div class="form-field">
                <label class="label">淨重</label>
                <input v-model.number="form.weight" class="input" type="number" min="0" step="0.01" />
              </div>
              <div class="form-field form-field--full">
                <label class="label">檢驗狀態</label>
                <div class="radio-row">
                  <label class="radio"><input type="radio" :value="1" v-model="form.status" /> 合格</label>
                  <label class="radio"><input type="radio" :value="2" v-model="form.status" /> 待複檢</label>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- aside -->
        <div class="sform__aside">
          <div class="form-card">
            <h2 class="form-section__title">操作</h2>
            <div class="sform__submit-row">
              <button type="submit" class="btn btn--primary" :disabled="saving">
                {{ saving ? '儲存中…' : isEdit ? '儲存變更' : '確認入庫' }}
              </button>
              <button type="button" class="btn btn--ghost" @click="router.push('/admin/inventory')">取消</button>
            </div>
          </div>
        </div>
      </div>
    </form>
  </div>
</template>

<style scoped>
.sform { width: 100%; }
.sform__header { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.5rem; }
.sform__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); font-size: 1.25rem; margin: 0; }
.sform__muted { color: var(--tf-color-muted); }

.sform__layout { display: grid; grid-template-columns: 1fr; gap: 1.25rem; align-items: start; }
@media (min-width: 1024px) {
  .sform__layout { grid-template-columns: 1fr 360px; }
  .sform__aside { position: sticky; top: 1.5rem; }
}
@media (min-width: 1280px) {
  .sform__layout { grid-template-columns: 1fr 400px; }
}

.form-card { background: #fff; border: 1px solid var(--tf-color-border); border-radius: 6px; padding: 1.25rem; margin-bottom: 1.25rem; }
.sform__aside .form-card { padding: 1rem; }
.form-section__title { font-size: 1rem; font-weight: 600; color: var(--tf-color-primary-dark); margin: 0 0 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid var(--tf-color-border); }
.sform__aside .form-section__title { font-size: 0.875rem; margin-bottom: 0.75rem; padding-bottom: 0.4rem; }

.form-row { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 0.75rem 1.25rem; margin-bottom: 0.75rem; }
.form-field { display: flex; flex-direction: column; gap: 0.3rem; }
.form-field--full { grid-column: 1 / -1; }
.label { font-size: 0.8rem; font-weight: 500; color: #374151; }
.req { color: var(--tf-color-accent); margin-left: 0.1rem; }

.input, .select { padding: 0.5rem 0.75rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.9rem; font-family: inherit; background: #fff; transition: border-color 0.15s; }
.input:focus, .select:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.input:disabled { background: #f1f5f9; color: #64748b; }
.input--error { border-color: #dc3545; }

.radio-row { display: flex; gap: 1.25rem; padding-top: 0.3rem; }
.radio { display: inline-flex; align-items: center; gap: 0.35rem; font-size: 0.875rem; color: #475569; cursor: pointer; }
.radio input { accent-color: var(--tf-color-primary); }

.sform__hint { font-size: 0.8rem; color: var(--tf-color-muted); margin: 0; }
.sform__note { font-size: 0.78rem; color: var(--tf-color-muted); }
.sform__error { font-size: 0.78rem; color: #dc3545; }

.sform__submit-row { display: flex; flex-direction: column; gap: 0.5rem; }
.sform__submit-row .btn { width: 100%; justify-content: center; }

.form-msg--error { background: #fbeaea; color: #c0392b; border: 1px solid #f5c6c6; border-radius: 4px; padding: 0.6rem 0.9rem; font-size: 0.875rem; margin-bottom: 1rem; }
.form-msg--success { background: #e6f4ea; color: #1e7e34; border: 1px solid #b8dfc0; border-radius: 4px; padding: 0.6rem 0.9rem; font-size: 0.875rem; margin-bottom: 1rem; }

.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: all 0.15s; white-space: nowrap; text-decoration: none; font-family: inherit; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
</style>

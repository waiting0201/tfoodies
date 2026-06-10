<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'
import { toBlobUrl } from '../../lib/blobUrl'

interface Supplier { supplierId: string; title: string }
interface Exchange { exchangeId: string; title: string; rate: number }
interface ProductResult { productId: string; productNum: string; title: string; photo?: string; price?: number }
interface LineItem {
  purchaseDetailId?: string
  productId: string
  productNum: string
  productTitle: string
  unitPrice: number
  qty: number
}

const route = useRoute()
const router = useRouter()
const purchaseId = computed(() => (route.params.id as string) || '')
const isEdit = computed(() => !!purchaseId.value)

const suppliers = ref<Supplier[]>([])
const exchanges = ref<Exchange[]>([])
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const successMsg = ref('')

const header = reactive({
  supplierId: '',
  exchangeId: '',
  purchaseDate: new Date().toISOString().slice(0, 10),
  etd: '',
  payment: '',
  deliverTerm: '',
  note: '',
})

const lineItems = ref<LineItem[]>([])

// ── Product search ────────────────────────────────────────
const productKeyword = ref('')
const productResults = ref<ProductResult[]>([])
const productSearching = ref(false)

function errMsg(e: unknown, fallback: string) {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

async function searchProducts() {
  if (!productKeyword.value.trim()) return
  productSearching.value = true
  productResults.value = []
  try {
    const params = new URLSearchParams({ keyword: productKeyword.value.trim(), pageSize: '10' })
    const res = await apiFetch<{ items: ProductResult[] }>(`/admin/products?${params}`)
    productResults.value = res.items
  } catch {
    productResults.value = []
  } finally {
    productSearching.value = false
  }
}

function addProduct(p: ProductResult) {
  if (lineItems.value.some(i => i.productId === p.productId)) {
    alert('產品不可以重複加入')
    return
  }
  lineItems.value.push({
    productId: p.productId,
    productNum: p.productNum,
    productTitle: p.title,
    unitPrice: 0,
    qty: 1,
  })
  productResults.value = []
  productKeyword.value = ''
}

function removeRow(index: number) {
  lineItems.value.splice(index, 1)
}

const grandTotal = computed(() =>
  lineItems.value.reduce((sum, item) => sum + (item.unitPrice || 0) * (item.qty || 0), 0)
)

// ── Load reference data + (edit) existing purchase ────────
async function loadRefs() {
  const [sup, exc] = await Promise.allSettled([
    apiFetch<Supplier[]>('/admin/suppliers'),
    apiFetch<Exchange[]>('/admin/exchanges'),
  ])
  if (sup.status === 'fulfilled') suppliers.value = sup.value
  if (exc.status === 'fulfilled') exchanges.value = exc.value
}

async function loadPurchase() {
  if (!isEdit.value) return
  loading.value = true
  try {
    const res = await apiFetch<{ purchase: any; details: any[] }>(`/admin/purchases/${purchaseId.value}`)
    const p = res.purchase
    header.supplierId = p.supplierId
    header.exchangeId = p.exchangeId
    header.purchaseDate = p.purchaseDate ? String(p.purchaseDate).slice(0, 10) : ''
    header.etd = p.etd ? String(p.etd).slice(0, 10) : ''
    header.payment = p.payment ?? ''
    header.deliverTerm = p.deliverTerm ?? ''
    header.note = p.note ?? ''
    lineItems.value = (res.details ?? []).map(d => ({
      purchaseDetailId: d.purchaseDetailId,
      productId: d.productId,
      productNum: d.productNum ?? '',
      productTitle: d.productTitle ?? '',
      unitPrice: Number(d.unitPrice) || 0,
      qty: Number(d.qty) || 0,
    }))
  } catch (e) {
    error.value = errMsg(e, '載入採購單失敗')
  } finally {
    loading.value = false
  }
}

async function submit() {
  if (!header.supplierId) { error.value = '請選擇供應商'; return }
  if (!header.exchangeId) { error.value = '請選擇幣別'; return }
  if (!header.purchaseDate) { error.value = '請選擇採購日期'; return }
  const validItems = lineItems.value.filter(i => i.productId && i.qty > 0)
  if (validItems.length === 0) { error.value = '至少需要一筆採購明細'; return }

  saving.value = true
  error.value = ''
  successMsg.value = ''
  const payload = {
    supplierId: header.supplierId,
    exchangeId: header.exchangeId,
    purchaseDate: header.purchaseDate,
    etd: header.etd || null,
    payment: header.payment || '',
    deliverTerm: header.deliverTerm || null,
    note: header.note || null,
    items: validItems.map(i => ({
      purchaseDetailId: i.purchaseDetailId,
      productId: i.productId,
      unitPrice: i.unitPrice,
      qty: i.qty,
    })),
  }
  try {
    if (isEdit.value)
      await apiFetch(`/admin/purchases/${purchaseId.value}`, { method: 'PUT', body: JSON.stringify(payload) })
    else
      await apiFetch('/admin/purchases', { method: 'POST', body: JSON.stringify(payload) })
    successMsg.value = isEdit.value ? '採購單已更新' : '採購單已建立'
    setTimeout(() => router.push('/admin/purchases'), 700)
  } catch (e) {
    error.value = errMsg(e, '儲存失敗')
  } finally {
    saving.value = false
  }
}

function formatMoney(n: number) {
  return 'NT$ ' + (n ?? 0).toLocaleString()
}

onMounted(async () => {
  await loadRefs()
  await loadPurchase()
})
</script>

<template>
  <div class="pform">
    <div class="pform__header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/purchases')">← 返回採購管理</button>
      <h1 class="pform__title">{{ isEdit ? '編輯採購單' : '新增採購單' }}</h1>
    </div>

    <p v-if="loading" class="pform__muted">載入中…</p>

    <form v-else @submit.prevent="submit">
      <p v-if="error" class="form-msg form-msg--error">{{ error }}</p>
      <p v-if="successMsg" class="form-msg form-msg--success">{{ successMsg }}</p>

      <div class="pform__layout">
        <div class="pform__main">
          <!-- 採購資訊 -->
          <div class="form-card">
            <h2 class="form-section__title">採購資訊</h2>
            <div class="form-row">
              <div class="form-field">
                <label class="label">供應商 <span class="req">*</span></label>
                <select v-model="header.supplierId" class="select">
                  <option value="">請選擇供應商</option>
                  <option v-for="s in suppliers" :key="s.supplierId" :value="s.supplierId">{{ s.title }}</option>
                </select>
              </div>
              <div class="form-field">
                <label class="label">幣別 <span class="req">*</span></label>
                <select v-model="header.exchangeId" class="select">
                  <option value="">請選擇幣別</option>
                  <option v-for="e in exchanges" :key="e.exchangeId" :value="e.exchangeId">{{ e.title }}</option>
                </select>
              </div>
              <div class="form-field">
                <label class="label">採購日期 <span class="req">*</span></label>
                <input v-model="header.purchaseDate" class="input" type="date" />
              </div>
              <div class="form-field">
                <label class="label">ETD</label>
                <input v-model="header.etd" class="input" type="date" />
              </div>
              <div class="form-field">
                <label class="label">付款條件</label>
                <input v-model="header.payment" class="input" type="text" placeholder="如：月結 30 天" />
              </div>
              <div class="form-field">
                <label class="label">交貨期限</label>
                <input v-model="header.deliverTerm" class="input" type="text" placeholder="交貨期限" />
              </div>
              <div class="form-field form-field--full">
                <label class="label">備註</label>
                <textarea v-model="header.note" class="textarea" rows="3" placeholder="選填備註"></textarea>
              </div>
            </div>
          </div>

          <!-- 採購明細 -->
          <div class="form-card">
            <h2 class="form-section__title">採購明細</h2>

            <div class="pform__search">
              <input
                v-model="productKeyword"
                class="input pform__search-input"
                type="text"
                placeholder="輸入產品編號或名稱搜尋"
                @keyup.enter="searchProducts"
              />
              <button type="button" class="btn btn--secondary btn--sm" :disabled="productSearching" @click="searchProducts">
                {{ productSearching ? '搜尋中…' : '搜尋' }}
              </button>
            </div>

            <ul v-if="productResults.length" class="pform__results">
              <li v-for="p in productResults" :key="p.productId" class="pform__result" @click="addProduct(p)">
                <img v-if="p.photo" :src="toBlobUrl(p.photo)" class="pform__thumb" alt="" />
                <span v-else class="pform__thumb pform__thumb--empty">—</span>
                <span class="pform__result-num">{{ p.productNum }}</span>
                <span class="pform__result-title">{{ p.title }}</span>
              </li>
            </ul>

            <div class="pform__table-wrap">
              <table class="pform__table">
                <thead>
                  <tr>
                    <th>產品編號</th>
                    <th>產品名稱</th>
                    <th style="width:8rem;text-align:right">單價</th>
                    <th style="width:6rem;text-align:right">數量</th>
                    <th style="width:9rem;text-align:right">小計</th>
                    <th style="width:3rem"></th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(item, idx) in lineItems" :key="item.productId">
                    <td class="font-mono">{{ item.productNum }}</td>
                    <td>{{ item.productTitle }}</td>
                    <td>
                      <input v-model.number="item.unitPrice" class="input input--cell" type="number" min="0" step="1" />
                    </td>
                    <td>
                      <input v-model.number="item.qty" class="input input--cell" type="number" min="1" step="1" />
                    </td>
                    <td style="text-align:right">{{ formatMoney((item.unitPrice || 0) * (item.qty || 0)) }}</td>
                    <td style="text-align:center">
                      <button type="button" class="btn-remove" title="刪除此行" @click="removeRow(idx)">&times;</button>
                    </td>
                  </tr>
                  <tr v-if="lineItems.length === 0">
                    <td colspan="6" class="pform__empty">尚未加入任何產品，請於上方搜尋並點選。</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- aside -->
        <div class="pform__aside">
          <div class="form-card">
            <h2 class="form-section__title">合計</h2>
            <div class="pform__total-row">
              <span>採購總金額</span>
              <strong class="pform__total">{{ formatMoney(grandTotal) }}</strong>
            </div>
            <div class="pform__submit-row">
              <button type="submit" class="btn btn--primary" :disabled="saving">
                {{ saving ? '儲存中…' : isEdit ? '儲存變更' : '建立採購單' }}
              </button>
              <button type="button" class="btn btn--ghost" @click="router.push('/admin/purchases')">取消</button>
            </div>
          </div>
        </div>
      </div>
    </form>
  </div>
</template>

<style scoped>
.pform { width: 100%; }
.pform__header { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.5rem; }
.pform__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); font-size: 1.25rem; margin: 0; }
.pform__muted { color: var(--tf-color-muted); }

.pform__layout { display: grid; grid-template-columns: 1fr; gap: 1.25rem; align-items: start; }
@media (min-width: 1024px) {
  .pform__layout { grid-template-columns: 1fr 360px; }
  .pform__aside { position: sticky; top: 1.5rem; }
}
@media (min-width: 1280px) {
  .pform__layout { grid-template-columns: 1fr 400px; }
}

.form-card { background: #fff; border: 1px solid var(--tf-color-border); border-radius: 6px; padding: 1.25rem; margin-bottom: 1.25rem; }
.pform__aside .form-card { padding: 1rem; }

.form-section__title { font-size: 1rem; font-weight: 600; color: var(--tf-color-primary-dark); margin: 0 0 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid var(--tf-color-border); }
.pform__aside .form-section__title { font-size: 0.875rem; margin-bottom: 0.75rem; padding-bottom: 0.4rem; }

.form-row { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 0.75rem 1.25rem; margin-bottom: 0.75rem; }
.form-field { display: flex; flex-direction: column; gap: 0.3rem; }
.form-field--full { grid-column: 1 / -1; }
.label { font-size: 0.8rem; font-weight: 500; color: #374151; }
.req { color: var(--tf-color-accent); margin-left: 0.1rem; }

.input, .select, .textarea { padding: 0.5rem 0.75rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.9rem; font-family: inherit; background: #fff; transition: border-color 0.15s; }
.input:focus, .select:focus, .textarea:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.textarea { resize: vertical; }

/* Product search */
.pform__search { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.75rem; }
.pform__search-input { flex: 1; }
.pform__results { list-style: none; margin: 0 0 0.75rem; padding: 0; border: 1px solid var(--tf-color-border); border-radius: 6px; max-height: 18rem; overflow-y: auto; }
.pform__result { display: flex; align-items: center; gap: 0.65rem; padding: 0.5rem 0.65rem; cursor: pointer; border-bottom: 1px solid var(--tf-color-border); }
.pform__result:last-child { border-bottom: none; }
.pform__result:hover { background: #f8faf8; }
.pform__thumb { width: 38px; height: 38px; object-fit: cover; border-radius: 4px; flex-shrink: 0; }
.pform__thumb--empty { display: inline-flex; align-items: center; justify-content: center; background: #f1f5f9; color: var(--tf-color-muted); font-size: 0.85rem; }
.pform__result-num { font-family: 'IBM Plex Mono', monospace; font-size: 0.8rem; color: var(--tf-color-muted); }
.pform__result-title { font-size: 0.875rem; color: #334155; }

/* Line items */
.pform__table-wrap { overflow-x: auto; }
.pform__table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 560px; }
.pform__table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.55rem 0.65rem; font-weight: 600; white-space: nowrap; }
.pform__table td { padding: 0.45rem 0.65rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.pform__empty { text-align: center; color: var(--tf-color-muted); padding: 1.75rem; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }
.input--cell { width: 100%; box-sizing: border-box; text-align: right; padding: 0.35rem 0.5rem; font-size: 0.85rem; }
.btn-remove { width: 26px; height: 26px; border: none; border-radius: 50%; background: #fbeaea; color: #c0392b; font-size: 1rem; line-height: 1; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; transition: background 0.15s; }
.btn-remove:hover { background: #f5c6c6; }

/* Aside total */
.pform__total-row { display: flex; align-items: center; justify-content: space-between; font-size: 0.875rem; color: #475569; margin-bottom: 1rem; }
.pform__total { font-size: 1.1rem; color: var(--tf-color-primary-dark); }
.pform__submit-row { display: flex; flex-direction: column; gap: 0.5rem; }
.pform__submit-row .btn { width: 100%; justify-content: center; }

/* Messages */
.form-msg--error { background: #fbeaea; color: #c0392b; border: 1px solid #f5c6c6; border-radius: 4px; padding: 0.6rem 0.9rem; font-size: 0.875rem; margin-bottom: 1rem; }
.form-msg--success { background: #e6f4ea; color: #1e7e34; border: 1px solid #b8dfc0; border-radius: 4px; padding: 0.6rem 0.9rem; font-size: 0.875rem; margin-bottom: 1rem; }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: all 0.15s; white-space: nowrap; text-decoration: none; font-family: inherit; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }
</style>

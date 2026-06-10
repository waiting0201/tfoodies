<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

const route = useRoute()
const router = useRouter()

// ─── 模式判斷 ─────────────────────────────────────────────────────────────────

const editId = computed(() => route.params.id as string | undefined)
const isEdit = computed(() => !!editId.value)

// ─── 列舉標籤 ─────────────────────────────────────────────────────────────────

const RECEIVE_OPTIONS = [
  { value: 0, label: '退貨中' },
  { value: 1, label: '已到達' },
  { value: 2, label: '取消' },
  { value: 3, label: '免退回' },
]

const REFUND_OPTIONS = [
  { value: 0, label: '未退款' },
  { value: 1, label: '已退款' },
  { value: 2, label: '折讓' },
  { value: 3, label: '免退款' },
  { value: 4, label: '取消' },
]

// ─── types ────────────────────────────────────────────────────────────────────

interface OrderSearchResult {
  orderId: string
  code: string
  memberName: string
  orderDate?: string
}

interface OrderDetailItem {
  id: string            // orderDetailId
  productName: string
  productNum: string
  qty: number
  unitPrice: number
  subtotal: number
}

interface ReturnDetailItem {
  returndetailid?: string
  orderdetailid: string
  qty: number
  price: number
  subtotal: number
  productTitle: string
  maxQty?: number       // 記錄原始訂單 qty，用於限制輸入
}

// ─── 新增模式：訂單搜尋 ───────────────────────────────────────────────────────

const orderKeyword = ref('')
const orderResults = ref<OrderSearchResult[]>([])
const orderSearching = ref(false)
const selectedOrder = ref<OrderSearchResult | null>(null)
const loadingOrderDetail = ref(false)
const orderDetailError = ref('')

async function searchOrders() {
  if (!orderKeyword.value.trim()) return
  orderSearching.value = true
  orderResults.value = []
  try {
    const params = new URLSearchParams({ keyword: orderKeyword.value.trim(), pageSize: '10' })
    const res = await apiFetch<{ items: OrderSearchResult[] }>(`/admin/orders?${params}`)
    orderResults.value = res.items ?? []
  } catch {
    orderResults.value = []
  } finally {
    orderSearching.value = false
  }
}

async function selectOrder(o: OrderSearchResult) {
  selectedOrder.value = o
  orderResults.value = []
  orderKeyword.value = ''
  returnDetails.value = []
  orderDetailError.value = ''
  loadingOrderDetail.value = true
  form.orderId = o.orderId
  try {
    // GET /admin/orders/{code} 取得明細
    const res = await apiFetch<{ items: OrderDetailItem[] }>(`/admin/orders/${o.code}`)
    returnDetails.value = (res.items ?? []).map(d => ({
      orderdetailid: d.id,
      qty: 0,
      price: d.unitPrice,
      subtotal: 0,
      productTitle: d.productName,
      maxQty: d.qty,
    }))
  } catch (e) {
    orderDetailError.value = errMsg(e, '載入訂單明細失敗')
  } finally {
    loadingOrderDetail.value = false
  }
}

function clearOrder() {
  selectedOrder.value = null
  returnDetails.value = []
  form.orderId = ''
}

// ─── 表單主資料 ───────────────────────────────────────────────────────────────

const form = reactive({
  orderId: '',
  returnDate: '',
  receiveStatus: 0,
  receiveDate: '',
  refundStatus: 0,
  refundDate: '',
  note: '',
})

const returnDetails = ref<ReturnDetailItem[]>([])

// ─── 編輯模式：載入既有退貨單 ─────────────────────────────────────────────────

interface ReturnResponse {
  return: {
    returnid: string
    returncode: string
    returndate: string
    orderid: string
    ordercode: string
    memberName: string
    note: string
    receivestatus: number
    refundstatus: number
    warehousestatus: number
  }
  details: ReturnDetailItem[]
}

const loadingEdit = ref(false)
const loadError = ref('')

// 編輯模式下顯示用的唯讀欄位
const editInfo = ref<ReturnResponse['return'] | null>(null)

async function loadForEdit(id: string) {
  loadingEdit.value = true
  loadError.value = ''
  try {
    const data = await apiFetch<ReturnResponse>(`/admin/returns/${id}`)
    editInfo.value = data.return
    form.orderId = data.return.orderid
    form.returnDate = data.return.returndate ? data.return.returndate.slice(0, 10) : ''
    form.receiveStatus = data.return.receivestatus
    form.refundStatus = data.return.refundstatus
    form.note = data.return.note ?? ''
    // 欄位若後端回傳無 receiveDate/refundDate，先留空
    returnDetails.value = (data.details ?? []).map(d => ({
      returndetailid: d.returndetailid,
      orderdetailid: d.orderdetailid,
      qty: d.qty,
      price: d.price,
      subtotal: d.subtotal,
      productTitle: d.productTitle,
    }))
  } catch (e) {
    loadError.value = errMsg(e, '載入退貨單失敗')
  } finally {
    loadingEdit.value = false
  }
}

// ─── helpers ──────────────────────────────────────────────────────────────────

function errMsg(e: unknown, fallback: string) {
  const ae = e as ApiError
  if (ae.problem?.status === 501) return '此功能尚未開放（API 尚未實作）'
  return ae.problem?.detail ?? (e as Error).message ?? fallback
}

function fmtDate(d: string) {
  return d ? d.slice(0, 10) : '—'
}

function updateDetailSubtotal(d: ReturnDetailItem) {
  d.subtotal = d.qty * d.price
}

// 有效明細（qty > 0）
const validDetails = computed(() => returnDetails.value.filter(d => d.qty > 0))

// ─── 提交 ─────────────────────────────────────────────────────────────────────

const submitting = ref(false)
const submitError = ref('')
const submitSuccess = ref(false)

async function handleSubmit() {
  submitError.value = ''

  if (!form.orderId) {
    submitError.value = '請選擇訂單'
    return
  }
  if (!form.returnDate) {
    submitError.value = '請填寫退貨日期'
    return
  }
  if (!isEdit.value && validDetails.value.length === 0) {
    submitError.value = '請至少填寫一個商品的退貨數量'
    return
  }

  const detailsPayload = (isEdit.value ? returnDetails.value : validDetails.value).map(d => ({
    ...(d.returndetailid ? { returnDetailId: d.returndetailid } : {}),
    orderDetailId: d.orderdetailid,
    qty: d.qty,
  }))

  const body = JSON.stringify({
    orderId: form.orderId,
    returnDate: form.returnDate,
    receiveStatus: form.receiveStatus,
    receiveDate: form.receiveDate || null,
    refundStatus: form.refundStatus,
    refundDate: form.refundDate || null,
    note: form.note || null,
    details: detailsPayload,
  })

  submitting.value = true
  try {
    if (isEdit.value) {
      await apiFetch(`/admin/returns/${editId.value}`, { method: 'PUT', body })
    } else {
      await apiFetch('/admin/returns', { method: 'POST', body })
    }
    submitSuccess.value = true
    setTimeout(() => router.push('/admin/returns'), 1500)
  } catch (e) {
    submitError.value = errMsg(e, isEdit.value ? '更新失敗' : '建立退貨單失敗')
  } finally {
    submitting.value = false
  }
}

// ─── 初始化 ───────────────────────────────────────────────────────────────────

onMounted(() => {
  if (isEdit.value) {
    loadForEdit(editId.value!)
  }
})
</script>

<template>
  <div class="retform">
    <!-- 頁首 -->
    <div class="retform__header">
      <h1 class="retform__title">{{ isEdit ? '編輯退貨單' : '新增退貨單' }}</h1>
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/returns')">&larr; 返回退貨列表</button>
    </div>

    <!-- 編輯模式載入中 -->
    <p v-if="loadingEdit" class="retform__muted">載入中…</p>
    <p v-if="loadError" class="retform__error">{{ loadError }}</p>

    <!-- 成功訊息 -->
    <div v-if="submitSuccess" class="retform__alert retform__alert--success">
      退貨單已{{ isEdit ? '更新' : '建立' }}，即將返回列表…
    </div>

    <!-- 錯誤訊息 -->
    <div v-if="submitError" class="retform__alert retform__alert--error">{{ submitError }}</div>

    <form v-if="!loadingEdit && !loadError" @submit.prevent="handleSubmit">
      <div class="retform__layout">

        <!-- ── 左欄：主要資料 ──────────────────────────────────── -->
        <div class="retform__main">

          <!-- 編輯模式：退貨單基本資訊（唯讀） -->
          <div v-if="isEdit && editInfo" class="form-card">
            <h2 class="form-section__title">退貨單資訊</h2>
            <div class="form-row">
              <div class="form-field">
                <label class="label">退貨單號</label>
                <div class="retform__readonly">{{ editInfo.returncode }}</div>
              </div>
              <div class="form-field">
                <label class="label">關聯訂單</label>
                <div class="retform__readonly">{{ editInfo.ordercode }}</div>
              </div>
              <div class="form-field">
                <label class="label">會員</label>
                <div class="retform__readonly">{{ editInfo.memberName || '—' }}</div>
              </div>
            </div>
          </div>

          <!-- 新增模式：訂單搜尋 -->
          <div v-if="!isEdit" class="form-card">
            <h2 class="form-section__title">選擇訂單</h2>

            <div class="retform__search-wrap">
              <label class="label">以訂單編號 / 會員搜尋</label>
              <div class="retform__search-row">
                <input
                  v-model="orderKeyword"
                  class="input retform__search-input"
                  placeholder="輸入訂單編號或關鍵字"
                  @keyup.enter="searchOrders"
                />
                <button
                  type="button"
                  class="btn btn--secondary btn--sm retform__search-btn"
                  :disabled="orderSearching"
                  @click="searchOrders"
                >
                  {{ orderSearching ? '搜尋中…' : '搜尋' }}
                </button>
              </div>

              <!-- 搜尋結果下拉 -->
              <div v-if="orderResults.length > 0" class="retform__dropdown">
                <button
                  v-for="o in orderResults"
                  :key="o.orderId"
                  type="button"
                  class="retform__dropdown-item"
                  @click="selectOrder(o)"
                >
                  <span class="retform__dropdown-name">{{ o.code }}</span>
                  <span class="retform__dropdown-sub">{{ o.memberName }}{{ o.orderDate ? ' · ' + fmtDate(o.orderDate) : '' }}</span>
                </button>
              </div>
            </div>

            <!-- 已選訂單 -->
            <div v-if="selectedOrder" class="retform__selected-order">
              <span class="retform__selected-icon">✓</span>
              <div class="retform__selected-info">
                <span class="retform__selected-code">{{ selectedOrder.code }}</span>
                <span class="retform__selected-sub">{{ selectedOrder.memberName }}</span>
              </div>
              <button type="button" class="btn btn--ghost btn--sm" @click="clearOrder">變更</button>
            </div>
            <p v-else class="retform__hint">請先搜尋並選取要退貨的訂單</p>

            <!-- 訂單明細載入錯誤 -->
            <p v-if="orderDetailError" class="retform__error" style="margin-top:0.5rem">{{ orderDetailError }}</p>
          </div>

          <!-- 退貨商品明細 -->
          <div v-if="returnDetails.length > 0 || isEdit" class="form-card">
            <h2 class="form-section__title">退貨商品明細</h2>

            <div v-if="loadingOrderDetail" class="retform__muted">載入商品明細中…</div>

            <template v-else-if="returnDetails.length > 0">
              <div class="retform__items">
                <div class="retform__items-header">
                  <span class="retform__col-name">商品名稱</span>
                  <span class="retform__col-price">單價</span>
                  <span class="retform__col-qty">退貨數量</span>
                  <span class="retform__col-sub">小計</span>
                </div>
                <div
                  v-for="d in returnDetails"
                  :key="d.orderdetailid"
                  class="retform__item-row"
                >
                  <span class="retform__col-name">
                    <span class="retform__item-name">{{ d.productTitle }}</span>
                  </span>
                  <span class="retform__col-price">
                    NT$ {{ d.price.toLocaleString() }}
                  </span>
                  <span class="retform__col-qty">
                    <input
                      v-model.number="d.qty"
                      type="number"
                      min="0"
                      :max="d.maxQty"
                      class="retform__qty-input"
                      @input="updateDetailSubtotal(d)"
                    />
                    <span v-if="d.maxQty !== undefined" class="retform__qty-max">/ {{ d.maxQty }}</span>
                  </span>
                  <span class="retform__col-sub">
                    NT$ {{ (d.qty * d.price).toLocaleString() }}
                  </span>
                </div>
              </div>

              <!-- 退貨合計 -->
              <div class="retform__total-row">
                <span class="retform__total-label">退貨合計</span>
                <span class="retform__total-value">
                  NT$ {{ validDetails.reduce((acc, d) => acc + d.qty * d.price, 0).toLocaleString() }}
                </span>
              </div>
            </template>

            <p v-else class="retform__hint">{{ isEdit ? '無退貨明細' : '選取訂單後顯示商品明細' }}</p>
          </div>

        </div><!-- /.retform__main -->

        <!-- ── 右欄：狀態設定 + 提交 ───────────────────────────── -->
        <div class="retform__aside">

          <!-- 退貨日期 -->
          <div class="form-card">
            <h2 class="form-section__title">退貨資訊</h2>
            <div class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="returnDate">退貨日期 <span class="req">*</span></label>
                <input id="returnDate" v-model="form.returnDate" type="date" class="input" />
              </div>
            </div>
          </div>

          <!-- 收貨狀態 -->
          <div class="form-card">
            <h2 class="form-section__title">收貨狀態</h2>
            <div class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="receiveStatus">收貨狀態</label>
                <select id="receiveStatus" v-model="form.receiveStatus" class="select">
                  <option v-for="opt in RECEIVE_OPTIONS" :key="opt.value" :value="opt.value">
                    {{ opt.label }}
                  </option>
                </select>
              </div>
              <div class="form-field form-field--full">
                <label class="label" for="receiveDate">到貨日期</label>
                <input id="receiveDate" v-model="form.receiveDate" type="date" class="input" />
              </div>
            </div>
          </div>

          <!-- 退款狀態 -->
          <div class="form-card">
            <h2 class="form-section__title">退款狀態</h2>
            <div class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="refundStatus">退款狀態</label>
                <select id="refundStatus" v-model="form.refundStatus" class="select">
                  <option v-for="opt in REFUND_OPTIONS" :key="opt.value" :value="opt.value">
                    {{ opt.label }}
                  </option>
                </select>
              </div>
              <div class="form-field form-field--full">
                <label class="label" for="refundDate">退款日期</label>
                <input id="refundDate" v-model="form.refundDate" type="date" class="input" />
              </div>
            </div>
          </div>

          <!-- 備註 -->
          <div class="form-card">
            <h2 class="form-section__title">備註</h2>
            <div class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="note">退貨備註</label>
                <textarea id="note" v-model="form.note" class="textarea" rows="3" placeholder="退貨原因或備註（選填）"></textarea>
              </div>
            </div>
          </div>

          <!-- 提交按鈕 -->
          <div class="retform__submit-row">
            <button type="button" class="btn btn--ghost" @click="router.push('/admin/returns')">取消</button>
            <button type="submit" class="btn btn--primary" :disabled="submitting">
              {{ submitting ? (isEdit ? '更新中…' : '建立中…') : (isEdit ? '儲存變更' : '建立退貨單') }}
            </button>
          </div>

        </div><!-- /.retform__aside -->

      </div><!-- /.retform__layout -->
    </form>
  </div>
</template>

<style scoped>
.retform { width: 100%; }

/* 頁首 */
.retform__header {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: 1.5rem;
}
.retform__title {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  font-size: 1.25rem; margin: 0;
}
.retform__error { color: #dc3545; font-size: 0.875rem; margin: 0 0 0.75rem; }
.retform__muted { color: var(--tf-color-muted); font-size: 0.875rem; margin: 0 0 0.75rem; }
.retform__hint { font-size: 0.82rem; color: var(--tf-color-muted); margin: 0.25rem 0 0; }

/* 提示訊息 */
.retform__alert { padding: 0.75rem 1rem; border-radius: 4px; margin-bottom: 1rem; font-size: 0.9rem; }
.retform__alert--success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
.retform__alert--error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }

/* 兩欄版型 */
.retform__layout { display: grid; grid-template-columns: 1fr; gap: 1.25rem; align-items: start; }
@media (min-width: 1024px) {
  .retform__layout { grid-template-columns: 1fr 360px; }
  .retform__aside { position: sticky; top: 1.5rem; }
}
@media (min-width: 1280px) {
  .retform__layout { grid-template-columns: 1fr 400px; }
}

/* 卡片 */
.form-card {
  background: #fff; border: 1px solid var(--tf-color-border);
  border-radius: 6px; padding: 1.25rem; margin-bottom: 1.25rem;
}
.retform__aside .form-card { padding: 1rem; }
.retform__aside .form-section__title { font-size: 0.875rem; margin-bottom: 0.75rem; padding-bottom: 0.4rem; }

/* Section 標題 */
.form-section__title {
  font-size: 1rem; font-weight: 600;
  color: var(--tf-color-primary-dark);
  margin: 0 0 1rem; padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--tf-color-border);
}

/* 欄位佈局 */
.form-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 0.75rem 1.25rem; margin-bottom: 0.75rem;
}
.form-field { display: flex; flex-direction: column; gap: 0.3rem; }
.form-field--full { grid-column: 1 / -1; }
.label { font-size: 0.8rem; font-weight: 500; color: #374151; }
.req { color: var(--tf-color-accent); margin-left: 0.1rem; }

/* 輸入控制 */
.input, .select, .textarea {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px; font-size: 0.9rem; font-family: inherit;
  background: #fff; transition: border-color 0.15s;
}
.input:focus, .select:focus, .textarea:focus {
  outline: none; border-color: var(--tf-color-primary);
  box-shadow: 0 0 0 2px rgba(38,183,188,0.15);
}
.textarea { resize: vertical; }

/* 唯讀顯示 */
.retform__readonly {
  padding: 0.45rem 0.65rem;
  background: #f8fafc; border: 1px solid var(--tf-color-border);
  border-radius: 4px; font-size: 0.9rem; color: #475569;
  font-family: 'IBM Plex Mono', monospace;
}

/* 訂單搜尋 */
.retform__search-wrap { display: flex; flex-direction: column; gap: 0.3rem; margin-bottom: 0.75rem; }
.retform__search-row { display: flex; align-items: center; gap: 0.5rem; flex-wrap: nowrap; }
.retform__search-input { flex: 1 1 0; min-width: 0; }
.retform__search-btn { flex-shrink: 0; white-space: nowrap; }

.retform__dropdown {
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  background: #fff; box-shadow: 0 4px 12px rgba(0,0,0,0.08);
  margin-top: 0.25rem; overflow: hidden; max-height: 200px; overflow-y: auto;
}
.retform__dropdown-item {
  display: flex; flex-direction: column; align-items: flex-start;
  gap: 0.1rem; width: 100%; padding: 0.6rem 0.75rem;
  background: none; border: none; cursor: pointer; text-align: left;
  border-bottom: 1px solid var(--tf-color-border); font-family: inherit;
  transition: background 0.1s;
}
.retform__dropdown-item:last-child { border-bottom: none; }
.retform__dropdown-item:hover { background: #f0fafa; }
.retform__dropdown-name { font-size: 0.875rem; font-weight: 500; color: #1f2937; font-family: 'IBM Plex Mono', monospace; }
.retform__dropdown-sub { font-size: 0.75rem; color: var(--tf-color-muted); }

/* 已選訂單列 */
.retform__selected-order {
  display: flex; align-items: center; gap: 0.75rem;
  padding: 0.6rem 0.75rem;
  background: #f0fafa; border: 1px solid var(--tf-color-primary);
  border-radius: 4px; margin-top: 0.5rem;
}
.retform__selected-icon { color: var(--tf-color-primary); font-weight: 700; font-size: 1rem; }
.retform__selected-info { flex: 1; display: flex; flex-direction: column; gap: 0.1rem; }
.retform__selected-code { font-size: 0.9rem; font-weight: 500; color: #1f2937; font-family: 'IBM Plex Mono', monospace; }
.retform__selected-sub { font-size: 0.75rem; color: var(--tf-color-muted); }

/* 商品明細表格 */
.retform__items {
  border: 1px solid var(--tf-color-border);
  border-radius: 4px; overflow: hidden; margin-bottom: 0.75rem;
}
.retform__items-header,
.retform__item-row {
  display: grid;
  grid-template-columns: 1fr 7rem 8rem 7rem;
  align-items: center; gap: 0.5rem; padding: 0.5rem 0.75rem;
}
.retform__items-header {
  background: var(--tf-color-primary); color: #fff;
  font-size: 0.8rem; font-weight: 500;
}
.retform__item-row {
  border-top: 1px solid var(--tf-color-border); font-size: 0.875rem;
}
.retform__item-row:hover { background: #f8faf8; }

.retform__col-name { display: flex; flex-direction: column; gap: 0.1rem; overflow: hidden; }
.retform__col-price, .retform__col-sub { white-space: nowrap; }
.retform__col-qty { display: flex; align-items: center; gap: 0.3rem; }

.retform__item-name { font-size: 0.875rem; color: #1f2937; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.retform__qty-input {
  width: 4.5rem; padding: 0.3rem 0.5rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  font-size: 0.875rem; font-family: inherit; text-align: center;
}
.retform__qty-input:focus { outline: none; border-color: var(--tf-color-primary); }
.retform__qty-max { font-size: 0.78rem; color: var(--tf-color-muted); }

/* 退貨合計列 */
.retform__total-row {
  display: flex; justify-content: flex-end; align-items: center;
  gap: 1.5rem; padding-top: 0.75rem;
  border-top: 2px solid var(--tf-color-primary);
}
.retform__total-label { font-size: 0.875rem; font-weight: 600; color: var(--tf-color-primary-dark); }
.retform__total-value { font-size: 1rem; font-weight: 700; color: var(--tf-color-primary-dark); white-space: nowrap; }

/* 提交列 */
.retform__submit-row {
  display: flex; flex-direction: column; gap: 0.5rem;
  margin-top: 0.5rem; padding-bottom: 1rem;
}
.retform__submit-row .btn { width: 100%; justify-content: center; padding: 0.6rem 1rem; }

/* 按鈕 */
.btn {
  display: inline-flex; align-items: center; justify-content: center;
  padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px;
  cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit;
  text-decoration: none; transition: opacity 0.15s, background 0.15s; white-space: nowrap;
}
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }
</style>

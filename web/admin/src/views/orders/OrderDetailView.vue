<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch, apiDownload, ApiError } from '../../lib/apiClient'

interface OrderItem {
  id: string
  productId: string
  productName: string
  productNum: string | null
  photo: string | null
  qty: number
  unitPrice: number
  subtotal: number
  isGift: boolean
}

interface OrderDetail {
  code: string
  orderDate: string
  createdAt: string
  memberId: string
  memberName: string
  memberMobile: string
  total: number
  shippingFee: number
  discount: number
  payType: number       // 1=信用卡, 2=貨到付款, 3=ATM, 4=免付款, 5=現金, 6=電匯, 7=支票
  payStatus: number     // 0=未付款, 1=已付款, 2=退款, 3=免付款, 4=取消
  deliverStatus: number // 0=未出貨, 4=待出貨, 1=已出貨, 3=已取消
  payDate: string | null
  deliverDate: string | null
  invoiceType: number   // 1=二聯, 2=捐贈, 3=三聯, 4=免開, 5=POS
  invoiceStatus: number // 0=未開, 1=已開, 2=作廢, 3=折讓
  invoiceCode: string | null
  companyTitle: string | null
  companyNumber: string | null
  loveCode: string | null
  atmCode: string | null
  atmExpiry: string | null
  receiverName: string
  receiverMobile: string
  receiverCity: string | null
  receiverArea: string | null
  receiverAddress: string
  receiverTime: number
  remark: string | null
  note: string | null
  trackingNumber: string | null
  items: OrderItem[]
}

const route = useRoute()
const router = useRouter()
const code = route.params.code as string

const order = ref<OrderDetail | null>(null)
const loading = ref(false)
const error = ref('')
const actionBusy = ref(false)
const actionError = ref('')

const showShipModal = ref(false)
const shipTracking = ref('')
const shipError = ref('')

// 對齊後端 Domain.Enums（PayType/PayStatus/InvoiceType/InvoiceStatus）。
const PAY_TYPE_LABELS: Record<number, string> = { 1: '信用卡', 2: '貨到付款', 3: 'ATM轉帳', 4: '免付款', 5: '現金', 6: '電匯', 7: '支票' }
const PAY_STATUS_LABELS: Record<number, string> = { 0: '未付款', 1: '已付款', 2: '退款', 3: '免付款', 4: '取消' }
const DELIVER_LABELS: Record<number, string> = { 0: '未出貨', 1: '已出貨', 3: '已取消', 4: '待出貨' }
const INVOICE_TYPE_LABELS: Record<number, string> = { 1: '二聯式', 2: '愛心捐贈', 3: '三聯式', 4: '免開', 5: 'POS機' }
const INVOICE_STATUS_LABELS: Record<number, string> = { 0: '未開立', 1: '已開立', 2: '作廢', 3: '折讓' }

function payTypeLabel(t: number) { return PAY_TYPE_LABELS[t] ?? `類型${t}` }
function payStatusLabel(s: number) { return PAY_STATUS_LABELS[s] ?? `${s}` }
function deliverLabel(s: number) { return DELIVER_LABELS[s] ?? `${s}` }
function invoiceTypeLabel(t: number) { return INVOICE_TYPE_LABELS[t] ?? `類型${t}` }
function invoiceStatusLabel(s: number) { return INVOICE_STATUS_LABELS[s] ?? `${s}` }

// 三聯式(B2B)但缺統一編號：無法開立 B2B 發票，須先補填統編。
const triplicateMissingUbn = computed(() =>
  order.value?.invoiceType === 3 && !order.value?.companyNumber?.trim())

async function load() {
  loading.value = true
  error.value = ''
  try {
    order.value = await apiFetch<OrderDetail>(`/admin/orders/${code}`)
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

async function patchOrder(action: string, body?: object) {
  actionBusy.value = true
  actionError.value = ''
  try {
    await apiFetch(`/admin/orders/${code}/${action}`, {
      method: 'PATCH',
      body: body ? JSON.stringify(body) : undefined,
    })
    await load()
  } catch (e) {
    actionError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
  } finally {
    actionBusy.value = false
  }
}

async function downloadDeliver() {
  try {
    await apiDownload(`/admin/orders/${code}/deliver`, `${code}_deliver.xlsx`)
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '出貨單下載失敗'
  }
}

async function handlePending() { await patchOrder('pending') }

function handleShip() {
  shipTracking.value = order.value?.trackingNumber ?? ''
  shipError.value = ''
  showShipModal.value = true
}

async function submitShip() {
  shipError.value = ''
  actionBusy.value = true
  try {
    await apiFetch(`/admin/orders/${code}/ship`, {
      method: 'PATCH',
      body: JSON.stringify({ trackingNumber: shipTracking.value.trim() || null }),
    })
    showShipModal.value = false
    await load()
  } catch (e) {
    shipError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
  } finally {
    actionBusy.value = false
  }
}

async function handleCancel() {
  if (!order.value) return
  if (!confirm(`確認取消訂單 ${order.value.code}？`)) return
  await patchOrder('cancel')
}

async function handlePay() { await patchOrder('pay') }

// 後台對未付款的信用卡訂單發起財金 WEBPOS 線上刷卡：取得 form 欄位後 auto-submit 整頁導向財金。
// 刷卡結果由財金導回 /store/payment/return-admin，再 302 回本訂單詳情頁。
async function handleCharge() {
  actionBusy.value = true
  actionError.value = ''
  try {
    const res = await apiFetch<{ actionUrl: string; fields: Record<string, string> }>(
      `/admin/orders/${code}/charge`, { method: 'POST' },
    )
    const f = document.createElement('form')
    f.method = 'post'
    f.action = res.actionUrl
    f.acceptCharset = 'UTF-8'
    for (const [k, v] of Object.entries(res.fields)) {
      const i = document.createElement('input')
      i.type = 'hidden'
      i.name = k
      i.value = v ?? ''
      f.appendChild(i)
    }
    document.body.appendChild(f)
    f.submit()
    // 不重置 actionBusy：頁面即將整頁導向財金。
  } catch (e) {
    actionError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '發起刷卡失敗'
    actionBusy.value = false
  }
}

// 補開電子發票（開票失敗、當下未開、或作廢後重新開立的訂單）。
async function handleIssueInvoice() {
  actionBusy.value = true
  actionError.value = ''
  try {
    await apiFetch(`/admin/orders/${code}/invoice`, { method: 'POST' })
    await load()
  } catch (e) {
    actionError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '開立發票失敗'
  } finally {
    actionBusy.value = false
  }
}

// 作廢電子發票（退貨/開錯）。作廢後狀態轉「已作廢」，可再按「重新開立發票」以新號重開。
async function handleVoidInvoice() {
  if (!order.value) return
  const reason = prompt(`作廢發票 ${order.value.invoiceCode}？\n請輸入作廢原因：`, '退貨')
  if (reason === null) return
  actionBusy.value = true
  actionError.value = ''
  try {
    await apiFetch(`/admin/orders/${code}/invoice/void`, {
      method: 'POST',
      body: JSON.stringify({ reason: reason.trim() || '退貨' }),
    })
    await load()
  } catch (e) {
    actionError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '作廢發票失敗'
  } finally {
    actionBusy.value = false
  }
}

function formatDate(s?: string | null) {
  if (!s) return '—'
  return new Date(s).toLocaleString('zh-TW')
}

onMounted(load)
</script>

<template>
  <main class="odetail">
    <div class="odetail__nav">
      <button class="odetail__back" @click="router.push('/admin/orders')">&larr; 返回訂單列表</button>
      <button v-if="order" class="odetail__edit-btn odetail__edit-btn--gray" @click="downloadDeliver">出貨單下載</button>
      <button v-if="order" class="odetail__edit-btn" @click="router.push(`/admin/orders/${code}/edit`)">編輯訂單</button>
    </div>

    <p v-if="loading" class="odetail__muted">載入中…</p>
    <p v-else-if="error" class="odetail__error">{{ error }}</p>

    <template v-else-if="order">
      <h1 class="odetail__title">訂單 {{ order.code }}</h1>

      <!-- 兩欄式佈局：左欄主要資訊、右欄發票 + 操作 -->
      <div class="odetail__cards-layout">

        <!-- 左欄：訂單資訊 + 會員/收件人 -->
        <div class="odetail__cards-main">

          <!-- 訂單資訊 -->
          <div class="odetail__card">
            <h2 class="odetail__section-title">訂單資訊</h2>
            <div class="odetail__grid">
              <div class="odetail__field">
                <span class="odetail__label">訂單編號</span>
                <span class="odetail__value odetail__value--mono">{{ order.code }}</span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">訂單日期</span>
                <span class="odetail__value">{{ formatDate(order.orderDate) }}</span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">建立時間</span>
                <span class="odetail__value">{{ formatDate(order.createdAt) }}</span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">付款狀態</span>
                <span :class="['odetail__badge', order.payStatus === 1 ? 'odetail__badge--paid' : 'odetail__badge--unpaid']">
                  {{ payStatusLabel(order.payStatus) }}
                </span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">出貨狀態</span>
                <span :class="['odetail__badge', `odetail__badge--deliver-${order.deliverStatus}`]">
                  {{ deliverLabel(order.deliverStatus) }}
                </span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">付款方式</span>
                <span class="odetail__value">{{ payTypeLabel(order.payType) }}</span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">付款時間</span>
                <span class="odetail__value">{{ formatDate(order.payDate) }}</span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">出貨時間</span>
                <span class="odetail__value">{{ formatDate(order.deliverDate) }}</span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">物流追蹤號</span>
                <span class="odetail__value odetail__value--mono">{{ order.trackingNumber || '—' }}</span>
              </div>
              <!-- ATM 欄位（僅 payType=1 時有效） -->
              <div class="odetail__field" v-if="order.payType === 1">
                <span class="odetail__label">ATM 繳款碼</span>
                <span class="odetail__value odetail__value--mono">{{ order.atmCode || '—' }}</span>
              </div>
              <div class="odetail__field" v-if="order.payType === 1">
                <span class="odetail__label">ATM 繳款期限</span>
                <span class="odetail__value">{{ formatDate(order.atmExpiry) }}</span>
              </div>
              <div class="odetail__field odetail__field--full" v-if="order.remark">
                <span class="odetail__label">備註</span>
                <span class="odetail__value">{{ order.remark }}</span>
              </div>
              <div class="odetail__field odetail__field--full" v-if="order.note">
                <span class="odetail__label">內部備注</span>
                <span class="odetail__value">{{ order.note }}</span>
              </div>
            </div>
          </div>

          <!-- 會員 / 收件人 -->
          <div class="odetail__card">
            <h2 class="odetail__section-title">會員 / 收件人</h2>
            <div class="odetail__grid">
              <div class="odetail__field">
                <span class="odetail__label">會員姓名</span>
                <span class="odetail__value">{{ order.memberName }}</span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">會員手機</span>
                <span class="odetail__value">{{ order.memberMobile || '—' }}</span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">收件人</span>
                <span class="odetail__value">{{ order.receiverName }}</span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">收件人手機</span>
                <span class="odetail__value">{{ order.receiverMobile }}</span>
              </div>
              <div class="odetail__field odetail__field--full">
                <span class="odetail__label">收件地址</span>
                <span class="odetail__value">{{ (order.receiverCity || '') + (order.receiverArea || '') + (order.receiverAddress || '') }}</span>
              </div>
            </div>
          </div>

        </div><!-- /.odetail__cards-main -->

        <!-- 右欄（aside）：發票資訊 + 操作按鈕 -->
        <div class="odetail__cards-aside">

          <!-- 發票資訊 -->
          <div class="odetail__card">
            <h2 class="odetail__section-title">發票資訊</h2>
            <div class="odetail__grid">
              <div class="odetail__field">
                <span class="odetail__label">發票類型</span>
                <span class="odetail__value">{{ invoiceTypeLabel(order.invoiceType) }}</span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">發票狀態</span>
                <span class="odetail__value">{{ invoiceStatusLabel(order.invoiceStatus) }}</span>
              </div>
              <div class="odetail__field">
                <span class="odetail__label">發票號碼</span>
                <span class="odetail__value odetail__value--mono">{{ order.invoiceCode || '—' }}</span>
              </div>
              <!-- 三聯式 -->
              <template v-if="order.invoiceType === 3">
                <div class="odetail__field">
                  <span class="odetail__label">公司抬頭</span>
                  <span class="odetail__value">{{ order.companyTitle || '—' }}</span>
                </div>
                <div class="odetail__field">
                  <span class="odetail__label">統一編號</span>
                  <span class="odetail__value odetail__value--mono">{{ order.companyNumber || '—' }}</span>
                </div>
                <p v-if="triplicateMissingUbn" class="odetail__hint odetail__hint--warn">
                  此訂單為三聯式發票但未填統一編號，無法開立 B2B 發票。請先於「編輯訂單」補填統編。
                </p>
              </template>
              <!-- 愛心捐贈 -->
              <div class="odetail__field" v-if="order.invoiceType === 2">
                <span class="odetail__label">愛心碼</span>
                <span class="odetail__value odetail__value--mono">{{ order.loveCode || '—' }}</span>
              </div>
            </div>
          </div>

          <!-- Action error -->
          <p v-if="actionError" class="odetail__error">{{ actionError }}</p>

          <!-- Action buttons -->
          <div class="odetail__actions">
            <button
              v-if="order.deliverStatus === 0"
              class="odetail__btn odetail__btn--secondary"
              :disabled="actionBusy"
              @click="handlePending"
            >標為待出貨</button>
            <button
              v-if="order.deliverStatus === 4"
              class="odetail__btn odetail__btn--primary"
              :disabled="actionBusy"
              @click="handleShip"
            >出貨（輸入追蹤號）</button>
            <button
              v-if="order.deliverStatus !== 3 && order.deliverStatus !== 1"
              class="odetail__btn odetail__btn--danger"
              :disabled="actionBusy"
              @click="handleCancel"
            >取消訂單</button>
            <button
              v-if="order.payStatus === 0"
              class="odetail__btn odetail__btn--accent"
              :disabled="actionBusy"
              @click="handlePay"
            >標記已付款</button>
            <button
              v-if="order.payType === 1 && order.payStatus === 0"
              class="odetail__btn odetail__btn--primary"
              :disabled="actionBusy"
              @click="handleCharge"
            >線上刷卡</button>
            <button
              v-if="(order.invoiceType === 1 || order.invoiceType === 3) && (order.invoiceStatus === 0 || order.invoiceStatus === 2)"
              class="odetail__btn odetail__btn--accent"
              :disabled="actionBusy || triplicateMissingUbn"
              :title="triplicateMissingUbn ? '三聯式發票缺統一編號，請先補填統編' : ''"
              @click="handleIssueInvoice"
            >{{ order.invoiceStatus === 2 ? '重新開立發票' : '補開發票' }}</button>
            <button
              v-if="(order.invoiceType === 1 || order.invoiceType === 3) && order.invoiceStatus === 1"
              class="odetail__btn odetail__btn--danger"
              :disabled="actionBusy"
              @click="handleVoidInvoice"
            >作廢發票</button>
          </div>

        </div><!-- /.odetail__cards-aside -->

      </div><!-- /.odetail__cards-layout -->

      <!-- 訂購商品（永遠全寬，在兩欄佈局下方） -->
      <div class="odetail__card">
        <h2 class="odetail__section-title">訂購商品</h2>
        <table class="odetail__table">
          <thead>
            <tr>
              <th>商品名稱</th>
              <th>單價</th>
              <th>數量</th>
              <th>小計</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in order.items" :key="item.id">
              <td>
                <span>{{ item.productName }}</span>
                <span v-if="item.isGift" class="odetail__gift-badge">贈品</span>
              </td>
              <td>NT$ {{ item.unitPrice.toLocaleString() }}</td>
              <td>{{ item.qty }}</td>
              <td>NT$ {{ item.subtotal.toLocaleString() }}</td>
            </tr>
          </tbody>
          <tfoot>
            <tr v-if="order.discount > 0" class="odetail__subtotal-row">
              <td colspan="3" class="odetail__subtotal-label">折扣</td>
              <td class="odetail__discount-amount">－NT$ {{ order.discount.toLocaleString() }}</td>
            </tr>
            <tr class="odetail__subtotal-row">
              <td colspan="3" class="odetail__subtotal-label">運費</td>
              <td>NT$ {{ (order.shippingFee ?? 0).toLocaleString() }}</td>
            </tr>
            <tr class="odetail__total-row">
              <td colspan="3" class="odetail__total-label">總計</td>
              <td class="odetail__total-amount">NT$ {{ order.total.toLocaleString() }}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </template>

    <!-- Ship modal -->
    <div v-if="showShipModal" class="odetail__modal-overlay" @click.self="showShipModal = false">
      <div class="odetail__modal" role="dialog" aria-modal="true" aria-label="出貨確認">
        <div class="odetail__modal-header">
          <h3 class="odetail__modal-title">出貨確認</h3>
          <button class="odetail__modal-close" @click="showShipModal = false">
            <svg style="width:1.1rem;height:1.1rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div class="odetail__modal-body">
          <label class="odetail__modal-label">物流追蹤號碼（可留空）</label>
          <input
            v-model="shipTracking"
            class="odetail__modal-input"
            placeholder="例：799123456789"
            @keyup.enter="submitShip"
            @keyup.esc="showShipModal = false"
            autofocus
          />
          <p v-if="shipError" class="odetail__modal-error">{{ shipError }}</p>
        </div>
        <div class="odetail__modal-footer">
          <button class="odetail__btn odetail__btn--ghost" @click="showShipModal = false">取消</button>
          <button class="odetail__btn odetail__btn--primary" :disabled="actionBusy" @click="submitShip">
            {{ actionBusy ? '處理中…' : '確認出貨' }}
          </button>
        </div>
      </div>
    </div>

  </main>
</template>

<style scoped>
.odetail {
  /* 填滿可用寬度，由 AdminLayout <main class="p-6"> 控制邊距 */
  width: 100%;
}

/* 兩欄式卡片佈局 */
.odetail__cards-layout {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.25rem;
  margin-bottom: 1.25rem;
}

@media (min-width: 1024px) {
  .odetail__cards-layout {
    grid-template-columns: 1fr 340px;
    align-items: start;
  }
  .odetail__cards-aside {
    position: sticky;
    top: 1.5rem;
  }
}

/* 左欄內的 card 間距（右欄 card 保留原 margin-bottom） */
.odetail__cards-main .odetail__card:last-child {
  margin-bottom: 0;
}

.odetail__nav {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.odetail__back {
  background: none;
  border: none;
  color: var(--tf-color-primary);
  cursor: pointer;
  font-size: 0.9rem;
  padding: 0;
  font-family: inherit;
  margin-right: auto;   /* 把後面的「出貨單下載／編輯訂單」推到最右 */
}
.odetail__back:hover { color: var(--tf-color-primary-dark); text-decoration: underline; }

.odetail__edit-btn {
  padding: 0.4rem 0.875rem;
  background: var(--tf-color-primary);
  color: #fff;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.875rem;
  font-family: inherit;
  transition: background 0.15s;
}
.odetail__edit-btn:hover { background: var(--tf-color-primary-dark); }

/* 灰色變體（出貨單下載：次要動作） */
.odetail__edit-btn--gray { background: #e9ecef; color: #495057; }
.odetail__edit-btn--gray:hover { background: #dee2e6; }

.odetail__title {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  margin-bottom: 1rem;
}

.odetail__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-bottom: 1.25rem;
}

.odetail__card {
  background: #fff;
  border: 1px solid var(--tf-color-border);
  border-radius: 6px;
  padding: 1.25rem;
  margin-bottom: 1.25rem;
}

.odetail__section-title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--tf-color-primary-dark);
  margin: 0 0 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--tf-color-border);
}

.odetail__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 0.75rem 1.5rem;
}

.odetail__field {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.odetail__field--full {
  grid-column: 1 / -1;
}

.odetail__label {
  font-size: 0.75rem;
  color: var(--tf-color-muted);
  text-transform: uppercase;
  letter-spacing: 0.03em;
}

.odetail__value {
  font-size: 0.9rem;
}

.odetail__value--mono {
  font-family: monospace;
  font-size: 0.85rem;
}

/* Badges */
.odetail__badge {
  display: inline-block;
  padding: 0.2rem 0.5rem;
  border-radius: 3px;
  font-size: 0.78rem;
}

.odetail__badge--paid { background: #d4edda; color: #155724; }
.odetail__badge--unpaid { background: #fff3cd; color: #856404; }
.odetail__badge--deliver-0 { background: #e2e3e5; color: #383d41; }
.odetail__badge--deliver-4 { background: #cce5ff; color: #004085; }
.odetail__badge--deliver-1 { background: #d4edda; color: #155724; }
.odetail__badge--deliver-3 { background: #f8d7da; color: #721c24; }

/* Gift badge */
.odetail__gift-badge {
  display: inline-block;
  margin-left: 0.4rem;
  padding: 0.1rem 0.4rem;
  border-radius: 3px;
  font-size: 0.72rem;
  background: #fef3c7;
  color: #92400e;
  font-weight: 500;
  vertical-align: middle;
}

/* Items table */
.odetail__table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.odetail__table th {
  background: var(--tf-color-primary);
  color: #fff;
  padding: 0.6rem 0.75rem;
  text-align: left;
  white-space: nowrap;
}

.odetail__table td {
  padding: 0.55rem 0.75rem;
  border-bottom: 1px solid var(--tf-color-border);
  vertical-align: middle;
}

.odetail__subtotal-row td {
  border-top: 1px solid var(--tf-color-border);
  color: var(--tf-color-muted);
}

.odetail__subtotal-label,
.odetail__total-label {
  text-align: right;
  font-weight: 500;
}

.odetail__discount-amount {
  color: var(--tf-color-accent);
}

.odetail__total-row td {
  border-top: 2px solid var(--tf-color-primary);
  font-weight: 600;
}

.odetail__total-amount {
  color: var(--tf-color-primary-dark);
  font-size: 1rem;
}

/* Buttons */
.odetail__btn {
  padding: 0.45rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.875rem;
  font-family: inherit;
  transition: opacity 0.15s;
}
.odetail__btn:disabled { opacity: 0.5; cursor: not-allowed; }

.odetail__btn--primary { background: var(--tf-color-primary); color: #fff; }
.odetail__btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); }

.odetail__btn--secondary { background: #e9ecef; color: #495057; }
.odetail__btn--secondary:hover:not(:disabled) { background: #dee2e6; }

.odetail__btn--danger { background: #dc3545; color: #fff; }
.odetail__btn--danger:hover:not(:disabled) { background: #b02a37; }

.odetail__btn--accent { background: var(--tf-color-accent); color: #fff; }
.odetail__btn--accent:hover:not(:disabled) { opacity: 0.85; }

.odetail__error { color: #dc3545; margin-bottom: 0.75rem; }
.odetail__hint { font-size: 0.8rem; margin: 0.25rem 0 0; grid-column: 1 / -1; }
.odetail__hint--warn { color: #b8860b; }
.odetail__muted { color: var(--tf-color-muted); }
.odetail__btn--ghost { background: transparent; color: var(--tf-color-primary); border: 1px solid var(--tf-color-primary); }
.odetail__btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }

/* Ship modal */
.odetail__modal-overlay {
  position: fixed; inset: 0; z-index: 60;
  background: rgba(15, 23, 42, 0.45);
  display: flex; align-items: center; justify-content: center;
  padding: 1rem;
}
.odetail__modal {
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 20px 60px rgba(0,0,0,0.2);
  width: 100%; max-width: 420px;
  display: flex; flex-direction: column;
}
.odetail__modal-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border);
}
.odetail__modal-title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.odetail__modal-close {
  background: none; border: none; cursor: pointer; color: var(--tf-color-muted);
  display: flex; padding: 0.25rem; border-radius: 4px;
}
.odetail__modal-close:hover { background: #f1f5f9; }
.odetail__modal-body { padding: 1.25rem 1.4rem; display: flex; flex-direction: column; gap: 0.5rem; }
.odetail__modal-label { font-size: 0.85rem; font-weight: 600; color: #475569; }
.odetail__modal-input {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 7px;
  font-size: 0.95rem;
  font-family: inherit;
  width: 100%; box-sizing: border-box;
}
.odetail__modal-input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38,183,188,0.15); }
.odetail__modal-error { color: #dc3545; font-size: 0.85rem; margin: 0; }
.odetail__modal-footer {
  display: flex; justify-content: flex-end; gap: 0.5rem;
  padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border);
}
</style>

<script setup lang="ts">
import { ref, computed, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'
import { toBlobUrl } from '../../lib/blobUrl'

const router = useRouter()

// ── 會員搜尋 ──────────────────────────────────────────────────────

interface MemberResult {
  memberId: string
  name: string
  mobile: string
  email: string
}

const memberKeyword = ref('')
const memberResults = ref<MemberResult[]>([])
const memberSearching = ref(false)
const selectedMember = ref<MemberResult | null>(null)

async function searchMembers() {
  if (!memberKeyword.value.trim()) return
  memberSearching.value = true
  memberResults.value = []
  try {
    const params = new URLSearchParams({ keyword: memberKeyword.value.trim(), pageSize: '10' })
    const res = await apiFetch<{ items: MemberResult[] }>(`/admin/members?${params}`)
    memberResults.value = res.items
  } catch {
    memberResults.value = []
  } finally {
    memberSearching.value = false
  }
}

function selectMember(m: MemberResult) {
  selectedMember.value = m
  memberResults.value = []
  memberKeyword.value = ''
  // 預填收件資訊
  form.receiverName = m.name
  form.receiverMobile = m.mobile
}

function clearMember() {
  selectedMember.value = null
}

// ── 商品搜尋 ──────────────────────────────────────────────────────

interface ProductResult {
  productId: string
  productNum: string
  title: string
  price: number
  photo: string | null
}

interface OrderItem {
  productId: string
  productName: string
  productNum: string
  photo: string | null
  unitPrice: number
  qty: number
}

const productKeyword = ref('')
const productResults = ref<ProductResult[]>([])
const productSearching = ref(false)
const orderItems = ref<OrderItem[]>([])

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
  // 若已加入，增加數量
  const existing = orderItems.value.find(i => i.productId === p.productId)
  if (existing) {
    existing.qty++
  } else {
    orderItems.value.push({
      productId: p.productId,
      productName: p.title,
      productNum: p.productNum,
      photo: p.photo,
      unitPrice: p.price,
      qty: 1,
    })
  }
  productResults.value = []
  productKeyword.value = ''
}

function removeItem(index: number) {
  orderItems.value.splice(index, 1)
}

// ── 表單主資料 ────────────────────────────────────────────────────

const form = reactive({
  receiverName: '',
  receiverMobile: '',
  receiverAddress: '',
  recivertime: 0,     // 0=不限, 1=上午, 2=下午（對應 DB recivertime）
  payType: 2,         // DB EnumPayType：1=信用卡 2=貨到付款 3=ATM 5=現金 6=電匯
  invoiceType: 1,     // DB EnumInvoiceType：1=二聯 2=捐贈 3=三聯 4=免開
  companytitle: '',   // 三聯式(3)：公司抬頭
  companynumber: '',  // 三聯式(3)：統一編號
  lovecode: '',       // 捐贈(2)：捐贈碼
  discount: 0,        // 人工折扣（元）
  remark: '',
  shippingFee: 0,
})

// ── 合計計算 ──────────────────────────────────────────────────────

const itemSubtotal = computed(() =>
  orderItems.value.reduce((acc, i) => acc + i.unitPrice * i.qty, 0)
)

// 滿 2000 免運
const computedShippingFee = computed(() =>
  itemSubtotal.value >= 2000 ? 0 : form.shippingFee
)

const grandTotal = computed(() => itemSubtotal.value + computedShippingFee.value - form.discount)

// ── 提交 ──────────────────────────────────────────────────────────

const submitting = ref(false)
const submitError = ref('')
const submitSuccess = ref(false)

async function handleSubmit() {
  submitError.value = ''

  // 基本驗證
  if (!selectedMember.value) { submitError.value = '請選擇會員'; return }
  if (!form.receiverName.trim()) { submitError.value = '請填寫收件人姓名'; return }
  if (!form.receiverMobile.trim()) { submitError.value = '請填寫收件人手機'; return }
  if (!form.receiverAddress.trim()) { submitError.value = '請填寫收件地址'; return }
  if (orderItems.value.length === 0) { submitError.value = '請至少新增一項商品'; return }

  // 欄位名稱需完全符合後端 CreateOrderRequest（camelCase）。
  const payload = {
    memberId: selectedMember.value.memberId,
    orderType: 2,                 // 線下單
    receiverName: form.receiverName,
    receiverMobile: form.receiverMobile,
    receiverAddress: form.receiverAddress,
    receiverZipcodeId: 0,         // 0 → 後端回退用會員登記地區
    receiverTime: form.recivertime,
    payType: form.payType,
    payStatus: 0,                 // 未付款
    deliverStatus: 0,             // 未出貨
    invoiceType: form.invoiceType,
    invoiceCode: null,
    companyTitle: form.invoiceType === 3 ? form.companytitle || null : null,
    companyNumber: form.invoiceType === 3 ? form.companynumber || null : null,
    loveCode: form.invoiceType === 2 ? form.lovecode || null : null,
    warehouseId: null,
    logisticId: null,
    trackingNumber: null,
    note: null,
    remark: form.remark || null,
    freight: computedShippingFee.value,
    discount: form.discount || 0,
    total: grandTotal.value,
    items: orderItems.value.map(i => ({
      productId: i.productId,
      qty: i.qty,
      price: i.unitPrice,
      subtotal: i.unitPrice * i.qty,
      isGift: false,
    })),
  }

  submitting.value = true
  try {
    // 接住回傳的 orderCode，新增後導向訂單詳情頁（信用卡單可直接按「線上刷卡」、其他可按「標記已付款」）。
    const res = await apiFetch<{ orderCode: string }>('/admin/orders', { method: 'POST', body: JSON.stringify(payload) })
    submitSuccess.value = true
    setTimeout(() => router.push(`/admin/orders/${res.orderCode}`), 1500)
  } catch (e) {
    const err = e as ApiError
    if (err.problem?.status === 501) {
      submitError.value = '此功能尚未開放（API 尚未實作）'
    } else {
      submitError.value = err.problem?.detail ?? (e as Error).message ?? '新增訂單失敗'
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="ocreate">
    <!-- 頁首 -->
    <div class="ocreate__header">
      <h1 class="ocreate__title">新增訂單</h1>
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/orders')">&larr; 返回訂單列表</button>
    </div>

    <!-- 成功提示 -->
    <div v-if="submitSuccess" class="ocreate__alert ocreate__alert--success">
      訂單已建立，即將跳轉至訂單列表…
    </div>

    <!-- 錯誤提示 -->
    <div v-if="submitError" class="ocreate__alert ocreate__alert--error">{{ submitError }}</div>

    <form @submit.prevent="handleSubmit">
      <div class="ocreate__layout">

        <!-- ── 左欄：主要資料輸入 ───────────────────────────── -->
        <div class="ocreate__main">

          <!-- 會員資訊 -->
          <div class="form-card">
            <h2 class="form-section__title">會員資訊</h2>

            <div class="ocreate__member-search">
              <label class="label">搜尋會員（姓名 / 手機 / Email）</label>
              <div class="ocreate__search-row">
                <input
                  v-model="memberKeyword"
                  class="input ocreate__search-input"
                  placeholder="輸入姓名、手機或 Email"
                  @keyup.enter="searchMembers"
                />
                <button type="button" class="btn btn--secondary btn--sm ocreate__search-btn" :disabled="memberSearching" @click="searchMembers">
                  {{ memberSearching ? '搜尋中…' : '搜尋' }}
                </button>
              </div>

              <!-- 搜尋結果下拉 -->
              <div v-if="memberResults.length > 0" class="ocreate__dropdown">
                <button
                  v-for="m in memberResults"
                  :key="m.memberId"
                  type="button"
                  class="ocreate__dropdown-item"
                  @click="selectMember(m)"
                >
                  <span class="ocreate__dropdown-name">{{ m.name }}</span>
                  <span class="ocreate__dropdown-sub">{{ m.mobile }} · {{ m.email }}</span>
                </button>
              </div>
            </div>

            <!-- 已選會員 -->
            <div v-if="selectedMember" class="ocreate__selected-member">
              <span class="ocreate__selected-icon">✓</span>
              <div class="ocreate__selected-info">
                <span class="ocreate__selected-name">{{ selectedMember.name }}</span>
                <span class="ocreate__selected-sub">{{ selectedMember.mobile }} · {{ selectedMember.email }}</span>
              </div>
              <button type="button" class="btn btn--ghost btn--sm" @click="clearMember">移除</button>
            </div>
            <p v-else class="ocreate__hint">未選取會員，訂單將建立為訪客訂單</p>
          </div>

          <!-- 收件資訊（含到貨時間偏好，合併原「收件設定」卡） -->
          <div class="form-card">
            <h2 class="form-section__title">收件資訊</h2>

            <div class="form-row">
              <div class="form-field">
                <label class="label" for="receiverName">收件人姓名 <span class="ocreate__req">*</span></label>
                <input id="receiverName" v-model="form.receiverName" class="input" required />
              </div>
              <div class="form-field">
                <label class="label" for="receiverMobile">收件人手機 <span class="ocreate__req">*</span></label>
                <input id="receiverMobile" v-model="form.receiverMobile" class="input" required />
              </div>
            </div>
            <div class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="receiverAddress">收件地址 <span class="ocreate__req">*</span></label>
                <input id="receiverAddress" v-model="form.receiverAddress" class="input" required />
              </div>
              <div class="form-field">
                <label class="label" for="recivertime">到貨時間偏好</label>
                <select id="recivertime" v-model="form.recivertime" class="select">
                  <option :value="0">不限</option>
                  <option :value="1">上午（09:00–12:00）</option>
                  <option :value="2">下午（13:00–18:00）</option>
                </select>
              </div>
            </div>
          </div>

          <!-- 訂購商品 -->
          <div class="form-card">
            <h2 class="form-section__title">訂購商品</h2>

            <!-- 商品搜尋 -->
            <div class="ocreate__member-search">
              <label class="label">新增商品</label>
              <div class="ocreate__search-row">
                <input
                  v-model="productKeyword"
                  class="input ocreate__search-input"
                  placeholder="輸入商品名稱後按 Enter 搜尋"
                  @keyup.enter="searchProducts"
                />
                <button type="button" class="btn btn--secondary btn--sm ocreate__search-btn" :disabled="productSearching" @click="searchProducts">
                  {{ productSearching ? '搜尋中…' : '搜尋' }}
                </button>
              </div>

              <!-- 商品搜尋結果 -->
              <div v-if="productResults.length > 0" class="ocreate__dropdown">
                  <button
                    v-for="p in productResults"
                    :key="p.productId"
                    type="button"
                    class="ocreate__dropdown-item"
                    @click="addProduct(p)"
                  >
                    <div class="ocreate__dropdown-row">
                      <img v-if="p.photo" :src="toBlobUrl(p.photo)" class="ocreate__dropdown-thumb" alt="" @error="($event.target as HTMLImageElement).style.display='none'" />
                      <div class="ocreate__dropdown-text">
                        <span class="ocreate__dropdown-name">{{ p.title }}</span>
                        <span class="ocreate__dropdown-sub">{{ p.productNum }} · NT$ {{ p.price.toLocaleString() }}</span>
                      </div>
                    </div>
                </button>
              </div>
            </div>

            <!-- 已加入的商品列表 -->
            <div v-if="orderItems.length > 0" class="ocreate__items">
              <div class="ocreate__items-header">
                <span class="ocreate__col-thumb"></span>
                <span class="ocreate__col-name">商品名稱</span>
                <span class="ocreate__col-price">單價</span>
                <span class="ocreate__col-qty">數量</span>
                <span class="ocreate__col-sub">小計</span>
                <span class="ocreate__col-action"></span>
              </div>
              <div v-for="(item, idx) in orderItems" :key="item.productId" class="ocreate__item-row">
                <span class="ocreate__col-thumb">
                  <img v-if="item.photo" :src="toBlobUrl(item.photo)" class="ocreate__item-thumb" alt="" @error="($event.target as HTMLImageElement).style.display='none'" />
                </span>
                <span class="ocreate__col-name">
                  <span class="ocreate__item-name">{{ item.productName }}</span>
                  <span class="ocreate__item-num">{{ item.productNum }}</span>
                </span>
                <span class="ocreate__col-price">NT$ {{ item.unitPrice.toLocaleString() }}</span>
                <span class="ocreate__col-qty">
                  <input
                    v-model.number="item.qty"
                    type="number"
                    min="1"
                    class="ocreate__qty-input"
                  />
                </span>
                <span class="ocreate__col-sub">NT$ {{ (item.unitPrice * item.qty).toLocaleString() }}</span>
                <span class="ocreate__col-action">
                  <button type="button" class="btn btn--ghost btn--sm" @click="removeItem(idx)">移除</button>
                </span>
              </div>
            </div>
            <p v-else class="ocreate__hint">尚未新增商品</p>
          </div>

        </div><!-- /.ocreate__main -->

        <!-- ── 右欄（aside）：設定 + 合計 + 提交 ─────────────── -->
        <div class="ocreate__aside">

          <!-- 付款方式 -->
          <div class="form-card">
            <h2 class="form-section__title">付款方式</h2>
            <div class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="payType">付款方式</label>
                <select id="payType" v-model="form.payType" class="select">
                  <option :value="1">信用卡線上刷卡</option>
                  <option :value="2">宅配貨到付款</option>
                  <option :value="3">ATM轉帳付款</option>
                  <option :value="5">現金支付</option>
                  <option :value="6">電匯</option>
                </select>
              </div>
            </div>
          </div>

          <!-- 發票設定 -->
          <div class="form-card">
            <h2 class="form-section__title">發票設定</h2>

            <div class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="invoiceType">發票類型</label>
                <select id="invoiceType" v-model="form.invoiceType" class="select">
                  <option :value="1">二聯式</option>
                  <option :value="2">愛心捐贈</option>
                  <option :value="3">三聯式（統編）</option>
                  <option :value="4">免開</option>
                </select>
              </div>
            </div>

            <!-- 三聯式：公司資訊 -->
            <div v-if="form.invoiceType === 3" class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="companytitle">公司抬頭</label>
                <input id="companytitle" v-model="form.companytitle" class="input" placeholder="公司名稱" />
              </div>
              <div class="form-field form-field--full">
                <label class="label" for="companynumber">統一編號</label>
                <input id="companynumber" v-model="form.companynumber" class="input" placeholder="8 碼統編" maxlength="8" />
              </div>
            </div>

            <!-- 愛心捐贈：捐贈碼 -->
            <div v-if="form.invoiceType === 2" class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="lovecode">愛心捐贈碼</label>
                <input id="lovecode" v-model="form.lovecode" class="input" placeholder="捐贈碼（選填，系統可自動填入）" />
              </div>
            </div>
          </div>

          <!-- 備註 -->
          <div class="form-card">
            <h2 class="form-section__title">備註</h2>
            <div class="form-row">
              <div class="form-field form-field--full">
                <label class="label" for="remark">訂購備註</label>
                <textarea id="remark" v-model="form.remark" class="textarea" rows="3" placeholder="訂購備註（選填）"></textarea>
              </div>
            </div>
          </div>

          <!-- 訂單合計 -->
          <div class="form-card">
            <h2 class="form-section__title">訂單合計</h2>

            <div class="ocreate__total-grid">
              <span class="ocreate__total-label">商品小計</span>
              <span class="ocreate__total-value">NT$ {{ itemSubtotal.toLocaleString() }}</span>

              <span class="ocreate__total-label">
                運費
                <span v-if="itemSubtotal >= 2000" class="ocreate__free-ship">（滿 2000 免運）</span>
              </span>
              <div class="ocreate__shipping-field">
                <input
                  v-if="itemSubtotal < 2000"
                  v-model.number="form.shippingFee"
                  type="number"
                  min="0"
                  class="ocreate__shipping-input"
                />
                <span v-else class="ocreate__total-value">NT$ 0</span>
              </div>

              <span class="ocreate__total-label">折扣</span>
              <div class="ocreate__shipping-field">
                <input
                  v-model.number="form.discount"
                  type="number"
                  min="0"
                  class="ocreate__shipping-input"
                  placeholder="0"
                />
              </div>

              <span class="ocreate__total-label ocreate__total-label--grand">總計</span>
              <span class="ocreate__total-value ocreate__total-value--grand">NT$ {{ grandTotal.toLocaleString() }}</span>
            </div>
          </div>

          <!-- 提交按鈕 -->
          <div class="ocreate__submit-row">
            <button type="button" class="btn btn--ghost" @click="router.push('/admin/orders')">取消</button>
            <button type="submit" class="btn btn--primary" :disabled="submitting">
              {{ submitting ? '建立中…' : '建立訂單' }}
            </button>
          </div>

        </div><!-- /.ocreate__aside -->

      </div><!-- /.ocreate__layout -->
    </form>
  </div>
</template>

<style scoped>
/* 容器：填滿可用寬度，由 AdminLayout <main class="p-6"> 控制邊距 */
.ocreate {
  width: 100%;
}

/* 兩欄式版型 */
.ocreate__layout {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.25rem;
  align-items: start;
}

@media (min-width: 1024px) {
  .ocreate__layout {
    grid-template-columns: 1fr 360px;
  }
  .ocreate__aside {
    position: sticky;
    top: 1.5rem;
  }
}

@media (min-width: 1280px) {
  .ocreate__layout {
    grid-template-columns: 1fr 400px;
  }
}

/* 右欄卡片稍微緊湊 */
.ocreate__aside .form-card {
  padding: 1rem;
}

.ocreate__aside .form-section__title {
  font-size: 0.875rem;
  margin-bottom: 0.75rem;
  padding-bottom: 0.4rem;
}

.ocreate__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.5rem;
}

.ocreate__title {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  margin: 0;
  font-size: 1.25rem;
}

/* Cards（與 doc 10 form-card 一致） */
.form-card {
  background: #fff;
  border: 1px solid var(--tf-color-border);
  border-radius: 6px;
  padding: 1.25rem;
  margin-bottom: 1.25rem;
}

.form-section__title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--tf-color-primary-dark);
  margin: 0 0 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--tf-color-border);
}

.form-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 0.75rem 1.25rem;
  margin-bottom: 0.75rem;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.form-field--full {
  grid-column: 1 / -1;
}

.label {
  font-size: 0.8rem;
  font-weight: 500;
  color: #374151;
}

.input,
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
.input:focus,
.select:focus,
.textarea:focus {
  outline: none;
  border-color: var(--tf-color-primary);
}
.textarea { resize: vertical; }

.ocreate__req {
  color: var(--tf-color-accent);
  margin-left: 0.1rem;
}

/* 會員搜尋容器（全寬，不受 form-row grid 切割） */
.ocreate__member-search {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  margin-bottom: 0.75rem;
}

/* 搜尋列：input + 按鈕橫排，永不換行 */
.ocreate__search-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: nowrap;
}

.ocreate__search-input {
  flex: 1 1 0;
  min-width: 0;    /* 允許 flex item 縮小至 0，防止 overflow 撐爆 */
}

.ocreate__search-btn {
  flex-shrink: 0;
  white-space: nowrap;
}

/* 搜尋結果下拉 */
.ocreate__dropdown {
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  background: #fff;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
  margin-top: 0.25rem;
  overflow: hidden;
  max-height: 200px;
  overflow-y: auto;
}

.ocreate__dropdown-item {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.1rem;
  width: 100%;
  padding: 0.6rem 0.75rem;
  background: none;
  border: none;
  cursor: pointer;
  text-align: left;
  border-bottom: 1px solid var(--tf-color-border);
  font-family: inherit;
  transition: background 0.1s;
}
.ocreate__dropdown-item:last-child { border-bottom: none; }
.ocreate__dropdown-item:hover { background: #f0fafa; }

.ocreate__dropdown-name { font-size: 0.875rem; color: #1f2937; }
.ocreate__dropdown-sub { font-size: 0.75rem; color: var(--tf-color-muted); }

/* 已選會員列 */
.ocreate__selected-member {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.6rem 0.75rem;
  background: #f0fafa;
  border: 1px solid var(--tf-color-primary);
  border-radius: 4px;
  margin-top: 0.5rem;
}

.ocreate__selected-icon {
  color: var(--tf-color-primary);
  font-weight: 700;
  font-size: 1rem;
}

.ocreate__selected-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.ocreate__selected-name { font-size: 0.9rem; font-weight: 500; color: #1f2937; }
.ocreate__selected-sub { font-size: 0.75rem; color: var(--tf-color-muted); }

/* 提示文字 */
.ocreate__hint {
  font-size: 0.82rem;
  color: var(--tf-color-muted);
  margin: 0.25rem 0 0;
}

/* 商品列表表格 */
.ocreate__items {
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  overflow: hidden;
}

.ocreate__items-header,
.ocreate__item-row {
  display: grid;
  grid-template-columns: 2.5rem 1fr 7rem 7rem 7rem 5rem;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
}

.ocreate__items-header {
  background: var(--tf-color-primary);
  color: #fff;
  font-size: 0.8rem;
  font-weight: 500;
}

.ocreate__item-row {
  border-top: 1px solid var(--tf-color-border);
  font-size: 0.875rem;
}

.ocreate__item-row:hover { background: #f8faf8; }

.ocreate__col-thumb { display: flex; align-items: center; justify-content: center; }
.ocreate__col-name { display: flex; flex-direction: column; gap: 0.15rem; }
.ocreate__col-price,
.ocreate__col-sub { white-space: nowrap; }
.ocreate__col-qty { }
.ocreate__col-action { display: flex; justify-content: flex-end; }

.ocreate__item-thumb {
  width: 2rem;
  height: 2rem;
  object-fit: cover;
  border-radius: 3px;
  border: 1px solid var(--tf-color-border);
}

.ocreate__item-name { font-size: 0.875rem; color: #1f2937; }
.ocreate__item-num { font-size: 0.75rem; color: var(--tf-color-muted); font-family: monospace; }

/* Dropdown row layout */
.ocreate__dropdown-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
}

.ocreate__dropdown-thumb {
  width: 2rem;
  height: 2rem;
  object-fit: cover;
  border-radius: 3px;
  flex-shrink: 0;
  border: 1px solid var(--tf-color-border);
}

.ocreate__dropdown-text {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  min-width: 0;
}

.ocreate__qty-input {
  width: 5rem;
  padding: 0.3rem 0.5rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem;
  font-family: inherit;
  text-align: center;
}

/* 訂單合計 */
.ocreate__total-grid {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 0.5rem 2rem;
  max-width: 360px;
  margin-left: auto;
}

.ocreate__total-label {
  font-size: 0.875rem;
  color: #374151;
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.ocreate__total-label--grand {
  font-weight: 600;
  font-size: 0.95rem;
  padding-top: 0.5rem;
  border-top: 2px solid var(--tf-color-primary);
  margin-top: 0.25rem;
}

.ocreate__total-value {
  font-size: 0.875rem;
  color: #1f2937;
  white-space: nowrap;
  text-align: right;
}

.ocreate__total-value--grand {
  font-weight: 700;
  font-size: 1.05rem;
  color: var(--tf-color-primary-dark);
  padding-top: 0.5rem;
  border-top: 2px solid var(--tf-color-primary);
  margin-top: 0.25rem;
}

.ocreate__free-ship {
  font-size: 0.72rem;
  color: var(--tf-color-primary);
  font-weight: 500;
}

.ocreate__shipping-field {
  display: flex;
  justify-content: flex-end;
}

.ocreate__shipping-input {
  width: 7rem;
  padding: 0.3rem 0.5rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem;
  font-family: inherit;
  text-align: right;
}

/* 提交列（在右欄底部，全寬堆疊） */
.ocreate__submit-row {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-top: 0.5rem;
  padding-bottom: 1rem;
}

.ocreate__submit-row .btn--primary {
  width: 100%;
  justify-content: center;
  padding: 0.6rem 1rem;
}

.ocreate__submit-row .btn--ghost {
  width: 100%;
  justify-content: center;
}

/* 提示訊息 */
.ocreate__alert {
  padding: 0.75rem 1rem;
  border-radius: 4px;
  margin-bottom: 1rem;
  font-size: 0.9rem;
}

.ocreate__alert--success {
  background: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}

.ocreate__alert--error {
  background: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
}

/* 全域 btn 覆寫（此視圖使用全域 .btn 類別） */
.btn {
  padding: 0.45rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.875rem;
  font-family: inherit;
  transition: opacity 0.15s, background 0.15s;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
}
.btn:disabled { opacity: 0.5; cursor: not-allowed; }

.btn--primary { background: var(--tf-color-primary); color: #fff; }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); }

.btn--secondary { background: #e9ecef; color: #495057; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }

.btn--ghost { background: transparent; border: 1px solid var(--tf-color-border); color: #495057; }
.btn--ghost:hover:not(:disabled) { background: #f1f3f5; }

.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
</style>

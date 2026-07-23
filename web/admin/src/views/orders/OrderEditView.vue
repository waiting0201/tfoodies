<script setup lang="ts">
import { ref, computed, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'
import { toBlobUrl } from '../../lib/blobUrl'

const route = useRoute()
const router = useRouter()
const code = route.params.code as string

// ── 型別 ──────────────────────────────────────────────────────────

interface EditItem {
  orderDetailId: string | null   // null = 新增
  productId: string
  productName: string
  productNum: string | null
  photo: string | null
  unitPrice: number
  qty: number
  discount: number | null  // 折數（如 8 = 八折）；空 = 不折扣
  subtotal: number         // 折後小計，可手動覆寫
  isGift: boolean
}

interface ProductResult {
  productId: string
  productNum: string
  title: string
  price: number
  photo: string | null
}

interface Warehouse { warehouseid: string; warehousetype: number; title: string }
interface Logistic { logisticid: string; title: string; isenable: boolean }
interface ZipArea { zipcodeId: number; area: string; zipcode: string }

// ── 參照資料（出貨倉 / 物流商 / 縣市鄉鎮）──────────────────────────
const warehouses = ref<Warehouse[]>([])
const logistics = ref<Logistic[]>([])
const cities = ref<string[]>([])
const areas = ref<ZipArea[]>([])

async function loadRefData() {
  try {
    const [w, l, c] = await Promise.all([
      apiFetch<Warehouse[]>('/admin/warehouses'),
      apiFetch<Logistic[]>('/admin/logistics'),
      apiFetch<{ cities: string[] }>('/admin/zipcodes/cities'),
    ])
    warehouses.value = w
    logistics.value = l.filter(x => x.isenable)
    cities.value = c.cities
  } catch {
    /* 載入失敗保持空清單，送出時驗證會擋下 */
  }
}

// 載入指定縣市的鄉鎮，並重置已選 zipcodeid
async function loadAreas(city: string) {
  areas.value = []
  form.receiverZipcodeId = 0
  if (!city) return
  try {
    const res = await apiFetch<{ areas: ZipArea[] }>(`/admin/zipcodes/areas?city=${encodeURIComponent(city)}`)
    areas.value = res.areas
  } catch {
    areas.value = []
  }
}

// 使用者手動切換縣市時觸發
function onCityChange() {
  loadAreas(form.receiverCity)
}

// ── 唯讀顯示資料 ──────────────────────────────────────────────────

const memberName = ref('')
const memberMobile = ref('')

// ── 表單資料 ──────────────────────────────────────────────────────

const form = reactive({
  warehouseId: '',    // 出貨倉（必填，對齊舊系統編輯頁）
  logisticId: '',     // 物流商（必填，對齊舊系統編輯頁）
  receiverName: '',
  receiverMobile: '',
  receiverCity: '',          // 收件縣市（級聯用，不直接送出）
  receiverZipcodeId: 0,      // 由鄉鎮選擇帶出（必填外鍵）
  receiverAddress: '',
  receiverTime: 0,
  payType: 1,
  payStatus: 0,
  payDate: '',
  deliverStatus: 0,
  deliverDate: '',
  trackingNumber: '',
  invoiceType: 1,
  invoiceCode: '',
  companyTitle: '',
  companyNumber: '',
  loveCode: '',
  freight: 0,
  discount: 0,
  note: '',
  remark: '',
})

const items = ref<EditItem[]>([])

// ── 載入 ──────────────────────────────────────────────────────────

const loading = ref(false)
const loadError = ref('')

function toDateInput(s?: string | null): string {
  if (!s) return ''
  const d = new Date(s)
  if (isNaN(d.getTime())) return ''
  return d.toISOString().substring(0, 10)
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const data = await apiFetch<any>(`/admin/orders/${code}`)
    memberName.value = data.memberName ?? ''
    memberMobile.value = data.memberMobile ?? ''
    Object.assign(form, {
      warehouseId:     data.warehouseId     ?? '',
      logisticId:      data.logisticId      ?? '',
      receiverName:    data.receiverName    ?? '',
      receiverMobile:  data.receiverMobile  ?? '',
      receiverCity:    data.receiverCity    ?? '',
      receiverAddress: data.receiverAddress ?? '',
      receiverTime:    data.receiverTime    ?? 0,
      payType:         data.payType         ?? 1,
      payStatus:       data.payStatus       ?? 0,
      payDate:         toDateInput(data.payDate),
      deliverStatus:   data.deliverStatus   ?? 0,
      deliverDate:     toDateInput(data.deliverDate),
      trackingNumber:  data.trackingNumber  ?? '',
      invoiceType:     data.invoiceType     ?? 1,
      invoiceCode:     data.invoiceCode     ?? '',
      companyTitle:    data.companyTitle    ?? '',
      companyNumber:   data.companyNumber   ?? '',
      loveCode:        data.loveCode        ?? '',
      freight:         data.shippingFee     ?? 0,
      discount:        data.discount        ?? 0,
      note:            data.note            ?? '',
      remark:          data.remark          ?? '',
    })
    items.value = (data.items ?? []).map((i: any) => ({
      orderDetailId: i.id ?? null,
      productId:     i.productId,
      productName:   i.productName,
      productNum:    i.productNum ?? null,
      photo:         i.photo ?? null,
      unitPrice:     i.unitPrice,
      qty:           i.qty,
      discount:      i.discount ?? null,
      subtotal:      i.subtotal ?? (i.unitPrice * i.qty),
      isGift:        i.isGift ?? false,
    }))
    // 載入該縣市鄉鎮後套用原本的 zipcodeid（loadAreas 會先歸零，故須在其後設定）
    if (form.receiverCity) {
      await loadAreas(form.receiverCity)
      if (data.receiverZipcodeId) form.receiverZipcodeId = data.receiverZipcodeId
    }
  } catch (e) {
    loadError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

// ── 商品搜尋 ──────────────────────────────────────────────────────

const productKeyword = ref('')
const productResults = ref<ProductResult[]>([])
const productSearching = ref(false)

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

// 輸入即時搜尋（autocomplete，300ms debounce）
let productDebounce: ReturnType<typeof setTimeout> | undefined
function onProductInput() {
  if (productDebounce) clearTimeout(productDebounce)
  if (!productKeyword.value.trim()) {
    productResults.value = []
    return
  }
  productDebounce = setTimeout(() => { searchProducts() }, 300)
}

function addProduct(p: ProductResult) {
  const existing = items.value.find(i => i.productId === p.productId)
  if (existing) {
    existing.qty++
    recalcItem(existing)
  } else {
    items.value.push({
      orderDetailId: null,
      productId:     p.productId,
      productName:   p.title,
      productNum:    p.productNum,
      photo:         p.photo,
      unitPrice:     p.price,
      qty:           1,
      discount:      null,
      subtotal:      p.price,
      isGift:        false,
    })
  }
  productResults.value = []
  productKeyword.value = ''
}

function removeItem(index: number) {
  items.value.splice(index, 1)
}

// 依單價與數量重算小計（改為直接編輯單價議價，不再用折數）。手動覆寫小計後仍可再改。
function recalcItem(item: EditItem) {
  item.subtotal = item.unitPrice * item.qty
}

// ── 合計計算 ──────────────────────────────────────────────────────

const itemSubtotal = computed(() =>
  items.value.reduce((acc, i) => acc + (i.subtotal || 0), 0)
)

const grandTotal = computed(() =>
  itemSubtotal.value + form.freight - form.discount
)

// ── 提交 ──────────────────────────────────────────────────────────

const saving = ref(false)
const saveError = ref('')

async function handleSubmit() {
  saveError.value = ''
  if (!form.warehouseId)            { saveError.value = '請選擇出貨倉';     return }
  if (!form.logisticId)             { saveError.value = '請選擇物流商';     return }
  if (!form.receiverName.trim())    { saveError.value = '請填寫收件人姓名'; return }
  if (!form.receiverMobile.trim())  { saveError.value = '請填寫收件人手機'; return }
  if (!form.receiverCity || !form.receiverZipcodeId) { saveError.value = '請選擇收件縣市與鄉鎮'; return }
  if (!form.receiverAddress.trim()) { saveError.value = '請填寫收件地址';   return }
  if (items.value.length === 0)     { saveError.value = '請至少保留一項商品'; return }

  const payload = {
    warehouseId:     form.warehouseId || null,
    logisticId:      form.logisticId || null,
    receiverName:    form.receiverName,
    receiverMobile:  form.receiverMobile,
    receiverZipcodeId: form.receiverZipcodeId,
    receiverAddress: form.receiverAddress,
    receiverTime:    form.receiverTime,
    payType:         form.payType,
    payStatus:       form.payStatus,
    payDate:         form.payDate || null,
    deliverStatus:   form.deliverStatus,
    deliverDate:     form.deliverDate || null,
    trackingNumber:  form.trackingNumber || null,
    invoiceType:     form.invoiceType,
    invoiceCode:     form.invoiceCode || null,
    companyTitle:    form.invoiceType === 3 ? form.companyTitle  || null : null,
    companyNumber:   form.invoiceType === 3 ? form.companyNumber || null : null,
    loveCode:        form.invoiceType === 2 ? form.loveCode      || null : null,
    freight:         form.freight,
    discount:        form.discount,
    // Orders.total 語意 = 純商品小計（不含運費/折扣），對齊舊系統。
    total:           itemSubtotal.value,
    note:            form.note    || null,
    remark:          form.remark  || null,
    items: items.value.map(i => ({
      orderDetailId: i.orderDetailId,
      productId:     i.productId,
      qty:           i.qty,
      price:         i.unitPrice,
      discount:      null, // 改為直接編輯單價議價，不再送折數
      subtotal:      i.subtotal,
      isGift:        i.isGift,
    })),
  }

  saving.value = true
  try {
    await apiFetch(`/admin/orders/${code}`, { method: 'PUT', body: JSON.stringify(payload) })
    router.push(`/admin/orders/${code}`)
  } catch (e) {
    saveError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '儲存失敗'
  } finally {
    saving.value = false
  }
}

onMounted(() => { loadRefData(); load() })
</script>

<template>
  <div class="oedit">

    <!-- 頁首 -->
    <div class="oedit__header">
      <h1 class="oedit__title">編輯訂單 <span class="oedit__code">{{ code }}</span></h1>
      <button class="btn btn--ghost btn--sm" @click="router.push(`/admin/orders/${code}`)">&larr; 返回訂單詳情</button>
    </div>

    <p v-if="loading" class="oedit__muted">載入中…</p>
    <p v-else-if="loadError" class="oedit__error">{{ loadError }}</p>

    <template v-else>
      <div v-if="saveError" class="oedit__alert oedit__alert--error">{{ saveError }}</div>

      <form @submit.prevent="handleSubmit">
        <div class="oedit__layout">

          <!-- ── 左欄：收件資訊 + 訂購商品 ───────────────────── -->
          <div class="oedit__main">

            <!-- 會員資訊（唯讀） -->
            <div class="form-card">
              <h2 class="form-section__title">會員資訊</h2>
              <div class="oedit__readonly-row">
                <span class="oedit__readonly-label">姓名</span>
                <span class="oedit__readonly-val">{{ memberName || '—' }}</span>
                <span class="oedit__readonly-label">手機</span>
                <span class="oedit__readonly-val">{{ memberMobile || '—' }}</span>
              </div>
            </div>

            <!-- 收件資訊 -->
            <div class="form-card">
              <h2 class="form-section__title">收件資訊</h2>
              <div class="form-row">
                <div class="form-field">
                  <label class="label" for="receiverName">收件人姓名 <span class="oedit__req">*</span></label>
                  <input id="receiverName" v-model="form.receiverName" class="input" required />
                </div>
                <div class="form-field">
                  <label class="label" for="receiverMobile">收件人手機 <span class="oedit__req">*</span></label>
                  <input id="receiverMobile" v-model="form.receiverMobile" class="input" required />
                </div>
              </div>
              <div class="form-row">
                <div class="form-field">
                  <label class="label" for="receiverCity">收件縣市 <span class="oedit__req">*</span></label>
                  <select id="receiverCity" v-model="form.receiverCity" class="select" @change="onCityChange">
                    <option value="">請選擇縣市</option>
                    <option v-for="c in cities" :key="c" :value="c">{{ c }}</option>
                  </select>
                </div>
                <div class="form-field">
                  <label class="label" for="receiverArea">鄉鎮市區 <span class="oedit__req">*</span></label>
                  <select id="receiverArea" v-model.number="form.receiverZipcodeId" class="select" :disabled="!form.receiverCity">
                    <option :value="0">請選擇鄉鎮市區</option>
                    <option v-for="a in areas" :key="a.zipcodeId" :value="a.zipcodeId">{{ a.area }}（{{ a.zipcode }}）</option>
                  </select>
                </div>
              </div>
              <div class="form-row">
                <div class="form-field form-field--full">
                  <label class="label" for="receiverAddress">收件地址 <span class="oedit__req">*</span></label>
                  <input id="receiverAddress" v-model="form.receiverAddress" class="input" required />
                </div>
                <div class="form-field">
                  <label class="label" for="receiverTime">到貨時間偏好</label>
                  <select id="receiverTime" v-model="form.receiverTime" class="select">
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

              <!-- 搜尋新增商品（autocomplete：輸入即時搜尋） -->
              <div class="oedit__product-search">
                <label class="label">新增商品</label>
                <div class="oedit__search-row">
                  <input
                    v-model="productKeyword"
                    class="input oedit__search-input"
                    placeholder="輸入商品名稱或編號，自動搜尋"
                    autocomplete="off"
                    @input="onProductInput"
                  />
                  <span v-if="productSearching" class="oedit__search-spinner">搜尋中…</span>
                </div>

                <div v-if="productResults.length > 0" class="oedit__dropdown">
                  <button
                    v-for="p in productResults"
                    :key="p.productId"
                    type="button"
                    class="oedit__dropdown-item"
                    @click="addProduct(p)"
                  >
                    <div class="oedit__dropdown-row">
                      <img v-if="p.photo" :src="toBlobUrl(p.photo)" class="oedit__dropdown-thumb" alt="" @error="($event.target as HTMLImageElement).style.display='none'" />
                      <div class="oedit__dropdown-text">
                        <span class="oedit__dropdown-name">{{ p.title }}</span>
                        <span class="oedit__dropdown-sub">{{ p.productNum }} · NT$ {{ p.price.toLocaleString() }}</span>
                      </div>
                    </div>
                  </button>
                </div>
              </div>

              <!-- 商品列表 -->
              <div v-if="items.length > 0" class="oedit__items">
                <div class="oedit__items-header">
                  <span class="oedit__col-thumb"></span>
                  <span class="oedit__col-name">商品名稱</span>
                  <span class="oedit__col-price">單價</span>
                  <span class="oedit__col-qty">數量</span>
                  <span class="oedit__col-sub">小計</span>
                  <span class="oedit__col-action"></span>
                </div>
                <div v-for="(item, idx) in items" :key="idx" class="oedit__item-row">
                  <span class="oedit__col-thumb">
                    <img v-if="item.photo" :src="toBlobUrl(item.photo)" class="oedit__item-thumb" alt="" @error="($event.target as HTMLImageElement).style.display='none'" />
                  </span>
                  <span class="oedit__col-name">
                    <span class="oedit__item-name">{{ item.productName }}</span>
                    <span class="oedit__item-num">{{ item.productNum }}</span>
                  </span>
                  <span class="oedit__col-price">
                    <input v-model.number="item.unitPrice" type="number" min="0" class="oedit__qty-input" @input="recalcItem(item)" />
                  </span>
                  <span class="oedit__col-qty">
                    <input v-model.number="item.qty" type="number" min="1" class="oedit__qty-input" @input="recalcItem(item)" />
                  </span>
                  <span class="oedit__col-sub">
                    <input v-model.number="item.subtotal" type="number" min="0" class="oedit__qty-input oedit__sub-input" />
                  </span>
                  <span class="oedit__col-action">
                    <button type="button" class="btn btn--ghost btn--sm" @click="removeItem(idx)">移除</button>
                  </span>
                </div>
              </div>
              <p v-else class="oedit__hint">尚未有商品</p>
            </div>

          </div><!-- /.oedit__main -->

          <!-- ── 右欄（aside）：狀態 + 發票 + 備註 + 合計 ──── -->
          <div class="oedit__aside">

            <!-- 付款資訊 -->
            <div class="form-card">
              <h2 class="form-section__title">付款資訊</h2>
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
                <div class="form-field form-field--full">
                  <label class="label" for="payStatus">付款狀態</label>
                  <select id="payStatus" v-model="form.payStatus" class="select">
                    <option :value="0">未付款</option>
                    <option :value="1">已付款</option>
                  </select>
                </div>
                <div v-if="form.payStatus === 1" class="form-field form-field--full">
                  <label class="label" for="payDate">付款日期</label>
                  <input id="payDate" v-model="form.payDate" type="date" class="input" />
                </div>
              </div>
            </div>

            <!-- 出貨資訊 -->
            <div class="form-card">
              <h2 class="form-section__title">出貨資訊</h2>
              <div class="form-row">
                <div class="form-field form-field--full">
                  <label class="label" for="warehouseId">出貨倉 <span class="oedit__req">*</span></label>
                  <select id="warehouseId" v-model="form.warehouseId" class="select">
                    <option value="">請選擇出貨倉</option>
                    <option v-for="w in warehouses" :key="w.warehouseid" :value="w.warehouseid">{{ w.title }}</option>
                  </select>
                </div>
                <div class="form-field form-field--full">
                  <label class="label" for="logisticId">物流商 <span class="oedit__req">*</span></label>
                  <select id="logisticId" v-model="form.logisticId" class="select">
                    <option value="">請選擇物流商</option>
                    <option v-for="l in logistics" :key="l.logisticid" :value="l.logisticid">{{ l.title }}</option>
                  </select>
                </div>
                <div class="form-field form-field--full">
                  <label class="label" for="deliverStatus">出貨狀態</label>
                  <select id="deliverStatus" v-model="form.deliverStatus" class="select">
                    <option :value="0">未出貨</option>
                    <option :value="4">待出貨</option>
                    <option :value="1">已出貨</option>
                    <option :value="3">已取消</option>
                  </select>
                </div>
                <div v-if="form.deliverStatus === 1" class="form-field form-field--full">
                  <label class="label" for="deliverDate">出貨日期</label>
                  <input id="deliverDate" v-model="form.deliverDate" type="date" class="input" />
                </div>
                <div class="form-field form-field--full">
                  <label class="label" for="trackingNumber">物流追蹤號</label>
                  <input id="trackingNumber" v-model="form.trackingNumber" class="input" placeholder="選填" />
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
                <div class="form-field form-field--full">
                  <label class="label" for="invoiceCode">發票號碼</label>
                  <input id="invoiceCode" v-model="form.invoiceCode" class="input" placeholder="系統自動產生，可人工覆蓋" />
                </div>
                <template v-if="form.invoiceType === 3">
                  <div class="form-field form-field--full">
                    <label class="label" for="companyTitle">公司抬頭</label>
                    <input id="companyTitle" v-model="form.companyTitle" class="input" />
                  </div>
                  <div class="form-field form-field--full">
                    <label class="label" for="companyNumber">統一編號</label>
                    <input id="companyNumber" v-model="form.companyNumber" class="input" maxlength="8" />
                  </div>
                </template>
                <div v-if="form.invoiceType === 2" class="form-field form-field--full">
                  <label class="label" for="loveCode">愛心捐贈碼</label>
                  <input id="loveCode" v-model="form.loveCode" class="input" />
                </div>
              </div>
            </div>

            <!-- 備註 -->
            <div class="form-card">
              <h2 class="form-section__title">備註</h2>
              <div class="form-row">
                <div class="form-field form-field--full">
                  <label class="label" for="remark">顧客備註</label>
                  <textarea id="remark" v-model="form.remark" class="textarea" rows="2" />
                </div>
                <div class="form-field form-field--full">
                  <label class="label" for="note">內部備注</label>
                  <textarea id="note" v-model="form.note" class="textarea" rows="2" />
                </div>
              </div>
            </div>

            <!-- 訂單合計 -->
            <div class="form-card">
              <h2 class="form-section__title">訂單合計</h2>
              <div class="oedit__total-grid">
                <span class="oedit__total-label">商品小計</span>
                <span class="oedit__total-value">NT$ {{ itemSubtotal.toLocaleString() }}</span>

                <span class="oedit__total-label">運費</span>
                <div class="oedit__shipping-field">
                  <input v-model.number="form.freight" type="number" min="0" class="oedit__shipping-input" />
                </div>

                <span class="oedit__total-label">折扣</span>
                <div class="oedit__shipping-field">
                  <input v-model.number="form.discount" type="number" min="0" class="oedit__shipping-input" placeholder="0" />
                </div>

                <span class="oedit__total-label oedit__total-label--grand">總計</span>
                <span class="oedit__total-value oedit__total-value--grand">NT$ {{ grandTotal.toLocaleString() }}</span>
              </div>
            </div>

            <!-- 操作按鈕 -->
            <div class="oedit__submit-row">
              <button type="button" class="btn btn--ghost" @click="router.push(`/admin/orders/${code}`)">取消</button>
              <button type="submit" class="btn btn--primary" :disabled="saving">
                {{ saving ? '儲存中…' : '儲存變更' }}
              </button>
            </div>

          </div><!-- /.oedit__aside -->

        </div><!-- /.oedit__layout -->
      </form>
    </template>

  </div>
</template>

<style scoped>
.oedit {
  width: 100%;
}

.oedit__layout {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.25rem;
  align-items: start;
}

@media (min-width: 1024px) {
  .oedit__layout {
    grid-template-columns: 1fr 360px;
  }
  .oedit__aside {
    position: sticky;
    top: 1.5rem;
  }
}

@media (min-width: 1280px) {
  .oedit__layout {
    grid-template-columns: 1fr 400px;
  }
}

.oedit__aside .form-card {
  padding: 1rem;
}

.oedit__aside .form-section__title {
  font-size: 0.875rem;
  margin-bottom: 0.75rem;
  padding-bottom: 0.4rem;
}

.oedit__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.5rem;
}

.oedit__title {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  margin: 0;
  font-size: 1.25rem;
}

.oedit__code {
  font-family: monospace;
  font-size: 1rem;
  color: var(--tf-color-primary);
}

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

.oedit__req {
  color: var(--tf-color-accent);
  margin-left: 0.1rem;
}

/* 唯讀欄位列 */
.oedit__readonly-row {
  display: grid;
  grid-template-columns: 4rem 1fr 4rem 1fr;
  gap: 0.4rem 0.75rem;
  align-items: center;
  font-size: 0.875rem;
}

.oedit__readonly-label {
  color: var(--tf-color-muted);
  font-size: 0.78rem;
}

.oedit__readonly-val {
  color: #1f2937;
}

/* 搜尋容器（全寬，脫離 form-row grid） */
.oedit__product-search {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  margin-bottom: 1rem;
}

/* 搜尋列：input + 按鈕永不換行 */
.oedit__search-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: nowrap;
}

.oedit__search-input {
  flex: 1 1 0;
  min-width: 0;
}

.oedit__search-btn {
  flex-shrink: 0;
  white-space: nowrap;
}

.oedit__search-spinner {
  flex-shrink: 0;
  white-space: nowrap;
  font-size: 0.8rem;
  color: var(--tf-color-muted);
  align-self: center;
}

/* 下拉 */
.oedit__dropdown {
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  background: #fff;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
  margin-top: 0.25rem;
  overflow: hidden;
  max-height: 200px;
  overflow-y: auto;
}

.oedit__dropdown-item {
  display: block;
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
.oedit__dropdown-item:last-child { border-bottom: none; }
.oedit__dropdown-item:hover { background: #f0fafa; }

.oedit__dropdown-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.oedit__dropdown-thumb {
  width: 2rem;
  height: 2rem;
  object-fit: cover;
  border-radius: 3px;
  flex-shrink: 0;
  border: 1px solid var(--tf-color-border);
}

.oedit__dropdown-text {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  min-width: 0;
}

.oedit__dropdown-name { font-size: 0.875rem; color: #1f2937; }
.oedit__dropdown-sub  { font-size: 0.75rem; color: var(--tf-color-muted); }

/* 商品列表 */
.oedit__items {
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  overflow: hidden;
}

.oedit__items-header,
.oedit__item-row {
  display: grid;
  grid-template-columns: 2.5rem 1fr 6rem 4.5rem 7rem 4rem;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
}

.oedit__items-header {
  background: var(--tf-color-primary);
  color: #fff;
  font-size: 0.8rem;
  font-weight: 500;
}

.oedit__item-row {
  border-top: 1px solid var(--tf-color-border);
  font-size: 0.875rem;
}
.oedit__item-row:hover { background: #f8faf8; }

.oedit__col-thumb { display: flex; align-items: center; justify-content: center; }
.oedit__col-name  { display: flex; flex-direction: column; gap: 0.15rem; }
.oedit__col-price { white-space: nowrap; }
.oedit__col-price .oedit__qty-input,
.oedit__col-qty .oedit__qty-input,
.oedit__col-sub .oedit__qty-input { width: 100%; }
.oedit__sub-input { text-align: right; }
.oedit__col-action { display: flex; justify-content: flex-end; }

.oedit__item-thumb {
  width: 2rem;
  height: 2rem;
  object-fit: cover;
  border-radius: 3px;
  border: 1px solid var(--tf-color-border);
}

.oedit__item-name { font-size: 0.875rem; color: #1f2937; }
.oedit__item-num  { font-size: 0.75rem; color: var(--tf-color-muted); font-family: monospace; }

.oedit__qty-input {
  width: 5rem;
  padding: 0.3rem 0.5rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem;
  font-family: inherit;
  text-align: center;
}

/* 合計 */
.oedit__total-grid {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 0.5rem 2rem;
  max-width: 360px;
  margin-left: auto;
}

.oedit__total-label {
  font-size: 0.875rem;
  color: #374151;
  display: flex;
  align-items: center;
}

.oedit__total-label--grand {
  font-weight: 600;
  font-size: 0.95rem;
  padding-top: 0.5rem;
  border-top: 2px solid var(--tf-color-primary);
  margin-top: 0.25rem;
}

.oedit__total-value {
  font-size: 0.875rem;
  color: #1f2937;
  white-space: nowrap;
  text-align: right;
}

.oedit__total-value--grand {
  font-weight: 700;
  font-size: 1.05rem;
  color: var(--tf-color-primary-dark);
  padding-top: 0.5rem;
  border-top: 2px solid var(--tf-color-primary);
  margin-top: 0.25rem;
}

.oedit__shipping-field {
  display: flex;
  justify-content: flex-end;
}

.oedit__shipping-input {
  width: 7rem;
  padding: 0.3rem 0.5rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem;
  font-family: inherit;
  text-align: right;
}

/* 提交列 */
.oedit__submit-row {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-top: 0.5rem;
  padding-bottom: 1rem;
}

.oedit__submit-row .btn--primary {
  width: 100%;
  justify-content: center;
  padding: 0.6rem 1rem;
}

.oedit__submit-row .btn--ghost {
  width: 100%;
  justify-content: center;
}

/* 提示 */
.oedit__alert {
  padding: 0.75rem 1rem;
  border-radius: 4px;
  margin-bottom: 1rem;
  font-size: 0.9rem;
}

.oedit__alert--error {
  background: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
}

.oedit__hint { font-size: 0.82rem; color: var(--tf-color-muted); margin: 0.25rem 0 0; }
.oedit__error { color: #dc3545; }
.oedit__muted { color: var(--tf-color-muted); }

/* ── Buttons（scoped 需各自定義） ──────────────────── */
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

<script setup lang="ts">
// 訂單明細：沿用會員中心的卡片設計語言（teal 主色、白卡、圓角、柔和陰影），
// 資料欄位參照舊系統 MemberMs/OrderDetail（收件人 / 訂單資訊 / 商品明細 / 匯款 / 備註）。
const config = useRuntimeConfig()
const memberAuth = useMemberAuthStore()
const route = useRoute()
const blobUrl = String(config.public.blobUrl)

const code = computed(() => String(route.params.code ?? ''))

useHead(() => ({ title: `訂單 ${code.value}` }))

if (!memberAuth.isAuthenticated) {
  await navigateTo('/Member/Login')
}

// API（OrderSummary）回傳 lines[]，狀態為列舉整數；此處對齊真實欄位並做標籤轉換。
interface OrderLine {
  productId: string
  productTitle: string
  productPhoto: string
  qty: number
  price: number
  subtotal: number
  capacity?: string
  isGift: number
}
interface OrderDetail {
  orderCode: string
  orderDate: string
  buyerName: string
  buyerMobile: string
  receiverName: string
  receiverMobile: string
  receiverAddress: string
  payType: number
  payStatus: number
  payDate?: string
  deliverStatus: number
  deliverDate?: string
  freight: number
  discount: number
  total: number
  lines: OrderLine[]
  atmCode?: string
  atmExpiry?: string
  remark?: string
}

// 對齊 Domain/Enums/Enums.cs。
const payTypeLabels: Record<number, string> = {
  1: '信用卡', 2: '貨到付款', 3: 'ATM 轉帳', 4: '免付款', 5: '現金', 6: '電匯', 7: '支票',
}
const payStatusLabels: Record<number, string> = {
  0: '未付款', 1: '已付款', 2: '退款', 3: '免付款', 4: '取消',
}
const deliverStatusLabels: Record<number, string> = {
  0: '未出貨', 1: '已出貨', 2: '退貨', 3: '取消', 4: '待出貨',
}

// 列舉值（對齊舊系統判斷未付款 + ATM 才顯示匯款資訊）。
const PAY_STATUS_UNPAID = 0
const PAY_TYPE_ATM = 3

const { data, pending, error } = await useFetch<OrderDetail>(
  () => `${config.public.apiBase}/member/orders/${code.value}`,
  {
    key: `member-order-${code.value}`,
    headers: computed<Record<string, string>>(() => {
      const h: Record<string, string> = {}
      if (memberAuth.accessToken) h.Authorization = `Bearer ${memberAuth.accessToken}`
      return h
    }),
    default: (): OrderDetail => ({
      orderCode: '',
      orderDate: '',
      buyerName: '',
      buyerMobile: '',
      receiverName: '',
      receiverMobile: '',
      receiverAddress: '',
      payType: 0,
      payStatus: 0,
      deliverStatus: 0,
      freight: 0,
      discount: 0,
      total: 0,
      lines: [],
    }),
  },
)

const nf = new Intl.NumberFormat('zh-TW')
const ntd = (n: number) => 'NT$ ' + nf.format(Math.trunc(n))
// 總計 = 商品總金額 + 運費 - 折扣（對齊舊系統）。
const grandTotal = computed(() => {
  const d = data.value
  return d ? d.total + d.freight - d.discount : 0
})
const showAtmInfo = computed(
  () => data.value?.payStatus === PAY_STATUS_UNPAID && data.value?.payType === PAY_TYPE_ATM,
)
</script>

<template>
  <main id="main">
    <section class="od-wrap">
      <div class="od-shell">
        <NuxtLink to="/Member/Center" class="od-back">← 返回我的訂單</NuxtLink>

        <div v-if="pending" class="od-state">載入中…</div>
        <div v-else-if="error" class="od-state err">無法載入訂單資料。</div>

        <template v-else-if="data?.orderCode">
          <!-- 標題列 -->
          <header class="od-head">
            <div class="od-head-main">
              <p class="od-eyebrow">訂單編號</p>
              <h1 class="od-code">{{ data.orderCode }}</h1>
              <p class="od-date">訂購日期 · {{ data.orderDate }}</p>
            </div>
          </header>

          <!-- 收件人 / 訂單資訊 -->
          <div class="od-grid">
            <section class="od-card">
              <h2 class="od-card-title">收件人資訊</h2>
              <dl class="od-dl">
                <div class="od-dl-row">
                  <dt>姓名</dt><dd>{{ data.receiverName || '—' }}</dd>
                </div>
                <div class="od-dl-row">
                  <dt>手機號碼</dt><dd>{{ data.receiverMobile || '—' }}</dd>
                </div>
                <div class="od-dl-row">
                  <dt>收件地址</dt><dd>{{ data.receiverAddress || '—' }}</dd>
                </div>
              </dl>
            </section>

            <section class="od-card">
              <h2 class="od-card-title">訂單資訊</h2>
              <dl class="od-dl">
                <div class="od-dl-row">
                  <dt>付款方式</dt><dd>{{ payTypeLabels[data.payType] ?? '—' }}</dd>
                </div>
                <div class="od-dl-row">
                  <dt>付款狀態</dt><dd>{{ payStatusLabels[data.payStatus] ?? '—' }}</dd>
                </div>
                <div class="od-dl-row">
                  <dt>付款日期</dt><dd>{{ data.payDate || '—' }}</dd>
                </div>
                <div class="od-dl-row">
                  <dt>出貨狀態</dt><dd>{{ deliverStatusLabels[data.deliverStatus] ?? '—' }}</dd>
                </div>
                <div class="od-dl-row">
                  <dt>出貨日期</dt><dd>{{ data.deliverDate || '—' }}</dd>
                </div>
              </dl>
            </section>
          </div>

          <!-- 商品明細 -->
          <section class="od-card">
            <h2 class="od-card-title">商品明細</h2>
            <div class="od-table-scroll">
              <table class="od-table">
                <thead>
                  <tr>
                    <th class="od-col-prod">商品</th>
                    <th class="ar">單價</th>
                    <th class="ac">數量</th>
                    <th class="ar">小計</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="item in data.lines" :key="item.productId">
                    <td>
                      <div class="od-prod">
                        <div class="od-thumb">
                          <img v-if="item.productPhoto" :src="blobUrl + item.productPhoto" :alt="item.productTitle">
                        </div>
                        <div class="od-prod-meta">
                          <span class="od-prod-name">{{ item.productTitle }}</span>
                          <span v-if="item.capacity" class="od-prod-cap">{{ item.capacity }}</span>
                          <span v-if="item.isGift === 1" class="od-gift">贈品</span>
                        </div>
                      </div>
                    </td>
                    <td class="ar">{{ ntd(item.price) }}</td>
                    <td class="ac">{{ item.qty }}</td>
                    <td class="ar strong">{{ ntd(item.subtotal) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- 金額彙總 -->
            <div class="od-totals">
              <div class="od-total-row">
                <span>商品總金額</span><span>{{ ntd(data.total) }}</span>
              </div>
              <div v-if="data.discount > 0" class="od-total-row">
                <span>額外折扣</span><span class="minus">- {{ ntd(data.discount) }}</span>
              </div>
              <div class="od-total-row">
                <span>運費</span><span>{{ ntd(data.freight) }}</span>
              </div>
              <div class="od-total-row grand">
                <span>總計</span><span>{{ ntd(grandTotal) }}</span>
              </div>
            </div>
          </section>

          <!-- 匯款資訊（未付款 + ATM 轉帳）-->
          <section v-if="showAtmInfo" class="od-card od-atm">
            <h2 class="od-card-title">匯款資訊</h2>
            <dl class="od-dl">
              <div class="od-dl-row">
                <dt>轉帳銀行</dt><dd>007 第一銀行</dd>
              </div>
              <div class="od-dl-row">
                <dt>匯款帳號</dt><dd class="mono">{{ data.atmCode || '—' }}</dd>
              </div>
              <div class="od-dl-row">
                <dt>繳款截止日</dt><dd class="warn-text">{{ data.atmExpiry || '—' }}</dd>
              </div>
            </dl>
          </section>

          <!-- 備註 -->
          <section v-if="data.remark" class="od-card">
            <h2 class="od-card-title">備註</h2>
            <p class="od-remark">{{ data.remark }}</p>
          </section>
        </template>
      </div>
    </section>
  </main>
</template>

<style scoped>
.od-wrap {
  display: flex;
  justify-content: center;
  padding: 3rem 1.25rem 5rem;
  background: linear-gradient(160deg, #f4fbfb 0%, #ffffff 55%);
}

.od-shell {
  width: 100%;
  max-width: 880px;
}

.od-back {
  display: inline-block;
  margin-bottom: 1.25rem;
  font-size: 0.85rem;
  letter-spacing: 0.04em;
  color: #9b9b9b;
  text-decoration: none;
  transition: color 0.18s;
}

.od-back:hover {
  color: #26b7bc;
}

.od-state {
  padding: 4rem 0;
  text-align: center;
  color: #9b9b9b;
}

.od-state.err {
  color: #d0021b;
}

/* 標題列 */
.od-head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
  margin-bottom: 1.75rem;
  padding-bottom: 1.4rem;
  border-bottom: 1px solid #e7f1f1;
}

.od-eyebrow {
  margin: 0 0 0.3rem;
  font-size: 0.72rem;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: #a9c2c2;
}

.od-code {
  margin: 0;
  font-size: 1.6rem;
  letter-spacing: 0.04em;
  color: #156467;
}

.od-date {
  margin: 0.45rem 0 0;
  font-size: 0.85rem;
  color: #9b9b9b;
}

/* 資訊卡 */
.od-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
  margin-bottom: 1.25rem;
}

.od-card {
  background: #fff;
  border: 1px solid #eef4f4;
  border-radius: 14px;
  padding: 1.5rem 1.6rem;
  box-shadow: 0 14px 40px -28px rgba(21, 100, 103, 0.45);
}

.od-card + .od-card,
.od-grid + .od-card {
  margin-top: 0;
}

.od-shell > .od-card {
  margin-bottom: 1.25rem;
}

.od-card-title {
  margin: 0 0 1.1rem;
  font-size: 0.95rem;
  letter-spacing: 0.05em;
  color: #156467;
  padding-bottom: 0.7rem;
  border-bottom: 1px solid #f1f6f6;
}

/* 定義列表 */
.od-dl {
  margin: 0;
}

.od-dl-row {
  display: grid;
  grid-template-columns: 5.5rem 1fr;
  gap: 0.75rem;
  padding: 0.5rem 0;
  font-size: 0.9rem;
}

.od-dl-row + .od-dl-row {
  border-top: 1px solid #f6f9f9;
}

.od-dl-row dt {
  color: #9fb3b3;
  letter-spacing: 0.03em;
}

.od-dl-row dd {
  margin: 0;
  color: #3e4d4d;
  word-break: break-all;
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  letter-spacing: 0.04em;
}

.warn-text {
  color: #c47d22 !important;
  font-weight: 600;
}

/* 商品表 */
.od-table-scroll {
  overflow-x: auto;
}

.od-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.od-table thead th {
  padding: 0.32rem 0.75rem;
  height: 1.7rem;
  font-weight: 600;
  font-size: 0.78rem;
  letter-spacing: 0.05em;
  color: #fff;
  background: #26b7bc;
  white-space: nowrap;
  text-align: left;
  vertical-align: middle;
}

.od-table thead th:first-child {
  border-top-left-radius: 8px;
  border-bottom-left-radius: 8px;
}

.od-table thead th:last-child {
  border-top-right-radius: 8px;
  border-bottom-right-radius: 8px;
}

.od-table tbody td {
  padding: 0.85rem 0.75rem;
  border-bottom: 1px solid #f4f8f8;
  color: #3e4d4d;
  vertical-align: middle;
}

.od-table .ar { text-align: right; }
.od-table .ac { text-align: center; }
.od-table .strong { font-weight: 600; color: #156467; }
.od-col-prod { width: 60%; }

.od-prod {
  display: flex;
  align-items: center;
  gap: 0.85rem;
}

.od-thumb {
  flex: 0 0 auto;
  width: 52px;
  height: 52px;
  border-radius: 10px;
  overflow: hidden;
  background: #f3f8f8;
  border: 1px solid #eef4f4;
}

.od-thumb img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.od-prod-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem 0.5rem;
}

.od-prod-name {
  font-weight: 500;
  color: #2f3d3d;
}

.od-prod-cap {
  font-size: 0.8rem;
  color: #9fb3b3;
}

.od-gift {
  font-size: 0.72rem;
  font-weight: 600;
  color: #c47d22;
  background: #fdf1e0;
  padding: 0.1rem 0.5rem;
  border-radius: 999px;
}

/* 金額彙總 */
.od-totals {
  margin: 1.4rem 0 0 auto;
  max-width: 300px;
}

.od-total-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.5rem 0;
  font-size: 0.9rem;
  color: #6b7a7a;
}

.od-total-row .minus {
  color: #1d8e76;
}

.od-total-row.grand {
  margin-top: 0.4rem;
  padding-top: 0.85rem;
  border-top: 1px solid #e7f1f1;
  font-size: 1.05rem;
  font-weight: 700;
  color: #156467;
}

/* 匯款 */
.od-atm {
  border-color: #f3e2c6;
  background: linear-gradient(180deg, #fffdf8 0%, #ffffff 100%);
}

.od-remark {
  margin: 0;
  font-size: 0.9rem;
  line-height: 1.7;
  color: #5a6868;
  white-space: pre-wrap;
}

/* 響應式 */
@media (max-width: 720px) {
  .od-wrap {
    padding: 2rem 0.9rem 3.5rem;
  }

  .od-grid {
    grid-template-columns: 1fr;
  }

  .od-code {
    font-size: 1.35rem;
  }
}
</style>

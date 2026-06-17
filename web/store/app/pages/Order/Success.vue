<script setup lang="ts">
const route = useRoute()
const orderCode = computed(() => String(route.query.code ?? ''))
const atmAccount = computed(() => String(route.query.atm ?? ''))
const atmExpiry = computed(() => String(route.query.atmExpiry ?? ''))
const isAtm = computed(() => !!atmAccount.value)

// 信用卡刷卡導回會帶 paid=1|0；非信用卡（一般下單成功）則無此參數。
const paidParam = computed(() => String(route.query.paid ?? ''))
const isCardReturn = computed(() => paidParam.value !== '')
const cardFailed = computed(() => isCardReturn.value && paidParam.value !== '1')

const heading = computed(() => (cardFailed.value ? '付款未完成' : '訂單成立'))
const lead = computed(() =>
  cardFailed.value
    ? '您的信用卡授權未成功，訂單已為您保留，您可至會員中心重新付款。'
    : isCardReturn.value
      ? '付款完成，感謝您的購買！我們已收到您的訂單。'
      : '感謝您的購買，我們已收到您的訂單。',
)

useHead({ title: heading })

// 漏斗第五關：購買成功。讀取結帳時暫存的訂單摘要（含金額/品項，廣告優化才準確）。
// 信用卡付款失敗(cardFailed)時不計入營收，但仍清掉暫存避免殘留。
onMounted(() => {
  const pending = takePendingPurchase()
  if (!pending || cardFailed.value) return

  // email/phone 僅供 server 端 CAPI 比對，絕不可進 dataLayer/GA4。
  const { email, phone, ...ecommerce } = pending

  // 瀏覽器端 Pixel/GA4（經 GTM）：帶 event_id(=訂單編號) 供 Meta 與 CAPI 去重。
  track('purchase', { event_id: pending.transaction_id, ecommerce })

  // server 端 Meta CAPI 補送（避免被擋廣告漏單；失敗不影響頁面）。
  $fetch('/api/meta/capi-purchase', {
    method: 'POST',
    body: {
      eventId: pending.transaction_id,
      value: pending.value,
      currency: pending.currency,
      contents: pending.items.map((i) => ({ id: i.item_id, quantity: i.quantity, item_price: i.price })),
      email,
      phone,
      sourceUrl: window.location.href,
    },
  }).catch(() => {})
})
</script>

<template>
  <main id="main">
    <section class="tallsection clr">
      <div class="restrict-wide allpadding">
        <div class="order-success" :class="{ 'order-success--failed': cardFailed }">
          <div class="order-success__icon" aria-hidden="true">
            <svg v-if="!cardFailed" viewBox="0 0 52 52" width="44" height="44">
              <path
                fill="none"
                stroke="#fff"
                stroke-width="4"
                stroke-linecap="round"
                stroke-linejoin="round"
                d="M14 27l8 8 16-18"
              />
            </svg>
            <svg v-else viewBox="0 0 52 52" width="44" height="44">
              <path
                fill="none"
                stroke="#fff"
                stroke-width="4"
                stroke-linecap="round"
                stroke-linejoin="round"
                d="M17 17l18 18M35 17L17 35"
              />
            </svg>
          </div>

          <h1 class="order-success__title">{{ heading }}</h1>
          <p class="order-success__lead">{{ lead }}</p>

          <div v-if="orderCode" class="order-success__code">
            <span class="order-success__code-label">訂單編號</span>
            <span class="order-success__code-value">{{ orderCode }}</span>
          </div>

          <div v-if="isAtm" class="order-success__atm">
            <h2 class="order-success__atm-title">ATM 轉帳資訊</h2>
            <dl class="order-success__atm-list">
              <div class="order-success__atm-row">
                <dt>虛擬帳號</dt>
                <dd>{{ atmAccount }}</dd>
              </div>
              <div v-if="atmExpiry" class="order-success__atm-row">
                <dt>繳費期限</dt>
                <dd>{{ atmExpiry }}</dd>
              </div>
            </dl>
            <p class="order-success__atm-note">
              請於期限前完成匯款，系統確認入帳後將為您安排出貨。
            </p>
          </div>

          <div class="order-success__actions">
            <a href="/Member/Orders" class="btn basic">查看我的訂單</a>
            <a href="/Products" class="outline-btn solidhover">繼續購物</a>
          </div>
        </div>
      </div>
    </section>
  </main>
</template>

<style scoped>
.order-success {
  max-width: 560px;
  margin: 0 auto;
  padding: 3em 2em;
  text-align: center;
  background: #fff;
  border: 1px solid #ececec;
  border-radius: 10px;
  box-shadow: 0 6px 28px rgba(38, 183, 188, 0.08);
}

.order-success__icon {
  width: 76px;
  height: 76px;
  margin: 0 auto 1.4em;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: #26b7bc;
  box-shadow: 0 6px 18px rgba(38, 183, 188, 0.3);
}

.order-success--failed .order-success__icon {
  background: #e0584f;
  box-shadow: 0 6px 18px rgba(224, 88, 79, 0.3);
}

.order-success__title {
  margin: 0;
  font-size: 1.9em;
  color: #2f3a3a;
  letter-spacing: 0.06em;
}

.order-success__lead {
  margin: 0.6em 0 0;
  color: #8a9292;
  font-size: 1em;
  line-height: 1.7;
}

.order-success__code {
  margin: 1.8em auto 0;
  padding: 0.9em 1.5em;
  display: inline-flex;
  align-items: center;
  gap: 0.9em;
  background: #f6fbfb;
  border: 1px solid #d8eeef;
  border-radius: 8px;
}

.order-success__code-label {
  font-size: 0.85em;
  color: #8a9292;
  letter-spacing: 0.08em;
}

.order-success__code-value {
  font-size: 1.15em;
  font-weight: 700;
  color: #1d8e92;
  letter-spacing: 0.04em;
}

.order-success__atm {
  margin-top: 2em;
  padding: 1.5em;
  text-align: left;
  background: #fafbfb;
  border: 1px solid #ececec;
  border-radius: 8px;
}

.order-success__atm-title {
  margin: 0 0 1em;
  font-size: 1.05em;
  color: #2f3a3a;
  padding-bottom: 0.7em;
  border-bottom: 1px solid #ececec;
}

.order-success__atm-list {
  margin: 0;
}

.order-success__atm-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 1em;
  padding: 0.5em 0;
}

.order-success__atm-row dt {
  color: #8a9292;
  font-size: 0.92em;
  flex: 0 0 auto;
}

.order-success__atm-row dd {
  margin: 0;
  font-weight: 600;
  color: #2f3a3a;
  letter-spacing: 0.03em;
  text-align: right;
}

.order-success__atm-note {
  margin: 1em 0 0;
  font-size: 0.85em;
  color: #aab1b1;
  line-height: 1.6;
}

.order-success__actions {
  margin-top: 2.2em;
  display: flex;
  gap: 0.8em;
  justify-content: center;
  flex-wrap: wrap;
}

.order-success__actions .btn,
.order-success__actions .outline-btn {
  border-radius: 6px;
}

@media (max-width: 600px) {
  .order-success {
    padding: 2.4em 1.3em;
  }

  .order-success__actions .btn,
  .order-success__actions .outline-btn {
    flex: 1 1 100%;
    text-align: center;
  }
}
</style>

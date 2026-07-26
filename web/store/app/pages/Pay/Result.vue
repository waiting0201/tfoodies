<script setup lang="ts">
// 收款連結 — 付款結果頁。銀行授權後由 API 的 return-paylink 端點 302 導回，
// query 為 ?code=PL...&paid=1|0。視覺沿用 Order/Success.vue（同為流程終點頁）。
definePageMeta({ layout: 'pay' })

const route = useRoute()
const code = computed(() => String(route.query.code ?? ''))
const failed = computed(() => String(route.query.paid ?? '') !== '1')

const heading = computed(() => (failed.value ? '付款未完成' : '付款完成'))
useHead({
  title: heading,
  meta: [{ name: 'robots', content: 'noindex,nofollow' }],
})

// 本頁 query 沒有 token，無法直接組出重試連結；history.back() 會退回銀行刷卡頁
// （多半已失效，且會觸發「表單重新送出」警告）。改讀付款頁在 mounted 時寄存的 token。
const retryUrl = ref('')
onMounted(() => {
  if (!failed.value) return
  try {
    const t = sessionStorage.getItem('pay_link_token')
    if (t) retryUrl.value = `/Pay/${t}`
  } catch { /* 無痕模式等情境：不給按了會出錯的按鈕 */ }
})

// 刻意不呼叫 takePendingPurchase() / track('purchase')：收款連結不是電商交易，
// 計入會污染 GA4/Meta 的營收與轉換數據。
</script>

<template>
  <div class="order-success" :class="{ 'order-success--failed': failed }">
    <div class="order-success__icon" aria-hidden="true">
      <svg v-if="!failed" viewBox="0 0 52 52" width="44" height="44">
        <path fill="none" stroke="#fff" stroke-width="4" stroke-linecap="round" stroke-linejoin="round" d="M14 27l8 8 16-18" />
      </svg>
      <svg v-else viewBox="0 0 52 52" width="44" height="44">
        <path fill="none" stroke="#fff" stroke-width="4" stroke-linecap="round" stroke-linejoin="round" d="M17 17l18 18M35 17L17 35" />
      </svg>
    </div>

    <h1 class="order-success__title">{{ heading }}</h1>
    <p class="order-success__lead">
      {{ failed ? '您的信用卡授權未成功，款項尚未完成。' : '已收到您的款項，感謝您的付款。' }}
    </p>

    <div v-if="code" class="order-success__code">
      <span class="order-success__code-label">收款單號</span>
      <span class="order-success__code-value">{{ code }}</span>
    </div>

    <div v-if="failed" class="order-success__actions">
      <a v-if="retryUrl" :href="retryUrl" class="btn basic">返回重新付款</a>
      <p v-else class="order-success__contact">請聯繫客服協助重新取得付款連結：service@tfoodies.com</p>
    </div>
    <p v-else class="order-success__contact">您可以關閉此頁面。</p>
  </div>
</template>

<style scoped>
/* 沿用 Order/Success.vue 的視覺骨架，讓兩個「流程終點頁」是同一套語言 */
.order-success {
  max-width: 560px; margin: 0 auto; padding: 3em 2em; text-align: center;
  background: #fff; border: 1px solid #ececec; border-radius: 10px;
  box-shadow: 0 6px 28px rgba(38, 183, 188, 0.08);
}

.order-success__icon {
  width: 76px; height: 76px; margin: 0 auto 1.4em;
  display: flex; align-items: center; justify-content: center;
  border-radius: 50%; background: #26b7bc; box-shadow: 0 6px 18px rgba(38, 183, 188, 0.3);
}
.order-success--failed .order-success__icon { background: #e0584f; box-shadow: 0 6px 18px rgba(224, 88, 79, 0.3); }

.order-success__title { margin: 0; font-size: 1.9em; color: #2f3a3a; letter-spacing: 0.06em; }
.order-success__lead { margin: 0.6em 0 0; color: #8a9292; font-size: 1em; line-height: 1.7; }

.order-success__code {
  margin: 1.8em auto 0; padding: 0.9em 1.5em;
  display: inline-flex; align-items: center; gap: 0.9em;
  background: #f6fbfb; border: 1px solid #d8eeef; border-radius: 8px;
}
.order-success__code-label { font-size: 0.85em; color: #8a9292; letter-spacing: 0.08em; }
.order-success__code-value { font-size: 1.15em; font-weight: 700; color: #1d8e92; letter-spacing: 0.04em; }

.order-success__actions { margin-top: 2.2em; display: flex; gap: 0.8em; justify-content: center; flex-wrap: wrap; }
.order-success__actions .btn { border-radius: 6px; }
.order-success__contact { margin: 1.8em 0 0; font-size: 0.85em; color: #aab1b1; line-height: 1.6; }

@media (max-width: 600px) {
  .order-success { padding: 2.4em 1.3em; }
  .order-success__actions .btn { flex: 1 1 100%; text-align: center; }
}
</style>

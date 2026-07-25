<script setup lang="ts">
// 刷卡收款連結 — 客人付款頁。後台人員產生連結後透過 LINE/email 傳給客人，
// 客人直接開啟（免登入、無購物車脈絡）：確認金額 → 填收件資料 → 跳轉銀行刷卡頁。
definePageMeta({ layout: 'pay' })

interface PayLink { code: string; title: string; amount: number; status: number; isExpired: boolean }

const route = useRoute()
const config = useRuntimeConfig()
const token = computed(() => String(route.params.token ?? ''))

useHead({
  title: '線上刷卡付款',
  meta: [{ name: 'robots', content: 'noindex,nofollow' }],
})

const link = ref<PayLink | null>(null)
const loading = ref(true)
const invalid = ref(false)     // 已逾期 / 已作廢 / 不存在，對客人是同一件事

// 資料在 client 端取，避免 token 出現在 SSR server log。
onMounted(async () => {
  // Result 頁的 query 只有 code 沒有 token，付款失敗要能導回本頁重試，故先寄存 token。
  try { sessionStorage.setItem('pay_link_token', token.value) } catch { /* 無痕模式等情境忽略 */ }

  try {
    const res = await $fetch<PayLink>(`${config.public.apiBase}/store/paylinks/${token.value}`)
    if (res.status === 2 || res.isExpired) invalid.value = true
    else link.value = res
  } catch {
    invalid.value = true
  } finally {
    loading.value = false
  }
  await loadCities()
})

const isPaid = computed(() => link.value?.status === 1)
const canPay = computed(() => !!link.value && link.value.status === 0 && !link.value.isExpired)

const ntd = (n: number) => 'NT$ ' + new Intl.NumberFormat('zh-TW').format(Math.trunc(n))

// ── 表單 ──────────────────────────────────────────────────────────────────────

const { cities, loadCities, loadAreas } = useZipcodes()
const areas = ref<{ zipcodeId: number; area: string }[]>([])

const form = reactive({ name: '', mobile: '', city: '', zipcodeId: null as number | null, address: '' })

watch(() => form.city, async (city) => {
  form.zipcodeId = null
  areas.value = await loadAreas(city)
})

const submitting = ref(false)
const redirecting = ref(false)
const submitError = ref('')

function validate(): string | null {
  if (!form.name.trim()) return '請填寫姓名。'
  if (!/^09\d{8}$/.test(form.mobile.trim())) return '請輸入正確的手機格式（09 開頭共 10 碼）。'
  if (!form.zipcodeId) return '請選擇縣市與鄉鎮市區。'
  if (!form.address.trim()) return '請填寫詳細地址。'
  return null
}

async function submit() {
  submitError.value = ''
  const err = validate()
  if (err) { submitError.value = err; return }

  submitting.value = true
  try {
    const init = await $fetch<{ actionUrl: string; fields: Record<string, string> }>(
      `${config.public.apiBase}/store/paylinks/${token.value}/checkout`,
      {
        method: 'POST',
        body: {
          name: form.name.trim(),
          mobile: form.mobile.trim(),
          zipcodeId: form.zipcodeId,
          address: form.address.trim(),
          returnOrigin: window.location.origin,
        },
      },
    )

    // 整頁跳轉到銀行刷卡頁。覆蓋層在 submit() 前就打開，擋住跳轉空窗期的重複點擊。
    redirecting.value = true
    const f = document.createElement('form')
    f.method = 'post'
    f.action = init.actionUrl
    f.acceptCharset = 'UTF-8'
    for (const [k, v] of Object.entries(init.fields)) {
      const input = document.createElement('input')
      input.type = 'hidden'
      input.name = k
      input.value = v
      f.appendChild(input)
    }
    document.body.appendChild(f)
    f.submit()
    // 不重置 submitting：頁面即將離開，重置只會讓按鈕在跳轉前一瞬間又可按。
  } catch (e: any) {
    submitError.value = e?.data?.error?.message ?? '無法進入刷卡頁，請稍後再試或聯繫客服。'
    submitting.value = false
  }
}
</script>

<template>
  <!-- 載入中：用與正式內容同尺寸的卡片殼，避免內容彈出時版面跳動 -->
  <div v-if="loading" class="pay-state pay-state--loading">
    <div class="pay-state__icon" aria-hidden="true" />
    <p class="pay-state__lead">正在確認收款資訊…</p>
  </div>

  <!-- 已失效：逾期／作廢／不存在合併為同一句，不暴露內部狀態機，也不再顯示金額 -->
  <div v-else-if="invalid" class="pay-state pay-state--invalid">
    <div class="pay-state__icon" aria-hidden="true">
      <svg viewBox="0 0 24 24" width="34" height="34" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round">
        <path d="M9.5 14.5 6.8 17.2a3.8 3.8 0 0 1-5.4-5.4l2.7-2.7" />
        <path d="M14.5 9.5l2.7-2.7a3.8 3.8 0 0 1 5.4 5.4l-2.7 2.7" />
        <path d="M8 16 16 8" opacity=".55" />
      </svg>
    </div>
    <h1 class="pay-state__title">此連結已失效</h1>
    <p class="pay-state__lead">可能是連結已過期或款項已取消，請聯繫對方重新提供付款連結，或洽客服協助。</p>
    <p class="pay-state__contact">客服信箱 service@tfoodies.com</p>
  </div>

  <!-- 已付款：保留收據卡供核對，但不顯示表單與任何 CTA -->
  <div v-else-if="isPaid" class="pay-state pay-state--paid">
    <div class="pay-state__icon" aria-hidden="true">
      <svg viewBox="0 0 52 52" width="40" height="40">
        <path fill="none" stroke="#fff" stroke-width="4" stroke-linecap="round" stroke-linejoin="round" d="M14 27l8 8 16-18" />
      </svg>
    </div>
    <h1 class="pay-state__title">本筆款項已完成付款</h1>
    <p class="pay-state__lead">感謝您的付款，我們已收到這筆款項。</p>

    <div class="pay-bill pay-bill--inline">
      <span class="pay-bill__eyebrow">收款項目</span>
      <p class="pay-bill__title">{{ link!.title }}</p>
      <div class="pay-bill__divider" />
      <div class="pay-bill__amount">
        <span class="pay-bill__amount-label">付款金額</span>
        <span class="pay-bill__amount-value"><span class="cur">NT$</span><span class="num">{{ new Intl.NumberFormat('zh-TW').format(link!.amount) }}</span></span>
      </div>
      <div class="pay-bill__code">
        <span class="pay-bill__code-label">收款單號</span>
        <span class="pay-bill__code-value">{{ link!.code }}</span>
      </div>
    </div>

    <p class="pay-state__contact">如有疑問，請洽客服信箱 service@tfoodies.com</p>
  </div>

  <!-- 正常：金額收據卡 + 表單 -->
  <template v-else-if="canPay">
    <div class="pay-bill">
      <span class="pay-bill__eyebrow">收款項目</span>
      <p class="pay-bill__title">{{ link!.title }}</p>
      <div class="pay-bill__divider" />
      <div class="pay-bill__amount">
        <span class="pay-bill__amount-label">應付金額</span>
        <span class="pay-bill__amount-value"><span class="cur">NT$</span><span class="num">{{ new Intl.NumberFormat('zh-TW').format(link!.amount) }}</span></span>
        <div class="pay-bill__code">
          <span class="pay-bill__code-label">收款單號</span>
          <span class="pay-bill__code-value">{{ link!.code }}</span>
        </div>
      </div>
    </div>

    <form class="pay-form" @submit.prevent="submit">
      <h2 class="pay-form__title">填寫付款人資訊</h2>
      <p class="ssl-note">🔒 本頁資料以 SSL 加密傳輸，請安心填寫。</p>

      <div class="field">
        <label for="pay-name"><span class="must">*</span>姓名</label>
        <input id="pay-name" v-model="form.name" class="input" type="text" maxlength="50" autocomplete="name">
      </div>

      <div class="field">
        <label for="pay-mobile"><span class="must">*</span>手機號碼</label>
        <input id="pay-mobile" v-model="form.mobile" class="input" type="tel" maxlength="10" inputmode="numeric" autocomplete="tel" placeholder="09xxxxxxxx">
      </div>

      <div class="field">
        <label for="pay-city"><span class="must">*</span>聯絡地址</label>
        <div class="addr-row">
          <select id="pay-city" v-model="form.city" class="input">
            <option value="">縣市</option>
            <option v-for="c in cities" :key="c" :value="c">{{ c }}</option>
          </select>
          <select v-model.number="form.zipcodeId" class="input" :disabled="!form.city">
            <option :value="null">鄉鎮市區</option>
            <option v-for="a in areas" :key="a.zipcodeId" :value="a.zipcodeId">{{ a.area }}</option>
          </select>
        </div>
        <input v-model="form.address" class="input" type="text" maxlength="200" placeholder="請填寫詳細地址（勿填郵政信箱）" autocomplete="street-address">
      </div>

      <p v-if="submitError" class="pay-form__submit-err">{{ submitError }}</p>

      <button class="pay-form__cta" type="submit" :disabled="submitting">
        {{ submitting ? '處理中…' : `確認付款 ${ntd(link!.amount)} →` }}
      </button>
      <p class="pay-form__hint">點擊後將導向銀行刷卡頁面完成付款</p>
    </form>

    <!-- 手機 sticky 確認列：捲到哪都看得到金額與 CTA（呼應外送 App 的下單列） -->
    <div class="pay-stickybar">
      <div class="pay-stickybar__amount">
        <span class="pay-stickybar__amount-label">應付金額</span>
        <span class="pay-stickybar__amount-value">{{ ntd(link!.amount) }}</span>
      </div>
      <button class="pay-stickybar__btn" type="button" :disabled="submitting" @click="submit">
        {{ submitting ? '處理中…' : '確認付款 →' }}
      </button>
    </div>
  </template>

  <!-- 跳轉刷卡前的全頁覆蓋層：與按鈕 disabled 形成兩道防線，避免重複建單 -->
  <div v-if="redirecting" class="pay-redirecting">
    <div class="pay-redirecting__spinner" aria-hidden="true" />
    <p class="pay-redirecting__text">正在前往銀行刷卡頁面…</p>
    <p class="pay-redirecting__sub">請勿關閉或重新整理此頁面</p>
  </div>
</template>

<style scoped>
/* ── 金額收據卡（本頁簽名元件：撕票孔語彙）───────────────────────────── */
.pay-bill {
  position: relative; background: #fff; border: 1px solid #ececec; border-radius: 10px;
  box-shadow: 0 6px 28px rgba(38, 183, 188, .08);
  padding: 1.8em 1.8em 1.6em; margin-bottom: 1.4em;
}
.pay-bill--inline { margin: 1.8em 0 0; text-align: left; box-shadow: none; }
.pay-bill__eyebrow { font-size: .76em; letter-spacing: .12em; color: #b3b3b3; }
.pay-bill__title { margin: .35em 0 0; font-size: 1.08em; color: #2f3a3a; font-weight: 500; line-height: 1.5; }

.pay-bill__divider { position: relative; margin: 1.3em -1.8em; border-top: 1px dashed #dde5e5; }
/* 撕票孔鏤空錯覺：底色必須與 layout 的 .pay-main 背景（#fafbfb）一致 */
.pay-bill__divider::before,
.pay-bill__divider::after {
  content: ''; position: absolute; top: -8px;
  width: 16px; height: 16px; border-radius: 50%; background: #fafbfb;
}
.pay-bill__divider::before { left: -8px; }
.pay-bill__divider::after { right: -8px; }

.pay-bill__amount { text-align: center; }
.pay-bill__amount-label { display: block; font-size: .85em; color: #8a9292; margin-bottom: .3em; }
.pay-bill__amount-value { color: #1d8e92; font-weight: 700; letter-spacing: .01em; }
.pay-bill__amount-value .cur { font-size: .42em; font-weight: 600; margin-right: .2em; }
.pay-bill__amount-value .num { font-size: 2.5em; font-variant-numeric: tabular-nums; }

.pay-bill__code {
  margin: 1.1em auto 0; padding: .6em 1.1em;
  display: inline-flex; align-items: center; gap: .7em;
  background: #f6fbfb; border: 1px solid #d8eeef; border-radius: 8px; font-size: .82em;
}
.pay-bill__code-label { color: #8a9292; letter-spacing: .06em; }
.pay-bill__code-value { font-weight: 700; color: #1d8e92; letter-spacing: .03em; }

@media (max-width: 480px) {
  .pay-bill { padding: 1.5em 1.4em 1.4em; }
  .pay-bill__divider { margin: 1.1em -1.4em; }
  .pay-bill__amount-value .num { font-size: 2.1em; }
}

/* ── 表單卡片（沿用 Checkout 的 .field/.input/.must 語彙）──────────────── */
.pay-form { border: 1px solid #eee; border-radius: 6px; padding: 1.5em 1.6em; background: #fff; }
.pay-form__title { font-size: 1.02em; color: #333; font-weight: 500; margin: 0 0 1.1em; padding-bottom: .6em; border-bottom: 1px solid #f0f0f0; }
.ssl-note { margin: 0 0 1.2em; color: #8a8a8a; font-size: .82em; }

.field { margin-bottom: 1.1em; }
.field:last-of-type { margin-bottom: 0; }
.field > label { display: block; font-size: .9em; color: #666; margin-bottom: .45em; }
.must { color: #ea5520; margin-right: .25em; }

.input {
  width: 100%; box-sizing: border-box; height: 42px; padding: 0 .8em;
  border: 1px solid #e2e2e2; border-radius: 4px; color: #444; font-size: .95em;
  background: #fff; transition: border-color .2s;
}
.input:focus { outline: none; border-color: #26b7bc; }
select.input { appearance: none; background: #fff url(/content/images/arrow_select.png) right .7em center/10px no-repeat; padding-right: 2em; cursor: pointer; }
select.input:disabled { background-color: #f7f7f7; cursor: not-allowed; }

/* 縣市/區在任何寬度都並排：容器只有 480px，堆疊會讓表單過長 */
.addr-row { display: flex; gap: .6em; margin-bottom: .6em; }
.addr-row .input { flex: 1 1 0; min-width: 0; }

.pay-form__submit-err { color: #d0021b; font-size: .85em; margin: 1.2em 0 0; text-align: center; }

.pay-form__cta {
  display: block; width: 100%; box-sizing: border-box; margin: 1.4em 0 0; padding: .95em 1em;
  border: 0; border-radius: 6px; background: #26b7bc; color: #fff;
  font-size: 1.05em; font-weight: 600; letter-spacing: .03em; text-align: center;
  cursor: pointer; transition: background .2s;
}
.pay-form__cta:hover:not(:disabled) { background: #1d8e92; }
.pay-form__cta:disabled { background: #a9d8da; cursor: not-allowed; }
.pay-form__hint { margin: .8em 0 0; text-align: center; font-size: .78em; color: #aab1b1; }

/* ── 手機 sticky 底部確認列 ──────────────────────────────────────────── */
.pay-stickybar {
  display: none;
  position: fixed; left: 0; right: 0; bottom: 0; z-index: 40;
  align-items: center; justify-content: space-between; gap: 1em;
  padding: .8em 1em calc(.8em + env(safe-area-inset-bottom));
  background: #fff; border-top: 1px solid #ececec; box-shadow: 0 -6px 20px rgba(0, 0, 0, .06);
}
.pay-stickybar__amount { display: flex; flex-direction: column; line-height: 1.3; }
.pay-stickybar__amount-label { font-size: .72em; color: #8a9292; }
.pay-stickybar__amount-value { font-size: 1.15em; font-weight: 700; color: #1d8e92; font-variant-numeric: tabular-nums; }
.pay-stickybar__btn { flex: 0 0 auto; border: 0; border-radius: 6px; padding: .8em 1.6em; background: #26b7bc; color: #fff; font-size: .95em; font-weight: 600; cursor: pointer; }
.pay-stickybar__btn:disabled { background: #a9d8da; cursor: not-allowed; }

@media (max-width: 767px) {
  .pay-stickybar { display: flex; }
  .pay-form__cta { display: none; }   /* 手機用 sticky bar 的按鈕，避免兩顆 CTA */
  .pay-form__hint { margin-top: 1.2em; }
}

/* ── 跳轉前覆蓋層 ────────────────────────────────────────────────────── */
.pay-redirecting {
  position: fixed; inset: 0; z-index: 100;
  display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 1.1em;
  background: rgba(255, 255, 255, .94); text-align: center; padding: 2em;
}
.pay-redirecting__spinner { width: 40px; height: 40px; border-radius: 50%; border: 3px solid #d8eeef; border-top-color: #26b7bc; animation: pay-spin .8s linear infinite; }
.pay-redirecting__text { color: #2f3a3a; font-size: 1em; font-weight: 500; margin: 0; }
.pay-redirecting__sub { color: #8a9292; font-size: .82em; margin: 0; }
@keyframes pay-spin { to { transform: rotate(360deg); } }
@media (prefers-reduced-motion: reduce) { .pay-redirecting__spinner { animation-duration: 2.2s; } }

/* ── 狀態卡（loading / paid / invalid）──────────────────────────────── */
.pay-state { text-align: center; padding: 3em 1.6em; background: #fff; border: 1px solid #ececec; border-radius: 10px; }
.pay-state__icon { width: 76px; height: 76px; margin: 0 auto 1.2em; border-radius: 50%; display: flex; align-items: center; justify-content: center; }
.pay-state--paid .pay-state__icon { background: #26b7bc; box-shadow: 0 6px 18px rgba(38, 183, 188, .3); }
.pay-state--invalid .pay-state__icon { background: #c9ced0; }
.pay-state--loading .pay-state__icon { background: transparent; border: 3px solid #d8eeef; border-top-color: #26b7bc; animation: pay-spin .8s linear infinite; }
.pay-state__title { margin: 0; font-size: 1.3em; color: #2f3a3a; letter-spacing: .04em; }
.pay-state__lead { margin: .6em 0 0; color: #8a9292; font-size: .92em; line-height: 1.7; }
.pay-state__contact { margin-top: 1.6em; font-size: .85em; color: #aab1b1; }
</style>

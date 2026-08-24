<script setup lang="ts">
// 結帳 — 功能與內容對齊舊系統 ShoppingProfile.cshtml（訂購人/收件人/發票/付款/備註 + 步驟列、
// 縣市→區域連動、訪客自動註冊欄位、服務條款同意），介面重新設計成與購物車一致的秀氣版型。
// 送出對齊新後端 StoreOrderController.PlaceOrder 合約（Lines:[{ProductId,Qty}] + 各欄位）。
useHead({ title: '結帳' })

const config = useRuntimeConfig()
const cartStore = useCartStore()
const memberAuth = useMemberAuthStore()
// 信用卡導向財金刷卡頁（與會員中心「重新付款」共用同一份表單組裝）。
const { redirectToFisc } = useFiscCheckout()
const { cities, loadCities, loadAreas } = useZipcodes()

const isLoggedIn = computed(() => memberAuth.isAuthenticated)
const DONATION_ORG = '信星集團愛星慈善基金會'

// 登入會員的訂購人資料（對齊舊系統 ShoppingProfile：姓名/手機/地址/縣市+郵遞區號預帶）。
interface MemberProfile {
  name: string
  mobile: string
  email: string | null
  address: string | null
  zipcodeId: number | null
  city: string | null
}

let prefilling = false
// 登入態失效（伺服器換金鑰、帳號被停用、token 被撤銷）時要顯示的提示。
const sessionExpired = ref(false)

async function prefillBuyerFromMember() {
  try {
    const profile = await $fetch<MemberProfile>(`${config.public.apiBase}/member/profile`, {
      headers: { Authorization: `Bearer ${memberAuth.accessToken}` },
    })
    prefilling = true
    form.buyerName = profile.name || memberAuth.memberName || ''
    form.buyerMobile = profile.mobile || ''
    form.buyerEmail = profile.email ?? ''
    form.buyerAddress = profile.address ?? ''
    if (profile.city) {
      form.buyerCity = profile.city
      buyerAreas.value = await loadAreas(profile.city)
      form.buyerZipcodeId = profile.zipcodeId
    }
  } catch (e: unknown) {
    // ⚠️ 401/403：token 還在（hydrate 只驗 exp、不驗簽章）但伺服器已不認。此時若仍當成「已登入」，
    // 訂購人欄位會是空白且 readonly ——「請填寫訂購人手機號碼」卻又不能打字，訂單永遠送不出去。
    // 直接登出改走訪客表單，讓使用者至少能完成這筆訂單。
    const status = (e as { status?: number; statusCode?: number; response?: { status?: number } })
    const code = status?.status ?? status?.statusCode ?? status?.response?.status
    if (code === 401 || code === 403) {
      memberAuth.logout()
      sessionExpired.value = true
      form.buyerName = ''
      return
    }
    // 其他錯誤（網路瞬斷等）退回 token 內的姓名，至少帶入姓名（舊系統最低限度）。
    if (memberAuth.memberName) form.buyerName = memberAuth.memberName
  } finally {
    prefilling = false
  }
}

// 郵遞區號參照載入失敗時，縣市/區域下拉會是空的 → 收件人區域永遠選不到、訂單永遠送不出去，
// 且畫面上完全沒有線索。改為明確顯示錯誤並提供重新載入。
const zipcodeError = ref('')
const reloadingZipcodes = ref(false)
async function reloadCities() {
  zipcodeError.value = ''
  reloadingZipcodes.value = true
  try {
    await loadCities()
  } catch {
    zipcodeError.value = '縣市資料載入失敗，無法選擇收件地址。'
  } finally {
    reloadingZipcodes.value = false
  }
}
async function safeLoadAreas(city: string) {
  if (!city) return []
  try {
    const areas = await loadAreas(city)
    zipcodeError.value = ''
    return areas
  } catch {
    zipcodeError.value = '鄉鎮市區載入失敗，請重新選擇縣市。'
    return []
  }
}

// 從刷卡頁退回時，訂單其實已經成立。提示編號，避免使用者再送一次變成兩筆訂單。
const pendingOrderCode = ref('')

// 與購物車頁相同的對帳：直接進結帳頁（書籤／從商品頁按「立即結帳」）也要抓到調價與下架。
const { unavailable, repriced, syncCart, removeUnavailable } = useCartSync()

onMounted(async () => {
  cartStore.hydrate()
  await syncCart()
  pendingOrderCode.value = peekPendingPurchase()?.transaction_id ?? ''
  await reloadCities()
  if (isLoggedIn.value) await prefillBuyerFromMember()
  // 漏斗第四關：進入結帳。
  if (cartStore.items.length > 0) {
    track('begin_checkout', {
      ecommerce: {
        currency: 'TWD',
        value: cartStore.subtotal,
        items: cartStore.items.map((i) => ({
          item_id: i.productId, item_name: i.title, price: i.unitPrice, quantity: i.quantity,
        })),
      },
    })
  }
})

// ── Form state ────────────────────────────────────────────────────────────────
const form = reactive({
  buyerName: '',
  buyerMobile: '',
  buyerEmail: '',
  gender: 1,                 // 1=男 0=女
  birthYear: '', birthMonth: '', birthDay: '',
  buyerCity: '', buyerZipcodeId: null as number | null, buyerAddress: '',
  sameAsBuyer: false,
  receiverName: '',
  receiverMobile: '',
  receiverCity: '', receiverZipcodeId: null as number | null, receiverAddress: '',
  receiverTime: 0,           // 0=不指定 1=上午 2=下午
  invoiceType: 1,            // 1=電子發票(二聯) 2=捐贈 3=三聯式(公司)
  companyNumber: '', companyTitle: '',
  payType: PAY_TYPE.CREDIT_CARD,
  remark: '',
  discountCode: '',
  agree: false,
})

const years = Array.from({ length: 81 }, (_, i) => new Date().getFullYear() - i)

// ── 可用付款方式 ──────────────────────────────────────────────────────────────
// 由後端 /store/payment/methods 提供（LINE Pay 未啟用時不會出現），與下單時的白名單
// 驗證同一份來源。API 讀不到時退回信用卡＋貨到付款，避免整頁不能結帳。
interface PaymentMethod { value: number; key: string; label: string; note: string }

const FALLBACK_METHODS: PaymentMethod[] = [
  { value: PAY_TYPE.CREDIT_CARD, key: 'credit', label: '信用卡線上刷卡', note: '結帳時將自動跳轉至銀行刷卡頁面' },
  { value: PAY_TYPE.CASH_ON_DELIVERY, key: 'delivery', label: '宅配貨到付款', note: '貨品寄達時向貨運司機支付款項' },
]

const payMethods = ref<PaymentMethod[]>(FALLBACK_METHODS)

onMounted(async () => {
  try {
    const res = await $fetch<{ methods: PaymentMethod[] }>(`${config.public.apiBase}/store/payment/methods`)
    if (res?.methods?.length) payMethods.value = res.methods
  }
  catch {
    // 保留 fallback
  }
  // 目前選取的方式若已下架（例如 LINE Pay 被關閉），退回第一個可用選項。
  if (!payMethods.value.some(m => m.value === form.payType))
    form.payType = payMethods.value[0]!.value
})

// ── City → area cascade ───────────────────────────────────────────────────────
const buyerAreas = ref<{ zipcodeId: number; area: string }[]>([])
const receiverAreas = ref<{ zipcodeId: number; area: string }[]>([])

watch(() => form.buyerCity, async (city) => {
  if (prefilling) return   // 預帶會員資料時由 prefill 自行設定區域與 zipcodeId
  form.buyerZipcodeId = null
  buyerAreas.value = await safeLoadAreas(city)
})
watch(() => form.receiverCity, async (city) => {
  if (copying) return
  form.receiverZipcodeId = null
  receiverAreas.value = await safeLoadAreas(city)
})

// ── 同訂購人資訊（對齊舊系統：須先填妥訂購人資訊才能勾選複製）──────────────────────
let copying = false
let validationUncheck = false
const sameError = ref('')
watch(() => form.sameAsBuyer, async (on) => {
  if (!on) {
    // keep the message when WE unchecked it for validation; clear on a manual uncheck
    if (!validationUncheck) sameError.value = ''
    validationUncheck = false
    return
  }
  if (!form.buyerName || !form.buyerMobile || !form.buyerCity || !form.buyerZipcodeId || !form.buyerAddress) {
    sameError.value = '請先確實填寫訂購人資訊，謝謝！'
    validationUncheck = true
    // let the checked state render first, otherwise true→false collapses and the
    // native checkbox stays visually checked.
    await nextTick()
    form.sameAsBuyer = false
    return
  }
  sameError.value = ''
  copying = true
  form.receiverName = form.buyerName
  form.receiverMobile = form.buyerMobile
  form.receiverCity = form.buyerCity
  form.receiverAddress = form.buyerAddress
  receiverAreas.value = await safeLoadAreas(form.buyerCity)
  form.receiverZipcodeId = form.buyerZipcodeId
  copying = false
})

// ── Discount ──────────────────────────────────────────────────────────────────
const appliedCode = ref('')
const discountAmount = ref(0)
const discountError = ref('')
const validatingDiscount = ref(false)
async function applyDiscount() {
  discountError.value = ''
  appliedCode.value = ''
  discountAmount.value = 0
  if (!form.discountCode.trim()) return
  validatingDiscount.value = true
  try {
    const res = await $fetch<{ discountCode: string; discountAmount: number }>(
      `${config.public.apiBase}/store/discount/apply`,
      // API 以 camelCase 大小寫敏感反序列化；PascalCase 會被當缺欄位回 400（同 login/profile 慣例）。
      { method: 'POST', body: { discountCode: form.discountCode.trim(), orderSubtotal: cartStore.subtotal } },
    )
    appliedCode.value = res.discountCode
    discountAmount.value = res.discountAmount
  } catch (e: unknown) {
    discountError.value = apiErrorMessage(e, '折扣碼無效')
  } finally {
    validatingDiscount.value = false
  }
}

// ── Freight / totals（顯示用；最終金額以後端為準）────────────────────────────────
const FREIGHT_THRESHOLD = 2000
const FREIGHT_FEE = 180
const freight = computed(() => (cartStore.subtotal >= FREIGHT_THRESHOLD ? 0 : FREIGHT_FEE))
const total = computed(() => Math.max(0, cartStore.subtotal + freight.value - discountAmount.value))
const ntd = (n: number) => 'NT$ ' + new Intl.NumberFormat('zh-TW').format(Math.trunc(n))

// ── Submit ────────────────────────────────────────────────────────────────────
const submitting = ref(false)
const submitError = ref('')

// 出錯的欄位：用來標紅 + 捲動 + focus。錯誤訊息本身顯示在右側摘要（送出鈕上方），
// 但表單很長，若使用者是在頁面上方按 Enter 送出，訊息會落在畫面外而看起來「按了沒反應」。
const errorField = ref('')

interface FieldError { msg: string; field: string }

function clientValidate(): FieldError | null {
  const e = (msg: string, field: string): FieldError => ({ msg, field })
  if (!form.buyerName.trim()) return e('請填寫訂購人姓名。', 'buyerName')
  if (!form.buyerMobile.trim()) return e('請填寫訂購人手機號碼。', 'buyerMobile')
  if (!isLoggedIn.value) {
    if (!/^09\d{8}$/.test(form.buyerMobile.trim())) return e('請輸入正確的手機格式，如：0987654321。', 'buyerMobile')
    // Email 對訪客是必填：帳號由後端自動建立且不設密碼，日後要登入只能走「忘記密碼」，
    // 而那支流程以「手機 + Email」核對身分後把新密碼寄到信箱——沒有 Email 就等於永遠拿不回帳號。
    if (!form.buyerEmail.trim()) return e('請填寫電子郵件。', 'buyerEmail')
  }
  if (!form.receiverName.trim()) return e('請填寫收件人姓名。', 'receiverName')
  if (!/^09\d{8}$/.test(form.receiverMobile.trim())) return e('請輸入正確的收件人手機格式。', 'receiverMobile')
  if (!form.receiverZipcodeId) return e('請選擇收件人縣市與鄉鎮市區。', 'receiverCity')
  if (!form.receiverAddress.trim()) return e('請填寫收件人地址。', 'receiverAddress')
  if (form.invoiceType === 3 && !form.companyNumber.trim()) return e('三聯式發票請填寫統一編號。', 'companyNumber')
  if (form.invoiceType === 3 && !form.companyTitle.trim()) return e('三聯式發票請填寫公司抬頭。', 'companyTitle')
  if (!form.agree) return e('請先閱讀並同意服務條款與隱私權政策。', 'agree')
  return null
}

// 捲到出錯欄位並 focus，讓「按了沒反應」變成「明確指出哪一格要改」。
async function focusErrorField() {
  await nextTick()
  if (!import.meta.client) return
  const el = document.querySelector<HTMLElement>(`[data-field="${errorField.value}"]`)
  if (!el) return
  el.scrollIntoView({ behavior: 'smooth', block: 'center' })
  el.focus({ preventScroll: true })
}

// 下單／發起付款都必須有上限：$fetch 預設不逾時，後端一慢（訂單通知信同步寄送最慢 10 秒、
// Functions 上限 230 秒）畫面就永遠停在「送出中…」，使用者只能重整，且重整很可能變成第二筆訂單。
const ORDER_TIMEOUT_MS = 20000
const PAYMENT_TIMEOUT_MS = 15000

async function submitOrder() {
  if (cartStore.items.length === 0) return
  // 已下架商品先擋在這裡：後端一定會拒，讓顧客白填一整張表單沒有意義。
  if (unavailable.value.length) {
    submitError.value = `「${unavailable.value.map(u => u.title).join('」、「')}」已下架，請先移除後再送出。`
    return
  }
  const err = clientValidate()
  if (err) {
    submitError.value = err.msg
    errorField.value = err.field
    await focusErrorField()
    return
  }
  errorField.value = ''
  submitError.value = ''
  submitting.value = true
  // 訂單一旦成立就記下編號：後續步驟（發起付款）失敗時，訊息必須告訴使用者「訂單已經成立」，
  // 否則他會再送一次。
  let placedCode = ''
  try {
    const birthday = form.birthYear && form.birthMonth && form.birthDay
      ? `${form.birthYear}-${String(form.birthMonth).padStart(2, '0')}-${String(form.birthDay).padStart(2, '0')}`
      : null

    // 後端以 camelCase 大小寫敏感反序列化；鍵名必須 camelCase（PascalCase 會綁成 null → 400）。
    const body: Record<string, unknown> = {
      lines: cartStore.items.map((i) => ({ productId: i.productId, qty: i.quantity })),
      buyerName: form.buyerName.trim(),
      buyerMobile: form.buyerMobile.trim(),
      buyerEmail: form.buyerEmail.trim() || null,
      buyerZipcodeId: form.buyerZipcodeId,
      buyerAddress: form.buyerAddress.trim() || null,
      gender: isLoggedIn.value ? null : form.gender,
      // 不送 password：訪客結帳不設密碼（後端會建立一組無人知悉的隨機密碼），要登入請走「忘記密碼」。
      birthday: birthday,
      receiverName: form.receiverName.trim(),
      receiverMobile: form.receiverMobile.trim(),
      receiverZipcodeId: form.receiverZipcodeId,
      receiverAddress: form.receiverAddress.trim(),
      receiverTime: form.receiverTime,
      payType: form.payType,
      invoiceType: form.invoiceType,
      companyTitle: form.invoiceType === 3 ? form.companyTitle.trim() : null,
      companyNumber: form.invoiceType === 3 ? form.companyNumber.trim() : null,
      loveCode: null,
      carrierType: null,
      carrierNum: null,
      discountCode: appliedCode.value || null,
      remark: form.remark.trim() || null,
    }

    const headers: Record<string, string> = {}
    if (memberAuth.accessToken) headers['Authorization'] = `Bearer ${memberAuth.accessToken}`

    const res = await $fetch<{
      orderCode: string; payTypeKey?: string; atmCode?: string; atmExpiry?: string
      total?: number; freight?: number; discount?: number
    }>(
      `${config.public.apiBase}/store/orders`,
      { method: 'POST', body, headers, timeout: ORDER_TIMEOUT_MS },
    )
    placedCode = res.orderCode

    // 應付 0 元（100% 折扣 + 免運）不發起金流：金流不收 0 元，送過去只會卡在錯誤頁。
    // 後端在下單時已把這種訂單標記為「無須付款」。
    const payableFromServer = (res.total ?? 0) + (res.freight ?? 0) - (res.discount ?? 0)
    const needsGateway = payableFromServer > 0
    // 漏斗第五關：購買。先暫存訂單摘要再清空購物車——信用卡會跳轉外部刷卡頁，
    // 由完成頁(/Order/Success)導回後才實際觸發 purchase 事件（見 Order/Success.vue）。
    setPendingPurchase({
      transaction_id: res.orderCode,
      value: total.value,
      shipping: freight.value,
      currency: 'TWD',
      items: cartStore.items.map((i) => ({
        item_id: i.productId, item_name: i.title, price: i.unitPrice, quantity: i.quantity,
      })),
      // 僅供 server 端 CAPI 比對（雜湊後送出，不進 dataLayer）。
      email: form.buyerEmail.trim() || null,
      phone: form.buyerMobile.trim() || null,
    })
    // 需離站付款的方式（信用卡 / LINE Pay）：先向後端取得導向資訊再離開本頁。
    // returnOrigin：帶上目前所在網域，讓付款返回時導回「同一個網域」的結果頁（多網域服務時避免
    // 跨域把使用者甩到主網域、且 purchase 追蹤的 sessionStorage 跨域讀不到而漏單）。後端會以白名單驗證。
    // ⚠️ 購物車一律留到完成頁(/Order/Success)才清空——在導向付款頁前就清空的話，只要使用者從
    //    刷卡頁退回或刷卡頁載入失敗，回到結帳頁就只剩「購物車是空的」，連重試都做不到。
    const initBody = { orderCode: res.orderCode, returnOrigin: window.location.origin }  // LINE Pay 用；信用卡走 useFiscCheckout

    // LINE Pay：後端建立交易後回傳付款頁網址，整頁導向；結果由 /store/payment/linepay/confirm 導回。
    if (form.payType === PAY_TYPE.LINE_PAY && needsGateway) {
      const init = await $fetch<{ paymentUrl: string }>(
        `${config.public.apiBase}/store/payment/linepay/create`,
        { method: 'POST', body: initBody, timeout: PAYMENT_TIMEOUT_MS },
      )
      window.location.href = init.paymentUrl
      return
    }

    // 信用卡：發起財金 FISC WEBPOS 刷卡。後端回傳 form action 與欄位，動態建表單
    // auto-submit 將使用者整頁導向財金刷卡頁；刷卡結果由財金導回 /store/payment/return。
    if (form.payType === PAY_TYPE.CREDIT_CARD && needsGateway) {
      // 表單組裝與逾時設定共用 useFiscCheckout（會員中心「重新付款」走同一份）。
      await redirectToFisc(res.orderCode)
      return
    }

    // 無須離站付款（貨到付款 / ATM）：購物車同樣由完成頁清空。
    const query: Record<string, string> = { code: res.orderCode }
    if (res.atmCode) query.atm = res.atmCode
    if (res.atmExpiry) query.atmExpiry = res.atmExpiry
    await navigateTo({ path: '/Order/Success', query })
  } catch (e: unknown) {
    if (placedCode) {
      // 訂單已經進資料庫、庫存也扣了，只是後續發起付款失敗——絕不能讓使用者再送一次。
      pendingOrderCode.value = placedCode
      submitError.value = isTimeout(e)
        ? `訂單 ${placedCode} 已成立，但轉往付款頁時逾時。請勿重複送出，請至會員中心查看訂單並重新付款。`
        : `訂單 ${placedCode} 已成立，但轉往付款頁失敗（${apiErrorMessage(e, '請稍後再試')}）。請勿重複送出，請至會員中心重新付款。`
    } else if (isTimeout(e)) {
      submitError.value = '連線逾時，訂單可能已經成立。請勿重複送出，請先至會員中心確認訂單，或與客服聯繫。'
    } else {
      submitError.value = apiErrorMessage(e, '訂單送出失敗，請稍後再試。')
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <main id="main">
    <section class="tallsection clr">
      <div class="restrict-wide allpadding">
        <!-- 標題（與購物車頁一致：h1 + direct-line）-->
        <div class="centered">
          <h1>填寫訂購資訊</h1>
          <div class="direct-line"></div>
        </div>

        <!-- 步驟列 -->
        <ol class="steps">
          <li class="done"><span class="dot">1</span><span class="lbl">商品資訊</span></li>
          <li class="active"><span class="dot">2</span><span class="lbl">訂購資訊</span></li>
          <li><span class="dot">3</span><span class="lbl">完成訂購</span></li>
        </ol>

        <!-- Empty -->
        <div v-if="cartStore.items.length === 0" class="centered" style="padding:2.5em 0;">
          <p class="checkout-empty-text">購物車是空的</p>
          <a href="/Products" class="btn basic">前往選購</a>
        </div>

        <form v-else class="checkout-grid" @submit.prevent="submitOrder">
          <!-- ── 左：填寫表單 ───────────────────────────────────────────── -->
          <div class="checkout-form">
            <p class="ssl-note descript">🔒 本站採用 SSL 加密傳輸，請安心填寫。</p>

            <!-- 購物車對帳結果：已下架要先移除、價格有變要講清楚（送出後才發現金額不同會變成客訴） -->
            <div v-if="unavailable.length" class="notice notice--err">
              以下商品已下架或無法購買，請先移除才能送出訂單：<strong>{{ unavailable.map(u => u.title).join('、') }}</strong>
              <button type="button" class="notice-btn" @click="removeUnavailable">一鍵移除</button>
            </div>
            <div v-if="repriced.length" class="notice notice--warn">
              部分商品價格已更新，下方金額為最新售價：
              <span v-for="(r, i) in repriced" :key="r.title">{{ i ? '、' : '' }}{{ r.title }}（{{ ntd(r.from) }} → {{ ntd(r.to) }}）</span>
            </div>

            <!-- 登入態失效：已自動切回訪客表單，告知原因，避免使用者以為資料莫名不見 -->
            <div v-if="sessionExpired" class="notice notice--warn">
              您的登入狀態已失效，已切換為訪客結帳。可先完成本次訂單，或
              <a href="/Member/Login">重新登入</a>後再結帳。
            </div>

            <!-- 從付款頁退回時：訂單其實已經成立，提醒不要再送一次 -->
            <div v-if="pendingOrderCode" class="notice notice--warn">
              您有一筆訂單 <strong>{{ pendingOrderCode }}</strong> 已經成立、尚未完成付款。
              請至<a href="/Member/Orders">會員中心 › 訂單查詢</a>重新付款，重新送出會產生另一筆訂單。
            </div>

            <!-- 郵遞區號載入失敗：不提示的話，使用者會卡在「請選擇收件人縣市」卻無從選起 -->
            <div v-if="zipcodeError" class="notice notice--err">
              {{ zipcodeError }}
              <button type="button" class="notice-btn" :disabled="reloadingZipcodes" @click="reloadCities">
                {{ reloadingZipcodes ? '載入中…' : '重新載入' }}
              </button>
            </div>

            <!-- 訂購人資訊 -->
            <div class="formstyle card">
              <h2 class="card-title">訂購人資訊</h2>
              <div class="field">
                <label><span class="must">*</span>姓名</label>
                <input v-model="form.buyerName" class="input" :class="{ 'field-error': errorField === 'buyerName' }" data-field="buyerName" maxlength="20" placeholder="請輸入姓名" :readonly="isLoggedIn">
              </div>
              <div class="field">
                <label><span class="must">*</span>手機號碼</label>
                <input v-model="form.buyerMobile" class="input" :class="{ 'field-error': errorField === 'buyerMobile' }" data-field="buyerMobile" maxlength="10" placeholder="例：0987654321" :readonly="isLoggedIn">
                <p v-if="!isLoggedIn" class="descript hint">手機號碼將成為您的會員帳號與聯絡電話，請輸入純數字。</p>
              </div>

              <template v-if="!isLoggedIn">
                <div class="field">
                  <label><span class="must">*</span>電子郵件</label>
                  <input v-model="form.buyerEmail" type="email" class="input" :class="{ 'field-error': errorField === 'buyerEmail' }" data-field="buyerEmail" placeholder="example@mail.com">
                </div>
                <div class="field">
                  <label>性別</label>
                  <div class="radio-row">
                    <label class="radio"><input type="radio" v-model.number="form.gender" :value="1"> 男生</label>
                    <label class="radio"><input type="radio" v-model.number="form.gender" :value="0"> 女生</label>
                  </div>
                </div>
                <div class="field">
                  <label>生日</label>
                  <div class="birth-row">
                    <select v-model="form.birthYear" class="input"><option value="">年</option><option v-for="y in years" :key="y" :value="y">{{ y }}</option></select>
                    <select v-model="form.birthMonth" class="input"><option value="">月</option><option v-for="m in 12" :key="m" :value="m">{{ m }}</option></select>
                    <select v-model="form.birthDay" class="input"><option value="">日</option><option v-for="d in 31" :key="d" :value="d">{{ d }}</option></select>
                  </div>
                </div>
              </template>

              <div class="field">
                <label>聯絡地址</label>
                <div class="addr-row">
                  <select v-model="form.buyerCity" class="input" :disabled="isLoggedIn"><option value="">縣市</option><option v-for="c in cities" :key="c" :value="c">{{ c }}</option></select>
                  <select v-model.number="form.buyerZipcodeId" class="input" :disabled="isLoggedIn || !form.buyerCity"><option :value="null">鄉鎮市區</option><option v-for="a in buyerAreas" :key="a.zipcodeId" :value="a.zipcodeId">{{ a.area }}</option></select>
                </div>
                <input v-model="form.buyerAddress" class="input" placeholder="請填寫詳細地址（勿填郵政信箱）" :readonly="isLoggedIn">
              </div>
              <p v-if="isLoggedIn" class="descript hint">訂購人資訊取自您的會員資料，如需修改請至<a href="/Member/Profile">會員中心 › 會員資料</a>。</p>
            </div>

            <!-- 收件人資訊 -->
            <div class="formstyle card">
              <div class="card-title-row">
                <h2 class="card-title">收件人資訊</h2>
                <label class="same-check descript"><input type="checkbox" v-model="form.sameAsBuyer"> 同訂購人資訊</label>
              </div>
              <p v-if="sameError" class="same-error">{{ sameError }}</p>
              <div class="field">
                <label><span class="must">*</span>姓名</label>
                <input v-model="form.receiverName" class="input" :class="{ 'field-error': errorField === 'receiverName' }" data-field="receiverName" maxlength="20" placeholder="請輸入收件人姓名">
              </div>
              <div class="field">
                <label><span class="must">*</span>手機號碼</label>
                <input v-model="form.receiverMobile" class="input" :class="{ 'field-error': errorField === 'receiverMobile' }" data-field="receiverMobile" maxlength="10" placeholder="例：0987654321">
              </div>
              <div class="field">
                <label><span class="must">*</span>聯絡地址</label>
                <div class="addr-row">
                  <select v-model="form.receiverCity" class="input" :class="{ 'field-error': errorField === 'receiverCity' }" data-field="receiverCity"><option value="">縣市</option><option v-for="c in cities" :key="c" :value="c">{{ c }}</option></select>
                  <select v-model.number="form.receiverZipcodeId" class="input" :class="{ 'field-error': errorField === 'receiverCity' }" :disabled="!form.receiverCity"><option :value="null">鄉鎮市區</option><option v-for="a in receiverAreas" :key="a.zipcodeId" :value="a.zipcodeId">{{ a.area }}</option></select>
                </div>
                <input v-model="form.receiverAddress" class="input" :class="{ 'field-error': errorField === 'receiverAddress' }" data-field="receiverAddress" placeholder="請填寫詳細地址（勿填郵政信箱）">
              </div>
              <div class="field">
                <label>配送時段</label>
                <select v-model.number="form.receiverTime" class="input">
                  <option :value="0">不指定</option>
                  <option :value="1">上午（09:00–13:00）</option>
                  <option :value="2">下午（14:00–18:00）</option>
                </select>
              </div>
            </div>

            <!-- 發票資訊 -->
            <div class="formstyle card">
              <h2 class="card-title"><span class="must">*</span>發票資訊</h2>
              <label class="opt"><input type="radio" v-model.number="form.invoiceType" :value="1"> 電子發票<span class="descript opt-note">（將寄送至您的電子郵件）</span></label>
              <label class="opt"><input type="radio" v-model.number="form.invoiceType" :value="2"> 捐贈發票</label>
              <p v-if="form.invoiceType === 2" class="descript opt-detail">捐贈單位：{{ DONATION_ORG }}</p>
              <label class="opt"><input type="radio" v-model.number="form.invoiceType" :value="3"> 三聯式發票（公司行號報帳用）</label>
              <template v-if="form.invoiceType === 3">
                <p class="descript opt-detail">依統一發票使用辦法，個人戶（二聯式）發票開立後不得更改為公司戶（三聯式）。</p>
                <div class="field inline-field">
                  <label>統一編號</label>
                  <input v-model="form.companyNumber" class="input" :class="{ 'field-error': errorField === 'companyNumber' }" data-field="companyNumber" maxlength="8" placeholder="8 碼統一編號">
                </div>
                <div class="field inline-field">
                  <label>公司抬頭</label>
                  <input v-model="form.companyTitle" class="input" :class="{ 'field-error': errorField === 'companyTitle' }" data-field="companyTitle" maxlength="50" placeholder="公司抬頭">
                </div>
              </template>
            </div>

            <!-- 付款方式 -->
            <div class="formstyle card">
              <h2 class="card-title"><span class="must">*</span>付款方式</h2>
              <label v-for="m in payMethods" :key="m.value" class="opt">
                <input type="radio" v-model.number="form.payType" :value="m.value"> {{ m.label }}<span class="descript opt-note">{{ m.note }}</span>
              </label>
            </div>

            <!-- 備註 -->
            <div class="formstyle card">
              <h2 class="card-title">備註欄</h2>
              <p class="descript hint" style="margin-top:0;">若有需特別註明的地方，歡迎留言於下方。</p>
              <textarea v-model="form.remark" rows="3" class="input" placeholder="例：配送注意事項"></textarea>
            </div>
          </div>

          <!-- ── 右：訂單摘要 ───────────────────────────────────────────── -->
          <aside class="checkout-aside">
            <div class="summary-card">
              <h2 class="card-title">訂單摘要</h2>
              <ul class="sum-items">
                <li v-for="item in cartStore.items" :key="item.productId" class="sum-item">
                  <span class="sum-name">{{ item.title }}<em class="sum-qty">×{{ item.quantity }}</em></span>
                  <span class="sum-amt">{{ ntd(item.unitPrice * item.quantity) }}</span>
                </li>
              </ul>

              <div class="coupon">
                <input v-model="form.discountCode" class="input" placeholder="折扣碼">
                <button type="button" class="btn basic coupon-btn" :disabled="validatingDiscount" @click="applyDiscount">
                  {{ validatingDiscount ? '…' : '套用' }}
                </button>
              </div>
              <p v-if="discountError" class="coupon-msg err">{{ discountError }}</p>
              <p v-else-if="appliedCode" class="coupon-msg ok">已套用「{{ appliedCode }}」，折抵 {{ ntd(discountAmount) }}</p>

              <dl class="sum-totals">
                <div><dt>商品小計</dt><dd>{{ ntd(cartStore.subtotal) }}</dd></div>
                <div>
                  <dt>運費</dt>
                  <dd><span v-if="freight === 0" class="freeship-note">免運</span><span v-else>{{ ntd(freight) }}</span></dd>
                </div>
                <div v-if="discountAmount > 0"><dt>折扣</dt><dd class="minus">-{{ ntd(discountAmount) }}</dd></div>
              </dl>
              <div class="sum-total"><span>應付金額</span><strong>{{ ntd(total) }}</strong></div>

              <label class="agree descript">
                <input type="checkbox" v-model="form.agree" class="agree-cb" :class="{ 'field-error': errorField === 'agree' }" data-field="agree">
                <span class="agree-text">我已閱讀並同意<a href="/Terms" target="_blank">服務條款</a>與<a href="/Policy" target="_blank">隱私權政策</a></span>
              </label>

              <p v-if="submitError" class="submit-err">{{ submitError }}</p>

              <button type="submit" class="btn basic submit-btn" :disabled="submitting">
                {{ submitting ? '送出中…' : '確認送出訂單' }}
              </button>
              <a href="/Cart" class="outline-btn solidhover back-btn">回上一步</a>
            </div>
          </aside>
        </form>
      </div>
    </section>
  </main>
</template>

<style scoped>
/* 與購物車一致的「秀氣 整齊」結帳版型：左表單卡片、右黏性訂單摘要。沿用 main.css 的
   step-wrap / formstyle / input / must 等舊樣式，再以細線、淺字、留白收斂視覺重量。
   品牌色 teal #26b7bc / 深 #1d8e92。 */
/* Step indicator — clean, self-contained (legacy .heading was font-size:0 and the
   step labels were white-on-transparent). Title above uses the global h1 + direct-line,
   identical to the cart page. */
.steps { list-style: none; display: flex; justify-content: center; align-items: flex-start; gap: 0; margin: .5em auto 2.5em; padding: 0; max-width: 460px; }
.steps li { position: relative; flex: 1 1 0; text-align: center; color: #c4c4c4; }
.steps li .dot {
  position: relative; z-index: 1; display: inline-flex; align-items: center; justify-content: center;
  width: 38px; height: 38px; border-radius: 50%; background: #ececec; color: #fff; font-size: 1.05em;
}
.steps li .lbl { display: block; margin-top: .55em; font-size: .85em; letter-spacing: .05em; }
/* connecting line between dots */
.steps li::before {
  content: ''; position: absolute; top: 19px; right: 50%; width: 100%; height: 2px; background: #ececec;
}
.steps li:first-child::before { display: none; }
.steps li.done .dot, .steps li.active .dot { background: #26b7bc; }
.steps li.done, .steps li.active { color: #1d8e92; }
.steps li.done::before, .steps li.active::before { background: #26b7bc; }

.checkout-grid {
  display: grid; grid-template-columns: 1fr 340px; gap: 2em; align-items: start;
  max-width: 1040px; margin: 0 auto;
}
.ssl-note { margin: 0 0 1.2em; color: #8a8a8a; }

/* Cards */
.card {
  border: 1px solid #eee; border-radius: 6px; padding: 1.4em 1.5em; margin-bottom: 1.2em; background: #fff;
}
.card-title {
  font-size: 1.05em; color: #333; font-weight: 500; margin: 0 0 1.1em;
  padding-bottom: .6em; border-bottom: 1px solid #f0f0f0; letter-spacing: .03em;
}
.card-title-row { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.1em; }
.card-title-row .card-title { margin: 0; border: 0; padding: 0; }
.same-check { color: #888; cursor: pointer; user-select: none; }
.same-check input { margin-right: .3em; }
.same-error { color: #d0021b; font-size: .82em; margin: -.4em 0 1em; }
.must { color: #ea5520; margin-right: .25em; }

/* Fields */
.field { margin-bottom: 1.1em; }
.field:last-child { margin-bottom: 0; }
.field > label { display: block; font-size: .9em; color: #666; margin-bottom: .45em; }
.checkout-form :deep(.input),
.checkout-form textarea.input {
  width: 100%; box-sizing: border-box; height: 40px; padding: 0 .8em; border: 1px solid #e2e2e2;
  border-radius: 4px; color: #444; font-size: .95em; background: #fff; transition: border-color .2s;
}
.checkout-form textarea.input { height: auto; padding: .6em .8em; line-height: 1.5; resize: vertical; }
.checkout-form :deep(.input:focus), .checkout-form textarea.input:focus { outline: none; border-color: #26b7bc; }
.checkout-form :deep(select.input) { appearance: none; background: #fff url(/content/images/arrow_select.png) right .7em center/10px no-repeat; padding-right: 2em; cursor: pointer; }
.checkout-form :deep(.input:disabled) { background: #f6f6f6; color: #aaa; cursor: not-allowed; }
/* 登入會員的訂購人欄位為唯讀，視覺與 disabled 一致（灰底、不可改）。 */
.checkout-form :deep(.input[readonly]) { background: #f6f6f6; color: #888; cursor: not-allowed; border-color: #ececec; }
.checkout-form :deep(.input[readonly]:focus) { border-color: #ececec; }

/* 驗證失敗的欄位標紅（配合 focusErrorField 捲動 + focus） */
.checkout-form :deep(.field-error), .agree-cb.field-error {
  border-color: #d0021b !important; background-color: #fff7f7;
  outline: 2px solid rgba(208, 2, 27, .12); outline-offset: 1px;
}

/* 頁面級提示（未完成付款的訂單 / 郵遞區號載入失敗） */
.notice {
  border-radius: 6px; padding: .9em 1.1em; margin-bottom: 1.2em;
  font-size: .88em; line-height: 1.7;
}
.notice--warn { background: #fff8ec; border: 1px solid #f3dca6; color: #8a6d1f; }
.notice--err { background: #fdf3f3; border: 1px solid #f0c9c9; color: #a33; }
.notice a { color: #1d8e92; text-decoration: underline; }
.notice-btn {
  margin-left: .6em; padding: .25em .9em; border: 1px solid currentColor; border-radius: 4px;
  background: transparent; color: inherit; font-size: .95em; cursor: pointer;
}
.notice-btn:disabled { opacity: .5; cursor: default; }

.addr-row, .birth-row { display: flex; gap: .6em; margin-bottom: .6em; }
.birth-row { margin-bottom: 0; }
.radio-row { display: flex; gap: 1.5em; padding-top: .4em; }
.radio, .opt { display: flex; align-items: baseline; gap: .4em; color: #555; cursor: pointer; }
.radio input, .opt input { flex: 0 0 auto; }

/* Invoice / payment options */
.opt { padding: .55em 0; border-bottom: 1px solid #f6f6f6; }
.opt:last-of-type { border-bottom: 0; }
.opt-note { font-size: .82em; color: #aaa; }
.opt-detail { margin: 0 0 .8em 1.6em; color: #999; font-size: .82em; line-height: 1.5; }
.inline-field { display: flex; align-items: center; gap: .8em; margin-left: 1.6em; }
.inline-field > label { margin: 0; flex: 0 0 70px; }
.inline-field .input { max-width: 240px; }
.hint { font-size: .8em; color: #aaa; margin: .4em 0 0; line-height: 1.5; }

/* ── Summary aside ── */
/* --header-sticky-top = 固定 header 高度 + 間距（header-sticky-offset plugin 動態量測），
   讓摘要釘在 menu 之下；SSR/未量測時退回 1em。 */
.checkout-aside { position: sticky; top: var(--header-sticky-top, 1em); }
.summary-card { border: 1px solid #eee; border-radius: 6px; padding: 1.4em 1.5em; background: #fafdfd; }
.sum-items { list-style: none; margin: 0 0 1.1em; padding: 0 0 1.1em; border-bottom: 1px solid #ececec; }
.sum-item { display: flex; justify-content: space-between; gap: .8em; margin-bottom: .7em; font-size: .9em; color: #555; }
.sum-item:last-child { margin-bottom: 0; }
.sum-name { line-height: 1.4; }
.sum-qty { font-style: normal; color: #aaa; margin-left: .4em; }
.sum-amt { flex: 0 0 auto; color: #444; }

.coupon { display: flex; gap: .5em; margin-bottom: .5em; }
.coupon .input { flex: 1 1 auto; height: 38px; box-sizing: border-box; padding: 0 .7em; border: 1px solid #e2e2e2; border-radius: 4px; }
/* (1) 套用按鈕與折扣碼 input 等高 */
.coupon-btn {
  flex: 0 0 auto; height: 38px; box-sizing: border-box; padding: 0 1.1em; margin: 0; min-width: auto;
  display: inline-flex; align-items: center; justify-content: center; line-height: 1;
}
.coupon-msg { font-size: .82em; margin: .2em 0 0; }
.coupon-msg.err { color: #d0021b; }
.coupon-msg.ok { color: #7c9a1e; }

.sum-totals { margin: 1.1em 0 0; }
.sum-totals > div { display: flex; justify-content: space-between; margin-bottom: .6em; color: #8a8a8a; font-size: .92em; }
.sum-totals dt, .sum-totals dd { margin: 0; }
.sum-totals dd { color: #555; }
.sum-totals dd.minus { color: #7c9a1e; }
.freeship-note { color: #95ad25; }

.sum-total {
  display: flex; align-items: baseline; justify-content: space-between;
  margin-top: .6em; padding-top: 1em; border-top: 1px solid #ececec; color: #444;
}
.sum-total strong { color: #1d8e92; font-size: 1.5em; }

/* (2) 同意條款：checkbox 對齊首行，文字可自然換行（窄螢幕 RWD 不溢出） */
.agree { display: flex; align-items: center; gap: .45em; margin: 1.2em 0; color: #888; font-size: .8em; cursor: pointer; }
/* 覆寫 main.css 全域 input[type=checkbox] 的 padding:0 1em / margin:1em（會把方塊推歪） */
.agree .agree-cb { flex: 0 0 auto; margin: 0; padding: 0; }
.agree .agree-text { line-height: 1.6; }
.agree a { color: #1d8e92; text-decoration: underline; white-space: nowrap; }

.submit-err { color: #d0021b; font-size: .85em; margin: 0 0 .8em; }
/* (3) 確認送出 / 回上一步 等寬、左右邊緣對齊（清除 legacy .btn 的左右 margin）*/
.submit-btn { display: block; width: 100%; box-sizing: border-box; text-align: center; margin: 0; }
.back-btn { display: block; width: 100%; box-sizing: border-box; text-align: center; margin: .7em 0 0; }

.checkout-empty-text { color: #a8a8a8; font-size: 1.05em; margin-bottom: 1.4em; }

/* ── Mobile ── */
@media (max-width: 880px) {
  .checkout-grid { grid-template-columns: 1fr; }
  .checkout-aside { position: static; }
}
@media (max-width: 600px) {
  .addr-row, .birth-row { flex-wrap: wrap; }
  .birth-row .input { flex: 1 1 28%; }
}
</style>

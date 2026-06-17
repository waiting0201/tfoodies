<script setup lang="ts">
// 結帳 — 功能與內容對齊舊系統 ShoppingProfile.cshtml（訂購人/收件人/發票/付款/備註 + 步驟列、
// 縣市→區域連動、訪客自動註冊欄位、服務條款同意），介面重新設計成與購物車一致的秀氣版型。
// 送出對齊新後端 StoreOrderController.PlaceOrder 合約（Lines:[{ProductId,Qty}] + 各欄位）。
useHead({ title: '結帳' })

const config = useRuntimeConfig()
const cartStore = useCartStore()
const memberAuth = useMemberAuthStore()
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
  } catch {
    // 取不到 profile 時退回 token 內的姓名，至少帶入姓名（舊系統最低限度）。
    if (memberAuth.memberName) form.buyerName = memberAuth.memberName
  } finally {
    prefilling = false
  }
}

onMounted(async () => {
  cartStore.hydrate()
  await loadCities()
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
  password: '',
  password2: '',
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
  payType: 1,                // 1=信用卡 2=貨到付款
  remark: '',
  discountCode: '',
  agree: false,
})

const years = Array.from({ length: 81 }, (_, i) => new Date().getFullYear() - i)

// ── City → area cascade ───────────────────────────────────────────────────────
const buyerAreas = ref<{ zipcodeId: number; area: string }[]>([])
const receiverAreas = ref<{ zipcodeId: number; area: string }[]>([])

watch(() => form.buyerCity, async (city) => {
  if (prefilling) return   // 預帶會員資料時由 prefill 自行設定區域與 zipcodeId
  form.buyerZipcodeId = null
  buyerAreas.value = await loadAreas(city)
})
watch(() => form.receiverCity, async (city) => {
  if (copying) return
  form.receiverZipcodeId = null
  receiverAreas.value = await loadAreas(city)
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
  receiverAreas.value = await loadAreas(form.buyerCity)
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
    discountError.value = (e as { data?: { message?: string } })?.data?.message ?? '折扣碼無效'
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

function clientValidate(): string | null {
  if (!form.buyerName.trim()) return '請填寫訂購人姓名。'
  if (!form.buyerMobile.trim()) return '請填寫訂購人手機號碼。'
  if (!isLoggedIn.value) {
    if (!/^09\d{8}$/.test(form.buyerMobile.trim())) return '請輸入正確的手機格式，如：0987654321。'
    if (!form.password || form.password.length < 6) return '請設定 6～20 字元的密碼。'
    if (form.password !== form.password2) return '兩次輸入的密碼不相同。'
    if (!form.buyerEmail.trim()) return '請填寫電子郵件。'
  }
  if (!form.receiverName.trim()) return '請填寫收件人姓名。'
  if (!/^09\d{8}$/.test(form.receiverMobile.trim())) return '請輸入正確的收件人手機格式。'
  if (!form.receiverZipcodeId) return '請選擇收件人縣市與鄉鎮市區。'
  if (!form.receiverAddress.trim()) return '請填寫收件人地址。'
  if (form.invoiceType === 3 && (!form.companyNumber.trim() || !form.companyTitle.trim()))
    return '三聯式發票請填寫統一編號與公司抬頭。'
  if (!form.agree) return '請先閱讀並同意服務條款與隱私權政策。'
  return null
}

async function submitOrder() {
  if (cartStore.items.length === 0) return
  const err = clientValidate()
  if (err) { submitError.value = err; return }
  submitError.value = ''
  submitting.value = true
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
      password: isLoggedIn.value ? null : form.password,
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

    const res = await $fetch<{ orderCode: string; payTypeKey?: string; atmCode?: string; atmExpiry?: string }>(
      `${config.public.apiBase}/store/orders`,
      { method: 'POST', body, headers },
    )
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
    // 信用卡：發起財金 FISC WEBPOS 刷卡。後端回傳 form action 與欄位，動態建表單
    // auto-submit 將使用者整頁導向財金刷卡頁；刷卡結果由財金導回 /store/payment/return。
    // ⚠️ 必須先成功取得刷卡 form 才清空購物車——否則 create 失敗時購物車已被清空，
    //    空購物車 v-if 會整塊取代表單（含錯誤訊息），使用者只會看到「購物車是空的」。
    if (form.payType === 1) {
      const init = await $fetch<{ actionUrl: string; fields: Record<string, string> }>(
        `${config.public.apiBase}/store/payment/create`,
        { method: 'POST', body: { orderCode: res.orderCode } },
      )
      cartStore.items = []
      cartStore.persist()
      const f = document.createElement('form')
      f.method = 'post'
      f.action = init.actionUrl
      f.acceptCharset = 'UTF-8'
      for (const [k, v] of Object.entries(init.fields)) {
        const i = document.createElement('input')
        i.type = 'hidden'
        i.name = k
        i.value = v ?? ''
        f.appendChild(i)
      }
      document.body.appendChild(f)
      f.submit()
      return
    }

    cartStore.items = []
    cartStore.persist()

    const query: Record<string, string> = { code: res.orderCode }
    if (res.atmCode) query.atm = res.atmCode
    if (res.atmExpiry) query.atmExpiry = res.atmExpiry
    await navigateTo({ path: '/Order/Success', query })
  } catch (e: unknown) {
    submitError.value = (e as { data?: { message?: string } })?.data?.message ?? '訂單送出失敗，請稍後再試。'
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

            <!-- 訂購人資訊 -->
            <div class="formstyle card">
              <h2 class="card-title">訂購人資訊</h2>
              <div class="field">
                <label><span class="must">*</span>姓名</label>
                <input v-model="form.buyerName" class="input" maxlength="20" placeholder="請輸入姓名" :readonly="isLoggedIn">
              </div>
              <div class="field">
                <label><span class="must">*</span>手機號碼</label>
                <input v-model="form.buyerMobile" class="input" maxlength="10" placeholder="例：0987654321" :readonly="isLoggedIn">
                <p v-if="!isLoggedIn" class="descript hint">手機號碼將成為您的會員帳號與聯絡電話，請輸入純數字。</p>
              </div>

              <template v-if="!isLoggedIn">
                <div class="field">
                  <label><span class="must">*</span>設定密碼</label>
                  <input v-model="form.password" type="password" class="input" maxlength="20" placeholder="6～20 字元">
                </div>
                <div class="field">
                  <label><span class="must">*</span>密碼確認</label>
                  <input v-model="form.password2" type="password" class="input" maxlength="20" placeholder="再次輸入密碼">
                </div>
                <div class="field">
                  <label><span class="must">*</span>電子郵件</label>
                  <input v-model="form.buyerEmail" type="email" class="input" placeholder="example@mail.com">
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
                <input v-model="form.receiverName" class="input" maxlength="20" placeholder="請輸入收件人姓名">
              </div>
              <div class="field">
                <label><span class="must">*</span>手機號碼</label>
                <input v-model="form.receiverMobile" class="input" maxlength="10" placeholder="例：0987654321">
              </div>
              <div class="field">
                <label><span class="must">*</span>聯絡地址</label>
                <div class="addr-row">
                  <select v-model="form.receiverCity" class="input"><option value="">縣市</option><option v-for="c in cities" :key="c" :value="c">{{ c }}</option></select>
                  <select v-model.number="form.receiverZipcodeId" class="input" :disabled="!form.receiverCity"><option :value="null">鄉鎮市區</option><option v-for="a in receiverAreas" :key="a.zipcodeId" :value="a.zipcodeId">{{ a.area }}</option></select>
                </div>
                <input v-model="form.receiverAddress" class="input" placeholder="請填寫詳細地址（勿填郵政信箱）">
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
                  <input v-model="form.companyNumber" class="input" maxlength="8" placeholder="8 碼統一編號">
                </div>
                <div class="field inline-field">
                  <label>公司抬頭</label>
                  <input v-model="form.companyTitle" class="input" maxlength="50" placeholder="公司抬頭">
                </div>
              </template>
            </div>

            <!-- 付款方式 -->
            <div class="formstyle card">
              <h2 class="card-title"><span class="must">*</span>付款方式</h2>
              <label class="opt"><input type="radio" v-model.number="form.payType" :value="1"> 信用卡線上刷卡<span class="descript opt-note">結帳時將自動跳轉至銀行刷卡頁面</span></label>
              <label class="opt"><input type="radio" v-model.number="form.payType" :value="2"> 宅配貨到付款<span class="descript opt-note">貨品寄達時向貨運司機支付款項</span></label>
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
                <input type="checkbox" v-model="form.agree" class="agree-cb">
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

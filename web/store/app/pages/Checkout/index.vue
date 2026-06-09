<script setup lang="ts">
useHead({ title: '結帳' })

const config = useRuntimeConfig()
const cartStore = useCartStore()
const memberAuth = useMemberAuthStore()

onMounted(() => cartStore.hydrate())

// ── Form state ──────────────────────────────────────────────────────────────
const form = reactive({
  buyerName: '',
  buyerMobile: '',
  buyerEmail: '',
  receiverName: '',
  receiverMobile: '',
  receiverAddress: '',
  receiverZipcodeId: 0,
  receiverTime: 0,       // 0=不指定 1=上午 2=下午
  payType: 1,            // 1=信用卡 2=ATM 3=貨到付款
  invoiceType: 1,        // 1=電子信箱 2=公司 3=捐贈
  invoiceEmail: '',
  companyTitle: '',
  companyNumber: '',
  loveCode: '',
  discountCode: '',
  remark: '',
})

// ── Discount code ────────────────────────────────────────────────────────────
const discountId = ref<string | null>(null)
const discountAmount = ref(0)
const discountError = ref('')
const validatingDiscount = ref(false)

async function applyDiscount() {
  discountError.value = ''
  discountId.value = null
  discountAmount.value = 0
  if (!form.discountCode.trim()) return
  validatingDiscount.value = true
  try {
    const res = await $fetch<{ discountId: string; discountAmount: number }>(
      `${config.public.apiBase}/store/discount/apply`,
      {
        method: 'POST',
        body: { DiscountCode: form.discountCode.trim(), Subtotal: cartStore.subtotal },
      },
    )
    discountId.value = res.discountId
    discountAmount.value = res.discountAmount
  } catch (e: unknown) {
    const err = e as { data?: { message?: string } }
    discountError.value = err?.data?.message ?? '折扣碼無效'
  } finally {
    validatingDiscount.value = false
  }
}

// ── Freight / totals ────────────────────────────────────────────────────────
const FREIGHT_THRESHOLD = 2000
const FREIGHT_FEE = 120
const freight = computed(() =>
  cartStore.subtotal >= FREIGHT_THRESHOLD ? 0 : FREIGHT_FEE,
)
const total = computed(() =>
  cartStore.subtotal + freight.value - discountAmount.value,
)

const ntd = (n: number) => 'NT$ ' + new Intl.NumberFormat('zh-TW').format(Math.trunc(n))

// ── Submit ───────────────────────────────────────────────────────────────────
const submitting = ref(false)
const submitError = ref('')

async function submitOrder() {
  if (cartStore.items.length === 0) return
  submitting.value = true
  submitError.value = ''
  try {
    const body: Record<string, unknown> = {
      ...form,
      items: cartStore.items.map((i) => ({
        ProductId: i.productId,
        Quantity: i.quantity,
        UnitPrice: i.unitPrice,
      })),
      freight: freight.value,
    }
    if (discountId.value) body.discountId = discountId.value

    const headers: Record<string, string> = {}
    if (memberAuth.accessToken) {
      headers['Authorization'] = `Bearer ${memberAuth.accessToken}`
    }

    const res = await $fetch<{ orderCode: string; atmAccount?: string; atmExpiry?: string }>(
      `${config.public.apiBase}/store/orders`,
      { method: 'POST', body, headers },
    )
    cartStore.items = []
    cartStore.persist()

    // Pass ATM details via query so Success page can display them
    const query: Record<string, string> = { code: res.orderCode }
    if (res.atmAccount) query.atm = res.atmAccount
    if (res.atmExpiry) query.atmExpiry = res.atmExpiry

    await navigateTo({ path: '/Order/Success', query })
  } catch (e: unknown) {
    const err = e as { data?: { message?: string } }
    submitError.value = err?.data?.message ?? '訂單送出失敗，請稍後再試。'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <main id="main">
    <section class="tallsection clr">
      <div class="restrict-wide">
        <div class="centered">
          <h1>結帳</h1>
        </div>

        <div v-if="cartStore.items.length === 0" class="centered allpadding">
          <p>購物車是空的。</p>
          <a href="/Products" class="btn outline-btn solidhover" style="margin-top:1rem; display:inline-block;">前往選購</a>
        </div>

        <form v-else @submit.prevent="submitOrder" style="display:grid; gap:2rem; grid-template-columns:1fr 360px; align-items:start;">

          <!-- ── Left column: form fields ─────────────────────────────────── -->
          <div>

            <!-- 訂購人 -->
            <fieldset style="border:1px solid #ddd; padding:1rem; margin-bottom:1.5rem;">
              <legend style="padding:0 0.5rem; font-weight:bold;">訂購人資料</legend>
              <div class="form-row" style="margin-bottom:0.75rem;">
                <label>姓名 <span style="color:red;">*</span></label>
                <input v-model="form.buyerName" required class="form-input" style="width:100%;" />
              </div>
              <div class="form-row" style="margin-bottom:0.75rem;">
                <label>手機 <span style="color:red;">*</span></label>
                <input v-model="form.buyerMobile" required class="form-input" style="width:100%;" />
              </div>
              <div class="form-row">
                <label>Email</label>
                <input v-model="form.buyerEmail" type="email" class="form-input" style="width:100%;" />
              </div>
            </fieldset>

            <!-- 收件人 -->
            <fieldset style="border:1px solid #ddd; padding:1rem; margin-bottom:1.5rem;">
              <legend style="padding:0 0.5rem; font-weight:bold;">收件人資料</legend>
              <div class="form-row" style="margin-bottom:0.75rem;">
                <label>姓名 <span style="color:red;">*</span></label>
                <input v-model="form.receiverName" required class="form-input" style="width:100%;" />
              </div>
              <div class="form-row" style="margin-bottom:0.75rem;">
                <label>手機 <span style="color:red;">*</span></label>
                <input v-model="form.receiverMobile" required class="form-input" style="width:100%;" />
              </div>
              <div class="form-row" style="margin-bottom:0.75rem;">
                <label>郵遞區號</label>
                <input v-model.number="form.receiverZipcodeId" type="number" class="form-input" style="width:120px;" />
              </div>
              <div class="form-row" style="margin-bottom:0.75rem;">
                <label>地址 <span style="color:red;">*</span></label>
                <input v-model="form.receiverAddress" required class="form-input" style="width:100%;" />
              </div>
              <div class="form-row">
                <label>配送時段</label>
                <select v-model.number="form.receiverTime" class="form-input">
                  <option :value="0">不指定</option>
                  <option :value="1">上午（9:00–13:00）</option>
                  <option :value="2">下午（14:00–18:00）</option>
                </select>
              </div>
            </fieldset>

            <!-- 付款方式 -->
            <fieldset style="border:1px solid #ddd; padding:1rem; margin-bottom:1.5rem;">
              <legend style="padding:0 0.5rem; font-weight:bold;">付款方式</legend>
              <label style="display:block; margin-bottom:0.5rem;">
                <input type="radio" v-model.number="form.payType" :value="1" /> 信用卡線上付款
              </label>
              <label style="display:block; margin-bottom:0.5rem;">
                <input type="radio" v-model.number="form.payType" :value="2" /> ATM 轉帳
              </label>
              <label style="display:block;">
                <input type="radio" v-model.number="form.payType" :value="3" /> 宅配貨到付款
              </label>
            </fieldset>

            <!-- 發票 -->
            <fieldset style="border:1px solid #ddd; padding:1rem; margin-bottom:1.5rem;">
              <legend style="padding:0 0.5rem; font-weight:bold;">發票資訊</legend>
              <label style="display:block; margin-bottom:0.5rem;">
                <input type="radio" v-model.number="form.invoiceType" :value="1" /> 電子信箱
              </label>
              <div v-if="form.invoiceType === 1" style="margin-left:1.5rem; margin-bottom:0.75rem;">
                <input v-model="form.invoiceEmail" type="email" placeholder="請輸入 Email" class="form-input" style="width:100%;" />
              </div>
              <label style="display:block; margin-bottom:0.5rem;">
                <input type="radio" v-model.number="form.invoiceType" :value="2" /> 公司行號
              </label>
              <template v-if="form.invoiceType === 2">
                <div style="margin-left:1.5rem; margin-bottom:0.5rem;">
                  <input v-model="form.companyTitle" placeholder="公司名稱" class="form-input" style="width:100%;" />
                </div>
                <div style="margin-left:1.5rem; margin-bottom:0.75rem;">
                  <input v-model="form.companyNumber" placeholder="統一編號" class="form-input" style="width:180px;" />
                </div>
              </template>
              <label style="display:block; margin-bottom:0.5rem;">
                <input type="radio" v-model.number="form.invoiceType" :value="3" /> 捐贈
              </label>
              <div v-if="form.invoiceType === 3" style="margin-left:1.5rem;">
                <input v-model="form.loveCode" placeholder="愛心碼" class="form-input" style="width:180px;" />
              </div>
            </fieldset>

            <!-- 折扣碼 -->
            <fieldset style="border:1px solid #ddd; padding:1rem; margin-bottom:1.5rem;">
              <legend style="padding:0 0.5rem; font-weight:bold;">折扣碼</legend>
              <div style="display:flex; gap:0.5rem; align-items:center;">
                <input v-model="form.discountCode" placeholder="輸入折扣碼" class="form-input" style="flex:1;" />
                <button
                  type="button"
                  @click="applyDiscount"
                  :disabled="validatingDiscount"
                  class="btn outline-btn"
                >驗證</button>
              </div>
              <p v-if="discountError" style="color:red; margin-top:0.5rem;">{{ discountError }}</p>
              <p v-if="discountId" style="color:green; margin-top:0.5rem;">
                折扣已套用：折抵 {{ ntd(discountAmount) }}
              </p>
            </fieldset>

            <!-- 備註 -->
            <fieldset style="border:1px solid #ddd; padding:1rem; margin-bottom:1.5rem;">
              <legend style="padding:0 0.5rem; font-weight:bold;">備註</legend>
              <textarea v-model="form.remark" rows="3" class="form-input" style="width:100%;"></textarea>
            </fieldset>
          </div>

          <!-- ── Right column: order summary ─────────────────────────────── -->
          <div style="position:sticky; top:1rem;">
            <div style="border:1px solid #ddd; padding:1.25rem;">
              <h3 style="margin-top:0;">訂單摘要</h3>
              <table style="width:100%; border-collapse:collapse; margin-bottom:1rem;">
                <tbody>
                  <tr v-for="item in cartStore.items" :key="item.productId" style="border-bottom:1px solid #eee;">
                    <td style="padding:0.5rem 0; font-size:0.9rem;">{{ item.title }} ×{{ item.quantity }}</td>
                    <td style="padding:0.5rem 0; text-align:right; font-size:0.9rem;">{{ ntd(item.unitPrice * item.quantity) }}</td>
                  </tr>
                </tbody>
              </table>
              <table style="width:100%; border-collapse:collapse;">
                <tbody>
                  <tr>
                    <td style="padding:0.4rem 0;">小計</td>
                    <td style="text-align:right;">{{ ntd(cartStore.subtotal) }}</td>
                  </tr>
                  <tr>
                    <td style="padding:0.4rem 0;">運費</td>
                    <td style="text-align:right;">
                      <span v-if="freight === 0" style="color:green;">免運</span>
                      <span v-else>{{ ntd(freight) }}</span>
                    </td>
                  </tr>
                  <tr v-if="discountAmount > 0">
                    <td style="padding:0.4rem 0;">折扣</td>
                    <td style="text-align:right; color:green;">-{{ ntd(discountAmount) }}</td>
                  </tr>
                  <tr style="border-top:2px solid #ccc; font-weight:bold;">
                    <td style="padding:0.75rem 0;">總計</td>
                    <td style="text-align:right;">{{ ntd(total) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <p v-if="submitError" style="color:red; margin-top:0.75rem;">{{ submitError }}</p>

            <button
              type="submit"
              :disabled="submitting"
              class="btn outline-btn solidhover"
              style="width:100%; margin-top:1rem;"
            >
              {{ submitting ? '送出中…' : '確認訂單' }}
            </button>
            <a href="/Cart" class="btn outline-btn" style="width:100%; display:block; text-align:center; margin-top:0.5rem;">返回購物車</a>
          </div>
        </form>
      </div>
    </section>
  </main>
</template>

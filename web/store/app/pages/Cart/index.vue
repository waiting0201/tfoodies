<script setup lang="ts">
// Shopping cart. Rebuilt on the legacy `.checkout-cart` design system (main.css) so it
// inherits the site's teal branding, the header/total bars and the responsive `data-label`
// row collapse — same content as before (items · qty · price · subtotal · freight · total ·
// 繼續購物 / 前往結帳), just on-brand and mobile-friendly.
useHead({ title: '購物車' })

const cartStore = useCartStore()
const blobUrl = String(useRuntimeConfig().public.blobUrl)
onMounted(() => cartStore.hydrate())

const FREIGHT_THRESHOLD = 2000
const FREIGHT_FEE = 180

const freight = computed(() => (cartStore.subtotal >= FREIGHT_THRESHOLD ? 0 : FREIGHT_FEE))
const total = computed(() => cartStore.subtotal + freight.value)
const freeShipRemaining = computed(() => Math.max(0, FREIGHT_THRESHOLD - cartStore.subtotal))
const freeShipProgress = computed(() =>
  Math.min(100, Math.round((cartStore.subtotal / FREIGHT_THRESHOLD) * 100)),
)

const ntd = (n: number) => 'NT$ ' + new Intl.NumberFormat('zh-TW').format(Math.trunc(n))
const productUrl = (title: string) => `/Product/${titleToUrlSlug(title)}`

const dec = (it: { productId: string; quantity: number }) =>
  cartStore.updateQty(it.productId, it.quantity - 1)
const inc = (it: { productId: string; quantity: number }) =>
  cartStore.updateQty(it.productId, it.quantity + 1)
const onQtyInput = (productId: string, e: Event) => {
  const n = Math.floor(Number((e.target as HTMLInputElement).value))
  cartStore.updateQty(productId, Number.isFinite(n) && n >= 1 ? n : 1)
}
</script>

<template>
  <main id="main">
    <section class="tallsection clr">
      <div class="restrict-wide allpadding">
        <div class="centered">
          <h1>購物車</h1>
          <div class="direct-line"></div>
        </div>

        <!-- Empty -->
        <div v-if="cartStore.items.length === 0" class="cart-empty centered">
          <div class="cart-empty-icon"></div>
          <p class="cart-empty-text">您的購物車目前是空的</p>
          <a href="/Products" class="btn basic">前往選購</a>
        </div>

        <!-- Items + summary -->
        <div v-else class="checkout-cart">
          <!-- Free-shipping progress -->
          <div class="freeship-bar" :class="{ qualified: freight === 0 }">
            <template v-if="freight === 0">
              <span class="freeship-tick">✓</span> 已符合免運門檻，本次訂單享 <strong>免運費</strong>！
            </template>
            <template v-else>
              再購買 <strong>{{ ntd(freeShipRemaining) }}</strong> 即可享 <strong>免運費</strong>（消費滿 {{ ntd(FREIGHT_THRESHOLD) }}）
            </template>
            <div class="freeship-track"><div class="freeship-fill" :style="{ width: freeShipProgress + '%' }"></div></div>
          </div>

          <!-- Items -->
          <div class="cart_title">
            <div class="label_info">商品</div>
            <div class="label_price">單價</div>
            <div class="label_quantity">數量／小計</div>
            <div class="label_remove">移除</div>
          </div>

          <ul class="cart-items">
            <li v-for="item in cartStore.items" :key="item.productId" class="item">
              <a :href="productUrl(item.title)" class="thumb">
                <img v-if="item.photo" :src="blobUrl + item.photo" :alt="item.title">
                <span v-else class="thumb-fallback"></span>
              </a>
              <div class="item-content">
                <div class="ci-info">
                  <a :href="productUrl(item.title)" class="ci-name">{{ item.title }}</a>
                  <div v-if="item.capacity" class="ci-description descript">{{ item.capacity }}</div>
                </div>
                <div class="ci-price" data-label="單價：">{{ ntd(item.unitPrice) }}</div>
                <div class="ci-quantity" data-label="數量：">
                  <div class="quantity-wrap">
                    <div class="quantity clr">
                      <div class="qty-minus"><a href="javascript:;" class="minus" @click.prevent="dec(item)">−</a></div>
                      <div class="qtyinput">
                        <input
                          type="text"
                          inputmode="numeric"
                          class="input form-input qty"
                          :value="item.quantity"
                          @input="onQtyInput(item.productId, $event)"
                        >
                      </div>
                      <div class="qty-plus"><a href="javascript:;" class="plus" @click.prevent="inc(item)">+</a></div>
                    </div>
                  </div>
                  <div class="ci-linetotal" data-label="小計：">{{ ntd(item.unitPrice * item.quantity) }}</div>
                </div>
              </div>
              <div class="remove">
                <a href="javascript:;" class="remove-x" title="移除" @click.prevent="cartStore.remove(item.productId)">✕</a>
              </div>
            </li>
          </ul>

          <!-- Summary -->
          <div class="cart-summary">
            <ul class="summary-items">
              <li class="item">
                <div class="label">商品小計</div>
                <div class="price">{{ ntd(cartStore.subtotal) }}</div>
              </li>
              <li class="item">
                <div class="label">
                  運費
                  <small v-if="freight === 0" class="freeship-note">滿 {{ ntd(FREIGHT_THRESHOLD) }} 免運</small>
                </div>
                <div class="price">
                  <span v-if="freight === 0" class="freeship-note">免運</span>
                  <span v-else>{{ ntd(freight) }}</span>
                </div>
              </li>
            </ul>
            <div class="total-calculation">
              <label>應付金額</label>
              <div class="total-amount">{{ ntd(total) }}</div>
            </div>
          </div>

          <!-- Actions -->
          <div class="cart-actions">
            <a href="/Products" class="outline-btn solidhover">繼續購物</a>
            <a href="/Checkout" class="btn basic">前往結帳</a>
          </div>
        </div>
      </div>
    </section>
  </main>
</template>

<style scoped>
/* Refined ("秀氣") treatment over the legacy .checkout-cart structure: the heavy filled-teal
   header/total bars are softened into hairline-ruled rows with teal accents, lighter type and
   more whitespace. Scoped overrides of legacy globals are written with the `.checkout-cart`
   prefix so they out-specify main.css. Brand teal #26b7bc / dark #156467. */
.checkout-cart { max-width: 760px; margin: 0 auto; }

/* Free-shipping progress — slim, understated */
.freeship-bar {
  background: none;
  border: 0;
  border-bottom: 1px solid #f0f0f0;
  color: #8a8a8a;
  padding: 0 .2em 1.2em;
  margin-bottom: 1.6em;
  font-size: .85em;
  letter-spacing: .02em;
}
.freeship-bar strong { color: #26b7bc; font-weight: 600; }
.freeship-bar.qualified { color: #7c9a1e; }
.freeship-bar.qualified strong { color: #7c9a1e; }
.freeship-tick {
  display: inline-block; width: 1.25em; height: 1.25em; line-height: 1.25em; text-align: center;
  border-radius: 50%; background: #95ad25; color: #fff; font-size: .75em; margin-right: .25em;
}
.freeship-track { height: 3px; background: #f0f0f0; border-radius: 2px; margin-top: .8em; overflow: hidden; }
.freeship-fill { height: 100%; background: #26b7bc; border-radius: 2px; transition: width .45s ease; }
.freeship-bar.qualified .freeship-fill { background: #95ad25; }
.freeship-note { color: #95ad25; }

/* Column header — soft grey labels instead of the filled teal bar */
.checkout-cart .cart_title {
  background: none; color: #b3b3b3; padding: .4em .2em .8em;
  border-bottom: 1px solid #ececec; font-size: .78em; font-weight: 400; letter-spacing: .12em;
}

/* Item rows */
.checkout-cart .cart-items .item { align-items: center; padding: 1.4em .2em; border-bottom: 1px solid #f4f4f4; }
.thumb { display: block; border: 1px solid #f0f0f0; border-radius: 4px; overflow: hidden; }
.thumb img { transition: transform .4s ease; display: block; }
.thumb:hover img { transform: scale(1.06); }
.thumb-fallback { display: block; width: 100%; height: 90px; background: #f6f6f6; }
.checkout-cart .cart-items .item .ci-info .ci-name { font-size: 1.02em; line-height: 1.4; }
.ci-name { color: #4a4a4a; font-weight: 400; transition: color .25s; }
.ci-name:hover { color: #26b7bc; }
.ci-description { font-size: .85em; color: #aaa; margin-top: .35em; }
.ci-price { color: #888; font-size: .95em; }
.ci-linetotal { margin-top: .5em; color: #1d8e92; font-weight: 500; font-size: .9em; letter-spacing: .02em; }

/* Quantity stepper — lighter, white, thin-bordered. Flexbox (not the legacy floats) so the
   −, input and + share one baseline and identical heights with a single seamless border.
   Desktop (≥768, legacy column layout): stepper + 小計 stacked centred in the 數量 column.
   Mobile: left-aligned to match the 單價 / 小計 rows (see the max-width block below). */
@media (min-width: 768px) {
  .ci-quantity { display: flex; flex-direction: column; align-items: center; }
}
.checkout-cart .quantity { display: inline-flex; align-items: stretch; height: 32px; }
.checkout-cart .qty-minus, .checkout-cart .qty-plus {
  float: none; display: flex; align-items: center; justify-content: center;
  width: 30px; height: 32px; box-sizing: border-box; background: #fff; border: 1px solid #e6e6e6;
}
.checkout-cart .qty-minus { border-radius: 3px 0 0 3px; border-right: 0; }
.checkout-cart .qty-plus { border-radius: 0 3px 3px 0; border-left: 0; }
.checkout-cart .qty-minus a, .checkout-cart .qty-plus a {
  display: block; width: 100%; height: 100%; line-height: 30px; text-align: center;
  color: #9b9b9b; font-size: 1.05em; text-decoration: none;
}
.checkout-cart .qty-minus a:hover, .checkout-cart .qty-plus a:hover { color: #26b7bc; }
.checkout-cart .qtyinput { float: none; width: 42px; height: 32px; }
.qtyinput .qty {
  display: block; width: 100%; height: 32px; box-sizing: border-box; text-align: center; color: #555; font-size: .95em;
  border: 1px solid #e6e6e6; border-radius: 0; padding: 0; -moz-appearance: textfield; background: #fff; line-height: 30px;
}
.qtyinput .qty::-webkit-outer-spin-button, .qtyinput .qty::-webkit-inner-spin-button { -webkit-appearance: none; margin: 0; }

/* Remove — quiet by default, teal on hover */
.remove-x {
  display: inline-block; width: 26px; height: 26px; line-height: 24px; text-align: center;
  border: 1px solid #ececec; border-radius: 50%; color: #cfcfcf; text-decoration: none; font-size: .72em;
  transition: all .25s;
}
.remove-x:hover { border-color: #26b7bc; color: #26b7bc; }

/* Summary — compact receipt aligned right */
.cart-summary { margin: 2.2em 0 0 auto; max-width: 320px; }
.checkout-cart .cart-summary .summary-items { padding: 0 .2em; }
.checkout-cart .cart-summary .summary-items .item { color: #8a8a8a; margin-bottom: .9em; }
.checkout-cart .cart-summary .summary-items .label { color: #8a8a8a; }
.checkout-cart .cart-summary .summary-items .price { color: #555; font-weight: 400; font-size: 1em; }
/* Total — hairline-ruled row with a teal amount, not a filled block */
.checkout-cart .cart-summary .total-calculation {
  background: none; color: #444; border-top: 1px solid #ececec; margin-top: .4em;
  padding: 1em .2em 0; font-size: 1em; font-weight: 400;
}
.checkout-cart .cart-summary .total-calculation .total-amount { color: #1d8e92; font-size: 1.5em; }

/* Actions — both buttons identical size (clear legacy .btn side-margin + give the outline
   button the same min-width so 繼續購物 / 前往結帳 match). */
.cart-actions { display: flex; gap: .8em; justify-content: flex-end; margin-top: 2em; flex-wrap: wrap; }
/* Fixed height + flex centring so .outline-btn (1px border) and .btn (no border) end up the
   exact same size; min-width keeps them equal width on desktop. */
.cart-actions a {
  min-width: 150px; height: 46px; margin: 0; padding: 0 2em; box-sizing: border-box;
  display: inline-flex; align-items: center; justify-content: center; text-align: center;
}

/* Empty state */
.cart-empty { padding: 3.5em 1em; }
.cart-empty-icon {
  width: 84px; height: 84px; margin: 0 auto 1.2em; border-radius: 50%;
  background: #f7fcfc url(/content/images/common/freeshipping.gif) center/38px no-repeat;
  border: 1px solid #e3f1f1;
}
.cart-empty-text { color: #a8a8a8; font-size: 1.05em; margin-bottom: 1.6em; letter-spacing: .03em; }

/* Mobile: full-width buttons + full-width summary; quantity row left-aligned like 單價/小計 */
@media (max-width: 767px) {
  .cart-summary { max-width: none; }
  /* stack full-width so both buttons are exactly equal (and consistent with checkout) */
  .cart-actions { flex-direction: column; }
  .cart-actions a { width: 100%; }
  /* 數量： label vertically centred with the stepper; 小計 drops to its own row */
  .checkout-cart .cart-items .item .ci-quantity { display: flex; align-items: center; flex-wrap: wrap; }
  .checkout-cart .cart-items .item .ci-quantity .quantity-wrap { margin-right: .5em; }
  .checkout-cart .cart-items .item .ci-quantity .ci-linetotal { width: 100%; margin-top: .5em; }
}
</style>

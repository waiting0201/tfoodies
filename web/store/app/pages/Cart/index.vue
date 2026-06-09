<script setup lang="ts">
useHead({ title: '購物車' })

const cartStore = useCartStore()
onMounted(() => cartStore.hydrate())

const FREIGHT_THRESHOLD = 2000
const FREIGHT_FEE = 120

const freight = computed(() =>
  cartStore.subtotal >= FREIGHT_THRESHOLD ? 0 : FREIGHT_FEE,
)
const total = computed(() => cartStore.subtotal + freight.value)

const ntd = (n: number) => 'NT$ ' + new Intl.NumberFormat('zh-TW').format(Math.trunc(n))
</script>

<template>
  <main id="main">
    <section class="tallsection clr">
      <div class="restrict-wide">
        <div class="centered">
          <h1>購物車</h1>
        </div>

        <template v-if="cartStore.items.length === 0">
          <div class="centered allpadding">
            <p>購物車目前沒有商品。</p>
            <a href="/Products" class="btn outline-btn solidhover" style="margin-top:1.5rem; display:inline-block;">
              前往選購
            </a>
          </div>
        </template>

        <template v-else>
          <div class="cart-table-wrap" style="overflow-x:auto;">
            <table style="width:100%; border-collapse:collapse; margin-bottom:1.5rem;">
              <thead>
                <tr style="border-bottom:2px solid #ccc; text-align:left;">
                  <th style="padding:0.75rem;">商品</th>
                  <th style="padding:0.75rem; text-align:center;">單價</th>
                  <th style="padding:0.75rem; text-align:center;">數量</th>
                  <th style="padding:0.75rem; text-align:right;">小計</th>
                  <th style="padding:0.75rem;"></th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="item in cartStore.items"
                  :key="item.productId"
                  style="border-bottom:1px solid #eee;"
                >
                  <td style="padding:0.75rem; display:flex; align-items:center; gap:0.75rem;">
                    <!-- photo placeholder -->
                    <div style="width:60px; height:60px; background:#f4f4f4; border:1px solid #ddd; flex-shrink:0;"></div>
                    <span>{{ item.title }}</span>
                  </td>
                  <td style="padding:0.75rem; text-align:center;">{{ ntd(item.unitPrice) }}</td>
                  <td style="padding:0.75rem; text-align:center;">
                    <input
                      type="number"
                      min="1"
                      :value="item.quantity"
                      @change="cartStore.updateQty(item.productId, Number(($event.target as HTMLInputElement).value))"
                      style="width:60px; text-align:center; border:1px solid #ccc; padding:0.25rem;"
                    />
                  </td>
                  <td style="padding:0.75rem; text-align:right;">{{ ntd(item.unitPrice * item.quantity) }}</td>
                  <td style="padding:0.75rem; text-align:center;">
                    <button
                      @click="cartStore.remove(item.productId)"
                      style="background:none; border:1px solid #ccc; cursor:pointer; padding:0.25rem 0.5rem;"
                      title="移除"
                    >✕</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Totals -->
          <div style="max-width:360px; margin-left:auto;">
            <table style="width:100%; border-collapse:collapse;">
              <tbody>
                <tr>
                  <td style="padding:0.5rem 0;">商品小計</td>
                  <td style="padding:0.5rem 0; text-align:right;">{{ ntd(cartStore.subtotal) }}</td>
                </tr>
                <tr>
                  <td style="padding:0.5rem 0;">
                    運費
                    <small v-if="freight === 0" style="color:green;">（滿 2000 免運）</small>
                  </td>
                  <td style="padding:0.5rem 0; text-align:right;">
                    <span v-if="freight === 0" style="color:green;">免運</span>
                    <span v-else>{{ ntd(freight) }}</span>
                  </td>
                </tr>
                <tr style="border-top:2px solid #ccc; font-weight:bold;">
                  <td style="padding:0.75rem 0;">合計</td>
                  <td style="padding:0.75rem 0; text-align:right;">{{ ntd(total) }}</td>
                </tr>
              </tbody>
            </table>

            <div style="display:flex; gap:1rem; margin-top:1.5rem; justify-content:flex-end; flex-wrap:wrap;">
              <a href="/Products" class="btn outline-btn solidhover">繼續購物</a>
              <a
                :href="cartStore.items.length ? '/Checkout' : undefined"
                class="btn outline-btn solidhover"
                :style="cartStore.items.length === 0 ? 'opacity:.4; pointer-events:none;' : ''"
              >前往結帳</a>
            </div>
          </div>
        </template>
      </div>
    </section>
  </main>
</template>

<script setup lang="ts">
const config = useRuntimeConfig()
const memberAuth = useMemberAuthStore()
const route = useRoute()

const code = computed(() => String(route.params.code ?? ''))

useHead(() => ({ title: `訂單 ${code.value}` }))

if (!memberAuth.isAuthenticated) {
  await navigateTo('/Member/Login')
}

interface OrderItem {
  productId: string
  title: string
  unitPrice: number
  quantity: number
  subtotal: number
}
interface OrderDetail {
  orderCode: string
  orderDate: string
  buyerName: string
  buyerMobile: string
  receiverName: string
  receiverMobile: string
  receiverAddress: string
  shipStatus: string
  payStatus: string
  payType: string
  freight: number
  discount: number
  total: number
  items: OrderItem[]
  atmAccount?: string
  atmExpiry?: string
}

const { data, pending, error } = await useFetch<OrderDetail>(
  () => `${config.public.apiBase}/member/orders/${code.value}`,
  {
    key: `member-order-${code.value}`,
    headers: computed(() =>
      memberAuth.accessToken
        ? { Authorization: `Bearer ${memberAuth.accessToken}` }
        : {},
    ),
    default: (): OrderDetail => ({
      orderCode: '',
      orderDate: '',
      buyerName: '',
      buyerMobile: '',
      receiverName: '',
      receiverMobile: '',
      receiverAddress: '',
      shipStatus: '',
      payStatus: '',
      payType: '',
      freight: 0,
      discount: 0,
      total: 0,
      items: [],
    }),
  },
)

const ntd = (n: number) => 'NT$ ' + new Intl.NumberFormat('zh-TW').format(Math.trunc(n))
</script>

<template>
  <main id="main">
    <section class="tallsection clr">
      <div class="restrict-wide">
        <div style="margin-bottom:1rem;">
          <a href="/Member/Orders" style="text-decoration:underline;">← 返回訂單列表</a>
        </div>

        <h1>訂單詳情</h1>

        <div v-if="pending" class="allpadding centered">載入中…</div>
        <div v-else-if="error" class="allpadding" style="color:red;">無法載入訂單資料。</div>

        <template v-else-if="data.orderCode">
          <!-- Order header info -->
          <div style="display:grid; grid-template-columns:1fr 1fr; gap:1.5rem; margin-top:1.5rem; flex-wrap:wrap;">
            <div>
              <h3 style="margin-top:0;">訂單資訊</h3>
              <p><strong>訂單編號：</strong>{{ data.orderCode }}</p>
              <p><strong>訂購日期：</strong>{{ data.orderDate }}</p>
              <p><strong>付款方式：</strong>{{ data.payType }}</p>
              <p><strong>付款狀態：</strong>{{ data.payStatus }}</p>
              <p><strong>出貨狀態：</strong>{{ data.shipStatus }}</p>
            </div>
            <div>
              <h3 style="margin-top:0;">收件資訊</h3>
              <p><strong>訂購人：</strong>{{ data.buyerName }} / {{ data.buyerMobile }}</p>
              <p><strong>收件人：</strong>{{ data.receiverName }} / {{ data.receiverMobile }}</p>
              <p><strong>地址：</strong>{{ data.receiverAddress }}</p>
            </div>
          </div>

          <!-- ATM info if applicable -->
          <template v-if="data.atmAccount">
            <div style="border:1px solid #ccc; padding:1rem; margin-top:1rem; background:#fafafa; display:inline-block;">
              <h3 style="margin-top:0;">ATM 轉帳資訊</h3>
              <p><strong>虛擬帳號：</strong>{{ data.atmAccount }}</p>
              <p v-if="data.atmExpiry"><strong>繳費期限：</strong>{{ data.atmExpiry }}</p>
            </div>
          </template>

          <!-- Items table -->
          <div style="overflow-x:auto; margin-top:1.5rem;">
            <table style="width:100%; border-collapse:collapse;">
              <thead>
                <tr style="border-bottom:2px solid #ccc; text-align:left;">
                  <th style="padding:0.75rem;">商品</th>
                  <th style="padding:0.75rem; text-align:center;">單價</th>
                  <th style="padding:0.75rem; text-align:center;">數量</th>
                  <th style="padding:0.75rem; text-align:right;">小計</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="item in data.items"
                  :key="item.productId"
                  style="border-bottom:1px solid #eee;"
                >
                  <td style="padding:0.75rem;">{{ item.title }}</td>
                  <td style="padding:0.75rem; text-align:center;">{{ ntd(item.unitPrice) }}</td>
                  <td style="padding:0.75rem; text-align:center;">{{ item.quantity }}</td>
                  <td style="padding:0.75rem; text-align:right;">{{ ntd(item.subtotal) }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Totals -->
          <div style="max-width:320px; margin-left:auto; margin-top:1rem;">
            <table style="width:100%; border-collapse:collapse;">
              <tbody>
                <tr>
                  <td style="padding:0.4rem 0;">運費</td>
                  <td style="text-align:right;">{{ ntd(data.freight) }}</td>
                </tr>
                <tr v-if="data.discount > 0">
                  <td style="padding:0.4rem 0;">折扣</td>
                  <td style="text-align:right; color:green;">-{{ ntd(data.discount) }}</td>
                </tr>
                <tr style="border-top:2px solid #ccc; font-weight:bold;">
                  <td style="padding:0.75rem 0;">合計</td>
                  <td style="text-align:right;">{{ ntd(data.total) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </template>
      </div>
    </section>
  </main>
</template>

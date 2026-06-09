<script setup lang="ts">
useHead({ title: '我的訂單' })

const config = useRuntimeConfig()
const memberAuth = useMemberAuthStore()

if (!memberAuth.isAuthenticated) {
  await navigateTo('/Member/Login')
}

interface OrderRow {
  orderCode: string
  orderDate: string
  total: number
  shipStatus: string
  payStatus: string
}
interface OrdersResponse {
  orders: OrderRow[]
  totalCount: number
}

const page = ref(1)
const pageSize = 10

const { data, pending, error } = await useFetch<OrdersResponse>(
  () => `${config.public.apiBase}/member/orders?page=${page.value}&pageSize=${pageSize}`,
  {
    key: 'member-orders',
    headers: computed(() =>
      memberAuth.accessToken
        ? { Authorization: `Bearer ${memberAuth.accessToken}` }
        : {},
    ),
    default: (): OrdersResponse => ({ orders: [], totalCount: 0 }),
  },
)
</script>

<template>
  <main id="main">
    <section class="tallsection clr">
      <div class="restrict-wide">
        <h1>我的訂單</h1>

        <div v-if="pending" class="centered allpadding">載入中…</div>
        <div v-else-if="error" class="centered allpadding" style="color:red;">無法載入訂單資料。</div>
        <template v-else>
          <div v-if="data.orders.length === 0" class="allpadding">
            <p>目前沒有訂單紀錄。</p>
            <a href="/Products" class="btn outline-btn solidhover" style="display:inline-block; margin-top:1rem;">前往選購</a>
          </div>
          <div v-else style="overflow-x:auto;">
            <table style="width:100%; border-collapse:collapse; margin-top:1rem;">
              <thead>
                <tr style="border-bottom:2px solid #ccc; text-align:left;">
                  <th style="padding:0.75rem;">訂單編號</th>
                  <th style="padding:0.75rem;">日期</th>
                  <th style="padding:0.75rem; text-align:right;">總額</th>
                  <th style="padding:0.75rem; text-align:center;">出貨狀態</th>
                  <th style="padding:0.75rem; text-align:center;">付款狀態</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="order in data.orders"
                  :key="order.orderCode"
                  style="border-bottom:1px solid #eee; cursor:pointer;"
                  @click="navigateTo(`/Member/Orders/${order.orderCode}`)"
                >
                  <td style="padding:0.75rem;">
                    <a :href="`/Member/Orders/${order.orderCode}`" style="text-decoration:underline;">
                      {{ order.orderCode }}
                    </a>
                  </td>
                  <td style="padding:0.75rem;">{{ order.orderDate }}</td>
                  <td style="padding:0.75rem; text-align:right;">
                    NT$ {{ new Intl.NumberFormat('zh-TW').format(order.total) }}
                  </td>
                  <td style="padding:0.75rem; text-align:center;">{{ order.shipStatus }}</td>
                  <td style="padding:0.75rem; text-align:center;">{{ order.payStatus }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </template>

        <div style="margin-top:1.5rem;">
          <a href="/Member/Center" class="btn outline-btn">返回會員中心</a>
        </div>
      </div>
    </section>
  </main>
</template>

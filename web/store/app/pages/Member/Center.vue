<script setup lang="ts">
useHead({ title: '會員中心 - 我的訂單' })

const config = useRuntimeConfig()
const memberAuth = useMemberAuthStore()

if (!memberAuth.isAuthenticated) {
  await navigateTo('/Member/Login')
}

// API 回傳 PaginatedResponse：{ items, totalCount, ... }，狀態為列舉整數（無 string 轉換）。
interface OrderRow {
  orderId: string
  orderCode: string
  orderDate: string
  total: number
  payStatus: number
  deliverStatus: number
}
interface OrdersResponse {
  items: OrderRow[]
  totalCount: number
}

// 對齊 Domain/Enums/Enums.cs 的 PayStatus / DeliverStatus 編碼。
const payStatusLabels: Record<number, string> = {
  0: '未付款', 1: '已付款', 2: '退款', 3: '免付款', 4: '取消',
}
const deliverStatusLabels: Record<number, string> = {
  0: '未出貨', 1: '已出貨', 2: '退貨', 3: '取消', 4: '待出貨',
}

const page = ref(1)
const pageSize = 10

const { data, pending, error } = await useFetch<OrdersResponse>(
  () => `${config.public.apiBase}/member/orders?page=${page.value}&pageSize=${pageSize}`,
  {
    key: 'member-center-orders',
    headers: computed<Record<string, string>>(() => {
      const h: Record<string, string> = {}
      if (memberAuth.accessToken) h.Authorization = `Bearer ${memberAuth.accessToken}`
      return h
    }),
    default: (): OrdersResponse => ({ items: [], totalCount: 0 }),
  },
)

const totalPages = computed(() => Math.max(1, Math.ceil((data.value?.totalCount ?? 0) / pageSize)))
const nf = new Intl.NumberFormat('zh-TW')
</script>

<template>
  <MemberShell>
    <h2 class="mc-title">我的訂單</h2>

    <div v-if="pending" class="mc-state">載入中…</div>
    <div v-else-if="error" class="mc-state err">無法載入訂單資料。</div>

    <template v-else>
      <div v-if="!data?.items?.length" class="mc-empty">
        <p>目前沒有訂單紀錄。</p>
        <NuxtLink to="/Products" class="mc-btn">前往選購</NuxtLink>
      </div>

      <div v-else class="mc-table-scroll">
        <table class="mc-table">
          <thead>
            <tr>
              <th class="ac">訂單編號</th>
              <th class="ac">日期</th>
              <th class="ar">總額</th>
              <th class="ac">出貨狀態</th>
              <th class="ac">付款狀態</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="order in data.items"
              :key="order.orderCode"
              class="row-link"
              @click="navigateTo(`/Member/Orders/${order.orderCode}`)"
            >
              <td class="code ac">{{ order.orderCode }}</td>
              <td class="ac">{{ order.orderDate }}</td>
              <td class="ar">NT$ {{ nf.format(order.total) }}</td>
              <td class="ac"><span class="pill">{{ deliverStatusLabels[order.deliverStatus] ?? '—' }}</span></td>
              <td class="ac"><span class="pill">{{ payStatusLabels[order.payStatus] ?? '—' }}</span></td>
            </tr>
          </tbody>
        </table>

        <div v-if="totalPages > 1" class="mc-pager">
          <button :disabled="page <= 1" @click="page--">上一頁</button>
          <span>{{ page }} / {{ totalPages }}</span>
          <button :disabled="page >= totalPages" @click="page++">下一頁</button>
        </div>
      </div>
    </template>
  </MemberShell>
</template>

<style scoped>
.mc-title {
  margin: 0 0 1.25rem;
  font-size: 1.2rem;
  letter-spacing: 0.06em;
  color: #156467;
}

.mc-state {
  padding: 2rem 0;
  text-align: center;
  color: #9b9b9b;
}

.mc-state.err {
  color: #d0021b;
}

.mc-empty {
  padding: 2.5rem 0;
  text-align: center;
  color: #777;
}

.mc-btn {
  display: inline-block;
  margin-top: 1.1rem;
  padding: 0.6rem 1.4rem;
  font-size: 0.9rem;
  letter-spacing: 0.08em;
  color: #fff;
  text-decoration: none;
  background: #26b7bc;
  border-radius: 8px;
  transition: background 0.18s;
}

.mc-btn:hover {
  background: #1d8e92;
}

.mc-table-scroll {
  overflow-x: auto;
}

.mc-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.mc-table th {
  padding: 0.7rem 0.75rem;
  text-align: left;
  font-weight: 600;
  color: #fff;
  background: #26b7bc;
  white-space: nowrap;
}

.mc-table thead th:first-child {
  border-top-left-radius: 8px;
  border-bottom-left-radius: 8px;
}

.mc-table thead th:last-child {
  border-top-right-radius: 8px;
  border-bottom-right-radius: 8px;
}

.mc-table td {
  padding: 0.85rem 0.75rem;
  border-bottom: 1px solid #f3f3f3;
  color: #3e3e3e;
}

.mc-table .ar { text-align: right; }
.mc-table .ac { text-align: center; }
.mc-table .code { font-weight: 600; color: #156467; }

.row-link {
  cursor: pointer;
  transition: background 0.15s;
}

.row-link:hover {
  background: #f7fcfc;
}

.pill {
  display: inline-block;
  padding: 0.18rem 0.7rem;
  font-size: 0.78rem;
  border-radius: 999px;
  background: #f0f7f7;
  color: #156467;
}

.mc-pager {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  margin-top: 1.5rem;
  font-size: 0.88rem;
  color: #777;
}

.mc-pager button {
  padding: 0.45rem 1rem;
  border: 1px solid #d8e6e6;
  border-radius: 7px;
  background: #fff;
  color: #156467;
  cursor: pointer;
  transition: background 0.15s, opacity 0.15s;
}

.mc-pager button:disabled {
  opacity: 0.4;
  cursor: default;
}

.mc-pager button:not(:disabled):hover {
  background: #f0f7f7;
}
</style>

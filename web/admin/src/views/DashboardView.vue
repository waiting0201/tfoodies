<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { apiFetch } from '../lib/apiClient'

const auth = useAuthStore()

interface DashboardStats {
  todayOrders: number
  pendingShipment: number
  unpaidOrders: number
  monthOrders: number
  monthRevenue: number
  activeProducts: number
  activeMembers: number
  lowStock: number
  lowStockThreshold: number
}

interface StatCard {
  label: string
  value: string
  sub: string
  accent: string
  icon: string
  to?: string
}

const ICONS = {
  order: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01"/>`,
  truck: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 17a2 2 0 11-4 0 2 2 0 014 0zM19 17a2 2 0 11-4 0 2 2 0 014 0z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M13 16V6a1 1 0 00-1-1H4a1 1 0 00-1 1v10a1 1 0 001 1h1m8-1a1 1 0 01-1 1H9m4-1V8a1 1 0 011-1h2.586a1 1 0 01.707.293l3.414 3.414a1 1 0 01.293.707V16a1 1 0 01-1 1h-1m-6-1a1 1 0 001 1h1"/>`,
  cash: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z"/>`,
  warning: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>`,
  chart: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/>`,
  revenue: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>`,
  product: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4"/>`,
  member: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"/>`,
}

const stats = ref<DashboardStats | null>(null)
const loading = ref(false)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    stats.value = await apiFetch<DashboardStats>('/admin/dashboard/stats')
  } catch (e: any) {
    error.value = e.message ?? '統計載入失敗'
  } finally {
    loading.value = false
  }
}

function fmt(n: number | undefined): string {
  if (n === undefined || n === null) return '—'
  return n.toLocaleString()
}

// 今日待辦
const todayCards = computed<StatCard[]>(() => {
  const s = stats.value
  return [
    { label: '今日訂單', value: fmt(s?.todayOrders), sub: '今日新成立', accent: '#26b7bc', icon: ICONS.order, to: '/admin/orders' },
    { label: '待出貨', value: fmt(s?.pendingShipment), sub: '未出貨 / 待出貨', accent: '#f97316', icon: ICONS.truck, to: '/admin/orders' },
    { label: '未付款', value: fmt(s?.unpaidOrders), sub: '待收款訂單', accent: '#f43f5e', icon: ICONS.cash, to: '/admin/orders' },
    { label: '庫存警示', value: fmt(s?.lowStock), sub: `低庫存品項（≤ ${s?.lowStockThreshold ?? 10}）`, accent: '#f59e0b', icon: ICONS.warning, to: '/admin/inventory' },
  ]
})

// 本月概覽
const overviewCards = computed<StatCard[]>(() => {
  const s = stats.value
  return [
    { label: '本月訂單', value: fmt(s?.monthOrders), sub: '本月成立訂單', accent: '#06b6d4', icon: ICONS.chart, to: '/admin/orders' },
    { label: '本月營收', value: s ? `NT$ ${fmt(s.monthRevenue)}` : '—', sub: '本月已付款訂單', accent: '#10b981', icon: ICONS.revenue },
    { label: '上架商品', value: fmt(s?.activeProducts), sub: '上架中商品', accent: '#14b8a6', icon: ICONS.product, to: '/admin/products' },
    { label: '活躍會員', value: fmt(s?.activeMembers), sub: '已啟用帳號', accent: '#8b5cf6', icon: ICONS.member, to: '/admin/members' },
  ]
})

const modules = [
  { label: '訂單管理', desc: '查看與處理客戶訂單、出貨、取消', path: '/admin/orders', dot: '#26b7bc' },
  { label: '商品管理', desc: '新增、編輯商品資訊與圖片', path: '/admin/products', dot: '#10b981' },
  { label: '庫存管理', desc: '追蹤各倉庫庫存及效期 FIFO', path: '/admin/inventory', dot: '#f59e0b' },
  { label: '採購管理', desc: '採購單、進貨入庫管理', path: '/admin/purchases', dot: '#06b6d4' },
  { label: '會計帳管理', desc: '營業支出、付款、請款、入帳與退款', path: '/admin/expenditures', dot: '#f43f5e' },
  { label: '內容管理', desc: '首頁橫幅、新聞、食譜等 CMS', path: '/admin/cms', dot: '#14b8a6' },
]

onMounted(load)
</script>

<template>
  <div class="font-sans">
    <!-- Page header -->
    <div class="mb-8">
      <div class="flex items-center gap-2 mb-1">
        <div class="w-1 h-5 bg-[#26b7bc] rounded-full"></div>
        <h1 class="text-slate-800 text-xl font-semibold">儀表板</h1>
      </div>
      <p class="text-slate-500 text-sm pl-3">
        歡迎回來，<span class="text-slate-700 font-medium">{{ auth.username || '管理員' }}</span>。今日數據概覽如下。
      </p>
    </div>

    <!-- Error -->
    <div v-if="error" class="mb-6 bg-rose-50 border border-rose-200 text-rose-600 text-sm rounded-xl p-3 flex items-center justify-between">
      <span>{{ error }}</span>
      <button class="text-rose-700 underline text-xs" @click="load">重試</button>
    </div>

    <!-- ── Block 1：今日待辦 ─────────────────────────────────────── -->
    <h2 class="text-slate-700 text-sm font-medium mb-3 flex items-center gap-2">
      <span>今日待辦</span>
      <span class="flex-1 h-px bg-slate-200"></span>
    </h2>
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4 mb-8">
      <component
        :is="card.to ? 'RouterLink' : 'div'"
        v-for="card in todayCards"
        :key="card.label"
        :to="card.to"
        class="bg-white rounded-xl border border-slate-200 p-5 relative overflow-hidden transition-all"
        :class="card.to ? 'hover:border-slate-300 hover:shadow-sm' : ''"
      >
        <div class="absolute top-0 left-0 right-0 h-0.5 rounded-t-xl" :style="{ backgroundColor: card.accent }"></div>
        <div class="flex items-start justify-between mb-4">
          <div>
            <p class="text-slate-500 text-xs font-medium tracking-wide uppercase mb-0.5">{{ card.label }}</p>
            <p class="text-slate-400 text-xs">{{ card.sub }}</p>
          </div>
          <div class="w-9 h-9 rounded-lg flex items-center justify-center shrink-0" :style="{ backgroundColor: card.accent + '15' }">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" :style="{ color: card.accent }" v-html="card.icon"></svg>
          </div>
        </div>
        <div class="font-mono text-3xl font-semibold text-slate-800 tracking-tight">
          <span v-if="loading" class="text-slate-300 animate-pulse">···</span>
          <span v-else>{{ card.value }}</span>
        </div>
      </component>
    </div>

    <!-- ── Block 2：本月概覽 ─────────────────────────────────────── -->
    <h2 class="text-slate-700 text-sm font-medium mb-3 flex items-center gap-2">
      <span>本月概覽</span>
      <span class="flex-1 h-px bg-slate-200"></span>
    </h2>
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4 mb-8">
      <component
        :is="card.to ? 'RouterLink' : 'div'"
        v-for="card in overviewCards"
        :key="card.label"
        :to="card.to"
        class="bg-white rounded-xl border border-slate-200 p-5 relative overflow-hidden transition-all"
        :class="card.to ? 'hover:border-slate-300 hover:shadow-sm' : ''"
      >
        <div class="absolute top-0 left-0 right-0 h-0.5 rounded-t-xl" :style="{ backgroundColor: card.accent }"></div>
        <div class="flex items-start justify-between mb-4">
          <div>
            <p class="text-slate-500 text-xs font-medium tracking-wide uppercase mb-0.5">{{ card.label }}</p>
            <p class="text-slate-400 text-xs">{{ card.sub }}</p>
          </div>
          <div class="w-9 h-9 rounded-lg flex items-center justify-center shrink-0" :style="{ backgroundColor: card.accent + '15' }">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" :style="{ color: card.accent }" v-html="card.icon"></svg>
          </div>
        </div>
        <div class="font-mono text-3xl font-semibold text-slate-800 tracking-tight">
          <span v-if="loading" class="text-slate-300 animate-pulse">···</span>
          <span v-else>{{ card.value }}</span>
        </div>
      </component>
    </div>

    <!-- Module shortcuts -->
    <div class="mb-6">
      <h2 class="text-slate-700 text-sm font-medium mb-4 flex items-center gap-2">
        <span>快速導覽</span>
        <span class="flex-1 h-px bg-slate-200"></span>
      </h2>
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        <RouterLink
          v-for="mod in modules"
          :key="mod.path"
          :to="mod.path"
          class="bg-white border border-slate-200 hover:border-[#26b7bc]/40 hover:shadow-sm rounded-xl p-4 flex items-start gap-3 transition-all group"
        >
          <span class="w-2 h-2 rounded-full mt-1.5 shrink-0 transition-all group-hover:scale-125" :style="{ backgroundColor: mod.dot }"></span>
          <div>
            <div class="text-slate-800 text-sm font-medium mb-0.5">{{ mod.label }}</div>
            <div class="text-slate-500 text-xs leading-relaxed">{{ mod.desc }}</div>
          </div>
        </RouterLink>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()

interface StatCard {
  label: string
  value: string
  sub: string
  accent: string
  icon: string
}

const stats: StatCard[] = [
  {
    label: '今日訂單',
    value: '—',
    sub: '待處理訂單',
    accent: '#26b7bc',
    icon: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01"/>`,
  },
  {
    label: '商品項目',
    value: '—',
    sub: '上架中商品',
    accent: '#10b981',
    icon: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4"/>`,
  },
  {
    label: '活躍會員',
    value: '—',
    sub: '已啟用帳號',
    accent: '#8b5cf6',
    icon: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"/>`,
  },
  {
    label: '庫存警示',
    value: '—',
    sub: '低庫存品項',
    accent: '#f59e0b',
    icon: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>`,
  },
]

const modules = [
  { label: '訂單管理', desc: '查看與處理客戶訂單、出貨、取消', path: '/admin/orders', dot: '#26b7bc' },
  { label: '商品管理', desc: '新增、編輯商品資訊與圖片', path: '/admin/products', dot: '#10b981' },
  { label: '庫存管理', desc: '追蹤各倉庫庫存及效期 FIFO', path: '/admin/inventory', dot: '#f59e0b' },
  { label: '採購管理', desc: '採購單、進貨入庫管理', path: '/admin/purchases', dot: '#06b6d4' },
  { label: '會計帳管理', desc: '營業支出、付款、請款、入帳與退款', path: '/admin/expenditures', dot: '#f43f5e' },
  { label: '內容管理', desc: '首頁橫幅、新聞、食譜等 CMS', path: '/admin/cms', dot: '#14b8a6' },
]
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

    <!-- Stat cards -->
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4 mb-8">
      <div
        v-for="card in stats"
        :key="card.label"
        class="bg-white rounded-xl border border-slate-200 p-5 relative overflow-hidden"
      >
        <!-- Top accent bar -->
        <div class="absolute top-0 left-0 right-0 h-0.5 rounded-t-xl" :style="{ backgroundColor: card.accent }"></div>

        <div class="flex items-start justify-between mb-4">
          <div>
            <p class="text-slate-500 text-xs font-medium tracking-wide uppercase mb-0.5">{{ card.label }}</p>
            <p class="text-slate-400 text-xs">{{ card.sub }}</p>
          </div>
          <div class="w-9 h-9 rounded-lg flex items-center justify-center shrink-0"
            :style="{ backgroundColor: card.accent + '15' }">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"
              :style="{ color: card.accent }"
              v-html="card.icon">
            </svg>
          </div>
        </div>

        <div class="font-mono text-3xl font-semibold text-slate-800 tracking-tight">
          {{ card.value }}
        </div>
      </div>
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
          <span class="w-2 h-2 rounded-full mt-1.5 shrink-0 transition-all group-hover:scale-125"
            :style="{ backgroundColor: mod.dot }"></span>
          <div>
            <div class="text-slate-800 text-sm font-medium mb-0.5">{{ mod.label }}</div>
            <div class="text-slate-500 text-xs leading-relaxed">{{ mod.desc }}</div>
          </div>
        </RouterLink>
      </div>
    </div>

    <!-- System note -->
    <div class="bg-[#26b7bc]/5 border border-[#26b7bc]/20 rounded-xl p-4 flex items-start gap-3">
      <svg class="w-4 h-4 text-[#26b7bc] mt-0.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
      </svg>
      <div>
        <p class="text-[#156467] text-xs font-medium mb-0.5">系統說明</p>
        <p class="text-[#1d8e92] text-xs leading-relaxed">
          ERP 模組（OrderMs / ProductMs / HomeMs …）依功能對等清單逐步移轉自舊系統。統計數據需串接 API 後顯示。
        </p>
      </div>
    </div>
  </div>
</template>

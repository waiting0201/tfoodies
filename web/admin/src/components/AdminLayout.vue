<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

async function handleLogout() {
  await auth.logout()
  router.push({ name: 'login' })
}

interface NavChild {
  label: string
  path: string
}

interface NavGroup {
  label: string
  module: string | null
  dot: string
  icon: string  // SVG innerHTML (path elements for 24x24 outline)
  children: NavChild[]
}

// 單一子項目的群組會直接渲染成連結，無需展開/收合
const navGroups: NavGroup[] = [
  {
    label: '首頁',
    module: null,
    dot: '#94a3b8',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"/>',
    children: [{ label: '首頁', path: '/' }],
  },
  {
    label: '訂單管理',
    module: 'OrderMs',
    dot: '#26b7bc',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z"/>',
    children: [
      { label: '訂單列表', path: '/admin/orders' },
      { label: '新增訂單', path: '/admin/orders/new' },
      { label: '退貨管理', path: '/admin/returns' },
    ],
  },
  {
    label: '商品管理',
    module: 'ProductMs',
    dot: '#10b981',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4"/>',
    children: [
      { label: '商品列表', path: '/admin/products' },
      { label: '新增商品', path: '/admin/products/new' },
      { label: '品牌管理', path: '/admin/brands' },
      { label: '商品分類', path: '/admin/producttypes' },
      { label: '標籤管理', path: '/admin/tags' },
    ],
  },
  {
    label: '庫存管理',
    module: 'InventoryMs',
    dot: '#f59e0b',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4"/>',
    children: [
      { label: '庫存管理', path: '/admin/inventory' },
      { label: '倉庫管理', path: '/admin/warehouses' },
    ],
  },
  {
    label: '採購管理',
    module: 'PurchaseMs',
    dot: '#06b6d4',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 17a2 2 0 11-4 0 2 2 0 014 0zM19 17a2 2 0 11-4 0 2 2 0 014 0z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M13 16V6a1 1 0 00-1-1H4a1 1 0 00-1 1v10a1 1 0 001 1h1m8-1a1 1 0 01-1 1H9m4-1V8a1 1 0 011-1h2.586a1 1 0 01.707.293l3.414 3.414a1 1 0 01.293.707V16a1 1 0 01-1 1h-1m-6-1a1 1 0 001 1h1"/>',
    children: [
      { label: '採購單', path: '/admin/purchases' },
      { label: '新增採購', path: '/admin/purchases/new' },
    ],
  },
  {
    label: '會員管理',
    module: 'MemberMs',
    dot: '#8b5cf6',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z"/>',
    children: [{ label: '會員列表', path: '/admin/members' }],
  },
  {
    label: '財務管理',
    module: 'AccountingMs',
    dot: '#f43f5e',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z"/>',
    children: [{ label: '財務記錄', path: '/admin/accounting' }],
  },
  {
    label: '發票管理',
    module: 'InvoiceMs',
    dot: '#0ea5e9',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>',
    children: [{ label: '發票列表', path: '/admin/invoices' }],
  },
  {
    label: '折扣活動',
    module: 'DiscountMs',
    dot: '#eda02f',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z"/>',
    children: [{ label: '折扣列表', path: '/admin/discounts' }],
  },
  {
    label: '網頁管理',
    module: 'HomeMs',
    dot: '#14b8a6',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/>',
    children: [
      { label: '首頁輪播', path: '/admin/web/banners' },
      { label: '美味料理', path: '/admin/web/recipes' },
      { label: '綠誌', path: '/admin/web/issues' },
      { label: '最新消息', path: '/admin/web/news' },
      { label: '部落客分享', path: '/admin/web/blogs' },
      { label: '活動花絮', path: '/admin/web/events' },
      { label: '小知識', path: '/admin/web/knowledges' },
    ],
  },
  {
    label: '銷售報表',
    module: 'ReportMs',
    dot: '#34d399',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/>',
    children: [{ label: '銷售報表', path: '/admin/reports' }],
  },
  {
    label: '系統設定',
    module: null,
    dot: '#94a3b8',
    icon: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>',
    children: [{ label: '帳號管理', path: '/admin/admin-accounts' }],
  },
]

function isVisible(module: string | null): boolean {
  if (module === null) return true
  if (!auth.isAuthenticated) return false
  return auth.can(module)
}

// 所有 nav 的葉節點路徑（用於判斷「精確 match 是否存在」）
const allNavPaths = navGroups.flatMap(g => g.children.map(c => c.path))

// 判斷某 path 是否為當前路由
// 規則：精確 match 優先；若當前 path 恰好等於某個 nav 項，只有那個 nav 項亮。
// 若當前 path 不在 nav 中（如詳細頁 /admin/orders/TF001），回退到最長前綴 match。
function isChildActive(path: string): boolean {
  if (path === '/') return route.path === '/'
  if (route.path === path) return true
  const exactMatchExists = allNavPaths.some(p => p !== '/' && route.path === p)
  if (exactMatchExists) return false
  return route.path.startsWith(path + '/')
}

// 判斷群組內是否有任何子項目處於 active 狀態
function isGroupActive(group: NavGroup): boolean {
  return group.children.some(c => isChildActive(c.path))
}

// 展開狀態：儲存已手動展開的群組 label；群組有 active 子項時也會自動展開
const openGroups = ref<Set<string>>(new Set())

function isGroupOpen(group: NavGroup): boolean {
  // 單一子項的群組不需要展開機制
  if (group.children.length <= 1) return false
  return openGroups.value.has(group.label) || isGroupActive(group)
}

function toggleGroup(group: NavGroup) {
  if (group.children.length <= 1) return
  if (openGroups.value.has(group.label)) {
    openGroups.value.delete(group.label)
  } else {
    openGroups.value.add(group.label)
  }
}

// 麵包屑：找出當前 active 的子項目標籤
const activeLabel = computed<string>(() => {
  for (const group of navGroups) {
    for (const child of group.children) {
      if (isChildActive(child.path)) {
        // 若群組只有一個子項，直接顯示群組名稱即可
        return group.children.length === 1 ? group.label : child.label
      }
    }
  }
  return '首頁'
})
</script>

<template>
  <div class="flex min-h-screen bg-slate-50 font-sans">

    <!-- ── Sidebar ──────────────────────────────────────────────── -->
    <aside class="w-60 shrink-0 bg-slate-900 flex flex-col fixed inset-y-0 left-0 z-30">

      <!-- Brand -->
      <div class="px-5 py-5 border-b border-slate-800">
        <div class="flex items-center gap-3">
          <div class="w-8 h-8 rounded-lg bg-[#26b7bc] flex items-center justify-center shrink-0">
            <span class="text-white text-sm font-bold font-mono">食</span>
          </div>
          <div>
            <div class="text-white text-base font-semibold leading-tight tracking-tight">食在呼</div>
            <div class="text-[#26b7bc] text-[10px] font-mono tracking-widest uppercase">ERP System</div>
          </div>
        </div>
      </div>

      <!-- Nav -->
      <nav class="flex-1 overflow-y-auto py-3 px-2">
        <div class="mb-1 px-3 py-1.5">
          <span class="text-slate-500 text-[10px] font-medium tracking-widest uppercase">主選單</span>
        </div>

        <template v-for="group in navGroups" :key="group.label">
          <template v-if="isVisible(group.module)">

            <!-- 單一子項目：直接渲染成連結 -->
            <template v-if="group.children.length === 1">
              <RouterLink :to="group.children[0].path" class="block mb-0.5">
                <div
                  class="flex items-center gap-3 px-3 py-2 rounded-lg transition-all duration-150 border-l-2"
                  :class="isChildActive(group.children[0].path)
                    ? 'bg-[#26b7bc]/10 border-[#26b7bc] text-[#26b7bc]'
                    : 'border-transparent text-slate-400 hover:bg-slate-800 hover:text-slate-200'"
                >
                  <svg
                    class="w-4 h-4 shrink-0"
                    fill="none" stroke="currentColor" viewBox="0 0 24 24"
                    v-html="group.icon"
                  />
                  <span class="text-sm font-medium tracking-tight">{{ group.label }}</span>
                </div>
              </RouterLink>
            </template>

            <!-- 多子項目：可展開的群組 -->
            <template v-else>
              <!-- 群組標題 -->
              <button
                class="w-full flex items-center gap-3 px-3 py-2 rounded-lg transition-all duration-150 border-l-2 mb-0.5"
                :class="isGroupActive(group)
                  ? 'border-l-[3px] text-slate-200'
                  : 'border-transparent text-slate-400 hover:bg-slate-800 hover:text-slate-200'"
                :style="isGroupActive(group) ? { borderLeftColor: group.dot } : {}"
                @click="toggleGroup(group)"
              >
                <svg
                  class="w-4 h-4 shrink-0"
                  fill="none" stroke="currentColor" viewBox="0 0 24 24"
                  v-html="group.icon"
                />
                <span class="text-sm font-medium tracking-tight flex-1 text-left">{{ group.label }}</span>
                <!-- 展開/收合箭頭 -->
                <svg
                  class="w-3.5 h-3.5 shrink-0 transition-transform duration-200"
                  :class="isGroupOpen(group) ? 'rotate-180' : ''"
                  fill="none" stroke="currentColor" viewBox="0 0 24 24"
                >
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                </svg>
              </button>

              <!-- 子項目列表 -->
              <div v-show="isGroupOpen(group)" class="mb-0.5">
                <RouterLink
                  v-for="child in group.children"
                  :key="child.path"
                  :to="child.path"
                  class="block"
                >
                  <div
                    class="flex items-center gap-2 pl-8 pr-3 py-1.5 rounded-lg transition-all duration-150 border-l-2 ml-1 mb-0.5"
                    :class="isChildActive(child.path)
                      ? 'bg-[#26b7bc]/10 border-[#26b7bc] text-[#26b7bc]'
                      : 'border-transparent text-slate-500 hover:bg-slate-800 hover:text-slate-300'"
                  >
                    <span class="text-xs tracking-tight">{{ child.label }}</span>
                  </div>
                </RouterLink>
              </div>
            </template>

          </template>
        </template>
      </nav>

      <!-- User footer -->
      <div class="px-4 py-4 border-t border-slate-800">
        <div class="flex items-center gap-3">
          <div class="w-7 h-7 rounded-full bg-slate-700 flex items-center justify-center shrink-0">
            <span class="text-slate-300 text-xs font-medium">
              {{ auth.username?.charAt(0)?.toUpperCase() || 'A' }}
            </span>
          </div>
          <div class="flex-1 min-w-0">
            <div class="text-slate-300 text-xs font-medium truncate">{{ auth.username || '管理員' }}</div>
            <div class="text-slate-600 text-[10px]">系統管理員</div>
          </div>
          <button
            @click="handleLogout"
            title="登出"
            class="text-slate-500 hover:text-slate-300 transition-colors p-1 rounded"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
            </svg>
          </button>
        </div>
      </div>
    </aside>

    <!-- ── Main area ─────────────────────────────────────────────── -->
    <div class="flex-1 flex flex-col min-w-0 ml-60">

      <!-- Topbar -->
      <header class="h-14 bg-white border-b border-slate-200 flex items-center justify-between px-6 shrink-0 sticky top-0 z-20">
        <!-- Breadcrumb / page title area -->
        <div class="flex items-center gap-2 text-slate-500 text-sm">
          <span class="text-slate-400">食在呼 ERP</span>
          <span class="text-slate-300">/</span>
          <span class="text-slate-700 font-medium">{{ activeLabel }}</span>
        </div>

        <!-- Right controls -->
        <div class="flex items-center gap-3">
          <div class="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-lg px-3 py-1.5">
            <div class="w-5 h-5 rounded-full bg-[#26b7bc]/15 flex items-center justify-center">
              <span class="text-[#156467] text-[10px] font-semibold">
                {{ auth.username?.charAt(0)?.toUpperCase() || 'A' }}
              </span>
            </div>
            <span class="text-slate-700 text-sm font-medium">{{ auth.username || '管理員' }}</span>
          </div>
          <button
            @click="handleLogout"
            class="text-slate-500 hover:text-slate-700 border border-slate-200 hover:border-slate-300 rounded-lg px-3 py-1.5 text-sm transition-colors flex items-center gap-1.5"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
            </svg>
            登出
          </button>
        </div>
      </header>

      <!-- Page content -->
      <main class="flex-1 p-6 overflow-auto">
        <RouterView />
      </main>
    </div>
  </div>
</template>

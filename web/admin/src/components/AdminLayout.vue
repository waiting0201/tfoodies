<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { apiFetch } from '../lib/apiClient'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

async function handleLogout() {
  await auth.logout()
  router.push({ name: 'login' })
}

// ─── 後端 /admin/menu 回傳的選單樹（已依權限過濾） ──────────────────────────
interface MenuChildDto { key: string; label: string }
interface MenuItemDto { key: string; label: string; icon: string | null; sort: number; children: MenuChildDto[] }

// Lim.Key → 前端 SPA 路由。未列出者代表該頁面尚未開發，選單顯示為停用（開發中）。
// 新增頁面時，只要在此補上 Lim.Key → 路徑即可，選單結構本身由 DB Lims 決定。
const ROUTES: Record<string, string> = {
  // HomeMs（網頁管理）
  Banners: '/admin/web/banners',
  Recipes: '/admin/web/recipes',
  Issues: '/admin/web/issues',
  News: '/admin/web/news',
  Blogs: '/admin/web/blogs',
  Events: '/admin/web/events',
  Knowledges: '/admin/web/knowledges',
  // ProductMs（產品管理）
  Brands: '/admin/brands',
  Producttypes: '/admin/producttypes',
  Tags: '/admin/tags',
  Products: '/admin/products',
  // OrderMs（訂單管理）— 待出貨/已出貨/已取消 為 /admin/orders 頁內 tab，
  // 不在側欄重複列出（見 HIDDEN_CHILD_KEYS），避免多個選單項共用同一路徑導致 active 誤判。
  Orders: '/admin/orders',
  Returns: '/admin/returns',
  Logistics: '/admin/logistics',
  Outofnotices: '/admin/outofnotices',
  Declarations: '/admin/declarations',
  // MemberMs（會員管理）
  Members: '/admin/members',
  Sms: '/admin/sms',
  // PurchaseMs（採購管理）
  Suppliers: '/admin/suppliers',
  Purchases: '/admin/purchases',
  // AccountingMs（會計帳管理）— Invoices(請款) 用 /admin/ar-invoices 以避開 InvoiceMs(電子發票) 的 /admin/invoices
  Exchanges: '/admin/exchanges',
  Accountings: '/admin/accountings',
  Expenditures: '/admin/expenditures',
  Outcomes: '/admin/outcomes',
  Refounds: '/admin/refounds',
  Invoices: '/admin/ar-invoices',
  Incomes: '/admin/incomes',
  // StatementMs（會計報表管理）
  Incomestatements: '/admin/income-statement',
  Balancesheet: '/admin/balance-sheet',
  // InventoryMs（庫存管理）
  Warehouses: '/admin/warehouses',
  Stocks: '/admin/inventory',
  Warehousestocks: '/admin/warehousestocks',
  // ReportMs（報表管理）
  Salereports: '/admin/reports',
  // SettingMs（系統管理）
  Admins: '/admin/admin-accounts',
  Discounts: '/admin/discounts',
  // childless 頂層模組（key 即模組）
  InvoiceMs: '/admin/invoices',
  DiscountMs: '/admin/discounts',
}

// 頂層 Lim.Key → SVG 圖示（24x24 outline 的 path 內容）。
const ICONS: Record<string, string> = {
  HomeMs:       '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/>',
  ProductMs:    '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4"/>',
  OrderMs:      '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z"/>',
  MemberMs:     '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z"/>',
  PurchaseMs:   '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 17a2 2 0 11-4 0 2 2 0 014 0zM19 17a2 2 0 11-4 0 2 2 0 014 0z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M13 16V6a1 1 0 00-1-1H4a1 1 0 00-1 1v10a1 1 0 001 1h1m8-1a1 1 0 01-1 1H9m4-1V8a1 1 0 011-1h2.586a1 1 0 01.707.293l3.414 3.414a1 1 0 01.293.707V16a1 1 0 01-1 1h-1m-6-1a1 1 0 001 1h1"/>',
  AccountingMs: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z"/>',
  StatementMs:  '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>',
  InventoryMs:  '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4"/>',
  ReportMs:     '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/>',
  SettingMs:    '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>',
  InvoiceMs:    '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>',
  DiscountMs:   '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z"/>',
}

// 頂層 Lim.Key → active 邊框點綴色。
const DOTS: Record<string, string> = {
  HomeMs: '#14b8a6', ProductMs: '#10b981', OrderMs: '#26b7bc', MemberMs: '#8b5cf6',
  PurchaseMs: '#06b6d4', AccountingMs: '#f43f5e', StatementMs: '#f97316', InventoryMs: '#f59e0b',
  ReportMs: '#34d399', SettingMs: '#94a3b8', InvoiceMs: '#0ea5e9', DiscountMs: '#eda02f',
}

const HOME_ICON = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"/>'
const DEFAULT_ICON = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M4 6h16M4 12h16M4 18h16"/>'

interface NavChild { label: string; path: string; enabled: boolean }
interface NavGroup { label: string; dot: string; icon: string; children: NavChild[] }

// ─── 載入選單 ────────────────────────────────────────────────────────────────
const menuItems = ref<MenuItemDto[]>([])
const menuLoading = ref(false)

async function loadMenu() {
  menuLoading.value = true
  try {
    const res = await apiFetch<{ items: MenuItemDto[] }>('/admin/menu')
    menuItems.value = Array.isArray(res?.items) ? res.items : []
  } catch {
    menuItems.value = []
  } finally {
    menuLoading.value = false
  }
}

// 不在側欄顯示的頂層模組（Lim.Key）。發票/折扣為無子項頂層，依需求隱藏；
// 折扣碼維護仍可由「系統管理 → 折扣碼維護」進入。
const HIDDEN_MODULES = new Set(['InvoiceMs', 'DiscountMs'])

// 不在側欄顯示的子項（Lim.Key）。待出貨/已出貨/已取消由「訂單管理」頁內 tab 切換，
// 不重複列為選單項（否則與 Orders 同路徑，會造成 active 同時命中多項）。
const HIDDEN_CHILD_KEYS = new Set(['Shipments', 'Shipped', 'Canceled'])

// 由 Lims 樹建出側欄群組；首頁為固定項，恆顯示。
const navGroups = computed<NavGroup[]>(() => {
  const groups: NavGroup[] = [
    { label: '首頁', dot: '#94a3b8', icon: HOME_ICON, children: [{ label: '首頁', path: '/', enabled: true }] },
  ]
  for (const it of menuItems.value) {
    if (HIDDEN_MODULES.has(it.key)) continue
    const icon = ICONS[it.key] ?? DEFAULT_ICON
    const dot = DOTS[it.key] ?? '#94a3b8'
    let children: NavChild[]
    if (!it.children || it.children.length === 0) {
      // childless 頂層模組：直接連結到模組頁
      const path = ROUTES[it.key]
      children = [{ label: it.label, path: path ?? '', enabled: !!path }]
    } else {
      const visible = it.children.filter(c => !HIDDEN_CHILD_KEYS.has(c.key))
      if (visible.length === 0) continue   // 子項全被隱藏 → 整組不顯示
      children = visible.map(c => {
        const path = ROUTES[c.key]
        return { label: c.label, path: path ?? '', enabled: !!path }
      })
    }
    groups.push({ label: it.label, dot, icon, children })
  }
  return groups
})

// 所有「可點」葉節點路徑（用於判斷精確 match 是否存在）
const allNavPaths = computed(() => navGroups.value.flatMap(g => g.children.filter(c => c.enabled).map(c => c.path)))

// 判斷某 path 是否為當前路由：精確 match 優先；否則回退最長前綴 match。
function isChildActive(path: string): boolean {
  if (!path) return false
  if (path === '/') return route.path === '/'
  if (route.path === path) return true
  const exactMatchExists = allNavPaths.value.some(p => p !== '/' && route.path === p)
  if (exactMatchExists) return false
  return route.path.startsWith(path + '/')
}

function isGroupActive(group: NavGroup): boolean {
  return group.children.some(c => c.enabled && isChildActive(c.path))
}

// 手風琴：同一時間最多展開一個群組。
//   openGroup === null        → 跟隨當前路由所在群組自動展開
//   openGroup === '__none__'  → 全部收合（使用者手動收掉了 active 群組）
//   openGroup === '某 label'  → 僅展開該群組
const openGroup = ref<string | null>(null)

// 當前路由所在（且為多子項）的群組 label
const activeGroupLabel = computed<string | null>(() => {
  const g = navGroups.value.find(grp => grp.children.length > 1 && isGroupActive(grp))
  return g ? g.label : null
})

function isGroupOpen(group: NavGroup): boolean {
  if (group.children.length <= 1) return false
  if (openGroup.value === '__none__') return false
  const effective = openGroup.value ?? activeGroupLabel.value
  return effective === group.label
}

function toggleGroup(group: NavGroup) {
  if (group.children.length <= 1) return
  // 展開此群組會自動收合其他群組；再點一次則收合自己
  openGroup.value = isGroupOpen(group) ? '__none__' : group.label
}

// 換頁後回到「跟隨 active 群組」模式（剛點進去的群組保持展開）
watch(() => route.path, () => { openGroup.value = null })

// 麵包屑：找出當前 active 的子項目標籤
const activeLabel = computed<string>(() => {
  for (const group of navGroups.value) {
    for (const child of group.children) {
      if (child.enabled && isChildActive(child.path)) {
        return group.children.length === 1 ? group.label : child.label
      }
    }
  }
  return '首頁'
})

onMounted(loadMenu)
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

        <p v-if="menuLoading" class="px-3 py-2 text-slate-500 text-xs">選單載入中…</p>

        <template v-for="group in navGroups" :key="group.label">

          <!-- 單一子項目：直接渲染成連結 -->
          <template v-if="group.children.length === 1">
            <RouterLink v-if="group.children[0].enabled" :to="group.children[0].path" class="block mb-0.5">
              <div
                class="flex items-center gap-3 px-3 py-2 rounded-lg transition-all duration-150 border-l-2"
                :class="isChildActive(group.children[0].path)
                  ? 'bg-[#26b7bc]/10 border-[#26b7bc] text-[#26b7bc]'
                  : 'border-transparent text-slate-400 hover:bg-slate-800 hover:text-slate-200'"
              >
                <svg class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" v-html="group.icon" />
                <span class="text-sm font-medium tracking-tight">{{ group.label }}</span>
              </div>
            </RouterLink>
            <!-- 未開發：停用顯示 -->
            <div v-else class="block mb-0.5" :title="`${group.label}（開發中）`">
              <div class="flex items-center gap-3 px-3 py-2 rounded-lg border-l-2 border-transparent text-slate-600 cursor-not-allowed">
                <svg class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" v-html="group.icon" />
                <span class="text-sm font-medium tracking-tight flex-1">{{ group.label }}</span>
                <span class="text-[9px] text-slate-600 border border-slate-700 rounded px-1 py-0.5">開發中</span>
              </div>
            </div>
          </template>

          <!-- 多子項目：可展開的群組 -->
          <template v-else>
            <button
              class="w-full flex items-center gap-3 px-3 py-2 rounded-lg transition-all duration-150 border-l-2 mb-0.5"
              :class="isGroupActive(group)
                ? 'border-l-[3px] text-slate-200'
                : 'border-transparent text-slate-400 hover:bg-slate-800 hover:text-slate-200'"
              :style="isGroupActive(group) ? { borderLeftColor: group.dot } : {}"
              @click="toggleGroup(group)"
            >
              <svg class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" v-html="group.icon" />
              <span class="text-sm font-medium tracking-tight flex-1 text-left">{{ group.label }}</span>
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
              <template v-for="(child, idx) in group.children" :key="idx">
                <RouterLink v-if="child.enabled" :to="child.path" class="block">
                  <div
                    class="flex items-center gap-2 pl-8 pr-3 py-1.5 rounded-lg transition-all duration-150 border-l-2 ml-1 mb-0.5"
                    :class="isChildActive(child.path)
                      ? 'bg-[#26b7bc]/10 border-[#26b7bc] text-[#26b7bc]'
                      : 'border-transparent text-slate-500 hover:bg-slate-800 hover:text-slate-300'"
                  >
                    <span class="text-xs tracking-tight">{{ child.label }}</span>
                  </div>
                </RouterLink>
                <!-- 未開發：停用顯示 -->
                <div v-else class="block" :title="`${child.label}（開發中）`">
                  <div class="flex items-center gap-2 pl-8 pr-3 py-1.5 rounded-lg border-l-2 border-transparent text-slate-600 ml-1 mb-0.5 cursor-not-allowed">
                    <span class="text-xs tracking-tight flex-1">{{ child.label }}</span>
                    <span class="text-[9px] text-slate-600 border border-slate-700 rounded px-1 py-0.5">開發中</span>
                  </div>
                </div>
              </template>
            </div>
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
        <div class="flex items-center gap-2 text-slate-500 text-sm">
          <span class="text-slate-400">食在呼 ERP</span>
          <span class="text-slate-300">/</span>
          <span class="text-slate-700 font-medium">{{ activeLabel }}</span>
        </div>

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

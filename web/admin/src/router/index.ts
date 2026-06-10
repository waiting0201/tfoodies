import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('../views/LoginView.vue'),
    meta: { public: true },
  },
  {
    path: '/',
    component: () => import('../components/AdminLayout.vue'),
    children: [
      {
        path: '',
        name: 'dashboard',
        component: () => import('../views/DashboardView.vue'),
      },
      // Orders
      {
        path: 'admin/orders',
        name: 'orders',
        component: () => import('../views/orders/OrdersView.vue'),
      },
      {
        path: 'admin/orders/new',
        name: 'order-new',
        component: () => import('../views/orders/OrderCreateView.vue'),
      },
      {
        path: 'admin/orders/:code/edit',
        name: 'order-edit',
        component: () => import('../views/orders/OrderEditView.vue'),
      },
      {
        path: 'admin/orders/:code',
        name: 'order-detail',
        component: () => import('../views/orders/OrderDetailView.vue'),
      },
      // OrderMs 子模組：物流商 / 缺貨通知 / 報關
      {
        path: 'admin/logistics',
        name: 'logistics',
        component: () => import('../views/orders/LogisticsView.vue'),
      },
      {
        path: 'admin/outofnotices',
        name: 'outofnotices',
        component: () => import('../views/orders/OutofnoticesView.vue'),
      },
      {
        path: 'admin/declarations',
        name: 'declarations',
        component: () => import('../views/orders/DeclarationsView.vue'),
      },
      // Products
      {
        path: 'admin/products',
        name: 'products',
        component: () => import('../views/products/ProductsView.vue'),
      },
      {
        path: 'admin/products/new',
        name: 'product-new',
        component: () => import('../views/products/ProductFormView.vue'),
      },
      {
        path: 'admin/products/:id/edit',
        name: 'product-edit',
        component: () => import('../views/products/ProductFormView.vue'),
      },
      {
        path: 'admin/products/:id/photos',
        name: 'product-photos',
        component: () => import('../views/products/ProductPhotosView.vue'),
      },
      {
        path: 'admin/brands',
        name: 'brands',
        component: () => import('../views/products/BrandsView.vue'),
      },
      {
        path: 'admin/brands/new',
        name: 'brand-new',
        component: () => import('../views/products/BrandFormView.vue'),
      },
      {
        path: 'admin/brands/:id/edit',
        name: 'brand-edit',
        component: () => import('../views/products/BrandFormView.vue'),
      },
      {
        path: 'admin/brands/:id/photos',
        name: 'brand-photos',
        component: () => import('../views/products/BrandPhotosView.vue'),
      },
      {
        path: 'admin/producttypes',
        name: 'producttypes',
        component: () => import('../views/products/ProductTypesView.vue'),
      },
      {
        path: 'admin/tags',
        name: 'tags',
        component: () => import('../views/products/TagsView.vue'),
      },
      // Members
      {
        path: 'admin/members',
        name: 'members',
        component: () => import('../views/members/MembersView.vue'),
      },
      // SMS 簡訊維護
      { path: 'admin/sms', name: 'sms', component: () => import('../views/sms/SmsView.vue') },
      { path: 'admin/sms/:id/recipients', name: 'sms-recipients', component: () => import('../views/sms/SmsRecipientsView.vue') },
      // Inventory
      {
        path: 'admin/inventory',
        name: 'inventory',
        component: () => import('../views/inventory/InventoryView.vue'),
      },
      {
        path: 'admin/warehouses',
        name: 'warehouses',
        component: () => import('../views/inventory/WarehousesView.vue'),
      },
      // Purchases
      {
        path: 'admin/suppliers',
        name: 'suppliers',
        component: () => import('../views/purchases/SuppliersView.vue'),
      },
      {
        path: 'admin/purchases',
        name: 'purchases',
        component: () => import('../views/purchases/PurchasesView.vue'),
      },
      {
        path: 'admin/purchases/new',
        name: 'purchase-new',
        component: () => import('../views/purchases/PurchaseFormView.vue'),
      },
      {
        path: 'admin/purchases/:id/edit',
        name: 'purchase-edit',
        component: () => import('../views/purchases/PurchaseFormView.vue'),
      },
      // Accounting（會計帳管理 — 7 個獨立子模組，對應 DB Lims）
      {
        path: 'admin/exchanges',
        name: 'exchanges',
        component: () => import('../views/accounting/ExchangesView.vue'),
      },
      {
        path: 'admin/accountings',
        name: 'accountings',
        component: () => import('../views/accounting/AccountingsView.vue'),
      },
      {
        path: 'admin/expenditures',
        name: 'expenditures',
        component: () => import('../views/accounting/ExpendituresView.vue'),
      },
      {
        path: 'admin/expenditures/new',
        name: 'expenditure-new',
        component: () => import('../views/accounting/ExpenditureFormView.vue'),
      },
      {
        path: 'admin/expenditures/:id/edit',
        name: 'expenditure-edit',
        component: () => import('../views/accounting/ExpenditureFormView.vue'),
      },
      {
        path: 'admin/outcomes',
        name: 'outcomes',
        component: () => import('../views/accounting/OutcomesView.vue'),
      },
      {
        path: 'admin/refounds',
        name: 'refounds',
        component: () => import('../views/accounting/RefoundsView.vue'),
      },
      {
        path: 'admin/ar-invoices',
        name: 'ar-invoices',
        component: () => import('../views/accounting/ArInvoicesView.vue'),
      },
      {
        path: 'admin/ar-invoices/new',
        name: 'ar-invoice-new',
        component: () => import('../views/accounting/ArInvoiceFormView.vue'),
      },
      {
        path: 'admin/incomes',
        name: 'incomes',
        component: () => import('../views/accounting/IncomesView.vue'),
      },
      {
        path: 'admin/incomes/new',
        name: 'income-new',
        component: () => import('../views/accounting/IncomeFormView.vue'),
      },
      // Returns
      {
        path: 'admin/returns',
        name: 'returns',
        component: () => import('../views/returns/ReturnsView.vue'),
      },
      {
        path: 'admin/returns/new',
        name: 'return-new',
        component: () => import('../views/returns/ReturnFormView.vue'),
      },
      {
        path: 'admin/returns/:id/edit',
        name: 'return-edit',
        component: () => import('../views/returns/ReturnFormView.vue'),
      },
      // CMS (legacy, kept for backward compat)
      {
        path: 'admin/cms',
        name: 'cms',
        component: () => import('../views/cms/CmsView.vue'),
      },
      {
        path: 'admin/cms/:type/new',
        name: 'cms-new',
        component: () => import('../views/cms/CmsFormView.vue'),
      },
      {
        path: 'admin/cms/:type/:id/edit',
        name: 'cms-edit',
        component: () => import('../views/cms/CmsFormView.vue'),
      },
      // 網頁管理 — 首頁輪播
      { path: 'admin/web/banners',           name: 'web-banners',      component: () => import('../views/web/banners/BannersView.vue') },
      { path: 'admin/web/banners/new',        name: 'web-banner-new',   component: () => import('../views/web/banners/BannerFormView.vue') },
      { path: 'admin/web/banners/:id/edit',   name: 'web-banner-edit',  component: () => import('../views/web/banners/BannerFormView.vue') },
      // 網頁管理 — 美味料理
      { path: 'admin/web/recipes',            name: 'web-recipes',      component: () => import('../views/web/recipes/RecipesView.vue') },
      { path: 'admin/web/recipes/new',        name: 'web-recipe-new',   component: () => import('../views/web/recipes/RecipeFormView.vue') },
      { path: 'admin/web/recipes/:id/edit',   name: 'web-recipe-edit',  component: () => import('../views/web/recipes/RecipeFormView.vue') },
      // 網頁管理 — 綠誌
      { path: 'admin/web/issues',             name: 'web-issues',       component: () => import('../views/web/issues/IssuesView.vue') },
      { path: 'admin/web/issues/new',         name: 'web-issue-new',    component: () => import('../views/web/issues/IssueFormView.vue') },
      { path: 'admin/web/issues/:id/edit',    name: 'web-issue-edit',   component: () => import('../views/web/issues/IssueFormView.vue') },
      // 網頁管理 — 最新消息
      { path: 'admin/web/news',               name: 'web-news',         component: () => import('../views/web/news/NewsView.vue') },
      { path: 'admin/web/news/new',           name: 'web-news-new',     component: () => import('../views/web/news/NewsFormView.vue') },
      { path: 'admin/web/news/:id/edit',      name: 'web-news-edit',    component: () => import('../views/web/news/NewsFormView.vue') },
      // 網頁管理 — 部落客分享
      { path: 'admin/web/blogs',              name: 'web-blogs',        component: () => import('../views/web/blogs/BlogsView.vue') },
      { path: 'admin/web/blogs/new',          name: 'web-blog-new',     component: () => import('../views/web/blogs/BlogFormView.vue') },
      { path: 'admin/web/blogs/:id/edit',     name: 'web-blog-edit',    component: () => import('../views/web/blogs/BlogFormView.vue') },
      // 網頁管理 — 活動花絮
      { path: 'admin/web/events',             name: 'web-events',       component: () => import('../views/web/events/EventsView.vue') },
      { path: 'admin/web/events/new',         name: 'web-event-new',    component: () => import('../views/web/events/EventFormView.vue') },
      { path: 'admin/web/events/:id/edit',    name: 'web-event-edit',   component: () => import('../views/web/events/EventFormView.vue') },
      { path: 'admin/web/events/:id/photos',  name: 'web-event-photos', component: () => import('../views/web/events/EventphotosView.vue') },
      // 網頁管理 — 小知識
      { path: 'admin/web/knowledges',          name: 'web-knowledges',      component: () => import('../views/web/knowledges/KnowledgesView.vue') },
      { path: 'admin/web/knowledges/new',      name: 'web-knowledge-new',   component: () => import('../views/web/knowledges/KnowledgeFormView.vue') },
      { path: 'admin/web/knowledges/:id/edit', name: 'web-knowledge-edit',  component: () => import('../views/web/knowledges/KnowledgeFormView.vue') },
      // Invoices
      {
        path: 'admin/invoices',
        name: 'invoices',
        component: () => import('../views/invoices/InvoicesView.vue'),
      },
      // Admin Accounts
      {
        path: 'admin/admin-accounts',
        name: 'admin-accounts',
        component: () => import('../views/accounts/AdminAccountsView.vue'),
      },
      // Discounts
      {
        path: 'admin/discounts',
        name: 'discounts',
        component: () => import('../views/discounts/DiscountsView.vue'),
      },
      // Reports
      {
        path: 'admin/reports',
        name: 'reports',
        component: () => import('../views/reports/ReportsView.vue'),
      },
    ],
  },
  // 請款單列印（獨立頁，不套 AdminLayout；需登入）
  {
    path: '/admin/ar-invoices/:id/print',
    name: 'ar-invoice-print',
    component: () => import('../views/accounting/ArInvoicePrintView.vue'),
  },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

// Memoised: runs only on the first navigation after a page load/refresh.
// Awaits the refresh-token exchange so isAuthenticated is stable before the guard evaluates.
let _initPromise: Promise<void> | null = null

router.beforeEach(async (to) => {
  const auth = useAuthStore()

  if (!_initPromise) {
    _initPromise = auth.initialize()
  }
  await _initPromise

  // Redirect already-authenticated users away from the login page
  if (to.name === 'login' && auth.isAuthenticated) {
    return { name: 'dashboard' }
  }

  if (!to.meta.public && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  return true
})

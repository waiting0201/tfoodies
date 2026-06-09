<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch } from '../../lib/apiClient'

interface Brand {
  brandId: string
  name: string
}
interface ProductType {
  producttypeId: string
  name: string
}
interface Product {
  productId: string
  title: string
  entitle: string
  price: number
  brandId: string
  brandName?: string
  producttypeId: string
  producttypeName?: string
  isdisabled: boolean
}
interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

const router = useRouter()

const brands = ref<Brand[]>([])
const productTypes = ref<ProductType[]>([])
const products = ref<Product[]>([])
const totalCount = ref(0)
const loading = ref(false)
const error = ref('')

const filter = reactive({
  keyword: '',
  brandId: '',
  typeId: '',
  page: 1,
  pageSize: 20,
})

async function loadMeta() {
  const [b, t] = await Promise.all([
    apiFetch<Brand[]>('/admin/brands'),
    apiFetch<ProductType[]>('/admin/producttypes'),
  ])
  brands.value = b
  productTypes.value = t
}

async function loadProducts() {
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({
      keyword: filter.keyword,
      brandId: filter.brandId,
      typeId: filter.typeId,
      page: String(filter.page),
      pageSize: String(filter.pageSize),
    })
    const res = await apiFetch<PagedResult<Product>>(`/admin/products?${params}`)
    products.value = res.items
    totalCount.value = res.totalCount
  } catch (e: any) {
    error.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

async function toggleDisabled(product: Product) {
  if (!confirm(`確定要${product.isdisabled ? '啟用' : '停用'}「${product.title}」？`)) return
  try {
    await apiFetch(`/admin/products/${product.productId}`, { method: 'DELETE' })
    await loadProducts()
  } catch (e: any) {
    alert(e.message ?? '操作失敗')
  }
}

function search() {
  filter.page = 1
  loadProducts()
}

function goToPage(p: number) {
  filter.page = p
  loadProducts()
}

const totalPages = () => Math.max(1, Math.ceil(totalCount.value / filter.pageSize))

onMounted(async () => {
  await loadMeta()
  await loadProducts()
})
</script>

<template>
  <main class="products">
    <div class="products__header">
      <h1>商品管理</h1>
      <button class="btn btn--primary" @click="router.push('/admin/products/new')">+ 新增商品</button>
    </div>

    <div class="products__filters">
      <input
        v-model="filter.keyword"
        class="input"
        placeholder="搜尋品名..."
        @keydown.enter="search"
      />
      <select v-model="filter.brandId" class="select" @change="search">
        <option value="">所有品牌</option>
        <option v-for="b in brands" :key="b.brandId" :value="b.brandId">{{ b.name }}</option>
      </select>
      <select v-model="filter.typeId" class="select" @change="search">
        <option value="">所有分類</option>
        <option v-for="t in productTypes" :key="t.producttypeId" :value="t.producttypeId">{{ t.name }}</option>
      </select>
      <button class="btn btn--secondary" @click="search">搜尋</button>
    </div>

    <p v-if="error" class="products__error">{{ error }}</p>

    <div v-if="loading" class="products__loading">載入中...</div>

    <div v-else class="card">
      <table class="table">
        <thead>
          <tr>
            <th>商品編號</th>
            <th>品名</th>
            <th>品牌</th>
            <th>分類</th>
            <th>售價</th>
            <th>狀態</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="products.length === 0">
            <td colspan="7" class="table__empty">無資料</td>
          </tr>
          <tr v-for="p in products" :key="p.productId" :class="{ 'row--disabled': p.isdisabled }">
            <td class="td--mono">{{ p.productId.slice(0, 8) }}…</td>
            <td>
              <span class="product-title">{{ p.title }}</span>
              <span v-if="p.entitle" class="product-entitle">{{ p.entitle }}</span>
            </td>
            <td>{{ p.brandName ?? p.brandId }}</td>
            <td>{{ p.producttypeName ?? p.producttypeId }}</td>
            <td class="td--number">NT$ {{ p.price.toLocaleString() }}</td>
            <td>
              <span :class="['badge', p.isdisabled ? 'badge--off' : 'badge--on']">
                {{ p.isdisabled ? '停用' : '上架' }}
              </span>
            </td>
            <td class="td--actions">
              <router-link :to="`/admin/products/${p.productId}/edit`" class="btn btn--sm btn--ghost">
                編輯
              </router-link>
              <button
                class="btn btn--sm"
                :class="p.isdisabled ? 'btn--primary' : 'btn--danger'"
                @click="toggleDisabled(p)"
              >
                {{ p.isdisabled ? '啟用' : '停用' }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="products__pagination">
      <button
        class="btn btn--sm btn--ghost"
        :disabled="filter.page <= 1"
        @click="goToPage(filter.page - 1)"
      >
        &laquo; 上一頁
      </button>
      <span class="pagination__info">第 {{ filter.page }} 頁（共 {{ totalCount }} 筆）</span>
      <button
        class="btn btn--sm btn--ghost"
        :disabled="filter.page >= totalPages()"
        @click="goToPage(filter.page + 1)"
      >
        下一頁 &raquo;
      </button>
    </div>
  </main>
</template>

<style scoped>
.products {
}

.products__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.5rem;
}

.products__header h1 {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  margin: 0;
}

.products__filters {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
  margin-bottom: 1.25rem;
}

.products__error {
  color: #c0392b;
  margin-bottom: 1rem;
}

.products__loading {
  color: var(--tf-color-muted);
  padding: 2rem;
  text-align: center;
}

.products__pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  margin-top: 1.5rem;
}

.pagination__info {
  color: var(--tf-color-muted);
  font-size: 0.875rem;
}

/* Card wrapper */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }

/* Table */
.table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.table th {
  background: var(--tf-color-primary);
  color: #fff;
  padding: 0.6rem 0.75rem;
  text-align: left;
  font-weight: 600;
  font-size: 0.875rem;
}

.table td {
  padding: 0.6rem 0.75rem;
  border-bottom: 1px solid var(--tf-color-border);
  vertical-align: middle;
}

.table__empty {
  text-align: center;
  color: var(--tf-color-muted);
  padding: 2rem !important;
}

.row--disabled td {
  opacity: 0.55;
}

.td--mono {
  font-family: monospace;
  font-size: 0.8rem;
  color: var(--tf-color-muted);
}

.td--number {
  text-align: right;
}

.td--actions {
  white-space: nowrap;
  display: flex;
  gap: 0.4rem;
}

.product-title {
  display: block;
  font-weight: 500;
}

.product-entitle {
  display: block;
  font-size: 0.8rem;
  color: var(--tf-color-muted);
}

/* Badges */
.badge {
  display: inline-block;
  padding: 0.2rem 0.5rem;
  border-radius: 3px;
  font-size: 0.78rem;
  font-weight: 600;
}

.badge--on {
  background: #e6f4ea;
  color: #1e7e34;
}

.badge--off {
  background: #fbeaea;
  color: #c0392b;
}

/* Shared button styles */
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.45rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.875rem;
  font-weight: 500;
  text-decoration: none;
  transition: opacity 0.15s;
}

.btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.btn--primary {
  background: var(--tf-color-primary);
  color: #fff;
}

.btn--primary:hover:not(:disabled) {
  background: var(--tf-color-primary-dark);
}

.btn--secondary {
  background: #f0f0f0;
  color: #333;
}

.btn--secondary:hover:not(:disabled) {
  background: #e0e0e0;
}

.btn--danger {
  background: #e74c3c;
  color: #fff;
}

.btn--danger:hover:not(:disabled) {
  opacity: 0.85;
}

.btn--ghost {
  background: transparent;
  color: var(--tf-color-primary);
  border: 1px solid var(--tf-color-primary);
}

.btn--ghost:hover:not(:disabled) {
  background: #f0f5f1;
}

.btn--sm {
  padding: 0.25rem 0.6rem;
  font-size: 0.8rem;
}

/* Inputs */
.input,
.select {
  padding: 0.45rem 0.65rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 0.875rem;
  background: #fff;
  min-width: 140px;
}

.input:focus,
.select:focus {
  outline: none;
  border-color: var(--tf-color-primary);
  box-shadow: 0 0 0 2px rgba(38,183,188,0.15);
}
</style>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch } from '../../lib/apiClient'

interface Brand {
  brandId: string
  name: string
}
interface ProductType {
  producttypeId: string
  name: string
}

const route = useRoute()
const router = useRouter()

const productId = computed(() => route.params.id as string | undefined)
const isEditing = computed(() => !!productId.value)

const brands = ref<Brand[]>([])
const productTypes = ref<ProductType[]>([])
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const successMsg = ref('')

const form = reactive({
  title: '',
  entitle: '',
  price: 0,
  fixprice: 0,
  unit: '',
  capacity: '',
  weight: '',
  description: '',
  brandId: '',
  producttypeId: '',
  ishot: false,
  isnew: false,
  isset: false,
  isdisabled: false,
})

async function loadMeta() {
  const [b, t] = await Promise.all([
    apiFetch<Brand[]>('/admin/brands'),
    apiFetch<ProductType[]>('/admin/producttypes'),
  ])
  brands.value = b
  productTypes.value = t
}

async function loadProduct(id: string) {
  loading.value = true
  error.value = ''
  try {
    const p = await apiFetch<typeof form & { productId: string }>(`/admin/products/${id}`)
    form.title = p.title ?? ''
    form.entitle = p.entitle ?? ''
    form.price = p.price ?? 0
    form.fixprice = p.fixprice ?? 0
    form.unit = p.unit ?? ''
    form.capacity = p.capacity ?? ''
    form.weight = p.weight ?? ''
    form.description = p.description ?? ''
    form.brandId = p.brandId ?? ''
    form.producttypeId = p.producttypeId ?? ''
    form.ishot = !!p.ishot
    form.isnew = !!p.isnew
    form.isset = !!p.isset
    form.isdisabled = !!p.isdisabled
  } catch (e: any) {
    error.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

async function submit() {
  if (!form.title.trim()) {
    error.value = '品名為必填'
    return
  }
  saving.value = true
  error.value = ''
  successMsg.value = ''
  try {
    if (isEditing.value) {
      await apiFetch(`/admin/products/${productId.value}`, {
        method: 'PUT',
        body: JSON.stringify(form),
      })
    } else {
      await apiFetch('/admin/products', {
        method: 'POST',
        body: JSON.stringify(form),
      })
    }
    successMsg.value = isEditing.value ? '已儲存' : '商品已建立'
    setTimeout(() => router.push('/admin/products'), 800)
  } catch (e: any) {
    error.value = e.message ?? '儲存失敗'
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  await loadMeta()
  if (isEditing.value) {
    await loadProduct(productId.value!)
  }
})
</script>

<template>
  <main class="product-form">
    <div class="product-form__header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/products')">
        &larr; 返回列表
      </button>
      <h1>{{ isEditing ? '編輯商品' : '新增商品' }}</h1>
    </div>

    <div v-if="loading" class="product-form__loading">載入中...</div>

    <form v-else class="form-card" @submit.prevent="submit">
      <p v-if="error" class="form-msg form-msg--error">{{ error }}</p>
      <p v-if="successMsg" class="form-msg form-msg--success">{{ successMsg }}</p>

      <div class="form-section">
        <h2 class="form-section__title">基本資訊</h2>

        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label label--required">品名</label>
            <input v-model="form.title" class="input" type="text" placeholder="商品中文名稱" required />
          </div>
          <div class="form-field form-field--grow">
            <label class="label">英文品名</label>
            <input v-model="form.entitle" class="input" type="text" placeholder="English title" />
          </div>
        </div>

        <div class="form-row">
          <div class="form-field">
            <label class="label label--required">品牌</label>
            <select v-model="form.brandId" class="select">
              <option value="">請選擇品牌</option>
              <option v-for="b in brands" :key="b.brandId" :value="b.brandId">{{ b.name }}</option>
            </select>
          </div>
          <div class="form-field">
            <label class="label label--required">分類</label>
            <select v-model="form.producttypeId" class="select">
              <option value="">請選擇分類</option>
              <option v-for="t in productTypes" :key="t.producttypeId" :value="t.producttypeId">{{ t.name }}</option>
            </select>
          </div>
        </div>
      </div>

      <div class="form-section">
        <h2 class="form-section__title">定價與規格</h2>

        <div class="form-row">
          <div class="form-field">
            <label class="label label--required">售價 (NT$)</label>
            <input v-model.number="form.price" class="input" type="number" min="0" step="1" />
          </div>
          <div class="form-field">
            <label class="label">定價 (NT$)</label>
            <input v-model.number="form.fixprice" class="input" type="number" min="0" step="1" />
          </div>
          <div class="form-field">
            <label class="label">單位</label>
            <input v-model="form.unit" class="input" type="text" placeholder="例：箱、包" />
          </div>
        </div>

        <div class="form-row">
          <div class="form-field">
            <label class="label">容量</label>
            <input v-model="form.capacity" class="input" type="text" placeholder="例：500ml" />
          </div>
          <div class="form-field">
            <label class="label">重量</label>
            <input v-model="form.weight" class="input" type="text" placeholder="例：300g" />
          </div>
        </div>
      </div>

      <div class="form-section">
        <h2 class="form-section__title">商品描述</h2>
        <div class="form-field">
          <label class="label">描述</label>
          <textarea v-model="form.description" class="textarea" rows="5" placeholder="詳細描述..." />
        </div>
      </div>

      <div class="form-section">
        <h2 class="form-section__title">標籤與狀態</h2>
        <div class="form-row form-row--checkboxes">
          <label class="checkbox-label">
            <input v-model="form.ishot" type="checkbox" class="checkbox" />
            <span>熱銷</span>
          </label>
          <label class="checkbox-label">
            <input v-model="form.isnew" type="checkbox" class="checkbox" />
            <span>新品</span>
          </label>
          <label class="checkbox-label">
            <input v-model="form.isset" type="checkbox" class="checkbox" />
            <span>組合商品</span>
          </label>
          <label class="checkbox-label checkbox-label--danger">
            <input v-model="form.isdisabled" type="checkbox" class="checkbox" />
            <span>停用</span>
          </label>
        </div>
      </div>

      <div class="form-actions">
        <button type="button" class="btn btn--ghost" @click="router.push('/admin/products')">取消</button>
        <button type="submit" class="btn btn--primary" :disabled="saving">
          {{ saving ? '儲存中...' : isEditing ? '儲存變更' : '建立商品' }}
        </button>
      </div>
    </form>
  </main>
</template>

<style scoped>
.product-form {
}

.product-form__header {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.product-form__header h1 {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  margin: 0;
}

.product-form__loading {
  color: var(--tf-color-muted);
  text-align: center;
  padding: 3rem;
}

.form-card {
  background: #fff;
  border: 1px solid #e5e5e5;
  border-radius: 8px;
  padding: 1.75rem;
}

.form-section {
  margin-bottom: 1.75rem;
  padding-bottom: 1.75rem;
  border-bottom: 1px solid #f0f0f0;
}

.form-section:last-of-type {
  border-bottom: none;
}

.form-section__title {
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--tf-color-primary);
  margin: 0 0 1rem 0;
}

.form-row {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  margin-bottom: 0.75rem;
}

.form-row--checkboxes {
  align-items: center;
  gap: 1.5rem;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  min-width: 160px;
}

.form-field--grow {
  flex: 1;
}

.label {
  font-size: 0.82rem;
  font-weight: 600;
  color: #444;
}

.label--required::after {
  content: ' *';
  color: #e74c3c;
}

.input,
.select,
.textarea {
  padding: 0.45rem 0.65rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-size: 0.875rem;
  background: #fff;
  font-family: inherit;
}

.input:focus,
.select:focus,
.textarea:focus {
  outline: none;
  border-color: var(--tf-color-primary);
  box-shadow: 0 0 0 2px rgba(62, 107, 68, 0.15);
}

.textarea {
  resize: vertical;
  width: 100%;
  box-sizing: border-box;
}

.checkbox-label {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  cursor: pointer;
  font-size: 0.875rem;
  user-select: none;
}

.checkbox-label--danger span {
  color: #c0392b;
}

.checkbox {
  width: 16px;
  height: 16px;
  cursor: pointer;
  accent-color: var(--tf-color-primary);
}

.form-msg {
  padding: 0.6rem 0.9rem;
  border-radius: 4px;
  font-size: 0.875rem;
  margin-bottom: 1rem;
}

.form-msg--error {
  background: #fbeaea;
  color: #c0392b;
  border: 1px solid #f5c6c6;
}

.form-msg--success {
  background: #e6f4ea;
  color: #1e7e34;
  border: 1px solid #b8dfc0;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  margin-top: 1.5rem;
  padding-top: 1.25rem;
  border-top: 1px solid #f0f0f0;
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
  opacity: 0.45;
  cursor: not-allowed;
}

.btn--primary {
  background: var(--tf-color-primary);
  color: #fff;
}

.btn--primary:hover:not(:disabled) {
  background: var(--tf-color-primary-dark);
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
</style>

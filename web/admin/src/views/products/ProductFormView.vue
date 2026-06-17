<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'
import { toBlobUrl } from '../../lib/blobUrl'
import { uploadImage } from '../../lib/upload'
import HtmlEditor from '../../components/HtmlEditor.vue'

// 注意：/admin/brands、/admin/producttypes、/admin/tags 由 Dapper dynamic 回傳，
// JSON key 為原始 DB 欄位名（小寫），非 camelCase。
interface Brand { brandid: string; title: string }
interface ProductType { producttypeid: string; title: string }
interface Tag { tagid: string; title: string }
interface ProductOption { productId: string; productNum?: string; title: string }
interface SetRow { productId: string; qty: number }
interface InventoryBatch {
  warehouseTitle: string
  noticenum: string
  expiredate: string
  quantity_left: number
}

const route = useRoute()
const router = useRouter()

const productId = computed(() => route.params.id as string | undefined)
const isEdit = computed(() => !!productId.value)

const brands = ref<Brand[]>([])
const productTypes = ref<ProductType[]>([])
const allTags = ref<Tag[]>([])
const productOptions = ref<ProductOption[]>([])
const inventory = ref<InventoryBatch[]>([])

const loading = ref(false)
const saving = ref(false)
const error = ref('')
const successMsg = ref('')
const uploading = ref(false)

const selectedTagIds = ref<string[]>([])
const setRows = ref<SetRow[]>([])

const form = reactive({
  productNum: '',
  title: '',
  enTitle: '',
  brandId: '',
  productTypeId: '',
  intro: '',
  memo: '',
  price: 0,
  fixPrice: null as number | null,
  added: 0,
  unit: '',
  conversion: null as number | null,
  capacity: '',
  weight: '',
  photo: '',
  keyword: '',
  description: '',
  shortener: '',
  sort: 0,
  isHot: false,
  isNew: false,
  isGroupBuy: false,
  isSet: false,
  isdisabled: false,
})

const numWarning = ref('')
const nameWarning = ref('')

function errMsg(e: unknown, fallback: string) {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

async function checkUnique(kind: 'num' | 'name') {
  const value = (kind === 'num' ? form.productNum : form.title).trim()
  const setWarn = (m: string) => { if (kind === 'num') numWarning.value = m; else nameWarning.value = m }
  if (!value) { setWarn(''); return }
  try {
    const params = new URLSearchParams({ value })
    if (isEdit.value) params.set('excludeId', productId.value!)
    const r = await apiFetch<{ available: boolean }>(`/admin/products/check-${kind}?${params}`)
    setWarn(r.available ? '' : (kind === 'num' ? '此商品編號已存在' : '此品名已存在'))
  } catch { /* 檢查失敗不阻擋 */ }
}

async function loadMeta() {
  const [b, t, tags] = await Promise.all([
    apiFetch<Brand[]>('/admin/brands'),
    apiFetch<ProductType[]>('/admin/producttypes'),
    apiFetch<Tag[]>('/admin/tags'),
  ])
  brands.value = b
  productTypes.value = t
  allTags.value = tags
  // 套裝組件可選商品（上架者）
  try {
    const res = await apiFetch<{ items: ProductOption[] }>('/admin/products?pageSize=500&disabled=false')
    productOptions.value = res.items
  } catch { /* 忽略 */ }
}

async function loadProduct(id: string) {
  loading.value = true
  error.value = ''
  try {
    const d = await apiFetch<{ product: Record<string, any>; tags: Tag[]; setProducts: any[] }>(`/admin/products/${id}`)
    const p = d.product
    form.productNum = p.productnum ?? ''
    form.title = p.title ?? ''
    form.enTitle = p.entitle ?? ''
    form.brandId = p.brandid ?? ''
    form.productTypeId = p.producttypeid ?? ''
    form.intro = p.intro ?? ''
    form.memo = p.memo ?? ''
    form.price = p.price ?? 0
    form.fixPrice = p.fixprice ?? null
    form.added = p.added ?? 0
    form.unit = p.unit ?? ''
    form.conversion = p.conversion ?? null
    form.capacity = p.capacity ?? ''
    form.weight = p.weight != null ? String(p.weight) : ''
    form.photo = p.photo ?? ''
    form.keyword = p.keyword ?? ''
    form.description = p.description ?? ''
    form.shortener = p.shortener ?? ''
    form.sort = p.sort ?? 0
    form.isHot = !!p.ishot
    form.isNew = !!p.isnew
    form.isGroupBuy = !!p.isgroupbuy
    form.isSet = !!p.isset
    form.isdisabled = !!p.isdisabled
    selectedTagIds.value = (d.tags ?? []).map(t => t.tagid)
    setRows.value = (d.setProducts ?? []).map(s => ({ productId: s.productid, qty: s.qty }))
  } catch (e) {
    error.value = errMsg(e, '載入失敗')
  } finally {
    loading.value = false
  }
}

async function loadInventory(id: string) {
  try {
    inventory.value = await apiFetch<InventoryBatch[]>(`/admin/inventory/${id}`)
  } catch { /* 無 InventoryMs 權限時隱藏 */ }
}

async function onFileChange(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  uploading.value = true
  error.value = ''
  try {
    form.photo = await uploadImage(file)
  } catch (err) {
    error.value = errMsg(err, '上傳失敗')
  } finally {
    uploading.value = false
    ;(e.target as HTMLInputElement).value = ''
  }
}

function addSetRow() { setRows.value.push({ productId: '', qty: 1 }) }
function removeSetRow(i: number) { setRows.value.splice(i, 1) }

async function submit() {
  if (!form.title.trim()) { error.value = '品名為必填'; return }
  if (form.price <= 0) { error.value = '售價必須大於 0'; return }
  if (!form.brandId) { error.value = '請選擇品牌'; return }
  if (!form.productTypeId) { error.value = '請選擇分類'; return }
  if (!isEdit.value && !form.photo) { error.value = '代表圖為必填'; return }
  const cleanSet = setRows.value.filter(r => r.productId && r.qty > 0)
  if (form.isSet && cleanSet.length === 0) { error.value = '組合商品必須設定至少一項組件'; return }

  saving.value = true
  error.value = ''
  successMsg.value = ''
  const body = {
    productTypeId: form.productTypeId,
    brandId: form.brandId,
    productNum: form.productNum,
    title: form.title,
    enTitle: form.enTitle,
    intro: form.intro,
    memo: form.memo,
    fixPrice: form.fixPrice,
    price: form.price,
    capacity: form.capacity,
    photo: form.photo,
    added: form.added,
    isHot: form.isHot,
    isNew: form.isNew,
    isDisabled: form.isdisabled,
    keyword: form.keyword,
    description: form.description,
    unit: form.unit,
    conversion: form.conversion,
    weight: form.weight ? Number(form.weight) : null,
    isSet: form.isSet,
    isGroupBuy: form.isGroupBuy,
    sort: form.sort,
    shortener: form.shortener,
    tagIds: selectedTagIds.value,
    setProducts: form.isSet ? cleanSet : [],
  }
  try {
    if (isEdit.value) {
      await apiFetch(`/admin/products/${productId.value}`, { method: 'PUT', body: JSON.stringify(body) })
    } else {
      await apiFetch('/admin/products', { method: 'POST', body: JSON.stringify(body) })
    }
    successMsg.value = isEdit.value ? '已儲存' : '商品已建立'
    setTimeout(() => router.push('/admin/products'), 700)
  } catch (e) {
    error.value = errMsg(e, '儲存失敗')
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  await loadMeta()
  if (isEdit.value) {
    await loadProduct(productId.value!)
    await loadInventory(productId.value!)
  }
})
</script>

<template>
  <main class="product-form">
    <div class="product-form__header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/products')">&larr; 返回列表</button>
      <h1 class="product-form__title">{{ isEdit ? '編輯商品' : '新增商品' }}</h1>
      <router-link
        v-if="isEdit"
        :to="`/admin/products/${productId}/photos`"
        class="btn btn--secondary btn--sm"
      >管理商品圖庫</router-link>
    </div>

    <div v-if="loading" class="state-msg">載入中…</div>

    <form v-else @submit.prevent="submit">
      <p v-if="error" class="form-msg form-msg--error">{{ error }}</p>
      <p v-if="successMsg" class="form-msg form-msg--success">{{ successMsg }}</p>

      <!-- 基本資訊 -->
      <div class="form-card">
        <h2 class="form-section__title">基本資訊</h2>
        <div class="form-row">
          <div class="form-field">
            <label class="label">商品編號</label>
            <input v-model="form.productNum" class="input" type="text" @blur="checkUnique('num')" />
            <span v-if="numWarning" class="field-warning">{{ numWarning }}</span>
          </div>
          <div class="form-field">
            <label class="label">品名 <span class="req">*</span></label>
            <input v-model="form.title" class="input" type="text" required @blur="checkUnique('name')" />
            <span v-if="nameWarning" class="field-warning">{{ nameWarning }}</span>
          </div>
          <div class="form-field">
            <label class="label">英文品名</label>
            <input v-model="form.enTitle" class="input" type="text" />
          </div>
          <div class="form-field">
            <label class="label">品牌 <span class="req">*</span></label>
            <select v-model="form.brandId" class="select">
              <option value="">請選擇品牌</option>
              <option v-for="b in brands" :key="b.brandid" :value="b.brandid">{{ b.title }}</option>
            </select>
          </div>
          <div class="form-field">
            <label class="label">分類 <span class="req">*</span></label>
            <select v-model="form.productTypeId" class="select">
              <option value="">請選擇分類</option>
              <option v-for="t in productTypes" :key="t.producttypeid" :value="t.producttypeid">{{ t.title }}</option>
            </select>
          </div>
          <div class="form-field form-field--full">
            <label class="label">簡介</label>
            <textarea v-model="form.intro" class="textarea" rows="2"></textarea>
          </div>
        </div>
      </div>

      <!-- 定價與規格 -->
      <div class="form-card">
        <h2 class="form-section__title">定價與規格</h2>
        <div class="form-row">
          <div class="form-field">
            <label class="label">售價 (NT$) <span class="req">*</span></label>
            <input v-model.number="form.price" class="input" type="number" min="0" />
          </div>
          <div class="form-field">
            <label class="label">定價 (NT$)</label>
            <input v-model.number="form.fixPrice" class="input" type="number" min="0" />
          </div>
          <div class="form-field">
            <label class="label">上架數</label>
            <input v-model.number="form.added" class="input" type="number" min="0" />
          </div>
          <div class="form-field">
            <label class="label">容量</label>
            <input v-model="form.capacity" class="input" type="text" placeholder="例：500ml" />
          </div>
          <div class="form-field">
            <label class="label">重量 (KG)</label>
            <input v-model="form.weight" class="input" type="text" placeholder="例：0.3" />
          </div>
          <div class="form-field">
            <label class="label">庫存大單位</label>
            <input v-model="form.unit" class="input" type="text" placeholder="例：箱" />
          </div>
          <div class="form-field">
            <label class="label">單位換算</label>
            <input v-model.number="form.conversion" class="input" type="number" min="0" />
          </div>
        </div>
      </div>

      <!-- 代表圖 -->
      <div class="form-card">
        <h2 class="form-section__title">代表圖 <span v-if="!isEdit" class="req">*</span></h2>
        <div class="upload-area">
          <label class="btn btn--ghost btn--sm upload-btn" :class="{ 'btn--loading': uploading }">
            {{ uploading ? '上傳中…' : '選擇圖片' }}
            <input type="file" accept="image/*" class="file-input" :disabled="uploading" @change="onFileChange" />
          </label>
          <span class="hint">建議 317×464</span>
        </div>
        <img v-if="form.photo" :src="toBlobUrl(form.photo)" class="preview" alt="代表圖" />
      </div>

      <!-- 商品內容 -->
      <div class="form-card">
        <h2 class="form-section__title">商品內容</h2>
        <HtmlEditor v-model="form.memo" />
      </div>

      <!-- 標籤 -->
      <div class="form-card">
        <h2 class="form-section__title">標籤</h2>
        <div v-if="allTags.length === 0" class="hint">尚無標籤，請先至「標籤管理」新增。</div>
        <div v-else class="tag-list">
          <label v-for="t in allTags" :key="t.tagid" class="tag-option">
            <input v-model="selectedTagIds" type="checkbox" :value="t.tagid" />
            <span>{{ t.title }}</span>
          </label>
        </div>
      </div>

      <!-- 套裝組合 -->
      <div class="form-card">
        <h2 class="form-section__title">套裝組合</h2>
        <label class="checkbox-line">
          <input v-model="form.isSet" type="checkbox" /> 此商品為組合 / 禮盒
        </label>
        <div v-if="form.isSet" class="set-block">
          <table class="set-table">
            <thead>
              <tr><th>組件商品</th><th style="width:120px">數量</th><th style="width:60px"></th></tr>
            </thead>
            <tbody>
              <tr v-for="(row, i) in setRows" :key="i">
                <td>
                  <select v-model="row.productId" class="select">
                    <option value="">請選擇商品</option>
                    <option v-for="o in productOptions" :key="o.productId" :value="o.productId">
                      {{ o.productNum ? `[${o.productNum}] ` : '' }}{{ o.title }}
                    </option>
                  </select>
                </td>
                <td><input v-model.number="row.qty" class="input" type="number" min="1" /></td>
                <td><button type="button" class="btn btn--sm btn--danger-ghost" @click="removeSetRow(i)">移除</button></td>
              </tr>
              <tr v-if="setRows.length === 0">
                <td colspan="3" class="set-empty">尚未加入組件</td>
              </tr>
            </tbody>
          </table>
          <button type="button" class="btn btn--sm btn--ghost" @click="addSetRow">+ 新增組件</button>
        </div>
      </div>

      <!-- SEO -->
      <div class="form-card">
        <h2 class="form-section__title">SEO 與其他</h2>
        <div class="form-row">
          <div class="form-field form-field--full">
            <label class="label">關鍵字</label>
            <input v-model="form.keyword" class="input" type="text" placeholder="以逗號分隔，3 個以內" />
          </div>
          <div class="form-field form-field--full">
            <label class="label">描述</label>
            <textarea v-model="form.description" class="textarea" rows="2" maxlength="150"></textarea>
          </div>
          <div class="form-field">
            <label class="label">短網址</label>
            <input v-model="form.shortener" class="input" type="text" />
          </div>
          <div class="form-field">
            <label class="label">排序</label>
            <input v-model.number="form.sort" class="input" type="number" min="0" />
          </div>
        </div>
      </div>

      <!-- 狀態旗標 -->
      <div class="form-card">
        <h2 class="form-section__title">狀態</h2>
        <div class="flag-row">
          <label class="checkbox-line"><input v-model="form.isHot" type="checkbox" /> 熱銷</label>
          <label class="checkbox-line"><input v-model="form.isNew" type="checkbox" /> 新品</label>
          <label class="checkbox-line"><input v-model="form.isGroupBuy" type="checkbox" /> 團購表單</label>
          <label v-if="isEdit" class="checkbox-line checkbox-line--danger"><input v-model="form.isdisabled" type="checkbox" /> 下架</label>
        </div>
        <p v-if="isEdit && form.isdisabled" class="hint">註：勾選後按「儲存變更」即會將商品下架（前台不再顯示）；取消勾選即重新上架。新增商品一律為上架。</p>
      </div>

      <!-- 庫存檢視（編輯模式） -->
      <div v-if="isEdit && inventory.length > 0" class="form-card">
        <h2 class="form-section__title">庫存批次（依效期 FIFO）</h2>
        <table class="set-table">
          <thead>
            <tr><th>倉庫</th><th>通知號</th><th>效期</th><th style="width:100px">剩餘量</th></tr>
          </thead>
          <tbody>
            <tr v-for="(b, i) in inventory" :key="i">
              <td>{{ b.warehouseTitle }}</td>
              <td class="mono">{{ b.noticenum }}</td>
              <td>{{ b.expiredate?.slice(0, 10) }}</td>
              <td>{{ b.quantity_left }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="form-actions">
        <button type="button" class="btn btn--ghost" @click="router.push('/admin/products')">取消</button>
        <button type="submit" class="btn btn--primary" :disabled="saving">
          {{ saving ? '儲存中…' : isEdit ? '儲存變更' : '建立商品' }}
        </button>
      </div>
    </form>
  </main>
</template>

<style scoped>
.product-form { width: 100%; }
.product-form__header { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.5rem; flex-wrap: wrap; }
.product-form__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); font-size: 1.25rem; margin: 0; }

.form-card { background: #fff; border: 1px solid var(--tf-color-border); border-radius: 6px; padding: 1.25rem; margin-bottom: 1.25rem; }
.form-section__title { font-size: 1rem; font-weight: 600; color: var(--tf-color-primary-dark); margin: 0 0 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid var(--tf-color-border); }

.form-row { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 0.75rem 1.25rem; }
.form-field { display: flex; flex-direction: column; gap: 0.3rem; }
.form-field--full { grid-column: 1 / -1; }
.label { font-size: 0.8rem; font-weight: 500; color: #374151; }
.req { color: var(--tf-color-accent); margin-left: 0.1rem; }
.hint { color: var(--tf-color-muted); font-size: 0.78rem; }
.field-warning { color: #c0392b; font-size: 0.78rem; }

.input, .select, .textarea { padding: 0.5rem 0.75rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.9rem; font-family: inherit; background: #fff; transition: border-color 0.15s; width: 100%; box-sizing: border-box; }
.input:focus, .select:focus, .textarea:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.textarea { resize: vertical; }

.checkbox-line { display: inline-flex; align-items: center; gap: 0.4rem; font-size: 0.9rem; color: #374151; cursor: pointer; }
.checkbox-line input { accent-color: var(--tf-color-primary); width: 16px; height: 16px; }
.checkbox-line--danger { color: #c0392b; }
.flag-row { display: flex; gap: 1.5rem; flex-wrap: wrap; }

.upload-area { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 0.5rem; }
.upload-btn { position: relative; overflow: hidden; cursor: pointer; }
.file-input { position: absolute; inset: 0; opacity: 0; cursor: pointer; font-size: 0; }
.preview { max-width: 200px; max-height: 280px; object-fit: cover; border-radius: 4px; border: 1px solid var(--tf-color-border); display: block; }

.tag-list { display: flex; flex-wrap: wrap; gap: 0.5rem 1.25rem; }
.tag-option { display: inline-flex; align-items: center; gap: 0.4rem; font-size: 0.875rem; color: #374151; cursor: pointer; }
.tag-option input { accent-color: var(--tf-color-primary); width: 16px; height: 16px; }

.set-block { margin-top: 0.75rem; }
.set-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; margin-bottom: 0.75rem; }
.set-table th { background: #f1f5f9; color: #475569; text-align: left; padding: 0.5rem 0.65rem; font-weight: 600; }
.set-table td { padding: 0.4rem 0.65rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; }
.set-empty { text-align: center; color: var(--tf-color-muted); padding: 1rem; }
.mono { font-family: 'IBM Plex Mono', monospace; }

.form-actions { display: flex; justify-content: flex-end; gap: 0.75rem; }
.form-msg { padding: 0.6rem 0.9rem; border-radius: 4px; font-size: 0.875rem; margin-bottom: 1rem; }
.form-msg--error { background: #fbeaea; color: #c0392b; border: 1px solid #f5c6c6; }
.form-msg--success { background: #e6f4ea; color: #1e7e34; border: 1px solid #b8dfc0; }
.state-msg { padding: 2rem; text-align: center; color: var(--tf-color-muted); }

.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; transition: opacity 0.15s, background 0.15s; white-space: nowrap; text-decoration: none; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38,183,188,0.06); }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }
.btn--loading { opacity: 0.6; pointer-events: none; }
</style>

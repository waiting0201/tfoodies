<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch } from '../../../lib/apiClient'
import { toBlobUrl } from '../../../lib/blobUrl'

interface ChildRow {
  title: string
  value: string
}

interface ProductItem {
  productId: string
  title: string
}

interface RecipeDetail {
  recipeId?: string
  title: string
  photo: string
  rphoto: string
  intro: string
  duration: number
  portion: number
  youtube: string
  keyword: string
  description: string
  sort: number
  shortener: string
  ingredients?: ChildRow[]
  seasonings?: ChildRow[]
  steps?: ChildRow[]
  productIds?: string[]
}

const route = useRoute()
const router = useRouter()

const id = route.params.id as string | undefined
const isEdit = computed(() => !!id)

const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const saveError = ref('')

const uploadingPhoto = ref(false)
const uploadPhotoError = ref('')
const uploadingRphoto = ref(false)
const uploadRphotoError = ref('')

const form = reactive<Omit<RecipeDetail, 'ingredients' | 'seasonings' | 'steps'>>({
  title: '',
  photo: '',
  rphoto: '',
  intro: '',
  duration: 0,
  portion: 0,
  youtube: '',
  keyword: '',
  description: '',
  sort: 0,
  shortener: '',
})

const ingredients = ref<ChildRow[]>([])
const seasonings = ref<ChildRow[]>([])
const steps = ref<ChildRow[]>([])

const allProducts = ref<ProductItem[]>([])
const productIds = ref<string[]>([])
const productSearch = ref('')

async function onPhotoChange(e: Event, field: 'photo' | 'rphoto') {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  const isMain = field === 'photo'
  if (isMain) { uploadingPhoto.value = true; uploadPhotoError.value = '' }
  else         { uploadingRphoto.value = true; uploadRphotoError.value = '' }
  try {
    const fd = new FormData()
    fd.append('file', file)
    const res = await apiFetch<{ fileName: string }>('/admin/upload', { method: 'POST', body: fd })
    form[field] = res.fileName
  } catch (err: any) {
    if (isMain) uploadPhotoError.value = err.message ?? '上傳失敗'
    else        uploadRphotoError.value = err.message ?? '上傳失敗'
  } finally {
    if (isMain) uploadingPhoto.value = false
    else        uploadingRphoto.value = false
    ;(e.target as HTMLInputElement).value = ''
  }
}

function addIngredient() { ingredients.value.push({ title: '', value: '' }) }
function removeIngredient(i: number) { ingredients.value.splice(i, 1) }

function addSeasoning() { seasonings.value.push({ title: '', value: '' }) }
function removeSeasoning(i: number) { seasonings.value.splice(i, 1) }

function addStep() { steps.value.push({ title: '', value: '' }) }
function removeStep(i: number) { steps.value.splice(i, 1) }

const filteredProducts = computed(() => {
  const q = productSearch.value.trim().toLowerCase()
  const list = q
    ? allProducts.value.filter(p => p.title.toLowerCase().includes(q))
    : allProducts.value
  // 已選的排到最前面
  return [...list].sort((a, b) => {
    const aS = productIds.value.includes(a.productId)
    const bS = productIds.value.includes(b.productId)
    if (aS === bS) return 0
    return aS ? -1 : 1
  })
})

function toggleProduct(pid: string) {
  const idx = productIds.value.indexOf(pid)
  if (idx >= 0) productIds.value.splice(idx, 1)
  else productIds.value.push(pid)
}

function productTitle(pid: string) {
  return allProducts.value.find(p => p.productId === pid)?.title ?? pid
}

onMounted(async () => {
  apiFetch<ProductItem[]>('/admin/cms/products/all')
    .then(data => { allProducts.value = data })
    .catch(() => {})

  if (!isEdit.value) return
  loading.value = true
  try {
    const data = await apiFetch<RecipeDetail>(`/admin/cms/recipes/${id}`)
    const { ingredients: ing, seasonings: sea, steps: stp, productIds: pids, ...rest } = data
    Object.assign(form, rest)
    ingredients.value = (ing ?? []).map(r => ({ title: r.title, value: r.value }))
    seasonings.value = (sea ?? []).map(r => ({ title: r.title, value: r.value }))
    steps.value = (stp ?? []).map(r => ({ title: r.title, value: r.value }))
    productIds.value = (pids ?? []).map(id => String(id))
  } catch (e: any) {
    loadError.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
})

async function save() {
  if (!form.title.trim()) {
    saveError.value = '標題為必填'
    return
  }
  saving.value = true
  saveError.value = ''
  const body = {
    title: form.title,
    photo: form.photo,
    rphoto: form.rphoto,
    intro: form.intro,
    duration: form.duration,
    portion: form.portion,
    youtube: form.youtube,
    keyword: form.keyword,
    description: form.description,
    sort: form.sort,
    shortener: form.shortener,
    ingredients: ingredients.value.map(r => ({ title: r.title, value: r.value })),
    seasonings: seasonings.value.map(r => ({ title: r.title, value: r.value })),
    steps: steps.value.map(r => ({ title: r.title, value: r.value })),
    productIds: productIds.value,
  }
  try {
    if (isEdit.value) {
      await apiFetch(`/admin/cms/recipes/${id}`, { method: 'PUT', body: JSON.stringify(body) })
    } else {
      await apiFetch('/admin/cms/recipes', { method: 'POST', body: JSON.stringify(body) })
    }
    router.push('/admin/web/recipes')
  } catch (e: any) {
    saveError.value = e.message ?? '儲存失敗'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <main class="recipe-form">
    <div class="recipe-form__header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/web/recipes')">
        &larr; 返回食譜清單
      </button>
      <h1 class="recipe-form__title">{{ isEdit ? '編輯' : '新增' }}食譜</h1>
    </div>

    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="loadError" class="state-msg state-msg--error">{{ loadError }}</div>

    <form v-else class="form-card" @submit.prevent="save">
      <p v-if="saveError" class="form-msg form-msg--error">{{ saveError }}</p>

      <!-- 基本資料 -->
      <div class="form-section">
        <h2 class="form-section__title">基本資料</h2>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label label--required">標題</label>
            <input v-model="form.title" class="input" type="text" required />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--fixed">
            <label class="label">時間（分鐘）</label>
            <input v-model.number="form.duration" class="input" type="number" min="0" />
          </div>
          <div class="form-field form-field--fixed">
            <label class="label">份量（人份）</label>
            <input v-model.number="form.portion" class="input" type="number" min="0" />
          </div>
          <div class="form-field form-field--fixed">
            <label class="label">排序</label>
            <input v-model.number="form.sort" class="input" type="number" min="0" />
          </div>
          <div class="form-field form-field--grow">
            <label class="label">短網址識別碼</label>
            <input v-model="form.shortener" class="input" type="text" placeholder="slug" />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">YouTube 連結</label>
            <input v-model="form.youtube" class="input" type="url" placeholder="https://youtube.com/…" />
          </div>
        </div>
      </div>

      <!-- 圖片 -->
      <div class="form-section">
        <h2 class="form-section__title">圖片</h2>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">代表圖</label>
            <div class="upload-area">
              <label class="btn btn--ghost btn--sm upload-btn" :class="{ 'btn--loading': uploadingRphoto }">
                {{ uploadingRphoto ? '上傳中…' : '選擇圖片' }}
                <input type="file" accept="image/jpeg,image/png,image/gif,image/webp" class="file-input" :disabled="uploadingRphoto" @change="onPhotoChange($event, 'rphoto')" />
              </label>
              <span v-if="uploadRphotoError" class="upload-error">{{ uploadRphotoError }}</span>
            </div>
            <img v-if="form.rphoto" :src="toBlobUrl(form.rphoto)" class="photo-preview" alt="代表圖預覽" />
          </div>
          <div class="form-field form-field--grow">
            <label class="label">主圖</label>
            <div class="upload-area">
              <label class="btn btn--ghost btn--sm upload-btn" :class="{ 'btn--loading': uploadingPhoto }">
                {{ uploadingPhoto ? '上傳中…' : '選擇圖片' }}
                <input type="file" accept="image/jpeg,image/png,image/gif,image/webp" class="file-input" :disabled="uploadingPhoto" @change="onPhotoChange($event, 'photo')" />
              </label>
              <span v-if="uploadPhotoError" class="upload-error">{{ uploadPhotoError }}</span>
            </div>
            <img v-if="form.photo" :src="toBlobUrl(form.photo)" class="photo-preview" alt="主圖預覽" />
          </div>
        </div>
      </div>

      <!-- 簡介 -->
      <div class="form-section">
        <h2 class="form-section__title">簡介</h2>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">簡介</label>
            <textarea v-model="form.intro" class="input input--textarea" rows="5"></textarea>
          </div>
        </div>
      </div>

      <!-- SEO -->
      <div class="form-section">
        <h2 class="form-section__title">SEO</h2>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">關鍵字</label>
            <input v-model="form.keyword" class="input" type="text" placeholder="填 3 個關鍵字，逗號分隔" />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">說明</label>
            <textarea v-model="form.description" class="input input--textarea" rows="3" maxlength="200"></textarea>
          </div>
        </div>
      </div>

      <!-- 食材 -->
      <div class="form-section">
        <h2 class="form-section__title">食材</h2>
        <div v-for="(row, idx) in ingredients" :key="idx" class="dynamic-row">
          <span class="dynamic-row__num">{{ idx + 1 }}</span>
          <div class="dynamic-row__fields">
            <div class="form-field form-field--grow">
              <label class="label">食材名稱</label>
              <input v-model="row.title" class="input" type="text" placeholder="食材名稱" />
            </div>
            <div class="form-field form-field--grow">
              <label class="label">份量</label>
              <input v-model="row.value" class="input" type="text" placeholder="例：100g" />
            </div>
          </div>
          <div class="dynamic-row__action">
            <button type="button" class="btn btn--danger-ghost btn--sm" @click="removeIngredient(idx)">刪除</button>
          </div>
        </div>
        <button type="button" class="btn btn--ghost btn--sm add-row-btn" @click="addIngredient">
          + 新增食材
        </button>
      </div>

      <!-- 調味料 -->
      <div class="form-section">
        <h2 class="form-section__title">調味料</h2>
        <div v-for="(row, idx) in seasonings" :key="idx" class="dynamic-row">
          <span class="dynamic-row__num">{{ idx + 1 }}</span>
          <div class="dynamic-row__fields">
            <div class="form-field form-field--grow">
              <label class="label">調味料名稱</label>
              <input v-model="row.title" class="input" type="text" placeholder="調味料名稱" />
            </div>
            <div class="form-field form-field--grow">
              <label class="label">份量</label>
              <input v-model="row.value" class="input" type="text" placeholder="例：1 大匙" />
            </div>
          </div>
          <div class="dynamic-row__action">
            <button type="button" class="btn btn--danger-ghost btn--sm" @click="removeSeasoning(idx)">刪除</button>
          </div>
        </div>
        <button type="button" class="btn btn--ghost btn--sm add-row-btn" @click="addSeasoning">
          + 新增調味料
        </button>
      </div>

      <!-- 製作步驟 -->
      <div class="form-section">
        <h2 class="form-section__title">製作步驟</h2>
        <div v-for="(row, idx) in steps" :key="idx" class="dynamic-row">
          <span class="dynamic-row__num">{{ idx + 1 }}</span>
          <div class="dynamic-row__fields">
            <div class="form-field form-field--grow">
              <label class="label">步驟名稱</label>
              <input v-model="row.title" class="input" type="text" placeholder="步驟名稱（選填）" />
            </div>
            <div class="form-field form-field--grow" style="flex-basis:100%">
              <label class="label">步驟說明</label>
              <textarea v-model="row.value" class="input input--textarea" rows="3" placeholder="說明此步驟的做法…"></textarea>
            </div>
          </div>
          <div class="dynamic-row__action">
            <button type="button" class="btn btn--danger-ghost btn--sm" @click="removeStep(idx)">刪除</button>
          </div>
        </div>
        <button type="button" class="btn btn--ghost btn--sm add-row-btn" @click="addStep">
          + 新增步驟
        </button>
      </div>

      <!-- 相關商品 -->
      <div class="form-section">
        <h2 class="form-section__title">
          相關商品
          <span v-if="productIds.length > 0" class="product-badge">已選 {{ productIds.length }}</span>
        </h2>

        <!-- 已選 chips -->
        <div v-if="productIds.length > 0" class="product-chips">
          <span v-for="pid in productIds" :key="pid" class="product-chip">
            {{ productTitle(pid) }}
            <button type="button" class="product-chip__remove" @click="toggleProduct(pid)" title="移除">×</button>
          </span>
        </div>
        <p v-else class="product-none">尚未選擇相關商品</p>

        <!-- 搜尋 + 清單 -->
        <div class="product-picker">
          <input
            v-model="productSearch"
            class="input product-picker__search"
            type="text"
            placeholder="搜尋商品名稱…"
          />
          <div class="product-picker__list">
            <div v-if="allProducts.length === 0" class="product-picker__empty">載入中…</div>
            <div v-else-if="filteredProducts.length === 0" class="product-picker__empty">無符合商品</div>
            <label
              v-for="p in filteredProducts"
              :key="p.productId"
              class="product-picker__item"
              :class="{ 'product-picker__item--selected': productIds.includes(p.productId) }"
            >
              <input
                type="checkbox"
                :value="p.productId"
                :checked="productIds.includes(p.productId)"
                @change="toggleProduct(p.productId)"
              />
              <span class="product-picker__title">{{ p.title }}</span>
            </label>
          </div>
        </div>
      </div>

      <div class="form-actions">
        <button type="button" class="btn btn--ghost" @click="router.push('/admin/web/recipes')">取消</button>
        <button type="submit" class="btn btn--primary" :disabled="saving">
          {{ saving ? '儲存中…' : '儲存' }}
        </button>
      </div>
    </form>
  </main>
</template>

<style scoped>
.recipe-form {}
.recipe-form__header { display:flex; align-items:center; gap:1rem; margin-bottom:1.5rem; flex-wrap:wrap; }
.recipe-form__title { margin:0; font-family:var(--tf-font-heading,inherit); color:var(--tf-color-primary-dark); font-size:1.5rem; }

.form-card { background:#fff; border:1px solid var(--tf-color-border); border-radius:10px; padding:1.75rem; }
.form-section { margin-bottom:1.75rem; padding-bottom:1.75rem; border-bottom:1px solid #f0f0f0; }
.form-section:last-of-type { border-bottom:none; }
.form-section__title { font-size:0.875rem; font-weight:700; color:var(--tf-color-primary); margin:0 0 1rem 0; text-transform:uppercase; letter-spacing:0.04em; }
.form-row { display:flex; gap:1rem; flex-wrap:wrap; margin-bottom:1rem; }
.form-field { display:flex; flex-direction:column; gap:0.3rem; min-width:120px; }
.form-field--grow { flex:1; }
.form-field--fixed { width:120px; flex-shrink:0; }
.label { font-size:0.875rem; font-weight:600; color:#444; }
.label--required::after { content:' *'; color:#e74c3c; }
.input { padding:0.45rem 0.65rem; border:1px solid var(--tf-color-border); border-radius:4px; font-size:0.875rem; font-family:inherit; background:#fff; width:100%; box-sizing:border-box; }
.input:focus { outline:none; border-color:var(--tf-color-primary); box-shadow:0 0 0 2px rgba(38,183,188,0.15); }
.input--textarea { resize:vertical; }
.form-actions { display:flex; gap:0.75rem; justify-content:flex-end; margin-top:1.5rem; padding-top:1.25rem; border-top:1px solid var(--tf-color-border); }
.form-msg { padding:0.6rem 0.9rem; border-radius:4px; font-size:0.875rem; margin-bottom:1rem; }
.form-msg--error { background:#fde8e8; color:#c0392b; }
.state-msg { padding:2rem; text-align:center; color:var(--tf-color-muted); }
.state-msg--error { color:#c0392b; }

.dynamic-row { display:flex; gap:0.75rem; align-items:flex-start; margin-bottom:0.5rem; padding:0.75rem; background:#f9fafb; border-radius:6px; border:1px solid var(--tf-color-border); }
.dynamic-row__num { min-width:24px; text-align:center; color:var(--tf-color-muted); font-size:0.875rem; padding-top:0.5rem; }
.dynamic-row__fields { flex:1; display:flex; gap:0.75rem; flex-wrap:wrap; }
.dynamic-row__action { padding-top:1.4rem; }
.add-row-btn { margin-top:0.5rem; }

.btn { display:inline-flex; align-items:center; justify-content:center; padding:0.45rem 1rem; border:1px solid transparent; border-radius:4px; cursor:pointer; font-size:0.875rem; font-weight:500; transition:opacity 0.15s,background 0.15s; white-space:nowrap; font-family:inherit; }
.btn:disabled { opacity:0.5; cursor:not-allowed; }
.btn--sm { padding:0.25rem 0.6rem; font-size:0.8rem; }
.btn--primary { background:var(--tf-color-primary); color:#fff; border-color:var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background:var(--tf-color-primary-dark); border-color:var(--tf-color-primary-dark); }
.btn--ghost { background:transparent; color:var(--tf-color-primary); border-color:var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background:#f0f5f1; }
.btn--danger-ghost { background:transparent; color:#ef4444; border-color:#fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background:#fef2f2; }

.upload-area { display:flex; align-items:center; gap:0.75rem; margin-bottom:0.5rem; }
.upload-btn { position:relative; overflow:hidden; cursor:pointer; }
.upload-btn.btn--loading { opacity:0.6; pointer-events:none; }
.file-input { position:absolute; inset:0; opacity:0; cursor:pointer; font-size:0; }
.upload-error { color:#c0392b; font-size:0.8rem; }
.photo-preview { max-width:240px; max-height:120px; object-fit:cover; border-radius:4px; border:1px solid var(--tf-color-border); display:block; margin-top:0.5rem; }

.product-badge { display:inline-flex; align-items:center; justify-content:center; background:var(--tf-color-primary); color:#fff; font-size:0.7rem; font-weight:700; border-radius:10px; padding:0.1rem 0.5rem; margin-left:0.4rem; vertical-align:middle; }
.product-chips { display:flex; flex-wrap:wrap; gap:0.4rem; margin-bottom:0.75rem; }
.product-chip { display:inline-flex; align-items:center; gap:0.3rem; background:#e6f7f6; border:1px solid #9de0dc; border-radius:20px; padding:0.25rem 0.6rem 0.25rem 0.75rem; font-size:0.8rem; color:#1a6b68; }
.product-chip__remove { background:none; border:none; cursor:pointer; color:#1a6b68; font-size:1rem; line-height:1; padding:0; opacity:0.6; }
.product-chip__remove:hover { opacity:1; }
.product-none { font-size:0.8rem; color:var(--tf-color-muted); margin:0 0 0.75rem; }
.product-picker { border:1px solid var(--tf-color-border); border-radius:6px; overflow:hidden; }
.product-picker__search { border:none; border-bottom:1px solid var(--tf-color-border); border-radius:0; }
.product-picker__search:focus { border-color:var(--tf-color-primary); box-shadow:none; }
.product-picker__list { max-height:280px; overflow-y:auto; }
.product-picker__empty { padding:1.5rem; text-align:center; color:var(--tf-color-muted); font-size:0.875rem; }
.product-picker__item { display:flex; align-items:center; gap:0.6rem; padding:0.5rem 0.75rem; cursor:pointer; border-bottom:1px solid #f3f4f6; transition:background 0.1s; }
.product-picker__item:last-child { border-bottom:none; }
.product-picker__item:hover { background:#f0faf8; }
.product-picker__item--selected { background:#e6f7f6; }
.product-picker__item input[type="checkbox"] { accent-color:var(--tf-color-primary); width:15px; height:15px; flex-shrink:0; cursor:pointer; }
.product-picker__title { font-size:0.875rem; color:#334155; flex:1; }
</style>

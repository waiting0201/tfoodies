<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'
import { toBlobUrl } from '../../lib/blobUrl'
import { uploadImage } from '../../lib/upload'
import HtmlEditor from '../../components/HtmlEditor.vue'

const route = useRoute()
const router = useRouter()

const id = computed(() => route.params.id as string | undefined)
const isEdit = computed(() => !!id.value)

const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const saveError = ref('')
const uploadingKey = ref('')

const form = reactive({
  title: '',
  subtitle: '',
  slogan: '',
  intro: '',
  logo: '',
  banner: '',
  iLogo: '',
  patternEnTitle: '',
  patternChTitle: '',
  partnerVideo: '',
  patternClass: '',
  patternMemo: '',
  storyEnTitle: '',
  storyChTitle: '',
  storyBgClass: '',
  storyMemo: '',
  peopleTitle: '',
  peopleSlogan: '',
  peoplePhoto: '',
  peopleMemo: '',
  keyword: '',
  description: '',
  sort: 0,
  isDisplay: true,
})

type FormKey = keyof typeof form

function errMsg(e: unknown, fallback: string) {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

onMounted(async () => {
  if (!isEdit.value) return
  loading.value = true
  try {
    const d = await apiFetch<Record<string, any>>(`/admin/brands/${id.value}`)
    form.title = d.title ?? ''
    form.subtitle = d.subtitle ?? ''
    form.slogan = d.slogan ?? ''
    form.intro = d.intro ?? ''
    form.logo = d.logo ?? ''
    form.banner = d.banner ?? ''
    form.iLogo = d.ilogo ?? ''
    form.patternEnTitle = d.patternentitle ?? ''
    form.patternChTitle = d.patternchtitle ?? ''
    form.partnerVideo = d.parttnervideo ?? ''
    form.patternClass = d.patternclass ?? ''
    form.patternMemo = d.patternmemo ?? ''
    form.storyEnTitle = d.storyentitle ?? ''
    form.storyChTitle = d.storychtitle ?? ''
    form.storyBgClass = d.storybgclass ?? ''
    form.storyMemo = d.storymemo ?? ''
    form.peopleTitle = d.peopletitle ?? ''
    form.peopleSlogan = d.peopleslogan ?? ''
    form.peoplePhoto = d.peoplephoto ?? ''
    form.peopleMemo = d.peoplememo ?? ''
    form.keyword = d.keyword ?? ''
    form.description = d.description ?? ''
    form.sort = d.sort ?? 0
    form.isDisplay = d.isdisplay === 1
  } catch (e) {
    loadError.value = errMsg(e, '載入失敗')
  } finally {
    loading.value = false
  }
})

async function onUpload(e: Event, key: FormKey) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  uploadingKey.value = key
  saveError.value = ''
  try {
    form[key] = await uploadImage(file) as never
  } catch (err) {
    saveError.value = errMsg(err, '上傳失敗')
  } finally {
    uploadingKey.value = ''
    ;(e.target as HTMLInputElement).value = ''
  }
}

async function save() {
  if (!form.title.trim()) { saveError.value = '品牌名稱為必填'; return }
  saving.value = true
  saveError.value = ''
  try {
    if (isEdit.value) {
      await apiFetch(`/admin/brands/${id.value}`, { method: 'PUT', body: JSON.stringify(form) })
    } else {
      await apiFetch('/admin/brands', { method: 'POST', body: JSON.stringify(form) })
    }
    router.push('/admin/brands')
  } catch (e) {
    saveError.value = errMsg(e, '儲存失敗')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <main class="brand-form">
    <div class="brand-form__header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/brands')">&larr; 返回品牌清單</button>
      <h1 class="brand-form__title">{{ isEdit ? '編輯品牌' : '新增品牌' }}</h1>
      <router-link
        v-if="isEdit"
        :to="`/admin/brands/${id}/photos`"
        class="btn btn--secondary btn--sm"
      >管理品牌圖庫</router-link>
    </div>

    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="loadError" class="state-msg state-msg--error">{{ loadError }}</div>

    <form v-else @submit.prevent="save">
      <p v-if="saveError" class="form-msg form-msg--error">{{ saveError }}</p>

      <!-- 基本資料 -->
      <div class="form-card">
        <h2 class="form-section__title">基本資料</h2>
        <div class="form-row">
          <div class="form-field">
            <label class="label">品牌名稱 <span class="req">*</span></label>
            <input v-model="form.title" class="input" type="text" required />
          </div>
          <div class="form-field">
            <label class="label">副標題</label>
            <input v-model="form.subtitle" class="input" type="text" />
          </div>
          <div class="form-field">
            <label class="label">Slogan</label>
            <input v-model="form.slogan" class="input" type="text" />
          </div>
          <div class="form-field form-field--full">
            <label class="label">簡介</label>
            <textarea v-model="form.intro" class="textarea" rows="3"></textarea>
          </div>
          <div class="form-field">
            <label class="label">排序</label>
            <input v-model.number="form.sort" class="input" type="number" min="0" />
          </div>
          <div class="form-field">
            <label class="label">顯示於前台</label>
            <label class="checkbox-line"><input v-model="form.isDisplay" type="checkbox" /> 顯示</label>
          </div>
        </div>
      </div>

      <!-- 品牌圖片 -->
      <div class="form-card">
        <h2 class="form-section__title">品牌圖片</h2>
        <div class="img-grid">
          <div class="img-field">
            <label class="label">Logo<span class="hint">建議 118×94</span></label>
            <label class="btn btn--ghost btn--sm upload-btn">
              {{ uploadingKey === 'logo' ? '上傳中…' : '選擇圖片' }}
              <input type="file" accept="image/*" class="file-input" @change="(e) => onUpload(e, 'logo')" />
            </label>
            <img v-if="form.logo" :src="toBlobUrl(form.logo)" class="preview" alt="logo" />
          </div>
          <div class="img-field">
            <label class="label">Banner<span class="hint">建議 1440×455</span></label>
            <label class="btn btn--ghost btn--sm upload-btn">
              {{ uploadingKey === 'banner' ? '上傳中…' : '選擇圖片' }}
              <input type="file" accept="image/*" class="file-input" @change="(e) => onUpload(e, 'banner')" />
            </label>
            <img v-if="form.banner" :src="toBlobUrl(form.banner)" class="preview" alt="banner" />
          </div>
          <div class="img-field">
            <label class="label">副 Logo<span class="hint">建議 92×81</span></label>
            <label class="btn btn--ghost btn--sm upload-btn">
              {{ uploadingKey === 'iLogo' ? '上傳中…' : '選擇圖片' }}
              <input type="file" accept="image/*" class="file-input" @change="(e) => onUpload(e, 'iLogo')" />
            </label>
            <img v-if="form.iLogo" :src="toBlobUrl(form.iLogo)" class="preview" alt="ilogo" />
          </div>
        </div>
      </div>

      <!-- Pattern 區塊 -->
      <div class="form-card">
        <h2 class="form-section__title">Pattern 區塊</h2>
        <div class="form-row">
          <div class="form-field">
            <label class="label">英文標題</label>
            <input v-model="form.patternEnTitle" class="input" type="text" />
          </div>
          <div class="form-field">
            <label class="label">中文標題</label>
            <input v-model="form.patternChTitle" class="input" type="text" />
          </div>
          <div class="form-field">
            <label class="label">合作影片 URL</label>
            <input v-model="form.partnerVideo" class="input" type="url" placeholder="https://…" />
          </div>
          <div class="img-field">
            <label class="label">背景圖<span class="hint">建議 1440×220</span></label>
            <label class="btn btn--ghost btn--sm upload-btn">
              {{ uploadingKey === 'patternClass' ? '上傳中…' : '選擇圖片' }}
              <input type="file" accept="image/*" class="file-input" @change="(e) => onUpload(e, 'patternClass')" />
            </label>
            <img v-if="form.patternClass" :src="toBlobUrl(form.patternClass)" class="preview" alt="pattern" />
          </div>
        </div>
        <div class="form-field form-field--full">
          <label class="label">內容</label>
          <HtmlEditor v-model="form.patternMemo" />
        </div>
      </div>

      <!-- Story 區塊 -->
      <div class="form-card">
        <h2 class="form-section__title">品牌故事 Story</h2>
        <div class="form-row">
          <div class="form-field">
            <label class="label">英文標題</label>
            <input v-model="form.storyEnTitle" class="input" type="text" />
          </div>
          <div class="form-field">
            <label class="label">中文標題</label>
            <input v-model="form.storyChTitle" class="input" type="text" />
          </div>
          <div class="img-field">
            <label class="label">背景圖<span class="hint">建議 1440×867</span></label>
            <label class="btn btn--ghost btn--sm upload-btn">
              {{ uploadingKey === 'storyBgClass' ? '上傳中…' : '選擇圖片' }}
              <input type="file" accept="image/*" class="file-input" @change="(e) => onUpload(e, 'storyBgClass')" />
            </label>
            <img v-if="form.storyBgClass" :src="toBlobUrl(form.storyBgClass)" class="preview" alt="story" />
          </div>
        </div>
        <div class="form-field form-field--full">
          <label class="label">內容</label>
          <HtmlEditor v-model="form.storyMemo" />
        </div>
      </div>

      <!-- People 區塊 -->
      <div class="form-card">
        <h2 class="form-section__title">人物 People</h2>
        <div class="form-row">
          <div class="form-field">
            <label class="label">標題</label>
            <input v-model="form.peopleTitle" class="input" type="text" />
          </div>
          <div class="form-field">
            <label class="label">Slogan</label>
            <input v-model="form.peopleSlogan" class="input" type="text" />
          </div>
          <div class="img-field">
            <label class="label">人物圖<span class="hint">建議 1440×654</span></label>
            <label class="btn btn--ghost btn--sm upload-btn">
              {{ uploadingKey === 'peoplePhoto' ? '上傳中…' : '選擇圖片' }}
              <input type="file" accept="image/*" class="file-input" @change="(e) => onUpload(e, 'peoplePhoto')" />
            </label>
            <img v-if="form.peoplePhoto" :src="toBlobUrl(form.peoplePhoto)" class="preview" alt="people" />
          </div>
        </div>
        <div class="form-field form-field--full">
          <label class="label">內容</label>
          <HtmlEditor v-model="form.peopleMemo" />
        </div>
      </div>

      <!-- SEO -->
      <div class="form-card">
        <h2 class="form-section__title">SEO</h2>
        <div class="form-row">
          <div class="form-field form-field--full">
            <label class="label">關鍵字</label>
            <input v-model="form.keyword" class="input" type="text" placeholder="以逗號分隔，3 個以內" />
          </div>
          <div class="form-field form-field--full">
            <label class="label">描述</label>
            <textarea v-model="form.description" class="textarea" rows="2" maxlength="150"></textarea>
          </div>
        </div>
      </div>

      <div class="form-actions">
        <button type="button" class="btn btn--ghost" @click="router.push('/admin/brands')">取消</button>
        <button type="submit" class="btn btn--primary" :disabled="saving">
          {{ saving ? '儲存中…' : '儲存' }}
        </button>
      </div>
    </form>
  </main>
</template>

<style scoped>
.brand-form { width: 100%; }
.brand-form__header { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.5rem; flex-wrap: wrap; }
.brand-form__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); font-size: 1.25rem; margin: 0; }

.form-card { background: #fff; border: 1px solid var(--tf-color-border); border-radius: 6px; padding: 1.25rem; margin-bottom: 1.25rem; }
.form-section__title { font-size: 1rem; font-weight: 600; color: var(--tf-color-primary-dark); margin: 0 0 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid var(--tf-color-border); }

.form-row { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 0.75rem 1.25rem; margin-bottom: 0.75rem; }
.form-field { display: flex; flex-direction: column; gap: 0.3rem; }
.form-field--full { grid-column: 1 / -1; }
.label { font-size: 0.8rem; font-weight: 500; color: #374151; }
.req { color: var(--tf-color-accent); margin-left: 0.1rem; }
.hint { color: var(--tf-color-muted); font-size: 0.72rem; font-weight: 400; margin-left: 0.4rem; }

.input, .textarea { padding: 0.5rem 0.75rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.9rem; font-family: inherit; background: #fff; transition: border-color 0.15s; width: 100%; box-sizing: border-box; }
.input:focus, .textarea:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.textarea { resize: vertical; }
.checkbox-line { display: flex; align-items: center; gap: 0.4rem; font-size: 0.9rem; color: #374151; padding-top: 0.4rem; }
.checkbox-line input { accent-color: var(--tf-color-primary); width: 16px; height: 16px; }

.img-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 1.25rem; }
.img-field { display: flex; flex-direction: column; gap: 0.4rem; align-items: flex-start; }
.upload-btn { position: relative; overflow: hidden; cursor: pointer; }
.file-input { position: absolute; inset: 0; opacity: 0; cursor: pointer; font-size: 0; }
.preview { max-width: 200px; max-height: 110px; object-fit: cover; border-radius: 4px; border: 1px solid var(--tf-color-border); }

.form-actions { display: flex; justify-content: flex-end; gap: 0.75rem; }
.form-msg { padding: 0.6rem 0.9rem; border-radius: 4px; font-size: 0.875rem; margin-bottom: 1rem; }
.form-msg--error { background: #fbeaea; color: #c0392b; border: 1px solid #f5c6c6; }
.state-msg { padding: 2rem; text-align: center; color: var(--tf-color-muted); }
.state-msg--error { color: #c0392b; }

.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; transition: opacity 0.15s, background 0.15s; white-space: nowrap; text-decoration: none; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38,183,188,0.06); }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }
</style>

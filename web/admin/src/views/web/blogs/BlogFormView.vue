<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch } from '../../../lib/apiClient'
import { toBlobUrl } from '../../../lib/blobUrl'

interface BlogDetail {
  blogId?: string
  title: string
  photo: string
  link: string
  sort: number
}

const route = useRoute()
const router = useRouter()

const id = route.params.id as string | undefined
const isEdit = computed(() => !!id)

const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const saveError = ref('')
const uploading = ref(false)
const uploadError = ref('')

const form = reactive<BlogDetail>({
  title: '',
  photo: '',
  link: '',
  sort: 0,
})

onMounted(async () => {
  if (!isEdit.value) return
  loading.value = true
  try {
    const data = await apiFetch<BlogDetail>(`/admin/cms/blogs/${id}`)
    Object.assign(form, data)
  } catch (e: any) {
    loadError.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
})

async function onFileChange(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  uploading.value = true
  uploadError.value = ''
  try {
    const fd = new FormData()
    fd.append('file', file)
    const res = await apiFetch<{ fileName: string }>('/admin/upload', { method: 'POST', body: fd })
    form.photo = res.fileName
  } catch (err: any) {
    uploadError.value = err.message ?? '上傳失敗'
  } finally {
    uploading.value = false
    ;(e.target as HTMLInputElement).value = ''
  }
}

async function save() {
  if (!form.title.trim()) {
    saveError.value = '標題為必填'
    return
  }
  if (!form.link.trim()) {
    saveError.value = '連結 URL 為必填'
    return
  }
  saving.value = true
  saveError.value = ''
  const body = {
    title: form.title,
    photo: form.photo,
    link: form.link,
    sort: form.sort,
  }
  try {
    if (isEdit.value) {
      await apiFetch(`/admin/cms/blogs/${id}`, { method: 'PUT', body: JSON.stringify(body) })
    } else {
      await apiFetch('/admin/cms/blogs', { method: 'POST', body: JSON.stringify(body) })
    }
    router.push('/admin/web/blogs')
  } catch (e: any) {
    saveError.value = e.message ?? '儲存失敗'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <main class="blog-form">
    <div class="blog-form__header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/web/blogs')">
        &larr; 返回部落格清單
      </button>
      <h1 class="blog-form__title">{{ isEdit ? '編輯' : '新增' }}部落格</h1>
    </div>

    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="loadError" class="state-msg state-msg--error">{{ loadError }}</div>

    <form v-else class="form-card" @submit.prevent="save">
      <p v-if="saveError" class="form-msg form-msg--error">{{ saveError }}</p>

      <div class="form-section">
        <h2 class="form-section__title">基本資料</h2>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label label--required">標題</label>
            <input v-model="form.title" class="input" type="text" required />
          </div>
          <div class="form-field form-field--fixed">
            <label class="label">排序</label>
            <input v-model.number="form.sort" class="input" type="number" min="0" />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label label--required">連結 URL</label>
            <input v-model="form.link" class="input" type="url" placeholder="https://…" required />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">圖片</label>
            <div class="upload-area">
              <label class="btn btn--ghost btn--sm upload-btn" :class="{ 'btn--loading': uploading }">
                {{ uploading ? '上傳中…' : '選擇圖片' }}
                <input type="file" accept="image/jpeg,image/png,image/gif,image/webp" class="file-input" :disabled="uploading" @change="onFileChange" />
              </label>
              <span v-if="uploadError" class="upload-error">{{ uploadError }}</span>
            </div>
            <img v-if="form.photo" :src="toBlobUrl(form.photo)" class="photo-preview" alt="預覽" />
          </div>
        </div>
      </div>

      <div class="form-actions">
        <button type="button" class="btn btn--ghost" @click="router.push('/admin/web/blogs')">取消</button>
        <button type="submit" class="btn btn--primary" :disabled="saving">
          {{ saving ? '儲存中…' : '儲存' }}
        </button>
      </div>
    </form>
  </main>
</template>

<style scoped>
.blog-form {}
.blog-form__header { display:flex; align-items:center; gap:1rem; margin-bottom:1.5rem; flex-wrap:wrap; }
.blog-form__title { margin:0; font-family:var(--tf-font-heading,inherit); color:var(--tf-color-primary-dark); font-size:1.5rem; }

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
.form-actions { display:flex; gap:0.75rem; justify-content:flex-end; margin-top:1.5rem; padding-top:1.25rem; border-top:1px solid var(--tf-color-border); }
.form-msg { padding:0.6rem 0.9rem; border-radius:4px; font-size:0.875rem; margin-bottom:1rem; }
.form-msg--error { background:#fde8e8; color:#c0392b; }
.state-msg { padding:2rem; text-align:center; color:var(--tf-color-muted); }
.state-msg--error { color:#c0392b; }

.btn { display:inline-flex; align-items:center; justify-content:center; padding:0.45rem 1rem; border:1px solid transparent; border-radius:4px; cursor:pointer; font-size:0.875rem; font-weight:500; transition:opacity 0.15s,background 0.15s; white-space:nowrap; font-family:inherit; }
.btn:disabled { opacity:0.5; cursor:not-allowed; }
.btn--sm { padding:0.25rem 0.6rem; font-size:0.8rem; }
.btn--primary { background:var(--tf-color-primary); color:#fff; border-color:var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background:var(--tf-color-primary-dark); border-color:var(--tf-color-primary-dark); }
.btn--ghost { background:transparent; color:var(--tf-color-primary); border-color:var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background:#f0f5f1; }

.upload-area { display:flex; align-items:center; gap:0.75rem; margin-bottom:0.5rem; }
.upload-btn { position:relative; overflow:hidden; cursor:pointer; }
.upload-btn.btn--loading { opacity:0.6; pointer-events:none; }
.file-input { position:absolute; inset:0; opacity:0; cursor:pointer; font-size:0; }
.upload-error { color:#c0392b; font-size:0.8rem; }
.photo-preview { max-width:240px; max-height:120px; object-fit:cover; border-radius:4px; border:1px solid var(--tf-color-border); display:block; margin-top:0.5rem; }
</style>

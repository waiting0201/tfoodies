<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch } from '../../../lib/apiClient'

interface IssueDetail {
  issueId?: string
  title: string
  photo: string
  intro: string
  keyword: string
  description: string
  sort: number
  shortener: string
  ispublish?: boolean
  isPublish: boolean
}

const route = useRoute()
const router = useRouter()

const id = route.params.id as string | undefined
const isEdit = computed(() => !!id)

const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const saveError = ref('')

const form = reactive<IssueDetail>({
  title: '',
  photo: '',
  intro: '',
  keyword: '',
  description: '',
  sort: 0,
  shortener: '',
  isPublish: true,
})

onMounted(async () => {
  if (!isEdit.value) return
  loading.value = true
  try {
    const data = await apiFetch<IssueDetail>(`/admin/cms/issues/${id}`)
    Object.assign(form, data)
    // 同步 ispublish → isPublish
    if (data.ispublish !== undefined) form.isPublish = data.ispublish
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
    intro: form.intro,
    keyword: form.keyword,
    description: form.description,
    sort: form.sort,
    shortener: form.shortener,
    isPublish: form.isPublish,
  }
  try {
    if (isEdit.value) {
      await apiFetch(`/admin/cms/issues/${id}`, { method: 'PUT', body: JSON.stringify(body) })
    } else {
      await apiFetch('/admin/cms/issues', { method: 'POST', body: JSON.stringify(body) })
    }
    router.push('/admin/web/issues')
  } catch (e: any) {
    saveError.value = e.message ?? '儲存失敗'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <main class="issue-form">
    <div class="issue-form__header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/web/issues')">
        &larr; 返回特集清單
      </button>
      <h1 class="issue-form__title">{{ isEdit ? '編輯' : '新增' }}特集</h1>
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
          <div class="form-field">
            <label class="label">&nbsp;</label>
            <label class="checkbox-label">
              <input v-model="form.isPublish" type="checkbox" />
              上架
            </label>
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
            <label class="label">圖片 URL</label>
            <input v-model="form.photo" class="input" type="url" placeholder="https://…" />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">簡介</label>
            <textarea v-model="form.intro" class="input input--textarea" rows="6"></textarea>
          </div>
        </div>
      </div>

      <!-- SEO -->
      <div class="form-section">
        <h2 class="form-section__title">SEO</h2>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">關鍵字</label>
            <input v-model="form.keyword" class="input" type="text" />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">說明</label>
            <textarea v-model="form.description" class="input input--textarea" rows="3" maxlength="200"></textarea>
          </div>
        </div>
      </div>

      <div class="form-actions">
        <button type="button" class="btn btn--ghost" @click="router.push('/admin/web/issues')">取消</button>
        <button type="submit" class="btn btn--primary" :disabled="saving">
          {{ saving ? '儲存中…' : '儲存' }}
        </button>
      </div>
    </form>
  </main>
</template>

<style scoped>
.issue-form {}
.issue-form__header { display:flex; align-items:center; gap:1rem; margin-bottom:1.5rem; flex-wrap:wrap; }
.issue-form__title { margin:0; font-family:var(--tf-font-heading,inherit); color:var(--tf-color-primary-dark); font-size:1.5rem; }

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
.checkbox-label { display:flex; align-items:center; gap:0.5rem; font-size:0.875rem; color:#444; cursor:pointer; padding-top:0.25rem; }
.checkbox-label input { accent-color:var(--tf-color-primary); width:16px; height:16px; }
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
</style>

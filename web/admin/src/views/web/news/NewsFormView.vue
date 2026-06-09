<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch } from '../../../lib/apiClient'

interface NewsDetail {
  newsId?: string
  title: string
  summary: string
  photo: string
  intro: string
  activityDate: string
  activitySchedule: string
  publishDate: string
  shortener: string
}

const route = useRoute()
const router = useRouter()

const id = route.params.id as string | undefined
const isEdit = computed(() => !!id)

const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const saveError = ref('')

const form = reactive<NewsDetail>({
  title: '',
  summary: '',
  photo: '',
  intro: '',
  activityDate: '',
  activitySchedule: '',
  publishDate: new Date().toISOString().slice(0, 10),
  shortener: '',
})

onMounted(async () => {
  if (!isEdit.value) return
  loading.value = true
  try {
    const data = await apiFetch<NewsDetail>(`/admin/cms/news/${id}`)
    if (data.publishDate) data.publishDate = data.publishDate.slice(0, 10)
    Object.assign(form, data)
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
    summary: form.summary,
    photo: form.photo,
    intro: form.intro,
    activityDate: form.activityDate,
    activitySchedule: form.activitySchedule,
    publishDate: form.publishDate,
    shortener: form.shortener,
  }
  try {
    if (isEdit.value) {
      await apiFetch(`/admin/cms/news/${id}`, { method: 'PUT', body: JSON.stringify(body) })
    } else {
      await apiFetch('/admin/cms/news', { method: 'POST', body: JSON.stringify(body) })
    }
    router.push('/admin/web/news')
  } catch (e: any) {
    saveError.value = e.message ?? '儲存失敗'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <main class="news-form">
    <div class="news-form__header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/web/news')">
        &larr; 返回消息清單
      </button>
      <h1 class="news-form__title">{{ isEdit ? '編輯' : '新增' }}最新消息</h1>
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
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">摘要</label>
            <input v-model="form.summary" class="input" type="text" placeholder="簡短描述" />
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
            <label class="label">內容</label>
            <textarea v-model="form.intro" class="input input--textarea" rows="8"></textarea>
          </div>
        </div>
      </div>

      <div class="form-section">
        <h2 class="form-section__title">活動資訊</h2>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">活動日期</label>
            <input v-model="form.activityDate" class="input" type="text" placeholder="例：2024年3月15日" />
          </div>
          <div class="form-field form-field--grow">
            <label class="label">活動場次</label>
            <input v-model="form.activitySchedule" class="input" type="text" placeholder="例：09:00–12:00" />
          </div>
        </div>
      </div>

      <div class="form-section">
        <h2 class="form-section__title">發布設定</h2>
        <div class="form-row">
          <div class="form-field">
            <label class="label">發布日期</label>
            <input v-model="form.publishDate" class="input" type="date" />
          </div>
          <div class="form-field form-field--grow">
            <label class="label">短網址識別碼</label>
            <input v-model="form.shortener" class="input" type="text" placeholder="slug" />
          </div>
        </div>
      </div>

      <div class="form-actions">
        <button type="button" class="btn btn--ghost" @click="router.push('/admin/web/news')">取消</button>
        <button type="submit" class="btn btn--primary" :disabled="saving">
          {{ saving ? '儲存中…' : '儲存' }}
        </button>
      </div>
    </form>
  </main>
</template>

<style scoped>
.news-form {}
.news-form__header { display:flex; align-items:center; gap:1rem; margin-bottom:1.5rem; flex-wrap:wrap; }
.news-form__title { margin:0; font-family:var(--tf-font-heading,inherit); color:var(--tf-color-primary-dark); font-size:1.5rem; }

.form-card { background:#fff; border:1px solid var(--tf-color-border); border-radius:10px; padding:1.75rem; }
.form-section { margin-bottom:1.75rem; padding-bottom:1.75rem; border-bottom:1px solid #f0f0f0; }
.form-section:last-of-type { border-bottom:none; }
.form-section__title { font-size:0.875rem; font-weight:700; color:var(--tf-color-primary); margin:0 0 1rem 0; text-transform:uppercase; letter-spacing:0.04em; }
.form-row { display:flex; gap:1rem; flex-wrap:wrap; margin-bottom:1rem; }
.form-field { display:flex; flex-direction:column; gap:0.3rem; min-width:120px; }
.form-field--grow { flex:1; }
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

.btn { display:inline-flex; align-items:center; justify-content:center; padding:0.45rem 1rem; border:1px solid transparent; border-radius:4px; cursor:pointer; font-size:0.875rem; font-weight:500; transition:opacity 0.15s,background 0.15s; white-space:nowrap; font-family:inherit; }
.btn:disabled { opacity:0.5; cursor:not-allowed; }
.btn--sm { padding:0.25rem 0.6rem; font-size:0.8rem; }
.btn--primary { background:var(--tf-color-primary); color:#fff; border-color:var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background:var(--tf-color-primary-dark); border-color:var(--tf-color-primary-dark); }
.btn--ghost { background:transparent; color:var(--tf-color-primary); border-color:var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background:#f0f5f1; }
</style>

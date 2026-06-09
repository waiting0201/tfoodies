<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch } from '../../lib/apiClient'

type CmsType = 'banner' | 'news' | 'recipe' | 'issue' | 'event' | 'faq'

const route = useRoute()
const router = useRouter()

const type = route.params.type as CmsType
const id = route.params.id as string | undefined
const isEdit = !!id

const LABELS: Record<CmsType, string> = {
  banner: '輪播', news: '消息', recipe: '食譜', issue: '特集', event: '活動', faq: 'FAQ',
}

const ENDPOINTS: Record<CmsType, string> = {
  banner: '/admin/cms/banners',
  news: '/admin/cms/news',
  recipe: '/admin/cms/recipes',
  issue: '/admin/cms/issues',
  event: '/admin/cms/events',
  faq: '/admin/cms/knowledges',
}

function defaultForm(t: CmsType): Record<string, any> {
  switch (t) {
    case 'banner':  return { title: '', subtitle: '', url: '', photoUrl: '', style: 0, sort: 0 }
    case 'news':    return { title: '', summary: '', intro: '', publishDate: '', photo: '', shortener: '' }
    case 'recipe':  return { title: '', photo: '', intro: '', duration: 0, portion: 0, sort: 0, shortener: '' }
    case 'issue':   return { title: '', photo: '', intro: '', description: '', sort: 0, shortener: '' }
    case 'event':   return { title: '', summary: '', photo: '', intro: '', eventDate: '', sort: 0, shortener: '' }
    case 'faq':     return { question: '', answer: '', sort: 0 }
  }
}

const form = ref<Record<string, any>>(defaultForm(type))
const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const saveError = ref('')

onMounted(async () => {
  if (!isEdit) return
  loading.value = true
  try {
    const data = await apiFetch<any>(`${ENDPOINTS[type]}/${id}`)
    if (data.publishDate) data.publishDate = (data.publishDate as string).slice(0, 10)
    if (data.eventDate)   data.eventDate   = (data.eventDate as string).slice(0, 10)
    form.value = { ...defaultForm(type), ...data }
  } catch (e: any) {
    loadError.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
})

async function save() {
  saving.value = true; saveError.value = ''
  try {
    if (isEdit) {
      await apiFetch(`${ENDPOINTS[type]}/${id}`, { method: 'PUT', body: JSON.stringify(form.value) })
    } else {
      await apiFetch(ENDPOINTS[type], { method: 'POST', body: JSON.stringify(form.value) })
    }
    goBack()
  } catch (e: any) {
    saveError.value = e.message ?? '儲存失敗'
  } finally {
    saving.value = false
  }
}

function goBack() {
  router.push({ name: 'cms', query: { tab: type } })
}
</script>

<template>
  <main class="cms-form">
    <div class="cms-form__header">
      <button class="btn btn--ghost btn--sm" @click="goBack">← 返回{{ LABELS[type] }}清單</button>
      <h1 class="cms-form__title">{{ isEdit ? '編輯' : '新增' }}{{ LABELS[type] }}</h1>
    </div>

    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="loadError" class="state-msg state-msg--error">{{ loadError }}</div>

    <form v-else class="form-card" @submit.prevent="save">
      <p v-if="saveError" class="form-msg form-msg--error">{{ saveError }}</p>

      <!-- ── 輪播 ── -->
      <template v-if="type === 'banner'">
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label label--required">標題</label>
            <input v-model="form.title" class="input" type="text" required />
          </div>
          <div class="form-field form-field--grow">
            <label class="label">副標題</label>
            <input v-model="form.subtitle" class="input" type="text" />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">連結 URL</label>
            <input v-model="form.url" class="input" type="url" placeholder="https://…" />
          </div>
          <div class="form-field form-field--fixed">
            <label class="label">排序</label>
            <input v-model.number="form.sort" class="input" type="number" min="0" />
          </div>
          <div class="form-field form-field--fixed">
            <label class="label">樣式代碼</label>
            <input v-model.number="form.style" class="input" type="number" min="0" />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">圖片 URL</label>
            <input v-model="form.photoUrl" class="input" type="url" placeholder="https://…" />
          </div>
        </div>
      </template>

      <!-- ── 消息 ── -->
      <template v-else-if="type === 'news'">
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label label--required">標題</label>
            <input v-model="form.title" class="input" type="text" required />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field">
            <label class="label">發布日期</label>
            <input v-model="form.publishDate" class="input" type="date" />
          </div>
          <div class="form-field">
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
            <label class="label">摘要</label>
            <input v-model="form.summary" class="input" type="text" placeholder="簡短描述" />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">內容</label>
            <textarea v-model="form.intro" class="input input--textarea" rows="8"></textarea>
          </div>
        </div>
      </template>

      <!-- ── 食譜 ── -->
      <template v-else-if="type === 'recipe'">
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
      </template>

      <!-- ── 特集 ── -->
      <template v-else-if="type === 'issue'">
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label label--required">標題</label>
            <input v-model="form.title" class="input" type="text" required />
          </div>
        </div>
        <div class="form-row">
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
            <input v-model="form.intro" class="input" type="text" />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">說明</label>
            <textarea v-model="form.description" class="input input--textarea" rows="6"></textarea>
          </div>
        </div>
      </template>

      <!-- ── 活動 ── -->
      <template v-else-if="type === 'event'">
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label label--required">標題</label>
            <input v-model="form.title" class="input" type="text" required />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field">
            <label class="label">活動日期</label>
            <input v-model="form.eventDate" class="input" type="date" />
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
            <label class="label">摘要</label>
            <input v-model="form.summary" class="input" type="text" />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label">內容</label>
            <textarea v-model="form.intro" class="input input--textarea" rows="6"></textarea>
          </div>
        </div>
      </template>

      <!-- ── FAQ ── -->
      <template v-else-if="type === 'faq'">
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label label--required">問題</label>
            <input v-model="form.question" class="input" type="text" required />
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--grow">
            <label class="label label--required">回答</label>
            <textarea v-model="form.answer" class="input input--textarea" rows="5" required></textarea>
          </div>
        </div>
        <div class="form-row">
          <div class="form-field form-field--fixed">
            <label class="label">排序</label>
            <input v-model.number="form.sort" class="input" type="number" min="0" />
          </div>
        </div>
      </template>

      <div class="form-actions">
        <button type="button" class="btn btn--ghost" @click="goBack">取消</button>
        <button type="submit" class="btn btn--primary" :disabled="saving">
          {{ saving ? '儲存中…' : '儲存' }}
        </button>
      </div>
    </form>
  </main>
</template>

<style scoped>
.cms-form { max-width: 800px; }
.cms-form__header { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.5rem; flex-wrap: wrap; }
.cms-form__title { margin: 0; font-family: var(--tf-font-heading, inherit); color: var(--tf-color-primary-dark); }

.form-card {
  background: #fff;
  border: 1px solid var(--tf-color-border);
  border-radius: 10px;
  padding: 1.75rem;
}

.form-row { display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 1rem; }
.form-field { display: flex; flex-direction: column; gap: 0.3rem; min-width: 120px; }
.form-field--grow { flex: 1; }
.form-field--fixed { width: 120px; flex-shrink: 0; }

.label { font-size: 0.875rem; font-weight: 600; color: #444; }
.label--required::after { content: ' *'; color: #e74c3c; }

.input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem;
  font-family: inherit;
  background: #fff;
  width: 100%;
  box-sizing: border-box;
}
.input:focus {
  outline: none;
  border-color: var(--tf-color-primary);
  box-shadow: 0 0 0 2px rgba(38,183,188,0.15);
}
.input--textarea { resize: vertical; }

.form-actions { display: flex; gap: 0.75rem; justify-content: flex-end; margin-top: 1.5rem; padding-top: 1.25rem; border-top: 1px solid var(--tf-color-border); }

.form-msg { padding: 0.6rem 0.9rem; border-radius: 4px; font-size: 0.875rem; margin-bottom: 1rem; }
.form-msg--error { background: #fde8e8; color: #c0392b; }

.state-msg { padding: 2rem; text-align: center; color: var(--tf-color-muted); }
.state-msg--error { color: #c0392b; }

.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: opacity 0.15s, background 0.15s; white-space: nowrap; font-family: inherit; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: #f0f5f1; }
</style>

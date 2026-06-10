<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch } from '../../lib/apiClient'
import HtmlEditor from '../../components/HtmlEditor.vue'

interface QuestionDetail {
  questionId?: string
  questiontypeId: string
  title: string
  answer: string
  sort: number
}

interface Questiontype {
  questiontypeId: string
  title: string
}

const route = useRoute()
const router = useRouter()

const id = route.params.id as string | undefined
const isEdit = computed(() => !!id)

const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const saveError = ref('')

const types = ref<Questiontype[]>([])

const form = reactive<QuestionDetail>({
  questiontypeId: '',
  title: '',
  answer: '',
  sort: 0,
})

onMounted(async () => {
  loading.value = true
  try {
    types.value = await apiFetch<Questiontype[]>('/admin/questiontypes')
    if (isEdit.value) {
      const data = await apiFetch<QuestionDetail>(`/admin/questions/${id}`)
      Object.assign(form, data)
    } else if (types.value.length > 0) {
      form.questiontypeId = types.value[0].questiontypeId
    }
  } catch (e: any) {
    loadError.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
})

async function save() {
  if (!form.questiontypeId) {
    saveError.value = '請選擇分類'
    return
  }
  if (!form.title.trim()) {
    saveError.value = '標題為必填'
    return
  }
  saving.value = true
  saveError.value = ''
  const body = {
    questiontypeId: form.questiontypeId,
    title: form.title,
    answer: form.answer,
    sort: form.sort,
  }
  try {
    if (isEdit.value) {
      await apiFetch(`/admin/questions/${id}`, { method: 'PUT', body: JSON.stringify(body) })
    } else {
      await apiFetch('/admin/questions', { method: 'POST', body: JSON.stringify(body) })
    }
    router.push('/admin/questions')
  } catch (e: any) {
    saveError.value = e.message ?? '儲存失敗'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <main>
    <div class="form-header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/questions')">&larr; 返回購物說明清單</button>
      <h1 class="form-title">{{ isEdit ? '編輯' : '新增' }}購物說明</h1>
    </div>

    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="loadError" class="state-msg state-msg--error">{{ loadError }}</div>

    <form v-else class="form-card" @submit.prevent="save">
      <p v-if="saveError" class="form-msg form-msg--error">{{ saveError }}</p>

      <p v-if="types.length === 0" class="form-msg form-msg--error">
        尚未建立任何購物說明分類，請先至「購物說明分類」新增分類。
      </p>

      <div class="form-row">
        <div class="form-field form-field--grow">
          <label class="label label--required">分類</label>
          <select v-model="form.questiontypeId" class="input" required>
            <option value="" disabled>--請選擇分類--</option>
            <option v-for="t in types" :key="t.questiontypeId" :value="t.questiontypeId">{{ t.title }}</option>
          </select>
        </div>
        <div class="form-field form-field--fixed">
          <label class="label">排序</label>
          <input v-model.number="form.sort" class="input" type="number" min="0" />
        </div>
      </div>

      <div class="form-row">
        <div class="form-field form-field--grow">
          <label class="label label--required">標題</label>
          <input v-model="form.title" class="input" type="text" maxlength="150" required />
        </div>
      </div>

      <div class="form-row">
        <div class="form-field form-field--grow">
          <label class="label label--required">內容</label>
          <HtmlEditor v-model="form.answer" />
        </div>
      </div>

      <div class="form-actions">
        <button type="button" class="btn btn--ghost" @click="router.push('/admin/questions')">取消</button>
        <button type="submit" class="btn btn--primary" :disabled="saving || types.length === 0">
          {{ saving ? '儲存中…' : '儲存' }}
        </button>
      </div>
    </form>
  </main>
</template>

<style scoped>
.form-header { display:flex; align-items:center; gap:1rem; margin-bottom:1.5rem; flex-wrap:wrap; }
.form-title { margin:0; font-family:var(--tf-font-heading,inherit); color:var(--tf-color-primary-dark); font-size:1.5rem; }
.form-card { background:#fff; border:1px solid var(--tf-color-border); border-radius:10px; padding:1.75rem; }
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
</style>

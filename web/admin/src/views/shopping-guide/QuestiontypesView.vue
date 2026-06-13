<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { apiFetch } from '../../lib/apiClient'

interface Questiontype {
  questiontypeId: string
  title: string
  sort: number
  questionCount: number
}

interface QuestiontypeForm {
  title: string
  sort: number
}

function emptyForm(): QuestiontypeForm {
  return { title: '', sort: 0 }
}

// ─── list state ───────────────────────────────────────────────────────────────
const items = ref<Questiontype[]>([])
const loading = ref(false)
const error = ref('')

async function loadList() {
  loading.value = true
  error.value = ''
  try {
    items.value = await apiFetch<Questiontype[]>('/admin/questiontypes')
  } catch (e: any) {
    error.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

onMounted(loadList)

// ─── side panel state ─────────────────────────────────────────────────────────
type PanelMode = 'create' | 'edit' | null
const panelMode = ref<PanelMode>(null)
const editingId = ref<string | null>(null)
const form = ref<QuestiontypeForm>(emptyForm())
const panelLoading = ref(false)
const panelError = ref('')

function openCreate() {
  panelMode.value = 'create'
  editingId.value = null
  form.value = emptyForm()
  panelError.value = ''
}

async function openEdit(item: Questiontype) {
  panelMode.value = 'edit'
  editingId.value = item.questiontypeId
  panelError.value = ''
  panelLoading.value = true
  try {
    const detail = await apiFetch<Questiontype>(`/admin/questiontypes/${item.questiontypeId}`)
    form.value = { title: detail.title, sort: detail.sort }
  } catch (e: any) {
    panelError.value = e.message ?? '載入明細失敗'
  } finally {
    panelLoading.value = false
  }
}

function closePanel() {
  panelMode.value = null
  editingId.value = null
  panelError.value = ''
}

async function submitForm() {
  if (!form.value.title.trim()) {
    panelError.value = '分類標題為必填'
    return
  }
  panelLoading.value = true
  panelError.value = ''
  try {
    const body = { title: form.value.title, sort: form.value.sort }
    if (panelMode.value === 'create') {
      await apiFetch('/admin/questiontypes', { method: 'POST', body: JSON.stringify(body) })
    } else {
      await apiFetch(`/admin/questiontypes/${editingId.value}`, { method: 'PUT', body: JSON.stringify(body) })
    }
    closePanel()
    await loadList()
  } catch (e: any) {
    panelError.value = e.message ?? '儲存失敗'
  } finally {
    panelLoading.value = false
  }
}

// ─── delete confirm ───────────────────────────────────────────────────────────
const deleteTarget = ref<Questiontype | null>(null)
const deleteLoading = ref(false)
const deleteError = ref('')

function askDelete(item: Questiontype) {
  deleteTarget.value = item
  deleteError.value = ''
}

function cancelDelete() {
  deleteTarget.value = null
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleteLoading.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/questiontypes/${deleteTarget.value.questiontypeId}`, { method: 'DELETE' })
    deleteTarget.value = null
    await loadList()
  } catch (e: any) {
    deleteError.value = e.message ?? '刪除失敗'
  } finally {
    deleteLoading.value = false
  }
}
</script>

<template>
  <main>
    <div class="page-header">
      <h1 class="page-title">購物說明分類</h1>
      <button class="btn btn--primary" @click="openCreate">+ 新增分類</button>
    </div>

    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="error" class="state-msg state-msg--error">{{ error }}</div>

    <template v-else>
      <div class="card">
        <table class="data-table">
          <thead>
            <tr>
              <th style="width:80px">排序</th>
              <th>分類標題</th>
              <th style="width:120px">購物說明數</th>
              <th class="action-th"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="items.length === 0">
              <td colspan="4" class="empty-cell">目前沒有購物說明分類</td>
            </tr>
            <tr v-for="t in items" :key="t.questiontypeId" class="data-table__row">
              <td>{{ t.sort }}</td>
              <td>{{ t.title }}</td>
              <td>{{ t.questionCount }}</td>
              <td class="action-cell">
                <button class="btn btn--sm btn--ghost" @click="openEdit(t)">編輯</button>
                <button class="btn btn--sm btn--danger-ghost" style="margin-left:0.375rem" @click="askDelete(t)">刪除</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <!-- ── Side panel ─────────────────────────────────────────────────────── -->
    <Teleport to="body">
      <div v-if="panelMode" class="panel-overlay" @click.self="closePanel">
        <aside class="side-panel" role="complementary" :aria-label="panelMode === 'create' ? '新增購物說明分類' : '編輯購物說明分類'">
          <div class="panel__header">
            <h2 class="panel__title">{{ panelMode === 'create' ? '新增購物說明分類' : '編輯購物說明分類' }}</h2>
            <button class="panel__close" aria-label="關閉" @click="closePanel">
              <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div class="panel__body" :aria-busy="panelLoading">
            <div class="form-field">
              <label class="form-field__label" for="f-title">分類標題 <span class="required">*</span></label>
              <input id="f-title" v-model="form.title" type="text" maxlength="50" placeholder="例：訂購流程" class="form-field__input" />
            </div>

            <div class="form-field">
              <label class="form-field__label" for="f-sort">排序</label>
              <input id="f-sort" v-model.number="form.sort" type="number" min="0" class="form-field__input" />
            </div>

            <div v-if="panelError" class="form-error">{{ panelError }}</div>
          </div>

          <div class="panel__footer">
            <button class="btn btn--ghost" :disabled="panelLoading" @click="closePanel">取消</button>
            <button class="btn btn--primary" :disabled="panelLoading || !form.title.trim()" @click="submitForm">
              {{ panelLoading ? '儲存中…' : '儲存' }}
            </button>
          </div>
        </aside>
      </div>
    </Teleport>

    <!-- ── Delete confirm modal ───────────────────────────────────────────── -->
    <Teleport to="body">
      <div v-if="deleteTarget" class="modal-overlay" @click.self="cancelDelete">
        <div class="modal" role="alertdialog" aria-label="確認刪除">
          <div class="modal__header">
            <h2 class="modal__title">確認刪除分類</h2>
          </div>
          <div class="modal__body">
            <p class="delete-msg">
              確定要刪除分類 <strong>{{ deleteTarget.title }}</strong> 嗎？
              <template v-if="deleteTarget.questionCount > 0">
                此分類底下的 <strong>{{ deleteTarget.questionCount }}</strong> 筆購物說明也會一併刪除。
              </template>
              此操作無法復原。
            </p>
            <div v-if="deleteError" class="form-error mt-2">{{ deleteError }}</div>
          </div>
          <div class="modal__footer">
            <button class="btn btn--ghost" :disabled="deleteLoading" @click="cancelDelete">取消</button>
            <button class="btn btn--danger" :disabled="deleteLoading" @click="confirmDelete">
              {{ deleteLoading ? '刪除中…' : '確認刪除' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </main>
</template>

<style scoped>
.page-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.5rem; }
.page-title { font-size: 1.5rem; font-weight: 700; color: var(--tf-color-primary-dark); letter-spacing: -0.02em; margin: 0; font-family: var(--tf-font-heading, inherit); }

/* Card */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }

/* Tables */
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.9rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 130px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: #94a3b8; padding: 3rem; }
.action-cell { white-space: nowrap; text-align: right; }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: all 0.15s; white-space: nowrap; font-family: inherit; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
.btn--danger { background: #ef4444; color: #fff; border-color: #ef4444; }
.btn--danger:hover:not(:disabled) { background: #dc2626; }
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }

/* State messages */
.state-msg { padding: 2rem; text-align: center; color: #94a3b8; }
.state-msg--error { color: #dc2626; }

/* ─── Side panel ─────────────────────────────────────────────────────────── */
.panel-overlay {
  position: fixed; inset: 0; z-index: 50;
  background: rgba(15, 23, 42, 0.4);
  backdrop-filter: blur(1px);
  display: flex; justify-content: flex-end;
  animation: fadeIn 0.15s ease;
}
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }

.side-panel {
  width: 100%;
  max-width: 440px;
  height: 100%;
  background: #fff;
  box-shadow: -8px 0 40px rgba(0,0,0,0.15);
  display: flex;
  flex-direction: column;
  animation: slideInRight 0.22s cubic-bezier(0.25, 0.46, 0.45, 0.94);
}
@keyframes slideInRight { from { transform: translateX(100%); } to { transform: none; } }

.panel__header { display: flex; align-items: center; justify-content: space-between; padding: 1.25rem 1.5rem; border-bottom: 1px solid #e2e8f0; }
.panel__title { font-size: 1.05rem; font-weight: 700; color: #1e293b; margin: 0; }
.panel__close { background: none; border: none; cursor: pointer; color: #94a3b8; padding: 0.25rem; border-radius: 4px; display: flex; }
.panel__close:hover { color: #475569; background: #f1f5f9; }
.panel__body { flex: 1; overflow-y: auto; padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
.panel__footer { padding: 1rem 1.5rem; border-top: 1px solid #e2e8f0; display: flex; justify-content: flex-end; gap: 0.5rem; }

/* Form elements */
.form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.required { color: #ef4444; }
.form-field__input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem;
  color: #1e293b;
  background: #fff;
  transition: border-color 0.15s, box-shadow 0.15s;
  font-family: inherit;
}
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38,183,188,0.15); }

.form-error { color: #dc2626; font-size: 0.85rem; }
.mt-2 { margin-top: 0.5rem; }

/* ─── Delete modal ───────────────────────────────────────────────────────── */
.modal-overlay {
  position: fixed; inset: 0; z-index: 60;
  background: rgba(15, 23, 42, 0.55);
  backdrop-filter: blur(2px);
  display: flex; align-items: center; justify-content: center;
  padding: 1rem;
}
.modal {
  background: #fff;
  border-radius: 14px;
  width: 100%;
  max-width: 440px;
  box-shadow: 0 20px 60px rgba(0,0,0,0.2);
  animation: slideUp 0.2s ease;
}
@keyframes slideUp { from { opacity: 0; transform: translateY(16px); } to { opacity: 1; transform: none; } }

.modal__header { padding: 1.25rem 1.5rem 0; }
.modal__title { font-size: 1.05rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1rem 1.5rem; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 0 1.5rem 1.25rem; }
.delete-msg { color: #475569; font-size: 0.9rem; line-height: 1.6; }
</style>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

interface Exchange {
  exchangeId: string
  title: string
  rate: number
}

const items = ref<Exchange[]>([])
const loading = ref(false)
const error = ref('')

const panelMode = ref<'create' | 'edit' | null>(null)
const panelTarget = ref<Exchange | null>(null)
const form = reactive({ title: '', rate: '' })
const saving = ref(false)
const saveError = ref('')

const deleteTarget = ref<Exchange | null>(null)
const deleteError = ref('')
const deleting = ref(false)

function errMsg(e: unknown, fallback: string) {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    items.value = await apiFetch<Exchange[]>('/admin/exchanges')
  } catch (e) {
    error.value = errMsg(e, '載入失敗')
  } finally {
    loading.value = false
  }
}

function openCreate() {
  form.title = ''
  form.rate = ''
  saveError.value = ''
  panelTarget.value = null
  panelMode.value = 'create'
}

function openEdit(e: Exchange) {
  form.title = e.title
  form.rate = String(e.rate)
  saveError.value = ''
  panelTarget.value = e
  panelMode.value = 'edit'
}

function closePanel() { panelMode.value = null }

async function save() {
  if (!form.title.trim()) { saveError.value = '幣別名稱為必填'; return }
  const rate = Number(form.rate)
  if (!rate || rate <= 0) { saveError.value = '匯率必須大於 0'; return }
  saving.value = true
  saveError.value = ''
  try {
    const body = JSON.stringify({ title: form.title, rate })
    if (panelMode.value === 'create') {
      await apiFetch('/admin/exchanges', { method: 'POST', body })
    } else {
      await apiFetch(`/admin/exchanges/${panelTarget.value!.exchangeId}`, { method: 'PUT', body })
    }
    closePanel()
    await load()
  } catch (e) {
    saveError.value = errMsg(e, '儲存失敗')
  } finally {
    saving.value = false
  }
}

function askDelete(e: Exchange) {
  deleteTarget.value = e
  deleteError.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/exchanges/${deleteTarget.value.exchangeId}`, { method: 'DELETE' })
    deleteTarget.value = null
    await load()
  } catch (e) {
    deleteError.value = errMsg(e, '刪除失敗')
  } finally {
    deleting.value = false
  }
}

load()
</script>

<template>
  <main class="exchanges">
    <div class="exchanges__header">
      <h1 class="exchanges__title">匯率維護</h1>
      <button class="btn btn--primary" @click="openCreate">+ 新增幣別</button>
    </div>

    <p v-if="loading" class="exchanges__muted">載入中…</p>
    <p v-if="error" class="exchanges__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th>幣別名稱</th>
            <th style="width:10rem">匯率</th>
            <th class="action-th"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="e in items" :key="e.exchangeId" class="data-table__row">
            <td class="font-semibold">{{ e.title }}</td>
            <td class="font-mono">{{ e.rate }}</td>
            <td class="action-cell">
              <button class="btn btn--sm btn--ghost" @click="openEdit(e)">編輯</button>
              <button class="btn btn--sm btn--danger-ghost" @click="askDelete(e)">刪除</button>
            </td>
          </tr>
          <tr v-if="items.length === 0">
            <td colspan="3" class="empty-cell">目前沒有幣別資料</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Side panel -->
    <div v-if="panelMode" class="panel-overlay" @click.self="closePanel">
      <aside class="side-panel">
        <div class="panel__header">
          <h2 class="panel__title">{{ panelMode === 'create' ? '新增幣別' : '編輯幣別' }}</h2>
          <button class="panel__close" @click="closePanel" aria-label="關閉">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div class="panel__body">
          <div class="form-field">
            <label class="form-field__label">幣別名稱 <span class="required">*</span></label>
            <input v-model="form.title" class="form-field__input" placeholder="例：美金 USD" />
          </div>
          <div class="form-field">
            <label class="form-field__label">匯率 <span class="required">*</span></label>
            <input v-model="form.rate" type="number" step="0.0001" min="0" class="form-field__input" placeholder="例：31.5" />
          </div>
          <p v-if="saveError" class="form-error">{{ saveError }}</p>
        </div>
        <div class="panel__footer">
          <button class="btn btn--ghost" @click="closePanel">取消</button>
          <button class="btn btn--primary" :disabled="saving" @click="save">
            {{ saving ? '儲存中…' : '儲存' }}
          </button>
        </div>
      </aside>
    </div>

    <!-- Delete modal -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget = null">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認刪除幣別</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除幣別 <strong>{{ deleteTarget.title }}</strong> 嗎？</p>
          <p class="modal__hint">若已被採購單引用，系統將拒絕刪除。</p>
          <p v-if="deleteError" class="form-error">{{ deleteError }}</p>
        </div>
        <div class="modal__footer">
          <button class="btn btn--ghost" @click="deleteTarget = null">取消</button>
          <button class="btn btn--danger" :disabled="deleting" @click="confirmDelete">
            {{ deleting ? '刪除中…' : '確認刪除' }}
          </button>
        </div>
      </div>
    </div>
  </main>
</template>

<style scoped>
.exchanges {}
.exchanges__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.exchanges__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.exchanges__error { color: #dc3545; }
.exchanges__muted { color: var(--tf-color-muted); }

.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 130px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }
.font-semibold { font-weight: 600; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }

.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: all 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
.btn--danger { background: #dc3545; color: #fff; border-color: #dc3545; }
.btn--danger:hover:not(:disabled) { background: #b02a37; }
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }

.panel-overlay { position: fixed; inset: 0; z-index: 50; background: rgba(15, 23, 42, 0.4); backdrop-filter: blur(1px); display: flex; justify-content: flex-end; animation: fadeIn 0.15s ease; }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
.side-panel { width: 100%; max-width: 440px; height: 100%; background: #fff; box-shadow: -8px 0 40px rgba(0,0,0,0.15); display: flex; flex-direction: column; animation: slideInRight 0.22s cubic-bezier(0.25,0.46,0.45,0.94); }
@keyframes slideInRight { from { transform: translateX(100%); } to { transform: none; } }
.panel__header { display: flex; align-items: center; justify-content: space-between; padding: 1.25rem 1.5rem; border-bottom: 1px solid var(--tf-color-border); }
.panel__title { font-size: 1.05rem; font-weight: 700; color: #1e293b; margin: 0; }
.panel__close { background: none; border: none; cursor: pointer; color: var(--tf-color-muted); padding: 0.25rem; border-radius: 4px; display: flex; }
.panel__close:hover { color: #475569; background: #f1f5f9; }
.panel__body { flex: 1; overflow-y: auto; padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
.panel__footer { padding: 1rem 1.5rem; border-top: 1px solid var(--tf-color-border); display: flex; justify-content: flex-end; gap: 0.5rem; }

.form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.required { color: #ef4444; }
.form-field__input { padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; color: #1e293b; background: #fff; transition: border-color 0.15s; font-family: inherit; }
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38,183,188,0.15); }
.form-error { color: #dc3545; font-size: 0.85rem; }

.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15,23,42,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 380px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1.25rem 1.4rem; display: flex; flex-direction: column; gap: 0.5rem; }
.modal__hint { font-size: 0.85rem; color: var(--tf-color-muted); margin: 0; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }
</style>

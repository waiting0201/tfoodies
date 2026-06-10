<script setup lang="ts">
import { ref, reactive } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

interface Warehouse {
  warehouseid: string
  warehousetype: number
  title: string
}

const WAREHOUSE_TYPES: Record<number, string> = { 1: '線上倉', 2: '線下倉', 3: '瑕疵品倉' }

const items = ref<Warehouse[]>([])
const loading = ref(false)
const error = ref('')

const panelMode = ref<'create' | 'edit' | null>(null)
const panelTarget = ref<Warehouse | null>(null)
const form = reactive({ title: '', warehouseType: 1 })
const saving = ref(false)
const saveError = ref('')

const deleteTarget = ref<Warehouse | null>(null)
const deleteError = ref('')
const deleting = ref(false)

async function load() {
  loading.value = true
  error.value = ''
  try {
    items.value = await apiFetch<Warehouse[]>('/admin/warehouses')
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

function openCreate() {
  form.title = ''
  form.warehouseType = 1
  saveError.value = ''
  panelTarget.value = null
  panelMode.value = 'create'
}

function openEdit(w: Warehouse) {
  form.title = w.title
  form.warehouseType = w.warehousetype
  saveError.value = ''
  panelTarget.value = w
  panelMode.value = 'edit'
}

function closePanel() { panelMode.value = null }

async function save() {
  if (!form.title.trim()) { saveError.value = '名稱為必填'; return }
  saving.value = true
  saveError.value = ''
  try {
    const payload = { title: form.title, warehouseType: form.warehouseType }
    if (panelMode.value === 'create') {
      await apiFetch('/admin/warehouses', { method: 'POST', body: JSON.stringify(payload) })
    } else {
      await apiFetch(`/admin/warehouses/${panelTarget.value!.warehouseid}`, { method: 'PUT', body: JSON.stringify(payload) })
    }
    closePanel()
    await load()
  } catch (e) {
    saveError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '儲存失敗'
  } finally {
    saving.value = false
  }
}

function askDelete(w: Warehouse) {
  deleteTarget.value = w
  deleteError.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/warehouses/${deleteTarget.value.warehouseid}`, { method: 'DELETE' })
    deleteTarget.value = null
    await load()
  } catch (e) {
    deleteError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '刪除失敗'
  } finally {
    deleting.value = false
  }
}

load()
</script>

<template>
  <main class="wh">
    <div class="wh__header">
      <h1 class="wh__title">倉庫管理</h1>
      <button class="btn btn--primary" @click="openCreate">+ 新增倉庫</button>
    </div>

    <p v-if="loading" class="wh__muted">載入中…</p>
    <p v-if="error" class="wh__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th>倉庫名稱</th>
            <th style="width:8rem">類型</th>
            <th class="action-th"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="w in items" :key="w.warehouseid" class="data-table__row">
            <td class="font-semibold">{{ w.title }}</td>
            <td>
              <span class="badge badge--type">{{ WAREHOUSE_TYPES[w.warehousetype] ?? `類型${w.warehousetype}` }}</span>
            </td>
            <td class="action-cell">
              <button class="btn btn--sm btn--ghost" @click="openEdit(w)">編輯</button>
              <button class="btn btn--sm btn--danger-ghost" @click="askDelete(w)">刪除</button>
            </td>
          </tr>
          <tr v-if="items.length === 0">
            <td colspan="3" class="empty-cell">目前沒有倉庫資料</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Side panel -->
    <div v-if="panelMode" class="panel-overlay" @click.self="closePanel">
      <aside class="side-panel">
        <div class="panel__header">
          <h2 class="panel__title">{{ panelMode === 'create' ? '新增倉庫' : '編輯倉庫' }}</h2>
          <button class="panel__close" @click="closePanel">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div class="panel__body">
          <div class="form-field">
            <label class="form-field__label">倉庫名稱 <span class="required">*</span></label>
            <input v-model="form.title" class="form-field__input" placeholder="例：台中主倉" />
          </div>
          <div class="form-field">
            <label class="form-field__label">倉庫類型</label>
            <select v-model.number="form.warehouseType" class="form-field__input">
              <option :value="1">線上倉</option>
              <option :value="2">線下倉</option>
              <option :value="3">瑕疵品倉</option>
            </select>
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
          <h3 class="modal__title">確認刪除倉庫</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除倉庫 <strong>{{ deleteTarget.title }}</strong> 嗎？</p>
          <p class="modal__hint">若倉庫仍有庫存，系統將拒絕刪除。</p>
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
.wh {}
.wh__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.wh__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.wh__error { color: #dc3545; }
.wh__muted { color: var(--tf-color-muted); }

.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 130px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.font-semibold { font-weight: 600; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }

.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.75rem; font-weight: 600; }
.badge--type { background: #e0f2fe; color: #0369a1; }

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

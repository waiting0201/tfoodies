<script setup lang="ts">
import { ref, reactive } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

interface Supplier {
  supplierId: string
  title: string
  contactor: string
  phone: string
  address: string
}

function errMsg(e: unknown, fallback: string) {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

const suppliers = ref<Supplier[]>([])
const loading = ref(false)
const error = ref('')

const panelOpen = ref(false)
const editingId = ref<string | null>(null)
const form = reactive({ title: '', contactor: '', phone: '', address: '' })
const saving = ref(false)
const formError = ref('')

const deleteTarget = ref<Supplier | null>(null)
const deleteError = ref('')
const deleting = ref(false)

async function load() {
  loading.value = true
  error.value = ''
  try {
    suppliers.value = await apiFetch<Supplier[]>('/admin/suppliers')
  } catch (e) {
    error.value = errMsg(e, '載入失敗')
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = null
  form.title = ''; form.contactor = ''; form.phone = ''; form.address = ''
  formError.value = ''
  panelOpen.value = true
}

function openEdit(s: Supplier) {
  editingId.value = s.supplierId
  form.title = s.title; form.contactor = s.contactor ?? ''
  form.phone = s.phone ?? ''; form.address = s.address ?? ''
  formError.value = ''
  panelOpen.value = true
}

async function save() {
  if (!form.title.trim()) { formError.value = '請輸入供應商名稱'; return }
  saving.value = true
  formError.value = ''
  try {
    const body = JSON.stringify({ title: form.title, contactor: form.contactor, phone: form.phone, address: form.address })
    if (editingId.value)
      await apiFetch(`/admin/suppliers/${editingId.value}`, { method: 'PUT', body })
    else
      await apiFetch('/admin/suppliers', { method: 'POST', body })
    panelOpen.value = false
    await load()
  } catch (e) {
    formError.value = errMsg(e, '儲存失敗')
  } finally {
    saving.value = false
  }
}

function askDelete(s: Supplier) { deleteTarget.value = s; deleteError.value = '' }

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/suppliers/${deleteTarget.value.supplierId}`, { method: 'DELETE' })
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
  <main class="suppliers">
    <div class="suppliers__header">
      <h1 class="suppliers__title">供應商維護</h1>
      <button class="btn btn--primary" @click="openCreate">+ 新增供應商</button>
    </div>

    <p v-if="loading" class="suppliers__muted">載入中…</p>
    <p v-if="error" class="suppliers__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th>供應商名稱</th>
            <th>聯絡人</th>
            <th>電話</th>
            <th>地址</th>
            <th class="action-th"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="s in suppliers" :key="s.supplierId" class="data-table__row">
            <td class="font-semibold">{{ s.title }}</td>
            <td>{{ s.contactor || '—' }}</td>
            <td>{{ s.phone || '—' }}</td>
            <td class="text-muted">{{ s.address || '—' }}</td>
            <td class="action-cell">
              <button class="btn btn--sm btn--ghost" @click="openEdit(s)">編輯</button>
              <button class="btn btn--sm btn--danger-ghost" @click="askDelete(s)">刪除</button>
            </td>
          </tr>
          <tr v-if="suppliers.length === 0">
            <td colspan="5" class="empty-cell">目前沒有供應商資料</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 新增/編輯 滑出面板 -->
    <div v-if="panelOpen" class="panel-overlay" @click.self="panelOpen = false">
      <div class="side-panel">
        <div class="panel__header">
          <h2 class="panel__title">{{ editingId ? '編輯供應商' : '新增供應商' }}</h2>
          <button class="panel__close" aria-label="關閉" @click="panelOpen = false">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div class="panel__body">
          <p v-if="formError" class="form-error">{{ formError }}</p>
          <div class="form-field">
            <label class="form-field__label">供應商名稱 <span class="required">*</span></label>
            <input v-model="form.title" class="form-field__input" type="text" placeholder="供應商名稱" />
          </div>
          <div class="form-field">
            <label class="form-field__label">聯絡人</label>
            <input v-model="form.contactor" class="form-field__input" type="text" placeholder="聯絡人姓名" />
          </div>
          <div class="form-field">
            <label class="form-field__label">電話</label>
            <input v-model="form.phone" class="form-field__input" type="text" placeholder="02-xxxx-xxxx" />
          </div>
          <div class="form-field">
            <label class="form-field__label">地址</label>
            <input v-model="form.address" class="form-field__input" type="text" placeholder="供應商地址" />
          </div>
        </div>
        <div class="panel__footer">
          <button class="btn btn--ghost" @click="panelOpen = false">取消</button>
          <button class="btn btn--primary" :disabled="saving" @click="save">
            {{ saving ? '儲存中…' : '儲存' }}
          </button>
        </div>
      </div>
    </div>

    <!-- 刪除確認 Modal -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget = null">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認刪除供應商</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除供應商 <strong>{{ deleteTarget.title }}</strong> 嗎？此操作無法復原。</p>
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
.suppliers {}
.suppliers__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.suppliers__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.suppliers__error { color: #dc3545; }
.suppliers__muted { color: var(--tf-color-muted); }

.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 160px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }
.font-semibold { font-weight: 600; }
.text-muted { color: var(--tf-color-muted); font-size: 0.85rem; }

.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: all 0.15s; white-space: nowrap; text-decoration: none; font-family: inherit; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
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
.modal__body { padding: 1.25rem 1.4rem; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }
</style>

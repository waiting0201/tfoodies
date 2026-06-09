<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { apiFetch } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────
interface AdminAccount {
  adminId: string
  username: string
  name: string
  email: string
  isEnable: boolean
}
interface Permission {
  module: string
  label: string
  children?: Permission[]
  granted: boolean
}

// ─── list ─────────────────────────────────────────────────────────────────────
const accounts = ref<AdminAccount[]>([])
const loading = ref(false)
const error = ref('')

async function loadAccounts() {
  loading.value = true; error.value = ''
  try {
    const d = await apiFetch<AdminAccount[] | { items: AdminAccount[] }>('/admin/admin-accounts')
    accounts.value = Array.isArray(d) ? d : d.items ?? []
  } catch (e: any) { error.value = e.message ?? '載入失敗' }
  finally { loading.value = false }
}

// ─── create form ──────────────────────────────────────────────────────────────
interface CreateForm { username: string; password: string; name: string; email: string }
const createForm = ref<CreateForm | null>(null)
const createSaving = ref(false)
const createError = ref('')

function openCreateForm() {
  createForm.value = { username: '', password: '', name: '', email: '' }
  createError.value = ''
  editForm.value = null
}
async function submitCreate() {
  if (!createForm.value) return
  createSaving.value = true; createError.value = ''
  try {
    await apiFetch('/admin/admin-accounts', { method: 'POST', body: JSON.stringify(createForm.value) })
    createForm.value = null
    await loadAccounts()
  } catch (e: any) { createError.value = e.message ?? '建立失敗' }
  finally { createSaving.value = false }
}

// ─── edit form ────────────────────────────────────────────────────────────────
interface EditForm { adminId: string; name: string; email: string; password: string; isEnable: boolean }
const editForm = ref<EditForm | null>(null)
const editSaving = ref(false)
const editError = ref('')

function openEditForm(a: AdminAccount) {
  editForm.value = { adminId: a.adminId, name: a.name, email: a.email, password: '', isEnable: a.isEnable }
  editError.value = ''
  createForm.value = null
  permModal.value = null
}
async function submitEdit() {
  if (!editForm.value) return
  editSaving.value = true; editError.value = ''
  try {
    const body: Record<string, any> = {
      name: editForm.value.name,
      email: editForm.value.email,
      isEnable: editForm.value.isEnable,
    }
    if (editForm.value.password) body.password = editForm.value.password
    await apiFetch(`/admin/admin-accounts/${editForm.value.adminId}`, { method: 'PUT', body: JSON.stringify(body) })
    editForm.value = null
    await loadAccounts()
  } catch (e: any) { editError.value = e.message ?? '儲存失敗' }
  finally { editSaving.value = false }
}

// ─── permissions modal ────────────────────────────────────────────────────────
interface PermModal { adminId: string; name: string; permissions: Permission[] }
const permModal = ref<PermModal | null>(null)
const permLoading = ref(false)
const permError = ref('')
const permSaving = ref(false)
const permSaveError = ref('')

async function openPermModal(a: AdminAccount) {
  permLoading.value = true; permError.value = ''
  permModal.value = { adminId: a.adminId, name: a.name, permissions: [] }
  createForm.value = null; editForm.value = null
  try {
    const data = await apiFetch<Permission[]>(`/admin/admin-accounts/${a.adminId}/permissions`)
    permModal.value.permissions = data
  } catch (e: any) { permError.value = e.message ?? '載入失敗' }
  finally { permLoading.value = false }
}

function collectGranted(perms: Permission[]): string[] {
  const granted: string[] = []
  function walk(p: Permission) {
    if (p.granted) granted.push(p.module)
    p.children?.forEach(walk)
  }
  perms.forEach(walk)
  return granted
}

async function savePermissions() {
  if (!permModal.value) return
  permSaving.value = true; permSaveError.value = ''
  try {
    const modules = collectGranted(permModal.value.permissions)
    await apiFetch(`/admin/admin-accounts/${permModal.value.adminId}/permissions`, {
      method: 'PUT',
      body: JSON.stringify({ modules }),
    })
    permModal.value = null
  } catch (e: any) { permSaveError.value = e.message ?? '儲存失敗' }
  finally { permSaving.value = false }
}

onMounted(loadAccounts)
</script>

<template>
  <main class="adm-accounts">
    <div class="page-header">
      <h1 class="page-header__title">管理員帳號</h1>
      <button class="btn btn--primary btn--sm" @click="openCreateForm">+ 新增帳號</button>
    </div>

    <!-- Create form -->
    <div v-if="createForm" class="inline-form inline-form--create">
      <h4 class="inline-form__title">新增管理員</h4>
      <div class="form-grid">
        <label>帳號 <input v-model="createForm.username" type="text" autocomplete="off" /></label>
        <label>密碼 <input v-model="createForm.password" type="password" autocomplete="new-password" /></label>
        <label>姓名 <input v-model="createForm.name" type="text" /></label>
        <label>Email <input v-model="createForm.email" type="email" /></label>
      </div>
      <div v-if="createError" class="form-error">{{ createError }}</div>
      <div class="form-actions">
        <button class="btn btn--ghost" @click="createForm = null">取消</button>
        <button class="btn btn--primary" :disabled="createSaving" @click="submitCreate">{{ createSaving ? '建立中…' : '建立' }}</button>
      </div>
    </div>

    <!-- Account list -->
    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="error" class="state-msg state-msg--error">{{ error }}</div>
    <template v-else>
      <div class="card">
        <table class="data-table">
          <thead>
            <tr><th>帳號</th><th>姓名</th><th>Email</th><th>啟用</th><th></th></tr>
          </thead>
          <tbody>
            <template v-for="a in accounts" :key="a.adminId">
              <tr>
                <td>{{ a.username }}</td>
                <td>{{ a.name }}</td>
                <td>{{ a.email }}</td>
                <td>
                  <span class="badge" :class="a.isEnable ? 'badge--active' : 'badge--inactive'">
                    {{ a.isEnable ? '啟用' : '停用' }}
                  </span>
                </td>
                <td class="action-cell">
                  <button class="btn btn--ghost btn--sm" @click="openEditForm(a)">編輯</button>
                  <button class="btn btn--outline btn--sm" @click="openPermModal(a)">權限</button>
                </td>
              </tr>

              <!-- Inline edit row -->
              <tr v-if="editForm && editForm.adminId === a.adminId" class="inline-edit-row">
                <td colspan="5">
                  <div class="inline-form">
                    <h4 class="inline-form__title">編輯：{{ a.username }}</h4>
                    <div class="form-grid">
                      <label>姓名 <input v-model="editForm.name" type="text" /></label>
                      <label>Email <input v-model="editForm.email" type="email" /></label>
                      <label>新密碼（留空不變） <input v-model="editForm.password" type="password" autocomplete="new-password" /></label>
                      <label class="form-grid__checkbox">
                        <span>啟用</span>
                        <input v-model="editForm.isEnable" type="checkbox" />
                      </label>
                    </div>
                    <div v-if="editError" class="form-error">{{ editError }}</div>
                    <div class="form-actions">
                      <button class="btn btn--ghost" @click="editForm = null">取消</button>
                      <button class="btn btn--primary" :disabled="editSaving" @click="submitEdit">{{ editSaving ? '儲存中…' : '儲存' }}</button>
                    </div>
                  </div>
                </td>
              </tr>
            </template>
            <tr v-if="accounts.length === 0">
              <td colspan="5" class="empty-cell">尚無管理員帳號</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <!-- Permissions modal overlay -->
    <div v-if="permModal" class="modal-overlay" @click.self="permModal = null">
      <div class="modal">
        <div class="modal__header">
          <h3>{{ permModal.name }} — 權限設定</h3>
          <button class="modal__close" @click="permModal = null">✕</button>
        </div>
        <div class="modal__body">
          <div v-if="permLoading" class="state-msg">載入中…</div>
          <div v-else-if="permError" class="state-msg state-msg--error">{{ permError }}</div>
          <template v-else>
            <div v-if="permModal.permissions.length === 0" class="state-msg">無權限設定</div>
            <div v-else class="perm-tree">
              <div v-for="p in permModal.permissions" :key="p.module" class="perm-node">
                <label class="perm-label perm-label--parent">
                  <input v-model="p.granted" type="checkbox" />
                  <span>{{ p.label || p.module }}</span>
                </label>
                <div v-if="p.children?.length" class="perm-children">
                  <div v-for="c in p.children" :key="c.module" class="perm-node">
                    <label class="perm-label">
                      <input v-model="c.granted" type="checkbox" />
                      <span>{{ c.label || c.module }}</span>
                    </label>
                    <div v-if="c.children?.length" class="perm-children">
                      <div v-for="gc in c.children" :key="gc.module" class="perm-node">
                        <label class="perm-label">
                          <input v-model="gc.granted" type="checkbox" />
                          <span>{{ gc.label || gc.module }}</span>
                        </label>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </template>
        </div>
        <div class="modal__footer">
          <div v-if="permSaveError" class="form-error">{{ permSaveError }}</div>
          <div class="form-actions">
            <button class="btn btn--ghost" @click="permModal = null">取消</button>
            <button class="btn btn--primary" :disabled="permSaving || permLoading" @click="savePermissions">{{ permSaving ? '儲存中…' : '儲存權限' }}</button>
          </div>
        </div>
      </div>
    </div>
  </main>
</template>

<style scoped>
.adm-accounts { }

.page-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.5rem; }
.page-header__title { font-family: var(--tf-font-heading, inherit); color: var(--tf-color-primary-dark); margin: 0; }

/* Inline form */
.inline-form { background: #fffbf7; border-left: 3px solid var(--tf-color-accent); padding: 1rem 1.25rem; margin-bottom: 1rem; border-radius: 0 4px 4px 0; }
.inline-form--create { margin-bottom: 1.5rem; }
.inline-form__title { margin: 0 0 0.75rem; font-size: 0.95rem; color: var(--tf-color-accent); font-weight: 600; }
.form-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 0.75rem 1rem; margin-bottom: 0.75rem; }
.form-grid label { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.85rem; color: #444; }
.form-grid input[type="text"], .form-grid input[type="email"], .form-grid input[type="password"] { padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; }
.form-grid__checkbox { flex-direction: row; align-items: center; gap: 0.5rem; }
.form-grid__checkbox input { width: auto; }
.form-error { color: #c0392b; font-size: 0.85rem; margin-bottom: 0.5rem; }
.form-actions { display: flex; gap: 0.5rem; }

/* Inline edit row */
.inline-edit-row > td { padding: 0; }
.inline-edit-row .inline-form { margin-bottom: 0; border-radius: 0; border-left-color: var(--tf-color-primary); background: #f5fdf7; }
.inline-edit-row .inline-form__title { color: var(--tf-color-primary-dark); }

/* Card wrapper */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }

/* Table */
.data-table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; border-bottom: 2px solid #d0e2d4; }
.data-table td { padding: 0.6rem 0.75rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; align-items: center; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2rem; }

/* Badges */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 600; }
.badge--active { background: #d4edda; color: #155724; }
.badge--inactive { background: #f0f0f0; color: #666; }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: #f0f5f1; }
.btn--outline { background: transparent; color: #666; border-color: #bbb; }
.btn--outline:hover:not(:disabled) { background: #f5f5f5; }

/* State messages */
.state-msg { padding: 2rem; text-align: center; color: var(--tf-color-muted); }
.state-msg--error { color: #c0392b; }

/* Modal */
.modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.45); z-index: 200; display: flex; align-items: center; justify-content: center; }
.modal { background: #fff; border-radius: 8px; width: 560px; max-width: 95vw; max-height: 85vh; display: flex; flex-direction: column; box-shadow: 0 8px 32px rgba(0,0,0,0.18); }
.modal__header { display: flex; align-items: center; justify-content: space-between; padding: 1rem 1.25rem; border-bottom: 1px solid #e0ece2; }
.modal__header h3 { margin: 0; font-size: 1rem; color: var(--tf-color-primary-dark); }
.modal__close { background: transparent; border: none; font-size: 1.1rem; cursor: pointer; color: var(--tf-color-muted); line-height: 1; padding: 0.25rem; }
.modal__close:hover { color: #333; }
.modal__body { flex: 1; overflow-y: auto; padding: 1rem 1.25rem; }
.modal__footer { padding: 0.75rem 1.25rem; border-top: 1px solid #e0ece2; }

/* Permission tree */
.perm-tree { display: flex; flex-direction: column; gap: 0.25rem; }
.perm-node { }
.perm-label { display: flex; align-items: center; gap: 0.5rem; padding: 0.3rem 0; cursor: pointer; font-size: 0.88rem; }
.perm-label--parent { font-weight: 600; color: var(--tf-color-primary-dark); }
.perm-label input[type="checkbox"] { width: 15px; height: 15px; accent-color: var(--tf-color-primary); flex-shrink: 0; }
.perm-children { margin-left: 1.5rem; border-left: 2px solid #d0e2d4; padding-left: 0.75rem; margin-top: 0.1rem; }
</style>

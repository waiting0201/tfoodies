<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { apiFetch } from '../../lib/apiClient'

// ─── 型別定義 ────────────────────────────────────────────────────────────────

interface AdminRow {
  adminId: number
  username: string
  isEnable: boolean
}

interface LimPerm {
  limId: number
  key: string
  label: string
  groupId: number
  groupLabel: string
  canAdd: boolean
  canUpdate: boolean
  canDelete: boolean
}

interface CreateForm {
  username: string
  password: string
}

interface EditForm {
  adminId: number
  username: string
  password: string
  isEnable: boolean
}

// ─── 清單 ───────────────────────────────────────────────────────────────────

const accounts = ref<AdminRow[]>([])
const loading  = ref(false)
const error    = ref('')

async function loadAccounts() {
  loading.value = true
  error.value   = ''
  try {
    const d = await apiFetch<AdminRow[]>('/admin/admin-accounts')
    accounts.value = Array.isArray(d) ? d : []
  } catch (e: any) {
    error.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

// ─── 面板狀態（新增 / 編輯共用同一個 side-panel） ───────────────────────────

type PanelMode = 'create' | 'edit' | null

const panelMode   = ref<PanelMode>(null)
const panelSaving = ref(false)
const panelError  = ref('')

// 新增表單
const createForm = ref<CreateForm>({ username: '', password: '' })

// 編輯表單
const editForm = ref<EditForm>({ adminId: 0, username: '', password: '', isEnable: true })

function openCreatePanel() {
  createForm.value = { username: '', password: '' }
  panelError.value = ''
  panelMode.value  = 'create'
}

function openEditPanel(a: AdminRow) {
  editForm.value  = { adminId: a.adminId, username: a.username, password: '', isEnable: a.isEnable }
  panelError.value = ''
  panelMode.value  = 'edit'
}

function closePanel() {
  panelMode.value = null
}

async function submitCreate() {
  if (panelSaving.value) return
  if (!createForm.value.username.trim()) { panelError.value = '請填寫帳號'; return }
  if (!createForm.value.password.trim()) { panelError.value = '請填寫密碼'; return }

  panelSaving.value = true
  panelError.value  = ''
  try {
    await apiFetch('/admin/admin-accounts', {
      method: 'POST',
      body: JSON.stringify({ username: createForm.value.username, password: createForm.value.password }),
    })
    panelMode.value = null
    await loadAccounts()
  } catch (e: any) {
    panelError.value = e.message ?? '建立失敗'
  } finally {
    panelSaving.value = false
  }
}

async function submitEdit() {
  if (panelSaving.value) return

  panelSaving.value = true
  panelError.value  = ''
  try {
    const body: Record<string, unknown> = { isEnable: editForm.value.isEnable }
    if (editForm.value.password.trim()) body.password = editForm.value.password
    await apiFetch(`/admin/admin-accounts/${editForm.value.adminId}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    })
    panelMode.value = null
    await loadAccounts()
  } catch (e: any) {
    panelError.value = e.message ?? '儲存失敗'
  } finally {
    panelSaving.value = false
  }
}

// ─── 停用確認 Modal ──────────────────────────────────────────────────────────

const disableModal   = ref<AdminRow | null>(null)
const disabling      = ref(false)
const disableError   = ref('')

function openDisableModal(a: AdminRow) {
  disableModal.value = a
  disableError.value = ''
}

async function confirmDisable() {
  if (!disableModal.value || disabling.value) return
  disabling.value    = true
  disableError.value = ''
  try {
    await apiFetch(`/admin/admin-accounts/${disableModal.value.adminId}`, { method: 'DELETE' })
    disableModal.value = null
    await loadAccounts()
  } catch (e: any) {
    disableError.value = e.message ?? '停用失敗'
  } finally {
    disabling.value = false
  }
}

// ─── 權限 Modal ──────────────────────────────────────────────────────────────

interface PermTarget {
  adminId: number
  username: string
}

const permTarget   = ref<PermTarget | null>(null)
const perms        = ref<LimPerm[]>([])
const permLoading  = ref(false)
const permError    = ref('')
const permSaving   = ref(false)
const permSaveErr  = ref('')

async function openPermModal(a: AdminRow) {
  permTarget.value  = { adminId: a.adminId, username: a.username }
  perms.value       = []
  permLoading.value = true
  permError.value   = ''
  permSaveErr.value = ''
  try {
    const data = await apiFetch<LimPerm[]>(`/admin/admin-accounts/${a.adminId}/permissions`)
    perms.value = Array.isArray(data) ? data : []
  } catch (e: any) {
    permError.value = e.message ?? '載入失敗'
  } finally {
    permLoading.value = false
  }
}

async function savePermissions() {
  if (!permTarget.value || permSaving.value) return
  permSaving.value  = true
  permSaveErr.value = ''
  try {
    await apiFetch(`/admin/admin-accounts/${permTarget.value.adminId}/permissions`, {
      method: 'PUT',
      body: JSON.stringify({
        grants: perms.value.map(p => ({
          limId:     p.limId,
          canAdd:    p.canAdd,
          canUpdate: p.canUpdate,
          canDelete: p.canDelete,
        })),
      }),
    })
    permTarget.value = null
  } catch (e: any) {
    permSaveErr.value = e.message ?? '儲存失敗'
  } finally {
    permSaving.value = false
  }
}

// ─── 權限分群（依 groupId 分組，保持原有順序） ────────────────────────────

const permGroups = computed(() => {
  const map = new Map<number, { groupId: number; groupLabel: string; items: LimPerm[] }>()
  for (const p of perms.value) {
    if (!map.has(p.groupId)) map.set(p.groupId, { groupId: p.groupId, groupLabel: p.groupLabel, items: [] })
    map.get(p.groupId)!.items.push(p)
  }
  return [...map.values()]
})

// ─── 初始化 ─────────────────────────────────────────────────────────────────

onMounted(loadAccounts)
</script>

<template>
  <main class="admins">
    <!-- 頁首 -->
    <div class="admins__header">
      <h1 class="admins__title">管理員帳號</h1>
      <button class="btn btn--primary" @click="openCreatePanel">+ 新增帳號</button>
    </div>

    <!-- 載入中 / 錯誤 -->
    <p v-if="loading" class="admins__muted">載入中…</p>
    <p v-else-if="error" class="admins__error">{{ error }}</p>

    <!-- 清單 -->
    <div v-else class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th>帳號</th>
            <th>狀態</th>
            <th class="action-th"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="accounts.length === 0">
            <td colspan="3" class="empty-cell">尚無管理員帳號</td>
          </tr>
          <tr
            v-for="a in accounts"
            :key="a.adminId"
            class="data-table__row"
          >
            <td>{{ a.username }}</td>
            <td>
              <span class="badge" :class="a.isEnable ? 'badge--active' : 'badge--disabled'">
                {{ a.isEnable ? '啟用' : '停用' }}
              </span>
            </td>
            <td class="action-cell">
              <button class="btn btn--ghost btn--sm" @click="openEditPanel(a)">編輯</button>
              <button class="btn btn--ghost btn--sm" @click="openPermModal(a)">權限</button>
              <button class="btn btn--danger-ghost btn--sm" @click="openDisableModal(a)">停用</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- ── 右側滑出面板（新增 / 編輯）── -->
    <div
      v-if="panelMode !== null"
      class="panel-overlay"
      @click.self="closePanel"
    >
      <div class="side-panel">
        <!-- 面板 header -->
        <div class="panel__header">
          <h2 class="panel__title">{{ panelMode === 'create' ? '新增管理員' : `編輯：${editForm.username}` }}</h2>
          <button class="panel__close" @click="closePanel" aria-label="關閉">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <!-- 面板 body -->
        <div class="panel__body">
          <!-- 新增表單 -->
          <template v-if="panelMode === 'create'">
            <div class="form-field">
              <label class="form-field__label">帳號 <span class="required">*</span></label>
              <input
                v-model="createForm.username"
                class="form-field__input"
                type="text"
                autocomplete="off"
                placeholder="請輸入帳號"
              />
            </div>
            <div class="form-field">
              <label class="form-field__label">密碼 <span class="required">*</span></label>
              <input
                v-model="createForm.password"
                class="form-field__input"
                type="password"
                autocomplete="new-password"
                placeholder="請輸入密碼"
              />
            </div>
          </template>

          <!-- 編輯表單 -->
          <template v-else-if="panelMode === 'edit'">
            <div class="form-field">
              <label class="form-field__label">帳號</label>
              <input
                :value="editForm.username"
                class="form-field__input"
                type="text"
                readonly
                disabled
              />
            </div>
            <div class="form-field">
              <label class="form-field__label">新密碼（留空不變）</label>
              <input
                v-model="editForm.password"
                class="form-field__input"
                type="password"
                autocomplete="new-password"
                placeholder="留空則不修改密碼"
              />
            </div>
            <div class="form-field">
              <label class="checkbox-option">
                <input v-model="editForm.isEnable" type="checkbox" />
                啟用此帳號
              </label>
            </div>
          </template>

          <p v-if="panelError" class="form-error">{{ panelError }}</p>
        </div>

        <!-- 面板 footer -->
        <div class="panel__footer">
          <button class="btn btn--ghost" @click="closePanel">取消</button>
          <button
            v-if="panelMode === 'create'"
            class="btn btn--primary"
            :disabled="panelSaving"
            @click="submitCreate"
          >{{ panelSaving ? '建立中…' : '建立' }}</button>
          <button
            v-else-if="panelMode === 'edit'"
            class="btn btn--primary"
            :disabled="panelSaving"
            @click="submitEdit"
          >{{ panelSaving ? '儲存中…' : '儲存' }}</button>
        </div>
      </div>
    </div>

    <!-- ── 停用確認 Modal ── -->
    <div
      v-if="disableModal !== null"
      class="modal-overlay"
      @click.self="disableModal = null"
    >
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">停用管理員</h3>
        </div>
        <div class="modal__body">
          <p class="modal__hint">確定要停用「{{ disableModal?.username }}」的帳號？停用後該帳號將無法登入後台。</p>
          <p v-if="disableError" class="form-error" style="margin-top:0.75rem">{{ disableError }}</p>
        </div>
        <div class="modal__footer">
          <button class="btn btn--ghost" @click="disableModal = null">取消</button>
          <button class="btn btn--danger" :disabled="disabling" @click="confirmDisable">
            {{ disabling ? '停用中…' : '確認停用' }}
          </button>
        </div>
      </div>
    </div>

    <!-- ── 權限 Modal ── -->
    <div
      v-if="permTarget !== null"
      class="modal-overlay"
      @click.self="permTarget = null"
    >
      <div class="modal modal--wide">
        <div class="modal__header">
          <h3 class="modal__title">{{ permTarget?.username }} — 模組權限</h3>
        </div>
        <div class="modal__body">
          <p v-if="permLoading" class="modal__hint">載入中…</p>
          <p v-else-if="permError" class="form-error">{{ permError }}</p>
          <template v-else>
            <p v-if="permGroups.length === 0" class="modal__hint">尚無模組設定</p>
            <template v-else>
              <div v-for="group in permGroups" :key="group.groupId" class="perm-group">
                <div class="perm-group__header">{{ group.groupLabel }}</div>
                <table class="perm-table">
                  <thead>
                    <tr>
                      <th class="perm-table__col-name">功能名稱</th>
                      <th class="perm-table__col-action">新增</th>
                      <th class="perm-table__col-action">修改</th>
                      <th class="perm-table__col-action">刪除</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="perm in group.items" :key="perm.limId" class="perm-table__row">
                      <td class="perm-table__label">{{ perm.label || perm.key }}</td>
                      <td class="perm-table__check">
                        <input v-model="perm.canAdd" type="checkbox" class="perm-checkbox" />
                      </td>
                      <td class="perm-table__check">
                        <input v-model="perm.canUpdate" type="checkbox" class="perm-checkbox" />
                      </td>
                      <td class="perm-table__check">
                        <input v-model="perm.canDelete" type="checkbox" class="perm-checkbox" />
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </template>
            <p v-if="permSaveErr" class="form-error" style="margin-top:0.75rem">{{ permSaveErr }}</p>
          </template>
        </div>
        <div class="modal__footer">
          <button class="btn btn--ghost" @click="permTarget = null">取消</button>
          <button
            class="btn btn--primary"
            :disabled="permSaving || permLoading"
            @click="savePermissions"
          >{{ permSaving ? '儲存中…' : '儲存權限' }}</button>
        </div>
      </div>
    </div>
  </main>
</template>

<style scoped>
/* ── 根容器（不加 padding，由 AdminLayout p-6 提供） ── */
.admins {}

/* ── 標題列 ── */
.admins__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.admins__title  { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.admins__error  { color: #dc3545; }
.admins__muted  { color: var(--tf-color-muted); }

/* ── 表格卡片 ── */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }

.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th {
  background: var(--tf-color-primary);
  color: #fff;
  text-align: left;
  padding: 0.65rem 0.75rem;
  font-size: 0.875rem;
  font-weight: 600;
  white-space: nowrap;
}
.action-th { width: 160px; }
.data-table td {
  padding: 0.65rem 0.9rem;
  border-bottom: 1px solid var(--tf-color-border);
  vertical-align: middle;
  color: #334155;
}
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }
.empty-cell  { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }

/* ── Badge ── */
.badge          { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 500; white-space: nowrap; }
.badge--active  { background: #dcfce7; color: #166534; }
.badge--disabled{ background: #f1f5f9; color: #64748b; }

/* ── 按鈕 ── */
.btn {
  display: inline-flex; align-items: center; justify-content: center;
  padding: 0.45rem 1rem;
  border: 1px solid transparent; border-radius: 4px;
  cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit;
  text-decoration: none; transition: opacity 0.15s, background 0.15s;
  white-space: nowrap;
}
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm           { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary      { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost        { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled)   { background: rgba(38, 183, 188, 0.06); }
.btn--danger       { background: #dc3545; color: #fff; border-color: #dc3545; }
.btn--danger:hover:not(:disabled)  { background: #b02a37; }
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }

/* ── 右側滑出面板 ── */
.panel-overlay {
  position: fixed; inset: 0; z-index: 50;
  background: rgba(15, 23, 42, 0.4); backdrop-filter: blur(1px);
  display: flex; justify-content: flex-end;
  animation: fadeIn 0.15s ease;
}
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }

.side-panel {
  width: 100%; max-width: 440px; height: 100%;
  background: #fff; box-shadow: -8px 0 40px rgba(0, 0, 0, 0.15);
  display: flex; flex-direction: column;
  animation: slideInRight 0.22s cubic-bezier(0.25, 0.46, 0.45, 0.94);
}
@keyframes slideInRight { from { transform: translateX(100%); } to { transform: none; } }

.panel__header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 1.25rem 1.5rem; border-bottom: 1px solid var(--tf-color-border);
}
.panel__title  { font-size: 1.05rem; font-weight: 700; color: #1e293b; margin: 0; }
.panel__close  {
  background: none; border: none; cursor: pointer;
  color: var(--tf-color-muted); padding: 0.25rem; border-radius: 4px; display: flex;
}
.panel__close:hover { color: #475569; background: #f1f5f9; }
.panel__body   { flex: 1; overflow-y: auto; padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
.panel__footer { padding: 1rem 1.5rem; border-top: 1px solid var(--tf-color-border); display: flex; justify-content: flex-end; gap: 0.5rem; }

/* ── 表單欄位 ── */
.form-field        { display: flex; flex-direction: column; gap: 0.35rem; }
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.required          { color: #ef4444; }
.form-field__input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem; color: #1e293b; background: #fff;
  transition: border-color 0.15s; font-family: inherit;
}
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38, 183, 188, 0.15); }
.form-field__input:disabled { background: #f8fafc; color: var(--tf-color-muted); cursor: not-allowed; }
.checkbox-option       { display: flex; align-items: center; gap: 0.5rem; font-size: 0.875rem; color: #475569; cursor: pointer; padding-top: 0.4rem; }
.checkbox-option input { accent-color: var(--tf-color-primary); width: 16px; height: 16px; }
.form-error { color: #dc3545; font-size: 0.85rem; }

/* ── Modal（停用確認，z-index: 60） ── */
.modal-overlay {
  position: fixed; inset: 0; z-index: 60;
  background: rgba(15, 23, 42, 0.45);
  display: flex; align-items: center; justify-content: center; padding: 1rem;
}
.modal         { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0, 0, 0, 0.2); width: 100%; max-width: 380px; }
.modal--wide   { max-width: 640px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title  { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body   { padding: 1.25rem 1.4rem; overflow-y: auto; max-height: 60vh; }
.modal__hint   { font-size: 0.85rem; color: var(--tf-color-muted); margin: 0; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }

/* ── 權限群組 ── */
.perm-group        { margin-bottom: 1.25rem; }
.perm-group:last-child { margin-bottom: 0; }
.perm-group__header {
  font-size: 0.8rem; font-weight: 700; color: var(--tf-color-primary-dark);
  padding: 0.35rem 0.5rem; background: #f0fafa;
  border-left: 3px solid var(--tf-color-primary); margin-bottom: 0.25rem;
  border-radius: 0 3px 3px 0;
}

/* ── 權限表格 ── */
.perm-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.perm-table th {
  background: var(--tf-color-primary);
  color: #fff;
  text-align: left;
  padding: 0.5rem 0.75rem;
  font-size: 0.82rem;
  font-weight: 600;
  white-space: nowrap;
}
.perm-table__col-name   { width: auto; }
.perm-table__col-action { width: 64px; text-align: center; }
.perm-table__row td     { padding: 0.55rem 0.75rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.perm-table__row:last-child td { border-bottom: none; }
.perm-table__row:hover td      { background: #f8faf8; }
.perm-table__label { font-weight: 500; }
.perm-table__check { text-align: center; }
.perm-checkbox { accent-color: var(--tf-color-primary); width: 15px; height: 15px; cursor: pointer; }
</style>

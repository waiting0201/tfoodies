<script setup lang="ts">
import { ref, reactive } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────

interface Logistic {
  logisticid: string
  logisticcode: string
  title: string
  address: string
  phone: string
  isenable: boolean
}

// ─── list state ───────────────────────────────────────────────────────────────

const items = ref<Logistic[]>([])
const loading = ref(false)
const error = ref('')

// ─── panel state ──────────────────────────────────────────────────────────────

type PanelMode = 'create' | 'edit'

const panelOpen = ref(false)
const panelMode = ref<PanelMode>('create')
const panelLoading = ref(false)
const panelError = ref('')
const submitting = ref(false)

const form = reactive({
  logisticCode: '',
  title: '',
  address: '',
  phone: '',
  isEnable: true,
})

const editingId = ref<string | null>(null)

// ─── helpers ──────────────────────────────────────────────────────────────────

function errMsg(e: unknown, fallback: string) {
  const ae = e as ApiError
  if (ae.problem?.status === 501) return '此功能尚未開放（API 尚未實作）'
  return ae.problem?.detail ?? (e as Error).message ?? fallback
}

function resetForm() {
  form.logisticCode = ''
  form.title = ''
  form.address = ''
  form.phone = ''
  form.isEnable = true
}

// ─── data loading ─────────────────────────────────────────────────────────────

async function load() {
  loading.value = true
  error.value = ''
  try {
    items.value = await apiFetch<Logistic[]>('/admin/logistics')
  } catch (e) {
    error.value = errMsg(e, '載入失敗')
  } finally {
    loading.value = false
  }
}

// ─── panel actions ────────────────────────────────────────────────────────────

function openCreate() {
  resetForm()
  editingId.value = null
  panelMode.value = 'create'
  panelError.value = ''
  panelOpen.value = true
}

async function openEdit(item: Logistic) {
  resetForm()
  editingId.value = item.logisticid
  panelMode.value = 'edit'
  panelError.value = ''
  panelOpen.value = true
  panelLoading.value = true
  try {
    const data = await apiFetch<Logistic>(`/admin/logistics/${item.logisticid}`)
    form.logisticCode = data.logisticcode
    form.title = data.title
    form.address = data.address ?? ''
    form.phone = data.phone ?? ''
    form.isEnable = data.isenable
  } catch (e) {
    panelError.value = errMsg(e, '載入物流商資料失敗')
  } finally {
    panelLoading.value = false
  }
}

function closePanel() {
  panelOpen.value = false
}

async function handleSubmit() {
  panelError.value = ''
  if (!form.title.trim()) {
    panelError.value = '請填寫物流商名稱'
    return
  }
  submitting.value = true
  try {
    const body = JSON.stringify({
      logisticCode: form.logisticCode,
      title: form.title,
      address: form.address,
      phone: form.phone,
      isEnable: form.isEnable,
    })
    if (panelMode.value === 'create') {
      await apiFetch('/admin/logistics', { method: 'POST', body })
    } else {
      await apiFetch(`/admin/logistics/${editingId.value}`, { method: 'PUT', body })
    }
    panelOpen.value = false
    await load()
  } catch (e) {
    panelError.value = errMsg(e, '儲存失敗')
  } finally {
    submitting.value = false
  }
}

load()
</script>

<template>
  <main class="logistics">
    <div class="logistics__header">
      <h1 class="logistics__title">物流商管理</h1>
      <button class="btn btn--primary" @click="openCreate">+ 新增物流商</button>
    </div>

    <p v-if="loading" class="logistics__muted">載入中…</p>
    <p v-if="error" class="logistics__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th>物流商名稱</th>
            <th>代碼</th>
            <th>電話</th>
            <th>地址</th>
            <th style="width:6rem">狀態</th>
            <th class="action-th"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in items" :key="item.logisticid" class="data-table__row">
            <td class="font-semibold">{{ item.title }}</td>
            <td class="font-mono">{{ item.logisticcode || '—' }}</td>
            <td>{{ item.phone || '—' }}</td>
            <td class="text-muted">{{ item.address || '—' }}</td>
            <td>
              <span class="badge" :class="item.isenable ? 'badge--active' : 'badge--disabled'">
                {{ item.isenable ? '啟用' : '停用' }}
              </span>
            </td>
            <td class="action-cell">
              <button class="btn btn--sm btn--ghost" @click="openEdit(item)">編輯</button>
            </td>
          </tr>
          <tr v-if="items.length === 0">
            <td colspan="6" class="empty-cell">目前沒有物流商資料</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 右側滑出面板 -->
    <div v-if="panelOpen" class="panel-overlay" @click.self="closePanel">
      <div class="side-panel">
        <div class="panel__header">
          <h2 class="panel__title">{{ panelMode === 'create' ? '新增物流商' : '編輯物流商' }}</h2>
          <button class="panel__close" aria-label="關閉" @click="closePanel">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <div class="panel__body">
          <div v-if="panelLoading" class="logistics__muted">載入中…</div>
          <template v-else>
            <p v-if="panelError" class="form-error">{{ panelError }}</p>

            <div class="form-field">
              <label class="form-field__label">物流商名稱 <span class="required">*</span></label>
              <input v-model="form.title" class="form-field__input" placeholder="請輸入物流商名稱" />
            </div>

            <div class="form-field">
              <label class="form-field__label">代碼</label>
              <input v-model="form.logisticCode" class="form-field__input" placeholder="物流商識別代碼（選填）" />
            </div>

            <div class="form-field">
              <label class="form-field__label">電話</label>
              <input v-model="form.phone" class="form-field__input" placeholder="聯絡電話（選填）" />
            </div>

            <div class="form-field">
              <label class="form-field__label">地址</label>
              <input v-model="form.address" class="form-field__input" placeholder="公司地址（選填）" />
            </div>

            <label class="checkbox-option">
              <input v-model="form.isEnable" type="checkbox" />
              啟用此物流商
            </label>
          </template>
        </div>

        <div class="panel__footer">
          <button class="btn btn--ghost" @click="closePanel">取消</button>
          <button class="btn btn--primary" :disabled="submitting || panelLoading" @click="handleSubmit">
            {{ submitting ? '儲存中…' : '儲存' }}
          </button>
        </div>
      </div>
    </div>
  </main>
</template>

<style scoped>
.logistics {}
.logistics__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.logistics__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.logistics__error { color: #dc3545; }
.logistics__muted { color: var(--tf-color-muted); }

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
.text-muted { color: var(--tf-color-muted); font-size: 0.85rem; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }

.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 500; white-space: nowrap; }
.badge--active { background: #dcfce7; color: #166534; }
.badge--disabled { background: #f1f5f9; color: #64748b; }

/* 右側滑出面板 */
.panel-overlay {
  position: fixed; inset: 0; z-index: 50;
  background: rgba(15, 23, 42, 0.4); backdrop-filter: blur(1px);
  display: flex; justify-content: flex-end;
  animation: fadeIn 0.15s ease;
}
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }

.side-panel {
  width: 100%; max-width: 440px; height: 100%;
  background: #fff; box-shadow: -8px 0 40px rgba(0,0,0,0.15);
  display: flex; flex-direction: column;
  animation: slideInRight 0.22s cubic-bezier(0.25,0.46,0.45,0.94);
}
@keyframes slideInRight { from { transform: translateX(100%); } to { transform: none; } }

.panel__header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 1.25rem 1.5rem; border-bottom: 1px solid var(--tf-color-border);
}
.panel__title { font-size: 1.05rem; font-weight: 700; color: #1e293b; margin: 0; }
.panel__close {
  background: none; border: none; cursor: pointer;
  color: var(--tf-color-muted); padding: 0.25rem; border-radius: 4px; display: flex;
}
.panel__close:hover { color: #475569; background: #f1f5f9; }
.panel__body { flex: 1; overflow-y: auto; padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
.panel__footer { padding: 1rem 1.5rem; border-top: 1px solid var(--tf-color-border); display: flex; justify-content: flex-end; gap: 0.5rem; }

.form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.required { color: #ef4444; }
.form-field__input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem; color: #1e293b; background: #fff;
  transition: border-color 0.15s; font-family: inherit;
}
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38,183,188,0.15); }
.checkbox-option { display: flex; align-items: center; gap: 0.5rem; font-size: 0.875rem; color: #475569; cursor: pointer; padding-top: 0.4rem; }
.checkbox-option input { accent-color: var(--tf-color-primary); width: 16px; height: 16px; }
.form-error { color: #dc3545; font-size: 0.85rem; margin: 0; }

.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; text-decoration: none; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
</style>

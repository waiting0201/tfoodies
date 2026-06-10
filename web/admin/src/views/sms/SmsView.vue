<script setup lang="ts">
import { ref, reactive, computed } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

// ─── Types ───────────────────────────────────────────────────────────────────

interface SmsItem {
  id: string
  title: string
  smbody: string
  dlvtime: string | null
  recipientCount: number
}

interface SmsPage {
  items: SmsItem[]
  total: number
  page: number
  pageSize: number
  totalPages: number
  totalCount: number
}

type PanelMode = 'add' | 'edit' | null

// ─── State ───────────────────────────────────────────────────────────────────

const router = useRouter()

const page = ref(1)
const pageSize = 20

const data = ref<SmsPage | null>(null)
const loading = ref(false)
const error = ref('')

// ─── Panel ───────────────────────────────────────────────────────────────────

const panelMode = ref<PanelMode>(null)
const panelEditId = ref<string | null>(null)
const panelSaving = ref(false)
const panelError = ref('')

const form = reactive({
  title: '',
  smbody: '',
  dlvtime: '',   // datetime-local string
})

const smbodyCount = computed(() => form.smbody.length)

// ─── Delete modal ────────────────────────────────────────────────────────────

const deleteTarget = ref<SmsItem | null>(null)
const deleteError = ref('')
const deleting = ref(false)

// ─── Helpers ─────────────────────────────────────────────────────────────────

function errMsg(e: unknown, fallback: string): string {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

function formatDlvtime(iso: string | null): string {
  if (!iso) return '—'
  const d = new Date(iso)
  const y = d.getFullYear()
  const mo = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  const h = String(d.getHours()).padStart(2, '0')
  const mi = String(d.getMinutes()).padStart(2, '0')
  return `${y}-${mo}-${day} ${h}:${mi}`
}

/** Convert ISO string → datetime-local input value (yyyy-MM-ddTHH:mm) */
function isoToDatetimeLocal(iso: string | null): string {
  if (!iso) return ''
  return iso.slice(0, 16)
}

/** Convert datetime-local value → ISO string (or null if empty) */
function datetimeLocalToIso(val: string): string | null {
  if (!val) return null
  return new Date(val).toISOString()
}

// ─── Load ────────────────────────────────────────────────────────────────────

async function load() {
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize) })
    data.value = await apiFetch<SmsPage>(`/admin/sms?${params}`)
  } catch (e) {
    error.value = errMsg(e, '載入失敗')
  } finally {
    loading.value = false
  }
}

function prevPage() {
  if (page.value > 1) { page.value--; load() }
}

function nextPage() {
  if (data.value && page.value * pageSize < data.value.total) { page.value++; load() }
}

// ─── Panel ───────────────────────────────────────────────────────────────────

function openAdd() {
  form.title = ''
  form.smbody = ''
  form.dlvtime = ''
  panelError.value = ''
  panelMode.value = 'add'
  panelEditId.value = null
}

function openEdit(item: SmsItem) {
  form.title = item.title
  form.smbody = item.smbody
  form.dlvtime = isoToDatetimeLocal(item.dlvtime)
  panelError.value = ''
  panelMode.value = 'edit'
  panelEditId.value = item.id
}

function closePanel() {
  panelMode.value = null
  panelEditId.value = null
}

const panelTitle = computed(() => panelMode.value === 'add' ? '新增簡訊' : '編輯簡訊')

async function savePanel() {
  panelError.value = ''
  if (!form.title.trim()) { panelError.value = '請填寫標題'; return }
  if (!form.smbody.trim()) { panelError.value = '請填寫訊息內容'; return }

  panelSaving.value = true
  try {
    const body = {
      title: form.title,
      smbody: form.smbody,
      dlvtime: datetimeLocalToIso(form.dlvtime),
    }
    if (panelMode.value === 'add') {
      await apiFetch<{ id: string }>('/admin/sms', { method: 'POST', body: JSON.stringify(body) })
    } else {
      await apiFetch(`/admin/sms/${panelEditId.value}`, { method: 'PUT', body: JSON.stringify(body) })
    }
    closePanel()
    page.value = 1
    await load()
  } catch (e) {
    panelError.value = errMsg(e, '儲存失敗')
  } finally {
    panelSaving.value = false
  }
}

// ─── Delete ──────────────────────────────────────────────────────────────────

function askDelete(item: SmsItem) {
  deleteTarget.value = item
  deleteError.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/sms/${deleteTarget.value.id}`, { method: 'DELETE' })
    deleteTarget.value = null
    await load()
  } catch (e) {
    deleteError.value = errMsg(e, '刪除失敗')
  } finally {
    deleting.value = false
  }
}

// ─── Navigation ──────────────────────────────────────────────────────────────

function goToRecipients(id: string) {
  router.push({ name: 'sms-recipients', params: { id } })
}

load()
</script>

<template>
  <main class="sms">
    <!-- Header -->
    <div class="sms__header">
      <h1 class="sms__title">簡訊維護</h1>
      <button class="btn btn--primary" @click="openAdd">+ 新增簡訊</button>
    </div>

    <p v-if="loading" class="sms__muted">載入中…</p>
    <p v-if="error" class="sms__error">{{ error }}</p>

    <template v-if="!loading && data">
      <!-- Table card -->
      <div class="card">
        <table class="data-table">
          <thead>
            <tr>
              <th style="width:11rem">預約時間</th>
              <th>標題</th>
              <th style="width:7rem">收訊人</th>
              <th class="action-th"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in data.items" :key="item.id" class="data-table__row">
              <td class="font-mono">{{ formatDlvtime(item.dlvtime) }}</td>
              <td class="font-semibold">{{ item.title }}</td>
              <td>{{ item.recipientCount }}</td>
              <td class="action-cell">
                <button class="btn btn--sm btn--ghost" @click="goToRecipients(item.id)">收訊人</button>
                <button class="btn btn--sm btn--ghost" @click="openEdit(item)">編輯</button>
                <button class="btn btn--sm btn--danger-ghost" @click="askDelete(item)">刪除</button>
              </td>
            </tr>
            <tr v-if="data.items.length === 0">
              <td colspan="4" class="empty-cell">目前沒有簡訊資料</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div class="sms__pagination">
        <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
        <span class="sms__page-info">第 {{ page }} 頁（共 {{ data.total }} 筆）</span>
        <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= data.total" @click="nextPage">下一頁</button>
      </div>
    </template>

    <!-- ── Add / Edit side panel ─────────────────────────────────────────── -->
    <div v-if="panelMode" class="panel-overlay" @click.self="closePanel">
      <aside class="side-panel">
        <div class="panel__header">
          <h2 class="panel__title">{{ panelTitle }}</h2>
          <button class="panel__close" aria-label="關閉" @click="closePanel">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <div class="panel__body">
          <!-- 標題 -->
          <div class="form-field">
            <label class="form-field__label">標題 <span class="required">*</span></label>
            <input v-model="form.title" class="form-field__input" placeholder="簡訊標題" maxlength="50" />
          </div>

          <!-- 預約時間 -->
          <div class="form-field">
            <label class="form-field__label">預約時間</label>
            <input v-model="form.dlvtime" type="datetime-local" class="form-field__input" />
          </div>

          <!-- 訊息內容 -->
          <div class="form-field">
            <label class="form-field__label">
              訊息 <span class="required">*</span>
              <span class="sms__char-count">{{ smbodyCount }} / 160</span>
            </label>
            <textarea
              v-model="form.smbody"
              class="form-field__input form-field__textarea"
              rows="5"
              maxlength="160"
              placeholder="簡訊內容（最多 160 字）"
            ></textarea>
          </div>

          <p v-if="panelError" class="form-error">{{ panelError }}</p>
        </div>

        <div class="panel__footer">
          <button class="btn btn--ghost" @click="closePanel">取消</button>
          <button class="btn btn--primary" :disabled="panelSaving" @click="savePanel">
            {{ panelSaving ? '儲存中…' : '儲存' }}
          </button>
        </div>
      </aside>
    </div>

    <!-- ── Delete confirm modal ───────────────────────────────────────────── -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget = null">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認刪除簡訊</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除簡訊 <strong>{{ deleteTarget.title }}</strong> 嗎？此操作無法復原。</p>
          <p v-if="deleteError" class="form-error" style="margin-top:0.5rem">{{ deleteError }}</p>
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
.sms {}
.sms__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.sms__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.sms__error { color: #dc3545; margin-bottom: 0.75rem; }
.sms__muted { color: var(--tf-color-muted); }

/* ── Table card ── */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 180px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; display: flex; gap: 0.35rem; justify-content: flex-end; }
.font-mono { font-family: 'IBM Plex Mono', monospace; font-size: 0.82rem; }
.font-semibold { font-weight: 600; }

/* ── Pagination ── */
.sms__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.sms__page-info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* ── Buttons ── */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; text-decoration: none; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }
.btn--accent { background: var(--tf-color-accent); color: #fff; border-color: var(--tf-color-accent); }
.btn--accent:hover:not(:disabled) { opacity: 0.85; }
.btn--danger { background: #dc3545; color: #fff; border-color: #dc3545; }
.btn--danger:hover:not(:disabled) { background: #b02a37; }
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }

/* ── Side panel ── */
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

/* ── Form fields ── */
.form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; display: flex; justify-content: space-between; align-items: center; }
.required { color: #ef4444; }
.sms__char-count { font-size: 0.78rem; color: var(--tf-color-muted); font-weight: 400; }
.form-field__input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  font-size: 0.875rem; color: #1e293b; background: #fff;
  transition: border-color 0.15s; font-family: inherit;
  width: 100%; box-sizing: border-box;
}
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38,183,188,0.15); }
.form-field__textarea { resize: vertical; min-height: 6rem; }
.form-error { color: #dc3545; font-size: 0.85rem; }

/* ── Delete modal ── */
.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15,23,42,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 380px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1.25rem 1.4rem; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }
</style>

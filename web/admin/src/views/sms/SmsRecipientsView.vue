<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

// ─── Types ───────────────────────────────────────────────────────────────────

interface SmsInfo {
  id: string
  title: string
  smbody: string
  dlvtime: string | null
}

interface Recipient {
  id: string
  section: string
  ismember: number   // 1=會員, 2=客戶
  city: string | null
  name: string
  mobile: string
  statusCode: string | null
  issend: number     // 0=未發送, 1=已發送
}

interface AvailableMember {
  id: string
  name: string
  mobile: string
  ismember: number
  city: string | null
}

// ─── State ───────────────────────────────────────────────────────────────────

const route = useRoute()
const router = useRouter()
const smsId = route.params.id as string

const sms = ref<SmsInfo | null>(null)
const recipients = ref<Recipient[]>([])
const loading = ref(false)
const error = ref('')

const filterIssend = ref<string>('')  // '', '0', '1'

// ─── Delete recipient ────────────────────────────────────────────────────────

const deleteTarget = ref<Recipient | null>(null)
const deleteError = ref('')
const deleting = ref(false)

// ─── Add members modal ───────────────────────────────────────────────────────

const addModalOpen = ref(false)
const availableMembers = ref<AvailableMember[]>([])
const availableLoading = ref(false)
const availableError = ref('')
const selectedMemberIds = ref<Set<string>>(new Set())
const addingMembers = ref(false)
const addMembersError = ref('')
const addMembersResult = ref('')

// Select all checkbox state
const allSelected = computed(() => {
  if (availableMembers.value.length === 0) return false
  return availableMembers.value.every(m => selectedMemberIds.value.has(m.id))
})

function toggleAll() {
  if (allSelected.value) {
    selectedMemberIds.value = new Set()
  } else {
    selectedMemberIds.value = new Set(availableMembers.value.map(m => m.id))
  }
}

function toggleMember(id: string) {
  const next = new Set(selectedMemberIds.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  selectedMemberIds.value = next
}

// ─── Send modal ──────────────────────────────────────────────────────────────

const sendModalOpen = ref(false)
const sending = ref(false)
const sendError = ref('')
const sendResult = ref('')

// ─── Helpers ─────────────────────────────────────────────────────────────────

function errMsg(e: unknown, fallback: string): string {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

function ismemberLabel(v: number): string {
  return v === 1 ? '會員' : '客戶'
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

// ─── Load ────────────────────────────────────────────────────────────────────

async function load() {
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams()
    if (filterIssend.value !== '') params.set('issend', filterIssend.value)
    const res = await apiFetch<{ sms: SmsInfo; recipients: Recipient[] }>(
      `/admin/sms/${smsId}/recipients${params.toString() ? '?' + params.toString() : ''}`
    )
    sms.value = res.sms
    recipients.value = res.recipients
  } catch (e) {
    error.value = errMsg(e, '載入失敗')
  } finally {
    loading.value = false
  }
}

function onFilterChange() {
  load()
}

// ─── Delete recipient ────────────────────────────────────────────────────────

function askDelete(r: Recipient) {
  deleteTarget.value = r
  deleteError.value = ''
}

async function confirmDeleteRecipient() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/sms/recipients/${deleteTarget.value.id}`, { method: 'DELETE' })
    deleteTarget.value = null
    await load()
  } catch (e) {
    deleteError.value = errMsg(e, '刪除失敗')
  } finally {
    deleting.value = false
  }
}

// ─── Add members ─────────────────────────────────────────────────────────────

async function openAddModal() {
  addModalOpen.value = true
  selectedMemberIds.value = new Set()
  addMembersError.value = ''
  addMembersResult.value = ''
  availableError.value = ''
  availableLoading.value = true
  try {
    const res = await apiFetch<{ members: AvailableMember[] }>(`/admin/sms/${smsId}/available-members`)
    availableMembers.value = res.members
  } catch (e) {
    availableError.value = errMsg(e, '載入失敗')
  } finally {
    availableLoading.value = false
  }
}

function closeAddModal() {
  addModalOpen.value = false
}

async function confirmAddMembers() {
  if (selectedMemberIds.value.size === 0) { addMembersError.value = '請至少選擇一位收訊人'; return }
  addingMembers.value = true
  addMembersError.value = ''
  addMembersResult.value = ''
  try {
    const res = await apiFetch<{ message: string; added: number }>(
      `/admin/sms/${smsId}/recipients`,
      { method: 'POST', body: JSON.stringify({ memberIds: Array.from(selectedMemberIds.value) }) }
    )
    addMembersResult.value = res.message ?? `已新增 ${res.added} 位收訊人`
    selectedMemberIds.value = new Set()
    await load()
    // Close after short delay so user sees success
    setTimeout(() => { closeAddModal() }, 800)
  } catch (e) {
    addMembersError.value = errMsg(e, '新增失敗')
  } finally {
    addingMembers.value = false
  }
}

// ─── Send ────────────────────────────────────────────────────────────────────

function openSendModal() {
  sendModalOpen.value = true
  sendError.value = ''
  sendResult.value = ''
}

function closeSendModal() {
  sendModalOpen.value = false
}

async function confirmSend() {
  sending.value = true
  sendError.value = ''
  sendResult.value = ''
  try {
    const res = await apiFetch<{ message: string; sent: number; failed: number }>(
      `/admin/sms/${smsId}/send`,
      { method: 'POST' }
    )
    sendResult.value = res.message ?? `發送完成：成功 ${res.sent} 筆，失敗 ${res.failed} 筆`
    await load()
  } catch (e) {
    sendError.value = errMsg(e, '發送失敗')
  } finally {
    sending.value = false
  }
}

onMounted(load)
</script>

<template>
  <main class="sms-recipients">

    <!-- Header -->
    <div class="sms-recipients__header">
      <div class="sms-recipients__header-left">
        <button class="btn btn--ghost" @click="router.push('/admin/sms')">← 返回</button>
        <h1 class="sms-recipients__title">收訊人管理</h1>
      </div>
      <div class="sms-recipients__header-actions">
        <button class="btn btn--primary" @click="openAddModal">+ 新增收訊人</button>
        <button class="btn btn--accent" @click="openSendModal">開始發送</button>
      </div>
    </div>

    <!-- SMS info card -->
    <div v-if="sms" class="sms-recipients__info-card">
      <div class="sms-recipients__info-row">
        <span class="sms-recipients__info-label">標題</span>
        <span class="sms-recipients__info-value font-semibold">{{ sms.title }}</span>
      </div>
      <div class="sms-recipients__info-row">
        <span class="sms-recipients__info-label">預約時間</span>
        <span class="sms-recipients__info-value font-mono">{{ formatDlvtime(sms.dlvtime) }}</span>
      </div>
      <div class="sms-recipients__info-row">
        <span class="sms-recipients__info-label">訊息內容</span>
        <span class="sms-recipients__info-value">{{ sms.smbody }}</span>
      </div>
    </div>

    <!-- Filters -->
    <div class="sms-recipients__filters">
      <select v-model="filterIssend" class="filter-select" @change="onFilterChange">
        <option value="">全部</option>
        <option value="0">未發送</option>
        <option value="1">已發送</option>
      </select>
    </div>

    <p v-if="loading" class="sms-recipients__muted">載入中…</p>
    <p v-if="error" class="sms-recipients__error">{{ error }}</p>

    <!-- Table card -->
    <div v-if="!loading" class="card">
      <div class="sms-recipients__table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>Section</th>
              <th style="width:5rem">型態</th>
              <th style="width:6rem">縣市</th>
              <th>姓名</th>
              <th style="width:8rem">手機</th>
              <th style="width:6rem">狀態碼</th>
              <th style="width:5rem">狀態</th>
              <th class="action-th"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in recipients" :key="r.id" class="data-table__row">
              <td>{{ r.section || '—' }}</td>
              <td>
                <span class="badge" :class="r.ismember === 1 ? 'badge--active' : 'badge--client'">
                  {{ ismemberLabel(r.ismember) }}
                </span>
              </td>
              <td>{{ r.city || '' }}</td>
              <td class="font-semibold">{{ r.name }}</td>
              <td class="font-mono">{{ r.mobile }}</td>
              <td>{{ r.statusCode || '—' }}</td>
              <td>
                <span class="badge" :class="r.issend === 1 ? 'badge--active' : 'badge--unpaid'">
                  {{ r.issend === 1 ? '已發送' : '未發送' }}
                </span>
              </td>
              <td class="action-cell">
                <button class="btn btn--sm btn--danger-ghost" @click="askDelete(r)">刪除</button>
              </td>
            </tr>
            <tr v-if="recipients.length === 0">
              <td colspan="8" class="empty-cell">目前沒有收訊人</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- ── Delete recipient modal ─────────────────────────────────────────── -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget = null">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認刪除收訊人</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除收訊人 <strong>{{ deleteTarget.name }}</strong>（{{ deleteTarget.mobile }}）嗎？</p>
          <p v-if="deleteError" class="form-error" style="margin-top:0.5rem">{{ deleteError }}</p>
        </div>
        <div class="modal__footer">
          <button class="btn btn--ghost" @click="deleteTarget = null">取消</button>
          <button class="btn btn--danger" :disabled="deleting" @click="confirmDeleteRecipient">
            {{ deleting ? '刪除中…' : '確認刪除' }}
          </button>
        </div>
      </div>
    </div>

    <!-- ── Add members modal ──────────────────────────────────────────────── -->
    <div v-if="addModalOpen" class="modal-overlay" @click.self="closeAddModal">
      <div class="modal modal--wide">
        <div class="modal__header">
          <h3 class="modal__title">新增收訊人</h3>
        </div>
        <div class="modal__body modal__body--scroll">
          <p v-if="availableLoading" class="sms-recipients__muted">載入中…</p>
          <p v-else-if="availableError" class="form-error">{{ availableError }}</p>
          <template v-else>
            <table class="data-table">
              <thead>
                <tr>
                  <th style="width:2.5rem">
                    <input type="checkbox" :checked="allSelected" @change="toggleAll" style="accent-color:var(--tf-color-primary)" />
                  </th>
                  <th style="width:6rem">縣市</th>
                  <th>姓名</th>
                  <th style="width:8rem">手機</th>
                  <th style="width:5rem">型態</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="m in availableMembers"
                  :key="m.id"
                  class="data-table__row"
                  style="cursor:pointer"
                  @click="toggleMember(m.id)"
                >
                  <td @click.stop>
                    <input
                      type="checkbox"
                      :checked="selectedMemberIds.has(m.id)"
                      @change="toggleMember(m.id)"
                      style="accent-color:var(--tf-color-primary)"
                    />
                  </td>
                  <td>{{ m.city || '' }}</td>
                  <td class="font-semibold">{{ m.name }}</td>
                  <td class="font-mono">{{ m.mobile }}</td>
                  <td>
                    <span class="badge" :class="m.ismember === 1 ? 'badge--active' : 'badge--client'">
                      {{ ismemberLabel(m.ismember) }}
                    </span>
                  </td>
                </tr>
                <tr v-if="availableMembers.length === 0">
                  <td colspan="5" class="empty-cell">沒有可新增的收訊人</td>
                </tr>
              </tbody>
            </table>
          </template>
          <p v-if="addMembersError" class="form-error" style="margin-top:0.75rem">{{ addMembersError }}</p>
          <p v-if="addMembersResult" class="form-success" style="margin-top:0.75rem">{{ addMembersResult }}</p>
        </div>
        <div class="modal__footer">
          <span class="sms-recipients__selected-hint">已選 {{ selectedMemberIds.size }} 位</span>
          <button class="btn btn--ghost" @click="closeAddModal">取消</button>
          <button class="btn btn--primary" :disabled="addingMembers || selectedMemberIds.size === 0" @click="confirmAddMembers">
            {{ addingMembers ? '加入中…' : '確定加入' }}
          </button>
        </div>
      </div>
    </div>

    <!-- ── Send confirm modal ─────────────────────────────────────────────── -->
    <div v-if="sendModalOpen" class="modal-overlay" @click.self="closeSendModal">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認發送簡訊</h3>
        </div>
        <div class="modal__body">
          <template v-if="!sendResult">
            <p>確定要發送此簡訊給所有收訊人嗎？發送後無法取消。</p>
            <p v-if="sendError" class="form-error" style="margin-top:0.5rem">{{ sendError }}</p>
          </template>
          <p v-else class="form-success">{{ sendResult }}</p>
        </div>
        <div class="modal__footer">
          <button class="btn btn--ghost" @click="closeSendModal">{{ sendResult ? '關閉' : '取消' }}</button>
          <button v-if="!sendResult" class="btn btn--accent" :disabled="sending" @click="confirmSend">
            {{ sending ? '發送中…' : '確認發送' }}
          </button>
        </div>
      </div>
    </div>

  </main>
</template>

<style scoped>
.sms-recipients {}
.sms-recipients__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; flex-wrap: wrap; gap: 0.75rem; }
.sms-recipients__header-left { display: flex; align-items: center; gap: 0.75rem; }
.sms-recipients__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.sms-recipients__header-actions { display: flex; gap: 0.5rem; }

/* ── Info card ── */
.sms-recipients__info-card {
  background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border);
  padding: 1rem 1.25rem; margin-bottom: 1rem;
  display: flex; flex-direction: column; gap: 0.5rem;
}
.sms-recipients__info-row { display: flex; gap: 1rem; align-items: flex-start; }
.sms-recipients__info-label { font-size: 0.78rem; font-weight: 600; color: var(--tf-color-muted); min-width: 5.5rem; padding-top: 0.1rem; }
.sms-recipients__info-value { font-size: 0.875rem; color: #334155; }

/* ── Filters ── */
.sms-recipients__filters { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
.filter-select {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  background: #fff; font-size: 0.875rem; cursor: pointer; font-family: inherit;
  transition: border-color 0.15s;
}
.filter-select:focus { outline: none; border-color: var(--tf-color-primary); }

/* ── Table ── */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }
.sms-recipients__table-wrap { overflow-x: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 640px; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 90px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; display: flex; gap: 0.35rem; justify-content: flex-end; }
.font-mono { font-family: 'IBM Plex Mono', monospace; font-size: 0.82rem; }
.font-semibold { font-weight: 600; }

/* ── Badges ── */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 500; white-space: nowrap; }
.badge--active   { background: #dcfce7; color: #166534; }
.badge--disabled { background: #f1f5f9; color: #64748b; }
.badge--client   { background: #dbeafe; color: #1e40af; }
.badge--unpaid   { background: #fff3cd; color: #856404; }

/* ── Buttons ── */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; text-decoration: none; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
.btn--accent { background: var(--tf-color-accent); color: #fff; border-color: var(--tf-color-accent); }
.btn--accent:hover:not(:disabled) { opacity: 0.85; }
.btn--danger { background: #dc3545; color: #fff; border-color: #dc3545; }
.btn--danger:hover:not(:disabled) { background: #b02a37; }
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }

/* ── Modals ── */
.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15,23,42,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 380px; }
.modal--wide { max-width: 640px; max-height: 80vh; display: flex; flex-direction: column; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1.25rem 1.4rem; }
.modal__body--scroll { overflow-y: auto; flex: 1; padding: 1rem 1.4rem; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); align-items: center; }

/* ── Misc ── */
.form-error { color: #dc3545; font-size: 0.85rem; }
.form-success { color: #166534; font-size: 0.875rem; background: #dcfce7; border-radius: 4px; padding: 0.5rem 0.75rem; }
.sms-recipients__muted { color: var(--tf-color-muted); }
.sms-recipients__error { color: #dc3545; margin-bottom: 0.75rem; }
.sms-recipients__selected-hint { font-size: 0.82rem; color: var(--tf-color-muted); margin-right: auto; }
</style>

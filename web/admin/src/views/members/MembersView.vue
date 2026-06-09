<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

interface Member {
  id: string
  name: string
  mobile: string
  email: string
  level: string
  createdAt: string
}

interface MemberDetail extends Member {
  address?: string
  birthday?: string | null
  totalOrders?: number
  totalSpend?: number
  note?: string | null
  isDisabled?: boolean
  gender?: number | null
}

interface MembersPage {
  items: Member[]
  total: number
  page: number
  pageSize: number
}

const filters = reactive({ keyword: '' })
const page = ref(1)
const pageSize = 20

const data = ref<MembersPage | null>(null)
const loading = ref(false)
const error = ref('')

const expandedId = ref<string | null>(null)
const detailMap = reactive<Record<string, MemberDetail>>({})
const detailLoading = ref<string | null>(null)
const detailError = ref<string | null>(null)

const disablingId = ref<string | null>(null)
const disableError = ref('')

// Edit panel state
const editTargetId = ref<string | null>(null)
const editForm = reactive({
  name: '',
  email: '',
  gender: null as number | null,
  birthday: '',
  address: '',
  note: '',
  password: '',
})
const editSaving = ref(false)
const editError = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({
      keyword: filters.keyword,
      page: String(page.value),
      pageSize: String(pageSize),
    })
    data.value = await apiFetch<MembersPage>(`/admin/members?${params}`)
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

function search() {
  page.value = 1
  load()
}

function prevPage() {
  if (page.value > 1) { page.value--; load() }
}

function nextPage() {
  if (data.value && page.value * pageSize < data.value.total) { page.value++; load() }
}

async function toggleDetail(id: string) {
  if (expandedId.value === id) {
    expandedId.value = null
    return
  }
  expandedId.value = id
  if (detailMap[id]) return
  detailLoading.value = id
  detailError.value = null
  try {
    const detail = await apiFetch<MemberDetail>(`/admin/members/${id}`)
    detailMap[id] = detail
  } catch (e) {
    detailError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入失敗'
  } finally {
    detailLoading.value = null
  }
}

async function handleDisable(id: string, name: string) {
  if (!confirm(`確認停用會員「${name}」？此操作無法復原。`)) return
  disablingId.value = id
  disableError.value = ''
  try {
    await apiFetch(`/admin/members/${id}`, { method: 'DELETE' })
    if (data.value) {
      data.value.items = data.value.items.filter(m => m.id !== id)
      data.value.total = Math.max(0, data.value.total - 1)
    }
    if (expandedId.value === id) expandedId.value = null
    delete detailMap[id]
  } catch (e) {
    disableError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
  } finally {
    disablingId.value = null
  }
}

function openEdit(id: string) {
  const d = detailMap[id]
  if (!d) return
  editTargetId.value = id
  editForm.name = d.name
  editForm.email = d.email ?? ''
  editForm.gender = d.gender ?? null
  editForm.birthday = d.birthday ? d.birthday.slice(0, 10) : ''
  editForm.address = d.address ?? ''
  editForm.note = d.note ?? ''
  editForm.password = ''
  editError.value = ''
}

function closeEdit() {
  editTargetId.value = null
}

async function saveEdit() {
  if (!editTargetId.value) return
  editSaving.value = true
  editError.value = ''
  try {
    const updated = await apiFetch<MemberDetail>(`/admin/members/${editTargetId.value}`, {
      method: 'PUT',
      body: JSON.stringify({
        name: editForm.name,
        email: editForm.email || null,
        gender: editForm.gender,
        birthday: editForm.birthday ? new Date(editForm.birthday).toISOString() : null,
        isAgent: false,
        agentDiscount: 0,
        zipcodeId: null,
        address: editForm.address || null,
        memo: editForm.note || null,
        password: editForm.password || null,
      }),
    })
    // Update cached detail
    detailMap[editTargetId.value] = { ...detailMap[editTargetId.value], ...updated }
    // Update list row name
    if (data.value) {
      const row = data.value.items.find(m => m.id === editTargetId.value)
      if (row) row.name = updated.name
    }
    closeEdit()
  } catch (e) {
    editError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '儲存失敗'
  } finally {
    editSaving.value = false
  }
}

function formatDate(s?: string) {
  if (!s) return '—'
  return new Date(s).toLocaleDateString('zh-TW')
}

onMounted(load)
</script>

<template>
  <main class="members">
    <h1 class="members__title">會員管理</h1>

    <!-- Search bar -->
    <div class="members__filters">
      <input
        v-model="filters.keyword"
        class="members__input"
        placeholder="搜尋姓名 / 手機"
        @keyup.enter="search"
      />
      <button class="members__btn members__btn--secondary" @click="search">搜尋</button>
    </div>

    <p v-if="error" class="members__error">{{ error }}</p>
    <p v-if="disableError" class="members__error">{{ disableError }}</p>
    <p v-if="loading" class="members__muted">載入中…</p>

    <template v-else-if="data">
      <div class="members__card">
        <div class="members__table-wrap">
          <table class="members__table">
            <thead>
              <tr>
                <th>姓名</th>
                <th>手機</th>
                <th>Email</th>
                <th>會員等級</th>
                <th>建立日期</th>
                <th>操作</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="m in data.items" :key="m.id">
                <!-- Member row -->
                <tr
                  class="members__row"
                  :class="{ 'members__row--expanded': expandedId === m.id }"
                  @click="toggleDetail(m.id)"
                >
                  <td class="members__name">{{ m.name }}</td>
                  <td class="members__mono">{{ m.mobile }}</td>
                  <td>{{ m.email }}</td>
                  <td>
                    <span class="members__badge">{{ m.level || '一般' }}</span>
                  </td>
                  <td class="members__date">{{ formatDate(m.createdAt) }}</td>
                  <td class="members__actions" @click.stop>
                    <button
                      class="members__btn members__btn--sm members__btn--ghost"
                      @click="toggleDetail(m.id)"
                    >{{ expandedId === m.id ? '收起' : '查看' }}</button>
                    <button
                      class="members__btn members__btn--sm members__btn--danger-ghost"
                      :disabled="disablingId === m.id"
                      @click="handleDisable(m.id, m.name)"
                    >停用</button>
                  </td>
                </tr>

                <!-- Expanded detail panel -->
                <tr v-if="expandedId === m.id" class="members__detail-row">
                  <td colspan="6" class="members__detail-cell">
                    <div class="members__detail-panel">
                      <p v-if="detailLoading === m.id" class="members__muted">載入中…</p>
                      <p v-else-if="detailError && expandedId === m.id" class="members__error">{{ detailError }}</p>

                      <template v-else-if="detailMap[m.id]">
                        <div class="members__detail-grid">
                          <div class="members__detail-field">
                            <span class="members__detail-label">會員 ID</span>
                            <span class="members__detail-value members__mono">{{ detailMap[m.id].id }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">姓名</span>
                            <span class="members__detail-value">{{ detailMap[m.id].name }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">手機</span>
                            <span class="members__detail-value members__mono">{{ detailMap[m.id].mobile }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">Email</span>
                            <span class="members__detail-value">{{ detailMap[m.id].email }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">會員等級</span>
                            <span class="members__detail-value">{{ detailMap[m.id].level || '一般' }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">生日</span>
                            <span class="members__detail-value">{{ formatDate(detailMap[m.id].birthday ?? undefined) }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">建立日期</span>
                            <span class="members__detail-value">{{ formatDate(detailMap[m.id].createdAt) }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">訂單數</span>
                            <span class="members__detail-value">{{ detailMap[m.id].totalOrders ?? '—' }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">累計消費</span>
                            <span class="members__detail-value">
                              {{ detailMap[m.id].totalSpend != null ? `NT$ ${detailMap[m.id].totalSpend!.toLocaleString()}` : '—' }}
                            </span>
                          </div>
                          <div class="members__detail-field members__detail-field--full">
                            <span class="members__detail-label">地址</span>
                            <span class="members__detail-value">{{ detailMap[m.id].address || '—' }}</span>
                          </div>
                          <div class="members__detail-field members__detail-field--full">
                            <span class="members__detail-label">備註</span>
                            <span class="members__detail-value">{{ detailMap[m.id].note || '—' }}</span>
                          </div>
                        </div>
                        <div class="members__detail-actions">
                          <button class="members__btn members__btn--sm members__btn--primary" @click.stop="openEdit(m.id)">
                            編輯會員
                          </button>
                        </div>
                      </template>
                    </div>
                  </td>
                </tr>
              </template>

              <tr v-if="data.items.length === 0">
                <td colspan="6" class="members__empty">無會員資料</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Pagination -->
      <div class="members__pagination">
        <button class="members__btn members__btn--sm members__btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
        <span class="members__page-info">第 {{ page }} 頁（共 {{ data.total }} 筆）</span>
        <button class="members__btn members__btn--sm members__btn--ghost" :disabled="page * pageSize >= data.total" @click="nextPage">下一頁</button>
      </div>
    </template>

    <!-- Edit side panel -->
    <div v-if="editTargetId" class="members__panel-overlay" @click.self="closeEdit">
      <aside class="members__side-panel">
        <div class="members__panel-header">
          <h2 class="members__panel-title">編輯會員</h2>
          <button class="members__panel-close" aria-label="關閉" @click="closeEdit">
            <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div class="members__panel-body">
          <div class="members__form-field">
            <label class="members__form-label">姓名 <span class="members__required">*</span></label>
            <input v-model="editForm.name" class="members__form-input" placeholder="姓名" />
          </div>
          <div class="members__form-field">
            <label class="members__form-label">Email</label>
            <input v-model="editForm.email" type="email" class="members__form-input" placeholder="email@example.com" />
          </div>
          <div class="members__form-row">
            <div class="members__form-field">
              <label class="members__form-label">性別</label>
              <select v-model="editForm.gender" class="members__form-input">
                <option :value="null">不指定</option>
                <option :value="1">男</option>
                <option :value="2">女</option>
              </select>
            </div>
            <div class="members__form-field">
              <label class="members__form-label">生日</label>
              <input v-model="editForm.birthday" type="date" class="members__form-input" />
            </div>
          </div>
          <div class="members__form-field">
            <label class="members__form-label">地址</label>
            <input v-model="editForm.address" class="members__form-input" placeholder="完整地址" />
          </div>
          <div class="members__form-field">
            <label class="members__form-label">備註</label>
            <textarea v-model="editForm.note" class="members__form-input members__form-textarea" rows="3" placeholder="後台備註"></textarea>
          </div>
          <div class="members__form-field">
            <label class="members__form-label">新密碼（留空則不變更）</label>
            <input v-model="editForm.password" type="password" class="members__form-input" placeholder="••••••••" autocomplete="new-password" />
          </div>
          <p v-if="editError" class="members__form-error">{{ editError }}</p>
        </div>
        <div class="members__panel-footer">
          <button class="members__btn members__btn--ghost" @click="closeEdit">取消</button>
          <button class="members__btn members__btn--primary" :disabled="editSaving" @click="saveEdit">
            {{ editSaving ? '儲存中…' : '儲存' }}
          </button>
        </div>
      </aside>
    </div>
  </main>
</template>

<style scoped>
.members {
}

.members__title {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  margin-bottom: 1.25rem;
}

.members__filters {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
  align-items: center;
}

.members__input {
  flex: 1 1 240px;
  max-width: 360px;
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem;
}

.members__card {
  background: #fff;
  border-radius: 10px;
  border: 1px solid var(--tf-color-border);
  overflow: hidden;
  margin-bottom: 1rem;
}

.members__table-wrap {
  overflow-x: auto;
}

.members__table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.members__table th {
  background: var(--tf-color-primary);
  color: #fff;
  padding: 0.65rem 0.75rem;
  text-align: left;
  white-space: nowrap;
}

.members__table td {
  padding: 0.6rem 0.75rem;
  border-bottom: 1px solid var(--tf-color-border);
  vertical-align: middle;
}

.members__row {
  cursor: pointer;
  transition: background 0.1s;
}

.members__row:hover td {
  background: #f8faf8;
}

.members__row--expanded td {
  background: #f0f7f1;
  border-bottom-color: transparent;
}

.members__name {
  font-weight: 500;
  color: var(--tf-color-primary-dark);
}

.members__mono {
  font-family: monospace;
  font-size: 0.82rem;
}

.members__date {
  white-space: nowrap;
  color: var(--tf-color-muted);
}

.members__actions {
  display: flex;
  gap: 0.35rem;
}

.members__badge {
  display: inline-block;
  padding: 0.2rem 0.5rem;
  border-radius: 3px;
  font-size: 0.78rem;
  background: #e8f0e9;
  color: var(--tf-color-primary-dark);
}

.members__empty {
  text-align: center;
  color: var(--tf-color-muted);
  padding: 2rem 0;
}

/* Detail panel */
.members__detail-row td {
  padding: 0;
  border-bottom: 1px solid var(--tf-color-border);
}

.members__detail-cell {
  padding: 0 !important;
}

.members__detail-panel {
  background: #f7fbf7;
  border-top: 2px solid var(--tf-color-primary);
  padding: 1.25rem 1.5rem;
}

.members__detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 0.75rem 1.5rem;
}

.members__detail-field {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.members__detail-field--full {
  grid-column: 1 / -1;
}

.members__detail-label {
  font-size: 0.72rem;
  color: var(--tf-color-muted);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.members__detail-value {
  font-size: 0.875rem;
}

.members__detail-actions {
  margin-top: 1rem;
  display: flex;
  justify-content: flex-end;
}

/* Pagination */
.members__pagination {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-top: 1rem;
  justify-content: flex-end;
}

.members__page-info {
  font-size: 0.875rem;
  color: var(--tf-color-muted);
}

/* Buttons */
.members__btn {
  padding: 0.45rem 1rem;
  border: 1px solid transparent;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.875rem;
  font-weight: 500;
  transition: all 0.15s;
  white-space: nowrap;
}
.members__btn:disabled { opacity: 0.5; cursor: not-allowed; }

.members__btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }

.members__btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.members__btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }

.members__btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.members__btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }

.members__btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.members__btn--secondary:hover:not(:disabled) { background: #dee2e6; }

.members__btn--danger { background: #dc3545; color: #fff; border-color: #dc3545; }
.members__btn--danger:hover:not(:disabled) { background: #b02a37; }

.members__btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.members__btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }

.members__error { color: #dc3545; margin-bottom: 0.75rem; }
.members__muted { color: var(--tf-color-muted); }

/* ─── Edit side panel ─────────────────────────────────────────────────── */
.members__panel-overlay {
  position: fixed; inset: 0; z-index: 50;
  background: rgba(15, 23, 42, 0.4);
  backdrop-filter: blur(1px);
  display: flex; justify-content: flex-end;
  animation: fadeIn 0.15s ease;
}
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }

.members__side-panel {
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

.members__panel-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 1.25rem 1.5rem; border-bottom: 1px solid var(--tf-color-border);
}
.members__panel-title { font-size: 1.05rem; font-weight: 700; color: #1e293b; margin: 0; }
.members__panel-close {
  background: none; border: none; cursor: pointer; color: var(--tf-color-muted);
  padding: 0.25rem; border-radius: 4px; display: flex;
}
.members__panel-close:hover { color: #475569; background: #f1f5f9; }

.members__panel-body {
  flex: 1; overflow-y: auto; padding: 1.5rem;
  display: flex; flex-direction: column; gap: 1rem;
}

.members__panel-footer {
  padding: 1rem 1.5rem; border-top: 1px solid var(--tf-color-border);
  display: flex; justify-content: flex-end; gap: 0.5rem;
}

/* Form elements inside panel */
.members__form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.members__form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
.members__form-label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.members__required { color: #ef4444; }
.members__form-input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.875rem;
  color: #1e293b;
  background: #fff;
  transition: border-color 0.15s, box-shadow 0.15s;
  font-family: inherit;
  width: 100%;
  box-sizing: border-box;
}
.members__form-input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38, 183, 188, 0.15); }
.members__form-textarea { resize: vertical; min-height: 4rem; }
.members__form-error { color: #dc3545; font-size: 0.85rem; }
</style>

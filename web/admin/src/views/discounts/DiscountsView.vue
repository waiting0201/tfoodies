<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { apiFetch } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────
interface Discount {
  discountId: string
  discountcode: string
  istype: 1 | 2
  startdate: string
  expiredate: string
  isonetime: boolean
  v: number
  memo: string
  isdisable: boolean
}

interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

interface DiscountForm {
  discountcode: string
  istype: 1 | 2
  v: string
  startdate: string
  expiredate: string
  isonetime: boolean
  memo: string
  isdisable: boolean
}

// ─── helpers ──────────────────────────────────────────────────────────────────
function fmtDate(d: string) {
  return d ? d.slice(0, 10) : '—'
}

function fmtValue(item: Discount): string {
  if (item.istype === 1) return `NT$ ${item.v.toLocaleString()}`
  return `${item.v}%`
}

function emptyForm(): DiscountForm {
  return {
    discountcode: '',
    istype: 1,
    v: '',
    startdate: '',
    expiredate: '',
    isonetime: false,
    memo: '',
    isdisable: false,
  }
}

// ─── filter state ─────────────────────────────────────────────────────────────
type FilterDisable = '' | 'false' | 'true'
const filterDisable = ref<FilterDisable>('')

// ─── list state ───────────────────────────────────────────────────────────────
const loading = ref(false)
const error = ref('')
const items = ref<Discount[]>([])
const page = ref(1)
const total = ref(0)
const PAGE_SIZE = 20
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / PAGE_SIZE)))

async function loadList() {
  loading.value = true
  error.value = ''
  try {
    const qs = new URLSearchParams({
      page: String(page.value),
      pageSize: String(PAGE_SIZE),
    })
    if (filterDisable.value !== '') qs.set('isdisable', filterDisable.value)
    const data = await apiFetch<PagedResult<Discount>>(`/admin/discounts?${qs}`)
    items.value = data.items ?? []
    total.value = data.total ?? items.value.length
  } catch (e: any) {
    error.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

watch(page, loadList)
watch(filterDisable, () => {
  page.value = 1
  loadList()
})

onMounted(loadList)

// ─── side panel state ─────────────────────────────────────────────────────────
type PanelMode = 'create' | 'edit' | null
const panelMode = ref<PanelMode>(null)
const editingId = ref<string | null>(null)
const form = ref<DiscountForm>(emptyForm())
const panelLoading = ref(false)
const panelError = ref('')

function openCreate() {
  panelMode.value = 'create'
  editingId.value = null
  form.value = emptyForm()
  panelError.value = ''
}

async function openEdit(item: Discount) {
  panelMode.value = 'edit'
  editingId.value = item.discountId
  panelError.value = ''
  panelLoading.value = true
  try {
    const detail = await apiFetch<Discount>(`/admin/discounts/${item.discountId}`)
    form.value = {
      discountcode: detail.discountcode,
      istype: detail.istype,
      v: String(detail.v),
      startdate: detail.startdate ? detail.startdate.slice(0, 10) : '',
      expiredate: detail.expiredate ? detail.expiredate.slice(0, 10) : '',
      isonetime: detail.isonetime,
      memo: detail.memo ?? '',
      isdisable: detail.isdisable,
    }
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
  panelLoading.value = true
  panelError.value = ''
  try {
    const body = {
      ...form.value,
      v: Number(form.value.v),
    }
    if (panelMode.value === 'create') {
      await apiFetch('/admin/discounts', { method: 'POST', body: JSON.stringify(body) })
    } else {
      await apiFetch(`/admin/discounts/${editingId.value}`, {
        method: 'PUT',
        body: JSON.stringify(body),
      })
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
const deleteTarget = ref<Discount | null>(null)
const deleteLoading = ref(false)
const deleteError = ref('')

function askDelete(item: Discount) {
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
    await apiFetch(`/admin/discounts/${deleteTarget.value.discountId}`, { method: 'DELETE' })
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
  <main class="disc">
    <div class="page-header">
      <h1 class="page-title">折扣管理</h1>
      <button class="btn btn--primary" @click="openCreate">+ 新增折扣碼</button>
    </div>

    <!-- Filter bar -->
    <div class="filter-bar">
      <div class="filter-group">
        <label class="filter-label">狀態篩選</label>
        <div class="seg-ctrl">
          <button
            v-for="opt in ([
              { label: '全部', value: '' },
              { label: '啟用中', value: 'false' },
              { label: '已停用', value: 'true' },
            ] as { label: string; value: FilterDisable }[])"
            :key="opt.value"
            class="seg-ctrl__btn"
            :class="{ 'seg-ctrl__btn--active': filterDisable === opt.value }"
            @click="filterDisable = opt.value"
          >{{ opt.label }}</button>
        </div>
      </div>
    </div>

    <!-- State -->
    <div v-if="loading" class="state-msg">載入中…</div>
    <div v-else-if="error" class="state-msg state-msg--error">{{ error }}</div>

    <!-- Table -->
    <template v-else>
      <div class="card">
        <table class="data-table">
          <thead>
            <tr>
              <th>折扣碼</th>
              <th>類型</th>
              <th>折扣值</th>
              <th>起始日期</th>
              <th>截止日期</th>
              <th>一次性</th>
              <th>狀態</th>
              <th>備註</th>
              <th class="action-th"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in items" :key="item.discountId" class="data-table__row">
              <td class="font-mono font-semibold">{{ item.discountcode }}</td>
              <td>
                <span class="badge" :class="item.istype === 1 ? 'badge--amount' : 'badge--percent'">
                  {{ item.istype === 1 ? '折抵金額' : '折扣比例' }}
                </span>
              </td>
              <td class="font-mono">{{ fmtValue(item) }}</td>
              <td>{{ fmtDate(item.startdate) }}</td>
              <td>{{ fmtDate(item.expiredate) }}</td>
              <td>
                <span class="indicator" :class="item.isonetime ? 'indicator--yes' : 'indicator--no'">
                  {{ item.isonetime ? '是' : '否' }}
                </span>
              </td>
              <td>
                <span class="badge" :class="item.isdisable ? 'badge--disabled' : 'badge--active'">
                  {{ item.isdisable ? '停用' : '啟用' }}
                </span>
              </td>
              <td class="text-muted text-sm">{{ item.memo || '—' }}</td>
              <td class="action-cell">
                <button class="btn btn--sm btn--ghost" @click="openEdit(item)">編輯</button>
                <button class="btn btn--sm btn--danger-ghost" style="margin-left:0.375rem" @click="askDelete(item)">刪除</button>
              </td>
            </tr>
            <tr v-if="items.length === 0">
              <td colspan="9" class="empty-cell">目前沒有折扣碼資料</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="pagination">
        <button class="btn btn--ghost btn--sm" :disabled="page <= 1" @click="page--">‹ 上一頁</button>
        <span class="pagination__info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
        <button class="btn btn--ghost btn--sm" :disabled="page >= totalPages" @click="page++">下一頁 ›</button>
      </div>
    </template>

    <!-- ── Side panel ─────────────────────────────────────────────────────── -->
    <Teleport to="body">
      <div v-if="panelMode" class="panel-overlay" @click.self="closePanel">
        <aside class="side-panel" role="complementary" :aria-label="panelMode === 'create' ? '新增折扣碼' : '編輯折扣碼'">
          <div class="panel__header">
            <h2 class="panel__title">{{ panelMode === 'create' ? '新增折扣碼' : '編輯折扣碼' }}</h2>
            <button class="panel__close" aria-label="關閉" @click="closePanel">
              <svg style="width:1.25rem;height:1.25rem" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <div class="panel__body" :aria-busy="panelLoading">
            <div v-if="panelLoading && panelMode === 'edit' && !form.discountcode" class="state-msg">載入中…</div>
            <template v-else>
              <div class="form-field">
                <label class="form-field__label" for="f-code">折扣碼 <span class="required">*</span></label>
                <input id="f-code" v-model="form.discountcode" type="text" placeholder="例：SUMMER2026" class="form-field__input" />
              </div>

              <div class="form-field">
                <label class="form-field__label">折扣類型 <span class="required">*</span></label>
                <div class="radio-group">
                  <label class="radio-option">
                    <input type="radio" v-model="form.istype" :value="1" />
                    <span>折抵金額（NT$）</span>
                  </label>
                  <label class="radio-option">
                    <input type="radio" v-model="form.istype" :value="2" />
                    <span>折扣比例（%）</span>
                  </label>
                </div>
              </div>

              <div class="form-field">
                <label class="form-field__label" for="f-v">
                  {{ form.istype === 1 ? '折抵金額（NT$）' : '折扣比例（%）' }}
                  <span class="required">*</span>
                </label>
                <input
                  id="f-v"
                  v-model="form.v"
                  type="number"
                  :min="form.istype === 2 ? 1 : 1"
                  :max="form.istype === 2 ? 100 : undefined"
                  :placeholder="form.istype === 1 ? '例：200' : '例：10'"
                  class="form-field__input"
                />
              </div>

              <div class="form-row">
                <div class="form-field">
                  <label class="form-field__label" for="f-start">起始日期</label>
                  <input id="f-start" v-model="form.startdate" type="date" class="form-field__input" />
                </div>
                <div class="form-field">
                  <label class="form-field__label" for="f-expire">截止日期</label>
                  <input id="f-expire" v-model="form.expiredate" type="date" class="form-field__input" />
                </div>
              </div>

              <div class="form-field">
                <label class="form-field__label" for="f-memo">備註</label>
                <textarea id="f-memo" v-model="form.memo" rows="2" placeholder="選填備註" class="form-field__input" style="resize:vertical;"></textarea>
              </div>

              <div class="form-checks">
                <label class="checkbox-option">
                  <input type="checkbox" v-model="form.isonetime" />
                  <span>單次使用（每位會員僅限一次）</span>
                </label>
                <label class="checkbox-option">
                  <input type="checkbox" v-model="form.isdisable" />
                  <span>停用此折扣碼</span>
                </label>
              </div>

              <div v-if="panelError" class="form-error">{{ panelError }}</div>
            </template>
          </div>

          <div class="panel__footer">
            <button class="btn btn--ghost" :disabled="panelLoading" @click="closePanel">取消</button>
            <button
              class="btn btn--primary"
              :disabled="panelLoading || !form.discountcode || !form.v"
              @click="submitForm"
            >
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
            <h2 class="modal__title">確認刪除折扣碼</h2>
          </div>
          <div class="modal__body">
            <p class="delete-msg">
              確定要刪除折扣碼
              <strong class="font-mono">{{ deleteTarget.discountcode }}</strong> 嗎？此操作無法復原。
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
.disc { }

.page-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.5rem; }
.page-title { font-size: 1.5rem; font-weight: 700; color: #1e293b; letter-spacing: -0.02em; }

/* Filter bar */
.filter-bar { margin-bottom: 1.25rem; }
.filter-group { display: inline-flex; flex-direction: column; gap: 0.35rem; }
.filter-label { font-size: 0.75rem; font-weight: 600; color: #64748b; text-transform: uppercase; letter-spacing: 0.05em; }

/* Segmented control */
.seg-ctrl { display: flex; background: #f1f5f9; border-radius: 8px; padding: 3px; gap: 2px; }
.seg-ctrl__btn { padding: 0.3rem 0.85rem; font-size: 0.85rem; font-weight: 500; border: none; background: transparent; cursor: pointer; border-radius: 6px; color: #64748b; transition: all 0.15s; }
.seg-ctrl__btn--active { background: #fff; color: #4f46e5; box-shadow: 0 1px 3px rgba(0,0,0,0.12); font-weight: 600; }
.seg-ctrl__btn:not(.seg-ctrl__btn--active):hover { background: rgba(255,255,255,0.5); color: #334155; }

/* Card */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }

/* Tables */
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.9rem; border-bottom: 1px solid #e2e8f0; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 130px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: #94a3b8; padding: 3rem; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }
.font-semibold { font-weight: 600; }
.text-muted { color: #94a3b8; }
.text-sm { font-size: 0.85rem; }

/* Badge */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.75rem; font-weight: 600; }
.badge--amount   { background: #dbeafe; color: #1e40af; }
.badge--percent  { background: #ede9fe; color: #5b21b6; }
.badge--active   { background: #dcfce7; color: #166534; }
.badge--disabled { background: #f1f5f9; color: #64748b; }

/* Indicator */
.indicator { display: inline-block; padding: 0.18em 0.55em; border-radius: 4px; font-size: 0.75rem; font-weight: 600; }
.indicator--yes { background: #fef3c7; color: #92400e; }
.indicator--no  { background: #f1f5f9; color: #64748b; }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: all 0.15s; white-space: nowrap; }
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
/* Action column */
.action-cell { white-space: nowrap; text-align: right; }

/* Pagination */
.pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.pagination__info { font-size: 0.875rem; color: var(--tf-color-muted); }

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
.panel__title { font-size: 1.05rem; font-weight: 700; color: #1e293b; }
.panel__close { background: none; border: none; cursor: pointer; color: #94a3b8; padding: 0.25rem; border-radius: 4px; display: flex; }
.panel__close:hover { color: #475569; background: #f1f5f9; }
.panel__body { flex: 1; overflow-y: auto; padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
.panel__footer { padding: 1rem 1.5rem; border-top: 1px solid #e2e8f0; display: flex; justify-content: flex-end; gap: 0.5rem; }

/* Form elements */
.form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
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
.form-field__input:focus { outline: none; border-color: #6366f1; box-shadow: 0 0 0 3px rgba(99,102,241,0.15); }

.radio-group { display: flex; gap: 1.25rem; }
.radio-option { display: flex; align-items: center; gap: 0.4rem; font-size: 0.875rem; color: #475569; cursor: pointer; }
.radio-option input { accent-color: var(--tf-color-primary); }

.form-checks { display: flex; flex-direction: column; gap: 0.6rem; }
.checkbox-option { display: flex; align-items: center; gap: 0.5rem; font-size: 0.875rem; color: #475569; cursor: pointer; }
.checkbox-option input { accent-color: var(--tf-color-primary); width: 16px; height: 16px; }

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
.modal__title { font-size: 1.05rem; font-weight: 700; color: #1e293b; }
.modal__body { padding: 1rem 1.5rem; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 0 1.5rem 1.25rem; }

.delete-msg { color: #475569; font-size: 0.9rem; line-height: 1.6; }
</style>

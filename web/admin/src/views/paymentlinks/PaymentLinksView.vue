<script setup lang="ts">
// 收款連結 — 後台產生一次性付款連結給客人付款（非商城訂單，屬臨時收款）。
// 付款方式於建立時指定（信用卡走 FISC 刷卡頁、LINE Pay 走 LINE Pay 付款頁），客人不能改。
// 權限沿用 OrderMs；DB Lims 無對應模組列，側欄不會出現，入口為儀表板「快速導覽」卡片。
import { ref, reactive, computed, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'
import { PAY_TYPE, PAYMENT_LINK_PAY_METHOD_OPTIONS, payTypeLabel } from '../../lib/payType'

interface PaymentLink {
  id: string
  code: string
  token: string
  url: string
  title: string
  note: string | null
  amount: number
  status: number            // 0=未付款 1=已付款 2=已作廢
  isExpired: boolean        // 後端已算好（未付款且已過期）
  payMethod: number         // PayType 編碼：1=信用卡 8=LINE Pay
  customerName: string | null
  customerMobile: string | null
  customerAddress: string | null   // 已組好的完整地址
  lastPan4: string | null
  payDate: string | null
  expireDate: string | null
  createDate: string
}

interface CreatedLink { id: string; code: string; token: string; url: string }

// ── 列表 ──────────────────────────────────────────────────────────────────────

const items = ref<PaymentLink[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 20
const loading = ref(false)
const error = ref('')

// 「已逾期」是 status=0 的衍生狀態，不是獨立 status 值，故用 status + isExpired 兩個參數表達。
const TABS = [
  { key: 'all',     label: '全部',   status: null, isExpired: null },
  { key: 'unpaid',  label: '未付款', status: 0,    isExpired: false },
  { key: 'overdue', label: '已逾期', status: 0,    isExpired: true },
  { key: 'paid',    label: '已付款', status: 1,    isExpired: null },
  { key: 'voided',  label: '已作廢', status: 2,    isExpired: null },
] as const
const activeTab = ref<string>('all')

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

async function loadList() {
  loading.value = true
  error.value = ''
  try {
    const tab = TABS.find(t => t.key === activeTab.value)!
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize) })
    if (tab.status !== null) params.set('status', String(tab.status))
    if (tab.isExpired !== null) params.set('isExpired', String(tab.isExpired))

    const res = await apiFetch<{ items: PaymentLink[]; total: number }>(`/admin/paymentlinks?${params}`)
    items.value = res.items
    total.value = res.total
  } catch (e: any) {
    error.value = (e as ApiError).problem?.detail ?? e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

watch(activeTab, () => { page.value = 1; loadList() })
watch(page, loadList)
onMounted(loadList)

// ── 狀態呈現 ──────────────────────────────────────────────────────────────────

function statusInfo(item: PaymentLink) {
  if (item.status === 1) return { text: '已付款', cls: 'badge--paid' }
  if (item.status === 2) return { text: '已作廢', cls: 'badge--voided' }
  if (item.isExpired) return { text: '已逾期', cls: 'badge--overdue' }
  return { text: '未付款', cls: 'badge--unpaid' }
}

// 未付款（含逾期）才有作廢／標記已付款的意義；已付/已作廢直接不 render，不留無效按鈕。
const canAct = (item: PaymentLink) => item.status === 0

function fmtDateTime(v: string | null) {
  if (!v) return '—'
  const d = new Date(v)
  return `${String(d.getMonth() + 1).padStart(2, '0')}/${String(d.getDate()).padStart(2, '0')} ` +
         `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}
function fmtDate(v: string | null) {
  if (!v) return ''
  const d = new Date(v)
  return `${d.getFullYear()}/${String(d.getMonth() + 1).padStart(2, '0')}/${String(d.getDate()).padStart(2, '0')}`
}
const fmtAmount = (n: number) => n.toLocaleString('zh-TW')

// ── 複製 ──────────────────────────────────────────────────────────────────────

const toastMsg = ref('')
let toastTimer: ReturnType<typeof setTimeout> | null = null
function showToast(msg: string) {
  toastMsg.value = msg
  if (toastTimer) clearTimeout(toastTimer)
  toastTimer = setTimeout(() => (toastMsg.value = ''), 2000)
}
onBeforeUnmount(() => { if (toastTimer) clearTimeout(toastTimer) })

// navigator.clipboard 需要 secure context（https 或 localhost）；內網 IP 預覽時會不存在，
// 故保留 execCommand fallback，兩層都失敗才要求使用者手動複製。
async function copyText(text: string, fallbackEl?: HTMLInputElement | null): Promise<boolean> {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text)
      return true
    }
    throw new Error('clipboard api unavailable')
  } catch {
    const ta = document.createElement('textarea')
    ta.value = text
    ta.style.position = 'fixed'
    ta.style.opacity = '0'
    document.body.appendChild(ta)
    ta.select()
    let ok = false
    try { ok = document.execCommand('copy') } catch { ok = false }
    document.body.removeChild(ta)
    if (!ok && fallbackEl) { fallbackEl.focus(); fallbackEl.select() }
    return ok
  }
}

const justCopied = ref(false)
const linkInput = ref<HTMLInputElement | null>(null)

async function onCopyMain() {
  if (!created.value) return
  const ok = await copyText(created.value.url, linkInput.value)
  if (ok) {
    justCopied.value = true
    setTimeout(() => (justCopied.value = false), 1800)
  } else {
    showToast('複製失敗，請手動選取後按 Ctrl+C / Cmd+C')
  }
}

async function onCopyRow(item: PaymentLink) {
  const ok = await copyText(item.url)
  showToast(ok ? '已複製收款連結' : '複製失敗，請改用手動複製')
}

// ── 建立面板 ──────────────────────────────────────────────────────────────────

const panelOpen = ref(false)
const created = ref<CreatedLink | null>(null)   // 非 null = 面板切換為成功態
const saving = ref(false)
const formError = ref('')

const form = reactive({
  amount: '' as string | number,
  title: '',
  note: '',
  validDays: 7 as string | number,
  payMethod: PAY_TYPE.CREDIT_CARD as number,
})

// 成功態要顯示金額與到期日，但 created 只有 id/code/token/url，故另存送出當下的表單快照。
const createdMeta = reactive({ title: '', amount: 0, validDays: 0, payMethod: PAY_TYPE.CREDIT_CARD as number })

function openPanel() {
  form.amount = ''
  form.title = ''
  form.note = ''
  form.validDays = 7
  form.payMethod = PAY_TYPE.CREDIT_CARD
  formError.value = ''
  created.value = null
  panelOpen.value = true
}

function closePanel() {
  panelOpen.value = false
  created.value = null
  loadList()   // 剛建立的那筆會出現在最上方，是忘記複製時的救援路徑
}

const expireHint = computed(() => {
  if (!createdMeta.validDays) return '不限期'
  const d = new Date()
  d.setDate(d.getDate() + createdMeta.validDays)
  return `${createdMeta.validDays} 天後到期（${fmtDate(d.toISOString())}）`
})

async function submitCreate() {
  const amount = Number(form.amount)
  const validDays = Number(form.validDays)
  formError.value = ''

  if (!form.title.trim()) { formError.value = '請填寫收款項目說明。'; return }
  if (!Number.isInteger(amount) || amount <= 0) { formError.value = '金額必須為大於 0 的整數。'; return }
  if (!Number.isInteger(validDays) || validDays < 0 || validDays > 365) {
    formError.value = '有效天數需介於 0（不限期）至 365 之間。'; return
  }

  saving.value = true
  try {
    const res = await apiFetch<CreatedLink>('/admin/paymentlinks', {
      method: 'POST',
      body: JSON.stringify({
        title: form.title.trim(),
        note: form.note.trim() || null,
        amount,
        validDays,
        payMethod: form.payMethod,
      }),
    })
    createdMeta.title = form.title.trim()
    createdMeta.amount = amount
    createdMeta.validDays = validDays
    createdMeta.payMethod = form.payMethod
    created.value = res
    await nextTick()
    linkInput.value?.focus()
  } catch (e: any) {
    formError.value = (e as ApiError).problem?.detail ?? e.message ?? '建立失敗'
  } finally {
    saving.value = false
  }
}

function createNext() {
  form.amount = ''
  form.title = ''
  form.note = ''
  form.validDays = 7
  form.payMethod = PAY_TYPE.CREDIT_CARD
  formError.value = ''
  created.value = null
  loadList()
}

// ── 作廢 ──────────────────────────────────────────────────────────────────────

const voidTarget = ref<PaymentLink | null>(null)
const voidLoading = ref(false)
const voidError = ref('')

function askVoid(item: PaymentLink) { voidTarget.value = item; voidError.value = '' }
function cancelVoid() { if (!voidLoading.value) voidTarget.value = null }

async function confirmVoid() {
  if (!voidTarget.value) return
  voidLoading.value = true
  voidError.value = ''
  try {
    await apiFetch(`/admin/paymentlinks/${voidTarget.value.id}/cancel`, { method: 'PATCH' })
    voidTarget.value = null
    showToast('已作廢收款連結')
    await loadList()
  } catch (e: any) {
    voidError.value = (e as ApiError).problem?.detail ?? e.message ?? '作廢失敗'
  } finally {
    voidLoading.value = false
  }
}

// ── 手動標記已付款 ────────────────────────────────────────────────────────────

const markTarget = ref<PaymentLink | null>(null)
const markLoading = ref(false)
const markError = ref('')

function askMarkPaid(item: PaymentLink) { markTarget.value = item; markError.value = '' }
function cancelMarkPaid() { if (!markLoading.value) markTarget.value = null }

async function confirmMarkPaid() {
  if (!markTarget.value) return
  markLoading.value = true
  markError.value = ''
  try {
    await apiFetch(`/admin/paymentlinks/${markTarget.value.id}/paid`, { method: 'PATCH' })
    markTarget.value = null
    showToast('已標記為已付款')
    await loadList()
  } catch (e: any) {
    markError.value = (e as ApiError).problem?.detail ?? e.message ?? '標記失敗'
  } finally {
    markLoading.value = false
  }
}
</script>

<template>
  <div class="paylinks">
    <div class="paylinks__header">
      <div>
        <h1 class="paylinks__title">收款連結</h1>
        <p class="paylinks__subtitle">產生一次性付款連結給客人付款；款項不進訂單與會計收入，請人工入帳與開立發票。</p>
      </div>
      <button class="btn btn--primary" @click="openPanel">+ 建立收款連結</button>
    </div>

    <div class="paylinks__tabs">
      <button
        v-for="t in TABS"
        :key="t.key"
        class="paylinks__tab"
        :class="{ 'paylinks__tab--active': activeTab === t.key }"
        @click="activeTab = t.key"
      >{{ t.label }}</button>
    </div>

    <p v-if="error" class="paylinks__error">{{ error }}</p>

    <div class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th>收款單號</th>
            <th>收款項目</th>
            <th>金額</th>
            <th>付款方式</th>
            <th>狀態</th>
            <th>客人資訊</th>
            <th>建立時間</th>
            <th>付款時間</th>
            <th class="action-th">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading">
            <td colspan="9" class="empty-cell">載入中…</td>
          </tr>
          <tr v-else-if="!items.length">
            <td colspan="9" class="empty-cell">目前沒有收款連結</td>
          </tr>
          <tr v-for="item in items" v-else :key="item.id" class="data-table__row">
            <td class="font-mono">{{ item.code }}</td>
            <td>
              <div class="font-semibold">{{ item.title }}</div>
              <div v-if="item.note" class="text-muted" :title="item.note">{{ item.note }}</div>
            </td>
            <td class="font-semibold">NT$ {{ fmtAmount(item.amount) }}</td>
            <td class="text-muted">{{ payTypeLabel(item.payMethod) }}</td>
            <td>
              <span
                class="badge"
                :class="statusInfo(item).cls"
                :title="item.expireDate ? `到期日：${fmtDate(item.expireDate)}` : '不限期'"
              >{{ statusInfo(item).text }}</span>
            </td>
            <td class="customer-cell">
              <template v-if="item.customerName">
                <div class="customer-cell__line1">
                  <span class="customer-cell__name">{{ item.customerName }}</span>
                  <span class="customer-cell__mobile">{{ item.customerMobile || '—' }}</span>
                </div>
                <div v-if="item.customerAddress" class="customer-cell__addr" :title="item.customerAddress">
                  {{ item.customerAddress }}
                </div>
              </template>
              <span v-else class="text-muted">尚未付款</span>
            </td>
            <td class="text-muted">{{ fmtDateTime(item.createDate) }}</td>
            <td class="text-muted">{{ fmtDateTime(item.payDate) }}</td>
            <td class="action-cell">
              <div class="action-cell__inner">
                <button class="btn btn--sm btn--secondary" @click="onCopyRow(item)">複製連結</button>
                <template v-if="canAct(item)">
                  <button class="btn btn--sm btn--accent" @click="askMarkPaid(item)">標記已付</button>
                  <button class="btn btn--sm btn--danger" @click="askVoid(item)">作廢</button>
                </template>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="paylinks__pagination">
      <span class="paylinks__page-info">第 {{ page }} / {{ totalPages }} 頁（共 {{ total }} 筆）</span>
      <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="page--">上一頁</button>
      <button class="btn btn--sm btn--ghost" :disabled="page >= totalPages" @click="page++">下一頁</button>
    </div>

    <!-- ── 建立面板（送出成功後原地切換為成功態，不關閉：複製是最高頻動作）── -->
    <div v-if="panelOpen" class="panel-overlay" @click.self="closePanel">
      <div class="side-panel">
        <div class="panel__header">
          <h2 class="panel__title">{{ created ? '✓ 已建立收款連結' : '建立收款連結' }}</h2>
          <button class="panel__close" @click="closePanel">✕</button>
        </div>

        <!-- 表單態 -->
        <template v-if="!created">
          <div class="panel__body">
            <div class="form-field">
              <label class="form-field__label">金額（元）<span class="required">*</span></label>
              <input v-model="form.amount" class="form-field__input" type="number" min="1" step="1" placeholder="例如 1200">
            </div>

            <div class="form-field">
              <label class="form-field__label">
                收款項目說明 <span class="required">*</span>
                <span class="form-field__count">{{ form.title.length }}/100</span>
              </label>
              <input v-model="form.title" class="form-field__input" maxlength="100" placeholder="例如：客訂商品補款">
              <span class="form-field__hint">會顯示給客人看</span>
            </div>

            <div class="form-field">
              <label class="form-field__label">
                內部備註
                <span class="form-field__count">{{ form.note.length }}/500</span>
              </label>
              <textarea v-model="form.note" class="form-field__input" rows="3" maxlength="500"></textarea>
              <span class="form-field__hint">僅後台可見，客人不會看到</span>
            </div>

            <div class="form-field">
              <label class="form-field__label">付款方式 <span class="required">*</span></label>
              <select v-model.number="form.payMethod" class="form-field__input">
                <option v-for="o in PAYMENT_LINK_PAY_METHOD_OPTIONS" :key="o.value" :value="o.value">{{ o.label }}</option>
              </select>
              <span class="form-field__hint">建立後不可更改，客人只能用指定的方式付款</span>
            </div>

            <div class="form-field">
              <label class="form-field__label">有效天數</label>
              <input v-model="form.validDays" class="form-field__input" type="number" min="0" max="365" step="1">
              <span class="form-field__hint">0 = 不限期，請謹慎使用</span>
            </div>

            <p v-if="formError" class="form-error">{{ formError }}</p>
          </div>
          <div class="panel__footer">
            <button class="btn btn--ghost" :disabled="saving" @click="closePanel">取消</button>
            <button class="btn btn--primary" :disabled="saving" @click="submitCreate">
              {{ saving ? '建立中…' : '建立' }}
            </button>
          </div>
        </template>

        <!-- 成功態 -->
        <template v-else>
          <div class="panel__body">
            <div class="created-code font-mono">{{ created.code }}</div>

            <div class="copy-block">
              <input ref="linkInput" class="copy-block__url" :value="created.url" readonly @focus="($event.target as HTMLInputElement).select()">
              <button class="btn btn--copy" :class="{ 'btn--copy--done': justCopied }" @click="onCopyMain">
                {{ justCopied ? '✓ 已複製' : '複製連結' }}
              </button>
            </div>

            <div class="success-summary">
              <div class="success-summary__row">
                <span class="success-summary__label">收款項目</span><span>{{ createdMeta.title }}</span>
              </div>
              <div class="success-summary__row">
                <span class="success-summary__label">金額</span><span>NT$ {{ fmtAmount(createdMeta.amount) }}</span>
              </div>
              <div class="success-summary__row">
                <span class="success-summary__label">付款方式</span><span>{{ payTypeLabel(createdMeta.payMethod) }}</span>
              </div>
              <div class="success-summary__row">
                <span class="success-summary__label">有效期限</span><span>{{ expireHint }}</span>
              </div>
            </div>
          </div>
          <div class="panel__footer">
            <button class="btn btn--ghost" @click="createNext">建立下一筆</button>
            <button class="btn btn--primary" @click="closePanel">完成</button>
          </div>
        </template>
      </div>
    </div>

    <!-- ── 作廢確認 ── -->
    <div v-if="voidTarget" class="modal-overlay" @click.self="cancelVoid">
      <div class="modal">
        <div class="modal__header"><h2 class="modal__title">確認作廢收款連結</h2></div>
        <div class="modal__body">
          <p class="modal__msg">
            收款單號 <strong class="font-mono">{{ voidTarget.code }}</strong>
            （{{ voidTarget.title }}・NT$ {{ fmtAmount(voidTarget.amount) }}）
          </p>
          <p class="modal__hint">作廢後客人將無法用此連結付款，此操作無法復原。</p>
          <p v-if="voidError" class="form-error">{{ voidError }}</p>
        </div>
        <div class="modal__footer">
          <button class="btn btn--ghost" :disabled="voidLoading" @click="cancelVoid">取消</button>
          <button class="btn btn--danger" :disabled="voidLoading" @click="confirmVoid">
            {{ voidLoading ? '作廢中…' : '確認作廢' }}
          </button>
        </div>
      </div>
    </div>

    <!-- ── 手動標記已付款確認 ── -->
    <div v-if="markTarget" class="modal-overlay" @click.self="cancelMarkPaid">
      <div class="modal">
        <div class="modal__header"><h2 class="modal__title">確認標記為已付款</h2></div>
        <div class="modal__body">
          <p class="modal__msg">
            收款單號 <strong class="font-mono">{{ markTarget.code }}</strong>
            （{{ markTarget.title }}・NT$ {{ fmtAmount(markTarget.amount) }}）
          </p>
          <p class="modal__hint">
            此操作僅用於已透過其他方式（現金、匯款等）確認收款的情況，系統不會再次向客人請款，請先確認金額已實際入帳。
          </p>
          <p v-if="markError" class="form-error">{{ markError }}</p>
        </div>
        <div class="modal__footer">
          <button class="btn btn--ghost" :disabled="markLoading" @click="cancelMarkPaid">取消</button>
          <button class="btn btn--accent" :disabled="markLoading" @click="confirmMarkPaid">
            {{ markLoading ? '處理中…' : '確認標記已付款' }}
          </button>
        </div>
      </div>
    </div>

    <Teleport to="body">
      <Transition name="toast">
        <div v-if="toastMsg" class="copy-toast">{{ toastMsg }}</div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
/* ── 根容器 / 標題列 ── */
.paylinks__header { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; margin-bottom: 1.25rem; }
.paylinks__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; font-size: 1.35rem; }
.paylinks__subtitle { margin: 0.35rem 0 0; font-size: 0.85rem; color: var(--tf-color-muted); }
.paylinks__error { color: #dc3545; font-size: 0.875rem; margin-bottom: 0.75rem; }

/* ── 狀態 tabs ── */
.paylinks__tabs { display: flex; flex-wrap: wrap; gap: 0.25rem; margin-bottom: 1rem; border-bottom: 2px solid var(--tf-color-border); }
.paylinks__tab {
  padding: 0.45rem 1rem; border: 1px solid var(--tf-color-border); border-bottom: none;
  border-radius: 4px 4px 0 0; background: #fff; color: #495057;
  cursor: pointer; font-size: 0.875rem; font-family: inherit;
  transition: background 0.15s, color 0.15s; position: relative; bottom: -2px;
}
.paylinks__tab:hover:not(.paylinks__tab--active) { background: #f1f3f5; }
.paylinks__tab--active { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); font-weight: 500; }

/* ── 表格 ── */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 980px; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.action-th { width: 260px; }
/* td 不直接 display:flex — 按鈕數量不同時（已付/已作廢只有 1 顆）儲存格會脫離
   table-cell 佈局導致列高與對齊跑掉；改由內層 div 套 flex，td 維持正常 table-cell。 */
.action-cell { white-space: nowrap; vertical-align: middle; }
.action-cell__inner { display: flex; gap: 0.35rem; flex-wrap: wrap; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.font-mono { font-family: 'IBM Plex Mono', ui-monospace, monospace; }
.font-semibold { font-weight: 600; }
.text-muted { color: var(--tf-color-muted); font-size: 0.85rem; }

/* ── Badge ── */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 500; white-space: nowrap; cursor: default; }
.badge--paid    { background: #d4edda; color: #155724; }
.badge--unpaid  { background: #fff3cd; color: #856404; }
.badge--overdue { background: #ffe4c4; color: #9a3412; }
.badge--voided  { background: #f8d7da; color: #721c24; }

/* ── 客人資訊欄（姓名/手機/地址合併，地址單行截斷 + title 顯示全文）── */
.customer-cell { max-width: 220px; }
.customer-cell__line1 { display: flex; align-items: baseline; gap: 0.4rem; }
.customer-cell__name { font-weight: 600; color: #1e293b; font-size: 0.875rem; }
.customer-cell__mobile { font-family: 'IBM Plex Mono', ui-monospace, monospace; font-size: 0.78rem; color: var(--tf-color-muted); }
.customer-cell__addr { margin-top: 0.15rem; font-size: 0.78rem; color: var(--tf-color-muted); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 220px; }

/* ── 分頁 ── */
.paylinks__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.paylinks__page-info { font-size: 0.875rem; color: var(--tf-color-muted); }

/* ── 按鈕 ── */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
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

/* ── 複製主按鈕（成功態專用，全頁唯一帶陰影的按鈕）── */
.created-code { font-size: 1.15rem; font-weight: 700; color: var(--tf-color-primary-dark); }
.copy-block { display: flex; flex-direction: column; gap: 0.5rem; margin: 0.25rem 0 0.5rem; }
.copy-block__url { padding: 0.55rem 0.75rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-family: 'IBM Plex Mono', ui-monospace, monospace; font-size: 0.8rem; color: #475569; background: #f8fafc; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.copy-block__url:focus { outline: none; border-color: var(--tf-color-primary); }
.btn--copy { width: 100%; padding: 0.75rem 1rem; font-size: 0.95rem; font-weight: 600; gap: 0.5rem; background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); box-shadow: 0 4px 14px rgba(38, 183, 188, 0.28); }
.btn--copy:hover { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--copy--done { background: #16a34a; border-color: #16a34a; box-shadow: 0 4px 14px rgba(22, 163, 74, 0.28); }

.success-summary { display: flex; flex-direction: column; gap: 0.4rem; padding-top: 0.75rem; border-top: 1px solid var(--tf-color-border); font-size: 0.85rem; color: #475569; }
.success-summary__row { display: flex; justify-content: space-between; gap: 1rem; }
.success-summary__label { color: var(--tf-color-muted); }

/* ── 側滑面板（docs/10 標準）── */
.panel-overlay { position: fixed; inset: 0; z-index: 50; background: rgba(15, 23, 42, 0.4); backdrop-filter: blur(1px); display: flex; justify-content: flex-end; animation: fadeIn 0.15s ease; }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
.side-panel { width: 100%; max-width: 440px; height: 100%; background: #fff; box-shadow: -8px 0 40px rgba(0, 0, 0, 0.15); display: flex; flex-direction: column; animation: slideInRight 0.22s cubic-bezier(0.25, 0.46, 0.45, 0.94); }
@keyframes slideInRight { from { transform: translateX(100%); } to { transform: none; } }
.panel__header { display: flex; align-items: center; justify-content: space-between; padding: 1.25rem 1.5rem; border-bottom: 1px solid var(--tf-color-border); }
.panel__title { font-size: 1.05rem; font-weight: 700; color: #1e293b; margin: 0; }
.panel__close { background: none; border: none; cursor: pointer; color: var(--tf-color-muted); padding: 0.25rem; border-radius: 4px; display: flex; font-size: 1rem; }
.panel__close:hover { color: #475569; background: #f1f5f9; }
.panel__body { flex: 1; overflow-y: auto; padding: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
.panel__footer { padding: 1rem 1.5rem; border-top: 1px solid var(--tf-color-border); display: flex; justify-content: flex-end; gap: 0.5rem; }

/* ── 表單欄位（docs/10 標準）── */
.form-field { display: flex; flex-direction: column; gap: 0.35rem; }
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; display: flex; align-items: baseline; gap: 0.35rem; }
.form-field__count { margin-left: auto; font-weight: 400; font-size: 0.75rem; color: var(--tf-color-muted); }
.form-field__hint { font-size: 0.75rem; color: var(--tf-color-muted); }
.required { color: #ef4444; }
.form-field__input { padding: 0.45rem 0.65rem; border: 1px solid var(--tf-color-border); border-radius: 4px; font-size: 0.875rem; color: #1e293b; background: #fff; transition: border-color 0.15s; font-family: inherit; resize: vertical; }
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38, 183, 188, 0.15); }
.form-error { color: #dc3545; font-size: 0.85rem; margin: 0; }

/* ── Modal（docs/10 標準）── */
.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15, 23, 42, 0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0, 0, 0, 0.2); width: 100%; max-width: 380px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1.25rem 1.4rem; display: flex; flex-direction: column; gap: 0.6rem; }
.modal__msg { font-size: 0.9rem; color: #334155; margin: 0; }
.modal__hint { font-size: 0.85rem; color: var(--tf-color-muted); margin: 0; line-height: 1.6; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }

/* ── Toast ── */
.copy-toast { position: fixed; left: 50%; bottom: 2.5rem; transform: translateX(-50%); z-index: 1000; padding: 0.65rem 1.4rem; font-size: 0.875rem; color: #fff; background: rgba(21, 100, 103, 0.96); border-radius: 999px; box-shadow: 0 6px 20px rgba(0, 0, 0, 0.18); white-space: nowrap; }
.toast-enter-active, .toast-leave-active { transition: opacity 0.25s ease, transform 0.25s ease; }
.toast-enter-from, .toast-leave-to { opacity: 0; transform: translate(-50%, 0.6rem); }
</style>

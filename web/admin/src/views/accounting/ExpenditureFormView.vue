<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

const route = useRoute()
const router = useRouter()

const id = route.params.id as string | undefined
const isEdit = !!id

// ── 本地助手 ──────────────────────────────────────────────────────

function fmtDate(d: string | null) { return d ? String(d).slice(0, 10) : '—' }
function fmtMoney(n: number) { return n == null ? '—' : `NT$ ${Number(n).toLocaleString('zh-TW')}` }

// ── 型別 ─────────────────────────────────────────────────────────

interface AccountingOption {
  accountingId: string
  accountingCode: string
  title: string
}

interface LineRow {
  expenditureDetailId?: string
  accountingId: string
  summary: string
  price: string
}

// ── 狀態 ─────────────────────────────────────────────────────────

const accountingOptions = ref<AccountingOption[]>([])

const header = reactive({
  expenditureDate: '',
  note: '',
})

const lines = ref<LineRow[]>([{ accountingId: '', summary: '', price: '' }])

const submitting = ref(false)
const submitError = ref('')
const initLoading = ref(false)

// ── 計算合計 ──────────────────────────────────────────────────────

const lineTotal = computed(() =>
  lines.value.reduce((acc, r) => acc + (Number(r.price) || 0), 0)
)

// 依 accountingId 取科目名稱（對照舊系統「科目名稱」自動帶出欄）
function titleOf(accountingId: string) {
  return accountingOptions.value.find(o => o.accountingId === accountingId)?.title ?? ''
}

// ── 初始化 ────────────────────────────────────────────────────────

onMounted(async () => {
  initLoading.value = true
  try {
    // 載入會計科目選項
    accountingOptions.value = await apiFetch<AccountingOption[]>('/admin/accountings')

    // 若為編輯，載入現有資料
    if (isEdit) {
      const data = await apiFetch<{
        expenditure: { expenditureDate: string; note: string | null }
        details: { expenditureDetailId: string; accountingId?: string; accountingCode: string; accountingTitle: string; summary: string | null; price: number }[]
      }>(`/admin/expenditures/${id}`)

      header.expenditureDate = String(data.expenditure.expenditureDate).slice(0, 10)
      header.note = data.expenditure.note ?? ''

      if (data.details && data.details.length > 0) {
        // 將 accountingCode+title 對應回 accountingId
        lines.value = data.details.map(d => {
          // 優先使用 API 回傳的 accountingId；若無則從 options 比對
          let resolvedId = d.accountingId ?? ''
          if (!resolvedId) {
            const matched = accountingOptions.value.find(
              o => o.accountingCode === d.accountingCode && o.title === d.accountingTitle
            )
            resolvedId = matched?.accountingId ?? ''
          }
          return {
            expenditureDetailId: d.expenditureDetailId,
            accountingId: resolvedId,
            summary: d.summary ?? '',
            price: String(d.price),
          }
        })
      }
    }
  } catch {
    // 若科目載入失敗仍保持空陣列，表單依舊可操作
  } finally {
    initLoading.value = false
  }
})

// ── 明細列操作 ────────────────────────────────────────────────────

function addLine() {
  lines.value.push({ accountingId: '', summary: '', price: '' })
}

function removeLine(idx: number) {
  if (lines.value.length <= 1) return
  lines.value.splice(idx, 1)
}

// ── 提交 ─────────────────────────────────────────────────────────

async function handleSubmit() {
  submitError.value = ''

  // 驗證
  if (!header.expenditureDate) {
    submitError.value = '請填寫支出日期'
    return
  }
  if (lines.value.length === 0) {
    submitError.value = '請至少新增一筆支出明細'
    return
  }
  const seen = new Set<string>()
  for (let i = 0; i < lines.value.length; i++) {
    const l = lines.value[i]
    if (!l.accountingId) {
      submitError.value = `第 ${i + 1} 列請選擇科目`
      return
    }
    if (seen.has(l.accountingId)) {
      submitError.value = '科目不可以重複加入！'   // 對照舊系統規則
      return
    }
    seen.add(l.accountingId)
    if (!l.price || Number(l.price) <= 0) {
      submitError.value = `第 ${i + 1} 列請輸入有效金額（> 0）`
      return
    }
  }

  submitting.value = true
  try {
    if (!isEdit) {
      // 新增
      await apiFetch('/admin/expenditures', {
        method: 'POST',
        body: JSON.stringify({
          expenditureDate: header.expenditureDate,
          note: header.note || null,
          lines: lines.value.map(r => ({
            accountingId: r.accountingId,
            price: Number(r.price),
            summary: r.summary || null,
          })),
        }),
      })
    } else {
      // 編輯
      await apiFetch(`/admin/expenditures/${id}`, {
        method: 'PUT',
        body: JSON.stringify({
          expenditureDate: header.expenditureDate,
          note: header.note || null,
          lines: lines.value.map(r => ({
            expenditureDetailId: r.expenditureDetailId ?? null,
            accountingId: r.accountingId,
            price: Number(r.price),
            summary: r.summary || null,
          })),
        }),
      })
    }
    router.push('/admin/expenditures')
  } catch (e) {
    submitError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
  } finally {
    submitting.value = false
  }
}

// suppress unused-import lint for fmtDate / fmtMoney (available for template if needed)
void fmtDate
void fmtMoney
</script>

<template>
  <div class="expform">
    <!-- 頁首（h1 左、返回 右，比照 OrderCreateView） -->
    <div class="expform__header">
      <h1 class="expform__title">{{ isEdit ? '編輯營業支出' : '新增營業支出' }}</h1>
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/expenditures')">&larr; 返回支出列表</button>
    </div>

    <p v-if="initLoading" class="expform__muted">載入中…</p>

    <form v-if="!initLoading" @submit.prevent="handleSubmit">
      <div class="expform__layout">

        <!-- 基本資料 -->
        <div class="form-card">
          <h2 class="form-section__title">基本資料</h2>
          <div class="form-row">
            <div class="form-field">
              <label class="label" for="expenditureDate">支出日期 <span class="req">*</span></label>
              <input
                id="expenditureDate"
                v-model="header.expenditureDate"
                type="date"
                class="input"
                required
              />
            </div>
          </div>
          <div class="form-row">
            <div class="form-field form-field--full">
              <label class="label" for="note">備註</label>
              <textarea
                id="note"
                v-model="header.note"
                class="textarea"
                rows="3"
                placeholder="備註（選填）"
              ></textarea>
            </div>
          </div>
        </div>

        <!-- 支出明細 -->
        <div class="form-card">
          <h2 class="form-section__title">支出明細</h2>

          <!-- 明細列表（欄位對照舊系統：科目代號 / 科目名稱 / 備註 / 金額 / 刪除） -->
          <div class="expform__lines">
            <!-- 欄位標題 -->
            <div class="expform__line-header">
              <span>科目代號 <span class="req">*</span></span>
              <span>科目名稱</span>
              <span>備註</span>
              <span>金額 <span class="req">*</span></span>
              <span class="expform__col-action"></span>
            </div>

            <!-- 每一列 -->
            <div
              v-for="(line, idx) in lines"
              :key="idx"
              class="expform__line-row"
            >
              <div>
                <select v-model="line.accountingId" class="select" required>
                  <option value="">請選擇</option>
                  <option
                    v-for="opt in accountingOptions"
                    :key="opt.accountingId"
                    :value="opt.accountingId"
                  >{{ opt.accountingCode }}</option>
                </select>
              </div>
              <div class="expform__col-title">{{ titleOf(line.accountingId) || '—' }}</div>
              <div>
                <input
                  v-model="line.summary"
                  type="text"
                  class="input"
                  placeholder="備註（選填）"
                />
              </div>
              <div>
                <input
                  v-model="line.price"
                  type="number"
                  min="1"
                  class="input"
                  placeholder="0"
                  required
                />
              </div>
              <div class="expform__col-action">
                <button
                  type="button"
                  class="btn btn--sm btn--danger-ghost"
                  :disabled="lines.length <= 1"
                  @click="removeLine(idx)"
                >刪除</button>
              </div>
            </div>

            <!-- 合計列 -->
            <div class="expform__line-total">
              <span>合計</span>
              <span class="expform__total-value">{{ fmtMoney(lineTotal) }}</span>
            </div>
          </div>

          <!-- 新增支出明細按鈕 -->
          <button type="button" class="btn btn--ghost expform__add-line" @click="addLine">
            + 新增支出
          </button>
        </div>

        <!-- 錯誤訊息 -->
        <p v-if="submitError" class="form-msg--error">{{ submitError }}</p>

        <!-- 操作列（取消 ghost 左、主操作 primary 右，比照介面規範） -->
        <div class="expform__actions">
          <button type="button" class="btn btn--ghost" @click="router.push('/admin/expenditures')">取消</button>
          <button type="submit" class="btn btn--primary" :disabled="submitting">
            {{ submitting ? (isEdit ? '儲存中…' : '建立中…') : (isEdit ? '儲存變更' : '建立支出單') }}
          </button>
        </div>

      </div>
    </form>
  </div>
</template>

<style scoped>
/* ── 根容器 ── */
.expform { width: 100%; }

/* ── 頁首 ── */
.expform__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.5rem;
}
.expform__title {
  font-family: var(--tf-font-heading);
  color: var(--tf-color-primary-dark);
  font-size: 1.25rem;
  margin: 0;
}
.expform__muted { color: var(--tf-color-muted); }

/* ── 單欄版型 ── */
.expform__layout { display: flex; flex-direction: column; max-width: 860px; }

/* ── 卡片 ── */
.form-card {
  background: #fff;
  border: 1px solid var(--tf-color-border);
  border-radius: 6px;
  padding: 1.25rem;
  margin-bottom: 1.25rem;
}

/* ── Section 標題 ── */
.form-section__title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--tf-color-primary-dark);
  margin: 0 0 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--tf-color-border);
}

/* ── 欄位 ── */
.form-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 0.75rem 1.25rem;
  margin-bottom: 0.75rem;
}
.form-field { display: flex; flex-direction: column; gap: 0.3rem; }
.form-field--full { grid-column: 1 / -1; }
.label { font-size: 0.8rem; font-weight: 500; color: #374151; }
.req { color: var(--tf-color-accent); margin-left: 0.1rem; }

.input,
.select,
.textarea {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  font-size: 0.9rem;
  font-family: inherit;
  background: #fff;
  transition: border-color 0.15s;
}
.input:focus,
.select:focus,
.textarea:focus {
  outline: none;
  border-color: var(--tf-color-primary);
  box-shadow: 0 0 0 2px rgba(38, 183, 188, 0.15);
}
.textarea { resize: vertical; }

/* ── 明細列表 ── */
.expform__lines {
  border: 1px solid var(--tf-color-border);
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 0.75rem;
}

.expform__line-header,
.expform__line-row {
  display: grid;
  grid-template-columns: 130px 1fr 1.4fr 120px 64px;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  align-items: center;
}
.expform__col-title { font-size: 0.85rem; color: #475569; padding-left: 0.15rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

.expform__line-header {
  background: var(--tf-color-primary);
  color: #fff;
  font-size: 0.8rem;
  font-weight: 600;
}

.expform__line-row {
  border-top: 1px solid var(--tf-color-border);
  background: #fff;
}
.expform__line-row:hover { background: #f8faf8; }

.expform__col-account,
.expform__col-summary,
.expform__col-price { display: flex; flex-direction: column; }

.expform__col-action { display: flex; justify-content: center; }

/* 合計列 */
.expform__line-total {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: 1.5rem;
  padding: 0.5rem 0.75rem;
  border-top: 2px solid var(--tf-color-primary);
  background: rgba(38, 183, 188, 0.04);
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--tf-color-primary-dark);
}
.expform__total-value {
  font-size: 1rem;
  color: var(--tf-color-primary-dark);
  font-weight: 700;
}

/* 新增明細按鈕 */
.expform__add-line { width: 100%; justify-content: center; }

/* ── 錯誤訊息 ── */
.form-msg--error {
  background: #fbeaea;
  color: #c0392b;
  border: 1px solid #f5c6c6;
  border-radius: 4px;
  padding: 0.6rem 0.9rem;
  font-size: 0.875rem;
  margin-bottom: 1rem;
}

/* ── 操作列（取消 + 主操作，右對齊） ── */
.expform__actions { display: flex; justify-content: flex-end; gap: 0.5rem; margin-bottom: 2rem; }
.expform__actions .btn { padding: 0.55rem 1.5rem; }

/* ── 按鈕 ── */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; text-decoration: none; transition: opacity 0.15s, background 0.15s; white-space: nowrap; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: rgba(38, 183, 188, 0.06); }
.btn--danger-ghost { background: transparent; color: #ef4444; border-color: #fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background: #fef2f2; }
</style>

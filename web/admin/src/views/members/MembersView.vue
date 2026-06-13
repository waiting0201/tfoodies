<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

// ─── Types ───────────────────────────────────────────────────────────────────

interface MemberRow {
  id: string
  ismember: number        // 1=會員, 2=客戶
  city: string | null
  name: string
  gender: number | null   // 0=女生, 1=男生
  mobile: string
  email: string
  isEnable: boolean
}

interface MemberDetail {
  id: string
  name: string
  mobile: string
  email: string
  gender: number | null
  birthday: string | null
  ismember: number
  isMember: boolean
  isEnable: boolean
  isAgent: boolean
  agentDiscount: number
  zipcodeId: number | null
  address: string | null
  note: string | null
  createdAt: string
  totalOrders: number
  totalSpend: number
  recentOrders: unknown[]
  city?: string | null
  area?: string | null
}

interface MembersPage {
  items: MemberRow[]
  total: number
  page: number
  pageSize: number
  totalPages: number
  totalCount: number
}

interface ZipcodeArea {
  zipcodeId: number
  area: string
  zipcode: string
}

type PanelMode = 'add' | 'edit' | null

// ─── List state ──────────────────────────────────────────────────────────────

const filters = reactive({ keyword: '' })
const page = ref(1)
const pageSize = 20

const data = ref<MembersPage | null>(null)
const loading = ref(false)
const error = ref('')

// ─── Row expand (detail) ─────────────────────────────────────────────────────

const expandedId = ref<string | null>(null)
const detailMap = reactive<Record<string, MemberDetail>>({})
const detailLoading = ref<string | null>(null)
const detailError = ref<string | null>(null)

// ─── Disable (soft-delete) ───────────────────────────────────────────────────

const disableTarget = ref<MemberRow | null>(null)
const disableError = ref('')
const disabling = ref(false)

// ─── Panel (add / edit) ──────────────────────────────────────────────────────

const panelMode = ref<PanelMode>(null)
const panelEditId = ref<string | null>(null)
const panelSaving = ref(false)
const panelError = ref('')
const mobileError = ref('')

const form = reactive({
  name: '',
  mobile: '',
  password: '',
  gender: 1 as number | null,     // default 男生(1)
  birthday: '',
  email: '',
  ismember: 2 as number,          // default 客戶(2)
  note: '',
  // Address cascade
  city: '',
  areaId: '' as string | number,
  zipcodeId: null as number | null,
  address: '',
  // Edit-only
  isEnable: true,
})

// ─── Address cascade ─────────────────────────────────────────────────────────

const cities = ref<string[]>([])
const areas = ref<ZipcodeArea[]>([])
const citiesLoading = ref(false)
const areasLoading = ref(false)
const showZipcodeHint = ref(false)  // edit mode hint

async function loadCities() {
  citiesLoading.value = true
  try {
    const res = await apiFetch<{ cities: string[] }>('/admin/zipcodes/cities')
    cities.value = res.cities
  } catch {
    cities.value = []
  } finally {
    citiesLoading.value = false
  }
}

async function onCityChange() {
  form.areaId = ''
  form.zipcodeId = null
  areas.value = []
  if (!form.city) return
  areasLoading.value = true
  try {
    const res = await apiFetch<{ areas: ZipcodeArea[] }>(`/admin/zipcodes/areas?city=${encodeURIComponent(form.city)}`)
    areas.value = res.areas
  } catch {
    areas.value = []
  } finally {
    areasLoading.value = false
  }
}

function onAreaChange() {
  const found = areas.value.find(a => String(a.zipcodeId) === String(form.areaId))
  form.zipcodeId = found ? found.zipcodeId : null
}

// ─── Mobile duplicate check ──────────────────────────────────────────────────

async function checkMobile(): Promise<boolean> {
  if (!form.mobile) return true
  try {
    const params = new URLSearchParams({ mobile: form.mobile })
    if (panelMode.value === 'edit' && panelEditId.value) {
      params.set('excludeId', panelEditId.value)
    }
    const res = await apiFetch<{ valid: boolean }>(`/admin/members/check-mobile?${params}`)
    if (!res.valid) {
      mobileError.value = '手機號碼已存在'
      return false
    }
    mobileError.value = ''
    return true
  } catch {
    mobileError.value = ''
    return true   // server-side will catch it on submit
  }
}

// ─── Load list ───────────────────────────────────────────────────────────────

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

// ─── Row expand detail ───────────────────────────────────────────────────────

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

// ─── Open panels ─────────────────────────────────────────────────────────────

function resetForm() {
  form.name = ''
  form.mobile = ''
  form.password = ''
  form.gender = 1
  form.birthday = ''
  form.email = ''
  form.ismember = 2
  form.note = ''
  form.city = ''
  form.areaId = ''
  form.zipcodeId = null
  form.address = ''
  form.isEnable = true
  areas.value = []
  showZipcodeHint.value = false
  mobileError.value = ''
  panelError.value = ''
}

function openAdd() {
  resetForm()
  panelMode.value = 'add'
  panelEditId.value = null
  if (cities.value.length === 0) loadCities()
}

async function openEdit(id: string) {
  resetForm()
  panelMode.value = 'edit'
  panelEditId.value = id

  if (cities.value.length === 0) await loadCities()

  // Use cached detail if available
  let detail = detailMap[id]
  if (!detail) {
    try {
      detail = await apiFetch<MemberDetail>(`/admin/members/${id}`)
      detailMap[id] = detail
    } catch (e) {
      panelError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入失敗'
      return
    }
  }

  form.name = detail.name
  form.mobile = detail.mobile
  form.email = detail.email ?? ''
  form.gender = detail.gender ?? null
  form.birthday = detail.birthday ? detail.birthday.slice(0, 10) : ''
  form.ismember = detail.ismember
  form.note = detail.note ?? ''
  form.address = detail.address ?? ''
  form.isEnable = detail.isEnable
  form.password = ''

  // 預選縣市 / 區域：detail 帶回 city（用於連動）+ zipcodeId（區域 select 的 value）
  if (detail.city) {
    form.city = detail.city
    areasLoading.value = true
    try {
      const res = await apiFetch<{ areas: ZipcodeArea[] }>(
        `/admin/zipcodes/areas?city=${encodeURIComponent(detail.city)}`)
      areas.value = res.areas
    } catch {
      areas.value = []
    } finally {
      areasLoading.value = false
    }
    if (detail.zipcodeId) {
      form.areaId = detail.zipcodeId
      form.zipcodeId = detail.zipcodeId
    }
  } else if (detail.zipcodeId) {
    // 沒有縣市資訊但有 zipcodeId（理論上不會發生）→ 保留值並提示
    form.zipcodeId = detail.zipcodeId
    showZipcodeHint.value = true
  }
}

function closePanel() {
  panelMode.value = null
  panelEditId.value = null
}

const panelTitle = computed(() => panelMode.value === 'add' ? '新增會員' : '編輯會員')

// ─── Submit ──────────────────────────────────────────────────────────────────

async function savePanel() {
  panelError.value = ''
  mobileError.value = ''

  if (!form.name.trim()) { panelError.value = '請填寫姓名'; return }
  if (!form.mobile.trim()) { panelError.value = '請填寫手機號碼'; return }
  if (panelMode.value === 'add' && !form.password) { panelError.value = '請填寫密碼'; return }

  const mobileOk = await checkMobile()
  if (!mobileOk) return

  panelSaving.value = true
  try {
    if (panelMode.value === 'add') {
      await apiFetch<{ id: string }>('/admin/members', {
        method: 'POST',
        body: JSON.stringify({
          name: form.name,
          mobile: form.mobile,
          password: form.password,
          email: form.email || null,
          gender: form.gender,
          birthday: form.birthday ? new Date(form.birthday).toISOString() : null,
          zipcodeId: form.zipcodeId || null,
          address: form.address || null,
          ismember: form.ismember,
          memo: form.note || null,
        }),
      })
    } else {
      await apiFetch(`/admin/members/${panelEditId.value}`, {
        method: 'PUT',
        body: JSON.stringify({
          name: form.name,
          mobile: form.mobile,
          email: form.email || null,
          gender: form.gender,
          birthday: form.birthday ? new Date(form.birthday).toISOString() : null,
          zipcodeId: form.zipcodeId || null,
          address: form.address || null,
          ismember: form.ismember,
          isEnable: form.isEnable,
          memo: form.note || null,
          password: form.password || null,
        }),
      })
      // Invalidate cached detail so it reloads on next expand
      if (panelEditId.value) delete detailMap[panelEditId.value]
    }
    closePanel()
    await load()
  } catch (e) {
    const msg = (e as ApiError).problem?.detail ?? (e as Error).message ?? '儲存失敗'
    if (msg.includes('手機')) {
      mobileError.value = msg
    } else {
      panelError.value = msg
    }
  } finally {
    panelSaving.value = false
  }
}

// ─── Disable (soft-delete) ───────────────────────────────────────────────────

function askDisable(m: MemberRow) {
  disableTarget.value = m
  disableError.value = ''
}

async function confirmDisable() {
  if (!disableTarget.value) return
  disabling.value = true
  disableError.value = ''
  try {
    await apiFetch(`/admin/members/${disableTarget.value.id}`, { method: 'DELETE' })
    disableTarget.value = null
    // Do NOT remove from list; reload so row shows 關閉
    await load()
    // Refresh cached detail if open
    const id = disableTarget.value
    if (id) delete detailMap[(id as unknown as MemberRow).id]
  } catch (e) {
    disableError.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '操作失敗'
  } finally {
    disabling.value = false
  }
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatDate(s?: string | null): string {
  if (!s) return '—'
  return new Date(s).toLocaleDateString('zh-TW')
}

function genderLabel(g: number | null | undefined): string {
  if (g === 0) return '女生'
  if (g === 1) return '男生'
  return ''
}

function ismemberLabel(v: number): string {
  return v === 1 ? '會員' : '客戶'
}

onMounted(load)
</script>

<template>
  <main class="members">

    <!-- Header -->
    <div class="members__header">
      <h1 class="members__title">會員管理</h1>
      <button class="btn btn--primary" @click="openAdd">+ 新增會員</button>
    </div>

    <!-- Filters -->
    <div class="members__filters">
      <input
        v-model="filters.keyword"
        class="filter-input"
        placeholder="搜尋姓名 / 手機"
        @keyup.enter="search"
      />
      <button class="btn btn--secondary" @click="search">搜尋</button>
    </div>

    <p v-if="error" class="members__error">{{ error }}</p>
    <p v-if="loading" class="members__muted">載入中…</p>

    <template v-if="!loading && data">
      <!-- Table card -->
      <div class="card">
        <div class="members__table-wrap">
          <table class="data-table">
            <thead>
              <tr>
                <th style="width:5rem">型態</th>
                <th style="width:6rem">縣市</th>
                <th>姓名</th>
                <th style="width:5rem">性別</th>
                <th style="width:8rem">電話</th>
                <th>Email</th>
                <th style="width:5rem">開通</th>
                <th class="action-th"></th>
              </tr>
            </thead>
            <tbody>
              <template v-for="m in data.items" :key="m.id">
                <!-- Member row -->
                <tr class="data-table__row" :class="{ 'members__row--expanded': expandedId === m.id }">
                  <td>
                    <span class="badge" :class="m.ismember === 1 ? 'badge--active' : 'badge--client'">
                      {{ ismemberLabel(m.ismember) }}
                    </span>
                  </td>
                  <td>{{ m.city || '' }}</td>
                  <td class="font-semibold">{{ m.name }}</td>
                  <td>{{ genderLabel(m.gender) }}</td>
                  <td class="font-mono">{{ m.mobile }}</td>
                  <td>{{ m.email }}</td>
                  <td>
                    <span class="badge" :class="m.isEnable ? 'badge--active' : 'badge--disabled'">
                      {{ m.isEnable ? '開通' : '關閉' }}
                    </span>
                  </td>
                  <td class="action-cell">
                    <button
                      class="btn btn--sm btn--ghost"
                      @click="toggleDetail(m.id)"
                    >{{ expandedId === m.id ? '收起' : '查看' }}</button>
                    <button
                      class="btn btn--sm btn--ghost"
                      @click="openEdit(m.id)"
                    >編輯</button>
                    <button
                      class="btn btn--sm btn--danger-ghost"
                      @click="askDisable(m)"
                    >停用</button>
                  </td>
                </tr>

                <!-- Expanded detail row -->
                <tr v-if="expandedId === m.id" class="members__detail-row">
                  <td colspan="8" class="members__detail-cell">
                    <div class="detail-panel">
                      <p v-if="detailLoading === m.id" class="members__muted">載入中…</p>
                      <p v-else-if="detailError" class="members__error">{{ detailError }}</p>

                      <template v-else-if="detailMap[m.id]">
                        <div class="members__detail-grid">
                          <div class="members__detail-field">
                            <span class="members__detail-label">會員 ID</span>
                            <span class="members__detail-value font-mono">{{ detailMap[m.id].id }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">姓名</span>
                            <span class="members__detail-value font-semibold">{{ detailMap[m.id].name }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">手機</span>
                            <span class="members__detail-value font-mono">{{ detailMap[m.id].mobile }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">Email</span>
                            <span class="members__detail-value">{{ detailMap[m.id].email || '—' }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">型態</span>
                            <span class="members__detail-value">{{ ismemberLabel(detailMap[m.id].ismember) }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">性別</span>
                            <span class="members__detail-value">{{ genderLabel(detailMap[m.id].gender) || '—' }}</span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">生日</span>
                            <span class="members__detail-value">{{ formatDate(detailMap[m.id].birthday) }}</span>
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
                              {{ detailMap[m.id].totalSpend != null ? `NT$ ${detailMap[m.id].totalSpend.toLocaleString()}` : '—' }}
                            </span>
                          </div>
                          <div class="members__detail-field">
                            <span class="members__detail-label">開通</span>
                            <span class="members__detail-value">
                              <span class="badge" :class="detailMap[m.id].isEnable ? 'badge--active' : 'badge--disabled'">
                                {{ detailMap[m.id].isEnable ? '開通' : '關閉' }}
                              </span>
                            </span>
                          </div>
                          <div class="members__detail-field members__detail-field--full">
                            <span class="members__detail-label">縣市 / 地址</span>
                            <span class="members__detail-value">{{ detailMap[m.id].address || '—' }}</span>
                          </div>
                          <div class="members__detail-field members__detail-field--full">
                            <span class="members__detail-label">備註</span>
                            <span class="members__detail-value">{{ detailMap[m.id].note || '—' }}</span>
                          </div>
                        </div>
                      </template>
                    </div>
                  </td>
                </tr>
              </template>

              <tr v-if="data.items.length === 0">
                <td colspan="8" class="empty-cell">目前沒有會員資料</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Pagination -->
      <div class="members__pagination">
        <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
        <span class="members__page-info">第 {{ page }} 頁（共 {{ data.total }} 筆）</span>
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
          <!-- 姓名 -->
          <div class="form-field">
            <label class="form-field__label">姓名 <span class="required">*</span></label>
            <input v-model="form.name" class="form-field__input" placeholder="姓名" />
          </div>

          <!-- 手機 -->
          <div class="form-field">
            <label class="form-field__label">手機 <span class="required">*</span></label>
            <input
              v-model="form.mobile"
              class="form-field__input"
              :class="{ 'form-field__input--error': mobileError }"
              placeholder="0912345678"
              @blur="checkMobile"
            />
            <span v-if="mobileError" class="form-error">{{ mobileError }}</span>
          </div>

          <!-- 密碼 -->
          <div class="form-field">
            <label class="form-field__label">
              <template v-if="panelMode === 'add'">密碼 <span class="required">*</span></template>
              <template v-else>新密碼（留空則不變更）</template>
            </label>
            <input
              v-model="form.password"
              type="password"
              class="form-field__input"
              placeholder="••••••••"
              autocomplete="new-password"
            />
          </div>

          <!-- 性別 -->
          <div class="form-field">
            <label class="form-field__label">性別</label>
            <div class="radio-group">
              <label class="radio-option">
                <input type="radio" :value="1" v-model="form.gender" /> 男生
              </label>
              <label class="radio-option">
                <input type="radio" :value="0" v-model="form.gender" /> 女生
              </label>
              <label class="radio-option">
                <input type="radio" :value="null" v-model="form.gender" /> 不指定
              </label>
            </div>
          </div>

          <!-- 生日 -->
          <div class="form-field">
            <label class="form-field__label">生日</label>
            <input v-model="form.birthday" type="date" class="form-field__input" />
          </div>

          <!-- Email -->
          <div class="form-field">
            <label class="form-field__label">Email</label>
            <input v-model="form.email" type="email" class="form-field__input" placeholder="email@example.com" />
          </div>

          <!-- 地址 cascade -->
          <div class="form-field">
            <label class="form-field__label">縣市</label>
            <select v-model="form.city" class="form-field__input" @change="onCityChange" :disabled="citiesLoading">
              <option value="">-- 選擇縣市 --</option>
              <option v-for="c in cities" :key="c" :value="c">{{ c }}</option>
            </select>
          </div>

          <div class="form-field">
            <label class="form-field__label">鄉鎮市區</label>
            <select
              v-model="form.areaId"
              class="form-field__input"
              @change="onAreaChange"
              :disabled="!form.city || areasLoading"
            >
              <option value="">-- 選擇鄉鎮市區 --</option>
              <option v-for="a in areas" :key="a.zipcodeId" :value="a.zipcodeId">
                {{ a.zipcode }} {{ a.area }}
              </option>
            </select>
            <span v-if="panelMode === 'edit' && showZipcodeHint && !form.city" class="form-hint">
              目前地區已設定，重新選擇縣市可更新
            </span>
          </div>

          <div class="form-field">
            <label class="form-field__label">詳細地址</label>
            <input v-model="form.address" class="form-field__input" placeholder="路/街/巷/弄/號" />
          </div>

          <!-- 型態 -->
          <div class="form-field">
            <label class="form-field__label">型態</label>
            <div class="radio-group">
              <label class="radio-option">
                <input type="radio" :value="2" v-model="form.ismember" /> 客戶
              </label>
              <label class="radio-option">
                <input type="radio" :value="1" v-model="form.ismember" /> 會員
              </label>
            </div>
          </div>

          <!-- 開通（edit only） -->
          <div v-if="panelMode === 'edit'" class="form-field">
            <label class="form-field__label">開通</label>
            <div class="radio-group">
              <label class="radio-option">
                <input type="radio" :value="true" v-model="form.isEnable" /> 是
              </label>
              <label class="radio-option">
                <input type="radio" :value="false" v-model="form.isEnable" /> 否
              </label>
            </div>
          </div>

          <!-- 備註 -->
          <div class="form-field">
            <label class="form-field__label">備註</label>
            <textarea v-model="form.note" class="form-field__input form-field__textarea" rows="3" placeholder="後台備註"></textarea>
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

    <!-- ── Disable confirm modal ──────────────────────────────────────────── -->
    <div v-if="disableTarget" class="modal-overlay" @click.self="disableTarget = null">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認停用</h3>
        </div>
        <div class="modal__body">
          <p>確定要停用會員 <strong>{{ disableTarget.name }}</strong> 嗎？停用後該會員將無法登入。</p>
          <p v-if="disableError" class="form-error" style="margin-top:0.5rem">{{ disableError }}</p>
        </div>
        <div class="modal__footer">
          <button class="btn btn--ghost" @click="disableTarget = null">取消</button>
          <button class="btn btn--danger" :disabled="disabling" @click="confirmDisable">
            {{ disabling ? '處理中…' : '確認停用' }}
          </button>
        </div>
      </div>
    </div>

  </main>
</template>

<style scoped>
.members {}
.members__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.members__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.members__error { color: #dc3545; margin-bottom: 0.75rem; }
.members__muted { color: var(--tf-color-muted); }

/* ── Filters ── */
.members__filters { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; align-items: center; }
.filter-input {
  flex: 1 1 200px;
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  font-size: 0.875rem; font-family: inherit; background: #fff;
  transition: border-color 0.15s;
}
.filter-input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }

/* ── Table ── */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }
.members__table-wrap { overflow-x: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 640px; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 180px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.members__row--expanded td { background: #f0f7f7; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.action-cell { white-space: nowrap; display: flex; gap: 0.35rem; justify-content: flex-end; }
.font-mono { font-family: 'IBM Plex Mono', monospace; font-size: 0.82rem; }
.font-semibold { font-weight: 600; }

/* ── Badges ── */
.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.78rem; font-weight: 500; white-space: nowrap; }
.badge--active   { background: #dcfce7; color: #166534; }
.badge--disabled { background: #f1f5f9; color: #64748b; }
.badge--client   { background: #dbeafe; color: #1e40af; }

/* ── Detail expand ── */
.members__detail-row td { padding: 0; border-bottom: 1px solid var(--tf-color-border); }
.members__detail-cell { padding: 0 !important; }
.detail-panel {
  background: rgba(38, 183, 188, 0.04);
  border-left: 3px solid var(--tf-color-primary);
  padding: 1rem 1.25rem;
}
.members__detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 0.75rem 1.5rem;
}
.members__detail-field { display: flex; flex-direction: column; gap: 0.2rem; }
.members__detail-field--full { grid-column: 1 / -1; }
.members__detail-label { font-size: 0.72rem; color: var(--tf-color-muted); text-transform: uppercase; letter-spacing: 0.04em; }
.members__detail-value { font-size: 0.875rem; color: #334155; }

/* ── Pagination ── */
.members__pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.members__page-info { font-size: 0.875rem; color: var(--tf-color-muted); }

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
.form-field__label { font-size: 0.82rem; font-weight: 600; color: #475569; }
.required { color: #ef4444; }
.form-field__input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  font-size: 0.875rem; color: #1e293b; background: #fff;
  transition: border-color 0.15s; font-family: inherit;
  width: 100%; box-sizing: border-box;
}
.form-field__input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 3px rgba(38,183,188,0.15); }
.form-field__input--error { border-color: #dc3545; }
.form-field__textarea { resize: vertical; min-height: 4rem; }
.form-error { color: #dc3545; font-size: 0.85rem; }
.form-hint { font-size: 0.8rem; color: var(--tf-color-accent-warm, #eda02f); }

/* ── Radio group ── */
.radio-group { display: flex; flex-wrap: wrap; gap: 0.75rem; padding-top: 0.2rem; }
.radio-option { display: flex; align-items: center; gap: 0.35rem; font-size: 0.875rem; color: #475569; cursor: pointer; }
.radio-option input { accent-color: var(--tf-color-primary); }

/* ── Delete modal ── */
.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15,23,42,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 380px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1.25rem 1.4rem; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }
</style>

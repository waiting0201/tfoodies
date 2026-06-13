<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { apiFetch, ApiError } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────

interface Declaration {
  declarationId: string
  declarationType: string
  declarationDate: string
  soldTarget: string
  createDate: string
  orderCount: number
}

interface DeclarableOrder {
  orderId: string
  orderCode: string
  orderDate: string
  memberName: string
  receiverName: string
  total: number
}

// ─── state ────────────────────────────────────────────────────────────────────

const declarations = ref<Declaration[]>([])
const declarables = ref<DeclarableOrder[]>([])
const loadingDecl = ref(false)
const loadingDecl2 = ref(false)
const errorDecl = ref('')
const errorDecl2 = ref('')

// ─── helpers ──────────────────────────────────────────────────────────────────

function errMsg(e: unknown, fallback: string) {
  const ae = e as ApiError
  if (ae.problem?.status === 501) return '此功能尚未開放（API 尚未實作）'
  return ae.problem?.detail ?? (e as Error).message ?? fallback
}

function fmtDate(d: string) {
  return d ? d.slice(0, 10) : '—'
}

function fmtMoney(n: number) {
  return n?.toLocaleString('zh-TW', { style: 'currency', currency: 'TWD', maximumFractionDigits: 0 }) ?? '—'
}

// ─── data loading ─────────────────────────────────────────────────────────────

async function loadDeclarations() {
  loadingDecl.value = true
  errorDecl.value = ''
  try {
    declarations.value = await apiFetch<Declaration[]>('/admin/declarations')
  } catch (e) {
    errorDecl.value = errMsg(e, '載入報關單失敗')
  } finally {
    loadingDecl.value = false
  }
}

async function loadDeclarables() {
  loadingDecl2.value = true
  errorDecl2.value = ''
  try {
    declarables.value = await apiFetch<DeclarableOrder[]>('/admin/declarations/declarable')
  } catch (e) {
    errorDecl2.value = errMsg(e, '載入待報關訂單失敗')
  } finally {
    loadingDecl2.value = false
  }
}

onMounted(() => {
  loadDeclarations()
  loadDeclarables()
})
</script>

<template>
  <main class="declarations">
    <div class="declarations__header">
      <h1 class="declarations__title">報關管理</h1>
    </div>

    <!-- 說明橫幅 -->
    <div class="declarations__notice">
      <svg style="width:1.1rem;height:1.1rem;flex-shrink:0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
      </svg>
      <span>此模組為報關資料唯讀檢視。報關功能在舊系統中僅為初步實作（stub），目前僅提供資料瀏覽，不支援新增或編輯操作。</span>
    </div>

    <!-- 表格一：已建立報關單 -->
    <div class="declarations__section-header">
      <h2 class="declarations__section-title">已建立報關單</h2>
    </div>

    <p v-if="loadingDecl" class="declarations__muted">載入中…</p>
    <p v-if="errorDecl" class="declarations__error">{{ errorDecl }}</p>

    <div v-if="!loadingDecl" class="card declarations__card">
      <table class="data-table">
        <thead>
          <tr>
            <th>報關類型</th>
            <th style="width:8rem">報關日期</th>
            <th>銷售對象</th>
            <th style="width:8rem">建立日期</th>
            <th style="width:6rem">訂單數</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="d in declarations" :key="d.declarationId" class="data-table__row">
            <td class="font-semibold">{{ d.declarationType || '—' }}</td>
            <td>{{ fmtDate(d.declarationDate) }}</td>
            <td>{{ d.soldTarget || '—' }}</td>
            <td>{{ fmtDate(d.createDate) }}</td>
            <td class="font-mono">{{ d.orderCount }}</td>
          </tr>
          <tr v-if="declarations.length === 0">
            <td colspan="5" class="empty-cell">目前沒有報關單資料</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 表格二：待報關訂單 -->
    <div class="declarations__section-header declarations__section-header--gap">
      <h2 class="declarations__section-title">待報關訂單</h2>
    </div>

    <p v-if="loadingDecl2" class="declarations__muted">載入中…</p>
    <p v-if="errorDecl2" class="declarations__error">{{ errorDecl2 }}</p>

    <div v-if="!loadingDecl2" class="card">
      <div class="declarations__table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>訂單編號</th>
              <th style="width:8rem">訂單日期</th>
              <th>會員姓名</th>
              <th>收件人</th>
              <th style="width:8rem">訂單金額</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="o in declarables" :key="o.orderId" class="data-table__row">
              <td class="font-mono">{{ o.orderCode }}</td>
              <td>{{ fmtDate(o.orderDate) }}</td>
              <td>{{ o.memberName || '—' }}</td>
              <td>{{ o.receiverName || '—' }}</td>
              <td>{{ fmtMoney(o.total) }}</td>
            </tr>
            <tr v-if="declarables.length === 0">
              <td colspan="5" class="empty-cell">目前沒有待報關訂單</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </main>
</template>

<style scoped>
.declarations {}
.declarations__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1rem; }
.declarations__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.declarations__error { color: #dc3545; }
.declarations__muted { color: var(--tf-color-muted); }

/* 說明橫幅 */
.declarations__notice {
  display: flex; align-items: flex-start; gap: 0.6rem;
  background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 6px;
  padding: 0.75rem 1rem; margin-bottom: 1.5rem;
  font-size: 0.875rem; color: #1e40af; line-height: 1.5;
}

/* Section 標題 */
.declarations__section-header { margin-bottom: 0.75rem; }
.declarations__section-header--gap { margin-top: 2rem; }
.declarations__section-title {
  font-size: 1rem; font-weight: 600;
  color: var(--tf-color-primary-dark); margin: 0;
}

.declarations__card { margin-bottom: 0; }
.declarations__table-wrap { overflow-x: auto; }

.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }
.data-table { width: 100%; min-width: 560px; border-collapse: collapse; font-size: 0.875rem; }
.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }
.font-semibold { font-weight: 600; }
</style>

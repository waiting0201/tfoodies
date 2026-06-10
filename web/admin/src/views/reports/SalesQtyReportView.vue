<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { apiFetch } from '../../lib/apiClient'

// ─── types ────────────────────────────────────────────────────────────────────
interface SalesItem {
  productId: string
  name: string
  qty: number
}
interface SalesReport {
  year: number
  month: number
  items: SalesItem[]
}

// ─── helpers ──────────────────────────────────────────────────────────────────
function fmtInt(n: number) {
  return (n ?? 0).toLocaleString('en-US')
}

// ─── state ────────────────────────────────────────────────────────────────────
const now = new Date()
const defaultMonth = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
const salesMonth = ref(defaultMonth)            // 'YYYY-MM'
const loading = ref(false)
const error = ref('')
const data = ref<SalesReport | null>(null)

const hasData = computed(() => !!data.value && data.value.items.length > 0)

async function query() {
  if (!salesMonth.value) {
    error.value = '請選擇訂單月份。'
    return
  }
  const [year, month] = salesMonth.value.split('-')
  loading.value = true
  error.value = ''
  try {
    data.value = await apiFetch<SalesReport>(`/admin/reports/sales?year=${year}&month=${Number(month)}`)
  } catch (e: any) {
    error.value = e.message ?? '查詢失敗'
  } finally {
    loading.value = false
  }
}

onMounted(query)
</script>

<template>
  <main class="rpt">
    <div class="rpt__header">
      <h1 class="rpt__title">銷售量報表</h1>
    </div>

    <div class="rpt__filters">
      <input v-model="salesMonth" type="month" class="filter-input filter-input--month" />
      <button class="btn btn--secondary" :disabled="loading" @click="query">
        {{ loading ? '查詢中…' : '查詢' }}
      </button>
    </div>

    <p v-if="loading" class="rpt__muted">載入中…</p>
    <p v-else-if="error" class="rpt__error">{{ error }}</p>

    <div v-else class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th>產品</th>
            <th class="num-th">數量</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in data?.items ?? []" :key="item.productId" class="data-table__row">
            <td>{{ item.name }}</td>
            <td class="num-cell">{{ fmtInt(item.qty) }}</td>
          </tr>
          <tr v-if="!hasData">
            <td colspan="2" class="empty-cell">查無資料</td>
          </tr>
        </tbody>
      </table>
    </div>
  </main>
</template>

<style scoped>
.rpt {}
.rpt__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.rpt__title  { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.rpt__error  { color: #dc3545; }
.rpt__muted  { color: var(--tf-color-muted); }

/* ── 篩選列 ── */
.rpt__filters { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; align-items: center; }
.filter-input {
  padding: 0.45rem 0.65rem;
  border: 1px solid var(--tf-color-border); border-radius: 4px;
  font-size: 0.875rem; font-family: inherit; background: #fff;
  transition: border-color 0.15s;
}
.filter-input:focus { outline: none; border-color: var(--tf-color-primary); box-shadow: 0 0 0 2px rgba(38,183,188,0.15); }
.filter-input--month { flex: 0 0 auto; width: 11rem; }

/* ── 表格卡片 ── */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: hidden; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.data-table th {
  background: var(--tf-color-primary);
  color: #fff; text-align: left;
  padding: 0.65rem 0.75rem;
  font-size: 0.875rem; font-weight: 600; white-space: nowrap;
}
.data-table td {
  padding: 0.65rem 0.9rem;
  border-bottom: 1px solid var(--tf-color-border);
  vertical-align: middle; color: #334155;
}
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.num-th  { text-align: right; }
.num-cell { text-align: right; white-space: nowrap; }

/* ── 按鈕 ── */
.btn {
  display: inline-flex; align-items: center; justify-content: center;
  padding: 0.45rem 1rem;
  border: 1px solid transparent; border-radius: 4px;
  cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit;
  transition: opacity 0.15s, background 0.15s; white-space: nowrap;
}
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn--secondary { background: #e9ecef; color: #495057; border-color: #dee2e6; }
.btn--secondary:hover:not(:disabled) { background: #dee2e6; }
</style>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'

interface PrintDetailLine {
  invoiceDetailId: string
  orderCode: string
  orderDate: string | null
  orderInvoiceCode: string | null
  price: number
  tax: number
  note: string | null
}
interface PrintResponse {
  invoice: {
    invoiceCode: string
    requestDate: string
    incomeId: string | null
    note: string | null
    memberName: string
    memberMobile: string | null
    memberAddress: string | null
  }
  details: PrintDetailLine[]
}

const route = useRoute()
const data = ref<PrintResponse | null>(null)
const loading = ref(true)
const error = ref('')

function fmtDate(d: string | null) { return d ? String(d).slice(0, 10) : '—' }
function money(n: number) { return Number(n || 0).toLocaleString('zh-TW') }

const subtotal = computed(() => (data.value?.details ?? []).reduce((s, d) => s + (d.price || 0), 0))
const taxTotal = computed(() => (data.value?.details ?? []).reduce((s, d) => s + (d.tax || 0), 0))
const grandTotal = computed(() => subtotal.value + taxTotal.value)

function doPrint() { window.print() }

onMounted(async () => {
  try {
    data.value = await apiFetch<PrintResponse>(`/admin/ar-invoices/${route.params.id}`)
    // 載入完成後自動帶出列印對話框（使用者亦可手動按列印）
    setTimeout(() => window.print(), 350)
  } catch (e) {
    error.value = (e as ApiError).problem?.detail ?? (e as Error).message ?? '載入失敗'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="print-page">
    <!-- 工具列（列印時隱藏） -->
    <div class="toolbar no-print">
      <button class="tbtn tbtn--ghost" @click="$router.back()">← 返回</button>
      <button class="tbtn tbtn--primary" :disabled="!data" @click="doPrint">列印 / 另存 PDF</button>
    </div>

    <p v-if="loading" class="state">載入中…</p>
    <p v-else-if="error" class="state state--error">{{ error }}</p>

    <div v-else-if="data" class="sheet">
      <!-- 抬頭 -->
      <header class="sheet__head">
        <div class="seller">
          <div class="seller__logo">食</div>
          <div>
            <div class="seller__name">食在呼 TFoodies</div>
            <div class="seller__sub">食品電商 · 請款單據</div>
          </div>
        </div>
        <div class="doc-meta">
          <h1 class="doc-title">請款單</h1>
          <div class="doc-row"><span>請款單號</span><b class="mono">{{ data.invoice.invoiceCode }}</b></div>
          <div class="doc-row"><span>請款日期</span><b>{{ fmtDate(data.invoice.requestDate) }}</b></div>
          <div class="doc-row">
            <span>收款狀態</span>
            <b :class="data.invoice.incomeId ? 'pill pill--paid' : 'pill pill--unpaid'">
              {{ data.invoice.incomeId ? '已入帳' : '未入帳' }}
            </b>
          </div>
        </div>
      </header>

      <!-- 買方 -->
      <section class="party">
        <div class="party__label">買方</div>
        <div class="party__body">
          <div class="party__name">{{ data.invoice.memberName }}</div>
          <div class="party__line">電話：{{ data.invoice.memberMobile || '—' }}</div>
          <div class="party__line">地址：{{ data.invoice.memberAddress || '—' }}</div>
        </div>
      </section>

      <!-- 明細 -->
      <table class="lines">
        <thead>
          <tr>
            <th style="width:4%">#</th>
            <th>訂單單號</th>
            <th style="width:13%">訂單日期</th>
            <th style="width:16%">發票號碼</th>
            <th style="width:13%;text-align:right">未稅</th>
            <th style="width:11%;text-align:right">稅額</th>
            <th style="width:14%;text-align:right">金額</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(d, i) in data.details" :key="d.invoiceDetailId">
            <td class="c">{{ i + 1 }}</td>
            <td class="mono">{{ d.orderCode }}</td>
            <td class="c">{{ fmtDate(d.orderDate) }}</td>
            <td class="mono">{{ d.orderInvoiceCode || '—' }}</td>
            <td class="r">{{ money(d.price) }}</td>
            <td class="r">{{ money(d.tax) }}</td>
            <td class="r b">{{ money(d.price + d.tax) }}</td>
          </tr>
          <tr v-if="data.details.length === 0">
            <td colspan="7" class="c muted" style="padding:1.5rem">無明細資料</td>
          </tr>
        </tbody>
      </table>

      <!-- 合計 -->
      <div class="totals">
        <div class="totals__row"><span>未稅合計</span><b>NT$ {{ money(subtotal) }}</b></div>
        <div class="totals__row"><span>營業稅 5%</span><b>NT$ {{ money(taxTotal) }}</b></div>
        <div class="totals__row totals__row--grand"><span>應收總計</span><b>NT$ {{ money(grandTotal) }}</b></div>
      </div>

      <p v-if="data.invoice.note" class="note">備註：{{ data.invoice.note }}</p>

      <footer class="sheet__foot">
        <div class="sign">賣方簽章</div>
        <div class="sign">買方簽章</div>
      </footer>
    </div>
  </div>
</template>

<style scoped>
.print-page { background: #f1f5f9; min-height: 100vh; padding: 1.5rem; }
.state { text-align: center; color: #64748b; padding: 3rem; }
.state--error { color: #dc3545; }

/* 工具列 */
.toolbar { max-width: 800px; margin: 0 auto 1rem; display: flex; justify-content: flex-end; gap: 0.5rem; }
.tbtn { padding: 0.5rem 1.1rem; border-radius: 4px; border: 1px solid transparent; cursor: pointer; font-size: 0.875rem; font-weight: 500; font-family: inherit; }
.tbtn--primary { background: #26b7bc; color: #fff; border-color: #26b7bc; }
.tbtn--primary:hover:not(:disabled) { background: #1d8e92; }
.tbtn--primary:disabled { opacity: 0.5; cursor: not-allowed; }
.tbtn--ghost { background: #fff; color: #475569; border-color: #cbd5e1; }
.tbtn--ghost:hover { background: #f8fafc; }

/* A4 紙張 */
.sheet {
  max-width: 800px; margin: 0 auto; background: #fff; color: #1e293b;
  padding: 2.5rem 2.75rem; box-shadow: 0 4px 24px rgba(0,0,0,0.1);
  font-size: 0.875rem; line-height: 1.5;
}

.sheet__head { display: flex; justify-content: space-between; align-items: flex-start; padding-bottom: 1.25rem; border-bottom: 2px solid #26b7bc; }
.seller { display: flex; align-items: center; gap: 0.75rem; }
.seller__logo { width: 44px; height: 44px; border-radius: 8px; background: #26b7bc; color: #fff; font-weight: 700; display: flex; align-items: center; justify-content: center; font-size: 1.25rem; }
.seller__name { font-size: 1.15rem; font-weight: 700; color: #156467; }
.seller__sub { font-size: 0.78rem; color: #64748b; }

.doc-meta { text-align: right; min-width: 220px; }
.doc-title { margin: 0 0 0.5rem; font-size: 1.5rem; letter-spacing: 0.3em; color: #156467; }
.doc-row { display: flex; justify-content: space-between; gap: 1rem; font-size: 0.82rem; padding: 0.1rem 0; }
.doc-row span { color: #64748b; }

.party { margin: 1.25rem 0; display: flex; gap: 0.75rem; }
.party__label { background: #156467; color: #fff; padding: 0.35rem 0.75rem; border-radius: 4px; font-size: 0.8rem; align-self: flex-start; }
.party__name { font-weight: 700; font-size: 1rem; }
.party__line { font-size: 0.82rem; color: #475569; }

.lines { width: 100%; border-collapse: collapse; margin-top: 0.5rem; font-size: 0.82rem; }
.lines th { background: #f0fafa; color: #156467; border-top: 1px solid #cbd5e1; border-bottom: 1px solid #cbd5e1; padding: 0.5rem 0.6rem; text-align: left; font-weight: 600; }
.lines td { border-bottom: 1px solid #e2e8f0; padding: 0.45rem 0.6rem; vertical-align: top; }
.mono { font-family: 'IBM Plex Mono', ui-monospace, monospace; }
.c { text-align: center; }
.r { text-align: right; }
.b { font-weight: 700; }
.muted { color: #94a3b8; }

.totals { margin-top: 1rem; margin-left: auto; width: 280px; }
.totals__row { display: flex; justify-content: space-between; padding: 0.35rem 0.25rem; font-size: 0.85rem; }
.totals__row span { color: #64748b; }
.totals__row--grand { border-top: 2px solid #26b7bc; margin-top: 0.25rem; padding-top: 0.6rem; font-size: 1.05rem; }
.totals__row--grand b { color: #156467; }

.note { margin-top: 1.5rem; font-size: 0.82rem; color: #475569; background: #f8fafc; border-left: 3px solid #26b7bc; padding: 0.6rem 0.9rem; }

.sheet__foot { display: flex; justify-content: space-around; margin-top: 3rem; padding-top: 1rem; }
.sign { border-top: 1px solid #94a3b8; padding-top: 0.4rem; width: 40%; text-align: center; font-size: 0.8rem; color: #64748b; }

.pill { padding: 0.1em 0.5em; border-radius: 3px; font-size: 0.78rem; }
.pill--paid { background: #d4edda; color: #155724; }
.pill--unpaid { background: #fff3cd; color: #856404; }

/* ── 列印樣式 ── */
@media print {
  .print-page { background: #fff; padding: 0; }
  .no-print { display: none !important; }
  .sheet { max-width: none; box-shadow: none; padding: 0.5cm 1cm; margin: 0; }
  @page { size: A4; margin: 1cm; }
}
</style>

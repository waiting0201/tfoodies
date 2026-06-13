<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch } from '../../../lib/apiClient'

interface NewsItem {
  newsId: string
  title: string
  publishDate: string
}

interface PagedResult {
  items: NewsItem[]
  totalCount: number
  page: number
  pageSize: number
}

const router = useRouter()
const items = ref<NewsItem[]>([])
const loading = ref(false)
const error = ref('')
const page = ref(1)
const pageSize = 20
const total = ref(0)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const data = await apiFetch<PagedResult>(`/admin/cms/news?page=${page.value}&pageSize=${pageSize}`)
    items.value = data.items
    total.value = data.totalCount
  } catch (e: any) {
    error.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

function prevPage() {
  if (page.value <= 1) return
  page.value--
  load()
}

function nextPage() {
  if (page.value * pageSize >= total.value) return
  page.value++
  load()
}

async function deleteNews(id: string, title: string) {
  if (!confirm(`確定要刪除「${title}」嗎？`)) return
  try {
    await apiFetch(`/admin/cms/news/${id}`, { method: 'DELETE' })
    await load()
  } catch (e: any) {
    error.value = e.message ?? '刪除失敗'
  }
}

function formatDate(d: string) {
  if (!d) return '—'
  return d.slice(0, 10)
}

onMounted(load)
</script>

<template>
  <main class="news">
    <div class="news__header">
      <h1 class="news__title">最新消息</h1>
      <button class="btn btn--primary" @click="router.push('/admin/web/news/new')">
        + 新增
      </button>
    </div>

    <p v-if="loading" class="news__muted">載入中…</p>
    <p v-else-if="error" class="news__error">{{ error }}</p>

    <template v-else>
      <div class="card">
        <table class="data-table">
          <thead>
            <tr>
              <th>標題</th>
              <th style="width:120px">發布日期</th>
              <th class="action-th">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="items.length === 0">
              <td colspan="3" class="empty-cell">目前沒有消息</td>
            </tr>
            <tr v-for="n in items" :key="n.newsId" class="data-table__row">
              <td>{{ n.title }}</td>
              <td>{{ formatDate(n.publishDate) }}</td>
              <td>
                <div class="action-cell">
                  <button
                    class="btn btn--ghost btn--sm"
                    @click="router.push(`/admin/web/news/${n.newsId}/edit`)"
                  >編輯</button>
                  <button
                    class="btn btn--danger-ghost btn--sm"
                    @click="deleteNews(n.newsId, n.title)"
                  >刪除</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="news__pagination">
        <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
        <span class="news__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
        <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
      </div>
    </template>
  </main>
</template>

<style scoped>
.news {}
.news__header { display:flex; align-items:center; justify-content:space-between; margin-bottom:1.25rem; }
.news__title { font-family:var(--tf-font-heading); color:var(--tf-color-primary-dark); margin:0; }
.news__error { color:#dc3545; }
.news__muted { color:var(--tf-color-muted); }

.card { background:#fff; border-radius:10px; border:1px solid var(--tf-color-border); overflow:auto; }
.data-table { width:100%; border-collapse:collapse; font-size:0.875rem; min-width: 720px; }.data-table th { background:var(--tf-color-primary); color:#fff; text-align:left; padding:0.65rem 0.75rem; font-size:0.875rem; font-weight:600; white-space:nowrap; }
.action-th { width:130px; }
.data-table td { padding:0.65rem 0.9rem; border-bottom:1px solid var(--tf-color-border); vertical-align:middle; color:#334155; }
.data-table__row:last-child td { border-bottom:none; }
.data-table__row:hover td { background:#f8faf8; }
.empty-cell { text-align:center; color:var(--tf-color-muted); padding:2.5rem; }
.action-cell { white-space:nowrap; text-align:right; display:flex; gap:0.35rem; justify-content:flex-end; }

.news__pagination { display:flex; align-items:center; gap:0.75rem; justify-content:flex-end; margin-top:1rem; }
.news__page-info { font-size:0.875rem; color:var(--tf-color-muted); }

.btn { display:inline-flex; align-items:center; justify-content:center; padding:0.45rem 1rem; border:1px solid transparent; border-radius:4px; cursor:pointer; font-size:0.875rem; font-weight:500; transition:opacity 0.15s,background 0.15s; white-space:nowrap; font-family:inherit; }
.btn:disabled { opacity:0.45; cursor:not-allowed; }
.btn--sm { padding:0.25rem 0.6rem; font-size:0.8rem; }
.btn--primary { background:var(--tf-color-primary); color:#fff; border-color:var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background:var(--tf-color-primary-dark); border-color:var(--tf-color-primary-dark); }
.btn--ghost { background:transparent; color:var(--tf-color-primary); border-color:var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background:rgba(38,183,188,0.06); }
.btn--danger-ghost { background:transparent; color:#ef4444; border-color:#fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background:#fef2f2; }
</style>

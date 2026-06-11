<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch } from '../../../lib/apiClient'
import { toBlobUrl } from '../../../lib/blobUrl'

interface Recipe {
  recipeId: string
  title: string
  photo?: string
  duration: number
  portion: number
  sort: number
}

interface PagedResult {
  items: Recipe[]
  totalCount: number
  page: number
  pageSize: number
}

const router = useRouter()
const items = ref<Recipe[]>([])
const loading = ref(false)
const error = ref('')
const page = ref(1)
const pageSize = 20
const total = ref(0)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const data = await apiFetch<PagedResult>(`/admin/cms/recipes?page=${page.value}&pageSize=${pageSize}`)
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

async function deleteRecipe(id: string, title: string) {
  if (!confirm(`確定要刪除「${title}」嗎？`)) return
  try {
    await apiFetch(`/admin/cms/recipes/${id}`, { method: 'DELETE' })
    await load()
  } catch (e: any) {
    error.value = e.message ?? '刪除失敗'
  }
}

onMounted(load)
</script>

<template>
  <main class="recipes">
    <div class="recipes__header">
      <h1 class="recipes__title">食譜管理</h1>
      <button class="btn btn--primary" @click="router.push('/admin/web/recipes/new')">
        + 新增
      </button>
    </div>

    <p v-if="loading" class="recipes__muted">載入中…</p>
    <p v-else-if="error" class="recipes__error">{{ error }}</p>

    <template v-else>
      <div class="card">
        <table class="data-table">
          <thead>
            <tr>
              <th style="width:60px">排序</th>
              <th style="width:80px">圖片</th>
              <th>標題</th>
              <th style="width:80px">時間(分)</th>
              <th style="width:70px">份量</th>
              <th class="action-th">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="items.length === 0">
              <td colspan="6" class="empty-cell">目前沒有食譜</td>
            </tr>
            <tr v-for="r in items" :key="r.recipeId" class="data-table__row">
              <td>{{ r.sort }}</td>
              <td>
                <img v-if="r.photo" :src="toBlobUrl(r.photo)" :alt="r.title" class="thumb" />
                <span v-else class="text-muted">—</span>
              </td>
              <td>{{ r.title }}</td>
              <td>{{ r.duration }}</td>
              <td>{{ r.portion }}</td>
              <td>
                <div class="action-cell">
                  <button
                    class="btn btn--ghost btn--sm"
                    @click="router.push(`/admin/web/recipes/${r.recipeId}/edit`)"
                  >編輯</button>
                  <button
                    class="btn btn--danger-ghost btn--sm"
                    @click="deleteRecipe(r.recipeId, r.title)"
                  >刪除</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="recipes__pagination">
        <button class="btn btn--sm btn--ghost" :disabled="page <= 1" @click="prevPage">上一頁</button>
        <span class="recipes__page-info">第 {{ page }} 頁（共 {{ total }} 筆）</span>
        <button class="btn btn--sm btn--ghost" :disabled="page * pageSize >= total" @click="nextPage">下一頁</button>
      </div>
    </template>
  </main>
</template>

<style scoped>
.recipes {}
.recipes__header { display:flex; align-items:center; justify-content:space-between; margin-bottom:1.25rem; }
.recipes__title { font-family:var(--tf-font-heading); color:var(--tf-color-primary-dark); margin:0; }
.recipes__error { color:#dc3545; }
.recipes__muted { color:var(--tf-color-muted); }

.card { background:#fff; border-radius:10px; border:1px solid var(--tf-color-border); overflow:hidden; }
.data-table { width:100%; border-collapse:collapse; font-size:0.875rem; }
.data-table th { background:var(--tf-color-primary); color:#fff; text-align:left; padding:0.65rem 0.75rem; font-size:0.875rem; font-weight:600; white-space:nowrap; }
.action-th { width:130px; }
.data-table td { padding:0.65rem 0.9rem; border-bottom:1px solid var(--tf-color-border); vertical-align:middle; color:#334155; }
.data-table__row:last-child td { border-bottom:none; }
.data-table__row:hover td { background:#f8faf8; }
.empty-cell { text-align:center; color:var(--tf-color-muted); padding:2.5rem; }
.action-cell { white-space:nowrap; text-align:right; display:flex; gap:0.35rem; justify-content:flex-end; }
.text-muted { color:var(--tf-color-muted); font-size:0.85rem; }
.thumb { width:56px; height:40px; object-fit:cover; border-radius:3px; display:block; }

.recipes__pagination { display:flex; align-items:center; gap:0.75rem; justify-content:flex-end; margin-top:1rem; }
.recipes__page-info { font-size:0.875rem; color:var(--tf-color-muted); }

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

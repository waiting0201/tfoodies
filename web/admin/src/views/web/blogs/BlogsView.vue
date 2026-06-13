<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch } from '../../../lib/apiClient'
import { toBlobUrl } from '../../../lib/blobUrl'

interface Blog {
  blogId: string
  title: string
  photo?: string
  link: string
  sort: number
}

const router = useRouter()
const items = ref<Blog[]>([])
const loading = ref(false)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    items.value = await apiFetch<Blog[]>('/admin/cms/blogs')
  } catch (e: any) {
    error.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

async function deleteBlog(id: string, title: string) {
  if (!confirm(`確定要刪除「${title}」嗎？`)) return
  try {
    await apiFetch(`/admin/cms/blogs/${id}`, { method: 'DELETE' })
    items.value = items.value.filter(b => b.blogId !== id)
  } catch (e: any) {
    error.value = e.message ?? '刪除失敗'
  }
}

onMounted(load)
</script>

<template>
  <main class="blogs">
    <div class="blogs__header">
      <h1 class="blogs__title">部落格管理</h1>
      <button class="btn btn--primary" @click="router.push('/admin/web/blogs/new')">
        + 新增
      </button>
    </div>

    <p v-if="loading" class="blogs__muted">載入中…</p>
    <p v-else-if="error" class="blogs__error">{{ error }}</p>

    <div v-else class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th style="width:60px">排序</th>
            <th style="width:80px">圖片</th>
            <th>標題</th>
            <th>連結</th>
            <th class="action-th">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="items.length === 0">
            <td colspan="5" class="empty-cell">目前沒有部落格</td>
          </tr>
          <tr v-for="b in items" :key="b.blogId" class="data-table__row">
            <td>{{ b.sort }}</td>
            <td>
              <img v-if="b.photo" :src="toBlobUrl(b.photo)" :alt="b.title" class="thumb" />
              <span v-else class="text-muted">—</span>
            </td>
            <td>{{ b.title }}</td>
            <td class="link-cell">
              <a v-if="b.link" :href="b.link" target="_blank" rel="noopener" class="link">{{ b.link }}</a>
              <span v-else class="text-muted">—</span>
            </td>
            <td>
              <div class="action-cell">
                <button
                  class="btn btn--ghost btn--sm"
                  @click="router.push(`/admin/web/blogs/${b.blogId}/edit`)"
                >編輯</button>
                <button
                  class="btn btn--danger-ghost btn--sm"
                  @click="deleteBlog(b.blogId, b.title)"
                >刪除</button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </main>
</template>

<style scoped>
.blogs {}
.blogs__header { display:flex; align-items:center; justify-content:space-between; margin-bottom:1.25rem; }
.blogs__title { font-family:var(--tf-font-heading); color:var(--tf-color-primary-dark); margin:0; }
.blogs__error { color:#dc3545; }
.blogs__muted { color:var(--tf-color-muted); }

.card { background:#fff; border-radius:10px; border:1px solid var(--tf-color-border); overflow:auto; }
.data-table { width:100%; border-collapse:collapse; font-size:0.875rem; min-width: 720px; }.data-table th { background:var(--tf-color-primary); color:#fff; text-align:left; padding:0.65rem 0.75rem; font-size:0.875rem; font-weight:600; white-space:nowrap; }
.action-th { width:130px; }
.data-table td { padding:0.65rem 0.9rem; border-bottom:1px solid var(--tf-color-border); vertical-align:middle; color:#334155; }
.data-table__row:last-child td { border-bottom:none; }
.data-table__row:hover td { background:#f8faf8; }
.empty-cell { text-align:center; color:var(--tf-color-muted); padding:2.5rem; }
.action-cell { white-space:nowrap; text-align:right; display:flex; gap:0.35rem; justify-content:flex-end; }
.text-muted { color:var(--tf-color-muted); font-size:0.85rem; }
.thumb { width:56px; height:40px; object-fit:cover; border-radius:3px; display:block; }
.link-cell { max-width:220px; overflow:hidden; }
.link { color:var(--tf-color-primary); text-decoration:none; font-size:0.8rem; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; display:block; }
.link:hover { text-decoration:underline; }

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

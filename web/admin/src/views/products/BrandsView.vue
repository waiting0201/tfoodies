<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, ApiError } from '../../lib/apiClient'
import { toBlobUrl } from '../../lib/blobUrl'

interface Brand {
  brandid: string
  title: string
  subtitle: string
  logo: string
  sort: number
  isdisplay: number
}

const router = useRouter()

const items = ref<Brand[]>([])
const loading = ref(false)
const error = ref('')

const deleteTarget = ref<Brand | null>(null)
const deleteError = ref('')
const deleting = ref(false)

function errMsg(e: unknown, fallback: string) {
  return (e as ApiError).problem?.detail ?? (e as Error).message ?? fallback
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    items.value = await apiFetch<Brand[]>('/admin/brands')
  } catch (e) {
    error.value = errMsg(e, '載入失敗')
  } finally {
    loading.value = false
  }
}

function askDelete(b: Brand) {
  deleteTarget.value = b
  deleteError.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await apiFetch(`/admin/brands/${deleteTarget.value.brandid}`, { method: 'DELETE' })
    deleteTarget.value = null
    await load()
  } catch (e) {
    deleteError.value = errMsg(e, '刪除失敗')
  } finally {
    deleting.value = false
  }
}

load()
</script>

<template>
  <main class="brands">
    <div class="brands__header">
      <h1 class="brands__title">品牌管理</h1>
      <button class="btn btn--primary" @click="router.push('/admin/brands/new')">+ 新增品牌</button>
    </div>

    <p v-if="loading" class="brands__muted">載入中…</p>
    <p v-if="error" class="brands__error">{{ error }}</p>

    <div v-if="!loading" class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th style="width:4rem">排序</th>
            <th style="width:5rem">Logo</th>
            <th>品牌名稱</th>
            <th>副標題</th>
            <th style="width:6rem">顯示</th>
            <th class="action-th"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="b in items" :key="b.brandid" class="data-table__row">
            <td class="font-mono">{{ b.sort }}</td>
            <td>
              <img v-if="b.logo" :src="toBlobUrl(b.logo)" class="thumb" alt="logo" />
              <span v-else class="text-muted">—</span>
            </td>
            <td class="font-semibold">{{ b.title }}</td>
            <td class="text-muted">{{ b.subtitle || '—' }}</td>
            <td>
              <span class="badge" :class="b.isdisplay === 1 ? 'badge--active' : 'badge--disabled'">
                {{ b.isdisplay === 1 ? '顯示' : '隱藏' }}
              </span>
            </td>
            <td class="action-cell">
              <router-link :to="`/admin/brands/${b.brandid}/photos`" class="btn btn--sm btn--secondary">圖庫</router-link>
              <router-link :to="`/admin/brands/${b.brandid}/edit`" class="btn btn--sm btn--ghost">編輯</router-link>
              <button class="btn btn--sm btn--danger-ghost" @click="askDelete(b)">刪除</button>
            </td>
          </tr>
          <tr v-if="items.length === 0">
            <td colspan="6" class="empty-cell">目前沒有品牌資料</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Delete modal -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget = null">
      <div class="modal">
        <div class="modal__header">
          <h3 class="modal__title">確認刪除品牌</h3>
        </div>
        <div class="modal__body">
          <p>確定要刪除品牌 <strong>{{ deleteTarget.title }}</strong> 嗎？此操作無法復原。</p>
          <p v-if="deleteError" class="form-error">{{ deleteError }}</p>
        </div>
        <div class="modal__footer">
          <button class="btn btn--ghost" @click="deleteTarget = null">取消</button>
          <button class="btn btn--danger" :disabled="deleting" @click="confirmDelete">
            {{ deleting ? '刪除中…' : '確認刪除' }}
          </button>
        </div>
      </div>
    </div>
  </main>
</template>

<style scoped>
.brands {}
.brands__header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1.25rem; }
.brands__title { font-family: var(--tf-font-heading); color: var(--tf-color-primary-dark); margin: 0; }
.brands__error { color: #dc3545; }
.brands__muted { color: var(--tf-color-muted); }

.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.65rem 0.75rem; font-size: 0.875rem; font-weight: 600; white-space: nowrap; }
.action-th { width: 200px; }
.data-table td { padding: 0.65rem 0.9rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; color: #334155; }
.data-table__row:last-child td { border-bottom: none; }
.data-table__row:hover td { background: #f8faf8; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2.5rem; }
.font-mono { font-family: 'IBM Plex Mono', monospace; }
.font-semibold { font-weight: 600; }
.text-muted { color: var(--tf-color-muted); font-size: 0.85rem; }
.action-cell { white-space: nowrap; text-align: right; display: flex; gap: 0.35rem; justify-content: flex-end; }
.thumb { width: 48px; height: 38px; object-fit: cover; border-radius: 3px; display: block; }

.badge { display: inline-block; padding: 0.2em 0.5em; border-radius: 3px; font-size: 0.75rem; font-weight: 600; }
.badge--active { background: #dcfce7; color: #166534; }
.badge--disabled { background: #f1f5f9; color: #64748b; }

.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: all 0.15s; white-space: nowrap; text-decoration: none; font-family: inherit; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
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

.modal-overlay { position: fixed; inset: 0; z-index: 60; background: rgba(15,23,42,0.45); display: flex; align-items: center; justify-content: center; padding: 1rem; }
.modal { background: #fff; border-radius: 12px; box-shadow: 0 20px 60px rgba(0,0,0,0.2); width: 100%; max-width: 380px; }
.modal__header { padding: 1.1rem 1.4rem; border-bottom: 1px solid var(--tf-color-border); }
.modal__title { font-size: 1rem; font-weight: 700; color: #1e293b; margin: 0; }
.modal__body { padding: 1.25rem 1.4rem; }
.modal__footer { display: flex; justify-content: flex-end; gap: 0.5rem; padding: 1rem 1.4rem; border-top: 1px solid var(--tf-color-border); }
.form-error { color: #dc3545; font-size: 0.85rem; }
</style>

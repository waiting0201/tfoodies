<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch } from '../../../lib/apiClient'
import { toBlobUrl } from '../../../lib/blobUrl'

interface EventPhoto {
  eventphotoId: string
  photo: string
  sort: number
}

interface EventDetail {
  eventId: string
  title: string
}

const route = useRoute()
const router = useRouter()

const eventId = route.params.id as string

const eventTitle = ref('')
const photos = ref<EventPhoto[]>([])
const loading = ref(false)
const error = ref('')

// 新增表單
const newPhoto = reactive({ photo: '', sort: 0 })
const adding = ref(false)
const addError = ref('')
const uploading = ref(false)
const uploadError = ref('')

async function loadEvent() {
  try {
    const ev = await apiFetch<EventDetail>(`/admin/cms/events/${eventId}`)
    eventTitle.value = ev.title
  } catch {
    // 無法取得標題時不中斷
  }
}

async function loadPhotos() {
  loading.value = true
  error.value = ''
  try {
    photos.value = await apiFetch<EventPhoto[]>(`/admin/cms/events/${eventId}/photos`)
  } catch (e: any) {
    error.value = e.message ?? '載入失敗'
  } finally {
    loading.value = false
  }
}

async function onFileChange(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  uploading.value = true
  uploadError.value = ''
  try {
    const fd = new FormData()
    fd.append('file', file)
    const res = await apiFetch<{ fileName: string }>('/admin/upload', { method: 'POST', body: fd })
    newPhoto.photo = res.fileName
  } catch (err: any) {
    uploadError.value = err.message ?? '上傳失敗'
  } finally {
    uploading.value = false
    ;(e.target as HTMLInputElement).value = ''
  }
}

async function addPhoto() {
  if (!newPhoto.photo.trim()) {
    addError.value = '請先上傳圖片'
    return
  }
  adding.value = true
  addError.value = ''
  try {
    await apiFetch(`/admin/cms/events/${eventId}/photos`, {
      method: 'POST',
      body: JSON.stringify({ photo: newPhoto.photo, sort: newPhoto.sort }),
    })
    newPhoto.photo = ''
    newPhoto.sort = 0
    await loadPhotos()
  } catch (e: any) {
    addError.value = e.message ?? '新增失敗'
  } finally {
    adding.value = false
  }
}

async function updateSort(p: EventPhoto) {
  try {
    await apiFetch(`/admin/cms/events/${eventId}/photos/${p.eventphotoId}`, {
      method: 'PUT',
      body: JSON.stringify({ photo: p.photo, sort: p.sort }),
    })
  } catch (e: any) {
    error.value = e.message ?? '更新排序失敗'
  }
}

async function deletePhoto(photoId: string) {
  if (!confirm('確定要刪除這張圖片嗎？')) return
  try {
    await apiFetch(`/admin/cms/events/${eventId}/photos/${photoId}`, { method: 'DELETE' })
    photos.value = photos.value.filter(p => p.eventphotoId !== photoId)
  } catch (e: any) {
    error.value = e.message ?? '刪除失敗'
  }
}

onMounted(async () => {
  await loadEvent()
  await loadPhotos()
})
</script>

<template>
  <main class="eventphotos">
    <div class="eventphotos__header">
      <button class="btn btn--ghost btn--sm" @click="router.push('/admin/web/events')">
        &larr; 返回活動清單
      </button>
      <h1 class="eventphotos__title">
        活動圖片管理
        <span v-if="eventTitle" class="eventphotos__subtitle">— {{ eventTitle }}</span>
      </h1>
    </div>

    <!-- 新增圖片表單 -->
    <div class="add-form">
      <h2 class="add-form__title">新增圖片</h2>
      <p v-if="addError" class="form-msg form-msg--error">{{ addError }}</p>
      <div class="add-form__row">
        <div class="form-field form-field--grow">
          <label class="label label--required">圖片</label>
          <div class="upload-area">
            <label class="btn btn--ghost btn--sm upload-btn" :class="{ 'btn--loading': uploading }">
              {{ uploading ? '上傳中…' : '選擇圖片' }}
              <input type="file" accept="image/jpeg,image/png,image/gif,image/webp" class="file-input" :disabled="uploading" @change="onFileChange" />
            </label>
            <span v-if="uploadError" class="upload-error">{{ uploadError }}</span>
          </div>
          <img v-if="newPhoto.photo" :src="toBlobUrl(newPhoto.photo)" class="photo-preview" alt="預覽" />
        </div>
        <div class="form-field form-field--fixed">
          <label class="label">排序</label>
          <input v-model.number="newPhoto.sort" class="input" type="number" min="0" />
        </div>
        <div class="form-field add-form__btn-field">
          <label class="label">&nbsp;</label>
          <button class="btn btn--primary" :disabled="adding || !newPhoto.photo" @click="addPhoto">
            {{ adding ? '新增中…' : '新增' }}
          </button>
        </div>
      </div>
    </div>

    <!-- 圖片清單 -->
    <p v-if="loading" class="eventphotos__muted">載入中…</p>
    <p v-else-if="error" class="eventphotos__error">{{ error }}</p>

    <div v-else class="card">
      <table class="data-table">
        <thead>
          <tr>
            <th style="width:80px">排序</th>
            <th style="width:100px">圖片預覽</th>
            <th>圖片 URL</th>
            <th class="action-th">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="photos.length === 0">
            <td colspan="4" class="empty-cell">目前沒有圖片</td>
          </tr>
          <tr v-for="p in photos" :key="p.eventphotoId" class="data-table__row">
            <td>
              <input
                v-model.number="p.sort"
                class="input input--sort"
                type="number"
                min="0"
                @change="updateSort(p)"
              />
            </td>
            <td>
              <img v-if="p.photo" :src="toBlobUrl(p.photo)" alt="活動圖片" class="thumb-lg" />
            </td>
            <td class="url-cell">{{ p.photo }}</td>
            <td>
              <div class="action-cell">
                <button
                  class="btn btn--danger-ghost btn--sm"
                  @click="deletePhoto(p.eventphotoId)"
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
.eventphotos {}
.eventphotos__header { display:flex; align-items:center; gap:1rem; margin-bottom:1.5rem; flex-wrap:wrap; }
.eventphotos__title { margin:0; font-family:var(--tf-font-heading,inherit); color:var(--tf-color-primary-dark); font-size:1.5rem; }
.eventphotos__subtitle { font-size:1rem; font-weight:400; color:var(--tf-color-muted); }
.eventphotos__error { color:#dc3545; }
.eventphotos__muted { color:var(--tf-color-muted); }

.add-form { background:#fff; border:1px solid var(--tf-color-border); border-radius:10px; padding:1.25rem 1.5rem; margin-bottom:1.25rem; }
.add-form__title { font-size:0.875rem; font-weight:700; color:var(--tf-color-primary); margin:0 0 1rem 0; text-transform:uppercase; letter-spacing:0.04em; }
.add-form__row { display:flex; gap:1rem; flex-wrap:wrap; align-items:flex-end; }
.add-form__btn-field { flex-shrink:0; }

.card { background:#fff; border-radius:10px; border:1px solid var(--tf-color-border); overflow:hidden; }
.data-table { width:100%; border-collapse:collapse; font-size:0.875rem; }
.data-table th { background:var(--tf-color-primary); color:#fff; text-align:left; padding:0.65rem 0.75rem; font-size:0.875rem; font-weight:600; white-space:nowrap; }
.action-th { width:90px; }
.data-table td { padding:0.65rem 0.9rem; border-bottom:1px solid var(--tf-color-border); vertical-align:middle; color:#334155; }
.data-table__row:last-child td { border-bottom:none; }
.data-table__row:hover td { background:#f8faf8; }
.empty-cell { text-align:center; color:var(--tf-color-muted); padding:2.5rem; }
.action-cell { white-space:nowrap; text-align:right; display:flex; gap:0.35rem; justify-content:flex-end; }
.url-cell { font-size:0.8rem; color:var(--tf-color-muted); word-break:break-all; }

.thumb-lg { width:72px; height:52px; object-fit:cover; border-radius:3px; display:block; }
.input--sort { width:72px; }

.form-field { display:flex; flex-direction:column; gap:0.3rem; min-width:120px; }
.form-field--grow { flex:1; }
.form-field--fixed { width:100px; flex-shrink:0; }
.label { font-size:0.875rem; font-weight:600; color:#444; }
.label--required::after { content:' *'; color:#e74c3c; }
.input { padding:0.45rem 0.65rem; border:1px solid var(--tf-color-border); border-radius:4px; font-size:0.875rem; font-family:inherit; background:#fff; width:100%; box-sizing:border-box; }
.input:focus { outline:none; border-color:var(--tf-color-primary); box-shadow:0 0 0 2px rgba(38,183,188,0.15); }

.form-msg { padding:0.6rem 0.9rem; border-radius:4px; font-size:0.875rem; margin-bottom:1rem; }
.form-msg--error { background:#fde8e8; color:#c0392b; }

.btn { display:inline-flex; align-items:center; justify-content:center; padding:0.45rem 1rem; border:1px solid transparent; border-radius:4px; cursor:pointer; font-size:0.875rem; font-weight:500; transition:opacity 0.15s,background 0.15s; white-space:nowrap; font-family:inherit; }
.btn:disabled { opacity:0.45; cursor:not-allowed; }
.btn--sm { padding:0.25rem 0.6rem; font-size:0.8rem; }
.btn--primary { background:var(--tf-color-primary); color:#fff; border-color:var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background:var(--tf-color-primary-dark); border-color:var(--tf-color-primary-dark); }
.btn--ghost { background:transparent; color:var(--tf-color-primary); border-color:var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background:rgba(38,183,188,0.06); }
.btn--danger-ghost { background:transparent; color:#ef4444; border-color:#fecaca; }
.btn--danger-ghost:hover:not(:disabled) { background:#fef2f2; }

.upload-area { display:flex; align-items:center; gap:0.75rem; margin-bottom:0.5rem; }
.upload-btn { position:relative; overflow:hidden; cursor:pointer; }
.upload-btn.btn--loading { opacity:0.6; pointer-events:none; }
.file-input { position:absolute; inset:0; opacity:0; cursor:pointer; font-size:0; }
.upload-error { color:#c0392b; font-size:0.8rem; }
.photo-preview { max-width:180px; max-height:100px; object-fit:cover; border-radius:4px; border:1px solid var(--tf-color-border); display:block; margin-top:0.5rem; }
</style>

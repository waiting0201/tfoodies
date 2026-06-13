<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch } from '../../lib/apiClient'
import { toBlobUrl } from '../../lib/blobUrl'

// ─── types ────────────────────────────────────────────────────────────────────
interface Banner { bannerId: string; title: string; subtitle?: string; url?: string; photoUrl?: string; style?: number; sort: number }
interface NewsItem { newsId: string; title: string; publishDate: string }
interface Recipe { recipeId: string; title: string; photo?: string; sort?: number }
interface Issue { issueId: string; title: string; photo?: string }
interface Event { eventId: string; title: string; eventDate?: string }
interface Knowledge { knowledgeId: string; question: string; answer: string }

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

type CmsTab = 'banner' | 'news' | 'recipe' | 'issue' | 'event' | 'faq'

// ─── tab state ────────────────────────────────────────────────────────────────
const route  = useRoute()
const router = useRouter()

const activeTab = ref<CmsTab>((route.query.tab as CmsTab) || 'banner')
watch(() => route.query.tab, (tab) => {
  if (tab && tab !== activeTab.value) activeTab.value = tab as CmsTab
})

// ─── generic paged loader state factory ───────────────────────────────────────
function makePagedState<T>() {
  return { items: ref<T[]>([]), loading: ref(false), error: ref(''), page: ref(1), total: ref(0) }
}

// ─── BANNER ───────────────────────────────────────────────────────────────────
const banners = ref<Banner[]>([])
const bannerLoading = ref(false)
const bannerError = ref('')

async function loadBanners() {
  bannerLoading.value = true; bannerError.value = ''
  try {
    const d = await apiFetch<Banner[]>('/admin/cms/banners')
    banners.value = Array.isArray(d) ? d : (d as any).items ?? []
  } catch (e: any) { bannerError.value = e.message ?? '載入失敗' }
  finally { bannerLoading.value = false }
}
async function deleteBanner(id: string) {
  if (!confirm('確定刪除此輪播？')) return
  try { await apiFetch(`/admin/cms/banners/${id}`, { method: 'DELETE' }); await loadBanners() }
  catch (e: any) { bannerError.value = e.message ?? '刪除失敗' }
}

// ─── NEWS ─────────────────────────────────────────────────────────────────────
const { items: newsItems, loading: newsLoading, error: newsError, page: newsPage, total: newsTotal } = makePagedState<NewsItem>()
const NEWS_PAGE_SIZE = 20

async function loadNews() {
  newsLoading.value = true; newsError.value = ''
  try {
    const d = await apiFetch<PagedResult<NewsItem>>(`/admin/cms/news?page=${newsPage.value}&pageSize=${NEWS_PAGE_SIZE}`)
    newsItems.value = Array.isArray(d) ? d : (d as any).items ?? []
    newsTotal.value = Array.isArray(d) ? d.length : (d as any).totalCount ?? 0
  } catch (e: any) { newsError.value = e.message ?? '載入失敗' }
  finally { newsLoading.value = false }
}
async function deleteNews(id: string) {
  if (!confirm('確定刪除此消息？')) return
  try { await apiFetch(`/admin/cms/news/${id}`, { method: 'DELETE' }); await loadNews() }
  catch (e: any) { newsError.value = e.message ?? '刪除失敗' }
}
watch(newsPage, loadNews)

// ─── RECIPE ───────────────────────────────────────────────────────────────────
const { items: recipeItems, loading: recipeLoading, error: recipeError, page: recipePage, total: recipeTotal } = makePagedState<Recipe>()

async function loadRecipes() {
  recipeLoading.value = true; recipeError.value = ''
  try {
    const d = await apiFetch<PagedResult<Recipe>>(`/admin/cms/recipes?page=${recipePage.value}&pageSize=20`)
    recipeItems.value = Array.isArray(d) ? d : (d as any).items ?? []
    recipeTotal.value = Array.isArray(d) ? d.length : (d as any).totalCount ?? 0
  } catch (e: any) { recipeError.value = e.message ?? '載入失敗' }
  finally { recipeLoading.value = false }
}
async function deleteRecipe(id: string) {
  if (!confirm('確定刪除此食譜？')) return
  try { await apiFetch(`/admin/cms/recipes/${id}`, { method: 'DELETE' }); await loadRecipes() }
  catch (e: any) { recipeError.value = e.message ?? '刪除失敗' }
}
watch(recipePage, loadRecipes)

// ─── ISSUE ────────────────────────────────────────────────────────────────────
const { items: issueItems, loading: issueLoading, error: issueError, page: issuePage } = makePagedState<Issue>()

async function loadIssues() {
  issueLoading.value = true; issueError.value = ''
  try {
    const d = await apiFetch<PagedResult<Issue>>(`/admin/cms/issues?page=${issuePage.value}&pageSize=20`)
    issueItems.value = Array.isArray(d) ? d : (d as any).items ?? []
  } catch (e: any) { issueError.value = e.message ?? '載入失敗' }
  finally { issueLoading.value = false }
}
async function deleteIssue(id: string) {
  if (!confirm('確定刪除此特集？')) return
  try { await apiFetch(`/admin/cms/issues/${id}`, { method: 'DELETE' }); await loadIssues() }
  catch (e: any) { issueError.value = e.message ?? '刪除失敗' }
}
watch(issuePage, loadIssues)

// ─── EVENT ────────────────────────────────────────────────────────────────────
const { items: eventItems, loading: eventLoading, error: eventError, page: eventPage } = makePagedState<Event>()

async function loadEvents() {
  eventLoading.value = true; eventError.value = ''
  try {
    const d = await apiFetch<PagedResult<Event>>(`/admin/cms/events?page=${eventPage.value}&pageSize=20`)
    eventItems.value = Array.isArray(d) ? d : (d as any).items ?? []
  } catch (e: any) { eventError.value = e.message ?? '載入失敗' }
  finally { eventLoading.value = false }
}
async function deleteEvent(id: string) {
  if (!confirm('確定刪除此活動？')) return
  try { await apiFetch(`/admin/cms/events/${id}`, { method: 'DELETE' }); await loadEvents() }
  catch (e: any) { eventError.value = e.message ?? '刪除失敗' }
}
watch(eventPage, loadEvents)

// ─── FAQ ──────────────────────────────────────────────────────────────────────
const { items: faqItems, loading: faqLoading, error: faqError, page: faqPage } = makePagedState<Knowledge>()
const faqOpenId = ref<string | null>(null)

async function loadFaq() {
  faqLoading.value = true; faqError.value = ''
  try {
    const d = await apiFetch<PagedResult<Knowledge>>(`/admin/cms/knowledges?page=${faqPage.value}&pageSize=20`)
    faqItems.value = Array.isArray(d) ? d : (d as any).items ?? []
  } catch (e: any) { faqError.value = e.message ?? '載入失敗' }
  finally { faqLoading.value = false }
}
async function deleteFaq(id: string) {
  if (!confirm('確定刪除此 FAQ？')) return
  try { await apiFetch(`/admin/cms/knowledges/${id}`, { method: 'DELETE' }); await loadFaq() }
  catch (e: any) { faqError.value = e.message ?? '刪除失敗' }
}
watch(faqPage, loadFaq)

// ─── tab switch ───────────────────────────────────────────────────────────────
const LOADERS: Record<CmsTab, () => void> = {
  banner: loadBanners, news: loadNews, recipe: loadRecipes,
  issue: loadIssues, event: loadEvents, faq: loadFaq,
}
function switchTab(t: CmsTab) {
  activeTab.value = t
  router.replace({ query: { tab: t } })
  LOADERS[t]()
}
onMounted(() => LOADERS[activeTab.value]())

// ─── navigation helpers ───────────────────────────────────────────────────────
function goNew(type: CmsTab) {
  router.push({ name: 'cms-new', params: { type } })
}
function goEdit(type: CmsTab, id: string) {
  router.push({ name: 'cms-edit', params: { type, id } })
}
</script>

<template>
  <main class="cms">
    <h1 class="cms__title">內容管理 (CMS)</h1>

    <div class="tabs" role="tablist">
      <button v-for="[key, label] in ([['banner','輪播'],['news','消息'],['recipe','食譜'],['issue','特集'],['event','活動'],['faq','FAQ']] as [CmsTab,string][])"
        :key="key" class="tabs__btn" :class="{ 'tabs__btn--active': activeTab === key }"
        role="tab" :aria-selected="activeTab === key" @click="switchTab(key)">
        {{ label }}
      </button>
    </div>

    <!-- ── BANNER ─────────────────────────────────────────────────────────────── -->
    <section v-if="activeTab === 'banner'" class="tab-panel">
      <div class="toolbar">
        <button class="btn btn--primary" @click="goNew('banner')">+ 新增輪播</button>
      </div>
      <div v-if="bannerLoading" class="state-msg">載入中…</div>
      <div v-else-if="bannerError" class="state-msg state-msg--error">{{ bannerError }}</div>
      <div v-else class="card">
        <table class="data-table">
          <thead><tr><th>排序</th><th>圖片</th><th>標題</th><th></th></tr></thead>
          <tbody>
            <tr v-for="b in banners" :key="b.bannerId">
              <td>{{ b.sort }}</td>
              <td>
                <img v-if="b.photoUrl" :src="toBlobUrl(b.photoUrl)" class="thumb" alt="" />
                <span v-else class="no-photo">—</span>
              </td>
              <td>{{ b.title }}</td>
              <td class="action-cell">
                <button class="btn btn--ghost btn--sm" @click="goEdit('banner', b.bannerId)">編輯</button>
                <button class="btn btn--danger btn--sm" @click="deleteBanner(b.bannerId)">刪除</button>
              </td>
            </tr>
            <tr v-if="banners.length === 0"><td colspan="4" class="empty-cell">尚無輪播</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- ── NEWS ──────────────────────────────────────────────────────────────── -->
    <section v-if="activeTab === 'news'" class="tab-panel">
      <div class="toolbar">
        <button class="btn btn--primary" @click="goNew('news')">+ 新增消息</button>
      </div>
      <div v-if="newsLoading" class="state-msg">載入中…</div>
      <div v-else-if="newsError" class="state-msg state-msg--error">{{ newsError }}</div>
      <div v-else class="card">
        <table class="data-table">
          <thead><tr><th>標題</th><th>發布日期</th><th></th></tr></thead>
          <tbody>
            <tr v-for="n in newsItems" :key="n.newsId">
              <td>{{ n.title }}</td>
              <td>{{ n.publishDate?.slice(0,10) }}</td>
              <td class="action-cell">
                <button class="btn btn--ghost btn--sm" @click="goEdit('news', n.newsId)">編輯</button>
                <button class="btn btn--danger btn--sm" @click="deleteNews(n.newsId)">刪除</button>
              </td>
            </tr>
            <tr v-if="newsItems.length === 0"><td colspan="3" class="empty-cell">尚無消息</td></tr>
          </tbody>
        </table>
      </div>
      <div v-if="newsTotal > NEWS_PAGE_SIZE" class="pagination">
        <button class="btn btn--ghost btn--sm" :disabled="newsPage <= 1" @click="newsPage--">‹</button>
        <span class="pagination__info">第 {{ newsPage }} 頁（共 {{ newsTotal }} 筆）</span>
        <button class="btn btn--ghost btn--sm" :disabled="newsItems.length < NEWS_PAGE_SIZE" @click="newsPage++">›</button>
      </div>
    </section>

    <!-- ── RECIPE ─────────────────────────────────────────────────────────────── -->
    <section v-if="activeTab === 'recipe'" class="tab-panel">
      <div class="toolbar">
        <button class="btn btn--primary" @click="goNew('recipe')">+ 新增食譜</button>
      </div>
      <div v-if="recipeLoading" class="state-msg">載入中…</div>
      <div v-else-if="recipeError" class="state-msg state-msg--error">{{ recipeError }}</div>
      <div v-else class="card">
        <table class="data-table">
          <thead><tr><th>排序</th><th>圖片</th><th>標題</th><th></th></tr></thead>
          <tbody>
            <tr v-for="r in recipeItems" :key="r.recipeId">
              <td>{{ r.sort ?? '—' }}</td>
              <td>
                <img v-if="r.photo" :src="toBlobUrl(r.photo)" class="thumb" alt="" />
                <span v-else class="no-photo">—</span>
              </td>
              <td>{{ r.title }}</td>
              <td class="action-cell">
                <button class="btn btn--ghost btn--sm" @click="goEdit('recipe', r.recipeId)">編輯</button>
                <button class="btn btn--danger btn--sm" @click="deleteRecipe(r.recipeId)">刪除</button>
              </td>
            </tr>
            <tr v-if="recipeItems.length === 0"><td colspan="4" class="empty-cell">尚無食譜</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- ── ISSUE ──────────────────────────────────────────────────────────────── -->
    <section v-if="activeTab === 'issue'" class="tab-panel">
      <div class="toolbar">
        <button class="btn btn--primary" @click="goNew('issue')">+ 新增特集</button>
      </div>
      <div v-if="issueLoading" class="state-msg">載入中…</div>
      <div v-else-if="issueError" class="state-msg state-msg--error">{{ issueError }}</div>
      <div v-else class="card">
        <table class="data-table">
          <thead><tr><th>圖片</th><th>標題</th><th></th></tr></thead>
          <tbody>
            <tr v-for="i in issueItems" :key="i.issueId">
              <td>
                <img v-if="i.photo" :src="toBlobUrl(i.photo)" class="thumb" alt="" />
                <span v-else class="no-photo">—</span>
              </td>
              <td>{{ i.title }}</td>
              <td class="action-cell">
                <button class="btn btn--ghost btn--sm" @click="goEdit('issue', i.issueId)">編輯</button>
                <button class="btn btn--danger btn--sm" @click="deleteIssue(i.issueId)">刪除</button>
              </td>
            </tr>
            <tr v-if="issueItems.length === 0"><td colspan="3" class="empty-cell">尚無特集</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- ── EVENT ──────────────────────────────────────────────────────────────── -->
    <section v-if="activeTab === 'event'" class="tab-panel">
      <div class="toolbar">
        <button class="btn btn--primary" @click="goNew('event')">+ 新增活動</button>
      </div>
      <div v-if="eventLoading" class="state-msg">載入中…</div>
      <div v-else-if="eventError" class="state-msg state-msg--error">{{ eventError }}</div>
      <div v-else class="card">
        <table class="data-table">
          <thead><tr><th>活動日期</th><th>標題</th><th></th></tr></thead>
          <tbody>
            <tr v-for="e in eventItems" :key="e.eventId">
              <td>{{ e.eventDate?.slice(0,10) ?? '—' }}</td>
              <td>{{ e.title }}</td>
              <td class="action-cell">
                <button class="btn btn--ghost btn--sm" @click="goEdit('event', e.eventId)">編輯</button>
                <button class="btn btn--danger btn--sm" @click="deleteEvent(e.eventId)">刪除</button>
              </td>
            </tr>
            <tr v-if="eventItems.length === 0"><td colspan="3" class="empty-cell">尚無活動</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- ── FAQ ───────────────────────────────────────────────────────────────── -->
    <section v-if="activeTab === 'faq'" class="tab-panel">
      <div class="toolbar">
        <button class="btn btn--primary" @click="goNew('faq')">+ 新增 FAQ</button>
      </div>
      <div v-if="faqLoading" class="state-msg">載入中…</div>
      <div v-else-if="faqError" class="state-msg state-msg--error">{{ faqError }}</div>
      <div v-else class="card">
        <div class="accordion">
          <div v-for="k in faqItems" :key="k.knowledgeId" class="accordion__item">
            <button class="accordion__q" @click="faqOpenId = faqOpenId === k.knowledgeId ? null : k.knowledgeId">
              <span>{{ k.question }}</span>
              <span class="accordion__chevron" :class="{ open: faqOpenId === k.knowledgeId }">▾</span>
            </button>
            <div v-if="faqOpenId === k.knowledgeId" class="accordion__a">{{ k.answer }}</div>
            <div class="accordion__actions">
              <button class="btn btn--ghost btn--sm" @click="goEdit('faq', k.knowledgeId)">編輯</button>
              <button class="btn btn--danger btn--sm" @click="deleteFaq(k.knowledgeId)">刪除</button>
            </div>
          </div>
          <div v-if="faqItems.length === 0" class="empty-cell">尚無 FAQ</div>
        </div>
      </div>
    </section>
  </main>
</template>

<style scoped>
.cms { }
.cms__title { font-family: var(--tf-font-heading, inherit); color: var(--tf-color-primary-dark); margin-bottom: 1.5rem; }

/* Tabs */
.tabs { display: flex; gap: 0.25rem; border-bottom: 2px solid var(--tf-color-border); margin-bottom: 1.5rem; flex-wrap: wrap; }
.tabs__btn { padding: 0.45rem 1rem; border: 1px solid transparent; border-bottom: none; background: transparent; cursor: pointer; font-size: 0.875rem; font-family: inherit; color: var(--tf-color-muted); border-radius: 4px 4px 0 0; position: relative; bottom: -2px; transition: background 0.15s; }
.tabs__btn--active { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.tabs__btn:not(.tabs__btn--active):hover { background: #f0f5f1; color: var(--tf-color-primary-dark); }

.tab-panel { animation: fadeIn 0.15s ease; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(4px); } to { opacity: 1; transform: none; } }

/* Toolbar */
.toolbar { display: flex; justify-content: flex-end; margin-bottom: 0.75rem; }

/* Card */
.card { background: #fff; border-radius: 10px; border: 1px solid var(--tf-color-border); overflow: auto; }

/* Table */
.data-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; min-width: 720px; }.data-table th { background: var(--tf-color-primary); color: #fff; text-align: left; padding: 0.6rem 0.75rem; font-weight: 600; font-size: 0.875rem; }
.data-table td { padding: 0.6rem 0.75rem; border-bottom: 1px solid var(--tf-color-border); vertical-align: middle; }
.data-table tbody tr:hover { background: #f8fbf8; }
.action-cell { white-space: nowrap; display: flex; gap: 0.4rem; justify-content: flex-end; align-items: center; }
.empty-cell { text-align: center; color: var(--tf-color-muted); padding: 2rem; }

/* Thumbnail */
.thumb { width: 56px; height: 40px; object-fit: cover; border-radius: 3px; display: block; }
.no-photo { color: var(--tf-color-muted); }

/* Accordion */
.accordion { }
.accordion__item { border-bottom: 1px solid var(--tf-color-border); }
.accordion__item:last-child { border-bottom: none; }
.accordion__q { display: flex; justify-content: space-between; align-items: center; width: 100%; padding: 0.75rem 1rem; background: #f5f9f6; border: none; cursor: pointer; font-size: 0.9rem; text-align: left; font-family: inherit; }
.accordion__q:hover { background: #edf5ef; }
.accordion__chevron { font-size: 1rem; transition: transform 0.2s; display: inline-block; }
.accordion__chevron.open { transform: rotate(180deg); }
.accordion__a { padding: 0.75rem 1rem; font-size: 0.88rem; color: #333; white-space: pre-wrap; background: #fff; }
.accordion__actions { display: flex; gap: 0.4rem; justify-content: flex-end; padding: 0.4rem 0.75rem; background: #fff; border-top: 1px solid #e8f0ea; }

/* Buttons */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 0.45rem 1rem; border: 1px solid transparent; border-radius: 4px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: opacity 0.15s, background 0.15s; white-space: nowrap; font-family: inherit; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn--sm { padding: 0.25rem 0.6rem; font-size: 0.8rem; }
.btn--primary { background: var(--tf-color-primary); color: #fff; border-color: var(--tf-color-primary); }
.btn--primary:hover:not(:disabled) { background: var(--tf-color-primary-dark); border-color: var(--tf-color-primary-dark); }
.btn--ghost { background: transparent; color: var(--tf-color-primary); border-color: var(--tf-color-primary); }
.btn--ghost:hover:not(:disabled) { background: #f0f5f1; }
.btn--danger { background: transparent; color: #c0392b; border-color: #c0392b; }
.btn--danger:hover:not(:disabled) { background: #fde8e8; }

/* Pagination */
.pagination { display: flex; align-items: center; gap: 0.75rem; justify-content: flex-end; margin-top: 1rem; }
.pagination__info { font-size: 0.875rem; color: var(--tf-color-muted); }

.state-msg { padding: 2rem; text-align: center; color: var(--tf-color-muted); }
.state-msg--error { color: #c0392b; }
</style>

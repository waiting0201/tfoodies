<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/Issues.cshtml.
// URL: /Issues  or  /Issues/{p}  with optional ?k= search
const route = useRoute()
const page = computed(() => Number(route.params.p ?? route.query.p ?? 1))
const keyword = computed(() => String(route.query.k ?? ''))
const { data } = await useIssuesData(page.value, keyword.value || undefined)

useSeo(() => ({
  title: '綠誌',
  description: '食在呼 TFoodies 綠誌：分享健康飲食、天然食材知識與品味生活的綠色篇章。',
  type: 'website',
}))

const searchInput = ref(keyword.value)
function submitSearch() {
  if (searchInput.value.trim()) {
    navigateTo({ path: '/Issues', query: { p: 1, k: searchInput.value } })
  }
}
</script>

<template>
  <main id="main">
    <section class="gray-bg searchsection">
      <div class="restrict allpadding">
        <div class="search-column recipe-search">
          <div class="searchall recipe-input">
            <form id="searchform" method="get" action="/Issues">
              <div class="controlgroup">
                <label for="searchall"></label>
                <input
                  v-model="searchInput"
                  type="text" name="k" placeholder="搜尋綠誌"
                  id="recipe-input" class="recipe-input"
                  @keypress.enter="submitSearch"
                >
              </div>
              <a href="javascript:;" class="search-icon" @click="submitSearch"></a>
            </form>
          </div>
        </div>
      </div>
    </section>

    <section class="allpadding clr section">
      <div class="restrict-wide">
        <div class="centered"><h1>綠誌</h1></div>
        <div class="direct-line"></div>
        <div class="three-column clr">
          <a
            v-for="item in data.items" :key="item.issueid"
            :href="`/Issue/${titleToUrlSlug(item.title)}/${data.currentPage}`"
            class="blog-wrap"
          >
            <div class="article-single centered">
              <div class="article-pic"><img :src="data.blobUrl + (item.photo ?? '')" :alt="item.title"></div>
              <div class="article-title">{{ item.title }}</div>
            </div>
          </a>
        </div>
      </div>
    </section>

    <PaginationBar
      :current-page="data.currentPage"
      :total-pages="data.totalPages"
      :build-url="(p) => `/Issues/${p}`"
    />
  </main>
</template>

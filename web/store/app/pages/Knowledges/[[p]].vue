<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/Knowledges.cshtml.
// URL: /Knowledges  or  /Knowledges/{p}  with optional ?k= search
const route = useRoute()
const page = computed(() => Number(route.params.p ?? route.query.p ?? 1))
const keyword = computed(() => String(route.query.k ?? ''))
const { data } = await useKnowledgesData(page.value, keyword.value || undefined)

useSeo(() => ({
  title: '小知識',
  description: '食在呼 TFoodies 小知識：解決你對橄欖油與天然食材的各種疑問。',
  type: 'website',
}))

const searchInput = ref(keyword.value)
function submitSearch() {
  if (searchInput.value.trim()) {
    navigateTo({ path: '/Knowledges', query: { p: 1, k: searchInput.value } })
  }
}
</script>

<template>
  <main id="main">
    <section class="gray-bg searchsection">
      <div class="restrict allpadding">
        <div class="search-column recipe-search">
          <div class="searchall recipe-input">
            <form id="searchform" method="get" action="/Knowledges">
              <div class="controlgroup">
                <label for="searchall"></label>
                <input
                  v-model="searchInput"
                  type="text" name="k" placeholder="搜尋小知識"
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
        <div class="centered">
          <h1>小知識</h1>
          <p class="restrict-small">解決你對橄欖油的各種疑問</p>
        </div>
        <div class="direct-line"></div>
        <div class="three-column clr">
          <a
            v-for="item in data.items" :key="item.knowledgeid"
            :href="`/Knowledge/${item.knowledgeid}/${data.currentPage}`"
            class="blog-wrap"
          >
            <div class="article-single centered">
              <div class="article-pic"><img :src="data.blobUrl + (item.photo ?? '')" :alt="item.question"></div>
              <div class="article-title">{{ item.question }}</div>
            </div>
          </a>
        </div>
      </div>
    </section>

    <PaginationBar
      :current-page="data.currentPage"
      :total-pages="data.totalPages"
      :build-url="(p) => `/Knowledges/${p}`"
    />
  </main>
</template>

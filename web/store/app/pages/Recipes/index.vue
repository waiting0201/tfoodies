<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/Recipes.cshtml.
// URL: /Recipes  or  /Recipes/{p}  with optional ?k= search
const route = useRoute()
const page = computed(() => Number(route.params.p ?? route.query.p ?? 1))
const keyword = computed(() => String(route.query.k ?? ''))
const { data } = await useRecipesData(page.value, keyword.value || undefined)

useHead({ title: '美味料理' })

const searchInput = ref(keyword.value)
function submitSearch() {
  if (searchInput.value.trim()) {
    navigateTo({ path: '/Recipes', query: { p: 1, k: searchInput.value } })
  }
}
</script>

<template>
  <main id="main">
    <section class="gray-bg searchsection">
      <div class="restrict allpadding">
        <div class="search-column recipe-search">
          <div class="searchall recipe-input">
            <form id="searchform" method="get" action="/Recipes">
              <div class="controlgroup">
                <label for="searchall"></label>
                <input
                  v-model="searchInput"
                  type="text" name="k" placeholder="搜尋美味料理"
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
        <div class="centered"><h1>美味料理</h1></div>
        <div class="direct-line"></div>
        <div class="three-column clr">
          <a
            v-for="item in data.items" :key="item.recipeid"
            :href="`/Recipe/${item.recipeid}/${data.currentPage}`"
            class="blog-wrap"
          >
            <div class="article-single centered">
              <div class="article-pic"><img :src="data.blobUrl + (item.rphoto ?? '')" :alt="`${item.title}, 橄欖油`"></div>
              <div class="article-title">{{ item.title }}</div>
            </div>
          </a>
        </div>
      </div>
    </section>

    <PaginationBar
      :current-page="data.currentPage"
      :total-pages="data.totalPages"
      :build-url="(p) => `/Recipes/${p}`"
    />
  </main>
</template>

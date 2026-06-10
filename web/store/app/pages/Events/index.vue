<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/Events.cshtml.
// URL: /Events  (no specific route in old RouteConfig — falls through default)
const route = useRoute()
const page = computed(() => Number(route.query.p ?? 1))
const { data } = await useEventsData(page.value)

useSeo(() => ({
  title: '活動花絮',
  description: '食在呼 TFoodies 活動花絮：橄欖油品油課程、餐會與健康飲食推廣活動的精彩記錄。',
  type: 'website',
}))
</script>

<template>
  <main id="main">
    <section class="allpadding clr tallsection">
      <div class="restrict-wide">
        <div class="centered">
          <h1>活動花絮</h1>
          <div class="space">
            <div class="content-wide">
              <div class="darkgray">食在呼致力於將最天然安心的食材從產地直送餐桌，帶給大家天然美味的料理。我們有優秀的義大利國立品油師和專業的團隊，為您舉辦健康油品課程或餐會活動，可依需求彈性調整活動或料理。歡迎公司團體或私人包班來電詢問。</div>
            </div>
            <div class="main">活動專線：04-24366659</div>
          </div>
        </div>
        <div class="direct-line"> </div>
        <div class="three-column clr page">
          <a
            v-for="item in data.items" :key="item.eventid"
            :href="`/Events/${item.eventid}`"
            class="blog-wrap news-blog-wrap"
          >
            <div class="article-single centered">
              <div class="descript left">
                {{ item.eventdate }}
                <div class="article-pic"><img :src="data.blobUrl + (item.photo ?? '')" :alt="item.title"></div>
              </div>
              <div class="article-title">{{ item.title }}</div>
              <div class="article-desc">{{ item.summary }}</div>
              <div class="left more">
                <div class="outline-btn">More</div>
              </div>
            </div>
          </a>
        </div>
      </div>
    </section>

    <PaginationBar
      :current-page="data.currentPage"
      :total-pages="data.totalPages"
      :build-url="(p) => `/Events?p=${p}`"
    />
  </main>
</template>

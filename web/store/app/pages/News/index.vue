<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/News.cshtml.
// URL: /News  or  /News/{p}
const route = useRoute()
const page = computed(() => Number(route.params.p ?? route.query.p ?? 1))
const { data } = await useNewsData(page.value)

useSeo(() => ({
  title: '最新消息',
  description: '食在呼 TFoodies 最新消息、活動公告與優惠訊息。',
  type: 'website',
}))
</script>

<template>
  <main id="main">
    <section class="allpadding clr tallsection">
      <div class="restrict-wide">
        <div class="centered"><h1>最新消息</h1></div>
        <div class="direct-line"> </div>
      </div>
    </section>

    <section class="allpadding">
      <div class="restrict-wide">
        <div class="three-column clr page">
          <a
            v-for="item in data.items" :key="item.newid"
            :href="`/NewsDetail/${item.newid}/1`"
            class="blog-wrap news-blog-wrap"
          >
            <div class="article-single centered">
              <div class="descript left">
                {{ item.publishdate }}
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
      :build-url="(p) => `/News/${p}`"
    />
  </main>
</template>

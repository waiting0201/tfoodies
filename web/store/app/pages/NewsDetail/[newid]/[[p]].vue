<script setup lang="ts">
// News detail. Functionality ported from reference/old/tfoodies/Views/MainMs/NewsDetail.cshtml
// (breadcrumb, title, activity time/schedule, share, publish date, intro, other news);
// interface redesigned into a tidier two-column article layout. URL: /NewsDetail/{newid}/{p?}
const route = useRoute()
const newid = computed(() => String(route.params.newid ?? ''))
const pageNum = computed(() => Number(route.params.p ?? 1))
const { data } = await useNewsDetailData(newid.value, pageNum.value)
const item = computed(() => data.value.item)

const siteUrl = String(useRuntimeConfig().public.siteUrl).replace(/\/+$/, '')
const ogImage = computed(() => (item.value?.photo ? data.value.blobUrl + item.value.photo : undefined))
const shareUrl = computed(() =>
  item.value?.shortener || `${siteUrl}/NewsDetail/${newid.value}/${pageNum.value}`)

useSeo(() => ({
  title: item.value?.title ?? '最新消息',
  description: item.value?.summary || item.value?.intro,
  image: ogImage.value,
  url: item.value?.shortener || undefined,
  type: 'article',
}))

useJsonLd(() => {
  if (!item.value) return null
  return [
    articleJsonLd({
      headline: item.value.title,
      description: item.value.summary || item.value.intro,
      image: ogImage.value,
      url: `${siteUrl}/NewsDetail/${item.value.newid}/1`,
      datePublished: item.value.publishdate,
    }),
    breadcrumbJsonLd([
      { name: '最新消息', url: `${siteUrl}/News` },
      { name: item.value.title },
    ]),
  ]
})

const others = computed(() =>
  data.value.others.map(o => ({ href: `/NewsDetail/${o.newid}`, photo: o.photo, label: o.title })))
</script>

<template>
  <main id="main" class="article-detail">
    <section class="restrict-wide allpadding">
      <nav class="crumb">
        <a :href="`/News/${data.pageNumber}`">最新消息</a>
        <span class="crumb__sep">/</span>
        <span class="crumb__current">{{ item?.title }}</span>
      </nav>
    </section>

    <section v-if="item" class="restrict-wide allpadding">
      <div class="layout">
        <article class="article">
          <header class="article__head">
            <h1 class="article__title">{{ item.title }}</h1>
            <ul class="meta">
              <li v-if="item.publishdate" class="meta__chip meta__chip--date">{{ item.publishdate }}</li>
              <li v-if="item.activitydate" class="meta__chip"><span class="meta__tag">活動時間</span>{{ item.activitydate }}</li>
              <li v-if="item.activityschedule" class="meta__chip"><span class="meta__tag">活動時程</span>{{ item.activityschedule }}</li>
            </ul>
            <ArticleShare :url="shareUrl" :title="item.title" />
            <div class="article__divider"></div>
          </header>

          <div class="prose" v-html="item.intro"></div>

          <div class="back">
            <a :href="`/News/${data.pageNumber}`" class="back__btn">返回列表</a>
          </div>
        </article>

        <ArticleAside heading="其他消息" :items="others" :blob-url="data.blobUrl" />
      </div>
    </section>
  </main>
</template>

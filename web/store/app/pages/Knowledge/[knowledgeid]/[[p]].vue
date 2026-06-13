<script setup lang="ts">
// Knowledge detail (小知識). Functionality ported from reference/old/tfoodies/Views/MainMs/KnowledgeDetail.cshtml
// (breadcrumb, question, share, date, answer, other articles); interface redesigned into a tidier
// two-column article layout. URL: /Knowledge/{knowledgeid}/{p?}
const route = useRoute()
const knowledgeid = computed(() => String(route.params.knowledgeid ?? ''))
const pageNum = computed(() => Number(route.params.p ?? 1))
const { data } = await useKnowledgeDetailData(knowledgeid.value, pageNum.value)
const item = computed(() => data.value.item)

const siteUrl = String(useRuntimeConfig().public.siteUrl).replace(/\/+$/, '')
const ogImage = computed(() => (item.value?.photo ? data.value.blobUrl + item.value.photo : undefined))
const shareUrl = computed(() =>
  item.value?.shortener || `${siteUrl}/Knowledge/${knowledgeid.value}/${pageNum.value}`)

useSeo(() => ({
  title: item.value?.question ?? '小知識',
  description: item.value?.description,
  image: ogImage.value,
  url: item.value?.shortener || undefined,
  type: 'article',
}))

useJsonLd(() => {
  if (!item.value) return null
  return [
    articleJsonLd({
      headline: item.value.question,
      description: item.value.description,
      image: ogImage.value,
      url: `${siteUrl}/Knowledge/${item.value.knowledgeid}/1`,
      datePublished: item.value.createdate,
    }),
    breadcrumbJsonLd([
      { name: '小知識', url: `${siteUrl}/Knowledges` },
      { name: item.value.question },
    ]),
  ]
})

const others = computed(() =>
  data.value.others.map(o => ({ href: `/Knowledge/${o.knowledgeid}`, photo: o.photo, label: o.question })))
</script>

<template>
  <main id="main" class="article-detail">
    <section class="restrict-wide allpadding">
      <nav class="crumb">
        <a :href="`/Knowledges/${data.pageNumber}`">小知識</a>
        <span class="crumb__sep">/</span>
        <span class="crumb__current">{{ item?.question }}</span>
      </nav>
    </section>

    <section v-if="item" class="restrict-wide allpadding">
      <div class="layout">
        <article class="article none-copy" oncopy="return false;">
          <header class="article__head">
            <h1 class="article__title">{{ item.question }}</h1>
            <ul class="meta">
              <li v-if="item.createdate" class="meta__chip meta__chip--date">{{ item.createdate }}</li>
            </ul>
            <ArticleShare :url="shareUrl" :title="item.question" />
            <div class="article__divider"></div>
          </header>

          <div class="prose" v-html="item.answer"></div>

          <div class="back">
            <a :href="`/Knowledges/${data.pageNumber}`" class="back__btn">返回</a>
          </div>
        </article>

        <ArticleAside heading="其他文章" :items="others" :blob-url="data.blobUrl" />
      </div>
    </section>
  </main>
</template>

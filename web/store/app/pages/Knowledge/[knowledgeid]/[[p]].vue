<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/KnowledgeDetail.cshtml.
// URL: /Knowledge/{knowledgeid}/{p?}  — detail keyed by knowledgeid (Guid), same as Recipe.
const route = useRoute()
const knowledgeid = computed(() => String(route.params.knowledgeid ?? ''))
const pageNum = computed(() => Number(route.params.p ?? 1))
const { data } = await useKnowledgeDetailData(knowledgeid.value, pageNum.value)
const item = computed(() => data.value.item)

const siteUrl = String(useRuntimeConfig().public.siteUrl).replace(/\/+$/, '')
const ogImage = computed(() => (item.value?.photo ? data.value.blobUrl + item.value.photo : undefined))

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
</script>

<template>
  <main id="main">
    <section class="restrict-wide allpadding">
      <div class="locate">
        <p>
          <a :href="`/Knowledges/${data.pageNumber}`" class="descript">小知識/</a>
          <a href="javascript:;" class="descript main">{{ item?.question }}</a>
        </p>
      </div>
    </section>

    <section v-if="item" class="allpadding clr section">
      <div class="restrict-wide">
        <div class="article-left none-copy" oncopy="return false;">
          <div class="timeline">
            <h1>{{ item.question }}</h1>
            <p class="inline">{{ item.createdate }}</p>
          </div>
          <section class="allsection" v-html="item.answer"></section>
          <div class="centered more">
            <a :href="`/Knowledges/${data.pageNumber}`" class="outline-btn">返回</a>
          </div>
        </div>

        <div class="article-right">
          <div class="other"><h2>其他文章</h2></div>
          <a
            v-for="other in data.others" :key="other.knowledgeid"
            :href="`/Knowledge/${other.knowledgeid}`"
            class="other-article"
          >
            <img :src="data.blobUrl + (other.photo ?? '')">
            <p class="centered">{{ other.question }}</p>
          </a>
        </div>
      </div>
    </section>

    <div class="centered more btn-show">
      <a :href="`/Knowledges/${data.pageNumber}`" class="outline-btn">返回</a>
    </div>
  </main>
</template>

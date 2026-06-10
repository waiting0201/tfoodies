<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/EventsDetail.cshtml.
// URL: /Events/{eventid}
const route = useRoute()
const eventid = computed(() => String(route.params.eventid ?? ''))
const pageNum = computed(() => Number(route.query.p ?? 1))
const { data } = await useEventDetailData(eventid.value, pageNum.value)
const item = computed(() => data.value.item)

const siteUrl = String(useRuntimeConfig().public.siteUrl).replace(/\/+$/, '')
const ogImage = computed(() => {
  const photo = item.value?.photos?.[0]?.photo
  return photo ? data.value.blobUrl + photo : undefined
})

useSeo(() => ({
  title: item.value?.title ?? '活動花絮',
  description: item.value?.intro,
  image: ogImage.value,
  url: item.value?.shortener || undefined,
  type: 'article',
}))

useJsonLd(() => {
  if (!item.value) return null
  return [
    articleJsonLd({
      headline: item.value.title,
      description: item.value.intro,
      image: ogImage.value,
      url: `${siteUrl}/Events/${item.value.eventid}`,
      datePublished: item.value.eventdate,
    }),
    breadcrumbJsonLd([
      { name: '活動花絮', url: `${siteUrl}/Events` },
      { name: item.value.title },
    ]),
  ]
})
</script>

<template>
  <main id="main">
    <section class="allpadding clr tallsection">
      <div class="restrict-wide">
        <div class="clr">
          <div class="pull-left">
            <a :href="`/Events?p=${data.pageNumber}`" class="back">
              <div class="inline">&#60;</div>
              <div class="inline">返回活動花絮列表</div>
            </a>
          </div>
        </div>
      </div>
    </section>

    <section v-if="item" class="allpadding clr section">
      <div class="restrict-wide">
        <div oncopy="return false;" class="article-left none-copy">
          <div class="timeline">
            <h1>{{ item.title }}</h1>
            <div class="clr">
              <div class="inline"><div class="time-tag">活動時間</div></div>
              <div class="inline">{{ item.eventdate }}</div>
            </div>
          </div>
        </div>
      </div>
      <div class="restrict-wide">
        <p class="darkgray" v-html="item.intro"></p>
      </div>
      <div class="restrict-wide">
        <div class="photo-wrapper clr page popup-gallery">
          <a
            v-for="photo in item.photos" :key="photo.sort"
            :href="data.blobUrl + photo.photo"
            title=""
            class="photo"
          >
            <div
              :style="{ backgroundImage: `url(${data.blobUrl}${photo.photo})` }"
              class="article-pic"
            >
              <img src="/content/images/section/photo-block.png">
            </div>
          </a>
        </div>
      </div>
      <div class="centered more"><a href="#wrapper" class="outline-btn">TOP</a></div>
    </section>
  </main>
</template>

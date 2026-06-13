<script setup lang="ts">
// Events detail (活動花絮). Functionality ported from reference/old/tfoodies/Views/MainMs/EventsDetail.cshtml
// (back link, title, event date, share, intro, photo gallery with lightbox); interface redesigned
// into a tidier gallery layout, the legacy magnificPopup replaced by a native Vue lightbox. URL: /Events/{eventid}
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
const shareUrl = computed(() =>
  item.value?.shortener || `${siteUrl}/Events/${eventid.value}`)

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

// 相片牆燈箱（取代舊系統 magnificPopup）
const photos = computed(() => item.value?.photos ?? [])
const lightboxIndex = ref(-1)
const lightboxOpen = computed(() => lightboxIndex.value >= 0)
const currentPhoto = computed(() => photos.value[lightboxIndex.value])

function openLightbox(i: number) { lightboxIndex.value = i }
function closeLightbox() { lightboxIndex.value = -1 }
function prevPhoto() { lightboxIndex.value = (lightboxIndex.value - 1 + photos.value.length) % photos.value.length }
function nextPhoto() { lightboxIndex.value = (lightboxIndex.value + 1) % photos.value.length }

function onKey(e: KeyboardEvent) {
  if (!lightboxOpen.value) return
  if (e.key === 'Escape') closeLightbox()
  else if (e.key === 'ArrowLeft') prevPhoto()
  else if (e.key === 'ArrowRight') nextPhoto()
}
onMounted(() => window.addEventListener('keydown', onKey))
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))
</script>

<template>
  <main id="main" class="events-detail">
    <section class="restrict-wide allpadding">
      <nav class="crumb">
        <a :href="`/Events?p=${data.pageNumber}`" class="crumb__back">
          <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6" /></svg>
          返回活動花絮列表
        </a>
      </nav>
    </section>

    <section v-if="item" class="restrict-wide allpadding">
      <header class="article__head none-copy" oncopy="return false;">
        <h1 class="article__title">{{ item.title }}</h1>
        <ul class="meta">
          <li v-if="item.eventdate" class="meta__chip"><span class="meta__tag">活動時間</span>{{ item.eventdate }}</li>
        </ul>
        <ArticleShare :url="shareUrl" :title="item.title" />
        <div class="article__divider"></div>
      </header>

      <div v-if="item.intro" class="prose none-copy" oncopy="return false;" v-html="item.intro"></div>

      <div v-if="photos.length" class="gallery">
        <button
          v-for="(photo, i) in photos" :key="photo.sort"
          type="button" class="gallery__item"
          :aria-label="`放大第 ${i + 1} 張照片`"
          @click="openLightbox(i)"
        >
          <img :src="data.blobUrl + photo.photo" :alt="`${item.title} 活動照片 ${i + 1}`" loading="lazy">
        </button>
      </div>

      <div class="back">
        <a :href="`/Events?p=${data.pageNumber}`" class="back__btn">返回列表</a>
      </div>
    </section>

    <Teleport to="body">
      <Transition name="lightbox-fade">
        <div v-if="lightboxOpen" class="events-detail">
          <div class="lightbox" @click.self="closeLightbox">
            <button type="button" class="lightbox__close" aria-label="關閉" @click="closeLightbox">&times;</button>
            <button v-if="photos.length > 1" type="button" class="lightbox__btn lightbox__btn--prev" aria-label="上一張" @click="prevPhoto">&#8249;</button>
            <img v-if="currentPhoto" :src="data.blobUrl + currentPhoto.photo" :alt="`${item?.title} 活動照片`" class="lightbox__img">
            <button v-if="photos.length > 1" type="button" class="lightbox__btn lightbox__btn--next" aria-label="下一張" @click="nextPhoto">&#8250;</button>
            <div v-if="photos.length > 1" class="lightbox__count">{{ lightboxIndex + 1 }} / {{ photos.length }}</div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </main>
</template>

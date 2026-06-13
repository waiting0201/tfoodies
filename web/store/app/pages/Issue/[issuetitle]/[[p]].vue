<script setup lang="ts">
// Issue detail (綠誌). Functionality ported from reference/old/tfoodies/Views/MainMs/IssueDetail.cshtml
// (breadcrumb, title, share, date, intro, other articles, related products, recipes); interface
// redesigned into a tidier two-column article layout. URL: /Issue/{issuetitle}/{p?}
const route = useRoute()
const slug = computed(() => String(route.params.issuetitle ?? ''))
const issuetitle = computed(() => urlSlugToTitle(slug.value))
const pageNum = computed(() => Number(route.params.p ?? 1))
const { data } = await useIssueDetailData(issuetitle.value, pageNum.value)
const item = computed(() => data.value.item)

const siteUrl = String(useRuntimeConfig().public.siteUrl).replace(/\/+$/, '')
const ogImage = computed(() => (item.value?.photo ? data.value.blobUrl + item.value.photo : undefined))
const shareUrl = computed(() =>
  item.value?.shortener || `${siteUrl}/Issue/${slug.value}/${pageNum.value}`)

useSeo(() => ({
  title: item.value?.title ?? '綠誌',
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
      url: `${siteUrl}/Issue/${titleToUrlSlug(item.value.title)}/1`,
      datePublished: item.value.createdate,
    }),
    breadcrumbJsonLd([
      { name: '健康生活', url: `${siteUrl}/Issues` },
      { name: item.value.title },
    ]),
  ]
})

const others = computed(() =>
  data.value.others.map(o => ({ href: `/Issue/${titleToUrlSlug(o.title)}`, photo: o.photo, label: o.title })))
const sortedProducts = computed(() =>
  data.value.products.filter(p => !p.isdisabled).sort((a, b) => b.sort - a.sort))
</script>

<template>
  <main id="main" class="article-detail">
    <section class="restrict-wide allpadding">
      <nav class="crumb">
        <a :href="`/Issues/${data.pageNumber}`">健康生活</a>
        <span class="crumb__sep">/</span>
        <span class="crumb__current">{{ item?.title }}</span>
      </nav>
    </section>

    <section v-if="item" class="restrict-wide allpadding">
      <div class="layout">
        <article class="article none-copy" oncopy="return false;">
          <header class="article__head">
            <h1 class="article__title">{{ item.title }}</h1>
            <ul class="meta">
              <li v-if="item.createdate" class="meta__chip meta__chip--date">{{ item.createdate }}</li>
            </ul>
            <ArticleShare :url="shareUrl" :title="item.title" />
            <div class="article__divider"></div>
          </header>

          <div class="prose" v-html="item.intro"></div>

          <div class="back">
            <a :href="`/Issues/${data.pageNumber}`" class="back__btn">返回</a>
          </div>
        </article>

        <ArticleAside heading="其他文章" :items="others" :blob-url="data.blobUrl" />
      </div>
    </section>

    <section v-if="item && sortedProducts.length" class="gray-bg allsection">
      <div class="restrict-wide allpadding">
        <h2 class="section-title">購買相關商品</h2>
        <div class="responsive promoteSlider clr">
          <ProductCard
            v-for="p in sortedProducts"
            :key="p.productid"
            :product="p" :blob-url="data.blobUrl" :promote-sliding="true"
          />
        </div>
      </div>
    </section>

    <section v-if="item && data.recipes.length" class="allsection">
      <div class="restrict-wide allpadding">
        <h2 class="section-title">查看食譜</h2>
        <div class="recipe-grid">
          <a
            v-for="r in data.recipes.slice(0, 3)" :key="r.recipeid"
            :href="`/Recipe/${r.recipeid}/1`"
            class="recipe-card"
          >
            <div class="recipe-card__pic"><img :src="data.blobUrl + (r.rphoto ?? '')" :alt="r.title" loading="lazy"></div>
            <div class="recipe-card__title">{{ r.title }}</div>
          </a>
        </div>
      </div>
    </section>
  </main>
</template>

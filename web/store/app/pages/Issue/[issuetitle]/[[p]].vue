<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/IssueDetail.cshtml.
// URL: /Issue/{issuetitle}/{p?}  — issuetitle uses hyphen↔slash convention (same as Product).
const route = useRoute()
const slug = computed(() => String(route.params.issuetitle ?? ''))
const issuetitle = computed(() => urlSlugToTitle(slug.value))
const pageNum = computed(() => Number(route.params.p ?? 1))
const { data } = await useIssueDetailData(issuetitle.value, pageNum.value)
const item = computed(() => data.value.item)

const siteUrl = String(useRuntimeConfig().public.siteUrl).replace(/\/+$/, '')
const ogImage = computed(() => (item.value?.photo ? data.value.blobUrl + item.value.photo : undefined))

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
</script>

<template>
  <main id="main">
    <section class="restrict-wide allpadding">
      <div class="locate">
        <p>
          <a :href="`/Issues/${data.pageNumber}`" class="descript">健康生活/</a>
          <a href="javascript:;" class="descript main">{{ item?.title }}</a>
        </p>
      </div>
    </section>

    <section v-if="item" class="allpadding clr section">
      <div class="restrict-wide">
        <div class="article-left none-copy" oncopy="return false;">
          <div class="timeline">
            <h1>{{ item.title }}</h1>
            <p class="inline">{{ item.createdate }}</p>
          </div>
          <section class="allsection" v-html="item.intro"></section>
          <div class="centered more">
            <a :href="`/Issues/${data.pageNumber}`" class="outline-btn">返回</a>
          </div>
        </div>

        <div class="article-right">
          <div class="other"><h2>其他文章</h2></div>
          <a
            v-for="other in data.others" :key="other.issueid"
            :href="`/Issue/${titleToUrlSlug(other.title)}`"
            class="other-article"
          >
            <img :src="data.blobUrl + (other.photo ?? '')">
            <p class="centered">{{ other.title }}</p>
          </a>
        </div>
      </div>
    </section>

    <div class="centered more btn-show">
      <a :href="`/Issues/${data.pageNumber}`" class="outline-btn">返回</a>
    </div>

    <section v-if="item && data.products.length" class="gray-bg allsection">
      <div class="restrict-wide allpadding">
        <div class="adstitle"><h2 class="main">購買相關商品</h2></div>
        <div class="responsive promoteSlider clr">
          <ProductCard
            v-for="p in data.products.filter(p => !p.isdisabled).sort((a,b) => b.sort - a.sort)"
            :key="p.productid"
            :product="p" :blob-url="data.blobUrl" :promote-sliding="true"
          />
        </div>
      </div>
    </section>

    <section v-if="item && data.recipes.length" class="allsection">
      <div class="restrict-wide allpadding">
        <div class="adstitle"><h2 class="main">查看食譜</h2></div>
        <div class="three-column clr">
          <a
            v-for="r in data.recipes.slice(0, 3)" :key="r.recipeid"
            :href="`/Recipe/${r.recipeid}/1`"
            class="blog-wrap"
          >
            <div class="article-single centered">
              <div class="article-pic"><img :src="data.blobUrl + (r.rphoto ?? '')" :alt="r.title"></div>
              <div class="article-title">{{ r.title }}</div>
            </div>
          </a>
        </div>
      </div>
    </section>
  </main>
</template>

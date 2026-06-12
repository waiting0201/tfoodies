<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/Brand.cshtml.
// URL: /Brand/{brandtitle}
const route = useRoute()
const brandtitle = computed(() => String(route.params.brandtitle ?? ''))
const { data } = await useBrandData(brandtitle.value)
const b = computed(() => data.value.brand)

// 系列商品「More」分頁載入（取代舊 jQuery $.post + _PartialProductList 字串）。
// 初始 4 筆由 useBrandData 帶入；之後以 skip/take=4 向 /store/brands/products 取得並 append。
const apiBase = String(useRuntimeConfig().public.apiBase)
const products = ref<ViewProduct[]>([...data.value.products])
const hasMore = ref(data.value.hasMore)
const loading = ref(false)

// SSR data 在 client 端切換品牌時會更新 → 重置清單。
watch(data, (d) => {
  products.value = [...d.products]
  hasMore.value = d.hasMore
})

async function loadMore() {
  if (loading.value || !hasMore.value) return
  loading.value = true
  try {
    const res = await $fetch<{ products: ApiProduct[]; hasMore: boolean }>(
      `${apiBase}/store/brands/products`,
      { query: { brandtitle: brandtitle.value, skip: products.value.length, take: 4 } },
    )
    products.value.push(...res.products.map(mapProduct))
    hasMore.value = res.hasMore
  } finally {
    loading.value = false
  }
}

const ogImage = computed(() => {
  const photo = b.value?.banner ?? b.value?.logo
  return photo ? data.value.blobUrl + photo : undefined
})

useSeo(() => ({
  title: b.value?.title ?? '品牌',
  description: b.value?.intro || b.value?.slogan,
  image: ogImage.value,
  type: 'website',
}))
</script>

<template>
  <main id="main">
    <section
      v-if="b"
      :style="{ backgroundImage: `url(${data.blobUrl}${b.banner ?? ''})` }"
      class="brandbanner allpadding"
    >
      <div class="restrict-wide">
        <div class="brand-logo"><img :src="data.blobUrl + (b.logo ?? '')"></div>
        <h1 class="ci-title white max-title">{{ b.title }}</h1>
        <h1 class="white">{{ b.subtitle }}</h1>
      </div>
    </section>

    <section v-if="b" class="allsection">
      <div class="restrict-wide allpadding">
        <div class="centered">
          <div class="banner-title ci-title">{{ b.patternentitle }}</div>
          <h2>{{ b.patternchtitle }}</h2>
          <div v-if="b.parttnervideo" class="brand-video">
            <iframe :src="b.parttnervideo" frameborder="0" allowfullscreen class="youtubeVideo"></iframe>
          </div>
          <div class="orangeblock"></div>
          <div class="content-wide" v-html="(b.patternmemo ?? '').replace(/\n/g, '<br />')"></div>
        </div>
      </div>
      <div class="pattern-block">
        <div :style="{ backgroundImage: `url(${data.blobUrl}${b.patternclass ?? ''})` }" class="labarre-pattern"></div>
        <div class="main-block"></div>
        <div class="pattern-slider">
          <ul class="allslider">
            <li v-for="photo in b.photos" :key="photo.sort">
              <img :src="data.blobUrl + photo.photo">
            </li>
          </ul>
        </div>
      </div>
    </section>

    <section
      v-if="b"
      :style="{ backgroundImage: `url(${data.blobUrl}${b.storybgclass ?? ''})` }"
      class="allpadding clr allsection storybg"
    >
      <div class="restrict">
        <div class="slogan">
          <img v-if="b.ilogo" :src="data.blobUrl + b.ilogo">
          <div class="italic">" {{ b.slogan }} "</div>
        </div>
        <div class="brandcontent">
          <div class="content">
            <p>{{ b.intro }}</p>
          </div>
        </div>
      </div>
    </section>

    <section v-if="b" class="allpadding clr">
      <div class="restrict">
        <div class="banner-title ci-title centered">{{ b.storyentitle }}</div>
        <h2>{{ b.storychtitle }}</h2>
        <div class="orangeblock"></div>
        <div class="content-wide" v-html="(b.storymemo ?? '').replace(/\n/g, '<br>')"></div>
      </div>
    </section>

    <section v-if="b">
      <div class="people-wrap">
        <div :style="{ backgroundImage: `url(${data.blobUrl}${b.peoplephoto ?? ''})` }" class="people">
          <img src="/content/images/section/people.png">
        </div>
        <div class="people-content">
          <h3 class="ci-title centered">{{ b.peopletitle }}</h3>
          <h2 class="centered">{{ b.peopleslogan }}</h2>
          <div class="orangeblock"></div>
          <div class="content"><p>{{ b.peoplememo }}</p></div>
        </div>
      </div>
    </section>

    <section class="allsection">
      <div class="restrict-wide allpadding">
        <div class="centered">
          <div class="banner-title ci-title">The Collection</div>
          <h2>系列商品</h2>
          <div class="orangeblock"></div>
        </div>
        <div class="four-column clr product_list">
          <ProductCard
            v-for="p in products" :key="p.productid"
            :product="p" :blob-url="data.blobUrl" :promote-sliding="true"
          />
        </div>
        <div v-if="hasMore" class="centered more">
          <a href="javascript:;" class="outline-btn moreA" :class="{ 'is-loading': loading }" rel="nofollow" @click="loadMore">
            {{ loading ? '載入中…' : 'More' }}
          </a>
        </div>
      </div>
    </section>
  </main>
</template>

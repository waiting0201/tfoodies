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

// 帶貨橋：把這篇綠誌的相關商品一次加入購物車（同食譜頁做法）。
// 每筆走 cart.add() → 一併觸發 add_to_cart 追蹤事件。
const cart = useCartStore()
const toastMsg = ref('')
let toastTimer: ReturnType<typeof setTimeout> | null = null
function showToast(msg: string) {
  toastMsg.value = msg
  if (toastTimer) clearTimeout(toastTimer)
  toastTimer = setTimeout(() => { toastMsg.value = '' }, 2200)
}
function addAllToCart() {
  const ps = sortedProducts.value
  if (!ps.length) return
  for (const p of ps) {
    cart.add({
      productId: p.productid,
      title: p.title,
      unitPrice: p.price,
      quantity: 1,
      photo: p.photo,
      capacity: p.capacity,
    })
  }
  showToast(`已加入 ${ps.length} 項商品到購物車`)
}
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
        <div class="buy-all">
          <p class="buy-all__hint">喜歡這篇介紹的商品？一次加入購物車 👇</p>
          <button type="button" class="buy-all__btn" @click="addAllToCart">
            🛒 一鍵把 {{ sortedProducts.length }} 項商品加入購物車
          </button>
          <a href="/Cart" class="buy-all__cart">前往購物車 →</a>
        </div>
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

    <Transition name="copy-toast">
      <div v-if="toastMsg" class="copy-toast">{{ toastMsg }}</div>
    </Transition>
  </main>
</template>

<style scoped>
/* 帶貨橋：一鍵把相關商品加入購物車（沿用 .article-detail 的 --teal 變數） */
.buy-all {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.55rem;
  margin: 0 auto 1.75rem;
  text-align: center;
}
.buy-all__hint { margin: 0; font-size: 0.95rem; color: #555; }
.buy-all__btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.85rem 2rem;
  font-size: 1.02rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  color: #fff;
  background: var(--teal);
  border: 0;
  border-radius: 999px;
  cursor: pointer;
  box-shadow: 0 6px 16px rgba(38, 183, 188, 0.35);
  transition: background 0.18s ease, transform 0.12s ease;
}
.buy-all__btn:hover { background: var(--teal-dark); }
.buy-all__btn:active { transform: translateY(1px); }
.buy-all__cart { font-size: 0.88rem; color: var(--teal); text-decoration: none; }
.buy-all__cart:hover { color: var(--teal-dark); text-decoration: underline; }

/* 複製/加入提示 toast */
.copy-toast {
  position: fixed;
  left: 50%; bottom: 2.5rem;
  transform: translateX(-50%);
  z-index: 1000;
  padding: 0.7rem 1.5rem;
  font-size: 0.9rem;
  letter-spacing: 0.05em;
  color: #fff;
  background: rgba(38, 183, 188, 0.96);
  border-radius: 999px;
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.18);
}
.copy-toast-enter-active,
.copy-toast-leave-active { transition: opacity 0.3s ease, transform 0.3s ease; }
.copy-toast-enter-from,
.copy-toast-leave-to { opacity: 0; transform: translate(-50%, 0.75rem); }
</style>

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
const sortedProducts = computed(() =>
  data.value.products.filter(p => !p.isdisabled).sort((a, b) => b.sort - a.sort))

// 帶貨橋：把這篇小知識的相關商品一次加入購物車（同綠誌/食譜頁做法）。
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

/* 加入提示 toast */
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

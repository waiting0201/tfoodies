<script setup lang="ts">
// Recipe detail. Functionality ported from reference/old/tfoodies/Views/MainMs/RecipeDetail.cshtml
// (breadcrumb, hero, cook meta, share, intro, ingredients/seasonings, steps, related products);
// interface redesigned into a tidier card-based layout. URL: /Recipe/{recipeid}/{p?}
const route = useRoute()
const recipeid = computed(() => String(route.params.recipeid ?? ''))
const pageNum = computed(() => Number(route.params.p ?? 1))
const { data } = await useRecipeDetailData(recipeid.value, pageNum.value)
const item = computed(() => data.value.item)

const siteUrl = String(useRuntimeConfig().public.siteUrl).replace(/\/+$/, '')
const ogImage = computed(() => {
  const photo = item.value?.photo ?? item.value?.rphoto
  return photo ? data.value.blobUrl + photo : undefined
})

useSeo(() => ({
  title: item.value?.title ?? '美味料理',
  description: item.value?.intro,
  image: ogImage.value,
  url: item.value?.shortener || undefined,
  type: 'article',
}))

useJsonLd(() => {
  if (!item.value) return null
  const ingredientLines = [...item.value.ingredients, ...item.value.seasonings]
    .map((g) => [g.title, g.value].filter(Boolean).join(' ').trim())
    .filter(Boolean)
  return [
    recipeJsonLd({
      name: item.value.title,
      description: item.value.intro,
      image: ogImage.value,
      url: `${siteUrl}/Recipe/${item.value.recipeid}/1`,
      durationMinutes: item.value.duration,
      yield: item.value.portion,
      ingredients: ingredientLines,
      steps: item.value.steps.map((s) => ({ title: s.title, text: s.value })),
      videoUrl: item.value.youtube,
    }),
    breadcrumbJsonLd([
      { name: '美味料理', url: `${siteUrl}/Recipes` },
      { name: item.value.title },
    ]),
  ]
})

// 分享連結（沿用舊系統的分享功能；優先用短網址，否則用本頁正式網址）
const shareUrl = computed(() =>
  item.value?.shortener || `${siteUrl}/Recipe/${recipeid.value}/${pageNum.value}`)
const fbShare = computed(() => `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(shareUrl.value)}`)
const lineShare = computed(() => `https://social-plugins.line.me/lineit/share?url=${encodeURIComponent(shareUrl.value)}`)

// 共用 toast（複製連結 / 加入購物車 共用同一個提示）
const toastMsg = ref('')
let toastTimer: ReturnType<typeof setTimeout> | null = null
function showToast(msg: string) {
  toastMsg.value = msg
  if (toastTimer) clearTimeout(toastTimer)
  toastTimer = setTimeout(() => { toastMsg.value = '' }, 2200)
}

async function copyLink() {
  try {
    await navigator.clipboard.writeText(shareUrl.value)
    showToast('已複製連結')
  } catch { /* clipboard 不可用時靜默忽略 */ }
}

// 帶貨橋：看完食譜「想煮」→ 一鍵把這道菜會用到的相關商品全部加入購物車，
// 不必逐一點進商品頁。每筆走 cart.add()，會一併觸發 add_to_cart 追蹤事件。
const cart = useCartStore()
const buyableProducts = computed(() =>
  (item.value?.products ?? []).filter((p) => !p.isdisabled),
)
function addAllToCart() {
  const ps = buyableProducts.value
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
  <main id="main" class="recipe-detail">
    <section class="restrict-wide allpadding">
      <nav class="crumb">
        <a :href="`/Recipes/${data.pageNumber}`">美味料理</a>
        <span class="crumb__sep">/</span>
        <span class="crumb__current">{{ item?.title }}</span>
      </nav>
    </section>

    <template v-if="item">
      <section class="restrict-wide allpadding">
        <!-- 主視覺：料理照片 + 標題、烹調資訊、分享、簡介 -->
        <div class="hero">
          <figure class="hero__media">
            <img :src="data.blobUrl + (item.photo ?? '')" :alt="`${item.title}, 橄欖油`">
          </figure>

          <div class="hero__body">
            <h1 class="hero__title">{{ item.title }}</h1>

            <ul class="meta">
              <li v-if="item.duration" class="meta__item">
                <svg viewBox="0 0 24 24" class="meta__icon" aria-hidden="true">
                  <circle cx="12" cy="13" r="8" /><path d="M12 9v4l2.5 2.5M9 2h6" />
                </svg>
                <span>{{ item.duration }} 分鐘</span>
              </li>
              <li v-if="item.portion" class="meta__item">
                <svg viewBox="0 0 24 24" class="meta__icon" aria-hidden="true">
                  <circle cx="9" cy="8" r="3.2" /><path d="M3 20c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5" />
                  <path d="M16 6.2a3 3 0 0 1 0 5.6M18 20c0-2.6-1.3-4.5-3.3-5.2" />
                </svg>
                <span>{{ item.portion }} 人份</span>
              </li>
              <li v-if="item.youtube" class="meta__item">
                <a :href="item.youtube" target="_blank" rel="noopener" class="meta__video">
                  <svg viewBox="0 0 24 24" class="meta__icon" aria-hidden="true">
                    <rect x="3" y="5" width="18" height="14" rx="3" /><path d="M10 9.5v5l4-2.5z" fill="currentColor" stroke="none" />
                  </svg>
                  <span>線上影片</span>
                </a>
              </li>
            </ul>

            <div class="share">
              <span class="share__label">分享</span>
              <a :href="fbShare" target="_blank" rel="noopener" class="share__btn" aria-label="分享到 Facebook">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M14 8.5V7c0-.8.4-1.2 1.3-1.2H17V3h-2.6C11.8 3 11 4.6 11 6.6v1.9H9V11h2v9h3v-9h2.2l.4-2.5z" fill="currentColor" stroke="none" /></svg>
              </a>
              <a :href="lineShare" target="_blank" rel="noopener" class="share__btn" aria-label="分享到 LINE">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 4C7 4 3 7.3 3 11.3c0 3.6 3.2 6.6 7.5 7.2.3.1.7.2.8.5.1.3 0 .7 0 1l-.1.8c0 .2-.2.9.8.5s5.4-3.2 7.4-5.5c1.3-1.4 2-2.9 2-4.5C21.5 7.3 17.5 4 12 4z" fill="currentColor" stroke="none" /></svg>
              </a>
              <button type="button" class="share__btn" aria-label="複製連結" @click="copyLink">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 9h9v9a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2V9z" /><path d="M5 15V6a2 2 0 0 1 2-2h9" /></svg>
              </button>
            </div>

            <div class="hero__divider"></div>
            <p class="hero__intro" v-html="(item.intro ?? '').replace(/\n/g, '<br />')"></p>
          </div>
        </div>

        <!-- 食材 / 調味料 -->
        <div class="panels">
          <section v-if="item.ingredients.length" class="panel">
            <h2 class="panel__title"><span class="panel__bar"></span>食材準備</h2>
            <ul class="ftable">
              <li v-for="ing in item.ingredients" :key="ing.sort" class="ftable__row">
                <span class="ftable__name">{{ ing.title }}</span>
                <span class="ftable__dots"></span>
                <span class="ftable__val">{{ ing.value }}</span>
              </li>
            </ul>
          </section>

          <section v-if="item.seasonings.length" class="panel">
            <h2 class="panel__title"><span class="panel__bar"></span>調味料準備</h2>
            <ul class="ftable">
              <li v-for="s in item.seasonings" :key="s.sort" class="ftable__row">
                <span class="ftable__name">{{ s.title }}</span>
                <span class="ftable__dots"></span>
                <span class="ftable__val">{{ s.value }}</span>
              </li>
            </ul>
          </section>
        </div>

        <!-- 製作步驟 -->
        <section v-if="item.steps.length" class="recipe-steps">
          <h2 class="section-title">製作步驟</h2>
          <ol class="steps">
            <li v-for="(rs, idx) in item.steps" :key="rs.sort" class="step">
              <div class="step__no">{{ idx + 1 }}</div>
              <div class="step__body">
                <h3 v-if="rs.title" class="step__title">{{ rs.title }}</h3>
                <p class="step__text" v-html="(rs.value ?? '').replace(/\n/g, '<br/>')"></p>
              </div>
            </li>
          </ol>
        </section>

        <div class="back">
          <a :href="`/Recipes/${data.pageNumber}`" class="back__btn">返回美味料理</a>
        </div>
      </section>

      <section v-if="item.products.length" class="gray-bg allsection">
        <div class="restrict-wide allpadding">
          <div class="adstitle"><h2 class="main">購買相關商品</h2></div>

          <div v-if="buyableProducts.length" class="buy-all">
            <p class="buy-all__hint">想煮這道菜？把需要的商品一次加入購物車 👇</p>
            <button type="button" class="buy-all__btn" @click="addAllToCart">
              🛒 一鍵把 {{ buyableProducts.length }} 項商品加入購物車
            </button>
            <a href="/Cart" class="buy-all__cart">前往購物車 →</a>
          </div>

          <div class="responsive promoteSlider clr">
            <ProductCard
              v-for="p in item.products.filter(p => !p.isdisabled).sort((a,b) => b.sort - a.sort)"
              :key="p.productid"
              :product="p" :blob-url="data.blobUrl" :promote-sliding="true"
            />
          </div>
        </div>
      </section>
    </template>

    <Transition name="copy-toast">
      <div v-if="toastMsg" class="copy-toast">{{ toastMsg }}</div>
    </Transition>
  </main>
</template>

<style scoped>
.recipe-detail {
  --teal: #26b7bc;
  --teal-dark: #156467;
  --ink: #393939;
  --muted: #9b9b9b;
  --line: #e8eaea;
  --soft: #f6f9f9;
  color: var(--ink);
}

/* 麵包屑 */
.crumb {
  display: flex;
  align-items: center;
  flex-wrap: nowrap;
  padding: 1.75rem 0 0.5rem;
  font-size: 0.85rem;
  letter-spacing: 0.04em;
  color: var(--muted);
  white-space: nowrap;
}
.crumb a { color: var(--teal); text-decoration: none; flex: 0 0 auto; }
.crumb a:hover { color: var(--teal-dark); }
.crumb__sep { margin: 0 0.5em; flex: 0 0 auto; }
.crumb__current {
  color: var(--ink);
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 主視覺 */
.hero {
  display: grid;
  gap: 1.75rem;
  padding: 1.5rem 0 2.5rem;
}
.hero__media {
  margin: 0;
  border-radius: 14px;
  overflow: hidden;
  box-shadow: 0 12px 30px rgba(21, 100, 103, 0.12);
  background: var(--soft);
}
.hero__media img { display: block; width: 100%; height: 100%; object-fit: cover; }
.hero__body { display: flex; flex-direction: column; }
.hero__title {
  margin: 0 0 1rem;
  font-size: 1.85rem;
  line-height: 1.3;
  font-weight: 600;
  letter-spacing: 0.02em;
  color: var(--ink);
}

/* 烹調資訊 chips */
.meta {
  list-style: none;
  margin: 0 0 1.25rem;
  padding: 0;
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
}
.meta__item,
.meta__video {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.4rem 0.9rem;
  font-size: 0.9rem;
  color: var(--teal-dark);
  background: var(--soft);
  border: 1px solid var(--line);
  border-radius: 999px;
}
.meta__video { padding: 0; border: 0; background: none; color: var(--teal); text-decoration: none; }
.meta__video:hover { color: var(--teal-dark); }
.meta__icon {
  width: 17px; height: 17px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.7;
  stroke-linecap: round;
  stroke-linejoin: round;
}

/* 分享 */
.share { display: flex; align-items: center; gap: 0.55rem; margin-bottom: 1.25rem; }
.share__label { font-size: 0.82rem; letter-spacing: 0.1em; color: var(--muted); margin-right: 0.15rem; }
.share__btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 34px; height: 34px;
  padding: 0;
  border: 1px solid var(--line);
  border-radius: 50%;
  background: #fff;
  color: var(--teal);
  cursor: pointer;
  transition: background 0.18s ease, color 0.18s ease, border-color 0.18s ease;
}
.share__btn:hover { background: var(--teal); color: #fff; border-color: var(--teal); }
.share__btn svg { width: 17px; height: 17px; fill: none; stroke: currentColor; stroke-width: 1.7; stroke-linecap: round; stroke-linejoin: round; }

.hero__divider { height: 1px; background: var(--line); margin: 0.25rem 0 1.1rem; }
.hero__intro { margin: 0; font-size: 1rem; line-height: 1.9; color: #555; }

/* 食材 / 調味料 兩欄卡片 */
.panels {
  display: grid;
  gap: 1.25rem;
  margin-bottom: 2.75rem;
}
.panel {
  padding: 1.5rem 1.6rem 1.25rem;
  background: var(--soft);
  border: 1px solid var(--line);
  border-radius: 14px;
}
.panel__title {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin: 0 0 1rem;
  font-size: 1.15rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  color: var(--teal-dark);
}
.panel__bar { width: 4px; height: 1.1em; border-radius: 2px; background: var(--teal); }
.ftable { list-style: none; margin: 0; padding: 0; }
.ftable__row {
  display: flex;
  align-items: flex-end;
  gap: 0.4rem;
  padding: 0.55rem 0;
  border-bottom: 1px dashed var(--line);
  font-size: 0.95rem;
}
.ftable__row:last-child { border-bottom: 0; }
.ftable__name { color: var(--ink); }
.ftable__dots { flex: 1; border-bottom: 1px dotted #cfd6d6; transform: translateY(-0.3em); }
.ftable__val { color: var(--teal-dark); font-weight: 500; white-space: nowrap; }

/* 製作步驟 */
.section-title {
  text-align: center;
  margin: 0 0 1.75rem;
  font-size: 1.4rem;
  font-weight: 600;
  letter-spacing: 0.08em;
  color: var(--ink);
}
.section-title::after {
  content: "";
  display: block;
  width: 46px; height: 3px;
  margin: 0.7rem auto 0;
  border-radius: 2px;
  background: var(--teal);
}
.steps { list-style: none; margin: 0 auto 2.5rem; padding: 0; max-width: 820px; }
.step { display: flex; gap: 1.1rem; padding: 1.1rem 0; border-bottom: 1px solid var(--line); }
.step:last-child { border-bottom: 0; }
.step__no {
  flex: 0 0 auto;
  width: 38px; height: 38px;
  display: flex; align-items: center; justify-content: center;
  font-size: 1rem; font-weight: 600;
  color: #fff;
  background: var(--teal);
  border-radius: 50%;
  box-shadow: 0 4px 10px rgba(38, 183, 188, 0.35);
}
.step__body { padding-top: 0.15rem; }
.step__title { margin: 0 0 0.35rem; font-size: 1.02rem; font-weight: 600; color: var(--teal-dark); }
.step__text { margin: 0; font-size: 0.97rem; line-height: 1.85; color: #555; }

/* 返回 */
.back { text-align: center; padding-bottom: 1rem; }
.back__btn {
  display: inline-block;
  padding: 0.7rem 2.6rem;
  font-size: 0.92rem;
  letter-spacing: 0.08em;
  color: var(--teal);
  border: 1px solid var(--teal);
  border-radius: 999px;
  text-decoration: none;
  transition: background 0.18s ease, color 0.18s ease;
}
.back__btn:hover { background: var(--teal); color: #fff; }

/* 帶貨橋：一鍵把食材加入購物車 */
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

/* 複製連結提示 */
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

/* 平板以上：主視覺左右排、食材調味兩欄 */
@media (min-width: 768px) {
  .hero { grid-template-columns: minmax(0, 1.05fr) minmax(0, 1fr); align-items: start; gap: 2.5rem; }
  /* 釘在固定 header（menu）之下：--header-sticky-top 由 header-sticky-offset plugin 動態量測 */
  .hero__media { position: sticky; top: var(--header-sticky-top, 1.5rem); }
  .hero__title { font-size: 2.1rem; }
  .panels { grid-template-columns: 1fr 1fr; gap: 1.75rem; }
}
</style>

<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/ProductDetail.cshtml.
// URL: /Product/{producttitle}  slug ↔ title via urlSlugToTitle().
const route = useRoute()
const slug = computed(() => String(route.params.producttitle ?? ''))
const title = computed(() => urlSlugToTitle(slug.value))

const { data } = await useProductDetailData(title.value)
const p = computed(() => data.value.product)
const ntd = (n: number) => 'NT. ' + new Intl.NumberFormat('en-US').format(Math.trunc(n))
const onSale = computed(() => p.value && p.value.fixprice > p.value.price)

// Cart wiring (legacy ProductDetail.cshtml relied on jQuery/inline scripts that weren't
// ported, so the quantity +/- and 加入購物車 controls were inert). Drive them via the
// pinia cart store instead.
const cartStore = useCartStore()
const memberAuth = useMemberAuthStore()
const config = useRuntimeConfig()
onMounted(() => {
  cartStore.hydrate()
  memberAuth.hydrate()
  refreshFavedState()
})

const qty = ref(1)
const decQty = () => { qty.value = Math.max(1, qty.value - 1) }
const incQty = () => { qty.value += 1 }
const onQtyInput = (e: Event) => {
  const n = Math.floor(Number((e.target as HTMLInputElement).value))
  qty.value = Number.isFinite(n) && n >= 1 ? n : 1
}

const justAdded = ref(false)
function addToCart() {
  const prod = p.value
  if (!prod || prod.added <= 0) return
  cartStore.add({
    productId: prod.productid,
    title: prod.title,
    unitPrice: prod.price,
    quantity: qty.value,
    // 代表圖 (representative image); fall back to the first gallery photo only if unset.
    photo: prod.photo || prod.photos?.[0]?.photo || undefined,
    capacity: prod.capacity ?? undefined,
  })
  // Brief pulse on the header cart badge (mirrors legacy `.addnumber.add-active`).
  justAdded.value = true
  setTimeout(() => { justAdded.value = false }, 1200)
}

// 收藏 (wishlist) wiring. Legacy ProductDetail.cshtml relied on jQuery `.mylistbtn.one('click')`
// (Views/Shared/_Scripts.cshtml) which wasn't ported, so the heart button was inert. Drive the
// new API instead: POST/DELETE /member/wishlist (TFoodies.Api.Functions MemberProfileController).
const authHeader = (): Record<string, string> =>
  memberAuth.accessToken ? { Authorization: `Bearer ${memberAuth.accessToken}` } : {}

const faved = ref(false)
const favoriting = ref(false)

// 加入收藏成功時的浮動通知，3 秒後自動消失。
const toast = ref('')
let toastTimer: ReturnType<typeof setTimeout> | undefined
function showToast(msg: string) {
  toast.value = msg
  if (toastTimer) clearTimeout(toastTimer)
  toastTimer = setTimeout(() => { toast.value = '' }, 3000)
}
onBeforeUnmount(() => { if (toastTimer) clearTimeout(toastTimer) })

// Reflect existing favourites so the heart loads in the right state (and click toggles correctly).
async function refreshFavedState() {
  const prod = p.value
  if (!prod || !memberAuth.isAuthenticated) { faved.value = false; return }
  try {
    const res = await $fetch<{ items: { productid: string }[] }>(
      `${config.public.apiBase}/member/wishlist`,
      { headers: authHeader() },
    )
    faved.value = !!res.items?.some((i) => i.productid === prod.productid)
  } catch {
    faved.value = false
  }
}

async function toggleFavorite() {
  const prod = p.value
  if (!prod || favoriting.value) return
  if (!memberAuth.isAuthenticated) {
    await navigateTo('/Member/Login')
    return
  }
  favoriting.value = true
  const next = !faved.value
  try {
    if (next) {
      await $fetch(`${config.public.apiBase}/member/wishlist`, {
        method: 'POST',
        headers: authHeader(),
        body: { productId: prod.productid },
      })
    } else {
      await $fetch(`${config.public.apiBase}/member/wishlist/${prod.productid}`, {
        method: 'DELETE',
        headers: authHeader(),
      })
    }
    faved.value = next
    showToast(next ? '成功加入收藏！' : '已取消收藏')
  } finally {
    favoriting.value = false
  }
}

const siteUrl = String(useRuntimeConfig().public.siteUrl).replace(/\/+$/, '')
const heroImage = computed(() => {
  const photo = p.value?.photos?.[0]?.photo ?? p.value?.photo
  return photo ? data.value.blobUrl + photo : undefined
})

useSeo(() => ({
  title: p.value?.title ?? title.value,
  description: p.value?.intro,
  image: heroImage.value,
  url: p.value?.shortener || undefined,
  type: 'product',
}))

useJsonLd(() => {
  if (!p.value) return null
  return [
    productJsonLd({
      name: p.value.title,
      description: p.value.intro,
      image: heroImage.value,
      url: p.value.shortener || `${siteUrl}/Product/${titleToUrlSlug(p.value.title)}`,
      price: p.value.price,
      inStock: p.value.added > 0,
      brand: p.value.brand?.title,
    }),
    breadcrumbJsonLd([
      { name: '所有產品', url: `${siteUrl}/Products` },
      ...(p.value.producttypetitle
        ? [{ name: p.value.producttypetitle, url: `${siteUrl}/Products/${encodeURIComponent(p.value.producttypetitle)}` }]
        : []),
      { name: p.value.title },
    ]),
  ]
})
</script>

<template>
  <main id="main">
    <section class="restrict-wide allpadding">
      <div class="locate">
        <p>
          <a href="/Products" class="descript">所有產品/</a>
          <a v-if="p?.producttypetitle" :href="`/Products/${encodeURIComponent(p.producttypetitle)}`" class="descript">{{ p.producttypetitle }}/</a>
          <a href="javascript:;" class="descript main">{{ p?.title }}</a>
        </p>
      </div>
    </section>

    <section v-if="p" class="allpadding clr section">
      <div class="restrict-wide">
        <div class="recipe-wrap">
          <div class="food productpart">
            <div class="slider-for">
              <div v-for="photo in p.photos" :key="photo.sort" class="product-display">
                <img :src="data.blobUrl + photo.photo" :alt="p.title">
              </div>
            </div>
            <div class="slider-nav">
              <div v-for="photo in p.photos" :key="photo.sort" class="product-thumb">
                <img :src="data.blobUrl + photo.photo" :alt="p.title">
              </div>
            </div>
          </div>

          <div class="food-desc">
            <h1>{{ p.title }}</h1>
            <h2>{{ p.entitle }}</h2>
            <p>{{ p.capacity }}</p>

            <div class="sale">
              <template v-if="onSale">
                <div class="line-through">原價 {{ ntd(p.fixprice) }}</div>
                <div class="centered">
                  <div class="sale-tag">特價</div>
                </div>
                <div class="product-price">{{ ntd(p.price) }}</div>
              </template>
              <template v-else>
                <div class="product-price">{{ ntd(p.price) }}</div>
              </template>
            </div>

            <div class="product-number">
              <div class="numb"><p>數量</p></div>
              <div class="quantity-wrap">
                <div class="quantity">
                  <div class="qty-minus"><a href="javascript:;" class="minus" @click.prevent="decQty">-</a></div>
                  <div class="qtyinput"><input type="text" name="qty" id="qty" inputmode="numeric" :value="qty" class="input form-input qty" @input="onQtyInput"></div>
                  <div class="qty-plus"><a href="javascript:;" class="plus" @click.prevent="incQty">+</a></div>
                </div>
              </div>
            </div>

            <div class="buybtn-wrap">
              <a v-if="p.added > 0" href="javascript:;" class="btn outline-btn solidhover js-add-cart" :data-productid="p.productid" :data-title="p.title" @click.prevent="addToCart">{{ justAdded ? '已加入購物車' : '加入購物車' }}</a>
              <a v-else href="javascript:;" class="btn outline-btn solidhover popup-contact" :data-productid="p.productid">到貨通知我</a>
              <a
                href="javascript:;"
                class="btn outline-btn solidhover mylistbtn"
                :class="{ 'is-faved': faved, 'is-busy': favoriting }"
                :data-productid="p.productid"
                :title="faved ? '取消收藏' : '加入收藏'"
                @click.prevent="toggleFavorite"
              >
                <div class="love"></div>
              </a>
            </div>

            <div class="horizon-line"></div>
            <div class="product-desc">
              <h2 class="main left">商品介紹</h2>
              <ul><li v-html="(p.intro ?? '').replace(/\n/g, '<br />')"></li></ul>
            </div>
            <div class="horizon-line"></div>
            <div class="product-desc">
              <h2 class="main left">付款方式</h2>
              <ul>
                <li>ATM轉帳付款</li>
                <li>宅配貨到付款</li>
                <li>信用卡線上付款</li>
              </ul>
            </div>
          </div>
        </div>

        <!-- RWD 版商品介紹（下方顯示） -->
        <div class="rwd-product-desc">
          <div class="product-desc">
            <h2 class="main left">商品介紹</h2>
            <p v-html="(p.intro ?? '').replace(/\n/g, '<br />')"></p>
          </div>
          <div class="horizon-line"></div>
          <div class="product-desc">
            <h2 class="main left">付款方式</h2>
            <ul>
              <li>ATM轉帳付款</li>
              <li>宅配貨到付款</li>
              <li>信用卡線上付款</li>
            </ul>
          </div>
        </div>

        <div class="horizon-line"></div>
        <section class="allsection" v-html="p.memo"></section>
        <div class="centered more"><a href="#wrapper" class="outline-btn">TOP</a></div>

        <template v-if="p.recipes.length">
          <div class="horizon-line"></div>
          <section class="allsection">
            <div class="restrict-wide">
              <h3 class="main left">適合料理</h3>
              <div class="responsive promoteSlider clr">
                <div v-for="recipe in p.recipes" :key="recipe.recipeid" class="promote-sliding product-single centered">
                  <div class="zoom-wrap">
                    <a :href="`/Recipe/${recipe.recipeid}/1`" class="blog-wrap">
                      <div class="article-single centered">
                        <div class="article-pic"><img :src="data.blobUrl + (recipe.rphoto ?? '')" :alt="recipe.title"></div>
                        <div class="article-title">{{ recipe.title }}</div>
                      </div>
                    </a>
                  </div>
                </div>
              </div>
            </div>
          </section>
          <div class="centered more"><a href="/Recipes" class="outline-btn">更多</a></div>
        </template>
      </div>
    </section>

    <!-- 品牌介紹區塊 -->
    <div
      v-if="p?.brand?.isdisplay === 1"
      :style="{ backgroundImage: `url(${data.blobUrl}${p.brand.storybgclass ?? ''})` }"
      class="allpadding clr allsection storybg productbrand"
    >
      <div class="restrict">
        <div class="centered">
          <h3>品牌介紹</h3>
          <div class="banner-title ci-title">{{ p.brand.title }}</div>
          <div class="content">
            <p v-html="(p.brand.intro ?? '').replace(/\n/g, '<br />')"></p>
            <a :href="`/Brand/${encodeURIComponent(p.brand.title)}`" class="btn yellow zindex-up">品牌介紹</a>
          </div>
        </div>
      </div>
    </div>
    <Transition name="fav-toast">
      <div v-if="toast" class="fav-toast" role="status" aria-live="polite">{{ toast }}</div>
    </Transition>
  </main>
</template>

<style scoped>
/* 已收藏狀態：沿用 .solidhover 的填色語意，讓愛心持續呈現選取樣式。 */
.mylistbtn.is-faved {
  background-color: #26b7bc;
  border-color: #26b7bc;
}

.mylistbtn.is-faved .love {
  filter: brightness(0) invert(1);
}

.mylistbtn.is-busy {
  pointer-events: none;
  opacity: 0.6;
}

/* 加入收藏的浮動通知 */
.fav-toast {
  position: fixed;
  left: 50%;
  bottom: 2.5rem;
  transform: translateX(-50%);
  z-index: 1000;
  padding: 0.75rem 1.5rem;
  font-size: 0.95rem;
  letter-spacing: 0.05em;
  color: #fff;
  background: rgba(38, 183, 188, 0.96);
  border-radius: 999px;
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.18);
}

.fav-toast-enter-active,
.fav-toast-leave-active {
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.fav-toast-enter-from,
.fav-toast-leave-to {
  opacity: 0;
  transform: translate(-50%, 0.75rem);
}
</style>

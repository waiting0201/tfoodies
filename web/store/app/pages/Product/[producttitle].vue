<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/ProductDetail.cshtml.
// URL: /Product/{producttitle}  slug ↔ title via urlSlugToTitle().
const route = useRoute()
const slug = computed(() => String(route.params.producttitle ?? ''))
const title = computed(() => urlSlugToTitle(slug.value))

const { data } = await useProductDetailData(title.value)
// 下架(isdisabled)或不存在的商品：API 回 null。直接回 404，避免下架商品仍留有可瀏覽的空殼頁面，
// 也讓搜尋引擎據此下架該網址（對齊舊系統 ProductDetail「查無導回」的精神，但用正確的 not-found 狀態）。
if (!data.value.product) {
  throw createError({ statusCode: 404, statusMessage: '商品不存在或已下架', fatal: true })
}
const p = computed(() => data.value.product)
const ntd = (n: number) => 'NT. ' + new Intl.NumberFormat('en-US').format(Math.trunc(n))
const onSale = computed(() => p.value && p.value.fixprice > p.value.price)

// 自製圖庫（取代舊版 slick .slider-for/.slider-nav）。商品相簿常混入橫式封面與直式手機照，
// 固定比例框 + object-fit:contain 讓各種比例都看起來一致，也根治了 slick fade 模式高度取最高張造成的空白跑版。
const galleryPhotos = computed(() => {
  const list = p.value?.photos?.length
    ? [...p.value.photos].sort((a, b) => a.sort - b.sort)
    : p.value?.photo
      ? [{ sort: 0, photo: p.value.photo }]
      : []
  return list.map((ph) => data.value.blobUrl + ph.photo)
})
const activePhoto = ref(0)
const mainPhoto = computed(() => galleryPhotos.value[activePhoto.value] ?? galleryPhotos.value[0])
function selectPhoto(i: number) { activePhoto.value = i }
function stepPhoto(d: number) {
  const n = galleryPhotos.value.length
  if (n) activePhoto.value = (activePhoto.value + d + n) % n
}

// 商品介紹文字以 ◎/● 等符號逐行條列；拆成乾淨的清單項目（去掉行首符號）。
const introLines = computed(() =>
  (p.value?.intro ?? '')
    .split(/\r?\n/)
    .map((s) => s.trim())
    .filter(Boolean)
    .map((s) => s.replace(/^[◎●•・*\-]\s*/, '')),
)
const inStock = computed(() => (p.value?.added ?? 0) > 0)

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
  // 漏斗第二關：瀏覽商品。
  const prod = p.value
  if (prod) {
    track('view_item', {
      ecommerce: {
        currency: 'TWD',
        value: prod.price,
        items: [{ item_id: prod.productid, item_name: prod.title, price: prod.price, quantity: 1 }],
      },
    })
  }
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

// 缺貨「到貨通知我」登記彈窗（對齊舊系統 _Footer.cshtml #checkOutofnotice + Ajax/PostOutofnotice）。
// reCAPTCHA v3（隱形）→ POST /store/outofnotices。舊系統的圖形驗證碼以 v3 取代，故無驗證碼輸入框。
const { execute: execRecaptcha } = useRecaptcha()
const noticeOpen = ref(false)
const noticeSubmitting = ref(false)
const noticeError = ref('')
const noticeForm = reactive({ name: '', email: '', mobile: '' })

function openNotice() {
  noticeError.value = ''
  // 登入會員預填姓名（store 僅保存姓名，Email/電話請使用者自行填寫）。
  noticeForm.name = memberAuth.memberName || ''
  noticeForm.email = ''
  noticeForm.mobile = ''
  noticeOpen.value = true
}
function closeNotice() { noticeOpen.value = false }

async function submitNotice() {
  noticeError.value = ''
  const prod = p.value
  if (!prod) return
  if (!noticeForm.name.trim()) { noticeError.value = '請填寫姓名。'; return }
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(noticeForm.email.trim())) { noticeError.value = 'Email 格式不正確。'; return }
  if (!noticeForm.mobile.trim()) { noticeError.value = '請填寫電話。'; return }

  noticeSubmitting.value = true
  try {
    const captchaToken = await execRecaptcha('outofnotice')
    await $fetch(`${config.public.apiBase}/store/outofnotices`, {
      method: 'POST',
      // API 以 camelCase 反序列化（大小寫敏感）。
      body: {
        productId: prod.productid,
        name: noticeForm.name.trim(),
        email: noticeForm.email.trim(),
        mobile: noticeForm.mobile.trim(),
        captchaToken,
      },
    })
    noticeOpen.value = false
    showToast('感謝您，到貨時將通知您！')
  } catch (e: unknown) {
    const msg = (e as { data?: { message?: string } })?.data?.message
    noticeError.value = msg || '送出失敗，請稍後再試。'
  } finally {
    noticeSubmitting.value = false
  }
}

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
  <main id="main" class="pd">
    <div class="pd-container">
      <!-- 麵包屑 -->
      <nav class="pd-crumb" aria-label="麵包屑">
        <a href="/Products">所有產品</a>
        <span class="pd-crumb-sep">/</span>
        <a v-if="p?.producttypetitle" :href="`/Products/${encodeURIComponent(p.producttypetitle)}`">{{ p.producttypetitle }}</a>
        <span v-if="p?.producttypetitle" class="pd-crumb-sep">/</span>
        <span class="pd-crumb-current">{{ p?.title }}</span>
      </nav>

      <div v-if="p" class="pd-top">
        <!-- 圖庫 -->
        <div class="pd-gallery">
          <div class="pd-stage">
            <span v-if="onSale" class="pd-flag">特價</span>
            <img v-if="mainPhoto" :src="mainPhoto" :alt="p.title" class="pd-stage-img">
            <template v-if="galleryPhotos.length > 1">
              <button type="button" class="pd-nav pd-nav-prev" aria-label="上一張" @click="stepPhoto(-1)">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M15 5l-7 7 7 7" /></svg>
              </button>
              <button type="button" class="pd-nav pd-nav-next" aria-label="下一張" @click="stepPhoto(1)">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 5l7 7-7 7" /></svg>
              </button>
            </template>
          </div>
          <div v-if="galleryPhotos.length > 1" class="pd-thumbs">
            <button
              v-for="(src, i) in galleryPhotos"
              :key="i"
              type="button"
              class="pd-thumb"
              :class="{ 'is-active': i === activePhoto }"
              :aria-label="`檢視第 ${i + 1} 張圖`"
              @click="selectPhoto(i)"
            >
              <img :src="src" :alt="`${p.title} 圖 ${i + 1}`">
            </button>
          </div>
        </div>

        <!-- 購買卡 -->
        <aside class="pd-buy">
          <p v-if="p.brand?.title || p.producttypetitle" class="pd-eyebrow">
            <span v-if="p.brand?.title">{{ p.brand.title }}</span>
            <span v-if="p.brand?.title && p.producttypetitle" class="pd-eyebrow-dot">·</span>
            <span v-if="p.producttypetitle">{{ p.producttypetitle }}</span>
          </p>
          <h1 class="pd-title">{{ p.title }}</h1>
          <p v-if="p.entitle" class="pd-entitle">{{ p.entitle }}</p>

          <div v-if="p.capacity || p.isset" class="pd-meta">
            <span v-if="p.capacity" class="pd-chip">{{ p.capacity }}</span>
            <span v-if="p.isset" class="pd-chip pd-chip-set">禮盒組</span>
          </div>

          <div class="pd-price">
            <span v-if="onSale" class="pd-was">原價 {{ ntd(p.fixprice) }}</span>
            <span class="pd-now" :class="{ 'is-sale': onSale }">{{ ntd(p.price) }}</span>
          </div>

          <p class="pd-stock" :class="inStock ? 'is-in' : 'is-out'">
            <span class="pd-stock-dot"></span>{{ inStock ? '現貨供應中' : '目前缺貨' }}
          </p>

          <div class="pd-actions">
            <div v-if="inStock" class="pd-qty">
              <button type="button" class="pd-qty-btn" aria-label="減少數量" @click="decQty">−</button>
              <input type="text" inputmode="numeric" :value="qty" class="pd-qty-input" aria-label="數量" @input="onQtyInput">
              <button type="button" class="pd-qty-btn" aria-label="增加數量" @click="incQty">+</button>
            </div>

            <button v-if="inStock" type="button" class="pd-btn pd-btn-primary js-add-cart" :data-productid="p.productid" :data-title="p.title" @click="addToCart">
              {{ justAdded ? '已加入購物車' : '加入購物車' }}
            </button>
            <button v-else type="button" class="pd-btn pd-btn-primary" :data-productid="p.productid" @click="openNotice">
              到貨通知我
            </button>

            <button
              type="button"
              class="pd-fav"
              :class="{ 'is-faved': faved, 'is-busy': favoriting }"
              :data-productid="p.productid"
              :aria-pressed="faved"
              :title="faved ? '取消收藏' : '加入最愛'"
              @click="toggleFavorite"
            >
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"/></svg>
            </button>
          </div>

          <ul class="pd-trust">
            <li>
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 6h11v9H3zM14 9h4l3 3v3h-7zM7 18a2 2 0 1 0 0-4 2 2 0 0 0 0 4zM18 18a2 2 0 1 0 0-4 2 2 0 0 0 0 4z"/></svg>
              <span>滿 NT.2,000 免運</span>
            </li>
            <li>
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 7l9-4 9 4v6c0 5-3.8 8-9 9-5.2-1-9-4-9-9z"/></svg>
              <span>宅配貨到付款</span>
            </li>
            <li>
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2 6h20v4H2zM2 12h20v6H2z"/></svg>
              <span>信用卡線上付款</span>
            </li>
          </ul>
        </aside>
      </div>

      <!-- 商品介紹 -->
      <section v-if="introLines.length" class="pd-section">
        <h2 class="pd-h">商品介紹</h2>
        <ul class="pd-intro">
          <li v-for="(line, i) in introLines" :key="i">{{ line }}</li>
        </ul>
      </section>

      <!-- 行銷圖文 -->
      <section v-if="p?.memo" class="pd-section pd-memo" v-html="p.memo"></section>

      <!-- 適合料理 -->
      <section v-if="p?.recipes.length" class="pd-section">
        <h2 class="pd-h">適合料理</h2>
        <div class="pd-recipes">
          <a v-for="recipe in p.recipes" :key="recipe.recipeid" :href="`/Recipe/${recipe.recipeid}/1`" class="pd-recipe">
            <div class="pd-recipe-pic"><img :src="data.blobUrl + (recipe.rphoto ?? '')" :alt="recipe.title"></div>
            <div class="pd-recipe-title">{{ recipe.title }}</div>
          </a>
        </div>
        <div class="pd-more"><a href="/Recipes" class="pd-btn pd-btn-ghost">看更多料理</a></div>
      </section>
    </div>

    <!-- 品牌介紹區塊（沿用原設計，main.css 既有樣式） -->
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

    <!-- 手機底部購買列 -->
    <div v-if="p" class="pd-bar">
      <div class="pd-bar-price">
        <span v-if="onSale" class="pd-bar-was">{{ ntd(p.fixprice) }}</span>
        <span class="pd-bar-now">{{ ntd(p.price) }}</span>
      </div>
      <button v-if="inStock" type="button" class="pd-btn pd-btn-primary pd-bar-btn js-add-cart" @click="addToCart">
        {{ justAdded ? '已加入 ✓' : '加入購物車' }}
      </button>
      <button v-else type="button" class="pd-btn pd-btn-primary pd-bar-btn" @click="openNotice">到貨通知我</button>
    </div>
    <!-- 缺貨「到貨通知我」登記彈窗（reCAPTCHA v3 隱形驗證，無圖形驗證碼輸入框）。 -->
    <Transition name="notice-fade">
      <div v-if="noticeOpen" class="notice-overlay" @click.self="closeNotice">
        <div class="notice-modal" role="dialog" aria-modal="true" aria-labelledby="noticeTitle">
          <a href="javascript:;" class="notice-close" aria-label="關閉" @click.prevent="closeNotice">×</a>
          <h3 id="noticeTitle" class="notice-title">到貨通知</h3>
          <p class="notice-sub">留下聯絡方式，商品到貨時我們會主動通知您。</p>
          <form class="notice-form" @submit.prevent="submitNotice">
            <label class="notice-field">
              <span class="notice-label"><i>*</i>姓名</span>
              <input v-model="noticeForm.name" type="text" class="input" maxlength="50" autocomplete="name">
            </label>
            <label class="notice-field">
              <span class="notice-label"><i>*</i>電子郵件</span>
              <input v-model="noticeForm.email" type="email" class="input" maxlength="150" autocomplete="email">
            </label>
            <label class="notice-field">
              <span class="notice-label"><i>*</i>電話</span>
              <input v-model="noticeForm.mobile" type="tel" class="input" maxlength="15" autocomplete="tel">
            </label>
            <p v-if="noticeError" class="notice-err">{{ noticeError }}</p>
            <button type="submit" class="btn basic notice-submit" :disabled="noticeSubmitting">
              {{ noticeSubmitting ? '送出中…' : '送出' }}
            </button>
          </form>
        </div>
      </div>
    </Transition>
    <Transition name="fav-toast">
      <div v-if="toast" class="fav-toast" role="status" aria-live="polite">{{ toast }}</div>
    </Transition>
  </main>
</template>

<style scoped>
/* ===== 商品詳細頁重新設計（品牌內現代化，全部 scoped；沿用青綠品牌色） ===== */
.pd {
  --teal: #26b7bc;
  --teal-dark: #156467;
  --teal-deep: #007382;
  --orange: #ea5520;
  --ink: #2f3a3a;
  --muted: #8a8a8a;
  --line: #e6ecec;
  --soft: #f6f9f9;
  --radius: 14px;
  color: var(--ink);
  background: #fff;
}

.pd-container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 1.25rem 1rem 5.5rem;
}

/* 麵包屑 */
.pd-crumb {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.35rem;
  margin-bottom: 1.25rem;
  font-size: 0.82rem;
  color: var(--muted);
}
.pd-crumb a { color: var(--muted); text-decoration: none; }
.pd-crumb a:hover { color: var(--teal-deep); }
.pd-crumb-sep { color: #cfdada; }
.pd-crumb-current { color: var(--ink); font-weight: 600; }

/* 上半：圖庫 + 購買卡 */
.pd-top { display: grid; gap: 1.75rem; }

/* 圖庫 */
.pd-stage {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  aspect-ratio: 4 / 3;
  overflow: hidden;
  background: var(--soft);
  border: 1px solid var(--line);
  border-radius: var(--radius);
}
.pd-stage-img {
  width: 100%;
  height: 100%;
  padding: 0.5rem;
  box-sizing: border-box;
  object-fit: contain;
}
.pd-flag {
  position: absolute;
  top: 0.9rem;
  left: 0.9rem;
  z-index: 2;
  padding: 0.3rem 0.7rem;
  font-size: 0.78rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  color: #fff;
  background: var(--orange);
  border-radius: 999px;
}
.pd-nav {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  padding: 0;
  border: none;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.92);
  cursor: pointer;
  box-shadow: 0 1px 6px rgba(21, 100, 103, 0.14);
  transition: background 0.15s;
}
.pd-nav svg { width: 16px; height: 16px; fill: none; stroke: var(--teal-dark); stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; }
.pd-nav:hover { background: #fff; }
.pd-nav:hover svg { stroke: var(--teal-deep); }
.pd-nav-prev { left: 0.6rem; }
.pd-nav-next { right: 0.6rem; }
.pd-thumbs {
  display: flex;
  gap: 0.6rem;
  margin-top: 0.8rem;
  padding-bottom: 0.25rem;
  overflow-x: auto;
}
.pd-thumb {
  flex: 0 0 auto;
  width: 72px;
  height: 72px;
  padding: 0;
  overflow: hidden;
  background: var(--soft);
  border: 2px solid var(--line);
  border-radius: 10px;
  cursor: pointer;
  transition: border-color 0.15s;
}
.pd-thumb img { display: block; width: 100%; height: 100%; object-fit: cover; }
.pd-thumb.is-active { border-color: var(--teal); }

/* 購買卡 */
.pd-eyebrow {
  margin: 0 0 0.55rem;
  font-size: 0.76rem;
  font-weight: 600;
  letter-spacing: 0.14em;
  color: var(--teal-deep);
}
.pd-eyebrow-dot { margin: 0 0.4rem; color: #b6cccc; }
.pd-title { margin: 0 0 0.35rem; font-size: 1.5rem; font-weight: 600; line-height: 1.35; letter-spacing: 0.01em; color: var(--ink); }
.pd-entitle { margin: 0 0 1rem; font-size: 0.9rem; font-style: italic; color: var(--muted); }
.pd-meta { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1.1rem; }
.pd-chip {
  padding: 0.3rem 0.8rem;
  font-size: 0.82rem;
  color: var(--teal-dark);
  background: var(--soft);
  border: 1px solid var(--line);
  border-radius: 999px;
}
.pd-chip-set { color: var(--orange); background: #fdf1ec; border-color: #f6d6c8; }
.pd-price {
  display: flex;
  align-items: baseline;
  flex-wrap: wrap;
  gap: 0.8rem;
  padding: 1.1rem 0;
  border-top: 1px solid var(--line);
  border-bottom: 1px solid var(--line);
}
.pd-was { font-size: 0.9rem; color: var(--muted); text-decoration: line-through; }
.pd-now { font-size: 1.65rem; font-weight: 700; letter-spacing: 0.01em; color: var(--ink); }
.pd-now.is-sale { color: var(--orange); }
.pd-stock {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 1rem 0 1.3rem;
  font-size: 0.88rem;
  font-weight: 600;
}
.pd-stock-dot { width: 8px; height: 8px; border-radius: 50%; }
.pd-stock.is-in { color: #3a8f4e; }
.pd-stock.is-in .pd-stock-dot { background: #3a8f4e; box-shadow: 0 0 0 3px rgba(58, 143, 78, 0.18); }
.pd-stock.is-out { color: #c0552e; }
.pd-stock.is-out .pd-stock-dot { background: #c0552e; box-shadow: 0 0 0 3px rgba(192, 85, 46, 0.18); }

/* 數量 + 加入購物車 + 加入最愛（同一列、等高、纖細）。
   統一控制高度 38px；數量框子元素以 height:100% 撐滿，避免 <input> 不隨容器拉伸而讓分隔線上下留白。 */
.pd-actions { display: flex; align-items: stretch; gap: 0.5rem; }
.pd-qty {
  display: inline-flex;
  flex: 0 0 auto;
  height: 38px;
  box-sizing: border-box;
  overflow: hidden;
  background: #fff;
  border: 1px solid var(--line);
  border-radius: 8px;
}
.pd-qty-btn {
  width: 34px;
  height: 100%;
  border: none;
  background: transparent;
  font-size: 0.95rem;
  font-weight: 400;
  color: var(--teal-dark);
  cursor: pointer;
  transition: background 0.15s;
}
.pd-qty-btn:hover { background: var(--soft); }
.pd-qty-input {
  width: 48px;
  min-width: 0;
  height: 100%;
  box-sizing: border-box;
  padding: 0 4px;
  border: none;
  border-right: 1px solid var(--line);
  border-left: 1px solid var(--line);
  font-size: 0.85rem;
  text-align: center;
  color: var(--ink);
  background: transparent;
  -moz-appearance: textfield;
}
.pd-qty-input:focus { outline: none; }
.pd-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 38px;
  padding: 0 1.2rem;
  font-size: 0.88rem;
  font-weight: 500;
  letter-spacing: 0.1em;
  border: 1px solid transparent;
  border-radius: 8px;
  cursor: pointer;
  transition: background 0.18s, color 0.18s, border-color 0.18s, transform 0.1s;
}
.pd-btn:active { transform: translateY(1px); }
.pd-btn-primary { flex: 1; color: #fff; background: var(--teal); border-color: var(--teal); }
.pd-btn-primary:hover { background: var(--teal-deep); border-color: var(--teal-deep); }
.pd-fav {
  flex: 0 0 38px;
  width: 38px;
  height: 38px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  line-height: 0;
  background: #fff;
  border: 1px solid var(--line);
  border-radius: 8px;
  cursor: pointer;
  transition: border-color 0.15s, background 0.15s;
}
.pd-fav svg { display: block; width: 18px; height: 18px; fill: none; stroke: var(--teal-dark); stroke-width: 1.5; }
.pd-fav:hover { border-color: var(--teal); }
.pd-fav.is-faved { background: var(--teal); border-color: var(--teal); }
.pd-fav.is-faved svg { fill: #fff; stroke: #fff; }
.pd-fav.is-busy { pointer-events: none; opacity: 0.6; }

/* 信任徽章（單欄堆疊，整齊對齊） */
.pd-trust {
  display: flex;
  flex-direction: column;
  gap: 0.7rem;
  margin: 1.4rem 0 0;
  padding: 1.2rem 0 0;
  list-style: none;
  border-top: 1px solid var(--line);
}
.pd-trust li { display: flex; align-items: center; gap: 0.6rem; font-size: 0.84rem; color: #607070; }
.pd-trust svg { flex: 0 0 18px; width: 18px; height: 18px; fill: var(--teal); }

/* 內容區段 */
.pd-section { margin-top: 3rem; }
.pd-h {
  margin: 0 0 1.2rem;
  padding-left: 0.7rem;
  font-size: 1.15rem;
  font-weight: 600;
  letter-spacing: 0.02em;
  line-height: 1.2;
  color: var(--ink);
  border-left: 3px solid var(--teal);
}
.pd-intro { display: grid; gap: 0.7rem; margin: 0; padding: 0; list-style: none; }
.pd-intro li { position: relative; padding-left: 1.4rem; font-size: 0.95rem; line-height: 1.7; color: #4a5656; }
.pd-intro li::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0.6em;
  width: 7px;
  height: 7px;
  background: var(--teal);
  border-radius: 50%;
}

/* 行銷圖文（後台 rich text，含寫死寬度的大圖 → 一律壓回容器寬度） */
.pd-memo { line-height: 1.8; color: #4a5656; }
.pd-memo :deep(img) { max-width: 100%; height: auto; margin: 0 auto; border-radius: 8px; }
.pd-memo :deep(.restrict-wide) { width: 100%; max-width: 100%; }
.pd-memo :deep(ul) { margin: 0; padding: 0; list-style: none; }
.pd-memo :deep(h1), .pd-memo :deep(h2) { text-align: center; color: var(--teal-dark); }

/* 適合料理 */
.pd-recipes { display: grid; grid-template-columns: repeat(2, 1fr); gap: 1.2rem; }
.pd-recipe { display: block; color: var(--ink); text-decoration: none; }
.pd-recipe-pic { aspect-ratio: 1 / 1; overflow: hidden; background: var(--soft); border-radius: 12px; }
.pd-recipe-pic img { display: block; width: 100%; height: 100%; object-fit: cover; transition: transform 0.35s; }
.pd-recipe:hover .pd-recipe-pic img { transform: scale(1.06); }
.pd-recipe-title { margin-top: 0.6rem; font-size: 0.92rem; line-height: 1.4; text-align: center; }
.pd-more { margin-top: 1.6rem; text-align: center; }
.pd-btn-ghost { color: var(--teal-deep); background: #fff; border-color: var(--teal); }
.pd-btn-ghost:hover { color: #fff; background: var(--teal); }

/* 手機底部購買列 */
.pd-bar {
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 900;
  display: flex;
  align-items: center;
  gap: 0.8rem;
  padding: 0.7rem 1rem calc(0.7rem + env(safe-area-inset-bottom));
  background: #fff;
  border-top: 1px solid var(--line);
  box-shadow: 0 -4px 20px rgba(0, 0, 0, 0.08);
}
.pd-bar-price { display: flex; flex-direction: column; line-height: 1.1; }
.pd-bar-was { font-size: 0.72rem; color: var(--muted); text-decoration: line-through; }
.pd-bar-now { font-size: 1.2rem; font-weight: 800; color: var(--orange); }
.pd-bar-btn { flex: 1; }

/* 平板以上 */
@media (min-width: 768px) {
  .pd-recipes { grid-template-columns: repeat(4, 1fr); }
}

/* 桌機：兩欄 + sticky 購買卡，隱藏手機底部列 */
@media (min-width: 992px) {
  .pd-container { padding: 2rem 1.5rem 4rem; }
  .pd-top { grid-template-columns: minmax(0, 1.1fr) minmax(360px, 0.9fr); gap: 3rem; align-items: start; }
  .pd-buy { position: sticky; top: 96px; }
  .pd-title { font-size: 1.75rem; }
  .pd-bar { display: none; }
}

@media (prefers-reduced-motion: reduce) {
  .pd-btn, .pd-recipe-pic img, .pd-nav { transition: none; }
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

/* 到貨通知彈窗 */
.notice-overlay {
  position: fixed;
  inset: 0;
  z-index: 1100;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
  background: rgba(0, 0, 0, 0.5);
}

.notice-modal {
  position: relative;
  width: 100%;
  max-width: 420px;
  padding: 2rem 1.75rem 1.75rem;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.25);
}

.notice-close {
  position: absolute;
  top: 0.5rem;
  right: 0.85rem;
  font-size: 1.6rem;
  line-height: 1;
  color: #9aa3a3;
  text-decoration: none;
}

.notice-title {
  margin: 0 0 0.35rem;
  font-size: 1.25rem;
  color: #156467;
}

.notice-sub {
  margin: 0 0 1.25rem;
  font-size: 0.85rem;
  color: #7a8585;
}

.notice-field {
  display: block;
  margin-bottom: 0.9rem;
}

.notice-label {
  display: block;
  margin-bottom: 0.3rem;
  font-size: 0.85rem;
  color: #2c3e3e;
}

.notice-label i {
  margin-right: 0.25rem;
  font-style: normal;
  color: #d9534f;
}

.notice-field .input {
  width: 100%;
  padding: 0.6rem 0.75rem;
  font-size: 0.95rem;
  border: 1px solid #d4dcdc;
  border-radius: 6px;
  box-sizing: border-box;
}

.notice-err {
  margin: 0 0 0.75rem;
  font-size: 0.85rem;
  color: #d9534f;
}

.notice-submit {
  width: 100%;
  margin-top: 0.25rem;
}

.notice-submit:disabled {
  opacity: 0.6;
  pointer-events: none;
}

.notice-fade-enter-active,
.notice-fade-leave-active {
  transition: opacity 0.25s ease;
}

.notice-fade-enter-from,
.notice-fade-leave-to {
  opacity: 0;
}
</style>
